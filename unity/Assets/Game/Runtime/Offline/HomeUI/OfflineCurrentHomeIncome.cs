using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Zjcs.Offline.Home;
using Zjcs.Official.HomeConditions;
using Zjcs.Official.Partners;

namespace Zjcs.Offline.HomeUI
{
    // Field projection only. Ordered row selection lives in the migrated
    // Console.dll parsers; composite IDs use this version's original conditions.
    public sealed class OfflineCurrentHomeIncome
    {
        private readonly Dictionary<HomeConditionNode, Dictionary<string, object>> nodes =
            new Dictionary<HomeConditionNode, Dictionary<string, object>>();
        private readonly HomeConditionRules conditions;
        private readonly Func<IHomeConditionPlayer> player;
        public HomeRulesData Data { get; }

        public OfflineCurrentHomeIncome(OfficialCurrentHomeRuleAssetCatalog configuration,
            PartnerConditionRules partners, Func<IHomeConditionPlayer> player)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            conditions = configuration.CreateConditions(partners);
            var rewardRows = configuration.Rows("hang_up_reward").Select(row => new HomeHangUpRewardRecord
            {
                id = Number(row, "Id"), order = Number(row, "Order"), condition = Condition(row["Condition"]),
                rewardRuleId = Number(row, "RewardRuleId"), hourlyOutput = Float(row["HourlyOutput"])
            }).ToArray();
            var rewardIds = new HashSet<int>(rewardRows.Select(row => row.rewardRuleId));
            Data = new HomeRulesData
            {
                version = "884_156692",
                hangUps = configuration.Rows("hang_up").Select(row => new HomeHangUpRecord
                {
                    id = Number(row, "Id"), bindFurniture = Number(row, "BindFurniture"),
                    sortOrder = Number(row, "SortOrder"), levelRuleId = Number(row, "LevelRuleId"),
                    unlock = Condition(row["UnlockCondition"]), notice = Condition(row["NoticeCondition"]),
                    canLevelUp = (bool)row["CanLevelUp"], useBlessingLevel = (bool)row["UseBlessingLevel"],
                    needRecordData = (bool)row["NeedRecordData"], rewardType = Number(row, "RewardType"),
                    rewardId = Number(row, "RewardId"), fastHangUpId = Number(row, "FastHangUpId"),
                    effectPercentProp = Number(row, "EffectPercentPropType"), effectFixedProp = Number(row, "EffectFixedPropType"),
                    maxStoreProp = Number(row, "MaxStorePropType"), finallyEffectPercentProp = Number(row, "FinallyEffectPercentPropType"),
                    initAutoUnlock = (bool)row["InitAutoUnlock"], initialExtraRewardSeconds = Number(row, "InitialExtraRewardSeconds"),
                    icon = (string)row["Icon"]
                }).ToArray(),
                hangUpRewards = rewardRows,
                fastHangUps = configuration.Rows("fast_hang_up").Select(row => new HomeFastHangUpRecord
                {
                    fastHangUpId = Number(row, "FastHangUpId"), bindFurniture = Number(row, "BindFurniture"),
                    hangUpIdList = Integers(row["HangUpIdList"]), extraHangUpRewardList = Integers(row["ExtraHangUpRewardList"]),
                    freeTimes = Number(row, "FastHangUpFreeTimes"), freeHours = Number(row, "FreeFastHangUpHour"),
                    maxUseItemCount = Number(row, "MaxUseItemCount"), autoReceive = (bool)row["AutoReceive"],
                    items = ((Dictionary<string, object>)row["FastHangUpItemDic"]).Select(item => new HomeFastHangUpItem
                    {
                        itemId = int.Parse(item.Key, CultureInfo.InvariantCulture), hours = Convert.ToInt32(item.Value)
                    }).ToArray()
                }).ToArray(),
                furnitureLevelRules = configuration.Rows("furniture_level_rule").Select(row => new HomeFurnitureLevelRuleRecord
                {
                    id = Number(row, "Id"), level = Number(row, "Level"), levelNum = Float(row["LevelNum"]),
                    maxStoreThread = Number(row, "MaxStoreTimeThread")
                }).ToArray(),
                homeLevels = configuration.Rows("home_level").Select(row => new HomeLevelRecord
                {
                    level = Number(row, "Level"), furnitureLevelUpLimit = Number(row, "FurnitureLevelUpLimit")
                }).ToArray(),
                hangUpUpgradeMaterials = configuration.Rows("hang_up_upgrade_material").Select(row => new HomeHangUpUpgradeRecord
                {
                    level = Number(row, "Level"), hangUpId = Number(row, "HangUpId"),
                    cost = ((Dictionary<string, object>)row["LevelUpCost"]).Select(item => new HomeCostEntry
                    {
                        itemId = int.Parse(item.Key, CultureInfo.InvariantCulture), count = Convert.ToInt64(item.Value)
                    }).ToArray()
                }).ToArray(),
                rewardRules = configuration.Rows("reward_rule").Where(row => rewardIds.Contains(Number(row, "Id")))
                    .Select(row => new HomeRewardRuleRecord
                    {
                        id = Number(row, "Id"), order = Number(row, "Order"), condition = Condition(row["Condition"]),
                        percent = Float(row["Percent"]), ids = Integers(row["IdList"]), weights = Integers(row["WeightList"]),
                        counts = ((List<object>)row["CountList"]).Select(value => (double)Float(value)).ToArray()
                    }).ToArray()
            };
        }

        public void Bind(OfficialHomeRules rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            rules.BindIncome(Data.version, Data, node => node == null || conditions.IsMet(nodes[node], player()));
        }

        private HomeConditionNode Condition(object value)
        {
            if (value == null) return null;
            var raw = (Dictionary<string, object>)value;
            var node = JsonConvert.DeserializeObject<HomeConditionNode>(JsonConvert.SerializeObject(raw));
            nodes.Add(node, raw);
            return node;
        }

        private static int Number(IReadOnlyDictionary<string, object> row, string key) => Convert.ToInt32(row[key]);
        private static int[] Integers(object value) => ((List<object>)value).Select(Convert.ToInt32).ToArray();
        private static float Float(object value) => (float)Convert.ToDouble(value, CultureInfo.InvariantCulture);
    }
}
