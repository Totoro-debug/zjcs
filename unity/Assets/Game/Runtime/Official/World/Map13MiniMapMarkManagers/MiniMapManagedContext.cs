using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13Entities;
namespace Zjcs.Official.World.Map13MiniMap
{
    public interface IMiniMapManagedStage:IMiniMapMarkStage
    { IMiniMapSpawnEntity Entity { get; } EcEntityWorldStageComponentStateUpdateEventDelegate StateUpdateEvent { get;set; } }
    public interface IMiniMapManagedGridMover { }
    public interface IMiniMapManagedEntitySource { IMiniMapSpawnEntity Entity { get; } EntityOwnershipData Ownership { get; } }
    public interface IMiniMapManagedAreaAgent
    { Dictionary<EcEntityIdentifier,IMiniMapSpawnEntity> GetAreaEntity(int area);int GridToRegion(int map,Pos2d position); }
    public sealed class MiniMapManagedEvents
    {
        public EcContainerAddEntityEventDelegate AddEntityEvent=new EcContainerAddEntityEventDelegate();
        public EcContainerRemoveEntityEventDelegate RemoveEntityEvent=new EcContainerRemoveEntityEventDelegate();
        public EcContainerReplaceEntityEventDelegate ReplaceEntityEvent=new EcContainerReplaceEntityEventDelegate();
        public event Action<int,int,MapRegionStatus,MapRegionStatus> OnMapStatusUpdate;
        public void FireMapStatusUpdate(int map,int region,MapRegionStatus previous,MapRegionStatus value)=>OnMapStatusUpdate?.Invoke(map,region,previous,value);
    }
    public interface IMiniMapManagedQueries
    {
        MiniMapManagedMapPartInfo GetMapPartInfo(int map);
        List<MapRegionStatus> GetCurrentMapStatus();
        bool TryGetStatusByContainerId(long container,out MapRegionStatus status);
        IMiniMapSpawnEntity GetEntity(EcEntityIdentifier id);
        IEnumerable<MiniMapMarkLoEntityInfo> GetEntityByMapRegion(int map,int region);
        long MainPlayerId { get; }
    }
    public abstract class MiniMapManagedContext:MiniMapSpawnContext
    {
        public override string GetMapEntityMiniMapMark(IMiniMapSpawnEntity entity,int group,int map,string tag)=>MiniMapManagedRules.GetMapEntityMiniMapMark(this,entity,group,map,tag);
        public abstract IMiniMapManagedQueries ManagedQueries { get; }
        public abstract MiniMapManagedEvents ManagedEvents { get; }
        public abstract IMiniMapManagedAreaAgent ManagedAreaAgent { get; }
        public abstract IMiniMapMarkGridGround ManagedGridGround { get; }
        public abstract void InstantiateMarkAsync(string path,Transform parent,Action<MiniMapMarkUIBase> callback);
    }
}
