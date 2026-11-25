using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.Partners;

namespace Zjcs.Editor.HomeKeyedCondition
{
    public static class KeyedConditionValidation
    {
        static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }

        public static void Run()
        {
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrEmpty(workspace)) throw new InvalidOperationException("An isolated validation workspace is required.");
            var config = ScriptableObject.CreateInstance<OfficialCurrentHomeRuleAssetCatalog>();
            config.configuration = new TextAsset(File.ReadAllText(Path.Combine(workspace, "home-rules-current.json")));
            var partners = new PartnerConditionRules(table => table == "conditions"
                ? config.Rows("conditions") : Array.Empty<Dictionary<string, object>>());
            var slots = new ProfileSlotCatalog(Path.Combine(workspace, "keyed-condition-save-" + Guid.NewGuid().ToString("N")));
            string slot = slots.CreateSlot();
            var profile = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            Require(profile.ApplyCharacterMutationIfChanged(player => { player.Level = 49; return true; }).IsApplied,
                "The isolated character must start below the official level gate.");
            bool OwnsFurniture(int type) => type == 11 && profile.Save.Player.FruitTree.PetFacilityRepaired;
            using (var conditions = new OfflineHomeHudConditions(profile, config, partners, 0,
                OwnsFurniture, () => false, () => false))
            {
                var petShop = conditions.GetFunctionCondition(FunctionType.Shop, 7);
                Require(petShop != null && !conditions.IsMet(petShop), "Shop 7 must resolve and remain locked at level 49.");
                Require(conditions.GetFunctionCondition(FunctionType.Shop, 7, 1) == null,
                    "A different key1 must not resolve the shop condition.");
                var friend = conditions.GetFunctionCondition(FunctionType.Friend);
                Require(friend != null && ReferenceEquals(friend, conditions.GetFunctionCondition(FunctionType.Friend, 0, 0)),
                    "The existing zero-key entry must preserve its identity.");
                int notifications = 0;
                Action changed = () => notifications++;
                conditions.Register(petShop, changed);
                Require(profile.ApplyCharacterMutationIfChanged(player => { player.Level = 50; return true; }).IsApplied,
                    "The isolated level change must persist.");
                conditions.NotifyLevelUpdate();
                Require(notifications == 1 && !conditions.IsMet(petShop), "Level alone must not unlock the shop.");
                var fruit = new OfflineFruitTreeService(profile);
                Require(fruit.TryRepair(OfflineFruitTreeService.PetFurnitureType, 50, false, 1700000000, "keyed-condition:pet-repair").IsApplied,
                    "The real local pet tree repair must persist.");
                conditions.NotifyItemCountUpdate();
                Require(notifications == 2 && conditions.IsMet(petShop), "Repaired pet tree and level must satisfy the official condition.");
                profile = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
                using (var reopened = new OfflineHomeHudConditions(profile, config, partners, 0,
                    OwnsFurniture, () => false, () => false))
                    Require(reopened.IsMet(reopened.GetFunctionCondition(FunctionType.Shop, 7)),
                        "The real saved level and furniture must survive a cold profile reopen.");
                conditions.Deregister(petShop, changed);
                conditions.NotifyLevelUpdate(); conditions.NotifyItemCountUpdate();
                Require(notifications == 2, "Deregistration must release both leaf notifications.");
            }
            UnityEngine.Object.DestroyImmediate(config.configuration);
            UnityEngine.Object.DestroyImmediate(config);
            Debug.Log("HOME_KEYED_CONDITION_VALIDATION_PASSED");
        }
    }
}
