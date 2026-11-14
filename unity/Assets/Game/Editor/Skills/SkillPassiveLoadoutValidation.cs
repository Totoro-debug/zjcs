using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Skills
{
    public static class SkillPassiveLoadoutValidation
    {
        [Serializable] private sealed class ProfessionRow
        {
            public int Profession, PrePro, Rank;
            public int[] Skills, FightUsedSkills;
        }
        [Serializable] private sealed class ItemRow { public int ClassId, ItemType; }
        [Serializable] private sealed class Rules { public ProfessionRow[] profession_base; public ItemRow[] item; }

        public static void RunAndBuild()
        {
            Run();
            OfficialSkillAssetTools.BuildWindowsValidation();
        }

        public static void Run()
        {
            ProjectTools.Validate();
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            if (string.IsNullOrEmpty(workspace) || !File.Exists(Path.Combine(workspace, ".zjcs-validation.json")))
                throw new InvalidOperationException("Use a marked validation workspace.");
            var assets = OfficialCharacterAssetCatalog.LoadDefault();
            var rules = assets.LoadSkillRules();
            var combatAssets = OfficialCombatAssetCatalog.LoadDefault();
            var combat = combatAssets.Load();
            var items = combatAssets.LoadSkillItems();
            var rawRules = JsonUtility.FromJson<Rules>(combatAssets.Rules.text);
            var professions = rawRules.profession_base.ToDictionary(row => row.Profession);
            int[] scope = { 1, 10, 2, 6, 11, 15 };
            int[] selected = scope.SelectMany(id => professions[id].Skills)
                .Distinct().Where(id => rules.Skills.Any(row => row.ClassId == id && row.ItemType == 101)).ToArray();
            Require(selected.Length == 46, "Original first-promotion passive scope differs: " + selected.Length);
            var slots = new ProfileSlotCatalog(Path.Combine(workspace, "passive-loadout-" + Guid.NewGuid().ToString("N")));
            string slotId = slots.CreateSlot();
            var session = OfflineMainlineProgressSession.OpenCharacter(slots, slotId);
            var progression = new OfflineCharacterProgressionService(session, assets.Load(), refreshDerivedProgress: false);
            progression.AttachSkillRules(rules);
            progression.AttachEquipmentCatalog(OfficialEquipmentAssetCatalog.LoadDefault().Load());
            int comparisons = 0, valid = 0, statusCount = 0;
            foreach (int itemId in selected)
            foreach (int profession in scope)
            foreach (int plan in Enumerable.Range(0, 7))
            {
                int slot = (plan + profession) % 4;
                var player = Player(itemId, profession, plan, slot);
                bool available = Available(professions, itemId, profession);
                Check(combat, items, progression, player, itemId, slot, available, ref statusCount);
                comparisons++;
                if (available) valid++;
            }
            int exclusions = 0;
            foreach (int itemId in selected)
            {
                int profession = scope.First(id => Available(professions, itemId, id));
                var player = Player(itemId, profession, 0, 0);
                player.Skills.Skills.Clear();
                Check(combat, items, progression, player, itemId, 0, false, ref statusCount); exclusions++;
                player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = itemId, Rank = 0 });
                Check(combat, items, progression, player, itemId, 0, false, ref statusCount); exclusions++;
                player.Skills.Skills[0].Rank = 17;
                player.Skills.SelectedPlan = 1;
                Check(combat, items, progression, player, itemId, 0, false, ref statusCount); exclusions++;
                foreach (int weapon in new[] { 200, 201, 0 })
                {
                    player = Player(itemId, 0, 0, 0);
                    player.InitialWeaponType = weapon;
                    int safe = weapon == 200 ? 1 : weapon == 201 ? 10 : 0;
                    Check(combat, items, progression, player, itemId, 0,
                        Available(professions, itemId, safe), ref statusCount);
                    comparisons++;
                }
                foreach (int weapon in new[] { 200, 201 })
                {
                    player = Player(itemId, 0, 0, 0);
                    player.InitialWeaponType = weapon == 200 ? 201 : 200;
                    player.Equipment.Instances.Add(new OfflineEquipmentInstance { InstanceId = 1,
                        ItemId = rawRules.item.First(row => row.ItemType == weapon).ClassId });
                    player.Equipment.Equipped.Add(new OfflineEquippedSlot { Position = 10, InstanceId = 1 });
                    Check(combat, items, progression, player, itemId, 0,
                        Available(professions, itemId, weapon == 200 ? 1 : 10), ref statusCount);
                    comparisons++;
                }
            }
            int activeId = rules.Skills.First(row => row.ItemType == 100 && Available(professions, row.ClassId, 1)).ClassId;
            Check(combat, items, progression, Player(activeId, 1, 0, 0), activeId, 0, false, ref statusCount); exclusions++;
            Debug.Log("[Passive loadout] Original profession tables passed " + comparisons + " property/status comparisons; "
                + valid + " available plan cases, " + exclusions + " ownership/type/inactive-plan exclusions and "
                + statusCount + " initial status provenance checks.");
        }

        private static OfflinePlayerProgress Player(int id, int profession, int plan, int slot)
        {
            var player = new OfflinePlayerProgress { Level = 50, SubRank = 5, SeasonRank = 17 };
            player.Wardrobe.Profession = profession;
            player.Skills.SelectedPlan = plan;
            player.Skills.Plans[plan].PassiveSkills[slot] = id;
            player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = id, Rank = 17 });
            player.Skills.PassiveSlotLevels = new[] { 2, 7, 20, 30 };
            player.Skills.BlessRecords.Add(new OfflineSkillBlessRecord { SeasonId = 18, PassiveSlotLevels = new[] { 99, 99, 99, 99 } });
            player.Skills.BlessRecords.Add(new OfflineSkillBlessRecord { SeasonId = 17, PassiveSlotLevels = new[] { 1, 4, 0, 3 } });
            return player;
        }

        // Independent traversal of the raw official table, rather than runtime availability arrays.
        private static bool Available(Dictionary<int, ProfessionRow> rows, int itemId, int profession)
        {
            if (profession == 0)
                return rows.Values.Any(row => row.Rank == 1 && (row.Skills ?? Array.Empty<int>()).Contains(itemId));
            while (profession != 0 && rows.TryGetValue(profession, out var row))
            {
                if ((row.Skills ?? Array.Empty<int>()).Contains(itemId)
                    || (row.FightUsedSkills ?? Array.Empty<int>()).Contains(itemId)) return true;
                profession = row.PrePro;
            }
            return false;
        }

        private static void Check(FightOfficialCombatCatalog combat, FightOfficialSkillItemCatalog items,
            OfflineCharacterProgressionService progression, OfflinePlayerProgress player, int itemId, int slot,
            bool available, ref int statusCount)
        {
            var item = items.Required(itemId);
            int level = player.Skills.PassiveSlotLevels[slot] + new[] { 1, 4, 0, 3 }[slot];
            var expected = new Dictionary<int, long>();
            if (available)
                foreach (var pair in combat.CalculatePassiveSkillProps(item.PassiveRankPropId, item.PassiveGroupLevelPropId,
                    item.PassiveRankFactorId, item.PassivePropFactors, 17, level, player.SubRank)) expected[pair.Key] = pair.Value;
            var actual = new Dictionary<int, long>();
            OfficialBattlePassivePropertyAssembler.ApplyEquippedPlayerPassives(combat, items, actual, player, progression);
            string context = " item=" + itemId + " profession=" + player.Wardrobe.Profession + " plan=" + player.Skills.SelectedPlan;
            Require(actual.Count == expected.Count && expected.All(pair => actual.TryGetValue(pair.Key, out long value) && value == pair.Value),
                "Battle passive property selection differs." + context);
            var unit = new FightBattleUnitState { Id = 1, CharacterType = 1, Level = player.Level,
                SubRank = player.SubRank, Profession = player.Wardrobe.Profession };
            OfficialBattlePlayerLoadoutAssembler.Assemble(combat, items, unit, player, progression);
            int[] statuses = available ? item.PassiveStatusIds.Where(id => id > 0).ToArray() : Array.Empty<int>();
            Require(unit.Statuses.Select(value => value.ClassId).OrderBy(id => id).SequenceEqual(statuses.OrderBy(id => id)),
                "Battle passive graph selection differs." + context);
            foreach (var status in unit.Statuses)
            {
                Require(status.ApplyingSkillItemId == itemId && status.RootSkillId == item.EntityId
                    && status.SourceSkillRank == 17 && status.SourceSkillLevel == level && status.SourceSubRank == player.SubRank
                    && status.Origin.IsSet && status.Origin.UnitId == unit.Id && status.Origin.SkillItemId == itemId,
                    "Passive status lost original cultivation/source." + context);
                statusCount++;
            }
        }

        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
