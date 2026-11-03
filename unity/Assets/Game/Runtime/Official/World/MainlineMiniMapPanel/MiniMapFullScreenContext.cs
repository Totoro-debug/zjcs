using System;
using UnityEngine;
namespace Zjcs.Official.World.Map13MiniMap
{
    // Internal inputs to the original panel. Visited-map and entity policies
    // belong to the owning local map session, never to this window.
    public abstract class MiniMapFullScreenContext
    {
        public abstract int CurrentMapClassId { get; }
        // Assigned by the owning common module before Awake.
        public MainlineMiniMapRoot CurrentMiniMap { get; set; }
        public abstract MiniMapPanelMapInfo[] GetAllMapInfos();
        public abstract MiniMapPanelMapInfo GetMapInfo(int map);
        public abstract bool IsWorldMapVisited(int map);
        public abstract long GetTick();
        public abstract double CalcElapsedSecond(long tick);
        public abstract void HideWorldUI();
        public abstract void ShowWorldUI();
        public abstract void InstantiateMiniMapAsync(string path, Transform parent, Action<MainlineMiniMapRoot> callback);
        public abstract void ReleaseMiniMap(MainlineMiniMapRoot map);
        public abstract IMiniMapDescriptionAssetLease<Sprite> LoadSprite(string path);
        public abstract void CloseWindow(GameObject window);
        public abstract void CloseLastWindow();
    }
    [Serializable] public sealed class MiniMapPanelMapInfo
    {
        public int ClassId, MapGroupId, MiniMapSwitchTabGroup;
        public string MiniMapIcon;
    }
}
