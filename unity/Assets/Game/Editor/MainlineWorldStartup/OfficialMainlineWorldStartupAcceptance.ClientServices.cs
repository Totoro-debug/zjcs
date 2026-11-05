using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14SharedEntities;

namespace Zjcs.Editor.MainlineWorldStartup
{
    public static partial class OfficialMainlineWorldStartupAcceptance
    {
        private static IEnumerator VerifyClientServices()
        {
            var catalog = Resources.Load<NativeMap14SharedAssemblyCatalog>("Map14/Native/shared-assembly-catalog");
            using (var shared = new NativeMap14SharedAssemblies(catalog))
            using (var logging = new NativeMap14SharedLogging(shared))
            using (var configuration = new NativeMap14SharedConfiguration(shared))
            {
                var load = typeof(NativeMap14SharedAssemblies).GetMethod("Load", BindingFlags.Instance | BindingFlags.NonPublic);
                var framework = (Assembly)load.Invoke(shared, new object[] { "Framework.FirstPass.Runtime" });
                var core = (Assembly)load.Invoke(shared, new object[] { "Framework.Core" });
                var current = framework.GetType("Framework.DependencyInjection.ApplicationScope", true).GetProperty("Current");
                var previous = current.GetValue(null);
                var formulaField = shared.CommonType("BattleFormula").GetField("Instance");
                var previousFormula = formulaField.GetValue(null);
                var generalFormulaField = shared.CommonType("Formula").GetField("Instance");
                var previousGeneralFormula = generalFormulaField.GetValue(null);
                using (var outer = new MainlineSharedClientServices(shared))
                {
                    var provider = current.GetValue(null);
                    var formula = formulaField.GetValue(null);
                    var generalFormula = generalFormulaField.GetValue(null);
                    if (formula?.GetType() != shared.CommonType("BattleFormulaHandler"))
                        throw new InvalidDataException("The original formula handler is not bound.");
                    if (generalFormula?.GetType() != shared.CommonType("FormulaHandler"))
                        throw new InvalidDataException("The original general formula handler is not bound.");
                    var contract = core.GetType("Framework.DependencyInjection.Abstractions.IInstanceInjector", true);
                    if (!(provider is IServiceProvider services) ||
                        services.GetService(contract)?.GetType().FullName != "Framework.DependencyInjection.DryIoc.DryIocInstanceInjector")
                        throw new InvalidDataException("Original client injector is not registered in the real provider.");
                    if (!ReferenceEquals(outer.Manager, shared.Shared.GetType("MeManager", true).GetProperty("Instance").GetValue(null)))
                        throw new InvalidDataException("Client services did not use the original singleton.");
                    using (var inner = new MainlineSharedClientServices(shared))
                    {
                        if (ReferenceEquals(current.GetValue(null), provider) || !ReferenceEquals(inner.Manager, outer.Manager))
                            throw new InvalidDataException("Nested client service scopes did not retain the original singleton.");
                        VerifyFreshPlayerDependencies(shared);
                        yield return null;
                    }
                    if (!ReferenceEquals(current.GetValue(null), provider))
                        throw new InvalidDataException("The inner scope did not restore its original owner.");
                    if (!ReferenceEquals(formulaField.GetValue(null), formula))
                        throw new InvalidDataException("The inner scope did not restore its original formula handler.");
                    if (!ReferenceEquals(generalFormulaField.GetValue(null), generalFormula))
                        throw new InvalidDataException("The inner scope did not restore its original general formula handler.");
                    VerifyFreshPlayerDependencies(shared);
                }
                if (!ReferenceEquals(current.GetValue(null), previous))
                    throw new InvalidDataException("Client services leaked their application scope.");
                if (!ReferenceEquals(formulaField.GetValue(null), previousFormula))
                    throw new InvalidDataException("Client services leaked their original formula handler.");
                if (!ReferenceEquals(generalFormulaField.GetValue(null), previousGeneralFormula))
                    throw new InvalidDataException("Client services leaked their original general formula handler.");
            }
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "client-services.json"),
                "{\"real_dryioc\":true,\"original_singleton\":true,\"fresh_players\":2,\"original_state_observers\":true,\"scope_restore\":true,\"play_mode\":true}");
            Debug.Log("MAINLINE_CLIENT_SERVICES_PASS: real injection, fresh PlayerDataWrap, original R3 observers and scope restoration.");
        }

        private static void VerifyFreshPlayerDependencies(NativeMap14SharedAssemblies shared)
        {
            // This dependency check does not call or replace Player.ClientInit.
            var player = Activator.CreateInstance(shared.CommonType("Player"));
            var wrap = NativeMap14SharedPrefabs.Get(player, "DataWrap");
            if (wrap == null) throw new InvalidDataException("The original fresh PlayerDataWrap is absent.");
            var id = NativeMap14SharedPrefabs.Get(wrap, "UniqueId");
            if (id == null || id.GetType().Assembly.GetName().FullName !=
                "LiteDB, Version=5.0.20.0, Culture=neutral, PublicKeyToken=4ee40123013c9f27")
                throw new InvalidDataException("The original Mongo identity resolved another LiteDB assembly.");
            var handler = Activator.CreateInstance(shared.CommonType("PlayerPlayStateHandler"), player);
            try
            {
                var states = (IEnumerable)handler.GetType().GetMethod("GetStates").Invoke(handler, null);
                foreach (var state in states)
                    throw new InvalidDataException("A fresh original player unexpectedly has an active play state: " + state);
            }
            finally { handler.GetType().GetMethod("OnDestroy").Invoke(handler, null); }
        }
    }
}
