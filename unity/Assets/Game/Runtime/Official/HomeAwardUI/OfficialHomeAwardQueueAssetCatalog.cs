using System;
using UnityEngine;

namespace Zjcs.Official.HomeAwardUI
{
    [Serializable]
    public sealed class HomeAwardPrefabBinding
    {
        public string originalGuid;
        public GameObject prefab;
    }
    [Serializable]
    public sealed class HomeAwardSoundBinding
    {
        public uint eventId;
        public AudioClip clip;
    }
    public sealed class OfficialHomeAwardQueueAssetCatalog : ScriptableObject
    {
        public string manifest;
        public GameObject queuePrefab, defaultIconPrefab;
        public GameObject itemIconPrefab, hintPrefab, extraGetPrefab;
        public Zjcs.Official.ProfessionUI.ItemIconConfig itemIconConfig;
        public HomeAwardPrefabBinding[] itemPrefabs;
        public HomeAwardSoundBinding[] sounds;
        public static OfficialHomeAwardQueueAssetCatalog LoadDefault()
        {
            var value = Resources.Load<OfficialHomeAwardQueueAssetCatalog>("Home/OfficialHomeAwardQueueAssetCatalog");
            if (!value || value.manifest != "884_156692" || !value.queuePrefab || !value.defaultIconPrefab
                || !value.itemIconPrefab || !value.itemIconConfig || !value.hintPrefab || !value.extraGetPrefab
                || value.itemPrefabs == null || value.itemPrefabs.Length != 2
                || value.sounds == null || value.sounds.Length == 0)
                throw new InvalidOperationException("Selected original award queue assets are unavailable.");
            return value;
        }
    }
}
