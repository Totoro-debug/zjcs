using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.HomeIncomeRules;

namespace Zjcs.Offline.Home
{
    public sealed partial class OfficialHomeRules
    {
        private HangUpRewardInfoParser<HomeHangUpRewardRecord> incomeRewardRows;
        private RewardRuleInfoParser<HomeRewardRuleRecord> incomeRewardRules;
        private HashSet<int> incomeRewardRuleIds;
        private HashSet<HomeConditionNode> selectedIncomeConditions;
        private Func<HomeConditionNode, bool> evaluateIncomeCondition;
        public string IncomeVersion { get; private set; }

        // Explicit opt-in: historical random rewards and other home systems keep
        // their catalogs. These rows and every nested condition belong to 884.
        public void BindIncome(string version, HomeRulesData selected, Func<HomeConditionNode, bool> evaluate)
        {
            if (version != "884_156692") throw new ArgumentException("Unexpected income configuration version.", nameof(version));
            if (selected == null) throw new ArgumentNullException(nameof(selected));
            if (selected.version != version) throw new ArgumentException("Income rows have another version.", nameof(selected));
            if (evaluate == null) throw new ArgumentNullException(nameof(evaluate));
            var hangUpMap = selected.hangUps.ToDictionary(row => row.id);
            var fastMap = selected.fastHangUps.ToDictionary(row => row.fastHangUpId);
            var levels = selected.furnitureLevelRules.ToDictionary(row => Key(row.id, row.level), row => row.levelNum);
            var homeLevelMap = selected.homeLevels.ToDictionary(row => row.level);
            var upgradeMap = selected.hangUpUpgradeMaterials.GroupBy(row => row.hangUpId)
                .ToDictionary(group => group.Key, group => group.OrderBy(row => row.level).ToList());
            var rewardMap = selected.hangUpRewards.GroupBy(row => row.id)
                .ToDictionary(group => group.Key, group => group.ToDictionary(row => row.order));
            var ruleMap = selected.rewardRules.GroupBy(row => row.id)
                .ToDictionary(group => group.Key, group => group.ToDictionary(row => row.order));
            var ruleIds = new HashSet<int>(ruleMap.Keys);
            if ((Rewards.groups ?? Array.Empty<HomeRewardGroupRecord>()).Any(row => ruleIds.Contains(row.ruleId)))
                throw new InvalidOperationException("Selected income rewards overlap the historical random reward catalog.");
            var rewardParser = new HangUpRewardInfoParser<HomeHangUpRewardRecord>(rewardMap, row => evaluate(row.condition));
            var ruleParser = new RewardRuleInfoParser<HomeRewardRuleRecord>(ruleMap,
                row => evaluate(row.condition), row => row.condition != null);
            var nodes = new HashSet<HomeConditionNode>(selected.hangUps.SelectMany(row => new[] { row.unlock, row.notice })
                .Concat(selected.hangUpRewards.Select(row => row.condition))
                .Concat(selected.rewardRules.Select(row => row.condition)).Where(node => node != null));

            hangUps.Clear();
            foreach (var row in hangUpMap) hangUps.Add(row.Key, row.Value);
            fastHangUps.Clear();
            foreach (var row in fastMap) fastHangUps.Add(row.Key, row.Value);
            levelNumbers.Clear();
            foreach (var row in levels) levelNumbers.Add(row.Key, row.Value);
            homeLevels.Clear();
            foreach (var row in homeLevelMap) homeLevels.Add(row.Key, row.Value);
            upgrades.Clear();
            foreach (var row in upgradeMap) upgrades.Add(row.Key, row.Value);
            Data.hangUps = selected.hangUps;
            Data.hangUpRewards = selected.hangUpRewards;
            Data.fastHangUps = selected.fastHangUps;
            Data.furnitureLevelRules = selected.furnitureLevelRules;
            Data.homeLevels = selected.homeLevels;
            Data.hangUpUpgradeMaterials = selected.hangUpUpgradeMaterials;
            Data.rewardRules = Data.rewardRules.Where(row => !ruleIds.Contains(row.id)).Concat(selected.rewardRules).ToArray();
            incomeRewardRows = rewardParser;
            incomeRewardRules = ruleParser;
            incomeRewardRuleIds = ruleIds;
            selectedIncomeConditions = nodes;
            evaluateIncomeCondition = evaluate;
            IncomeVersion = version;
        }

        public double IncomeMaxStoreHours(int ruleId, int level)
        {
            var exact = Data.furnitureLevelRules.FirstOrDefault(row => row.id == ruleId && row.level == level);
            if (IncomeVersion != null) return exact?.maxStoreThread ?? 0;
            return Math.Max(1, exact?.maxStoreThread
                ?? Data.furnitureLevelRules.FirstOrDefault(row => row.id == ruleId)?.maxStoreThread ?? 36);
        }
    }
}
