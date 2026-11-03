using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Offline.World.MainlineMiniMap;

namespace Zjcs.Editor.Map13MiniMap
{
    public static partial class OfficialMainlineMiniMapRootAcceptance
    {
        private sealed class LocalDataQueries : IMiniMapMarkDataQueries
        {
            internal readonly Dictionary<int, MiniMapMarkLoEntityInfo> Rows = new Dictionary<int, MiniMapMarkLoEntityInfo>();
            internal readonly List<int> Classes = new List<int>();
            public MiniMapMarkExtendInfo GetEntityExtend(int entity) => new MiniMapMarkExtendInfo
            { CompleteState=3, MiniMapMarkHideInEnd=entity!=9999, MiniMapMarkShowInPublic=true, MiniMapMarkShowInOffline=true, MiniMapMark="local-policy-marker-"+entity };
            public MiniMapMarkLoEntityInfo GetContainerEntityInfo(int map,int entity) => Rows[entity];
            public MiniMapMarkMapInfo GetMapInfo(int map) => new MiniMapMarkMapInfo { MapGroupId=1 };
            public string GetAreaTag(int map,int area) => "fixture-area";
            public string GetEntityTag(int map,int entity) => "fixture-entity";
            public EntityMapUIDisplayInfo GetEntityMapUIDisplay(int entity,int group,int map,string area,string tag)
            { Classes.Add(entity);return null; }
            public int GridToArea(int map,Pos2d position) => throw new NotSupportedException();
            public IMiniMapMarkEntity GetCommonPrefab(int entity) => throw new NotSupportedException();
            public Vector3 MapGridToPos(int map,Pos2d position) => throw new NotSupportedException();
            public MiniMapMarkTransportTargetInfo GetMapTransportTargetInfo(int map,int entity) => throw new NotSupportedException();
            public bool RegionIsActived(int map,int region) => throw new NotSupportedException();
            public int QueryRegion(Pos2d position) => throw new NotSupportedException();
            public int CurrentMeMapId=>16;public bool HasMainPlayer=>true;public bool HasMainMapContainer=>true;public int MainMapClassId=>16;
            public int GridToRegion(int map,Pos2d position) => throw new NotSupportedException();
            public MapRegionStatus GetMapRegionStatus(int map,int region) => throw new NotSupportedException();
            public UniTask<bool> DoTransfer(IMiniMapMarkEntity entity) => throw new NotSupportedException();
            public void DoEnterMap(int map,int entity) => throw new NotSupportedException();
        }

        private static IEnumerator VerifyLocalData(MainlineMiniMapRootStateFixture state, Queries queries,
            MapGroundGridBoolData bits)
        {
            var path=Path.Combine(Workspace,"local-minimap-snapshot.json");
            var snapshot=new LocalMiniMapSnapshot
            {
                MapClassId=13,Width=bits.Width,Height=bits.Height,Visited=true,
                GridVisibility=GridVisibilityCompressUtils.DoMyZip(bits),PublicRegions=new List<int>{38},
                // Deliberate test states, not replacements for official data.
                Entities=new List<LocalMiniMapEntity>
                {
                    new LocalMiniMapEntity{PlacementId=1,EntityClassId=1001,Region=0,State=1,Explored=true},
                    new LocalMiniMapEntity{PlacementId=2,EntityClassId=1002,Region=0,State=1},
                    new LocalMiniMapEntity{PlacementId=3,EntityClassId=1003,Region=38,State=1},
                    new LocalMiniMapEntity{PlacementId=4,EntityClassId=1004,Region=38,State=3},
                    new LocalMiniMapEntity{PlacementId=5,EntityClassId=1005,Region=0,State=3,Explored=true}
                }
            };
            File.WriteAllText(path,JsonUtility.ToJson(snapshot));
            var source=new LocalMiniMapDataSource(map=>JsonUtility.FromJson<LocalMiniMapSnapshot>(File.ReadAllText(path)));
            PlayerOfflineMiniMapDataResponse response=null;int completions=0;
            source.Request(13).OnComplete+=(value,error)=>{Require(error==0,"Local response succeeds");response=value;completions++;};
            Require(response==null,"Local completion allows original subscriber attachment");
            for(int i=0;i<4&&response==null;i++)yield return null;
            Require(completions==1&&response!=null&&response.UseLatestLocalEntityStates,"One deferred local completion");
            Require(response.LiteEntityDataDict.Keys.OrderBy(id=>id).SequenceEqual(new[]{1,3,4,5}),"Explored or public entities only; never reveal unexplored private entities");
            Require(response.GridVisibilityByte.SequenceEqual(snapshot.GridVisibility)&&response.ActiveRegions.SequenceEqual(new[]{38}),"Cold local read preserves original fog bytes and public regions");
            Require(snapshot.Entities.Single(e=>e.PlacementId==2).Explored==false,"Browsing never marks an entity explored");

            var dataQueries=new LocalDataQueries();
            foreach(var entity in snapshot.Entities)dataQueries.Rows.Add(entity.PlacementId,new MiniMapMarkLoEntityInfo
            {LoEntityId=entity.PlacementId,DefSpawnEntityInfo=new MiniMapMarkSpawnEntityInfo{ClassId=9999},PosInfo=new MiniMapMarkPositionInfo{Region=entity.Region,Area=0}});
            var previous=state.Queries;state.Queries=dataQueries;
            queries.RegionEntities=(map,region)=>dataQueries.Rows.Values.Where(row=>row.PosInfo.Region==region);
            var holder=new GameObject("OriginalLocalOfflineMarkerPolicy");
            var manager=holder.AddComponent<OfflineMiniMapMarkManager>();manager.MiniMap=state;manager.EntitySlot=holder.transform;
            manager.Init(13,response);
            Require(state.Pending.Count==2&&dataQueries.Classes.SequenceEqual(new[]{1001,1003}),"Original marker selection uses latest classes, hides completed marks and skips public default recreation");
            // Capture creation decisions before asynchronous prefab callbacks;
            // original prefab rendering is exercised by the native root/HUD tests.
            state.Pending.Clear();dataQueries.Classes.Clear();
            snapshot.Entities[0].EntityClassId=1011;snapshot.Entities[0].State=3;
            snapshot.Entities[2].State=2;
            File.WriteAllText(path,JsonUtility.ToJson(snapshot));response=null;
            source.Request(13).OnComplete+=(value,error)=>response=value;
            for(int i=0;i<4&&response==null;i++)yield return null;
            Require(response!=null&&response.LiteEntityDataDict[1].EntityClassId==1011&&response.LiteEntityDataDict[3].State==2,"Each browse reads the latest persisted type/state");
            manager.Init(13,response);Require(state.Pending.Count==1&&dataQueries.Classes.SequenceEqual(new[]{1003}),"Revisit applies refreshed completion visibility");state.Pending.Clear();
            var island=JsonUtility.FromJson<Fixture>(File.ReadAllText(Path.Combine(Workspace,"minimap-description-fixture-16.json")));
            var islandSnapshot=new LocalMiniMapSnapshot{MapClassId=16,Width=island.width,Height=island.height,Visited=true,
                GridVisibility=GridVisibilityCompressUtils.DoMyZip(new MapGroundGridBoolData(island.height,island.width)),
                PublicRegions=new List<int>{38},Entities=snapshot.Entities};
            var islandPath=Path.Combine(Workspace,"local-minimap-snapshot-16.json");File.WriteAllText(islandPath,JsonUtility.ToJson(islandSnapshot));
            var islandSource=new LocalMiniMapDataSource(map=>JsonUtility.FromJson<LocalMiniMapSnapshot>(File.ReadAllText(islandPath)));
            PlayerOfflineMiniMapDataResponse islandResponse=null;islandSource.Request(16).OnComplete+=(value,error)=>islandResponse=value;
            for(int i=0;i<4&&islandResponse==null;i++)yield return null;
            Require(islandResponse!=null&&islandResponse.MapClassId==16&&islandResponse.LiteEntityDataDict.Keys.OrderBy(id=>id).SequenceEqual(new[]{1,3,4,5}),"The same source selects explored/public entities for a second real map configuration");
            Require(GridVisibilityCompressUtils.DoMyUnzip(islandResponse.GridVisibilityByte).Width==140&&response.MapClassId==13,"Foreign map responses retain their independent original grid sizes");
            bool rejected=false;try{source.Request(16);}catch(InvalidDataException){rejected=true;}Require(rejected,"Foreign save identity rejected");
            snapshot.Visited=false;File.WriteAllText(path,JsonUtility.ToJson(snapshot));rejected=false;try{source.Request(13);}catch(InvalidDataException){rejected=true;}Require(rejected,"Unvisited maps cannot provide local entity data");
            snapshot.Visited=true;snapshot.GridVisibility=new byte[]{0};File.WriteAllText(path,JsonUtility.ToJson(snapshot));rejected=false;try{source.Request(13);}catch(InvalidDataException){rejected=true;}Require(rejected,"Corrupt local visibility is rejected without revealing the map");
            state.Queries=previous;queries.RegionEntities=null;UnityEngine.Object.Destroy(holder);yield return null;
            Debug.Log("MAINLINE_MINIMAP_LOCAL_DATA_PASS");
        }
    }
}
