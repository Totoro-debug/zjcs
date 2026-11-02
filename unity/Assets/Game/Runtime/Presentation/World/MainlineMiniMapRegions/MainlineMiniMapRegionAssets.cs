using System;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    public sealed class MainlineMiniMapRegionAssets : ScriptableObject
    {
        public int MapClassId;
        public GameObject Ground;
        public MainlineMiniMapRegionAsset[] Regions;
    }

    [Serializable]
    public sealed class MainlineMiniMapRegionAsset
    {
        public string OriginalGuid;
        public GameObject Prefab;
    }
}
