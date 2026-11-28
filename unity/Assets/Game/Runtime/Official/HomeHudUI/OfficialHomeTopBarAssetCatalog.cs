using System;
using System.Collections.Generic;
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
        private readonly List<Currency> additionalCurrencies = new List<Currency>();
        // Additional original windows supply their verified currency metadata for their own lifetime.
        public IDisposable RegisterCurrencies(IEnumerable<Currency> values)
        {
            var added = new List<Currency>();
            foreach (var value in values)
            {
                if (value == null || value.itemId <= 0 || !value.icon) throw new ArgumentException("Bind complete original currency metadata.", nameof(values));
                foreach (var row in currencies)
                    if (row.type == value.type && row.itemId != value.itemId) throw new ArgumentException("Original currency identity conflicts with the existing catalog.");
                foreach (var row in additionalCurrencies)
                    if (row.type == value.type && row.itemId != value.itemId) throw new ArgumentException("Original currency identity conflicts with another window.");
                foreach (var row in added)
                    if (row.type == value.type && row.itemId != value.itemId) throw new ArgumentException("Original currency identities conflict within the supplied window.");
                added.Add(value);
            }
            additionalCurrencies.AddRange(added);
            return new CurrencyScope(() => { foreach (var row in added) additionalCurrencies.Remove(row); });
        }
        private sealed class CurrencyScope : IDisposable
        {
            private Action release;
            public CurrencyScope(Action release) { this.release = release; }
            public void Dispose() { var action = release; release = null; action?.Invoke(); }
        }
        public static OfficialHomeTopBarAssetCatalog LoadDefault()
            => Resources.Load<OfficialHomeTopBarAssetCatalog>("Home/OfficialHomeTopBarAssetCatalog")
                ?? throw new InvalidOperationException("Selected original home top bar is unavailable.");
        public int ItemId(PlayerItemType type)
        {
            for (int i = additionalCurrencies.Count - 1; i >= 0; i--) if (additionalCurrencies[i].type == type) return additionalCurrencies[i].itemId;
            foreach (var row in currencies) if (row.type == type) return row.itemId;
            throw new ArgumentOutOfRangeException(nameof(type), type, "Currency outside the selected home top bar.");
        }
        public Sprite Icon(int itemId)
        {
            for (int i = additionalCurrencies.Count - 1; i >= 0; i--) if (additionalCurrencies[i].itemId == itemId) return additionalCurrencies[i].icon;
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
