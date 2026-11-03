using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.World.Map13MiniMap;
using MapRegionStatus = Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;

namespace Zjcs.Offline.World.MainlineMiniMap
{
    // Keep all original region, combat and window inputs. Only channel policy
    // changes: one local channel, consumed by the original prefab's hide flag.
    internal sealed class SingleChannelMiniMapHud : MiniMapHudContext
    {
        private readonly MiniMapHudContext source;
        public SingleChannelMiniMapHud(MiniMapHudContext source) { this.source = source; }
        public override MiniMapManagedContext Root => source.Root;
        public override int CurrentMapClassId => source.CurrentMapClassId;
        public override string GetMapGoddessHead(int map) => source.GetMapGoddessHead(map);
        public override string FormatText(string key, object[] args) => source.FormatText(key, args);
        public override void PopupFullScreen(Action<IMiniMapHudFullScreenPanel> callback) => source.PopupFullScreen(callback);
        public override void PopupMapNotify(Action<IMiniMapHudNotify> callback) => source.PopupMapNotify(callback);
        public override MiniMapHudMapPartInfo GetMapPartInfo(int map) => source.GetMapPartInfo(map);
        public override bool HasMainPlayer => source.HasMainPlayer;
        public override Dictionary<long, MiniMapHudFightData> CurrentGames => source.CurrentGames;
        public override Dictionary<int, List<MapRegionStatus>> MapRegionStatusDict => source.MapRegionStatusDict;
        public override bool TryGetPublicContainerId(out long id) => source.TryGetPublicContainerId(out id);
        public override PublicMapLineStatus PublicMapLineStatus => PublicMapLineStatus.Free;
        public override List<long> GroupMapIdOrderList => new List<long>
            { source.TryGetPublicContainerId(out long id) ? id : CurrentMapClassId };
        public override int GetMapLineIndex() => 1;
        public override MiniMapHudChannelColors ChannelColors => source.ChannelColors;
        public override event Action<int, int, MapRegionStatus, MapRegionStatus> OnPlayerMapStatusUpdate
        { add => source.OnPlayerMapStatusUpdate += value; remove => source.OnPlayerMapStatusUpdate -= value; }
        public override event Action<PublicMapLineStatus> OnPublicMapLineStatusChanged
        { add { } remove { } }
        public override event Action<List<long>> OnGroupMapIdOrderListChanged
        { add { } remove { } }
        public override void ShowError(int code) => source.ShowError(code);
        public override void PopupPublicMapBranchLineDialog() => source.PopupPublicMapBranchLineDialog();
    }
}
