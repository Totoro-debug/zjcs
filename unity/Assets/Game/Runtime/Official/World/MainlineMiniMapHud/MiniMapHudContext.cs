using System;
using System.Collections.Generic;
using UnityEngine;
using MapRegionStatus = Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;
namespace Zjcs.Official.World.Map13MiniMap
{
    // Local state/window/resource inputs to the original HUD. No channel or
    // offline entity-selection policy is chosen by this service boundary.
    public abstract class MiniMapHudContext
    {
        public abstract MiniMapManagedContext Root { get; }
        public abstract int CurrentMapClassId { get; }
        public abstract string GetMapGoddessHead(int map);
        public abstract string FormatText(string key, object[] args);
        public abstract void PopupFullScreen(Action<IMiniMapHudFullScreenPanel> callback);
        public abstract void PopupMapNotify(Action<IMiniMapHudNotify> callback);
        public abstract MiniMapHudMapPartInfo GetMapPartInfo(int map);
        public abstract bool HasMainPlayer { get; }
        public abstract Dictionary<long,MiniMapHudFightData> CurrentGames { get; }
        public abstract Dictionary<int,List<MapRegionStatus>> MapRegionStatusDict { get; }
        public abstract bool TryGetPublicContainerId(out long id);
        public abstract PublicMapLineStatus PublicMapLineStatus { get; }
        public abstract List<long> GroupMapIdOrderList { get; }
        public abstract int GetMapLineIndex();
        public abstract MiniMapHudChannelColors ChannelColors { get; }
        public abstract event Action<int,int,MapRegionStatus,MapRegionStatus> OnPlayerMapStatusUpdate;
        public abstract event Action<PublicMapLineStatus> OnPublicMapLineStatusChanged;
        public abstract event Action<List<long>> OnGroupMapIdOrderListChanged;
        public abstract void ShowError(int code);
        public abstract void PopupPublicMapBranchLineDialog();
    }
    public interface IMiniMapHudFullScreenPanel { void Init(int map); }
    public interface IMiniMapHudNotify { void AddData(Sprite sprite,string key); }
    public sealed class MiniMapHudMapPartInfo { public int ClassId; public bool SupportPublicRegion; }
    public sealed class MiniMapHudFightData { public long PlayerMapId; }
    public sealed class MiniMapHudChannelColors
    {
        public Color ServiceLoadFree,ServiceLoadBusy,ServiceLoadFull,ServiceLoadOverLoad;
    }
}
