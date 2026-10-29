using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.Map13Entities;
namespace Zjcs.Official.World.Map13MiniMap
{
    // Read-only projections of the exact original entity/configuration objects.
    // Commands delegate to the local map backend; these ports invent no state.
    public interface IMiniMapMarkEntity
    {
        object Original { get; }
        int ClassId { get; }
        int ContainerClassId { get; }
        IMiniMapMarkGrid GridTransform { get; }
        T GetComponent<T>() where T:class;
        string GetEntityTag();
    }
    public interface IMiniMapMarkGrid
    {
        IMiniMapMarkEntity Entity { get; }
        GridTransformComponentInfo Info { get; }
        Pos2d Pos { get; }
        List<Pos2d> GetBodyRange();
        Vector3 GridToPosition(Pos2d position);
    }
    public interface IMiniMapMarkStage { int State { get; } bool StateEqual(int state);bool IsActive(); }
    public interface IMiniMapMarkStageTransport { int TargetLoEntityId { get; } }
    public interface IMiniMapMarkWayPoint { IMiniMapMarkEntity Entity { get; } bool IsUsable(); }
    public interface IMiniMapMarkWorldEntry { IMiniMapMarkEntity Entity { get; } IMiniMapMarkGrid GridTransform { get; } bool Active { get; } }
    public sealed class MiniMapMarkExtendInfo
    {
        public int CompleteState;
        public string DialogIconPrefab;
    }
    public sealed class MiniMapMarkMapInfo { public int MapGroupId; }
    public sealed class MiniMapMarkTransportTargetInfo { public int TargetMapClassId,TargetRegion; }
    public sealed class MiniMapMarkPositionInfo { public Pos2d Pos;public int Area,Region; }
    public sealed class MiniMapMarkSpawnEntityInfo { public int ClassId; }
    public sealed class MiniMapMarkLoEntityInfo
    {
        public int LoEntityId;
        public MiniMapMarkPositionInfo PosInfo;
        public MiniMapMarkSpawnEntityInfo DefSpawnEntityInfo;
    }
    public interface IMiniMapMarkDataQueries
    {
        int GridToArea(int map,Pos2d position);
        string GetAreaTag(int map,int area);
        MiniMapMarkMapInfo GetMapInfo(int map);
        EntityMapUIDisplayInfo GetEntityMapUIDisplay(int entityClassId,int mapGroupId,int map,string areaTag,string entityTag);
        MiniMapMarkExtendInfo GetEntityExtend(int entityClassId);
        MiniMapMarkLoEntityInfo GetContainerEntityInfo(int map,int loEntityId);
        string GetEntityTag(int map,int loEntityId);
        IMiniMapMarkEntity GetCommonPrefab(int entityClassId);
        Vector3 MapGridToPos(int map,Pos2d position);
        MiniMapMarkTransportTargetInfo GetMapTransportTargetInfo(int map,int loEntityId);
        bool RegionIsActived(int map,int region);
        int QueryRegion(Pos2d position);
        int CurrentMeMapId { get; }
        bool HasMainPlayer { get; }
        bool HasMainMapContainer { get; }
        int MainMapClassId { get; }
        int GridToRegion(int map,Pos2d position);
        MapRegionStatus GetMapRegionStatus(int map,int region);
        UniTask<bool> DoTransfer(IMiniMapMarkEntity entity);
        void DoEnterMap(int map,int loEntityId);
    }
}
