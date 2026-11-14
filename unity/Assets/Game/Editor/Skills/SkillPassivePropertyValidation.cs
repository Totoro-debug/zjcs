using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Skills
{
    public static class SkillPassivePropertyValidation
    {
        [Serializable] private sealed class ProfessionRow { public int Profession; public int[] Skills, FightUsedSkills; }
        [Serializable] private sealed class Rules { public ProfessionRow[] profession_base; }

        public static void Run()
        {
            ProjectTools.Validate();
            var assets = OfficialCharacterAssetCatalog.LoadDefault();
            var character = assets.Load();
            var rules = assets.LoadSkillRules();
            var combatAssets = OfficialCombatAssetCatalog.LoadDefault();
            var combat = combatAssets.Load();
            var items = combatAssets.LoadSkillItems();
            var professions = JsonUtility.FromJson<Rules>(combatAssets.Rules.text).profession_base
                .Where(row => new[] { 1, 2, 6, 10, 11, 15 }.Contains(row.Profession)).ToArray();
            var selected = professions.SelectMany(row => row.Skills.Concat(row.FightUsedSkills)).Distinct()
                .Select(items.Required).Where(item => item.PassivePropFactors.Count > 0).ToArray();
            Require(selected.Length == 18, "Original first-promotion property scope differs.");
            var source = new OfflineSkillPropSource(character, rules);
            int comparisons = 0, exclusions = 0;
            foreach (var item in selected)
            {
                var definition = rules.Skills.Single(row => row.ClassId == item.ItemId);
                int profession = professions.First(row => row.Skills.Concat(row.FightUsedSkills).Contains(item.ItemId)).Profession;
                foreach (int plan in Enumerable.Range(0, 7))
                foreach (int slot in Enumerable.Range(0, 4))
                foreach (var growth in new[] { (rank: 1, level: 1, subRank: 1), (rank: 17, level: 20, subRank: 5), (rank: 34, level: 500, subRank: 40) })
                {
                    var player = new OfflinePlayerProgress { SubRank = growth.subRank, SeasonRank = 17 };
                    player.Wardrobe.Profession = profession;
                    player.Skills.SelectedPlan = plan;
                    player.Skills.Plans[plan].PassiveSkills[slot] = item.ItemId;
                    player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = item.ItemId, Rank = growth.rank });
                    player.Skills.PassiveSlotLevels[slot] = growth.level;
                    var bless = new OfflineSkillBlessRecord { SeasonId = 17 };
                    bless.PassiveSlotLevels[slot] = 3;
                    player.Skills.BlessRecords.Add(new OfflineSkillBlessRecord { SeasonId = 18, PassiveSlotLevels = new[] { 99, 99, 99, 99 } });
                    player.Skills.BlessRecords.Add(bless);
                    var expected = combat.CalculatePassiveSkillProps(item.PassiveRankPropId, item.PassiveGroupLevelPropId,
                        item.PassiveRankFactorId, item.PassivePropFactors, growth.rank, growth.level + 3, growth.subRank);
                    Equal(source.CalculateProps(player), expected);
                    comparisons++;
                    if (plan != 0 || slot != 0 || growth.rank != 1) continue;
                    player.Skills.SelectedPlan = 1;
                    Require(source.CalculateProps(player).Count == 0, "An inactive plan contributes properties."); exclusions++;
                    player.Skills.SelectedPlan = 0;
                    player.Skills.Skills.Clear();
                    Require(source.CalculateProps(player).Count == 0, "An unowned secret contributes properties."); exclusions++;
                    player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = item.ItemId, Rank = growth.rank });
                    int unavailable = professions.Select(row => row.Profession).First(id => !definition.AvailableProfessions.Contains(id));
                    player.Wardrobe.Profession = unavailable;
                    Require(source.CalculateProps(player).Count == 0, "An unavailable profession contributes secret properties."); exclusions++;
                }
            }
            Debug.Log("[Passive property source] Production character asset inputs passed " + comparisons
                + " plan/slot/growth/blessing comparisons and " + exclusions + " exclusion checks.");
        }

        private static void Equal(IReadOnlyDictionary<int, long> actual, IReadOnlyDictionary<int, long> expected)
            => Require(actual.Count == expected.Count && expected.All(pair => actual.TryGetValue(pair.Key, out long value) && value == pair.Value),
                "The character property source differs from original equipped skill inputs.");
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
