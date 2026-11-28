using System;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    // The original generic material icon, count and slider controls share this window-owned boundary.
    public interface IProfessionMaterialServices
    {
        ItemIconConfig IconConfig { get; }
        ItemIcon GetIcon(int itemId, Transform parent);
        void RecycleIcon(ItemIcon icon);
        string GetItemNameKey(int itemId);
        object DecorateLack(object value);
    }

    [Serializable] public sealed class ProfessionPrefabGuidProxy
    {
        public int ContainerSource;
        public string mAssetGuid;
        public bool IsValid=>!string.IsNullOrEmpty(mAssetGuid);
        public string AssetGuid=>mAssetGuid;
    }

    public interface IProfessionDebugUIServices
    {
        bool PlayerAvailable { get; }
        bool IsGameMaster { get; }
        IProfessionImageResourceLoader Resources { get; }
        void CheckGameMaster(bool invokeByPlayer,Action<bool> completed);
        GameObject Instantiate(GameObject prefab,Transform parent);
    }
}
