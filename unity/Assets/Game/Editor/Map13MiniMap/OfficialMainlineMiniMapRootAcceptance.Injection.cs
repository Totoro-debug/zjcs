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
            Require(catalog.Licenses != null && catalog.Licenses.Length == 19 &&
                catalog.Licenses.All(license => license && license.bytes.Length != 0),
                "The local Resources catalog retains all 19 publisher notices in the Player dependency closure");
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
                VerifyOriginalInstanceInjector(shared, core, entry);
                var selector = core.GetType("Framework.DependencyInjection.DryIoc.InjectAttributeSelector", true);
                var selectProperties = selector.GetMethod("Properties");
                Require(selectProperties != null && selectProperties.GetParameters().Length == 1 &&
                    Convert.ToInt32(selectProperties.GetParameters()[0].DefaultValue) == 2 && core.GetTypes().Length == 7,
                    "The recorded Core port retains the five inspected original types and two selector closures");
            }
            Debug.Log("MAINLINE_SHARED_INJECTION_PASS: original provider and DryIoc injector ABI, target/null/exception identity, original MeManager construction and scope restoration. Resolver receiver is a fixture; offline services and player startup are pending.");
        }

        private static void VerifyOriginalInstanceInjector(NativeMap14SharedAssemblies shared, Assembly core, MethodInfo entry)
        {
            var load = typeof(NativeMap14SharedAssemblies).GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Instance);
            var dry = (Assembly)load.Invoke(shared, new object[] { "DryIoc" });
            Require(dry.FullName == "DryIoc, Version=5.4.3.0, Culture=neutral, PublicKeyToken=dfbf2bd50fcf7768",
                "Original client DryIoc strong identity resolves to the pinned public package");
            var contextType = dry.GetType("DryIoc.IResolverContext", true);
            object received = null; string[] members = new[] { "unmodified" }; int calls = 0;
            var context = CreateResolverReceiver(contextType, (target, names) => { received = target; members = names; calls++; });
            var injectorType = core.GetType("Framework.DependencyInjection.DryIoc.DryIocInstanceInjector", true);
            var injector = Activator.CreateInstance(injectorType, context);
            var field = injectorType.GetField("mContext", BindingFlags.NonPublic | BindingFlags.Instance);
            Require(field.IsInitOnly && ReferenceEquals(field.GetValue(injector), context),
                "Original constructor retains the same resolver in its readonly field");
            var provider = new OriginalInjectionProvider { Service = injector }; var targetObject = new object();
            entry.Invoke(null, new object[] { provider, targetObject });
            Require(calls == 1 && ReferenceEquals(received, targetObject) && members == null,
                "Original injector forwards the same target and original null member list through the public DryIoc call");
            entry.Invoke(null, new object[] { provider, null });
            Require(calls == 2 && received == null && members == null, "Original injector preserves a null target");
            var expected = new InvalidOperationException("Resolver injection failure");
            provider.Service = Activator.CreateInstance(injectorType, CreateResolverReceiver(contextType, (target, names) => throw expected));
            Require(ReferenceEquals(InjectionFailure(entry, provider, targetObject), expected), "Original resolver failure propagates unchanged");
            provider.Service = Activator.CreateInstance(injectorType, new object[] { null });
            Require(field.GetValue(provider.Service) == null && InjectionFailure(entry, provider, targetObject) is NullReferenceException,
                "Original constructor adds no resolver null guard");

            var framework = (Assembly)load.Invoke(shared, new object[] { "Framework.FirstPass.Runtime" });
            var current = framework.GetType("Framework.DependencyInjection.ApplicationScope", true).GetProperty("Current");
            var previous = current.GetValue(null);
            try
            {
                calls = 0; provider.Service = injector; current.SetValue(null, provider);
                // Construct the unchanged original manager, without touching its
                // readonly singleton, creating a player or bypassing ClientInit.
                var managerType = shared.Shared.GetType("MeManager", true);
                var manager = Activator.CreateInstance(managerType);
                Require(calls == 1 && ReferenceEquals(received, manager) && members == null,
                    "The unchanged original manager constructor reaches its real recovered injector");
                Require(managerType.GetField("GameData").GetValue(manager) != null &&
                    managerType.GetProperty("MapData").GetValue(manager) != null &&
                    managerType.GetField("RoomData").GetValue(manager) != null &&
                    managerType.GetProperty("Player").GetValue(manager) == null,
                    "Original field initializers create game/map/room state while player startup remains separate");
            }
            finally { current.SetValue(null, previous); }
            Require(ReferenceEquals(current.GetValue(null), previous), "Original application scope restores its previous provider");
        }

        private static object CreateResolverReceiver(Type contract, Action<object, string[]> callback)
        {
            // Only this fixture supplies a resolver receiver. Production member
            // selection, registration and offline client services remain separate.
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Zjcs.ResolverFixture." + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
            var builder = assembly.DefineDynamicModule("Resolver").DefineType("Resolver", TypeAttributes.Public | TypeAttributes.Sealed);
            builder.AddInterfaceImplementation(contract); builder.DefineDefaultConstructor(MethodAttributes.Public);
            var field = builder.DefineField("Callback", typeof(Action<object, string[]>), FieldAttributes.Public);
            var methods = new[] { contract }.Concat(contract.GetInterfaces()).SelectMany(type => type.GetMethods()).Distinct()
                .GroupBy(method => method.ToString());
            foreach (var group in methods)
            {
                var original = group.First();
                Require(!original.ContainsGenericParameters, "Pinned resolver fixture methods have no open generic parameters");
                var parameters = original.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
                var method = builder.DefineMethod(original.Name, MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final |
                    MethodAttributes.NewSlot | MethodAttributes.HideBySig, original.ReturnType, parameters);
                var il = method.GetILGenerator();
                if (original.Name == "InjectPropertiesAndFields" && parameters.SequenceEqual(new[] { typeof(object), typeof(string[]) }))
                {
                    il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, field); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldarg_2);
                    il.Emit(OpCodes.Callvirt, typeof(Action<object, string[]>).GetMethod("Invoke")); il.Emit(OpCodes.Ret);
                }
                else
                {
                    il.Emit(OpCodes.Ldstr, "Unexpected fixture resolver call: " + original.Name);
                    il.Emit(OpCodes.Newobj, typeof(NotSupportedException).GetConstructor(new[] { typeof(string) })); il.Emit(OpCodes.Throw);
                }
                foreach (var implemented in group) builder.DefineMethodOverride(method, implemented);
            }
            var type = builder.CreateType(); var instance = Activator.CreateInstance(type);
            type.GetField("Callback").SetValue(instance, callback); return instance;
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
