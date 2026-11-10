using System;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeHudUI
{
    public sealed class OfficialHomeTopBarAssetCatalog : ScriptableObject
    {
        [Serializable] public sealed class Currency
        {
            public PlayerItemType type;
            public int itemId;
            public Sprite icon;
        }
        [Serializable] public sealed class SoundEntry { public uint eventId; public AudioClip clip; }
        public GameObject parentPrefab, managerPrefab, topBarPrefab, currencyPrefab;
        public Currency[] currencies;
        public SoundEntry[] sounds;
        public static OfficialHomeTopBarAssetCatalog LoadDefault()
            => Resources.Load<OfficialHomeTopBarAssetCatalog>("Home/OfficialHomeTopBarAssetCatalog")
                ?? throw new InvalidOperationException("Selected original home top bar is unavailable.");
        public int ItemId(PlayerItemType type)
        {
            foreach (var row in currencies) if (row.type == type) return row.itemId;
            throw new ArgumentOutOfRangeException(nameof(type), type, "Currency outside the selected home top bar.");
        }
        public Sprite Icon(int itemId)
        {
            foreach (var row in currencies) if (row.itemId == itemId && row.icon) return row.icon;
            throw new ArgumentOutOfRangeException(nameof(itemId));
        }
        public AudioClip Sound(uint eventId)
        {
            foreach (var row in sounds) if (row.eventId == eventId) return row.clip;
            return null;
        }
    }
}
