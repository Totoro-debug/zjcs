using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Zjcs.Offline.Equipment;

namespace Zjcs.Offline.Progression
{
    // ItemDetailDialogUseResult.LoadResultFromChest's explicit-reward branch.
    // The selected payload proves that its candidates are ordinary materials;
    // skill ordering and random reward rules require their own source adapters.
    public sealed class OfficialChestUsePreviewRules
    {
        private readonly Dictionary<int, Dictionary<string, object>> items;
        private readonly Dictionary<int, Dictionary<string, object>> chests;

        public OfficialChestUsePreviewRules(string json)
        {
            var root = Map(OfficialEquipmentJson.Parse(json));
            var tables = Map(root["tables"]);
            items = Rows(tables["item"]).ToDictionary(row => Int(row["ClassId"]));
            chests = Rows(tables["chest"]).ToDictionary(row => Int(row["ClassId"]));
            var settings = Map(tables["game_settings"]);
            var skillTypes = new HashSet<int>(Map(settings["SkillTypes"]).Keys.Select(int.Parse));
            var pieceTypes = new HashSet<int>(((List<object>)settings["SkillPieceTypes"]).Select(Int));
            foreach (var chest in chests.Values)
            {
                int id = Int(chest["ClassId"]);
                if (!items.TryGetValue(id, out var info) || Int(info["ItemType"]) != 302
                    || !(bool)info["ShowUseDetail"])
                    throw new FormatException("Explicit chest preview item closure is invalid: " + id);
                if (chest["RewardRules"] is List<object> rules && rules.Count > 0)
                    throw new NotSupportedException("Bind the original random reward-rule preview before loading this chest.");
                foreach (var reward in Rewards(chest["Rewards"]).Concat(Rewards(chest["OptionalRewards"])))
                {
                    if (reward.Key <= 0 || reward.Value <= 0 || !items.TryGetValue(reward.Key, out var material))
                        throw new FormatException("Explicit chest preview reward closure is invalid: " + reward.Key);
                    int type = Int(material["ItemType"]);
                    if (skillTypes.Contains(type) || pieceTypes.Contains(type))
                        throw new NotSupportedException("Bind original SkillOptionSortUtils before loading skill candidates.");
                }
            }
        }

        public ChestUsePreview Preview(int itemId)
        {
            if (!items.TryGetValue(itemId, out var info) || !(bool)info["ShowUseDetail"]) return null;
            if (!chests.TryGetValue(itemId, out var chest))
                throw new NotSupportedException("This item's original use-result loader is not bound: " + itemId);
            var optional = Rewards(chest["OptionalRewards"]);
            bool choose = optional.Count > 0 && Int(chest["OptionCount"]) > 0;
            var rewards = choose ? optional : Rewards(chest["Rewards"]);
            // SkillOptionSortUtils returns before sorting when a candidate is
            // not a skill. Keep the original dictionary order for materials.
            return new ChestUsePreview(itemId, choose ? Int(chest["OptionCount"]) : 0, rewards);
        }

        private static List<KeyValuePair<int, long>> Rewards(object value)
            => value == null ? new List<KeyValuePair<int, long>>() : Map(value)
                .Select(pair => new KeyValuePair<int, long>(int.Parse(pair.Key, CultureInfo.InvariantCulture),
                    Convert.ToInt64(pair.Value, CultureInfo.InvariantCulture))).ToList();
        private static IEnumerable<Dictionary<string, object>> Rows(object value)
            => ((List<object>)value).Select(Map);
        private static Dictionary<string, object> Map(object value)
            => value as Dictionary<string, object> ?? throw new FormatException("Expected an original chest preview data object.");
        private static int Int(object value) => Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    public sealed class ChestUsePreview
    {
        private readonly IReadOnlyList<KeyValuePair<int, long>> rewards;
        private long useCount = 1;
        internal ChestUsePreview(int itemId, int optionCount, List<KeyValuePair<int, long>> rewards)
        { ItemId = itemId; OptionCount = optionCount; this.rewards = rewards.AsReadOnly(); }
        public int ItemId { get; }
        public int OptionCount { get; }
        public bool IsRandom => false;
        public bool HasResults => rewards.Count > 0;
        public int Count => rewards.Count;
        public long PerUseCount => rewards.Count == 0 ? 0 : rewards[0].Value;
        // The official setter ignores nonpositive and unchanged values.
        public long UseCount { get => useCount; set { if (value > 0 && value != useCount) useCount = value; } }
        public int ItemAt(int index) => rewards[index].Key;
        public long CountAt(int index) => unchecked(rewards[index].Value * useCount);
    }
}
