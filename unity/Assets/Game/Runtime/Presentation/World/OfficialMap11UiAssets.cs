using System;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    public sealed class OfficialMap11UiAssets : ScriptableObject
    {
        public int[] itemIds;
        public Sprite[] itemSprites;
        public Sprite[] itemQualities;
        public Sprite quality;
        public string[] miniMapMarkNames;
        public Sprite[] miniMapMarkSprites;
        public int[] miniMapTabIds;
        public Sprite[] miniMapTabSprites;
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
            if (index < 0 || itemSprites[index] == null) throw new InvalidOperationException("Unbound Map11 item sprite: " + id);
            return itemSprites[index];
        }
        public Sprite ItemQuality(int id)
        {
            int index = Array.IndexOf(itemIds, id);
            if (index < 0 || itemQualities == null || index >= itemQualities.Length || itemQualities[index] == null)
                throw new InvalidOperationException("Unbound Map11 item quality: " + id);
            return itemQualities[index];
        }
    }
}
