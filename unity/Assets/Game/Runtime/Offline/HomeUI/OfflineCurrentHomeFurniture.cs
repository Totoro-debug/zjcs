using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Zjcs.Offline.Home;
using Zjcs.Offline.Progression;
using Zjcs.Official.HomeConditions;
using Zjcs.Official.Partners;

namespace Zjcs.Offline.HomeUI
{
    // Adapts the selected raw FurnitureInfo records to the existing scene/window
    // ports. All selected predicates go through the migrated original parser.
    public sealed class OfflineCurrentHomeFurniture
    {
        private readonly Dictionary<HomeConditionNode, Dictionary<string, object>> nodes =
            new Dictionary<HomeConditionNode, Dictionary<string, object>>();
        private readonly HomeConditionRules conditions;
        private readonly Func<IHomeConditionPlayer> player;
        public HomeFurnitureRecord[] Records { get; }

        public OfflineCurrentHomeFurniture(OfficialCurrentHomeRuleAssetCatalog configuration,
            PartnerConditionRules partners, Func<IHomeConditionPlayer> player)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            conditions = configuration.CreateConditions(partners);
            Records = configuration.Rows("furniture").Select(row => new HomeFurnitureRecord {
                classId = Number(row, "ClassId"), type = Number(row, "FurnitureType"),
                unlock = Condition(row["UnlockCondition"]),
                repairNotice = Condition(row["RepairNoticeCondition"]),
                autoRepair = Condition(row["AutoRepairCondition"]),
                repairCost = ((Dictionary<string, object>)row["RepairCost"]).Select(cost => new HomeCostEntry {
                    itemId = int.Parse(cost.Key, CultureInfo.InvariantCulture), count = Convert.ToInt64(cost.Value) }).ToArray(),
                additionalRepair = ((List<object>)row["AdditionalRepairList"]).Select(Convert.ToInt32).ToArray(),
                repairStory = (string)row["RepairStoryName"], canLevelUp = (bool)row["CanLevelUp"]
            }).ToArray();
        }

        public void Bind(OfficialHomeRules rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            rules.BindFurniture("884_156692", Records, node => conditions.IsMet(nodes[node], player()),
                type => rules.FurnitureOf(type) != null && player().GetItemAmount(rules.FurnitureClassId(type)) >= 1);
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
    }

    // Reads the current save on every query: profile transactions replace Save.
    // Furniture ownership has a separate local ledger, so expose its original
    // item identity to conditions without adding a second copy to inventory.
    public sealed class OfflineHomeConditionPlayer : IHomeProfessionConditionPlayer
    {
        private readonly Func<OfflineMainlineSave> save;
        private readonly Func<int, bool> ownsFurniture;
        private readonly Dictionary<int, int> itemTypes, professionRanks, baseLevelLimits;
        private readonly Dictionary<int, int> furnitureTypes;

        public OfflineHomeConditionPlayer(OfficialCurrentHomeRuleAssetCatalog configuration,
            Func<OfflineMainlineSave> save, Func<int, bool> ownsFurniture)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            this.save = save ?? throw new ArgumentNullException(nameof(save));
            this.ownsFurniture = ownsFurniture ?? throw new ArgumentNullException(nameof(ownsFurniture));
            itemTypes = configuration.Rows("item").ToDictionary(row => Number(row, "ClassId"), row => Number(row, "ItemType"));
            professionRanks = configuration.Rows("profession_base").ToDictionary(row => Number(row, "Profession"), row => Number(row, "Rank"));
            furnitureTypes = configuration.Rows("furniture").ToDictionary(row => Number(row, "ClassId"), row => Number(row, "FurnitureType"));
            baseLevelLimits = configuration.Rows("system_level_limit").Where(row => Number(row, "PlayerBlessLevel") == 0)
                .GroupBy(row => Number(row, "SubRank")).ToDictionary(group => group.Key, group => group.Max(row => Number(row, "PlayerLevel")));
        }

        private OfflinePlayerProgress Player => save().Player;
        public int SubRank => Player.SubRank;
        public int Profession => Player.Wardrobe.Profession;
        public int BaseLevel => baseLevelLimits.TryGetValue(Player.SubRank, out int limit) && limit > 0
            ? Math.Min(Player.Level, limit) : Player.Level;

        public long GetItemAmount(int classId)
        {
            long count = Player.Inventory.Where(stack => stack.ItemId == classId).Sum(stack => stack.Count);
            return furnitureTypes.TryGetValue(classId, out int type) && HasFurniture(type) ? Math.Max(1, count) : count;
        }

        private bool HasFurniture(int type) => ownsFurniture(type)
            || type == OfflineFruitTreeService.ProductionFurnitureType && Player.FruitTree.ProductionRepaired
            || type == OfflineFruitTreeService.PetFurnitureType && Player.FruitTree.PetFacilityRepaired;

        public IEnumerable<HomeConditionItem> GetInventoryItems()
        {
            foreach (var stack in Player.Inventory)
                yield return new HomeConditionItem(itemTypes[stack.ItemId], stack.Count);
            foreach (var furniture in furnitureTypes)
                if (HasFurniture(furniture.Value) && !Player.Inventory.Any(stack => stack.ItemId == furniture.Key && stack.Count > 0))
                    yield return new HomeConditionItem(itemTypes[furniture.Key], 1);
        }

        public int GetHighestUnlockedProfessionRank()
        {
            // PlayerProfessionDataEx.GetHighestUnlockedProfessionRank: inspect
            // each unlocked profession's original Rank; the saved summary is not authoritative.
            int num = 0;
            foreach (var unlockedDatum in Player.ProfessionLevels)
            {
                int rank = professionRanks[unlockedDatum.Profession];
                if (rank > num) num = rank;
            }
            return num;
        }

        public bool IsMapAccessible(int mapClassId) => Player.UnlockedMapClassIds.Contains(mapClassId);
        public bool RegionIsActived(int mapClassId, int region) => save().World.ActivatedGoddessRegions
            .Any(value => value.MapClassId == mapClassId && value.Region == region);
        public bool HasNpc(int npcClassId) => Player.CharacterProperties.NpcFriendships.Any(value => value.NpcClassId == npcClassId);
        public int GetStoryCount(string storyKey) => Player.Stories.FirstOrDefault(value => value.Key == storyKey)?.Count ?? 0;
        public bool IsAnyGoddessActived() => save().World.ActivatedGoddessRegions.Count > 0;
        public int GetStageCount(int stageId) => save().World.StageWinCounts.FirstOrDefault(value => value.StageId == stageId)?.Count ?? 0;
        private static int Number(IReadOnlyDictionary<string, object> row, string key) => Convert.ToInt32(row[key]);
    }
}
