using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.NativeTerrain.Common;
using Object=UnityEngine.Object;
using MapRegionStatus=Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;
using FullScreenAction=Zjcs.Official.StoryPlayables.GAction;

using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.Map13Entities;
using EcEntityIdentifier=Zjcs.Official.StoryPlayables.EcEntityIdentifier;

using Zjcs.Official.World.Map13EntityVisibilityViews;

namespace Zjcs.Editor.Map13MiniMap
{
    public class Map13ManagedFixture:MiniMapManagedContext
    {
        internal MiniMapSettings Settings;internal bool Root=true;internal int Current=13;internal Object MoveSource;internal Vector3 World;
        public override bool HasMapRoot=>Root;public override Object GetMoveAgent(IMiniMapCharacterController controller)=>MoveSource;public override Vector3 GetCharacterWorldPosition(IMiniMapCharacterController controller)=>World;
        public override MiniMapSettings.MiniMapSetting MiniMapSetting=>Settings.GetMinimapSetting(2);
        public override int MapClassId=>13;public override int CurrentMapClassId=>Current;public override bool HasMainPlayer=>true;public override IMapViewRangeHandler ViewRangeHandler=>Range;
        public override MiniMapKeepSizeHandler SizingHandler=>null;public override FullScreenAction HideFullScreenAction{get;}=new FullScreenAction();public override FullScreenAction ShowFullScreenAction{get;}=new FullScreenAction();public override bool IsFullScreen=>false;
        public override bool HasGridVisibilityAgent=>false;public override MiniMapVisibilityAgent VisibilityAgent=>null;public override int CharacterViewRange=>5;public override Pos2d CharacterGridPos=>Pos2d.Zero;
        public override IMap13LabelServices LabelServices=>throw new NotSupportedException();
        public override event Action<IMapViewRangeHandler> OnVisibleRangeUpdate;
        public override event Action<int,int,MapRegionStatus,MapRegionStatus> OnMapStatusUpdate;
        public override event Action<Pos2d,bool,bool> OnPosVisibilityUpdate;
        public override IEnumerable<Pos2d> ForEachMapRegionActivedAllGrid(int map,int region)=>throw new NotSupportedException();
        public override bool TryGetMapLiteData(int map,out MapRootEntityLiteSingleInfo info){info=Lite;return true;}
        public override IMiniMapGridMover GetSpawnGridMover(int map)=>throw new NotSupportedException();public override IReadOnlyDictionary<int,MiniMapRegionInfo> GetMapRegionDict(int map)=>throw new NotSupportedException();public override MiniMapRegionInfo GetRegion(int map,int region)=>Regions[region];public override int AreaToRegion(int map,int area)=>throw new NotSupportedException();public override int QueryRegion(Pos2d pos)=>throw new NotSupportedException();public override List<int> GetWorldMapProgresses(int map)=>throw new NotSupportedException();public override MiniMapRegionDisplayInfo GetRegionDisplay(int map,int region)=>throw new NotSupportedException();public override object QueryCommonGridGround(int map)=>throw new NotSupportedException();public override object QuerySceneContainer(Component component)=>throw new NotSupportedException();
        public override MiniMapRegionDescItem InstantiateDescription(MiniMapRegionDescItem prefab,Transform parent)=>throw new NotSupportedException();public override GameObject InstantiateDisplay(GameObject prefab,Transform parent)=>Object.Instantiate(prefab,parent,false);public override IMiniMapDescriptionAssetLease<GameObject> LoadPrefab(string path){Loads++;return new Lease<GameObject>(Icon,()=>Disposed++);}public override List<MapRegionStatus> GetMapStatusListInTeam(int map)=>throw new NotSupportedException();public override bool IsRangeInViewRange(Pos2d min,Pos2d max)=>throw new NotSupportedException();public override IMiniMapRegionAssetLease LoadAssetByGuid(string guid)=>throw new NotSupportedException();public override GameObject InstantiateRegion(GameObject prefab,Transform parent,bool world)=>throw new NotSupportedException();
    
        internal readonly List<string> Trace=new List<string>();internal readonly List<int> Errors=new List<int>();internal readonly List<GameObject> Windows=new List<GameObject>();internal int Loads,Disposed;internal GameObject Icon;internal Sprite Background;internal MiniMapMarkDisplayInfo BackgroundInfo=new MiniMapMarkDisplayInfo{MapBgPath="OriginalBackground"};internal MiniMapMarkDialogBase LastDialog;internal Func<Type,MiniMapMarkDialogBase> Factory;internal IMiniMapCharacterActionGroupHandle ActionGroup;internal bool Ground=true,Partition=true;internal Vector3 Reserved;
        internal MapRootEntityLiteSingleInfo Lite=new MapRootEntityLiteSingleInfo{MapClassId=13,MapGroundSize=new Pos2d(240,240),MapGroundOriginPos=new Vector3(-110.5f,-145.5f,0)};
        public override IMiniMapCharacterActionGroupHandle CharacterActionGroup=>ActionGroup;public override bool HasMapGroundRoot=>Ground;public override bool HasPartitionController=>Partition;public override void ReservePartition(Vector3 position){Reserved=position;Trace.Add("reserve");}
        public override string GetAreaTag(int map,Pos2d position){if(map!=13)throw new InvalidDataException();return "OriginalAreaTag";}public override MiniMapMarkDisplayInfo GetMapUIDisplay(int map,string tag){if(map!=13||tag!="OriginalAreaTag")throw new InvalidDataException();return BackgroundInfo;}
        public override IMiniMapDescriptionAssetLease<Sprite> LoadSprite(string path){if(path!="OriginalBackground")throw new InvalidDataException();Loads++;return new Lease<Sprite>(Background,()=>Disposed++);}
        public override void Popup(Type type,Action<MiniMapMarkDialogBase> callback){Trace.Add("popup:"+type.Name);LastDialog=Factory(type);Windows.Add(LastDialog.gameObject);callback(LastDialog);}
        public override void CloseWindow(GameObject window){Trace.Add("close");}public override void ShowError(int code){Errors.Add(code);}public override void FireToCloseFullScreen(){Trace.Add("fullscreen");}
        internal IMapViewRangeHandler Range;internal IMiniMapMarkDataQueries Queries;internal int Spawned;internal bool Missing;
        public override IMiniMapMarkDataQueries MarkDataQueries=>Queries;
        public override bool IsEntityInViewRange(IMiniMapSpawnEntity entity)=>Range.IsPosInViewRange(entity.GridTransform.Pos);
        public override string GetMapEntityMiniMapMark(IMiniMapSpawnEntity entity,int group,int map,string tag)=>"MarkerFixture";
        public override IMiniMapDescriptionAssetLease<GameObject> LoadMarkPrefab(string path){Loads++;return new Lease<GameObject>(Missing?null:Icon,()=>Disposed++);}
        public override GameObject InstantiateMark(GameObject prefab,Transform parent,bool world){Spawned++;return Object.Instantiate(prefab,parent,world);}
        internal void UpdateRange()=>OnVisibleRangeUpdate?.Invoke(Range);
        internal bool HasViewSubscribers=>OnVisibleRangeUpdate!=null;
        internal IMiniMapManagedQueries ManagerQueries;internal IMiniMapManagedAreaAgent Area;internal IMiniMapMarkGridGround GroundInput;
        internal readonly MiniMapManagedEvents Events=new MiniMapManagedEvents();internal readonly Queue<Action> Pending=new Queue<Action>();
        internal Dictionary<int,MiniMapRegionInfo> Regions;
        public override IMiniMapManagedQueries ManagedQueries=>ManagerQueries;
        public override MiniMapManagedEvents ManagedEvents=>Events;
        public override IMiniMapManagedAreaAgent ManagedAreaAgent=>Area;
        public override IMiniMapMarkGridGround ManagedGridGround=>GroundInput;
        public override void InstantiateMarkAsync(string path,Transform parent,Action<MiniMapMarkUIBase> callback)=>Pending.Enqueue(()=>{var instance=Object.Instantiate(Icon,parent,false);instance.SetActive(true);callback(instance.GetComponent<MiniMapMarkUIBase>());});
        internal void Flush(){while(Pending.Count>0)Pending.Dequeue()();}
        private sealed class Lease<T>:IMiniMapDescriptionAssetLease<T>{public T Result{get;}private readonly Action disposed;public Lease(T value,Action action){Result=value;disposed=action;}public void Dispose()=>disposed();}
    }



    public static class OfficialMap13MiniMapMarkManagersAcceptance
    {
        private const string Key="Zjcs.Map13.MarkManagers.Running";private static IEnumerator routine;private static int errors;
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
        private sealed class Source:IMiniMapManagedEntitySource{public IMiniMapSpawnEntity Entity{get;set;}public EntityOwnershipData Ownership{get;set;}}
        private sealed class Mover:IMiniMapManagedGridMover{}
        private sealed class Cell:IMiniMapMarkCell{public readonly List<IMiniMapSpawnEntity> Values=new List<IMiniMapSpawnEntity>();public bool ExistEntitys()=>Values.Count>0;public IEnumerable<IMiniMapSpawnEntity> Entitys=>Values;}
        private sealed class Ground:IMiniMapMarkGridGround,IMiniMapManagedAreaAgent
        {public readonly Dictionary<EcEntityIdentifier,IMiniMapSpawnEntity> Entities=new Dictionary<EcEntityIdentifier,IMiniMapSpawnEntity>();public Dictionary<EcEntityIdentifier,IMiniMapSpawnEntity> GetAreaEntity(int area)=>area==0?Entities:null;public int GridToRegion(int map,Pos2d pos)=>0;public IMiniMapMarkCell GetPosData(Pos2d p){var cell=new Cell();cell.Values.AddRange(Entities.Values.Where(e=>e.GridTransform.Pos==p));return cell;}}
        private sealed class Performance:IMiniMapMarkPerformance{public int Registered,Removed;public void RegisterSpawnManager(MiniMapMarkSpawnManager value){Registered++;}public void UnregisterSpawnManager(MiniMapMarkSpawnManager value){Removed++;}}
        private sealed class Controller:IMiniMapCharacterController{public MyGridTransform MyGridTransform=>null;}
        private sealed class Characters:IMiniMapCharacterManager
        {public IMiniMapCharacterController MyMeUiCharacter{get;}=new Controller();public event Action<IMiniMapCharacterController,IMiniMapCharacterController> OnMeMapControllerUpdated;public bool Subscribed=>OnMeMapControllerUpdated!=null;}
        private sealed class Entity:IMiniMapSpawnEntity
        {
            public EcEntityIdentifier Id{get;set;}
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
        private sealed class Stage:IMiniMapManagedStage
        {public IMiniMapSpawnEntity Entity{get;set;}public EcEntityWorldStageComponentStateUpdateEventDelegate StateUpdateEvent{get;set;}=new EcEntityWorldStageComponentStateUpdateEventDelegate();public int State{get;set;}public bool Active;public bool StateEqual(int state)=>State==state;public bool IsActive()=>Active;}
        private sealed class Transport:IMiniMapMarkStageTransport{public int TargetLoEntityId{get;set;}}
        private sealed class Entry:IMiniMapMarkWorldEntry
        {public IMiniMapMarkEntity Entity{get;set;}public IMiniMapMarkGrid GridTransform=>Entity.GridTransform;public bool Active{get;set;}}
        private sealed class WayPoint:IMiniMapMarkWayPoint
        {public IMiniMapMarkEntity Entity{get;set;}public Queries Queries;public bool IsUsable()=>MiniMapMarkWayPointRules.IsEntityUsable(Queries,Entity.GridTransform.Pos);}
        private sealed class Queries:IMiniMapMarkDataQueries,IMiniMapManagedQueries,IServiceProvider
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
            public readonly Dictionary<EcEntityIdentifier,IMiniMapSpawnEntity> Entities=new Dictionary<EcEntityIdentifier,IMiniMapSpawnEntity>();
            public MiniMapManagedMapPartInfo Part=new MiniMapManagedMapPartInfo{SupportPublicRegion=true,SupportPrivateRegion=true};public List<MapRegionStatus> Statuses=new List<MapRegionStatus>{MapRegionStatus.Private};public readonly Dictionary<long,MapRegionStatus> ContainerStatuses=new Dictionary<long,MapRegionStatus>();
            public MiniMapManagedMapPartInfo GetMapPartInfo(int map)=>Part;public List<MapRegionStatus> GetCurrentMapStatus()=>Statuses;public bool TryGetStatusByContainerId(long id,out MapRegionStatus status)=>ContainerStatuses.TryGetValue(id,out status);public IMiniMapSpawnEntity GetEntity(EcEntityIdentifier id)=>Entities.TryGetValue(id,out var value)?value:null;
            public IEnumerable<MiniMapMarkLoEntityInfo> GetEntityByMapRegion(int map,int region)=>Wraps.Values.Where(w=>w.PosInfo.Region==region);public long MainPlayerId=>7;
            public object GetService(Type type)=>type==typeof(IMiniMapMarkDataQueries)?this:null;
        }


        [Serializable] private sealed class Proof{public string editor;public int status_cases,private_stage_cases,ownership_cases,offline_conditions,offline_callbacks;public bool live_initialization,private_complete_removal,container_events,grid_mover_excluded,public_region_transition,public_visibility,offline_relative_grid_registration,offline_last_write,offline_empty_destination_retained,original_null_active_regions,stage_unsubscribe,container_unsubscribe,character_unsubscribe,resource_disposal,play_mode;}
        public static void Run(){Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");errors=0;Application.logMessageReceived+=Observe;SessionState.SetBool(Key,true);EditorApplication.playModeStateChanged+=State;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();}
        private static void Observe(string a,string b,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        [InitializeOnLoadMethod] private static void Resume(){if(SessionState.GetBool(Key,false)){errors=0;Application.logMessageReceived+=Observe;EditorApplication.playModeStateChanged+=State;}}
        private static void State(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){routine=Verify();EditorApplication.update+=Advance;}}
        private static void Advance(){try{if(routine.MoveNext())return;Require(errors==0,"Unexpected runtime errors: "+errors);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(1);}}
        private static object Get(object target,string name){for(var t=target.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly);if(f!=null)return f.GetValue(target);}throw new MissingFieldException(name);}
        private static void Set(object target,string name,object value){for(var t=target.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly);if(f!=null){f.SetValue(target,value);return;}}throw new MissingFieldException(name);}
        private static int Receivers(object value)=>((ICollection)Get(value,"mRecordEventReceivers")).Count;
        private static Entity Make(int id,Pos2d pos,GridTransformComponentInfo info){var e=new Entity{Id=new EcEntityIdentifier(13,id),ClassId=8363};e.GridTransform=new Grid{Entity=e,Info=info,Pos=pos};var stage=new Stage{Entity=e,State=0,Active=true};e.Components[typeof(IMiniMapManagedStage)]=stage;e.Components[typeof(IMiniMapMarkStage)]=stage;return e;}
        private static IEnumerator Verify()
        {
            var labels=new DescriptionLabels();var root=new GameObject("OriginalRegionMarkerRuleFixture");var map=root.AddComponent<Map13ManagedFixture>();var q=new Queries();map.Queries=q;map.ManagerQueries=q;map.Range=new MapAllViewRangeHandler();var ground=new Ground();map.Area=ground;map.GroundInput=ground;map.Regions=new Dictionary<int,MiniMapRegionInfo>{{0,new MiniMapRegionInfo{Areas=new List<int>{0}}}};
            var config=JsonUtility.FromJson<Zjcs.Offline.World.NativeMap13GridVisibility.OfflineMap13GridVisibility.BodyConfiguration>(Resources.Load<TextAsset>("Map13/grid-entities").text);var info=config.templates.First(t=>t.body.BodyRangeInfos.Count>1).body;info.Init();var entity=Make(1,new Pos2d(111,146),info);q.Common=entity;q.Entities[entity.Id]=entity;ground.Entities[entity.Id]=entity;q.ContainerStatuses[13]=MapRegionStatus.Private;q.Display.MiniMapMark="MarkerFixture";q.Extend.MiniMapMark="ExtendMarkerFixture";q.Extend.MiniMapMarkHideInEnd=true;q.Extend.MiniMapMarkShowInPublic=true;
            var prefab=new GameObject("ExplicitRegionMarkBehaviorFixture");prefab.SetActive(false);prefab.AddComponent<MiniMapOnlyMarkUI>();map.Icon=prefab;
            int statusCases=0;foreach(bool pub in new[]{false,true})foreach(bool priv in new[]{false,true})foreach(MapRegionStatus status in Enum.GetValues(typeof(MapRegionStatus))){var part=new MiniMapManagedMapPartInfo{SupportPublicRegion=pub,SupportPrivateRegion=priv};bool expected=status==MapRegionStatus.Public?pub:status==MapRegionStatus.Private&&priv;Require(part.RegionStatusSupported(status)==expected&&part.RegionStatusOnlySupport(status)==(expected&&!(pub&&priv)),"Original status support truth table");statusCases++;}
            Require(MiniMapManagedRules.GetMapEntityMiniMapMark(map,entity,7,13,"original-tag")=="MarkerFixture","Original marker display override");q.Display.MiniMapMark="";Require(MiniMapManagedRules.GetMapEntityMiniMapMark(map,entity,7,13,"original-tag")=="ExtendMarkerFixture","Original marker extend fallback");q.Display.MiniMapMark="MarkerFixture";
            var source=new Source{Entity=entity};entity.Components[typeof(IMiniMapManagedEntitySource)]=source;int ownerCases=0;foreach(EntityOwnershipType type in Enum.GetValues(typeof(EntityOwnershipType)))foreach(long owner in new long[]{-1,0,7,8})foreach(bool unique in new[]{false,true}){source.Ownership=new EntityOwnershipData{Type=type,OwnerId=owner};q.Extend.InteractionUnique=unique;Require(MiniMapManagedRules.EntityShowTipForMe(map,entity)==(owner<=0||type!=EntityOwnershipType.Player||owner==7||!unique),"Original ownership visibility");ownerCases++;}source.Ownership=null;Require(MiniMapManagedRules.EntityShowTipForMe(map,entity),"Missing ownership shows tip");q.Extend.InteractionUnique=false;
            var go=new GameObject("OriginalLiveMarkerManagers");go.SetActive(false);go.transform.SetParent(root.transform,false);var spawn=go.AddComponent<MiniMapMarkSpawnManager>();Set(spawn,"mMinimap",map);spawn.Services=labels;spawn.ServiceProvider=q;var performance=new Performance();spawn.PerformanceMiniMapMarkProxy=performance;var characters=new Characters();var privateManager=go.AddComponent<MiniMapPrivateMarkManager>();var publicManager=go.AddComponent<MiniMapPublicMarkManager>();foreach(var manager in new MiniMapMarkManager[]{privateManager,publicManager}){manager.MiniMap=map;manager.MapMyCharacterManager=characters;manager.Services=labels;}go.SetActive(true);yield return null;yield return null;
            Require(spawn.MarkUiDict.Count==1&&performance.Registered==1&&Receivers(map.Events.AddEntityEvent)==2,"Original character-driven initialization and region creation");var stage=entity.GetComponent<IMiniMapManagedStage>();Require(Receivers(stage.StateUpdateEvent)==1,"Private mark stage subscription");
            var checker=typeof(MiniMapPrivateMarkManager).GetMethod("CreateEntityIconCheck",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);int stageCases=0;foreach(bool hide in new[]{false,true})foreach(bool active in new[]{false,true})foreach(int state in new[]{-1,0,3,4}){q.Extend.MiniMapMarkHideInEnd=hide;((Stage)stage).Active=active;((Stage)stage).State=state;bool expected=!hide||(active&&state!=3);Require((bool)checker.Invoke(privateManager,new object[]{0,stage,q.Extend})==expected,"Original private hide/active/complete table");stageCases++;}Require(!(bool)checker.Invoke(privateManager,new object[]{0,null,q.Extend}),"Private marker needs stage");q.Extend.MiniMapMarkHideInEnd=true;((Stage)stage).Active=true;((Stage)stage).State=3;stage.StateUpdateEvent.FireEvent(stage,0,null);Require(spawn.MarkUiDict.Count==0&&Receivers(stage.StateUpdateEvent)==0,"Complete state removes private mark and stage listener during event dispatch");
            ((Stage)stage).State=0;entity.Components[typeof(IMiniMapManagedGridMover)]=new Mover();map.Events.AddEntityEvent.FireEvent(entity);Require(spawn.MarkUiDict.Count==0,"Moving entities excluded");entity.Components.Remove(typeof(IMiniMapManagedGridMover));map.Events.AddEntityEvent.FireEvent(entity);Require(spawn.MarkUiDict.Count==1,"Added entity creates private mark");map.Events.RemoveEntityEvent.FireEvent(entity,null);Require(spawn.MarkUiDict.Count==0,"Removed entity removes mark");map.Events.AddEntityEvent.FireEvent(entity);
            q.Statuses[0]=MapRegionStatus.Public;q.ContainerStatuses[13]=MapRegionStatus.Public;map.Events.FireMapStatusUpdate(13,0,MapRegionStatus.Private,MapRegionStatus.Public);Require(spawn.MarkUiDict.Count==1&&Receivers(stage.StateUpdateEvent)==0,"Public region transition replaces private mark and releases stage listener");source.Ownership=new EntityOwnershipData{Type=EntityOwnershipType.Player,OwnerId=8};q.Extend.InteractionUnique=true;map.Events.RemoveEntityEvent.FireEvent(entity,null);map.Events.AddEntityEvent.FireEvent(entity);Require(spawn.MarkUiDict.Count==0,"Public marker respects other player's unique ownership");source.Ownership.OwnerId=7;map.Events.AddEntityEvent.FireEvent(entity);Require(spawn.MarkUiDict.Count==1,"Public marker shows local player's ownership");
            Object.Destroy(go);yield return null;Require(!characters.Subscribed&&Receivers(map.Events.AddEntityEvent)==0&&Receivers(map.Events.RemoveEntityEvent)==0&&Receivers(map.Events.ReplaceEntityEvent)==0&&Receivers(stage.StateUpdateEvent)==0&&map.Loads==map.Disposed,"Original manager event/character/resource cleanup");
            q.Wraps.Clear();q.Wraps[91]=new MiniMapMarkLoEntityInfo{LoEntityId=91,PosInfo=new MiniMapMarkPositionInfo{Pos=new Pos2d(111,146),Area=0,Region=0},DefSpawnEntityInfo=new MiniMapMarkSpawnEntityInfo{ClassId=8363}};int offlineCases=0,callbacks=0;
            foreach(bool active in new[]{false,true})foreach(bool hide in new[]{false,true})foreach(int state in new[]{-1,0,3,4})foreach(bool showPublic in new[]{false,true})foreach(bool showOffline in new[]{false,true})
            {
                q.Extend.MiniMapMarkHideInEnd=hide;q.Extend.MiniMapMarkShowInPublic=showPublic;q.Extend.MiniMapMarkShowInOffline=showOffline;var data=new PlayerOfflineMiniMapDataResponse{ActiveRegions=active?new List<int>{0}:new List<int>(),LiteEntityDataDict=new Dictionary<int,PlayerOfflineMiniMapLiteEntityData>{{91,new PlayerOfflineMiniMapLiteEntityData{EntityClassId=8363,State=state}}}};
                var offlineRoot=new GameObject("OfflineRuleCase");offlineRoot.transform.SetParent(root.transform,false);var manager=offlineRoot.AddComponent<OfflineMiniMapMarkManager>();manager.MiniMap=map;manager.EntitySlot=offlineRoot.transform;manager.ServiceProvider=q;var added=new List<MiniMapMarkUIBase>();manager.OnOfflineMapMarkAdded+=ui=>{Require(manager.TryGetMark(ui.GridPos+ui.BodyRange[0],out var value)&&value==ui,"Offline callback follows grid registration");added.Add(ui);};manager.Init(13,data);int expected=(active||(state!=3&&state>=0)||!hide?1:0)+(active&&showPublic&&showOffline?1:0);Require(map.Pending.Count==expected&&added.Count==0,"Original offline conditions and deferred load callback");map.Flush();Require(added.Count==expected,"Offline completion count");callbacks+=added.Count;if(expected>0){var last=added.Last();foreach(var pos in info.GetBodyRange())Require(manager.TryGetMark(new Pos2d(111,146)+pos,out var value)&&value==last,"Offline relative body registration and last writer wins");var list=new List<MiniMapMarkUIBase>();((IMiniMapMarkSpawnSupporter)manager).GetAllMarkUI(list);Require(list.Count==info.GetBodyRange().Count&&list.All(ui=>ui==last),"Original offline query retains duplicate UI per occupied grid");}else{var list=new List<MiniMapMarkUIBase>{null};((IMiniMapMarkSpawnSupporter)manager).GetAllMarkUI(list);Require(list.Count==1,"Offline empty query retains destination");}Object.Destroy(offlineRoot);offlineCases++;
            }
            var nullRoot=new GameObject("OriginalNullRegionsBranch");nullRoot.transform.SetParent(root.transform,false);var nullManager=nullRoot.AddComponent<OfflineMiniMapMarkManager>();nullManager.MiniMap=map;nullManager.EntitySlot=nullRoot.transform;nullManager.ServiceProvider=q;bool threw=false;try{nullManager.Init(13,new PlayerOfflineMiniMapDataResponse{ActiveRegions=null,LiteEntityDataDict=new Dictionary<int,PlayerOfflineMiniMapLiteEntityData>()});}catch(NullReferenceException){threw=true;}Require(threw,"Original null ActiveRegions foreach branch retained");Object.Destroy(nullRoot);yield return null;
            Object.Destroy(root);Object.Destroy(prefab);yield return null;labels.Dispose();var proof=new Proof{editor=Application.unityVersion,status_cases=statusCases,private_stage_cases=stageCases,ownership_cases=ownerCases,offline_conditions=offlineCases,offline_callbacks=callbacks,live_initialization=true,private_complete_removal=true,container_events=true,grid_mover_excluded=true,public_region_transition=true,public_visibility=true,offline_relative_grid_registration=true,offline_last_write=true,offline_empty_destination_retained=true,original_null_active_regions=true,stage_unsubscribe=true,container_unsubscribe=true,character_unsubscribe=true,resource_disposal=true,play_mode=Application.isPlaying};File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),"minimap-mark-managers-proof.json"),JsonUtility.ToJson(proof,true));Debug.Log("Original marker managers PASS: "+offlineCases+" offline conditions, "+ownerCases+" ownership cases and live stage/region events.");
        }
    }

}
