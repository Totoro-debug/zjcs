using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.ItemMerge;

namespace Zjcs.Offline.Progression
{
    // Table ownership is independent of any building, saved inventory or window.
    public sealed class OfflineItemMergeCatalog
    {
        private readonly ItemMergeData data;

        private OfflineItemMergeCatalog(ItemMergeData data)
        {
            if (data == null || data.Recipes == null || data.ItemTypes == null || data.Levels == null)
                throw new FormatException("The original item-merge configuration is incomplete.");
            var products = data.Recipes.ToDictionary(row => row.ProductId);
            var types = data.ItemTypes.ToDictionary(row => row.ClassId);
            var levels = data.Levels.ToDictionary(row => row.Id);
            if (products.Keys.Any(id => !types.ContainsKey(id))
                || data.Recipes.Any(row => row.LevelFactorId > 0 && !levels.ContainsKey(row.LevelFactorId)))
                throw new FormatException("The original item-merge dependencies are incomplete.");
            // These recursive official methods require a finite configuration.
            var visited = new HashSet<int>();
            var visiting = new HashSet<int>();
            Action<int> visit = null;
            visit = id => {
                if (!products.TryGetValue(id, out var row) || visited.Contains(id)) return;
                if (!visiting.Add(id)) throw new FormatException("The item-merge configuration contains a cycle.");
                int[] materials = { row.Material1, row.Material2, row.Material3, row.Material4, row.Material5 };
                int[] counts = { row.Count1, row.Count2, row.Count3, row.Count4, row.Count5 };
                for (int index = 0; index < materials.Length; index++)
                    if (materials[index] > 0 && counts[index] > 0) visit(materials[index]);
                visiting.Remove(id); visited.Add(id);
            };
            foreach (int id in products.Keys) visit(id);
            this.data = data;
        }

        public static OfflineItemMergeCatalog Parse(string json)
            => new OfflineItemMergeCatalog(JsonUtility.FromJson<ItemMergeData>(json));
        public static OfflineItemMergeCatalog LoadDefault()
        {
            var asset = Resources.Load<TextAsset>("ItemMerge/item-merge-rules");
            if (!asset) throw new InvalidOperationException("The original item-merge configuration is not installed.");
            return Parse(asset.text);
        }
        public OfficialItemMergeRules CreateRules()
        {
            var types = data.ItemTypes.ToDictionary(row => row.ClassId, row => row.ItemType);
            var levels = new ItemMergeLevelNumbers(data.Levels,
                id => Debug.LogError("无法获取Id为" + id + "的等级系数"));
            return new OfficialItemMergeRules(data.Recipes, data.MaxMergeAmountAtATime, id => types[id], levels.GetNumber);
        }
    }
}
