using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.World.Map13MiniMap
{
    // Consumed original MiniMap/resource/session surfaces. The full original
    // MiniMap supplies these from its actual controller and local map state.
    public abstract class MiniMapRegionContext : MonoBehaviour
    {
        public abstract int MapClassId { get; }
        public abstract int CurrentMapClassId { get; }
        public abstract bool HasMainPlayer { get; }
        public abstract IMapViewRangeHandler ViewRangeHandler { get; }
        public abstract event Action<IMapViewRangeHandler> OnVisibleRangeUpdate;
        public abstract event Action<int,int,MapRegionStatus,MapRegionStatus> OnMapStatusUpdate;
        public abstract List<MapRegionStatus> GetMapStatusListInTeam(int mapClassId);
        public abstract bool IsRangeInViewRange(Pos2d min,Pos2d max);
        public abstract IMiniMapRegionAssetLease LoadAssetByGuid(string guid);
        public abstract GameObject InstantiateRegion(GameObject prefab,Transform parent,bool worldPositionStays);
    }
    public interface IMiniMapRegionAssetLease : IDisposable { GameObject Result { get; } }
    internal sealed class MiniMapRegionAssetBag : IDisposable
    {
        private readonly List<IMiniMapRegionAssetLease> leases=new List<IMiniMapRegionAssetLease>();
        public IMiniMapRegionAssetLease Add(IMiniMapRegionAssetLease lease){leases.Add(lease);return lease;}
        public void Dispose(){foreach(var lease in leases)lease.Dispose();leases.Clear();}
    }
}
