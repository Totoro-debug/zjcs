using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Editor.Map13MiniMap
{
    public static partial class OfficialMainlineMiniMapRootAcceptance
    {
        private static void VerifySharedLogging()
        {
            var catalog = Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            using (var shared = new NativeMap14SharedAssemblies(catalog))
            {
                var log = Assembly.Load("Framework.FirstPass.Runtime").GetType("Framework.Logging.Log", true);
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
                var loggerSlot = log.GetProperty("Logger", flags);
                var factorySlot = log.GetProperty("LoggerFactory", flags);
                object originalLogger = loggerSlot.GetValue(null), originalFactory = factorySlot.GetValue(null);
                var messages = new List<(int level, string text, Exception exception)>();
                var expected = new InvalidOperationException("Original receiver failure");
                try
                {
                    // Explicit diagnostics fixture: exercise ownership even if a
                    // surrounding Editor session already installed a logger.
                    loggerSlot.SetValue(null, null); factorySlot.SetValue(null, null);
                    using (var scope = new NativeMap14SharedLogging(shared, (level, text, exception) => messages.Add((level, text, exception))))
                    {
                        var factory = factorySlot.GetValue(null); var fallback = loggerSlot.GetValue(null);
                        Require(factory != null && fallback != null, "Original logging globals are installed together");
                        object Create(string category) => factorySlot.PropertyType.GetMethod("CreateLogger").Invoke(factory, new object[] { category });
                        var logger = Create("Lifecycle"); var contract = loggerSlot.PropertyType;
                        Require(ReferenceEquals(logger, Create("Lifecycle")), "Original logger categories are reused");
                        var enabled = contract.GetMethod("IsEnabled"); var levelType = enabled.GetParameters()[0].ParameterType;
                        Require((bool)enabled.Invoke(logger, new[] { Enum.ToObject(levelType, 4) })
                            && !(bool)enabled.Invoke(logger, new[] { Enum.ToObject(levelType, 6) }), "Error is enabled and None is disabled");
                        ((IDisposable)contract.GetMethod("BeginScope").MakeGenericMethod(typeof(string))
                            .Invoke(logger, new object[] { "scope" })).Dispose();
                        var emit = contract.GetMethod("Log").MakeGenericMethod(typeof(int));
                        var eventType = emit.GetParameters()[1].ParameterType;
                        Func<int, Exception, string> formatter = (value, exception) => "formatted " + value;
                        emit.Invoke(logger, new object[] { Enum.ToObject(levelType, 4), Activator.CreateInstance(eventType), 42, expected, formatter });
                        Require(messages.Count == 1 && messages[0].level == 4 && messages[0].text == "[Lifecycle] formatted 42"
                            && ReferenceEquals(messages[0].exception, expected), "Original generic value state, formatter and exception are preserved");
                        var generic = log.GetMethods().Single(m => m.Name == "CreateLogger" && m.IsGenericMethodDefinition);
                        Require(generic.MakeGenericMethod(shared.CommonType("EcContainer")).Invoke(null, null) != null,
                            "Original typed logger wrapper uses the same local factory");
                        using (var borrowed = new NativeMap14SharedLogging(shared))
                        using (var world = new MainlineSharedWorld(catalog, 10, 4000))
                            Require(ReferenceEquals(factorySlot.GetValue(null), factory) && ReferenceEquals(loggerSlot.GetValue(null), fallback),
                                "Nested map scope borrows the installed logging owner");
                        Require(ReferenceEquals(factorySlot.GetValue(null), factory) && ReferenceEquals(loggerSlot.GetValue(null), fallback),
                            "Disposing borrowed map scopes preserves the owner's logger");
                        var container = Activator.CreateInstance(shared.CommonType("EcContainer"));
                        var dispatcher = NativeMap14SharedPrefabs.Get(container, "StartEvent");
                        using (var receiver = new NativeMap14SharedEventSubscription(dispatcher,
                            shared.CommonType("IEcContainerStartEventReceiver"), args => throw expected))
                        {
                            try { dispatcher.GetType().GetMethod("FireEvent").Invoke(dispatcher, null); }
                            catch (Exception error)
                            {
                                while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException;
                                // Unity Mono cannot print signatures containing
                                // unsupported function pointers in dependency errors.
                                throw new InvalidOperationException("Original logging dispatch failed: " + error.GetType().FullName + ": " + error.Message);
                            }
                        }
                        Require(messages.Skip(1).Any(row => row.level == 4 && ReferenceEquals(row.exception, expected)),
                            "Unchanged original event dispatcher reports its receiver exception without a logging null reference");
                    }
                    Require(loggerSlot.GetValue(null) == null && factorySlot.GetValue(null) == null,
                        "Map logging scope restores both previous global bindings");
                    var owner = new NativeMap14SharedLogging(shared, (level, text, exception) => messages.Add((level, text, exception)));
                    var overlapping = new NativeMap14SharedLogging(shared);
                    object heldFactory = factorySlot.GetValue(null), heldLogger = loggerSlot.GetValue(null);
                    owner.Dispose(); owner.Dispose();
                    Require(ReferenceEquals(factorySlot.GetValue(null), heldFactory) && ReferenceEquals(loggerSlot.GetValue(null), heldLogger),
                        "Map transitions retain logging until the last overlapping map releases it");
                    overlapping.Dispose(); overlapping.Dispose();
                    Require(loggerSlot.GetValue(null) == null && factorySlot.GetValue(null) == null,
                        "Last overlapping map releases both logging globals exactly once");
                    using (var replaced = new NativeMap14SharedLogging(shared))
                    {
                        loggerSlot.SetValue(null, heldLogger); factorySlot.SetValue(null, heldFactory);
                    }
                    Require(ReferenceEquals(factorySlot.GetValue(null), heldFactory) && ReferenceEquals(loggerSlot.GetValue(null), heldLogger),
                        "Disposing a map does not overwrite a later external logging binding");
                    loggerSlot.SetValue(null, null); factorySlot.SetValue(null, null);
                    bool rejected = false;
                    try { using (var missing = new MainlineSharedWorld(catalog, int.MaxValue, 4001)) { } }
                    catch (InvalidOperationException) { rejected = true; }
                    Require(rejected && loggerSlot.GetValue(null) == null && factorySlot.GetValue(null) == null,
                        "Failed world construction releases its owned logging globals");
                }
                finally { loggerSlot.SetValue(null, originalLogger); factorySlot.SetValue(null, originalFactory); }
            }
            Debug.Log("MAINLINE_SHARED_LOGGING_PASS: original generic logger ABI, value-state formatter, exception identity, categories, nested map scopes and original event receiver failure reporting.");
        }
    }
}
