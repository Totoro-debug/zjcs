using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Editor.Map13MiniMap
{
    public static partial class OfficialMainlineMiniMapRootAcceptance
    {
        private sealed class OriginalInjectionProvider : IServiceProvider
        {
            public object Service;
            public readonly List<Type> Requests = new List<Type>();
            public object GetService(Type serviceType) { Requests.Add(serviceType); return Service; }
        }

        private static void VerifySharedInjection()
        {
            var catalog = Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            using (var shared = new NativeMap14SharedAssemblies(catalog))
            {
                // Cross the same verified loader seam as the original caller.
                var load = typeof(NativeMap14SharedAssemblies).GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Instance);
                var core = (Assembly)load.Invoke(shared, new object[] { "Framework.Core" });
                var contract = core.GetType("Framework.DependencyInjection.Abstractions.IInstanceInjector", true);
                var marker = core.GetType("Framework.DependencyInjection.InjectAttribute", true);
                var entry = core.GetType("Framework.DependencyInjection.ServiceProviderExtensions", true).GetMethod("Inject");
                var usage = marker.GetCustomAttribute<AttributeUsageAttribute>();
                Require(usage.ValidOn == AttributeTargets.Property && !usage.AllowMultiple && usage.Inherited,
                    "Original Inject marker remains an inherited single property attribute");
                Require(marker.GetConstructors().Length == 1 && Activator.CreateInstance(marker) is Attribute,
                    "Original marker constructor calls the managed Attribute base");
                var requests = new OriginalInjectionProvider();
                object target = new object(), received = null;
                int calls = 0;
                var receiver = CreateInjectionReceiver(contract, value => { received = value; calls++; });
                requests.Service = receiver;
                entry.Invoke(null, new object[] { requests, target });
                Require(calls == 1 && ReferenceEquals(received, target) && requests.Requests.SequenceEqual(new[] { contract }),
                    "Original static injection requests its exact interface once and forwards the same target");
                entry.Invoke(null, new object[] { requests, null });
                Require(calls == 2 && received == null, "Original injection does not replace a null target");
                var expected = new InvalidOperationException("Injector failure");
                requests.Service = CreateInjectionReceiver(contract, value => throw expected);
                Require(ReferenceEquals(InjectionFailure(entry, requests, target), expected),
                    "Original injection preserves the injector exception object");
                requests.Service = null;
                Require(InjectionFailure(entry, requests, target) is NullReferenceException,
                    "Original GetService and virtual call retain the missing-injector failure");
                requests.Service = new object();
                Require(InjectionFailure(entry, requests, target) is InvalidCastException,
                    "Original generic GetService rejects a service with the wrong interface");
                var missingProvider = InjectionFailure(entry, null, target) as ArgumentNullException;
                Require(missingProvider != null && missingProvider.ParamName == "provider",
                    "Original generic GetService retains its provider null guard");
                var manager = shared.Shared.GetType("MeManager", true);
                var properties = manager.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(property => property.GetCustomAttributes(marker, true).Length != 0).ToArray();
                Require(properties.Select(property => property.PropertyType.Name).OrderBy(name => name)
                    .SequenceEqual(new[] { "IConnectManager", "IVersionManager" }),
                    "The shipped original MeManager properties resolve the recovered marker identity");
                var version = properties.Single(property => property.PropertyType.Name == "IVersionManager").PropertyType;
                Require(version.IsInterface && version.Assembly.GetName().Name == "Launch" &&
                    version.GetMethods().Select(method => method.Name).OrderBy(name => name).SequenceEqual(
                        new[] { "Reload", "get_CodeVersion", "get_ResVersion", "get_Version", "set_CodeVersion", "set_ResVersion" }.OrderBy(name => name)),
                    "Original MeManager resolves the complete version interface in its original assembly");
                Require(core.GetTypes().Length == 3, "The recorded Core port contains only the three inspected original types");
            }
            Debug.Log("MAINLINE_SHARED_INJECTION_PASS: original service identity, target/null forwarding, exception identity, provider/service failures and original MeManager attribute binding. Full client context is pending.");
        }

        private static Exception InjectionFailure(MethodInfo entry, object provider, object target)
        {
            try { entry.Invoke(null, new[] { provider, target }); }
            catch (TargetInvocationException error) { return error.InnerException; }
            throw new InvalidOperationException("Original injection unexpectedly succeeded");
        }

        private static object CreateInjectionReceiver(Type contract, Action<object> callback)
        {
            // This receiver is only a fixture. Production service implementations
            // must come from the offline client context.
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Zjcs.InjectionFixture." + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
            var builder = assembly.DefineDynamicModule("Receiver").DefineType("Receiver", TypeAttributes.Public | TypeAttributes.Sealed);
            builder.AddInterfaceImplementation(contract); builder.DefineDefaultConstructor(MethodAttributes.Public);
            var field = builder.DefineField("Callback", typeof(Action<object>), FieldAttributes.Public);
            var method = builder.DefineMethod("Inject", MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot,
                typeof(void), new[] { typeof(object) });
            var il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, field); il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Callvirt, typeof(Action<object>).GetMethod("Invoke")); il.Emit(OpCodes.Ret);
            builder.DefineMethodOverride(method, contract.GetMethod("Inject"));
            var type = builder.CreateType(); var instance = Activator.CreateInstance(type);
            type.GetField("Callback").SetValue(instance, callback); return instance;
        }
    }
}
