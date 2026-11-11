// Selected original screen adaptation; local platform/service boundaries only.
using System;
using UnityEngine;
namespace Zjcs.Official.HomeScreenUI
{
    // Explicit local resource address; all frame geometry remains in the original prefab.
    public sealed class OfficialHomeScreenAssetCatalog : ScriptableObject
    {
        public GameObject framePrefab;
        public static OfficialHomeScreenAssetCatalog LoadDefault()
        {
            var result=Resources.Load<OfficialHomeScreenAssetCatalog>("Home/OfficialHomeScreenAssetCatalog");
            if(result==null || result.framePrefab==null)
                throw new InvalidOperationException("Original home screen frame catalog is unavailable.");
            return result;
        }
    }
}
