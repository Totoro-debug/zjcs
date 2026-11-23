using UnityEngine;

namespace Zjcs.Offline.HomeNativeAssets
{
    // One independently loadable local reference per original prefab address.
    public sealed class HomeNativePrefabReference : ScriptableObject
    {
        [SerializeField] string mOriginalLocation;
        [SerializeField] GameObject mPrefab;
        public string OriginalLocation => mOriginalLocation;
        public GameObject Prefab => mPrefab;
    }
}
