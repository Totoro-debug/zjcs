using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;

namespace Zjcs.Editor.Progression
{
    public static class MaterialGatheringAcceptance
    {
        [MenuItem("Zjcs/Validation/Material Gathering Rules")]
        public static void Run()
        {
            string root = Path.Combine(Path.GetTempPath(), "zjcs-material-gathering-" + Guid.NewGuid().ToString("N"));
            try
            {
                OfficialMaterialGatheringCatalog catalog = OfficialMaterialGatheringCatalog.LoadDefault();
                OfficialCharacterCatalog characterCatalog = OfficialCharacterAssetCatalog.LoadDefault().Load();
                OfficialPropCatalog props = characterCatalog.Props;
                var slots = new ProfileSlotCatalog(root);
                string slotId = slots.CreateSlot();
                var save = new OfflineMainlineSave
                {
                    CreatedAtUnixSeconds = slots.GetCreatedAtUnixSeconds(slotId)
                };
                save.Player.CharacterName = "采集规则验收";
                save.Player.CharacterCreated = true;
                save.Player.InitialWeaponType = 200;
                save.Player.Level = 20;
                save.Player.Experience = characterCatalog.CumulativeLevelExperience[20];
                save.Player.SubRank = 3;
                save.Player.Rank = 1;
                save.Player.SeasonRank = 1;
                save.Player.MaxProfessionRank = 1;
                save.Player.Wardrobe.Profession = 1;
                save.Player.CharacterProperties.TreasurePlaneRank = 1;
                save.Player.CharacterProperties.LegionLibraryLevel = 1;
                save.Player.Inventory = catalog.Dungeons.Select(dungeon =>
                    new OfflineItemStack { ItemId = dungeon.CostItemId, Count = 64 }).ToList();
                File.WriteAllText(slots.GetProgressPath(slotId), JsonUtility.ToJson(save, true));
                OfflineMainlineProgressSession profile = OfflineMainlineProgressSession.OpenCharacter(slots, slotId);

                var service = new OfflineMaterialGatheringService(profile, catalog, props);
                foreach (MaterialGatheringDungeon dungeon in catalog.Dungeons.OrderBy(value => value.Type))
                {
                    Require(dungeon.UnlockStages.All(stageId => profile.StageWinCount(stageId) == 0),
                        "A first-entry profile unexpectedly has a stage win for type " + dungeon.Type);
                    var propertyValues = new Dictionary<int, long>
                    {
                        [dungeon.AttackProp] = 1000000000,
                        [dungeon.CritRateProp] = 0,
                        [dungeon.CritPowerProp] = 0
                    };
                    long toolsBefore = profile.Save.Player.Inventory
                        .FirstOrDefault(item => item.ItemId == dungeon.CostItemId)?.Count ?? 0;
                    for (int stageIndex = 0; stageIndex < 3; stageIndex++)
                    {
                        MaterialGatheringActionResult result = service.Gather(dungeon.Type, 1, false,
                            quickUnlocked: true, subRank: 0, nonBattleProps: propertyValues,
                            receiptId: "acceptance:material-gathering:" + dungeon.Type + ":" + stageIndex);
                        Require(result.IsApplied && result.ActionCount == 1,
                            "A first-entry gather did not complete one action for type " + dungeon.Type + ": " + result.Message);
                        Require(profile.StageWinCount(dungeon.UnlockStages[stageIndex]) == 1,
                            "Breaking a material did not advance UnlockStages[" + stageIndex + "] for type " + dungeon.Type);
                        Require(dungeon.UnlockStages.Skip(stageIndex + 1)
                                .All(stageId => profile.StageWinCount(stageId) == 0),
                            "A break advanced more than one stage for type " + dungeon.Type);
                        Require(profile.Save.Player.Inventory
                                .FirstOrDefault(item => item.ItemId == dungeon.CostItemId)?.Count == --toolsBefore,
                            "A gather did not consume exactly one tool for type " + dungeon.Type);
                        Require(result.Rewards.Count > 0,
                            "A broken material produced no configured reward for type " + dungeon.Type);
                    }
                }

                profile.Reload();
                foreach (MaterialGatheringDungeon dungeon in catalog.Dungeons)
                {
                    Require(dungeon.UnlockStages.Take(3)
                            .All(stageId => profile.StageWinCount(stageId) == 1),
                        "Ordered stage wins did not survive profile reload for type " + dungeon.Type);
                    Require(profile.Save.Player.Inventory
                            .FirstOrDefault(item => item.ItemId == dungeon.CostItemId)?.Count == 61,
                        "Tool consumption did not survive profile reload for type " + dungeon.Type);
                }

                Debug.Log("[MaterialGatheringAcceptance] passed: four first-entry gathers, tool consumption, "
                    + "reward grants, ordered UnlockStages advancement, and profile reload.");
            }
            catch (Exception error)
            {
                Debug.LogError("[MaterialGatheringAcceptance] failed: " + error);
                throw;
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
