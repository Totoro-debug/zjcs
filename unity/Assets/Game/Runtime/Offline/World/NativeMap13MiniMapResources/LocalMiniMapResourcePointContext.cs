using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeMap13Labels;
using MapRegionStatus=Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;
using LabelRegionStatus=Zjcs.Official.World.NativeMap13Labels.MapRegionStatus;

namespace Zjcs.Offline.World.NativeMap13MiniMapResources
{
    public sealed class LocalMiniMapResourcePointContext:MiniMapResourcePointContext,IDisposable
    {
        [Serializable] private sealed class Group{public int Id;public string Icon;}
        [Serializable] private sealed class LocationData{public int[] RuleIds;}
        [Serializable] private sealed class ItemLocation{public int ItemId;public LocationData Data;}
        [Serializable] private sealed class WorldLocation{public int Id,Type;public int[] RefreshGroupSet;}
        [Serializable] private sealed class RegionRow{public int ClassId,Region;public int[] Areas;}
        [Serializable] private sealed class Data{public Group[] Groups;public ItemLocation[] ItemLocations;public WorldLocation[] WorldLocations;public RegionRow[] Regions;}
        private sealed class Lease<T>:IResourcePointAssetLease<T> where T:UnityEngine.Object
        {
            public T Result{get;private set;}
            public Lease(T result){Result=result;}
            // The serialized catalog owns the native assets for the map lifetime.
            public void Dispose(){Result=null;}
        }
        private readonly Dictionary<int,MiniMapResourcePointEntityGroupInfo> groups=new Dictionary<int,MiniMapResourcePointEntityGroupInfo>();
        private readonly Dictionary<int,MiniMapResourcePointItemLocationData> itemLocations=new Dictionary<int,MiniMapResourcePointItemLocationData>();
        private readonly Dictionary<int,MiniMapResourcePointRefreshGroupInfo> awardLocations=new Dictionary<int,MiniMapResourcePointRefreshGroupInfo>(),monsterLocations=new Dictionary<int,MiniMapResourcePointRefreshGroupInfo>();
        private readonly Dictionary<int,int> areaRegions=new Dictionary<int,int>();
        private readonly Dictionary<string,GameObject> prefabs=new Dictionary<string,GameObject>(StringComparer.Ordinal);
        private readonly Dictionary<string,Sprite> icons=new Dictionary<string,Sprite>(StringComparer.Ordinal);
        private readonly Func<string,Sprite> additionalSpriteResolver;
        private readonly IMap13LabelPlayer player;
        private bool disposed;
        public int MapClassId=>13;
        public bool HasMainPlayer=>player!=null;
        public IMap13LabelServices LabelServices{get;}
        public event Action<int,int,MapRegionStatus,MapRegionStatus> OnMapStatusUpdate;

        public LocalMiniMapResourcePointContext(MiniMapResourcePointAssets assets,IMap13LabelServices labels,Func<string,Sprite> additionalSpriteResolver=null)
        {
            if(!assets||!assets.SourceData)throw new ArgumentException("Map13 original resource-point assets are required",nameof(assets));
            LabelServices=labels??throw new ArgumentNullException(nameof(labels));this.additionalSpriteResolver=additionalSpriteResolver;player=labels.Player;
            var data=JsonUtility.FromJson<Data>(assets.SourceData.text);
            foreach(var g in data.Groups)groups.Add(g.Id,new MiniMapResourcePointEntityGroupInfo{Icon=g.Icon});
            foreach(var item in data.ItemLocations)itemLocations.Add(item.ItemId,item.Data==null?null:new MiniMapResourcePointItemLocationData{RuleIds=item.Data.RuleIds});
            foreach(var location in data.WorldLocations)
            {
                var value=new MiniMapResourcePointRefreshGroupInfo{RefreshGroupSet=location.RefreshGroupSet};
                // Official RefreshGroupLocationInfoType: Monster=1, AwardRule=2.
                if(location.Type==1)monsterLocations.Add(location.Id,value);
                else if(location.Type==2)awardLocations.Add(location.Id,value);
            }
            foreach(var region in data.Regions)
            {
                if(region.ClassId!=MapClassId)throw new InvalidOperationException("Wrong original map in resource-point region catalog");
                foreach(int area in region.Areas)areaRegions.Add(area,region.Region);
            }
            foreach(var board in assets.Boards)
            {
                if(!board.Prefab)throw new InvalidOperationException("Missing original resource board: "+board.RefreshGroup);
                prefabs.Add(board.OriginalGuid,board.Prefab);
            }
            foreach(var icon in assets.Icons)
            {
                if(!icon.Sprite)throw new InvalidOperationException("Missing original resource-point icon: "+icon.Path);
                icons.Add(icon.Path,icon.Sprite);
            }
            if(player!=null)player.OnMapStatusUpdate+=StatusUpdated;
        }
        private void StatusUpdated(int map,int region,LabelRegionStatus previous,LabelRegionStatus current)=>OnMapStatusUpdate?.Invoke(map,region,(MapRegionStatus)(int)previous,(MapRegionStatus)(int)current);
        public MiniMapResourcePointEntityGroupInfo GetRefreshGroupInfo(int group){groups.TryGetValue(group,out var value);return value;}
        public int AreaToRegion(int mapClassId,int area)=>mapClassId==MapClassId&&areaRegions.TryGetValue(area,out int region)?region:-1;
        public MapRegionStatus GetMapRegionStatus(int mapClassId,int region)=>(MapRegionStatus)(int)player.GetMapRegionStatus(mapClassId,region);
        public MiniMapResourcePointItemLocationData GetItemLocationInfo(int itemId){itemLocations.TryGetValue(itemId,out var value);return value;}
        public MiniMapResourcePointRefreshGroupInfo GetRefreshGroupsByAwardRule(int ruleId){awardLocations.TryGetValue(ruleId,out var value);return value;}
        public MiniMapResourcePointRefreshGroupInfo GetRefreshGroupsByMonsterId(int monsterId){monsterLocations.TryGetValue(monsterId,out var value);return value;}
        public IResourcePointAssetLease<GameObject> LoadPrefabByGuid(string guid)
        {
            if(!prefabs.TryGetValue(guid,out var prefab))throw new InvalidOperationException("Original resource-board GUID is not bound: "+guid);
            return new Lease<GameObject>(prefab);
        }
        public IResourcePointAssetLease<Sprite> LoadSprite(string path)
        {
            if(!icons.TryGetValue(path,out var sprite))sprite=additionalSpriteResolver?.Invoke(path);
            if(!sprite)throw new InvalidOperationException("Original minimap resource-point Sprite is not bound: "+path);
            return new Lease<Sprite>(sprite);
        }
        public GameObject InstantiatePrefab(GameObject prefab,Transform parent,bool worldPositionStays)=>UnityEngine.Object.Instantiate(prefab,parent,worldPositionStays);
        public void Dispose(){if(disposed)return;disposed=true;if(player!=null)player.OnMapStatusUpdate-=StatusUpdated;OnMapStatusUpdate=null;}
    }
}
