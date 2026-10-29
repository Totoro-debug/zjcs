using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using EcEntityIdentifier=Zjcs.Official.StoryPlayables.EcEntityIdentifier;
using Object=UnityEngine.Object;
using MapRegionStatus=Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;
using GroundType=Zjcs.Official.World.NativeTerrain.Common.GroundType;
namespace Zjcs.Editor.Map13MiniMap
{
    public sealed class Map13SupportFixture:Map13ManagedFixture,IMiniMapMarkSupportContext
    {
        public bool Leader=true,Container=true;
        public bool IsLeader=>Leader;public bool HasMapContainer=>Container;
        public IMiniMapMarkSupportPlayer Player;public IMiniMapMarkSupportQueries Support;
        public IMiniMapMarkSupportPlayer SupportPlayer=>Player;public IMiniMapMarkSupportQueries SupportQueries=>Support;
        public override MiniMapKeepSizeHandler SizingHandler=>GetComponent<MiniMapKeepSizeHandler>();
    }
    public static class OfficialMap13MiniMapMarkSupportsAcceptance
    {
        private const string Key="Zjcs.Map13.MarkSupports.Running";
        private static IEnumerator routine;private static int errors;
        private static string Workspace=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        private const string Native="Assets/Official/World/Map/13/NativeMiniMapMarks/Prefabs/UI/Map/MiniMap/MiniMapMark/";
        private static void Require(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
        private sealed class Source:IMiniMapManagedEntitySource{public IMiniMapSpawnEntity Entity{get;set;}public EntityOwnershipData Ownership{get;set;}}
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



        [Serializable] private sealed class Receipt{public string[] roots;}
        [Serializable] private sealed class Proof{public string editor;public int prefabs,sprites,stage_transitions,periodicity_cases,npc_events,tip_links,rendered_pixels;public bool original_support_binding,original_owner_checks,original_tip_identity,tip_show_switch,tip_number_cap,event_unsubscribe,reentry,native_render,play_mode;}
        private sealed class Player:IMiniMapMarkSupportPlayer
        {public long Id=>7;public int Count;public event Action<int,int> OnPeriodicityInteractionChanged;public int GetWorldPeriodicityInteractionCount(int id)=>Count;public void Change(int value){Count=value;OnPeriodicityInteractionChanged?.Invoke(7,value);}public int Subscribers=>OnPeriodicityInteractionChanged?.GetInvocationList().Length??0;}
        private sealed class Support:IMiniMapMarkSupportQueries
        {
            public MiniMapPeriodicityInfo Info=new MiniMapPeriodicityInfo{PeriodicityMaxCount=3};public int Period=7;public string Display="original-display-graph",Extend="original-extend-graph";
            public int GetPeriodicityInteractionId(IMiniMapMarkEntity entity)=>Period;public int GetOfflinePeriodicityInteractionId(int map,int id)=>Period;
            public bool TryGetPeriodicity(int id,out MiniMapPeriodicityInfo value){value=GetPeriodicity(id);return value!=null;}public MiniMapPeriodicityInfo GetPeriodicity(int id)=>id==7?Info:null;
            public GroundType GetGroundType(int map,Pos2d pos)=>default;
            public MiniMapReddotDisplayInfo GetEntityMapDisplay(int entity,int group,int map,string area,GroundType ground,string tag)=>new MiniMapReddotDisplayInfo{ReddotGraphName=Display};
            public string GetEntityExtendReddotGraphName(int entity)=>Extend;
        }
        private sealed class Visibility:IClientVisibilityHandler
        {
            public ClientVisibilityType VisibilityType=>default;public bool Visible{get;private set;}=true;
            public Zjcs.Official.EntityViews.ECViewEntityData Entity=>null;public event Action<bool> OnVisibleChanged;
            public void Init(Zjcs.Official.EntityViews.ECViewEntityData entity,bool value){}public void Release(){}
            public void Change(bool value){Visible=value;OnVisibleChanged?.Invoke(value);}public int Subscribers=>OnVisibleChanged?.GetInvocationList().Length??0;
        }
        private sealed class Tips:IMiniMapTipDotController,IMiniMapTipDotSystem
        {
            public bool Show=true;public bool IsShowTipDot=>Show;public Action OnTipDotShowChanged{get;set;}
            public readonly Dictionary<GameObject,Action<TipDotStateInfo>> Callbacks=new Dictionary<GameObject,Action<TipDotStateInfo>>();public int Links,Unlinks;
            public void LinkNodeStateChange(TipDotNodeKey key,GameObject owner,Action<TipDotStateInfo> callback){Require(key.IsValid()&&key.GraphName=="original-display-graph"&&key.NodeName=="0"&&key.InstanceInfo is MapEntityNodeInstanceInfo,"Original actual graph/node/entity identity");Callbacks[owner]=callback;Links++;}
            public void UnlinkNodeStateChange(TipDotNodeKey key,GameObject owner,Action<TipDotStateInfo> callback){Callbacks.Remove(owner);Unlinks++;}
            public void Fire(ETipDotLevelType level,string data){foreach(var callback in Callbacks.Values.ToArray())callback(new TipDotStateInfo{TipLevel=level,Data=data});}
            public void SetShow(bool value){Show=value;OnTipDotShowChanged?.Invoke();}
        }
        private static object Get(object target,string name){for(var type=target.GetType();type!=null;type=type.BaseType){var f=type.GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly);if(f!=null)return f.GetValue(target);}throw new MissingFieldException(name);}
        public static void Run(){Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");errors=0;Application.logMessageReceived+=Observe;SessionState.SetBool(Key,true);EditorApplication.playModeStateChanged+=State;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();}
        private static void Observe(string a,string b,LogType t){if(t==LogType.Error||t==LogType.Exception||t==LogType.Assert)errors++;}
        [InitializeOnLoadMethod] private static void Resume(){if(SessionState.GetBool(Key,false)){errors=0;Application.logMessageReceived+=Observe;EditorApplication.playModeStateChanged+=State;}}
        private static void State(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){routine=Verify();EditorApplication.update+=Advance;}}
        private static void Advance(){try{if(routine.MoveNext())return;Require(errors==0,"Unexpected errors: "+errors);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.update-=Advance;SessionState.EraseBool(Key);EditorApplication.Exit(1);}}
        private static int Capture(Camera camera,RenderTexture target,string name)
        {camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();RenderTexture.active=old;var pixels=image.GetPixels32().Count(p=>p.r+p.g+p.b>30);File.WriteAllBytes(Path.Combine(Workspace,name),image.EncodeToPNG());Object.Destroy(image);return pixels;}
        private static IEnumerator Verify()
        {
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");GraphicsSettings.renderPipelineAsset=pipeline;QualitySettings.renderPipeline=pipeline;
            var host=new GameObject("OriginalMarkerSupportFixture");host.SetActive(false);var map=host.AddComponent<Map13SupportFixture>();var handler=host.AddComponent<MiniMapKeepSizeHandler>();var player=new Player();var support=new Support();map.Player=player;map.Support=support;
            map.Settings=AssetDatabase.LoadAssetAtPath<MiniMapSettings>("Assets/Official/World/Map/13/NativeMiniMapSettings/ScriptableConfig/Modules/MapCommon/MiniMapSettings.asset");
            var labels=new DescriptionLabels();var tips=new Tips();var q=new Queries();map.Queries=q;map.ManagerQueries=q;
            var config=JsonUtility.FromJson<Zjcs.Offline.World.NativeMap13GridVisibility.OfflineMap13GridVisibility.BodyConfiguration>(Resources.Load<TextAsset>("Map13/grid-entities").text);var body=config.templates.First().body;body.Init();
            var visibility=new Visibility();var entity=new Entity{ClassId=8363,Id=new EcEntityIdentifier(13,1)};entity.GridTransform=new Grid{Entity=entity,Info=body,Pos=new Pos2d(110,145)};
            var stage=new Stage{Entity=entity,Active=true};entity.Components[typeof(IMiniMapMarkStage)]=stage;entity.Components[typeof(IMiniMapManagedStage)]=stage;entity.Components[typeof(IClientVisibilityHandler)]=visibility;
            var ownership=new Source{Entity=entity,Ownership=new EntityOwnershipData{Type=EntityOwnershipType.Player,OwnerId=9}};entity.Components[typeof(IMiniMapManagedEntitySource)]=ownership;
            entity.Components[typeof(IMiniMapMarkWayPoint)]=new WayPoint{Entity=entity,Queries=q};entity.Components[typeof(IMiniMapMarkWorldEntry)]=new Entry{Entity=entity,Active=true};q.Common=entity;q.Entities[entity.Id]=entity;
            var receipt=JsonUtility.FromJson<Receipt>(File.ReadAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT"),"assets/reference/official-map/map13-native-minimap-marks-source-manifest.json")));Require(receipt.roots.Length==41,"Actual Map13 selection");
            var instances=new List<MiniMapMarkUIBase>();int sprites=0;
            foreach(var path in receipt.roots)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Native+Path.GetFileName(path));Require(prefab,"Actual original marker prefab "+path);var go=Object.Instantiate(prefab,host.transform,false);labels.Bind(go);
                Require(go.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"Original marker component binding");sprites+=go.GetComponentsInChildren<SpriteRenderer>(true).Length;
                foreach(var link in go.GetComponentsInChildren<MapEntityTipDotNodeLinker>(true)){link.Controller=tips;link.TipDotSystem=tips;link.Context=map;link.MiniMap=map;}
                instances.Add(go.GetComponent<MiniMapMarkUIBase>());
            }
            host.SetActive(true);yield return null;
            for(int i=0;i<instances.Count;i++){var ui=instances[i];ui.Init(map,MiniMapEntityMark.Create(q,ui.MarkType,13,38,entity));ui.transform.localPosition=new Vector3((i%7-3)*8,(2.5f-i/7)*8,0);}
            Require(tips.Links>0&&visibility.Subscribers>0&&player.Subscribers>0,"Actual original support subscriptions");
            var camera=new GameObject("OriginalNativeMarkerCamera",typeof(Camera)).GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=26;camera.nearClipPlane=-2;camera.farClipPlane=3;camera.transform.position=new Vector3(0,0,-1);camera.cullingMask=1<<16;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.gameObject.AddComponent<UniversalAdditionalCameraData>().SetRenderer(1);
            var target=new RenderTexture(1200,720,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
            tips.Fire(ETipDotLevelType.Normal,"120");foreach(var link in host.GetComponentsInChildren<MapEntityTipDotNodeLinker>(true)){Require(link.TipLevelState.State==ETipDotLevelType.Normal,"Original tip state callback");var text=(MultiLanguageText)Get(link,"mDataText");if(text)Require(text.myKey=="99+","Original count cap");}
            yield return null;yield return null;int rendered=Capture(camera,target,"minimap-original-marks-state0.png");Require(rendered>1000,"Actual original markers draw through native URP");
            int stageTransitions=0;for(int state=0;state<=6;state++){var previous=stage.State;stage.State=state;stage.StateUpdateEvent.FireEvent(stage,previous,null);foreach(var component in host.GetComponentsInChildren<MiniMapWorldStageSwitcherSupport>(true))Require(((StateSwitcher<int>)Get(component,"mStateSwitcher")).State==state,"Original stage event refresh");foreach(var component in host.GetComponentsInChildren<MiniMapActiveSwitcherSupport>(true))Require(((StateSwitcher<bool>)Get(component,"mActiveSwitcher")).State==(state==q.Extend.CompleteState),"Original active means complete state");stageTransitions++;}
            stage.State=1;stage.StateUpdateEvent.FireEvent(stage,6,null);player.Change(3);yield return null;Capture(camera,target,"minimap-original-marks-state1.png");
            foreach(bool visible in new[]{false,true,false,true}){visibility.Change(visible);foreach(var component in host.GetComponentsInChildren<MiniMapActiveByNPCVisibleSupporter>(true))Require(((StateSwitcher<bool>)Get(component,"mShowSwitcher")).State==visible,"Actual NPC visibility event");}
            foreach(bool show in new[]{false,true}){var listeners=host.GetComponentsInChildren<MapEntityTipDotNodeLinker>(true).Where(link=>link.isActiveAndEnabled).ToArray();Require(listeners.Length>0,"Actual enabled tip listeners");tips.SetShow(show);foreach(var link in listeners)Require(((GameObject)Get(link,"mContent")).activeSelf==show,"Original enabled global tip visibility");}
            int periodicityCases=0;
            foreach(int id in new[]{7,-1})foreach(bool ignore in new[]{false,true})foreach(long owner in new long[]{7,9})foreach(int count in new[]{-1,0,2,3,4})
            {support.Period=id;support.Info.IsIgnoreOwner=ignore;ownership.Ownership.OwnerId=owner;player.Change(count);var expected=id<0?MiniMapPeriodicitySwitcherSupport.Status.NotPeriodicity:ignore&&owner==7||count<3?MiniMapPeriodicitySwitcherSupport.Status.HasCount:MiniMapPeriodicitySwitcherSupport.Status.NotCount;foreach(var component in host.GetComponentsInChildren<MiniMapPeriodicitySwitcherSupport>(true)){Require(((StateSwitcher<MiniMapPeriodicitySwitcherSupport.Status>)Get(component,"mHasPeriodicityCountSwitcher")).State==expected,"Exact periodicity count/owner status");Require(((IMiniMapWayPointBlockHandler)component).CanUse()==(expected==MiniMapPeriodicitySwitcherSupport.Status.NotCount),"Original waypoint block inversion");}periodicityCases++;}
            Require(MiniMapMarkSupportRules.CheckCanInteraction(support,-1,0)==2157&&MiniMapMarkSupportRules.CheckCanInteraction(support,7,2,true)==2171&&MiniMapMarkSupportRules.CheckCanInteraction(support,7,3,true)==0,"Original periodicity error codes and negation");
            var key=new TipDotNodeKey("g","0",new MapEntityNodeInstanceInfo(entity));Require(key==new TipDotNodeKey("g","0",new MapEntityNodeInstanceInfo(entity))&&key.IsSameGraphNode("g","0")&&!default(TipDotNodeKey).IsValid(),"Original stable node identity");
            support.Display="";Require(MiniMapMarkSupportRules.GetMapEntityReddotGraphName(map,map,entity,7,13,"original-tag")==support.Extend,"Original graph fallback");support.Display="original-display-graph";
            foreach(var ui in instances)ui.Release();Require(player.Subscribers==0&&visibility.Subscribers==0&&tips.Callbacks.Count==0,"Original Release removes actual entity/player/graph subscriptions");
            foreach(var ui in instances)ui.Init(map,MiniMapEntityMark.Create(q,ui.MarkType,13,38,entity));Require(player.Subscribers>0&&visibility.Subscribers>0&&tips.Callbacks.Count>0,"Original marker reuse reinstalls support listeners");
            // Original release runs again from OnDestroy; no UI/state fixtures replace prefabs.
            Object.Destroy(host);Object.Destroy(camera.gameObject);yield return null;Require(player.Subscribers==0&&visibility.Subscribers==0&&tips.Callbacks.Count==0&&tips.OnTipDotShowChanged==null,"Original marker destruction removes all subscriptions");labels.Dispose();Object.Destroy(target);yield return null;
            File.WriteAllText(Path.Combine(Workspace,"minimap-mark-supports-proof.json"),JsonUtility.ToJson(new Proof{editor=Application.unityVersion,prefabs=41,sprites=sprites,stage_transitions=stageTransitions,periodicity_cases=periodicityCases,npc_events=4,tip_links=tips.Links,rendered_pixels=rendered,original_support_binding=true,original_owner_checks=true,original_tip_identity=true,tip_show_switch=true,tip_number_cap=true,event_unsubscribe=true,reentry=true,native_render=true,play_mode=Application.isPlaying},true));Debug.Log("MAP13_ORIGINAL_MARK_SUPPORTS_PASS");
        }
    }
}
