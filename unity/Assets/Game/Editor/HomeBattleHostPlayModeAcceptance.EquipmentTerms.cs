using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static bool EquipmentBattleProbeEnabled =>
            Environment.GetEnvironmentVariable("ZJCS_BATTLE_EQUIPMENT_TERMS_ACCEPTANCE") == "1";

        private static void ConfigureEquipmentBattleProfile(OfflinePlayerProgress player, int playerRank)
        {
            if (!EquipmentBattleProbeEnabled) return;
            var equipment = OfficialEquipmentAssetCatalog.LoadDefault().Load();
            player.Equipment.Instances.Clear();
            player.Equipment.Equipped.Clear();
            for (int index = 0; index < 4; index++)
            {
                int position = 10 + index;
                int itemId = equipment.Rows("equip").Select(row => Convert.ToInt32(row["ClassId"]))
                    .First(id => equipment.PositionForItem(id) == position
                        && Convert.ToInt32(equipment.Find("item", id)["LimitRank"]) == playerRank
                        && (position != 10 || Convert.ToInt32(equipment.Find("item", id)["ItemType"])
                            == player.InitialWeaponType));
                long instanceId = player.Equipment.NextInstanceId++;
                var instance = new OfflineEquipmentInstance { InstanceId = instanceId, ItemId = itemId,
                    ItemLevel = 31 + index * 2, SuitId = 4 };
                if (index == 0)
                    instance.Terms.Add(new OfflineEquipmentTerm { Type = 5, PropertyId = 40015,
                        SkillQuality = 5, Indices = new List<int> { 0, 0 } });
                player.Equipment.Instances.Add(instance);
                player.Equipment.Equipped.Add(new OfflineEquippedSlot { Position = position, InstanceId = instanceId });
            }
            Debug.Log("[Equipment terms hosts] isolated equipped profile: original term 40015 and four-piece suit 4.");
        }

        private static void VerifyEquipmentBattlePassives(string name, FightBattleCheckpoint checkpoint, bool finished)
        {
            if (!EquipmentBattleProbeEnabled) return;
            var home = UnityEngine.Object.FindObjectOfType<Zjcs.Presentation.Home.HomeFeatureRuntime>();
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var terms = home.DailyDungeonProgression.EquipmentBattlePassiveTerms(home.Profile.Save.Player);
            Require(terms.Count == 2 && terms.Any(value => value.AttributeId == 40015)
                && terms.Any(value => value.AttributeId == 50008), name + " did not collect the equipped term and suit.");
            foreach (var term in terms)
            {
                var status = player.Statuses.Single(value => value.ApplyingSkillItemId == term.SkillItemId);
                Require(status.SourceSkillRank == term.SkillRank && status.SourceSkillLevel == term.SkillLevel
                    && status.SourceSubRank == player.SubRank,
                    name + " lost the equipment passive's first quality rank, equipment level or owner subrank.");
                foreach (var parameter in term.Parameters)
                    Require(status.ReplacementProperties.Any(value => value.PropertyId == parameter.PropertyId
                        && value.RawValue == parameter.RawValue && value.IsFloat == parameter.IsFloat),
                        name + " lost an original typed equipment parameter.");
                if (term.AttributeId == 40015)
                    Require(Math.Abs(status.SkillReplay.Rate - 0.08f) < 0.000001f
                        && status.SkillReplay.MaxCountEachRound == 1 && status.SkillReplay.CountEachRound <= 1,
                        name + " altered the real 8-percent equipment replay or its independent cap.");
            }
            string saved = FightBattleCheckpointSerializer.Save(checkpoint);
            Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                name + " lost equipped parameters on checkpoint restore.");
            if (finished)
            {
                int replays = checkpoint.Receipts.SelectMany(value => value.Event.SkillCasts)
                    .Count(value => value.ActorId == 1 && value.IsReplay && value.ReplayStatusInstanceId > 0);
                Debug.Log("[Equipment terms hosts] " + name + ": equipment level="
                    + terms.Single(value => value.AttributeId == 40015).SkillLevel + ", suit mean level="
                    + terms.Single(value => value.AttributeId == 50008).SkillLevel
                    + ", quality rank=" + terms[0].SkillRank + ", owner subrank=" + player.SubRank
                    + ", real passive replays=" + replays + "; parameters and checkpoint passed.");
            }
        }
    }
}
