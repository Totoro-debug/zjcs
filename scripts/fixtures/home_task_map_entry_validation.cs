using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.HomeTaskUI;
using Zjcs.Official.HomeTaskUI;

public static class HomeTaskMapEntryValidation
{
    public static void Run()
    {
        try{
            var data=OfflineHomeTaskMapEntryData.LoadDefault();var navigation=new HomeTaskMapEntryNavigation(data);
            var raw=JObject.Parse(Resources.Load<TextAsset>("Home/home-task-map-entry").text);
            var state=new State();int regions=0,entries=0,periodic=0;
            foreach(var region in raw["regions"]){
                int map=(int)region["ClassId"],id=(int)region["Region"];
                Require(navigation.FindRegionNearEntry(state,map,id)==data.GetMapPartInfo(map).DefEntryId,"no usable entry must use the original default");regions++;
            }
            foreach(var map in ((JObject)raw["entries"]).Properties())foreach(var region in ((JObject)map.Value).Properties()){
                int mapId=int.Parse(map.Name),regionId=int.Parse(region.Name);
                Require(data.TryGetMapRegionEntryDatas(mapId,regionId,out var rows),"original entry region exists");
                foreach(var entry in rows){
                    state.Status=HomeTaskMapRegionStatus.Private;state.Active.Clear();state.Count=0;
                    Require(!navigation.IsEntryUsable(state,mapId,entry,state.Active),"private inactive entry unavailable");
                    switch(entry.EntryType){
                        case HomeTaskMapEntryType.Entry:
                            state.Active.Add(entry.LoEntId);Require(navigation.IsEntryUsable(state,mapId,entry,state.Active),"saved activation enables normal entry");
                            Require(navigation.FindRegionNearEntry(state,mapId,regionId)==entry.LoEntId,"actual activated entry is selected in its original region");break;
                        case HomeTaskMapEntryType.ActivedEntry:
                        case HomeTaskMapEntryType.WayPoint:
                            state.Status=HomeTaskMapRegionStatus.Public;Require(navigation.IsEntryUsable(state,mapId,entry,null),"public region enables original active entry/waypoint");break;
                        case HomeTaskMapEntryType.PeriodicityInteractionWayPoint:
                            state.Status=HomeTaskMapRegionStatus.Public;int group=data.GetPeriodicityInteractionId(mapId,entry.LoEntId);
                            if(group>0){
                                int max=data.GetPeriodicity(group).PeriodicityMaxCount;
                                state.Count=max-1;Require(!navigation.IsEntryUsable(state,mapId,entry,null),"periodic waypoint below count limit unavailable");
                                state.Count=max;Require(navigation.IsEntryUsable(state,mapId,entry,null),"periodic waypoint at count limit enabled");
                                Require(data.CheckCanInteraction(group,max)==2155&&data.CheckCanInteraction(group,max-1)==0,"original periodic return codes");
                                Require(data.CheckCanInteraction(group,max,true)==0&&data.CheckCanInteraction(group,max-1,true)==2171,"original negation return codes");
                            }else Require(navigation.IsEntryUsable(state,mapId,entry,null),"absent periodic group uses original true branch");
                            periodic++;break;
                        default:throw new Exception("Selected unexpected entry type");
                    }
                    entries++;
                }
                // Caller mutation cannot change official entry ordering or identifiers.
                int original=(int)region.Value[0][0];rows[0].LoEntId=-1;rows.Clear();
                Require(data.TryGetMapRegionEntryDatas(mapId,regionId,out rows)&&rows[0].LoEntId==original,"entry results are isolated copies");
            }
            Require(periodic==0&&regions>60&&entries>60,"all three actual task target maps covered");
            Require(data.CheckCanInteraction(int.MaxValue,0)==2157,"missing periodic rule original error");
            Require(navigation.FindRegionNearEntry(state,int.MaxValue,0)==-1,"missing map original minus one");
            state.Status=HomeTaskMapRegionStatus.Private;state.Count=0;state.Active.Clear();
            var controlled=new OrderedData();var ordered=new HomeTaskMapEntryNavigation(controlled);
            state.Active.AddRange(new[]{201,301});
            Require(ordered.FindRegionNearEntry(state,10,0)==201,"breadth first search preserves adjacent order and entry list order through cycle");
            state.Active.Clear();state.Active.Add(301);Require(ordered.FindRegionNearEntry(state,10,0)==301,"search crosses locked region and terminates cyclic graph");
            var waypoint=new HomeTaskMapSingleEntry{LoEntId=400,Region=1,EntryType=HomeTaskMapEntryType.PeriodicityInteractionWayPoint};
            state.Status=HomeTaskMapRegionStatus.Private;state.Count=2;Require(!ordered.IsEntryUsable(state,10,waypoint,null),"periodic waypoint still requires public region");
            state.Status=HomeTaskMapRegionStatus.Public;state.Count=1;Require(!ordered.IsEntryUsable(state,10,waypoint,null),"periodic waypoint below limit is unusable");
            state.Count=2;Require(ordered.IsEntryUsable(state,10,waypoint,null),"periodic waypoint at limit is usable");
            Require(controlled.CheckCanInteraction(7,1)==0&&controlled.CheckCanInteraction(7,2)==2155&&controlled.CheckCanInteraction(7,1,true)==2171&&controlled.CheckCanInteraction(7,2,true)==0,"original count and negation result boundaries");
            waypoint.LoEntId=401;state.Count=0;Require(ordered.IsEntryUsable(state,10,waypoint,null),"missing periodic group allows public waypoint");
            state.Status=HomeTaskMapRegionStatus.Private;state.Active.Clear();
            var route=new Route();state.LastRegion=2;
            Require(!navigation.GotoMapRegion(null,route,10,0)&&route.Requests.Count==0,"null original player does not enter");
            Require(navigation.GotoMapRegion(state,route,10,0)&&route.Requests.Single()==(10,data.GetMapPartInfo(10).DefEntryId),"unredirected original region request selects real default entry");
            route.Requests.Clear();route.GroupMap=11;route.Current=11;
            Require(!navigation.GotoMapRegion(state,route,10,0)&&route.Requests.Count==0&&route.Messages.Single()=="PLAYER_MAP_GROUP_GOTO_TARGET_MAP_IS_SAME","already on group current map broadcasts and stops");
            route.Current=0;state.LastRegion=null;
            Require(navigation.GotoMapRegion(state,route,10,4)&&route.Requests.Last()==(11,data.GetMapPartInfo(11).DefEntryId),"redirected map falls back from region zero to selected original default");
            // Use distinguishable activated entries to prove last region is used, rather than requested region.
            state.LastRegion=2;state.Active.Add(301);route.GroupMap=10;route.Current=0;route.Requests.Clear();
            Require(ordered.GotoMapRegion(state,route,11,0)&&route.Requests.Single()==(10,301),"redirected route starts at actual last entry region");
            bool rejected=false;try{OfflineHomeTaskMapEntryData.Load("{}",Resources.Load<TextAsset>("Home/home-task-map-entry-hash").text);}catch(FormatException){rejected=true;}
            Require(rejected,"changed selected entry payload rejected");
            Debug.Log("HOME_TASK_MAP_ENTRY_PASSED: three original task target maps; "+regions+" regions; "+entries+" entries; original breadth-first/list order and cyclic graph; periodic waypoint/negation boundary using explicit state fixture; saved-state ports and group route requests. No actual map entry or production HUD claim.");
            EditorApplication.Exit(0);
        }catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    sealed class State:IHomeTaskMapEntryState
    {
        public readonly List<int> Active=new List<int>();public HomeTaskMapRegionStatus Status;public int Count;public int? LastRegion;
        public List<int> GetActivedMapEntries(int map)=>Active;
        public HomeTaskMapRegionStatus GetMapRegionStatus(int map,int region)=>Status;
        public int GetWorldPeriodicityInteractionCount(int group)=>Count;
        public bool TryGetLastMapEntry(int map,out int region){region=LastRegion??0;return LastRegion.HasValue;}
    }
    sealed class Route:IHomeTaskMapEntryRoute
    {
        public int GroupMap,Current;public int CurrentMapClassId=>Current;
        public readonly List<(int,int)> Requests=new List<(int,int)>();public readonly List<string> Messages=new List<string>();
        public int GetGroupCurrentMap(int map)=>GroupMap==0?map:GroupMap;
        public void Broadcast(string key)=>Messages.Add(key);
        public void DoEnterMap(int map,int entry)=>Requests.Add((map,entry));
    }
    sealed class OrderedData:IHomeTaskMapEntryData
    {
        public HomeTaskMapPart GetMapPartInfo(int map)=>new HomeTaskMapPart{DefEntryId=999};
        public HomeTaskMapEntryRegion Get(int map,int region)=>new HomeTaskMapEntryRegion{AdjacentRegions=region==0?new[]{1,2}:new[]{0}};
        public bool TryGetMapRegionEntryDatas(int map,int region,out List<HomeTaskMapSingleEntry> rows){
            rows=region==0?null:new List<HomeTaskMapSingleEntry>{new HomeTaskMapSingleEntry{LoEntId=region==1?200:300,Region=region,EntryType=HomeTaskMapEntryType.Entry},new HomeTaskMapSingleEntry{LoEntId=region==1?201:301,Region=region,EntryType=HomeTaskMapEntryType.Entry}};return rows!=null;
        }
        public int GetPeriodicityInteractionId(int map,int id)=>id==400?7:-1;
        public HomeTaskMapPeriodicity GetPeriodicity(int group)=>group==7?new HomeTaskMapPeriodicity{PeriodicityMaxCount=2}:null;
        public int CheckCanInteraction(int group,int count,bool negation=false)=>new HomeTaskMapPeriodicityRules(this).CheckCanInteraction(group,count,negation);
    }
}
