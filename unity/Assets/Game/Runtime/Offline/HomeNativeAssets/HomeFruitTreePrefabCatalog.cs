using System;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.HomeNativeAssets
{
    [CreateAssetMenu(menuName = "Home/Fruit Tree Prefab Catalog")]
    public sealed class HomeFruitTreePrefabCatalog : ScriptableObject
    {
        [Serializable] public sealed class Entry { public string AssetGuid; public HomeNativePrefabReference Reference; }
        [SerializeField] Entry[] entries;
        public static HomeFruitTreePrefabCatalog LoadDefault()
        {
            var value = Resources.Load<HomeFruitTreePrefabCatalog>("Home/FruitTree/Prefabs");
            if (!value || value.entries == null || value.entries.Length == 0)
                throw new InvalidOperationException("The official local fruit-tree prefab catalog is missing.");
            return value;
        }
        public GameObject Load(string assetGuid)
        {
            Entry row = entries.SingleOrDefault(value => value.AssetGuid == assetGuid);
            if (row == null || !row.Reference || !row.Reference.Prefab)
                throw new InvalidOperationException("The official fruit slot prefab is not routed locally: " + assetGuid);
            return row.Reference.Prefab;
        }
    }
}
