using System;
using UnityEngine;
using Zjcs.Official.World;
using Zjcs.Official.World.Map13EntityStateViews;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Offline.World.NativeMap13TreeEntities
{
    // Explicit references to the verified original prefabs and native clocks.
    public sealed class NativeMap13TreeWorldAssets : ScriptableObject
    {
        [Serializable] public sealed class Display { public string path; public GameObject prefab; }
        [Serializable] public sealed class Placement
        {
            public int placementId, entityClassId;
            public Pos2d position;
            public Direction direction;
            public bool flipX;
            public string tag;
            public GridTransformComponentInfo body;
            public OfficialWorldStageRule stage;
            public Map13StageStateQueryData stageQuery;
            public Map13AwardItemQueryData awards;
        }
        public string manifest, animationSha256;
        public TextAsset animationBinary, animationCatalog, dissolveBinary, dissolveCatalog;
        public string spriteCatalogResource;
        public Display[] displays;
        public Placement[] placements;
    }
}
