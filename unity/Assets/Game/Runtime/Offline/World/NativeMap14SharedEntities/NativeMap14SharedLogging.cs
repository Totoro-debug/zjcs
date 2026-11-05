using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap14SharedEntities
{
    // Supplies the unchanged IL's logging ABI without initializing its online app.
    public sealed class NativeMap14SharedLogging : IDisposable
    {
        private static readonly Dictionary<object, NativeMap14SharedLogging> Owners = new Dictionary<object, NativeMap14SharedLogging>();
        private readonly PropertyInfo loggerSlot, factorySlot;
        private readonly object previousLogger, previousFactory;
        private readonly object logger, factory;
        private readonly Dictionary<string, object> categories = new Dictionary<string, object>();
        private readonly NativeMap14SharedLogging owner;
        private int leases;
        private bool disposed;

        public NativeMap14SharedLogging(NativeMap14SharedAssemblies shared, Action<int, string, Exception> write = null)
        {
            if (shared == null) throw new ArgumentNullException(nameof(shared));
            var log = shared.Load("Framework.FirstPass.Runtime").GetType("Framework.Logging.Log", true);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            loggerSlot = log.GetProperty("Logger", flags);
            factorySlot = log.GetProperty("LoggerFactory", flags);
            previousLogger = loggerSlot.GetValue(null);
            previousFactory = factorySlot.GetValue(null);
            if (previousLogger != null && previousFactory != null)
            {
                if (Owners.TryGetValue(previousFactory, out var existing) && ReferenceEquals(previousLogger, existing.logger))
                {
                    owner = existing;
                    owner.leases++;
                }
                return;
            }
            var sink = write ?? WriteUnity;
            object CreateLogger(string category)
            {
                if (categories.TryGetValue(category, out var existing)) return existing;
                var created = Abi.Create(loggerSlot.PropertyType, (method, args) =>
                {
                    switch (method)
                    {
                        case "IsEnabled": return Convert.ToInt32(args[0]) < 6;
                        case "BeginScope": return new Scope();
                        case "Log":
                            int level = Convert.ToInt32(args[0]);
                            if (level >= 6) return null;
                            var exception = (Exception)args[3];
                            string message = (string)((Delegate)args[4]).DynamicInvoke(args[2], exception);
                            sink(level, "[" + category + "] " + message, exception);
                            return null;
                        default: throw new MissingMethodException(loggerSlot.PropertyType.FullName, method);
                    }
                });
                categories.Add(category, created);
                return created;
            }
            factory = Abi.Create(factorySlot.PropertyType, (method, args) =>
            {
                switch (method)
                {
                    case "CreateLogger": return CreateLogger((string)args[0]);
                    case "Dispose": return null;
                    default: throw new NotSupportedException("The local map logger does not register external providers: " + method);
                }
            });
            logger = CreateLogger("MainlineMap");
            log.GetMethod("Initialize").Invoke(null, new[] { factory, logger });
            leases = 1;
            Owners.Add(factory, this);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (owner != null) { owner.Release(); return; }
            if (factory != null) Release();
        }

        private void Release()
        {
            if (--leases != 0) return;
            if (factory != null && ReferenceEquals(factorySlot.GetValue(null), factory)) factorySlot.SetValue(null, previousFactory);
            if (logger != null && ReferenceEquals(loggerSlot.GetValue(null), logger)) loggerSlot.SetValue(null, previousLogger);
            Owners.Remove(factory);
            categories.Clear();
        }

        private static void WriteUnity(int level, string message, Exception exception)
        {
            if (exception != null) message += "\n" + exception;
            if (level >= 4) Debug.LogError(message);
            else if (level == 3) Debug.LogWarning(message);
            else Debug.Log(message);
        }

        private sealed class Scope : IDisposable { public void Dispose() { } }

        private static class Abi
        {
            private static readonly Dictionary<Type, Type> Types = new Dictionary<Type, Type>();
            private static ModuleBuilder module;
            public static object Create(Type contract, Func<string, object[], object> callback)
            {
                if (!Types.TryGetValue(contract, out var type))
                {
                    if (!contract.IsInterface || (contract.FullName != "Microsoft.Extensions.Logging.ILogger"
                        && contract.FullName != "Microsoft.Extensions.Logging.ILoggerFactory"))
                        throw new NotSupportedException("The original logging ABI differs: " + contract);
                    if (module == null) module = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Zjcs.Map.SharedLoggingAbi"),
                        AssemblyBuilderAccess.Run).DefineDynamicModule("Logging");
                    var builder = module.DefineType("Logger" + Types.Count, TypeAttributes.Public | TypeAttributes.Sealed);
                    builder.AddInterfaceImplementation(contract);
                    var callbackType = typeof(Func<string, object[], object>);
                    var field = builder.DefineField("callback", callbackType, FieldAttributes.Private | FieldAttributes.InitOnly);
                    var constructor = builder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { callbackType });
                    var il = constructor.GetILGenerator();
                    il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));
                    il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Stfld, field); il.Emit(OpCodes.Ret);
                    foreach (var original in contract.GetMethods().Concat(contract.GetInterfaces().SelectMany(t => t.GetMethods())).Distinct())
                    {
                        var method = builder.DefineMethod(original.Name, MethodAttributes.Public | MethodAttributes.Virtual
                            | MethodAttributes.Final | MethodAttributes.NewSlot);
                        var sourceGenerics = original.GetGenericArguments();
                        var generics = sourceGenerics.Length == 0 ? Type.EmptyTypes
                            : method.DefineGenericParameters(sourceGenerics.Select(t => t.Name).ToArray()).Cast<Type>().ToArray();
                        Type Bind(Type value)
                        {
                            if (value.IsGenericParameter) return generics[Array.IndexOf(sourceGenerics, value)];
                            if (value.IsGenericType) return value.GetGenericTypeDefinition().MakeGenericType(value.GetGenericArguments().Select(Bind).ToArray());
                            return value;
                        }
                        var parameters = original.GetParameters().Select(p => Bind(p.ParameterType)).ToArray();
                        var result = Bind(original.ReturnType);
                        method.SetReturnType(result); method.SetParameters(parameters);
                        il = method.GetILGenerator();
                        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, field); il.Emit(OpCodes.Ldstr, original.Name);
                        il.Emit(OpCodes.Ldc_I4, parameters.Length); il.Emit(OpCodes.Newarr, typeof(object));
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            il.Emit(OpCodes.Dup); il.Emit(OpCodes.Ldc_I4, i); il.Emit(OpCodes.Ldarg, i + 1);
                            if (parameters[i].IsValueType || parameters[i].IsGenericParameter) il.Emit(OpCodes.Box, parameters[i]);
                            il.Emit(OpCodes.Stelem_Ref);
                        }
                        il.Emit(OpCodes.Callvirt, callbackType.GetMethod("Invoke"));
                        if (result == typeof(void)) il.Emit(OpCodes.Pop);
                        else il.Emit(result.IsValueType || result.IsGenericParameter ? OpCodes.Unbox_Any : OpCodes.Castclass, result);
                        il.Emit(OpCodes.Ret);
                        builder.DefineMethodOverride(method, original);
                    }
                    type = builder.CreateType(); Types.Add(contract, type);
                }
                return Activator.CreateInstance(type, callback);
            }
        }
    }
}
