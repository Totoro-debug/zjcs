using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.World.NativeMap13Labels;
using MapRegionStatus=Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;
namespace Zjcs.Official.World.Map13MiniMap
{
    // The original configuration, player-state and resource-loader surfaces.
    // Concrete map integration supplies the selected local official data.
    public interface MiniMapResourcePointContext
    {
        int MapClassId { get; }
        bool HasMainPlayer { get; }
        IMap13LabelServices LabelServices { get; }
        event Action<int,int,MapRegionStatus,MapRegionStatus> OnMapStatusUpdate;
        MiniMapResourcePointEntityGroupInfo GetRefreshGroupInfo(int refreshGroup);
        int AreaToRegion(int mapClassId,int area);
        MapRegionStatus GetMapRegionStatus(int mapClassId,int region);
        MiniMapResourcePointItemLocationData GetItemLocationInfo(int itemId);
        MiniMapResourcePointRefreshGroupInfo GetRefreshGroupsByAwardRule(int ruleId);
        MiniMapResourcePointRefreshGroupInfo GetRefreshGroupsByMonsterId(int monsterId);
        IResourcePointAssetLease<GameObject> LoadPrefabByGuid(string guid);
        IResourcePointAssetLease<Sprite> LoadSprite(string path);
        GameObject InstantiatePrefab(GameObject prefab,Transform parent,bool worldPositionStays);
    }
    // Projections retain the consumed original nullable object/field boundaries.
    public sealed class MiniMapResourcePointEntityGroupInfo {public string Icon;}
    public sealed class MiniMapResourcePointItemLocationData {public IReadOnlyList<int> RuleIds;}
    public sealed class MiniMapResourcePointRefreshGroupInfo {public IEnumerable<int> RefreshGroupSet;}
    public interface IResourcePointAssetLease<out T>:IDisposable where T:UnityEngine.Object {T Result {get;}}
    internal struct MiniMapResourcePointDisposableBag:IDisposable
    {
        private List<IDisposable> leases;
        public void Add(IDisposable lease){if(leases==null)leases=new List<IDisposable>();leases.Add(lease);}
        public void Dispose(){if(leases==null)return;foreach(var lease in leases)lease.Dispose();leases.Clear();}
    }
    internal static class MiniMapResourcePointDisposable
    {
        public static T AddTo<T>(T lease,ref MiniMapResourcePointDisposableBag bag) where T:IDisposable {bag.Add(lease);return lease;}
    }
}
