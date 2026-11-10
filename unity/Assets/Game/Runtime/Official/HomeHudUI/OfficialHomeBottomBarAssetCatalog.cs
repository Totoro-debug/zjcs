using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.HomeHudUI
{
    public sealed class OfficialHomeBottomBarAssetCatalog : ScriptableObject
    {
        [Serializable] public sealed class SpriteEntry { public string guid; public Sprite sprite; }
        [Serializable] public sealed class SoundEntry { public uint eventId; public AudioClip clip; }
        [Serializable] private sealed class TextEntry { public string key, text; }
        [Serializable] private sealed class TextPayload { public string manifest; public TextEntry[] texts; }
        public GameObject prefab;
        public TextAsset texts;
        public TextAsset tipSourceObjects;
        public SpriteEntry[] sprites;
        public SoundEntry[] sounds;
        private Dictionary<string, string> localized;
        public static OfficialHomeBottomBarAssetCatalog LoadDefault()
            => Resources.Load<OfficialHomeBottomBarAssetCatalog>("Home/OfficialHomeBottomBarAssetCatalog")
                ?? throw new InvalidOperationException("Original home bottom bar assets are unavailable.");
        public string Text(string key)
        {
            if (localized == null)
            {
                var payload = JsonUtility.FromJson<TextPayload>(texts.text);
                if (payload == null || payload.manifest != "884_156692")
                    throw new InvalidOperationException("Unexpected original home bottom bar source.");
                localized = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var row in payload.texts) localized.Add(row.key, row.text);
            }
            return localized.TryGetValue(key, out var value) ? value : key;
        }
        public Sprite Sprite(string guid)
        {
            foreach (var row in sprites) if (row.guid == guid && row.sprite) return row.sprite;
            throw new KeyNotFoundException("Original bottom bar sprite is unavailable: " + guid);
        }
        public AudioClip Sound(uint eventId)
        {
            foreach (var row in sounds) if (row.eventId == eventId) return row.clip;
            return null;
        }
    }
}
