using System;
using UnityEngine;

namespace Zjcs.Official.HomePlayerPanelUI
{
    public sealed class OfficialHomePlayerPanelAssetCatalog : ScriptableObject
    {
        public GameObject prefab;
        public TextAsset originalParentContext;
        public TextAsset localizedTexts;
        public PrefabEntry[] statusPrefabs;
        public SpriteEntry[] sprites;
        public SoundEntry[] sounds;
        [Serializable] public sealed class PrefabEntry { public string guid; public GameObject prefab; }
        [Serializable] public sealed class SpriteEntry { public string guid; public Sprite sprite; }
        [Serializable] public sealed class SoundEntry { public uint eventId; public AudioClip clip; }
    }
}
