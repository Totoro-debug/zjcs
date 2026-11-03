using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.World.NativeMap13Labels;
namespace Zjcs.Official.World.Map13MiniMap
{
    // Exact consumed fields of the selected MapInfo/MiniMapMarkInfo tables.
    public sealed class MiniMapPanelResources : ScriptableObject
    {
        public MiniMapPanelMapInfo[] Maps;
        public Mark[] Marks;
        public MonoDictionary<string, Sprite> Icons;
        public Vector2 ReferenceResolution;
        public int ScaleMode, ScreenMatchMode;
        public float ReferencePixelsPerUnit, ScaleFactor, MatchWidthOrHeight;
        [Serializable] public sealed class Mark
        {
            public string Name, Image, Description;
            public bool ShowOnFilterEntry;
            public List<int> ShowOnMapGroupId, ShowOnMapClassId;
            public MiniMapFilterMarkInfo GetInfo() => new MiniMapFilterMarkInfo
            {
                Name=Name, Image=Image, Description=Description, ShowOnFilterEntry=ShowOnFilterEntry,
                ShowOnMapGroupId=ShowOnMapGroupId, ShowOnMapClassId=ShowOnMapClassId
            };
        }
    }
}
