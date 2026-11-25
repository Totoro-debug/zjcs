using System;
using UnityEngine;

namespace Zjcs.Offline.HomeAirship
{
    // Source sprites retain their native Unity asset identities and dependencies.
    public sealed class OfflineHomeAirshipAssets : ScriptableObject
    {
        [SerializeField] private Sprite mUnrepairedBackground;
        [SerializeField] private Sprite mRepairedBackground;
        public Sprite UnrepairedBackground => mUnrepairedBackground;
        public Sprite RepairedBackground => mRepairedBackground;

        public static OfflineHomeAirshipAssets LoadDefault()
        {
            var assets = Resources.Load<OfflineHomeAirshipAssets>("Home/Airship/airship-assets");
            if (!assets || !assets.mUnrepairedBackground || !assets.mRepairedBackground)
                throw new InvalidOperationException("The original airship background assets are unavailable.");
            return assets;
        }
    }
}
