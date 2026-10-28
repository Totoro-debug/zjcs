using System;
using UnityEngine;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.Map13Entities;
using Pos2d = Zjcs.Official.World.NativeTerrain.Common.Pos2d;
namespace Zjcs.Official.MapDisplays
{
    // Required consumed state/query/resource inputs. These supply no map,
    // server-disable, ownership or shared entity transition rules.
    public interface IMapDisplayRoot
    {
        Transform MapRoot { get; }
        int CurrentMapClassId { get; }
        bool Destroying { get; }
        GroundType GetGridGroundType(Pos2d position);
    }
    public interface IMapDisplayQueries
    {
        EntityMapDisplayInfoParser Selector { get; }
        int GetMapGroupId(int mapClassId);
        string GetAreaTag(int mapClassId, int areaId);
        bool IsMapForbidLowMemoryDeal();
    }
    public interface IMapDisplayPosition { int QueryArea(Pos2d position); }
    public interface IMapDisplayEntityState
    {
        bool Removing { get; }
        Pos2d GridPosition { get; }
        string EntityTag { get; }
    }
    public interface IMyEntityBindPreEventReceiver : IEventReceiver
    { void MyEntity_OnBindPre(ECViewEntityData entity); }
    public interface IMapDisplayEntityEvents
    {
        void AddBindPreReceiver(IMyEntityBindPreEventReceiver receiver);
        void RemoveBindPreReceiver(IMyEntityBindPreEventReceiver receiver);
    }
    public abstract class MapDisplayAssetLease : IDisposable
    {
        public abstract bool IsValid { get; }
        public abstract int Status { get; }
        public abstract GameObject Result { get; }
        public abstract void Dispose();
    }
    public interface IMapDisplayResourceLoader { MapDisplayAssetLease LoadPrefab(string path); }
    public interface IMapDisplayInstantiation { GameObject Instantiate(GameObject prefab); }
    public static class MapDisplayServices
    {
        public static GameObject Instantiate(IServiceProvider provider, GameObject prefab) =>
            (provider as IMapDisplayInstantiation ?? throw new InvalidOperationException("Original map-display injection/instantiation is not bound.")).Instantiate(prefab);
        public static IMapDisplayEntityState State(ECViewEntityData data) => data as IMapDisplayEntityState ??
            throw new InvalidOperationException("Original map-display shared entity state is not bound.");
    }
}
