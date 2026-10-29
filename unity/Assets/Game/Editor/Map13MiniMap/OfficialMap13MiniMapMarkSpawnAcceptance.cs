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

namespace Zjcs.Editor.Map13MiniMap
{
    public class Map13SpawnFixture:MiniMapSpawnContext
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
        public override IMiniMapGridMover GetSpawnGridMover(int map)=>throw new NotSupportedException();public override IReadOnlyDictionary<int,MiniMapRegionInfo> GetMapRegionDict(int map)=>throw new NotSupportedException();public override MiniMapRegionInfo GetRegion(int map,int region)=>throw new NotSupportedException();public override int AreaToRegion(int map,int area)=>throw new NotSupportedException();public override int QueryRegion(Pos2d pos)=>throw new NotSupportedException();public override List<int> GetWorldMapProgresses(int map)=>throw new NotSupportedException();public override MiniMapRegionDisplayInfo GetRegionDisplay(int map,int region)=>throw new NotSupportedException();public override object QueryCommonGridGround(int map)=>throw new NotSupportedException();public override object QuerySceneContainer(Component component)=>throw new NotSupportedException();
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
        private sealed class Lease<T>:IMiniMapDescriptionAssetLease<T>{public T Result{get;}private readonly Action disposed;public Lease(T value,Action action){Result=value;disposed=action;}public void Dispose()=>disposed();}
    }


    public static class OfficialMap13MiniMapMarkSpawnAcceptance
    {
        private const string Key="Zjcs.Map13.MarkSpawn.Running";private static IEnumerator routine;private static int errors;
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
        private sealed class Cell:IMiniMapMarkCell{public readonly List<IMiniMapSpawnEntity> Values=new List<IMiniMapSpawnEntity>();public bool ExistEntitys()=>Values.Count>0;public IEnumerable<IMiniMapSpawnEntity> Entitys=>Values;}
        private sealed class Ground:IMiniMapMarkGridGround
        {public readonly Dictionary<Pos2d,Cell> Cells=new Dictionary<Pos2d,Cell>();public IMiniMapMarkCell GetPosData(Pos2d p)=>Cells.TryGetValue(p,out var value)?value:null;public void Add(Entity entity){if(!Cells.TryGetValue(entity.GridTransform.Pos,out var cell))Cells[entity.GridTransform.Pos]=cell=new Cell();cell.Values.Add(entity);}}
        private sealed class Range:IMapViewRangeHandler
        {public List<Pos2d> Points=new List<Pos2d>();public GAction<IMapViewRangeHandler> OnVisibleRangeUpdate{get;set;}=new GAction<IMapViewRangeHandler>();public IReadOnlyList<Pos2d> VisiblePosList=>Points;public bool IsEntityInViewRange(IMiniMapEntityBody b)=>IsPosInViewRange(b.Pos);public bool IsPosInViewRange(Pos2d p)=>Points==null||Points.Contains(p);public bool IsRangeInViewRange(Pos2d min,Pos2d max)=>Points==null||Points.Any(p=>p.X>=min.X&&p.X<=max.X&&p.Y>=min.Y&&p.Y<=max.Y);public void Dispose(){}}
        private sealed class Performance:IMiniMapMarkPerformance{public int Registered,Removed;public void RegisterSpawnManager(MiniMapMarkSpawnManager value){Registered++;}public void UnregisterSpawnManager(MiniMapMarkSpawnManager value){Removed++;}}
        private sealed class Tip:IMiniMapMarkTipState{public ETipDotLevelType Level;public void SetState(ETipDotLevelType value){Level=value;}}
        private sealed class DisposeProbe:IDisposable{private readonly Action action;public DisposeProbe(Action value){action=value;}public void Dispose()=>action();}
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

        [Serializable] private sealed class Proof{public string editor;public int entities,range_transitions,pool_borrows,prefab_loads,disposed_leases;public bool initial_range,resident_marks,cell_order,replace_pending_identity,region_index,last_in_pool_reuse,original_parent_reuse,delayed_cleanup,cleanup_cancel,active_borrow_survives,resource_disposal,performance_lifetime,view_unsubscribe,empty_result_retained,play_mode;}
        public static void Run(){Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");errors=0;Application.logMessageReceived+=Observe;SessionState.SetBool(Key,true);EditorApplication.playModeStateChanged+=State;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();}
        private static void Observe(string a,string b,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        [InitializeOnLoadMethod] private static void Resume(){if(SessionState.GetBool(Key,false)){errors=0;Application.logMessageReceived+=Observe;EditorApplication.playModeStateChanged+=State;}}
        private static void State(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){routine=Verify();EditorApplication.update+=Advance;}}
        private static void Advance(){try{if(routine.MoveNext())return;Require(errors==0,"Unexpected runtime errors: "+errors);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(1);}}
        private static void Set(object target,string name,object value)=>target.GetType().GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(target,value);
        private static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(target);
        private static Entity Make(int id,Pos2d pos,GridTransformComponentInfo info){var entity=new Entity{Id=new EcEntityIdentifier(13,id),ClassId=8363};entity.GridTransform=new Grid{Entity=entity,Info=info,Pos=pos};return entity;}
        private static IEnumerator Verify()
        {
            var disposeOrder=new List<int>();var bag=default(MiniMapSpawnDisposableBag);bag.Add(new DisposeProbe(()=>disposeOrder.Add(1)));bag.Add(null);bag.Add(new DisposeProbe(()=>disposeOrder.Add(2)));bag.Clear();Require(disposeOrder.SequenceEqual(new[]{1,2}),"Pinned R3 disposal order and null entry");bag.Add(new DisposeProbe(()=>disposeOrder.Add(3)));bag.Dispose();bag.Add(new DisposeProbe(()=>disposeOrder.Add(4)));bag.Dispose();Require(disposeOrder.SequenceEqual(new[]{1,2,3,4}),"Pinned R3 clear/reuse and immediate post-disposal release");
            var labels=new DescriptionLabels();var host=new GameObject("MarkerSpawnRuleFixture");var map=host.AddComponent<Map13SpawnFixture>();var q=new Queries();map.Queries=q;var range=new Range();map.Range=range;
            var config=JsonUtility.FromJson<Zjcs.Offline.World.NativeMap13GridVisibility.OfflineMap13GridVisibility.BodyConfiguration>(Resources.Load<TextAsset>("Map13/grid-entities").text);var info=config.templates.First().body;info.Init();
            var a=Make(1,new Pos2d(1,1),info);var b=Make(2,new Pos2d(8,8),info);var c=Make(3,new Pos2d(9,9),info);q.Common=a;var ground=new Ground();ground.Add(a);ground.Add(b);ground.Add(c);range.Points.Add(a.GridTransform.Pos);
            var prefab=new GameObject("ExplicitMarkerBehaviorFixture");prefab.SetActive(false);prefab.AddComponent<MiniMapOnlyMarkUI>();map.Icon=prefab;
            var markerObject=new GameObject("SpawnManager");markerObject.SetActive(false);markerObject.transform.SetParent(host.transform,false);var manager=markerObject.AddComponent<MiniMapMarkSpawnManager>();manager.Services=labels;manager.ServiceProvider=q;var performance=new Performance();manager.PerformanceMiniMapMarkProxy=performance;Set(manager,"mMinimap",map);
            var trace=new List<string>();manager.OnUiMarkAdded=(ui,id)=>{Require(ui.Mark!=null&&ui.MiniMap==map&&manager.MarkUiDict[id]==ui,"Add event follows initialization and indexing");trace.Add("add:"+id.EntityId);};manager.OnUiMarkRemoved=(ui,id)=>{Require(ui.Mark!=null&&!manager.MarkUiDict.ContainsKey(id),"Remove event precedes pool release");trace.Add("remove:"+id.EntityId);};
            markerObject.SetActive(true);manager.Init(ground);manager.Init(ground);Require(performance.Registered==1&&map.HasViewSubscribers,"Original one-time init and view subscription");manager.CreateMark("MarkerFixture",38,a);manager.CreateMark("MarkerFixture",38,b);Require(manager.MarkUiDict.Count==1&&manager.MarkUiDict.ContainsKey(a.Id)&&trace.SequenceEqual(new[]{"add:1"}),"Initial visible/pending split");
            var firstUI=manager.MarkUiDict[a.Id];Require(manager.TryGetMark(a.GridTransform.Pos,out var hit)&&hit==firstUI&&!manager.TryGetMark(new Pos2d(100,100),out _),"Original grid hit and empty cell");
            manager.CreateResidentMark(c);var resident=manager.MarkUiDict[c.Id];var tip=resident.gameObject.AddComponent<MiniMapMarkTipDotLink>();var state=new Tip();tip.TipLevelState=state;manager.SetResidentMarkTipDot(c.Id,ETipDotLevelType.Important);manager.SetResidentMarkTipDot(a.Id,ETipDotLevelType.None);Require(state.Level==ETipDotLevelType.Important,"Resident-only tip state forwarding");
            range.Points.Clear();range.Points.Add(b.GridTransform.Pos);map.UpdateRange();Require(manager.MarkUiDict.Count==2&&manager.MarkUiDict[b.Id]==firstUI&&manager.MarkUiDict[c.Id]==resident,"Off-range return, pool reuse and resident retention");Require(manager.RegionEntityDict[38].SequenceEqual(new[]{b.Id})&&trace.Skip(2).SequenceEqual(new[]{"remove:1","add:2"}),"Original region index and remove-before-create order");
            var replacement=Make(20,b.GridTransform.Pos,info);manager.ReplaceEntity(replacement,b.Id);Require(manager.MarkUiDict.ContainsKey(replacement.Id)&&manager.RegionEntityDict[38].Single()==replacement.Id,"Replacement UI and region identity");map.UpdateRange();Require(manager.MarkUiDict.ContainsKey(b.Id)&&!manager.MarkUiDict.ContainsKey(replacement.Id),"Original pending replacement cache retains original entity identity");
            ground.Cells[b.GridTransform.Pos].Values.Insert(0,c);Require(manager.TryGetMark(b.GridTransform.Pos,out hit)&&hit==resident,"Grid cell entity order chooses first existing UI");
            var all=new List<MiniMapMarkUIBase>{null};((IMiniMapMarkSpawnSupporter)manager).GetAllMarkUI(all);Require(all.Count==2&&!all.Contains(null),"Nonempty all-mark query clears destination");manager.RemoveResidentMark(c.Id);manager.RemoveResidentMark(c.Id);manager.DestroyMark(a.Id);manager.DestroyMark(b.Id);all.Clear();all.Add(firstUI);((IMiniMapMarkSpawnSupporter)manager).GetAllMarkUI(all);Require(all.Count==1&&all[0]==firstUI,"Original empty all-mark query leaves caller list intact");
            range.Points=null;map.UpdateRange();Require(manager.MarkUiDict.Count==0,"Removed pending marks stay removed in all-visible mode");manager.CreateMark("MarkerFixture",39,a);manager.CreateMark("MarkerFixture",40,b);Require(manager.MarkUiDict.Count==2,"All-visible mode creates requested marks");
            ((IFullScrMiniMapStateListener)manager).OnHideFullScreenMiniMap(null);var pool=Get<MiniMapMarkPoolHandler>(manager,"mPoolHandler");Require(Get<System.Threading.CancellationTokenSource>(pool,"mCancellationTokenSource")!=null,"Hide schedules original ten-second cleanup");((IFullScrMiniMapStateListener)manager).OnShowFullScreenMiniMap(null);Require(Get<System.Threading.CancellationTokenSource>(pool,"mCancellationTokenSource")==null,"Show cancels cleanup");
            Object.Destroy(markerObject);yield return null;Require(performance.Removed==1&&!map.HasViewSubscribers&&map.Disposed==map.Loads,"Manager destruction releases leases, events and performance registration");
            var poolRoot=new GameObject("DirectPoolBehaviorFixture");poolRoot.transform.SetParent(host.transform,false);var other=new GameObject("OtherSlot");other.transform.SetParent(host.transform,false);pool=new MiniMapMarkPoolHandler(poolRoot.transform,map);int oldLoads=map.Loads,oldSpawned=map.Spawned;
            var one=pool.Borrow(8363,"MarkerFixture",poolRoot.transform);var two=pool.Borrow(8363,"MarkerFixture",poolRoot.transform);Require(one!=two&&map.Loads==oldLoads+1&&map.Spawned==oldSpawned+2,"One prefab lease and separate borrowed instances");pool.Return(one);pool.Return(two);var reused=pool.Borrow(8363,"MarkerFixture",other.transform);Require(reused==two&&reused.transform.parent==poolRoot.transform&&reused.gameObject.activeSelf,"Original last-in reuse preserves original parent");pool.Return(reused);
            pool.DelayClearPool(.02f);pool.StopClearPool();float end=Time.realtimeSinceStartup+.08f;while(Time.realtimeSinceStartup<end)yield return null;Require(one&&two,"Cancelled cleanup preserves pooled objects");
            var active=pool.Borrow(8363,"MarkerFixture",poolRoot.transform);pool.DelayClearPool(.02f);end=Time.realtimeSinceStartup+2;while(one&&Time.realtimeSinceStartup<end)yield return null;Require(!one&&active,"Delayed cleanup destroys only pooled instances");Require(Get<System.Threading.CancellationTokenSource>(pool,"mCancellationTokenSource")==null,"Successful delay clears cancellation reference");pool.Return(active);var again=pool.Borrow(8363,"MarkerFixture",poolRoot.transform);Require(again==active&&map.Loads==oldLoads+1,"Loaded prefab retained after pool cleanup");
            map.Missing=true;Require(pool.Borrow(1,"MissingFixture",poolRoot.transform)==null&&pool.Borrow(1,"MissingFixture",poolRoot.transform)==null&&map.Loads==oldLoads+3,"Missing prefab leases are retained and uncached");map.Missing=false;pool.Release();Require(map.Disposed==map.Loads,"All cached and failed resource leases disposed");Object.Destroy(host);Object.Destroy(prefab);yield return null;labels.Dispose();
            var proof=new Proof{editor=Application.unityVersion,entities=3,range_transitions=4,pool_borrows=7,prefab_loads=map.Loads,disposed_leases=map.Disposed,initial_range=true,resident_marks=true,cell_order=true,replace_pending_identity=true,region_index=true,last_in_pool_reuse=true,original_parent_reuse=true,delayed_cleanup=true,cleanup_cancel=true,active_borrow_survives=true,resource_disposal=true,performance_lifetime=true,view_unsubscribe=true,empty_result_retained=true,play_mode=Application.isPlaying};
            File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),"minimap-mark-spawn-proof.json"),JsonUtility.ToJson(proof,true));Debug.Log("Original minimap spawn and pool rule checks PASS.");
        }
    }

}
