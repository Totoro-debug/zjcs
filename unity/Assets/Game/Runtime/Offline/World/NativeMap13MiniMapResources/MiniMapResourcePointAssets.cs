using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap13MiniMapResources
{
    public sealed class MiniMapResourcePointAssets:ScriptableObject
    {
        [Serializable] public sealed class BoardAsset{public int RefreshGroup;public string OriginalGuid;public GameObject Prefab;}
        [Serializable] public sealed class IconAsset{public string Path;public Sprite Sprite;}
        public List<BoardAsset> Boards;
        public List<IconAsset> Icons;
        public GameObject ManagerPrefab;
        public TextAsset SourceData;
        public TextAsset ManagerFields;
    }
}
