using System;
using UnityEngine;
using Zjcs.Official.HomeHudInfrastructureUI;

namespace Zjcs.Offline.HomeHudUI
{
    // Packaged references to the selected original activity entry and its data.
    public sealed class OfflineHomeActivityEntryAssets : ScriptableObject
    {
        public GameObject EntryPrefab;
        public TextAsset Texts;
        public TextAsset TipSourceObjects;

        public static OfflineHomeActivityEntryAssets LoadDefault()
        {
            var assets = Resources.Load<OfflineHomeActivityEntryAssets>("Home/OfflineHomeActivityEntryAssets");
            if (!assets || !assets.EntryPrefab || !assets.Texts || !assets.TipSourceObjects
                || !assets.EntryPrefab.GetComponent<ActivityEntryBtn>())
                throw new InvalidOperationException("The original home activity entry is not bound.");
            return assets;
        }
    }
}
