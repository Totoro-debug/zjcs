using System;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    public sealed class OfficialMap11UiAssets : ScriptableObject
    {
        public int[] itemIds;
        public Sprite[] itemSprites;
        public Sprite quality;
        public Sprite Item(int id)
        {
            int index = Array.IndexOf(itemIds, id);
            if (index < 0 || itemSprites[index] == null) throw new InvalidOperationException("Unbound Map11 item sprite: " + id);
            return itemSprites[index];
        }
    }
}
