using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.HomeTipDots;
using Zjcs.Official.Partners;

namespace Zjcs.Editor.HomeKeyedCondition
{
    public static class PetShopSessionValidation
    {
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }

        public static void Run()
        {
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrEmpty(workspace)) throw new InvalidOperationException("An isolated validation workspace is required.");
            var config = ScriptableObject.CreateInstance<OfficialCurrentHomeRuleAssetCatalog>();
            config.configuration = new TextAsset(File.ReadAllText(Path.Combine(workspace, "home-rules-current.json")));
            var partners = new PartnerConditionRules(table => table == "conditions"
                ? config.Rows("conditions") : Array.Empty<Dictionary<string, object>>());
            var slots = new ProfileSlotCatalog(Path.Combine(workspace, "pet-shop-session-save-" + Guid.NewGuid().ToString("N")));
            string slot = slots.CreateSlot(), other = slots.CreateSlot();
            var writer = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            var otherWriter = OfflineMainlineProgressSession.OpenCharacter(slots, other);
            Require(writer.ApplyCharacterMutationIfChanged(p => { p.Level = 49; return true; }).IsApplied, "initial level");
            bool ended = false;
            var owner = new OfflinePetShopNewFunctionSession(slots, slot, config, partners, () => ended = true);
            Require(otherWriter.ApplyCharacterMutationIfChanged(p => { p.Level = 50; return true; }).IsApplied, "other profile commit");
            Require(!owner.State.IsNew(18, 7), "other profile isolation");
            Require(writer.ApplyCharacterMutationIfChanged(p => { p.Level = 50; return true; }).IsApplied, "level commit");
            Require(!owner.State.IsNew(18, 7), "level alone must not unlock");
            var otherInstance = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            var fruit = new OfflineFruitTreeService(otherInstance);
            Require(fruit.TryRepair(OfflineFruitTreeService.PetFurnitureType, 50, false, 1700000000, "session:pet-repair").IsApplied,
                "pet tree repair commit");
            Require(owner.State.IsNew(18, 7), "unlock should set session mark across profile instances");
            owner.OnTabChanged(false, true); owner.OnTabChanged(true, false);
            Require(owner.State.IsNew(18, 7), "unselected and other tabs must preserve mark");
            owner.OnTabChanged(true, true);
            Require(!owner.State.IsNew(18, 7), "selected shop clears mark");
            OfflineMainlineProgressSession.EndPlayerSession();
            Require(ended, "player logout ends owner");
            var reopened = new OfflinePetShopNewFunctionSession(slots, slot, config, partners);
            Require(!reopened.IsLimited && !reopened.State.IsNew(18, 7), "unlocked cold reopen must not create new mark");
            reopened.Dispose();
            UnityEngine.Object.DestroyImmediate(config.configuration);
            UnityEngine.Object.DestroyImmediate(config);
            Debug.Log("PET_SHOP_SESSION_VALIDATION_PASSED");
        }
    }
}
