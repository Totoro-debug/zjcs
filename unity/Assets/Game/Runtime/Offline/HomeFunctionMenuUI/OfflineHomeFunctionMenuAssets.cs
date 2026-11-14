using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Offline.HomeFunctionMenuUI
{
    public sealed class OfflineHomeFunctionMenuAssets : ScriptableObject
    {
        [Serializable] public sealed class SoundEntry { public uint eventId; public AudioClip clip; }
        [Serializable] private sealed class TextEntry { public string key, text; }
        [Serializable] private sealed class TextPayload { public string manifest; public TextEntry[] texts; }
        public GameObject prefab;
        public TextAsset texts;
        public TextAsset tipSourceObjects;
        public SoundEntry[] sounds;
        private Dictionary<string, string> localized;

        public static OfflineHomeFunctionMenuAssets LoadDefault()
            => Resources.Load<OfflineHomeFunctionMenuAssets>("Home/OfflineHomeFunctionMenuAssets")
                ?? throw new InvalidOperationException("Original function menu assets are unavailable.");
        public string Text(string key)
        {
            if (localized == null)
            {
                var payload = JsonUtility.FromJson<TextPayload>(texts.text);
                if (payload == null || payload.manifest != "884_156692")
                    throw new InvalidOperationException("Unexpected original function menu source.");
                localized = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var row in payload.texts) localized.Add(row.key, row.text);
            }
            return localized.TryGetValue(key, out var value) ? value : key;
        }
        public AudioClip Sound(uint eventId)
        {
            foreach (var row in sounds) if (row.eventId == eventId) return row.clip;
            return null;
        }
    }
}
