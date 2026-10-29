using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Bodies=Zjcs.Offline.World.NativeMap13GridVisibility.OfflineMap13GridVisibility;

namespace Zjcs.Editor.Map13MiniMap
{
    public static class OfficialMap13MiniMapMarkDataAcceptance
    {
        private const string Key="Zjcs.Map13.MarkData.Running";
        private static int errors;
        private static void Require(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
        private sealed class Entity:IMiniMapMarkEntity
        {
            public object Original{get;}=new object();public int ClassId{get;set;}public int ContainerClassId=>17;
            public IMiniMapMarkGrid GridTransform{get;set;}public readonly Dictionary<Type,object> Components=new Dictionary<Type,object>();
            public T GetComponent<T>() where T:class=>Components.TryGetValue(typeof(T),out var value)?(T)value:null;
            public string GetEntityTag()=>"original-tag";
        }
        private sealed class Grid:IMiniMapMarkGrid
        {
            public IMiniMapMarkEntity Entity{get;set;}public GridTransformComponentInfo Info{get;set;}public Pos2d Pos{get;set;}
            public List<Pos2d> GetBodyRange()=>Info.GetBodyRange();
            public Vector3 GridToPosition(Pos2d p)=>new Vector3(p.X-110.5f+Info.RealCenter.x,p.Y-145.5f+Info.RealCenter.y,0);
        }
        private sealed class Stage:IMiniMapMarkStage
        {public int State{get;set;}public bool Active;public bool StateEqual(int state)=>State==state;public bool IsActive()=>Active;}
        private sealed class Transport:IMiniMapMarkStageTransport{public int TargetLoEntityId{get;set;}}
        private sealed class Entry:IMiniMapMarkWorldEntry
        {public IMiniMapMarkEntity Entity{get;set;}public IMiniMapMarkGrid GridTransform=>Entity.GridTransform;public bool Active{get;set;}}
        private sealed class WayPoint:IMiniMapMarkWayPoint
        {public IMiniMapMarkEntity Entity{get;set;}public Queries Queries;public bool IsUsable()=>MiniMapMarkWayPointRules.IsEntityUsable(Queries,Entity.GridTransform.Pos);}
        private sealed class Queries:IMiniMapMarkDataQueries,IServiceProvider
        {
            public Entity Common;public readonly Dictionary<int,MiniMapMarkLoEntityInfo> Wraps=new Dictionary<int,MiniMapMarkLoEntityInfo>();
            public EntityMapUIDisplayInfo Display=new EntityMapUIDisplayInfo();public MiniMapMarkExtendInfo Extend=new MiniMapMarkExtendInfo{CompleteState=3,DialogIconPrefab="extend-icon"};
            public MiniMapMarkTransportTargetInfo Target;public MapRegionStatus Status=MapRegionStatus.Private;
            public int DisplayClass,DisplayMap,DisplayArea,EnteredMap,EnteredId,Transfers;public IMiniMapMarkEntity Transferred;
            public int GridToArea(int map,Pos2d position)=>61;public string GetAreaTag(int map,int area){DisplayArea=area;return "area-"+area;}
            public MiniMapMarkMapInfo GetMapInfo(int map)=>new MiniMapMarkMapInfo{MapGroupId=7};
            public EntityMapUIDisplayInfo GetEntityMapUIDisplay(int entityClassId,int mapGroupId,int map,string areaTag,string entityTag){Require(mapGroupId==7,"Original map group");DisplayClass=entityClassId;DisplayMap=map;return Display;}
            public MiniMapMarkExtendInfo GetEntityExtend(int entityClassId)=>Extend;
            public MiniMapMarkLoEntityInfo GetContainerEntityInfo(int map,int id)=>Wraps.TryGetValue(id,out var value)?value:null;
            public string GetEntityTag(int map,int id)=>"original-tag";public IMiniMapMarkEntity GetCommonPrefab(int id)=>Common;
            public Vector3 MapGridToPos(int map,Pos2d p)=>new Vector3(p.X-110.5f,p.Y-145.5f,0);
            public MiniMapMarkTransportTargetInfo GetMapTransportTargetInfo(int map,int id)=>Target;
            public bool RegionIsActived(int map,int region)=>Status==MapRegionStatus.Public;public int QueryRegion(Pos2d position)=>38;
            public int CurrentMeMapId{get;set;}=13;public bool HasMainPlayer{get;set;}=true;public bool HasMainMapContainer{get;set;}=true;public int MainMapClassId=>13;
            public int GridToRegion(int map,Pos2d position)=>38;public MapRegionStatus GetMapRegionStatus(int map,int region)=>Status;
            public UniTask<bool> DoTransfer(IMiniMapMarkEntity entity){Transfers++;Transferred=entity;return UniTask.FromResult(true);}
            public void DoEnterMap(int map,int id){EnteredMap=map;EnteredId=id;}
            public object GetService(Type type)=>type==typeof(IMiniMapMarkDataQueries)?this:null;
        }
        [Serializable] private sealed class Proof
        {public string editor;public int body_templates,placements,centroid_cases,factory_cases,state_cases,description_cases,transfer_cases;public bool original_relative_body_identity,original_null_branches,original_target_ids,original_live_offline_lookup,play_mode;}
        public static void Run()
        {Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");errors=0;Application.logMessageReceived+=Observe;SessionState.SetBool(Key,true);EditorApplication.playModeStateChanged+=State;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();}
        private static void Observe(string a,string b,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        [InitializeOnLoadMethod] private static void Resume(){if(SessionState.GetBool(Key,false)){errors=0;Application.logMessageReceived+=Observe;EditorApplication.playModeStateChanged+=State;}}
        private static void State(PlayModeStateChange value)
        {if(value!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;try{Verify();Require(errors==0,"Unexpected errors: "+errors);SessionState.EraseBool(Key);EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);SessionState.EraseBool(Key);EditorApplication.Exit(1);}}
        private static void Throws<T>(Action action) where T:Exception{try{action();}catch(T){return;}throw new InvalidDataException("Expected original "+typeof(T).Name);}
        private static void Verify()
        {
            var data=JsonUtility.FromJson<Bodies.BodyConfiguration>(Resources.Load<TextAsset>("Map13/grid-entities").text);
            Require(data.manifest=="871_156598"&&data.templates.Length==148&&data.entities.Length==2690,"Original complete body fixture");
            var templates=data.templates.ToDictionary(t=>t.classId,t=>t.body);foreach(var info in templates.Values)info.Init();
            var q=new Queries();int centroids=0;
            foreach(var placement in data.entities)
            {
                var info=templates[placement.classId];var entity=new Entity{ClassId=placement.classId};var grid=new Grid{Entity=entity,Info=info,Pos=placement.transform.Pos};entity.GridTransform=grid;q.Common=entity;
                q.Wraps[placement.placementId]=new MiniMapMarkLoEntityInfo{LoEntityId=placement.placementId,PosInfo=new MiniMapMarkPositionInfo{Pos=grid.Pos,Area=4,Region=38},DefSpawnEntityInfo=new MiniMapMarkSpawnEntityInfo{ClassId=entity.ClassId}};
                var live=new MiniMapEntityMark(13,38,entity,q);var offline=new MiniMapOfflineMark(13,placement.placementId,entity.ClassId,0,q);
                double x=info.BodyRangeInfos.Sum(p=>(double)p.Pos.X)/info.BodyRangeInfos.Count+grid.Pos.X-110.5;
                double y=info.BodyRangeInfos.Sum(p=>(double)p.Pos.Y)/info.BodyRangeInfos.Count+grid.Pos.Y-145.5;
                foreach(var mark in new MiniMapMarkBase[]{live,offline}){Require(Math.Abs(mark.LocalPosition.x-x)<.00003&&Math.Abs(mark.LocalPosition.y-y)<.00003,"Original body centroid at placement "+placement.placementId);Require(ReferenceEquals(mark.BodyRange,info.GetBodyRange()),"Original unrotated relative body identity");centroids++;}
            }
            var e=q.Common;var stage=new Stage();e.Components[typeof(IMiniMapMarkStage)]=stage;e.Components[typeof(IMiniMapMarkWayPoint)]=new WayPoint{Entity=e,Queries=q};var entry=new Entry{Entity=e};e.Components[typeof(IMiniMapMarkWorldEntry)]=entry;
            q.Wraps[91]=new MiniMapMarkLoEntityInfo{LoEntityId=92,PosInfo=new MiniMapMarkPositionInfo{Pos=e.GridTransform.Pos,Area=4,Region=38},DefSpawnEntityInfo=new MiniMapMarkSpawnEntityInfo{ClassId=999}};
            int factories=0;foreach(var kind in new[]{MiniMapMarkType.MarkOnly,MiniMapMarkType.Display,MiniMapMarkType.Track,MiniMapMarkType.WayPoint,MiniMapMarkType.Entry,(MiniMapMarkType)99})
            {
                var live=MiniMapEntityMark.Create(q,kind,13,38,e);var off=MiniMapOfflineMark.Create(q,kind,13,91,e.ClassId,0);
                Require(live.GetType()==(kind==MiniMapMarkType.Entry?typeof(MiniMapEntityEntryMark):kind==MiniMapMarkType.WayPoint?typeof(MiniMapEntityWayPointMark):typeof(MiniMapEntityMark)),"Live factory selection");
                Require(off.GetType()==(kind==MiniMapMarkType.Entry?typeof(MiniMapOfflineEntryMark):kind==MiniMapMarkType.WayPoint?typeof(MiniMapOfflineWayPointMark):typeof(MiniMapOfflineMark)),"Offline factory selection");factories+=2;
            }
            var lm=new MiniMapEntityMark(13,38,e,q);var om=new MiniMapOfflineMark(13,91,e.ClassId,0,q);int states=0;
            for(int s=-1;s<=4;s++)foreach(bool active in new[]{false,true}){stage.State=s;stage.Active=active;om.State=s;Require(lm.GetEntityState()==s&&om.GetEntityState()==s&&lm.EntityActive==(s==3)&&om.EntityActive==(s==3)&&lm.InteractPossible()==active&&om.InteractPossible()==(s==3),"Distinct complete and interact states");states++;}
            foreach(string displayIcon in new[]{null,"","display-icon"}){q.Display.DialogIconPrefab=displayIcon;Require(lm.GetDialogIconPrefabPath()==(string.IsNullOrEmpty(displayIcon)?"extend-icon":"display-icon")&&om.GetDialogIconPrefabPath()==lm.GetDialogIconPrefabPath(),"Original icon precedence");}q.Display.DialogIconPrefab=null;q.Extend.DialogIconPrefab=null;Require(lm.GetDialogIconPrefabPath()==null,"Empty icon fallback");
            var probe=new GameObject("IdentityProbe").AddComponent<Map13MarkIconProbe>();lm.InitDialogIconHandler(new IMapEntityDialogIconHandler[]{probe});Require(ReferenceEquals(probe.Entity,e.Original),"Actual original entity identity");om.InitDialogIconHandler(new IMapEntityDialogIconHandler[]{probe});var lite=(MapEntityDialogIconLiteEntityData)probe.Entity;Require(lite.LoEntityId==91&&lite.EntityClassId==e.ClassId&&lite.MapClassId==13&&lite.State==4,"Original offline lite payload");UnityEngine.Object.Destroy(probe.gameObject);
            q.Display.EntityNameKey="override-name";q.Display.EntityDescKey="override-desc";
            Require(lm.GetMarkNameKey()=="override-name"&&q.DisplayMap==17,"Live name uses entity container map");Require(lm.GetMarkDescTextArgKey().Key=="override-desc"&&q.DisplayMap==13,"Live description uses requested map");
            Require(om.GetMarkNameKey()=="override-name"&&q.DisplayClass==e.ClassId&&q.DisplayArea==61,"Offline name uses current position and requested entity class");Require(om.GetMarkDescTextArgKey().Key=="override-desc"&&q.DisplayClass==999&&q.DisplayArea==4,"Offline description uses original placement class and area");
            q.Display.EntityNameKey="";q.Display.EntityDescKey=null;Require(lm.GetMarkNameKey()=="entity_"+e.ClassId+"_name"&&om.GetMarkDescTextArgKey().Key=="entity_999_desc","Original localization fallback");
            Require(EntityDescParamsUtils.GetEntityDescParams(q,13,e)==null,"Missing transport has no parameters");var transport=new Transport();e.Components[typeof(IMiniMapMarkStageTransport)]=transport;Require(EntityDescParamsUtils.GetEntityDescParams(q,13,e).Single().KeyOrText=="Mainland_13","Official whole map name key");transport.TargetLoEntityId=-1;Require(EntityDescParamsUtils.GetEntityDescParams(q,13,e).Single().KeyOrText=="Mainland_13","Nonpositive target branch");transport.TargetLoEntityId=int.MaxValue;Require(EntityDescParamsUtils.GetEntityDescParams(q,13,e)==null,"Missing target preserves null result");transport.TargetLoEntityId=91;Require(EntityDescParamsUtils.GetEntityDescParams(q,13,e).Single().KeyOrText=="region_13_38","Live target region parameter");
            Require(LoEntityDescParamsUtils.GetLoEntityDescParams(q,13,q.Wraps[91])==null,"Absent offline transport target");q.Target=new MiniMapMarkTransportTargetInfo{TargetMapClassId=14,TargetRegion=27};Require(LoEntityDescParamsUtils.GetLoEntityDescParams(q,13,q.Wraps[91]).Single().KeyOrText=="region_14_27","Offline destination map and region");
            var lw=new MiniMapEntityWayPointMark(13,38,e,q);var ow=new MiniMapOfflineWayPointMark(13,91,e.ClassId,0,q);var le=new MiniMapEntityEntryMark(13,38,e,q);var oe=new MiniMapOfflineEntryMark(13,91,e.ClassId,0,q);
            foreach(MapRegionStatus status in Enum.GetValues(typeof(MapRegionStatus))){q.Status=status;Require(lw.InteractPossible()==(status==MapRegionStatus.Public)&&ow.InteractPossible()==(status==MapRegionStatus.Public),"Waypoint requires Public region");entry.Active=false;Require(le.InteractPossible()==(status==MapRegionStatus.Public),"Entry activated region");entry.Active=true;Require(le.InteractPossible(),"Entry entity active fallback");}
            q.HasMainPlayer=false;Require(!lw.InteractPossible()&&!ow.InteractPossible(),"Absent player disables waypoints");q.HasMainPlayer=true;q.HasMainMapContainer=false;Require(!lw.InteractPossible(),"Absent main map container disables live waypoint");q.HasMainMapContainer=true;
            lw.OnClick();Require(q.Transfers==1&&ReferenceEquals(q.Transferred,e),"Live waypoint transfers actual entity");q.CurrentMeMapId=0;le.OnClick();Require(q.Transfers==1,"Live entry main map guard");q.CurrentMeMapId=13;le.OnClick();Require(q.Transfers==2&&ReferenceEquals(q.Transferred,e),"Live entry transfer");ow.OnClick();Require(q.EnteredMap==13&&q.EnteredId==92,"Offline waypoint uses wrapped id");oe.OnClick();Require(q.EnteredId==91,"Offline entry uses requested id");Require(!oe.InteractPossible(),"Offline entry requires complete state");oe.State=3;Require(oe.InteractPossible(),"Offline entry complete state");
            Throws<NullReferenceException>(()=>new MiniMapEntityMark(13,38,null,q));
            var empty=new Entity{ClassId=0};empty.GridTransform=new Grid{Entity=empty,Pos=Pos2d.Zero,Info=new GridTransformComponentInfo{BodyRangeInfos=new List<GridTransformComponentBodyPosInfo>()}};Throws<Exception>(()=>new MiniMapEntityMark(13,38,empty,q));
            var proof=new Proof{editor=Application.unityVersion,body_templates=data.templates.Length,placements=data.entities.Length,centroid_cases=centroids,factory_cases=factories,state_cases=states,description_cases=12,transfer_cases=5,original_relative_body_identity=true,original_null_branches=true,original_target_ids=true,original_live_offline_lookup=true,play_mode=Application.isPlaying};
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),"minimap-mark-data-proof.json"),JsonUtility.ToJson(proof,true));Debug.Log("Original minimap marker data PASS: "+centroids+" centroids, six marker types, transport parameters and transfer guards.");
        }
    }
}
