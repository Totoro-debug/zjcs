using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Zjcs.Offline.World.NativeMap14SharedEntities
{
    // Implements only the original application receiver ABI. Registration,
    // snapshot iteration and removal remain in the unchanged shared delegate.
    public sealed class NativeMap14SharedEventSubscription : IDisposable
    {
        private static readonly Dictionary<Type, Type> Proxies = new Dictionary<Type, Type>();
        private static ModuleBuilder module;
        private object dispatcher;
        private object receiver;
        private readonly MethodInfo subtract;

        public NativeMap14SharedEventSubscription(object dispatcher, Type receiverType, Action<object[]> callback)
        {
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (receiverType.FullName != "Common.IGridTransformPosUpdatedEventReceiver" &&
                receiverType.FullName != "Common.IEcEntityWorldStageComponentStateUpdateEventReceiver" &&
                receiverType.FullName != "Common.IEcContainerStartEventReceiver" &&
                receiverType.FullName != "Common.IEcContainerStartEntityEventReceiver" &&
                receiverType.FullName != "Common.IEcContainerRemoveEntityEventReceiver" &&
                receiverType.FullName != "Common.IEcContainerDestroyEntityEventReceiver" &&
                receiverType.FullName != "Common.IEcContainerPrepareReplaceEntityEventReceiver" &&
                receiverType.FullName != "Common.IEcContainerReplaceEntityEventReceiver")
                throw new NotSupportedException("Original application receiver is not bound: " + receiverType.FullName);
            receiver = Activator.CreateInstance(Proxy(receiverType), callback);
            var add = Operator(dispatcher.GetType(), "op_Addition", receiverType);
            subtract = Operator(dispatcher.GetType(), "op_Subtraction", receiverType);
            add.Invoke(null, new[] { dispatcher, receiver });
        }

        public void Dispose()
        {
            if (dispatcher == null) return;
            subtract.Invoke(null, new[] { dispatcher, receiver });
            dispatcher = null;
            receiver = null;
        }

        private static MethodInfo Operator(Type type, string name, Type receiverType)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var method = current.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly,
                    null, new[] { current, receiverType }, null);
                if (method != null) return method;
            }
            throw new MissingMethodException(type.FullName, name);
        }

        private static Type Proxy(Type contract)
        {
            if (Proxies.TryGetValue(contract, out var existing)) return existing;
            var methods = contract.GetMethods().Concat(contract.GetInterfaces().SelectMany(t => t.GetMethods())).ToArray();
            if (!contract.IsInterface || methods.Length != 1 || methods[0].ReturnType != typeof(void) ||
                methods[0].GetParameters().Any(p => p.ParameterType.IsByRef))
                throw new NotSupportedException("Original application receiver signature differs.");
            if (module == null)
                module = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Zjcs.Map14.SharedReceiverAbi"),
                    AssemblyBuilderAccess.Run).DefineDynamicModule("Receivers");
            var builder = module.DefineType("Receiver" + Proxies.Count, TypeAttributes.Public | TypeAttributes.Sealed);
            builder.AddInterfaceImplementation(contract);
            var callback = builder.DefineField("callback", typeof(Action<object[]>), FieldAttributes.Private | FieldAttributes.InitOnly);
            var ctor = builder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new[] { typeof(Action<object[]>) });
            var il = ctor.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes));
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, callback);
            il.Emit(OpCodes.Ret);
            var original = methods[0];
            var parameters = original.GetParameters().Select(p => p.ParameterType).ToArray();
            var method = builder.DefineMethod(original.Name, MethodAttributes.Public | MethodAttributes.Virtual |
                MethodAttributes.Final | MethodAttributes.NewSlot, typeof(void), parameters);
            il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, callback);
            il.Emit(OpCodes.Ldc_I4, parameters.Length);
            il.Emit(OpCodes.Newarr, typeof(object));
            for (var i = 0; i < parameters.Length; i++)
            {
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldarg, i + 1);
                if (parameters[i].IsValueType) il.Emit(OpCodes.Box, parameters[i]);
                il.Emit(OpCodes.Stelem_Ref);
            }
            il.Emit(OpCodes.Callvirt, typeof(Action<object[]>).GetMethod("Invoke"));
            il.Emit(OpCodes.Ret);
            builder.DefineMethodOverride(method, original);
            var result = builder.CreateType();
            Proxies.Add(contract, result);
            return result;
        }
    }
}
