using System;
using UnityEngine;
using Zjcs.Official.HomeHudInfrastructureUI;

namespace Zjcs.Offline.HomeHudUI
{
    // Packaged references to the selected original first-pay entry and its data.
    public sealed class OfflineHomeFirstPayEntryAssets : ScriptableObject
    {
        public GameObject EntryPrefab;
        public TextAsset Texts;
        public TextAsset TipSourceObjects;

        public static OfflineHomeFirstPayEntryAssets LoadDefault()
        {
            var assets = Resources.Load<OfflineHomeFirstPayEntryAssets>("Home/OfflineHomeFirstPayEntryAssets");
            if (!assets || !assets.EntryPrefab || !assets.Texts || !assets.TipSourceObjects
                || !assets.EntryPrefab.GetComponent<FirstPayIconEntry>())
                throw new InvalidOperationException("The original home first-pay entry is not bound.");
            return assets;
        }
    }
}
