using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Zjcs.Official.HomePlayerHealthRules;
using Zjcs.Official.HomePlayerPanelUI;

namespace Zjcs.Offline.HomePlayerHealthUI
{
    [Serializable]
    public sealed class OfflineHomePlayerHealthConfiguration
    {
        public string manifest;
        public float playerHpRedDotThreshold;
        public int cookingItemType;
        public CookingInfo[] cooking;
        public ItemType[] itemTypes;
        [Serializable] public sealed class ItemType { public int id, type; }
        private Dictionary<int,int> types;
        private Dictionary<int,CookingInfo> food;

        public HomePanelItemInfo GetItemInfo(int itemId)
        {
            if (types == null) types = itemTypes.ToDictionary(row => row.id, row => row.type);
            if (food == null) food = cooking.ToDictionary(row => row.Id);
            if (!types.TryGetValue(itemId, out int type)) throw new InvalidDataException("Unknown selected original HUD item: " + itemId);
            if (type != cookingItemType) return new HomePanelItemInfo { ItemType = HomePanelItemType.Other };
            return new HomePanelItemInfo { ItemType = HomePanelItemType.Cooking,
                PartInfo = food.TryGetValue(itemId, out var info) ? new HomePanelCookingInfo { Type = (Zjcs.Official.HomePlayerPanelUI.CookingType)(int)info.Type } : null };
        }

        public static OfflineHomePlayerHealthConfiguration LoadDefault()
        {
            var asset = Resources.Load<TextAsset>("Home/player-health-ui");
            var result = asset ? JsonConvert.DeserializeObject<OfflineHomePlayerHealthConfiguration>(asset.text) : null;
            if (result == null || result.manifest != "884_156692" || result.cooking == null || result.itemTypes == null
                || result.cooking.Length == 0 || result.playerHpRedDotThreshold <= 0 || result.playerHpRedDotThreshold > 1)
                throw new InvalidDataException("The selected original health HUD configuration is unbound.");
            return result;
        }
    }
}
