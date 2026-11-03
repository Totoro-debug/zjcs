using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeTerrain.Common;
using Object = UnityEngine.Object;
using MapRegionStatus = Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;

namespace Zjcs.Offline.World.MainlineMiniMap
{
    // Concrete MonoScript for the recovered original prefab. All original root
    // behaviour remains in MainlineMiniMapRoot; this adapter supplies its state.
    public sealed class MainlineNativeMiniMapRoot : MainlineMiniMapRoot, IMiniMapFilterMapContext
    {
        MiniMapResourcePointManager IMiniMapFilterMapContext.ResourcePointManager => ResourcePointManager;
        internal MainlineMiniMapBindings Bindings;
        internal MainlineMiniMapVisibilityAgent LiveVisibility;
        internal MainlineMiniMapLabels TimedLabels;
        private MiniMapManagedContext State => Bindings.State;
        protected override MiniMapSettings Settings => Bindings.Settings;
        protected override int MinimapConfigId => Bindings.MinimapConfigId;
        protected override int MapViewRangeExtraSize => Bindings.MapViewRangeExtraSize;
        protected override MiniMapResourcePointContext ResourcePointContext => Bindings.ResourcePoints;
        protected override IMiniMapCharacterManager MapCharacters => Bindings.Characters;
        public override int CurrentMapClassId => State.CurrentMapClassId;
        public override bool HasMainPlayer => State.HasMainPlayer;
        public override event Action<int,int,MapRegionStatus,MapRegionStatus> OnMapStatusUpdate
        { add => State.OnMapStatusUpdate += value; remove => State.OnMapStatusUpdate -= value; }
        public override List<MapRegionStatus> GetMapStatusListInTeam(int mapClassId) => State.GetMapStatusListInTeam(mapClassId);
        public override IMiniMapRegionAssetLease LoadAssetByGuid(string guid) => State.LoadAssetByGuid(guid);
        public override GameObject InstantiateRegion(GameObject prefab,Transform parent,bool worldPositionStays)
            => State.InstantiateRegion(prefab,parent,worldPositionStays);
        public override IMap13LabelServices LabelServices => TimedLabels ?? State.LabelServices;
        public override bool HasGridVisibilityAgent => LiveVisibility || State.HasGridVisibilityAgent;
        public override event Action<Pos2d,bool,bool> OnPosVisibilityUpdate
        {
            add { if (LiveVisibility) LiveVisibility.OnPosVisibilityUpdate += value; else State.OnPosVisibilityUpdate += value; }
            remove { if (LiveVisibility) LiveVisibility.OnPosVisibilityUpdate -= value; else State.OnPosVisibilityUpdate -= value; }
        }
        public override bool TryGetMapLiteData(int map,out MapRootEntityLiteSingleInfo info) => State.TryGetMapLiteData(map,out info);
        public override IMiniMapGridMover GetSpawnGridMover(int map) => State.GetSpawnGridMover(map);
        public override IReadOnlyDictionary<int,MiniMapRegionInfo> GetMapRegionDict(int map)
            => TimedLabels != null ? TimedLabels.GetRegions(map) : State.GetMapRegionDict(map);
        public override MiniMapRegionInfo GetRegion(int map,int region)
            => TimedLabels != null ? TimedLabels.GetRegions(map)[region] : State.GetRegion(map,region);
        public override int AreaToRegion(int map,int area) => State.AreaToRegion(map,area);
        public override int QueryRegion(Pos2d pos) => State.QueryRegion(pos);
        public override List<int> GetWorldMapProgresses(int map) => State.GetWorldMapProgresses(map);
        public override MiniMapRegionDisplayInfo GetRegionDisplay(int map,int region) => State.GetRegionDisplay(map,region);
        public override object QueryCommonGridGround(int map) => State.QueryCommonGridGround(map);
        public override object QuerySceneContainer(Component component) => State.QuerySceneContainer(component);
        public override MiniMapRegionDescItem InstantiateDescription(MiniMapRegionDescItem prefab,Transform parent)
            => State.InstantiateDescription(prefab,parent);
        public override GameObject InstantiateDisplay(GameObject prefab,Transform parent) => State.InstantiateDisplay(prefab,parent);
        public override IMiniMapDescriptionAssetLease<GameObject> LoadPrefab(string path) => State.LoadPrefab(path);
        public override MiniMapVisibilityAgent VisibilityAgent => LiveVisibility ? LiveVisibility : State.VisibilityAgent;
        public override int CharacterViewRange => State.CharacterViewRange;
        public override Pos2d CharacterGridPos => State.CharacterGridPos;
        public override IEnumerable<Pos2d> ForEachMapRegionActivedAllGrid(int map,int region)
            => State.ForEachMapRegionActivedAllGrid(map,region);
        public override bool HasMapRoot => State.HasMapRoot;
        public override Object GetMoveAgent(IMiniMapCharacterController controller) => State.GetMoveAgent(controller);
        public override Vector3 GetCharacterWorldPosition(IMiniMapCharacterController controller) => State.GetCharacterWorldPosition(controller);
        public override IMiniMapCharacterActionGroupHandle CharacterActionGroup => State.CharacterActionGroup;
        public override bool HasMapGroundRoot => State.HasMapGroundRoot;
        public override bool HasPartitionController => State.HasPartitionController;
        public override void ReservePartition(Vector3 position) => State.ReservePartition(position);
        public override string GetAreaTag(int map,Pos2d position) => State.GetAreaTag(map,position);
        public override MiniMapMarkDisplayInfo GetMapUIDisplay(int map,string areaTag) => State.GetMapUIDisplay(map,areaTag);
        public override IMiniMapDescriptionAssetLease<Sprite> LoadSprite(string path) => State.LoadSprite(path);
        public override void Popup(Type dialogType,Action<MiniMapMarkDialogBase> callback) => State.Popup(dialogType,callback);
        public override void CloseWindow(GameObject window) => State.CloseWindow(window);
        public override void ShowError(int code) => State.ShowError(code);
        public override IMiniMapMarkDataQueries MarkDataQueries => State.MarkDataQueries;
        public override IMiniMapDescriptionAssetLease<GameObject> LoadMarkPrefab(string path) => State.LoadMarkPrefab(path);
        public override GameObject InstantiateMark(GameObject prefab,Transform parent,bool worldPositionStays)
            => State.InstantiateMark(prefab,parent,worldPositionStays);
        public override IMiniMapManagedQueries ManagedQueries => State.ManagedQueries;
        public override MiniMapManagedEvents ManagedEvents => State.ManagedEvents;
        public override IMiniMapManagedAreaAgent ManagedAreaAgent => State.ManagedAreaAgent;
        public override IMiniMapMarkGridGround ManagedGridGround => State.ManagedGridGround;
        public override void InstantiateMarkAsync(string path,Transform parent,Action<MiniMapMarkUIBase> callback)
            => State.InstantiateMarkAsync(path,parent,callback);
    }
}
