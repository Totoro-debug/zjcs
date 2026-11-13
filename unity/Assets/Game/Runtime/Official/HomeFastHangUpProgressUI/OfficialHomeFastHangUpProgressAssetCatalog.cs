using System;
using UnityEngine;

namespace Zjcs.Official.HomeFastHangUpProgressUI
{
    public sealed class OfficialHomeFastHangUpProgressAssetCatalog : ScriptableObject
    {
        public GameObject progressPrefab;
        public TextAsset texts;
        public static OfficialHomeFastHangUpProgressAssetCatalog LoadDefault()
            => Resources.Load<OfficialHomeFastHangUpProgressAssetCatalog>("Home/OfficialHomeFastHangUpProgressAssetCatalog")
                ?? throw new InvalidOperationException("Original home acceleration presentation assets are unavailable.");
    }
}
