using System;
using UnityEngine;

namespace Zjcs.Official.HomeHudUI
{
    public sealed class OfficialHomeAvatarAssetCatalog : ScriptableObject
    {
        [Serializable] public sealed class SoundEntry { public uint eventId; public AudioClip clip; }
        public GameObject prefab;
        public SoundEntry[] sounds;
        public static OfficialHomeAvatarAssetCatalog LoadDefault()
            => Resources.Load<OfficialHomeAvatarAssetCatalog>("Home/OfficialHomeAvatarAssetCatalog")
                ?? throw new InvalidOperationException("Selected original home avatar is unavailable.");
        public AudioClip Sound(uint eventId)
        {
            foreach (var entry in sounds) if (entry.eventId == eventId) return entry.clip;
            return null;
        }
    }
}
