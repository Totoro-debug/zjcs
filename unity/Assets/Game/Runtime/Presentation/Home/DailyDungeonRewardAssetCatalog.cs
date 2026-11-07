using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Presentation.Home
{
    public sealed class DailyDungeonRewardAssetCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Item
        {
            public int itemId;
            public int quality;
            public int itemType;
            public bool showPieceIcon;
            public Sprite sprite;
        }

        [SerializeField] private string manifest;
        [SerializeField] private Item[] items;
        private Dictionary<int, Item> byId;

        public static DailyDungeonRewardAssetCatalog LoadDefault()
        {
            var catalog = Resources.Load<DailyDungeonRewardAssetCatalog>("Dungeons/DailyDungeonRewardAssetCatalog");
            if (catalog == null || catalog.manifest != "884_156692")
                throw new InvalidOperationException("Official daily dungeon reward assets are missing.");
            return catalog;
        }

        public Item Find(int itemId)
        {
            if (byId == null)
            {
                byId = new Dictionary<int, Item>();
                foreach (Item item in items ?? Array.Empty<Item>())
                {
                    if (item == null || item.itemId <= 0 || item.sprite == null || byId.ContainsKey(item.itemId))
                        throw new InvalidOperationException("Official daily dungeon reward binding is invalid.");
                    byId.Add(item.itemId, item);
                }
            }
            return byId.TryGetValue(itemId, out Item found) ? found : null;
        }
    }
}
