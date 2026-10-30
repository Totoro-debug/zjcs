using System;
using UnityEngine;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.World
{
    public sealed class OfficialMap11UiAssets : ScriptableObject
    {
        public int[] itemIds;
        public string[] itemTemplates;
        public Sprite[] itemSprites;
        public Sprite[] itemQualities;
        public Sprite quality;
        public Sprite mapBackground;
        public string[] miniMapMarkNames;
        public Sprite[] miniMapMarkSprites;
        public int[] miniMapTabIds;
        public Sprite[] miniMapTabSprites;
        public string[] exploreIconNames;
        public Sprite[] exploreIcons;
        public int[] exploreQualityIds;
        public Sprite[] exploreQualities, exploreDetailQualities;
        public Sprite[] exploreAwardLights;
        public int[] starQualityIds;
        public Sprite[] starQualitySprites;
        public Sprite AwardLight(int qualityId) => exploreAwardLights[Array.IndexOf(exploreQualityIds, qualityId)];
        public Sprite ExploreIcon(string name) => exploreIcons[Array.IndexOf(exploreIconNames, name)];
        public Sprite ExploreQuality(int qualityId, bool detail = false) =>
            (detail ? exploreDetailQualities : exploreQualities)[Array.IndexOf(exploreQualityIds, qualityId)];
        public Sprite StarQuality(int qualityId)
        {
            int index = Array.IndexOf(starQualityIds, qualityId);
            if (index < 0) index = Array.IndexOf(starQualityIds, 0);
            if (index < 0 || starQualitySprites == null || index >= starQualitySprites.Length || starQualitySprites[index] == null)
                throw new InvalidOperationException("Unbound official skill star quality: " + qualityId);
            return starQualitySprites[index];
        }
        public Sprite MiniMapTab(int mapClassId)
        {
            int index = Array.IndexOf(miniMapTabIds, mapClassId);
            if (index < 0 || miniMapTabSprites[index] == null)
                throw new InvalidOperationException("Unbound official minimap tab: " + mapClassId);
            return miniMapTabSprites[index];
        }
        public Sprite MiniMapMark(string name)
        {
            int index = Array.IndexOf(miniMapMarkNames, name);
            if (index < 0 || miniMapMarkSprites[index] == null)
                throw new InvalidOperationException("Unbound official minimap mark: " + name);
            return miniMapMarkSprites[index];
        }
        public Sprite Item(int id)
        {
            int index = Array.IndexOf(itemIds, id);
            if (index < 0) throw new InvalidOperationException("Unbound official map item sprite: " + id);
            if (itemSprites[index] == null) throw new InvalidOperationException("Unbound Map11 item sprite: " + id);
            return itemSprites[index];
        }
        public Sprite ItemQuality(int id)
        {
            int index = Array.IndexOf(itemIds, id);
            if (index < 0) return ExploreQuality(ItemQualityId(id));
            if (index < 0 || itemQualities == null || index >= itemQualities.Length || itemQualities[index] == null)
                throw new InvalidOperationException("Unbound Map11 item quality: " + id);
            return itemQualities[index];
        }
        private OfficialWorldExplorationCatalog itemRules;
        public int ItemQualityId(int id)
        {
            int index = Array.IndexOf(itemIds, id);
            if (index >= 0)
            {
                int qualityIndex = Array.IndexOf(exploreQualities, itemQualities[index]);
                if (qualityIndex >= 0) return exploreQualityIds[qualityIndex];
            }
            return Array.Find((itemRules ??= OfficialWorldExplorationCatalog.Load()).Data.items, v => v.ClassId == id)?.Quality
                ?? throw new InvalidOperationException("Unbound original map reward quality: " + id);
        }
        public string ItemTemplate(int id)
        {
            int index = Array.IndexOf(itemIds, id);
            if (index < 0 || string.IsNullOrEmpty(itemTemplates[index]))
                throw new InvalidOperationException("Unbound original map item template: " + id);
            return itemTemplates[index];
        }
    }
}
