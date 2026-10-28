using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityViewPlayables;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StorySourceFade;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.World.NativeMap13;
using Zjcs.Offline.World.NativeMap14EntityAssets;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14EntityWorldStageAcceptance
    {
        private const string Key="Map14.EntityWorldStage";
        private static bool started;private static int errors;
        private static readonly List<string> Trace=new List<string>();
        private static string Output=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        [InitializeOnLoadMethod] private static void Hook(){if(!SessionState.GetBool(Key,false))return;started=false;errors=0;EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.playModeStateChanged-=Mode;EditorApplication.playModeStateChanged+=Mode;}
        public static void Run()
        {
            if(Environment.GetEnvironmentVariable("ZJCS_ISOLATED_ENTITY_VIEWS")=="1")Debug.Log("Explicit world-stage assembly isolation; full ProjectTools.Validate is not executed.");
            else{var tools=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Zjcs.Editor.ProjectTools")).First(t=>t!=null);tools.GetMethod("Validate").Invoke(null,null);}
            SessionState.SetString(Key+".pipeline",AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));SessionState.SetString(Key+".quality",AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));SessionState.SetInt(Key+".color",(int)PlayerSettings.colorSpace);
            GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");QualitySettings.renderPipeline=null;PlayerSettings.colorSpace=ColorSpace.Linear;
            SessionState.SetBool(Key,true);SessionState.SetInt(Key+".result",1);SessionState.SetFloat(Key+".deadline",(float)EditorApplication.timeSinceStartup+240);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Hook();EditorApplication.isPlaying=true;
        }
        private static void Mode(PlayModeStateChange state){if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key,false))return;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Observe;GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key+".pipeline",""));QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key+".quality",""));PlayerSettings.colorSpace=(ColorSpace)SessionState.GetInt(Key+".color",0);EditorApplication.Exit(SessionState.GetInt(Key+".result",1));}
        private static void Tick(){if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".deadline",0)){Debug.LogError("World-stage view validation timed out.");EditorApplication.Exit(1);return;}if(started||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;started=true;Application.logMessageReceived+=Observe;new GameObject("Original world-stage view validation").AddComponent<Runner>().StartCoroutine(Verify());}
        private static void Observe(string text,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private static IEnumerator Verify()
        {
            var stack=new Stack<IEnumerator>();stack.Push(Steps());while(stack.Count>0){object value;try{var top=stack.Peek();if(!top.MoveNext()){stack.Pop();continue;}value=top.Current;if(value is IEnumerator nested&&!(value is CustomYieldInstruction)){stack.Push(nested);continue;}}catch(Exception e){Debug.LogException(e);EditorApplication.isPlaying=false;yield break;}yield return value;}
            if(errors==0){SessionState.SetInt(Key+".result",0);Debug.Log("Map14 original world-stage views Play Mode passed: provider/legacy queries, replacement and buffered event order, native state display, start/loop forced visibility, stop/detach branches and release. Shared state/event/entity inputs are isolated; formal state rules/entity prefab/map/input/save/Player remain pending.");}EditorApplication.isPlaying=false;
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException("World-stage view: "+message);}
        private static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value);
        private static MyPlayable StatePlayable(ECPlayableWorldStageHandler handler)=>(MyPlayable)typeof(ECPlayableWorldStageHandler).GetField("mStatePlayable",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(handler);
        private static bool Forced(ClientVisibilitySupporterBase visibility)=>(bool)typeof(ClientVisibilitySupporterBase).GetField("mIsForceVisible",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(visibility);
        private static IEnumerator Frames(MyPlayableCenter center,float seconds){float elapsed=0;while(elapsed<seconds){yield return null;elapsed+=Time.deltaTime;center.Update(Time.deltaTime);center.LateUpdate();}}
        private static IEnumerator Steps()
        {
            Application.runInBackground=true;Application.targetFrameRate=60;Trace.Clear();
            var camera=new GameObject("Original stage display camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=12;camera.aspect=450f/800;camera.transform.position=new Vector3(0,6,-100);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.08f,.08f,1);camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var light=new GameObject("Stage Sprite illumination").AddComponent<Light2D>();light.lightType=Light2D.LightType.Global;
            var catalog=Resources.Load<NativeMap14EntitySpriteCatalog>("Map14/Native/entity-sprite-catalog");
            using(var sprites=new NativeMap14EntitySpriteLibrary(catalog))
            {
                var source=new GameObject("Isolated world-stage entity input");var entity=source.AddComponent<EntityInput>();var services=new Services();var data=entity.Data.Stage;
                var bone=new GameObject("CommonEffectSlot");bone.transform.SetParent(source.transform,false);source.AddComponent<AttachAgent>().GenerateBoneMap();var fade=source.AddComponent<FadeBridge>();
                var owner=new GameObject("Original world-stage view scope");owner.transform.SetParent(source.transform,false);
                var displays=Enumerable.Range(0,2).Select(i=>new GameObject("Original Sprite state "+i)).ToArray();
                for(int i=0;i<2;i++){displays[i].transform.SetParent(owner.transform,false);displays[i].transform.localPosition=new Vector3(i*6-3,0,0);var renderer=displays[i].AddComponent<SpriteRenderer>();renderer.sprite=sprites.Require(catalog.sprites.Single(s=>s.originalName=="building_statue_doushi_TaoYuan").key);renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Official/World/Map/14/NativeEntityMaterials/08c41716e96c66ddcf71bd56dd72f6e2/1419505037369228759.mat");}
                var displayConfig=WorldStateGameObjectConfig.FromBindings(new WorldStateGameObjectData{GoConfig=new List<WorldStateGameObjectData.StateGameObjectKV>{new WorldStateGameObjectData.StateGameObjectKV{State=0,GameObjects=new List<GameObject>{displays[0]}},new WorldStateGameObjectData.StateGameObjectKV{State=1,GameObjects=new List<GameObject>{displays[1]}}}});
                var stage=new ECWorldStageView{EcEntityBufferEventHandleModule=services,StageSwitcher=WorldStateSwitcher<int>.FromBindings(owner,new IWorldStateConfig[]{new SwitchInput(),displayConfig})};
                var prefab=Resources.Load<NativeMap13AssetCatalog>("Map14/Native/native-chaos-appearance-playable-catalog").Load<GameObject>("dd56c0b7ddba41545a66512acc8f5ea1");Require(prefab.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"native dynamic scripts are not fully bound");
                var operations=Enumerable.Range(0,3).Select(i=>new OperationInput(i)).ToArray();var rows=new List<WorldStageStateCfg>{new WorldStageStateCfg{myUIName="fixture zero",myLoopPlayable=prefab,InteractionOperation=new ECWorldStageInteractionOperationItemData{InteractionOperation=operations[0]}},new WorldStageStateCfg{myUIName="fixture one",myStartPlayable=prefab,myLoopPlayable=prefab,InteractionOperation=new ECWorldStageInteractionOperationItemData{InteractionOperation=operations[1]}},new WorldStageStateCfg{myUIName="fixture two",InteractionOperation=new ECWorldStageInteractionOperationItemData{InteractionOperation=operations[2]}}};
                var provider=new ECWorldStageCfgNormalProvider();Set(provider,"mStateCfgs",rows);Set(stage,"mWorldStageCfgProviderData",new ECWorldStageStateCfgBaseProviderItemData{ECWorldStageCfgProvider=provider});
                var visibility=new MapECViewClientVisibilitySupporter(services,null,null,owner.transform);visibility.Register(entity);entity.PublishStart();
                var scope=new GameObject("Original stage playback scope");var staging=new GameObject("Inactive original stage injection");staging.SetActive(false);var center=new MyPlayableCenter();var factory=new Factory(center,staging.transform);var manager=new MyPlayableManager{ScopeRoot=scope,InstanceFactory=factory,MyPlayableCenter=center,LowMemory=new Zjcs.Official.StoryPlayables.GAction(),ActivePublisher=_=>{},InactivePublisher=_=>{}};
                var oldFactory=StoryPlayableInstantiation.Resolve;StoryPlayableInstantiation.Resolve=root=>root.IsChildOf(scope.transform)?factory:null;manager.Initialize();manager.OnActivate();var oldSound=OfficialWwiseEvent.Host;OfficialWwiseEvent.Host=new SoundInput();
                var handler=new ECPlayableWorldStageHandler();Set(handler,"mIsNotCutOffInStartPlayable",true);var supporter=new ECPlayableSupporterView{MyPlayableManager=manager,ServiceProvider=services};Set(supporter,"mIsIgnoreFadeOut",true);Set(supporter,"mECPlayableHandlers",new List<ECPlayableBaseHandlerData>{new ECPlayableBaseHandlerData{Handler=handler}});
                var bridge=new BridgeInput(owner,visibility,fade,services,stage,supporter);var stageLife=(IECView)stage;var supporterLife=(IECView)supporter;stageLife.Init(bridge);supporterLife.Init(bridge);
                Trace.Clear();stageLife.Load(entity.Data,true);Require(Trace.SequenceEqual(new[]{"replace+","switch:0","buffer+"})&&displays[0].activeSelf&&!displays[1].activeSelf,"replacement/init/buffer subscription or original display order changed");
                Require(stage.State==0&&ReferenceEquals(stage.GetCurStateInfo(),data.Info)&&stage.GetStateComponentIndex(StageComponentType.BehStageBattleStele,4)==9&&data.LastSubState==4&&stage.CanInteractAfterMove(),"shared state/query forwarding differs");
                var legacy=stage.GetCurStateOldCfg();Require(legacy.myUIName==rows[0].myUIName&&legacy.myLoopPlayable==prefab&&legacy.InteractionOperation.InteractionOperation==operations[0],"legacy configuration projection differs");
                data.Value=1;int notified=0;stage.OnStateUpdate+=(component,previous,payload)=>{Require(ReferenceEquals(component,data)&&ReferenceEquals(payload,services.Payload)&&stage.StageSwitcher.State==data.Value,"switcher must precede exact event payload");notified++;Trace.Add("event");};entity.PublishReplace();Require(stage.StageSwitcher.State==1&&notified==0&&!displays[0].activeSelf&&displays[1].activeSelf,"replacement must only refresh display");data.Value=0;entity.PublishReplace();
                supporterLife.Load(entity.Data,true);supporterLife.OnShow();Require(supporter.IsVisible&&StatePlayable(handler),"visible state did not start original loop");yield return Frames(center,.65f);Capture(camera,"world-stage-original-loop.png");
                Trace.Clear();data.Value=1;services.Emit(data,0);var appear=StatePlayable(handler);Require(Trace.Take(2).SequenceEqual(new[]{"switch:1","event"})&&appear&&Forced(visibility)&&notified==1,"state start/forced visibility or buffered event order differs");
                Require(ReferenceEquals(stage.GetCurStateOldCfg(),legacy)&&legacy.myUIName==rows[1].myUIName&&legacy.myStartPlayable==prefab,"legacy projection must reuse its original temporary object");
                float elapsed=0;while(elapsed<4&&StatePlayable(handler)==appear){yield return null;elapsed+=Time.deltaTime;center.Update(Time.deltaTime);center.LateUpdate();}
                Require(StatePlayable(handler)&&StatePlayable(handler)!=appear&&!Forced(visibility),"start completion did not release force and begin loop");
                data.Value=2;services.Emit(data,1);Require(StatePlayable(handler)==null&&stage.GetCurStateCfg()==rows[2],"empty state did not stop old playback");yield return Frames(center,.1f);
                data.Value=3;services.Emit(data,2);Require(stage.GetCurStateCfg()==null&&stage.GetCurStateOldCfg()==null&&StatePlayable(handler)==null,"out-of-range provider behavior differs");data.Value=-1;Require(stage.GetCurStateCfg()==null,"negative provider index differs");
                data.Value=0;services.Emit(data,3);var surviving=StatePlayable(handler);Require(surviving,"state loop not restarted");services.Destroying=true;supporterLife.OnHide();Require(StatePlayable(handler)==surviving&&surviving.myStatus!=MyPlayable.PlayableStatus.Stop,"map destruction must suppress handler stop");supporterLife.Unload();Require(StatePlayable(handler)==null&&surviving.myStatus!=MyPlayable.PlayableStatus.Stop,"map destruction release must detach without stopping");
                Trace.Clear();stageLife.Unload();Require(Trace.SequenceEqual(new[]{"buffer-","replace-","release:0","release:1","release:2"})&&services.Subscribers==0&&entity.Replacements==0,"unload dispose/unsubscribe/provider release order differs");int before=notified;services.Emit(data,0);entity.PublishReplace();Require(notified==before,"unloaded stage retained input subscriptions");visibility.Unregister();
                manager.RemoveAllPlayable(false);manager.ForceReleaseObjectPool();manager.OnDeactivate();manager.Dispose();center.RemoveAll();StoryPlayableInstantiation.Resolve=oldFactory;OfficialWwiseEvent.Host=oldSound;
                Debug.Log("Original world-stage view verified: native state render, exact event payload/order, start -> loop, map-destroy detach and provider release; created pooled roots="+factory.Created);
                UnityEngine.Object.Destroy(scope);UnityEngine.Object.Destroy(staging);UnityEngine.Object.Destroy(source);yield return null;
            }
            UnityEngine.Object.Destroy(camera.gameObject);UnityEngine.Object.Destroy(light);Require(errors==0,"unexpected runtime errors");
        }
        private static void Capture(Camera camera,string name){var surface=new RenderTexture(450,800,24);surface.Create();var previous=RenderTexture.active;camera.targetTexture=surface;camera.Render();RenderTexture.active=surface;var image=new Texture2D(450,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,450,800),0,0);image.Apply();var p=image.GetPixels32();var bg=p[0];Require(p.Count(c=>Math.Abs(c.r-bg.r)>2||Math.Abs(c.g-bg.g)>2||Math.Abs(c.b-bg.b)>2)>100,"original runtime RGB frame is empty");File.WriteAllBytes(Path.Combine(Output,name),image.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.Destroy(image);surface.Release();UnityEngine.Object.Destroy(surface);}
        private sealed class StateInput:ECWorldStageStateData{public int Value,LastSubState;public readonly OfficialStageStateInfo Info=new OfficialStageStateInfo();public override int State=>Value;public override OfficialStageStateInfo GetCurStateInfo()=>Info;public override int GetStateComponentIndex(StageComponentType type,int index=0){LastSubState=index;return type==StageComponentType.BehStageBattleStele?9:-1;}}
        private sealed class DataInput:ECViewEntityData{public readonly StateInput Stage=new StateInput();public override int ClassId=>6650;public override EcEntityIdentifier Id=>new EcEntityIdentifier(14,101);public override T GetComponent<T>()=>typeof(T)==typeof(ECWorldStageStateData)?(T)(object)Stage:throw new InvalidOperationException("Unexpected fixture component query.");}
        private sealed class EntityInput:ECViewEntityBinding,IECWorldStageReplacementSource
        {
            public readonly DataInput Data=new DataInput();public override ECViewEntityData Entity=>Data;private readonly List<IMyEntityReplaceEventReceiver> replaced=new List<IMyEntityReplaceEventReceiver>();private readonly List<IMyEntityStartPostEventReceiver> posts=new List<IMyEntityStartPostEventReceiver>();private readonly List<IMyEntityRemovePreEventReceiver> pres=new List<IMyEntityRemovePreEventReceiver>();public int Replacements=>replaced.Count;
            public void AddReplaceReceiver(IMyEntityReplaceEventReceiver value){replaced.Add(value);Trace.Add("replace+");}public void RemoveReplaceReceiver(IMyEntityReplaceEventReceiver value){replaced.Remove(value);Trace.Add("replace-");}public void PublishReplace(){foreach(var v in replaced.ToArray())v.MyEntity_OnReplace(this,Data.Id);}public void PublishStart(){foreach(var v in posts.ToArray())v.MyEntity_OnStartPost(this);}
            public override void SetClientVisibilitySupporter(IClientVisibilitySupporter value){Require(value!=null,"visibility was not bound");}public override void AddStartPostReceiver(IMyEntityStartPostEventReceiver v)=>posts.Add(v);public override void RemoveStartPostReceiver(IMyEntityStartPostEventReceiver v)=>posts.Remove(v);public override void AddRemovePreReceiver(IMyEntityRemovePreEventReceiver v)=>pres.Add(v);public override void RemoveRemovePreReceiver(IMyEntityRemovePreEventReceiver v)=>pres.Remove(v);
            public override void AddStartReceiver(IMyEntityStartEventReceiver v)=>throw new InvalidOperationException();public override void RemoveStartReceiver(IMyEntityStartEventReceiver v)=>throw new InvalidOperationException();public override void AddRemoveReceiver(IMyEntityRemoveEventReceiver v)=>throw new InvalidOperationException();public override void RemoveRemoveReceiver(IMyEntityRemoveEventReceiver v)=>throw new InvalidOperationException();
        }
        private sealed class Services:IServiceProvider,IECViewMapRootLifetime,IEcEntityBufferEventHandleModule
        {private Action<EntityWorldStageUpdateEventMessage> callback;public bool Destroying{get;set;}public readonly object Payload=new object();public int Subscribers=>callback==null?0:1;public object GetService(Type type)=>type==typeof(IECViewMapRootLifetime)?this:throw new InvalidOperationException("Unexpected fixture service query.");public IDisposable Subscribe<T>(ECViewEntityData entity,Action<T> value) where T:IEcEntityBufferEventContext{Require(typeof(T)==typeof(EntityWorldStageUpdateEventMessage)&&callback==null,"unexpected subscription");callback=(Action<EntityWorldStageUpdateEventMessage>)(object)value;Trace.Add("buffer+");return new Lease(()=>{callback=null;Trace.Add("buffer-");});}public void Emit(StateInput data,int previous)=>callback?.Invoke(new EntityWorldStageUpdateEventMessage{StageComp=data,PreState=previous,Param=Payload});}
        private sealed class Lease:IDisposable{private Action action;public Lease(Action value){action=value;}public void Dispose(){var old=action;action=null;old?.Invoke();}}
        private sealed class SwitchInput:IWorldStateConfig{public void SetState(WorldStateConfigValue state)=>Trace.Add("switch:"+(int)state);public void Clear()=>throw new InvalidOperationException();}
        private sealed class OperationInput:IECWorldStageInteractionOperation{private readonly int id;public OperationInput(int id){this.id=id;}public bool DoOper(IServiceProvider provider,ECWorldStageStateData state)=>throw new InvalidOperationException("Interaction operations are not inferred by this fixture.");public bool ExistOperation<T>() where T:IECWorldStageInteractionOperation=>this is T;public void Release()=>Trace.Add("release:"+id);}
        private sealed class BridgeInput:IECViewBridge{private readonly IECView[] views;public BridgeInput(GameObject owner,IClientVisibilitySupporter visible,IEntityFadeAnimationBridge fade,IServiceProvider provider,params IECView[] values){GameObject=owner;ClientVisibilitySupporter=visible;EntityFadeAnimationBridge=fade;ServiceProvider=provider;views=values;}public IServiceProvider ServiceProvider{get;}public CancellationToken DestroyCancellationToken=>CancellationToken.None;public GameObject GameObject{get;}public IClientVisibilitySupporter ClientVisibilitySupporter{get;}public IEntityFadeAnimationBridge EntityFadeAnimationBridge{get;}public long StartCoroutine(IEnumerator value)=>throw new InvalidOperationException();public void StopCoroutine(long id)=>throw new InvalidOperationException();public T GetView<T>() where T:class,IECView=>views.OfType<T>().FirstOrDefault();}
        private sealed class Factory:IStoryPlayableInstanceFactory{private readonly MyPlayableCenter center;private readonly Transform staging;public int Created;public Factory(MyPlayableCenter value,Transform parent){center=value;staging=parent;}public GameObject Instantiate(GameObject prefab,Transform parent,bool worldPositionStays){var value=UnityEngine.Object.Instantiate(prefab,staging,false);foreach(var p in value.GetComponentsInChildren<MyPlayable>(true))p.MyPlayableCenter=center;value.transform.SetParent(parent,worldPositionStays);Created++;return value;}}
        private sealed class FadeBridge:MonoBehaviour,IEntityFadeAnimationBridge{public IEntityFadeAnimationSupporter EntityFadeAnimationSupporter{get;}=new EntityFadeAnimationSupporter(null);}
        private sealed class SoundInput:IProfessionSoundHost{private uint next;public uint PostEvent(uint id,GameObject emitter)=>++next;}
        private sealed class Runner:MonoBehaviour{}
    }
}
