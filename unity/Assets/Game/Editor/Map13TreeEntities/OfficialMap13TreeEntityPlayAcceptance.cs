using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityWorldStages;
using Zjcs.Official.EntityViewPlayables;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.World.Map13EntityStateViews;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13EntityGridViews;
using Zjcs.Official.World.Map13EntityFadeHandlers;
using Zjcs.Official.World.Map13EntityRendering;
using Zjcs.Official.World.Map13TreePlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Offline.World.NativeMap13EntityAssets;
using Zjcs.Offline.World.NativeMap13EntityViews;
using Zjcs.Offline.World.NativeMap13Skeletons;
using Zjcs.Offline.World.NativeMap13TreeEntities;
using StageKind=Zjcs.Official.World.Map13EntityStateViews.StageComponentType;

namespace Zjcs.Editor.Map13TreeEntities
{
    // Complete original entity lifecycle with explicit diagnostic data inputs.
    // This verifies resource integration; it does not implement map rules.
    [InitializeOnLoad]
    public static class OfficialMap13TreeEntityPlayAcceptance
    {
        private const string Key="Map13.TreeEntityLifecycle";
        private const string Receipt="assets/reference/official-map/map13-native-tree-entities-source-manifest.json";
        private static bool started,failed;
        private static string Output=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR")??throw new InvalidDataException("Explicit validation directory required.");
        private static string Repository=>Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT")??throw new InvalidDataException("Explicit repository root required.");
        static OfficialMap13TreeEntityPlayAcceptance()
        {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key,false))return;
                SessionState.SetBool(Key,false);
                GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key+".pipeline",""));
                QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key+".quality",""));
                PlayerSettings.colorSpace=(ColorSpace)SessionState.GetInt(Key+".color",0);
                EditorApplication.Exit(SessionState.GetInt(Key+".result",1));
            };
        }
        public static void Run()
        {
            var receipt=File.ReadAllBytes(Path.Combine(Repository,Receipt));
            var source=JsonUtility.FromJson<Source>(System.Text.Encoding.UTF8.GetString(receipt));
            foreach(var entry in source.inputs.Concat(source.outputs).Concat(source.tools))
            {
                var path=entry.path.StartsWith("unity/Assets/",StringComparison.Ordinal)?Path.Combine(Application.dataPath,entry.path.Substring("unity/Assets/".Length)):Path.Combine(Repository,entry.path);
                Require(Hash(File.ReadAllBytes(path))==entry.sha256,"Original tree dependency differs: "+entry.path);
            }
            SessionState.SetString(Key+".source",Hash(receipt));
            OfficialMap13TreeEntityAcceptance.Import();
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");
            SessionState.SetString(Key+".pipeline",AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            SessionState.SetString(Key+".quality",AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
            SessionState.SetInt(Key+".color",(int)PlayerSettings.colorSpace);
            GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");
            QualitySettings.renderPipeline=null;PlayerSettings.colorSpace=ColorSpace.Linear;
            SessionState.SetBool(Key,true);SessionState.SetInt(Key+".result",1);
            SessionState.SetFloat(Key+".deadline",(float)EditorApplication.timeSinceStartup+600);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;
        }
        private static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".deadline",0)){Debug.LogError("Tree lifecycle verification timed out.");EditorApplication.Exit(1);return;}
            if(EditorApplication.isPlaying)EditorApplication.QueuePlayerLoopUpdate();
            if(started||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            started=true;new GameObject("Original complete tree lifecycle verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            Application.logMessageReceived+=Observe;
            var stack=new Stack<IEnumerator>();stack.Push(Steps());
            while(stack.Count>0)
            {
                object next;
                try{var current=stack.Peek();if(!current.MoveNext()){stack.Pop();continue;}next=current.Current;if(next is IEnumerator nested&&!(next is CustomYieldInstruction)){stack.Push(nested);continue;}}
                catch(Exception e){Debug.LogException(e);EditorApplication.isPlaying=false;yield break;}
                yield return next;
            }
            if(!failed)SessionState.SetInt(Key+".result",0);
            Application.logMessageReceived-=Observe;EditorApplication.isPlaying=false;
        }
        private static void Observe(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)failed=true;}
        private static IEnumerator Steps()
        {
            Application.runInBackground=true;Application.targetFrameRate=60;
            var camera=new GameObject("Original complete tree diagnostic camera").AddComponent<Camera>();camera.orthographic=true;camera.aspect=1;camera.orthographicSize=5;camera.transform.position=new Vector3(0,3,-100);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.08f,.08f,1);camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var light=new GameObject("Original tree diagnostic illumination").AddComponent<Light2D>();light.lightType=Light2D.LightType.Global;
            var catalog=Resources.Load<NativeMap13EntitySpriteCatalog>("Map13/Native/entity-sprite-catalog");
            using(var animations=new NativeMap13AnimatorLibrary(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/World/Map/13/NativePropAnimators/map13-prop-animators.windows.bundle.bytes"),AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/World/Map/13/NativePropAnimators/execution-cases.json"),"65211c5a2dd5a677d26a0f37f88a518c306e7968425048cab263b54e0951bd4f"))
            using(var sprites=new NativeMap13EntitySpriteLibrary(catalog))
            using(var dissolve=new NativeMap13TreeModelAnimatorLibrary(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/World/Map/13/NativeTreeModelAnimator/model-animator.windows.bundle.bytes"),AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/World/Map/13/NativeTreeModelAnimator/catalog.json")))
            {
                int pixels=0,cycles=0,created=0;
                foreach(string name in new[]{"8363_view","8363_snow_view"})
                {
                    var scope=new GameObject("Original tree playable scope");var staging=new GameObject("Inactive original entity injection");staging.SetActive(false);
                    var center=new MyPlayableCenter();var factory=new Factory(center,staging.transform);
                    var manager=new MyPlayableManager{ScopeRoot=scope,InstanceFactory=factory,MyPlayableCenter=center,LowMemory=new GAction(),ActivePublisher=_=>{},InactivePublisher=_=>{}};
                    var oldFactory=StoryPlayableInstantiation.Resolve;StoryPlayableInstantiation.Resolve=root=>root.IsChildOf(scope.transform)?factory:oldFactory?.Invoke(root);
                    manager.Initialize();manager.OnActivate();var services=new Services(manager);Runner.Services=services;Runner.Center=center;
                    var host=new GameObject("Explicit original tree entity data input");host.SetActive(false);var entity=host.AddComponent<EntityInput>();
                    var count=host.AddComponent<Map13AwardCountBinding>();count.Stage=entity.Data.Stage;count.Awards=entity.Data.Awards;
                    var prefab=Resources.Load<GameObject>("Map13/Native/TreeEntities/"+name);var instance=UnityEngine.Object.Instantiate(prefab,host.transform,false);
                    var binding=instance.GetComponent<NativeMap13TreeEntityBinding>();binding.Prepare(animations,sprites,dissolve.Controllers,services,new MapClientVisibilitySupporterFactory{MapRootManagerProxy=services});
                    host.SetActive(true);binding.InitializeActive();yield return null;
                    Require(entity.StartCount==1&&entity.PostCount==1&&entity.RemoveCount==1&&entity.PreCount==1,"Original driver did not register all lifecycle receivers.");
                    entity.PublishStart(true);yield return Wait(4);
                    var stage=binding.Driver.GetView<ECWorldStageView>();var supporter=binding.Driver.GetView<ECPlayableSupporterView>();var handler=supporter.GetPlayableHandler<ECPlayableWorldStageHandler>();
                    Require(stage.State==0&&entity.ClientVisibilitySupporter.IsVisible&&supporter.IsVisible,"Original visible stage did not finish appearance.");
                    Require(services.Subscribers==1&&entity.ReplaceCount==1&&entity.FadeOutAction?.GetInvocationList().Length==1,"Original tree did not bind its stage, replacement and original fade handler.");
                    Require(StatePlayable(handler),"Original stage-zero loop did not start.");
                    Require(host.GetComponent<Zjcs.Official.StoryAttachment.AttachAgent>(),"Original attachment view did not create the source bone registry.");
                    var skeleton=binding.Model.Skeletons.Single();var bounds=skeleton.View.LocalBounds;
                    camera.transform.position=skeleton.transform.TransformPoint(bounds.center)+new Vector3(0,0,-50);
                    camera.orthographicSize=Mathf.Max(bounds.extents.y*skeleton.transform.lossyScale.y,bounds.extents.x*skeleton.transform.lossyScale.x)*1.5f;
                    CheckApples(binding,4);pixels+=Capture(camera,name+"-complete-loop.png");
                    entity.ClientVisibilitySupporter.SetEntityVisible(712,false);yield return Wait(.2f);
                    Require(!supporter.IsVisible&&!StatePlayable(handler),"Original visibility hide did not stop state playback.");
                    entity.ClientVisibilitySupporter.SetEntityVisible(712,true);yield return Wait(1);
                    Require(supporter.IsVisible&&StatePlayable(handler),"Original visibility show did not restart state playback.");
                    entity.Data.Shared.Value=1;services.Emit(entity.Data.Shared,0);yield return Wait(.1f);
                    Require(stage.State==1&&!StatePlayable(handler),"Original empty stage-one configuration did not stop its loop.");
                    entity.Data.Shared.Value=0;services.Emit(entity.Data.Shared,1);yield return Wait(.2f);Require(StatePlayable(handler),"Original stage-zero return did not restart loop.");
                    bool done=entity.FadeOutAction();Require(!done,"Original visible removal skipped the configured delay.");yield return Wait(.45f);pixels+=Capture(camera,name+"-complete-dissolve.png");
                    float deadline=Time.time+5;while(!done&&Time.time<deadline){yield return null;done=entity.FadeOutAction();}
                    Require(done,"Original removal performances did not complete.");
                    entity.PublishRemove();yield return null;
                    Require(services.Subscribers==0&&entity.ReplaceCount==0&&entity.FadeOutAction==null&&entity.StartCount==0&&entity.PostCount==0&&entity.RemoveCount==0&&entity.PreCount==0,"Original tree unload retained lifecycle/event/fade subscriptions.");
                    manager.RemoveAllPlayable(false);manager.ForceReleaseObjectPool();manager.OnDeactivate();manager.Dispose();center.RemoveAll();StoryPlayableInstantiation.Resolve=oldFactory;Runner.Center=null;Runner.Services=null;
                    created+=factory.Created;cycles++;UnityEngine.Object.Destroy(host);UnityEngine.Object.Destroy(scope);UnityEngine.Object.Destroy(staging);yield return null;
                }
                Require(!failed,"Original complete tree emitted runtime errors.");
                Require(Hash(File.ReadAllBytes(Path.Combine(Repository,Receipt)))==SessionState.GetString(Key+".source",""),"Original tree source changed during execution.");
                File.WriteAllText(Path.Combine(Output,"native-tree-entity-play.json"),JsonUtility.ToJson(new Proof{source_sha256=SessionState.GetString(Key+".source",""),editor=Application.unityVersion,backend=SystemInfo.graphicsDeviceType.ToString(),entities=cycles,original_objects=340,ordered_views=18,pooled_performances=created,rendered_pixels=pixels,play_mode=true},true));
                Debug.Log("Map13 complete original tree lifecycle passed: "+cycles+" entities, "+created+" pooled performances, "+pixels+" runtime pixels.");
            }
            UnityEngine.Object.Destroy(camera.gameObject);UnityEngine.Object.Destroy(light.gameObject);
        }
        private static IEnumerator Wait(float seconds){float until=Time.time+seconds;while(Time.time<until)yield return null;}
        private static MyPlayable StatePlayable(ECPlayableWorldStageHandler handler)=>(MyPlayable)typeof(ECPlayableWorldStageHandler).GetField("mStatePlayable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(handler);
        private static void CheckApples(NativeMap13TreeEntityBinding binding,int count){var items=(List<GameObject>)typeof(ECMapAwardItemCountSwitcherView).GetField("mItemGos",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(binding.Driver.GetView<ECMapAwardItemCountSwitcherView>());Require(items.Count==10,"Original tree apple bindings differ.");for(int i=0;i<items.Count;i++)Require(items[i].activeSelf==(i<count),"Original tree result-count state differs.");}
        private static int Capture(Camera camera,string name){var target=new RenderTexture(512,512,24);target.Create();var old=RenderTexture.active;camera.targetTexture=target;camera.Render();RenderTexture.active=target;var texture=new Texture2D(512,512,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,512,512),0,0);texture.Apply();var colors=texture.GetPixels32();var bg=colors[0];int pixels=colors.Count(c=>Math.Abs(c.r-bg.r)>3||Math.Abs(c.g-bg.g)>3||Math.Abs(c.b-bg.b)>3);Require(pixels>100,"Original full tree runtime frame is empty.");File.WriteAllBytes(Path.Combine(Output,name),texture.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.Destroy(texture);target.Release();UnityEngine.Object.Destroy(target);return pixels;}
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
        private static string Hash(byte[] bytes){using(var algorithm=SHA256.Create())return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        private sealed class Runner:MonoBehaviour{public static Services Services;public static MyPlayableCenter Center;private void Update(){Services?.Update();Center?.Update(Time.deltaTime);}private void LateUpdate(){Services?.LateUpdate();Center?.LateUpdate();SlicedFilledRenderService.Current.LateUpdate();}}
        private sealed class EntityInput:ECViewEntityBinding,IECWorldStageReplacementSource,IMap13EntityVisibilityBinding,IMap13EntityFadeOutBinding
        {
            public readonly DataInput Data=new DataInput();public override ECViewEntityData Entity=>Data;public IClientVisibilitySupporter ClientVisibilitySupporter{get;private set;}public Func<bool> FadeOutAction{get;set;}
            private readonly List<IMyEntityStartEventReceiver> starts=new List<IMyEntityStartEventReceiver>();private readonly List<IMyEntityRemoveEventReceiver> removes=new List<IMyEntityRemoveEventReceiver>();private readonly List<IMyEntityStartPostEventReceiver> posts=new List<IMyEntityStartPostEventReceiver>();private readonly List<IMyEntityRemovePreEventReceiver> pres=new List<IMyEntityRemovePreEventReceiver>();private readonly List<IMyEntityReplaceEventReceiver> replacements=new List<IMyEntityReplaceEventReceiver>();
            public int StartCount=>starts.Count;public int RemoveCount=>removes.Count;public int PostCount=>posts.Count;public int PreCount=>pres.Count;public int ReplaceCount=>replacements.Count;
            public void PublishStart(bool fresh){foreach(var v in starts.ToArray())v.MyEntity_OnStart(this,fresh);foreach(var v in posts.ToArray())v.MyEntity_OnStartPost(this);}
            public void PublishRemove(){foreach(var v in pres.ToArray())v.MyEntity_RemovePre(this);foreach(var v in removes.ToArray())v.MyEntity_OnRemove(this);}
            public override void SetClientVisibilitySupporter(IClientVisibilitySupporter value)=>ClientVisibilitySupporter=value;
            public override void AddStartReceiver(IMyEntityStartEventReceiver v)=>starts.Add(v);public override void RemoveStartReceiver(IMyEntityStartEventReceiver v)=>starts.Remove(v);public override void AddRemoveReceiver(IMyEntityRemoveEventReceiver v)=>removes.Add(v);public override void RemoveRemoveReceiver(IMyEntityRemoveEventReceiver v)=>removes.Remove(v);public override void AddStartPostReceiver(IMyEntityStartPostEventReceiver v)=>posts.Add(v);public override void RemoveStartPostReceiver(IMyEntityStartPostEventReceiver v)=>posts.Remove(v);public override void AddRemovePreReceiver(IMyEntityRemovePreEventReceiver v)=>pres.Add(v);public override void RemoveRemovePreReceiver(IMyEntityRemovePreEventReceiver v)=>pres.Remove(v);public void AddReplaceReceiver(IMyEntityReplaceEventReceiver v)=>replacements.Add(v);public void RemoveReplaceReceiver(IMyEntityReplaceEventReceiver v)=>replacements.Remove(v);
        }
        private sealed class DataInput:ECViewEntityData
        {
            public readonly Map13WorldStageQuery Stage=new Map13WorldStageQuery{Info=new Map13StageStateQueryData{StateInfos=new List<StageStateInfo>{new StageStateInfo{SubStateList=new List<SubStageStateInfo>{new SubStageStateInfo{Results=new List<StageCompCfg>{new StageCompCfg{CompType=StageKind.ResultAwardItem,CompIndex=0}}}}}}}};
            public readonly Map13AwardItemQuery Awards=new Map13AwardItemQuery{Info=new Map13AwardItemQueryData{AwardItemInfos=new List<StageSingleAwardItemInfo>{new StageSingleAwardItemInfo{ItemId=1001,Count=4}}}};
            public readonly SharedState Shared=new SharedState();public readonly Map13VisibilityData Visibility=new Map13VisibilityData();public readonly Map13EntitySourceQuery Source=new Map13EntitySourceQuery();public readonly Map13EntityInteractionQuery Interaction=new Map13EntityInteractionQuery();public readonly Map13GridTransformData Grid=new Map13GridTransformData();
            public DataInput(){Source.Entity=this;Interaction.mEntSrcComp=Source;}
            public override int ClassId=>8363;public override EcEntityIdentifier Id=>new EcEntityIdentifier(13,8363);
            public override T GetComponent<T>(){object value=typeof(T)==typeof(Map13WorldStageQuery)?Stage:typeof(T)==typeof(Map13AwardItemQuery)?Awards:typeof(T)==typeof(ECWorldStageStateData)?Shared:typeof(T)==typeof(Map13VisibilityData)?Visibility:typeof(T)==typeof(Map13EntitySourceQuery)?Source:typeof(T)==typeof(Map13EntityInteractionQuery)?Interaction:typeof(T)==typeof(Map13GridTransformData)?Grid:null;return value==null?default:(T)value;}
        }
        private sealed class SharedState:ECWorldStageStateData{public int Value;public override int State=>Value;public override Zjcs.Official.World.OfficialStageStateInfo GetCurStateInfo()=>null;public override int GetStateComponentIndex(Zjcs.Official.EntityWorldStages.StageComponentType type,int subStateIndex=0)=>-1;}
        private sealed class Clock:Map13PresentationServices{public override long PlayerId=>1;public override DateTime GetDateTime()=>DateTime.UtcNow;}
        private sealed class Services:IServiceProvider,IECViewInjection,IECViewMapRootLifetime,IECViewFrameSource,IEcEntityBufferEventHandleModule,IMap13FadeMapState
        {
            private readonly MyPlayableManager manager;private readonly Clock clock=new Clock();private Action<ECViewFrameUnit> updates,lates;private Action<EntityWorldStageUpdateEventMessage> stages;
            public Services(MyPlayableManager manager){this.manager=manager;}public bool Destroying=>false;public bool Initialized=>true;public bool IsFront=>true;public Map13LoadingPanelState LoadingPanel{get;}=new Map13LoadingPanelState{SplashStatus=SplashStatus.Hide};public int Subscribers=>stages?.GetInvocationList().Length??0;
            public object GetService(Type type){if(type==typeof(IServiceProvider))return this;if(type==typeof(IMyPlayableManager))return manager;if(type==typeof(Map13PresentationServices))return clock;if(type.IsInstanceOfType(this))return this;return null;}
            public void Inject(object value){foreach(var property in value.GetType().GetProperties().Where(p=>p.CanWrite&&p.GetIndexParameters().Length==0)){var service=GetService(property.PropertyType);if(service!=null)property.SetValue(value,service);}}
            public IDisposable SubscribeUpdate(Action<ECViewFrameUnit> value){updates+=value;return new Lease(()=>updates-=value);}public IDisposable SubscribePreLateUpdate(Action<ECViewFrameUnit> value){lates+=value;return new Lease(()=>lates-=value);}public void Update()=>updates?.Invoke(default);public void LateUpdate()=>lates?.Invoke(default);
            public IDisposable Subscribe<T>(ECViewEntityData entity,Action<T> value)where T:IEcEntityBufferEventContext{Require(typeof(T)==typeof(EntityWorldStageUpdateEventMessage),"Unexpected buffered event.");var callback=(Action<EntityWorldStageUpdateEventMessage>)(object)value;stages+=callback;return new Lease(()=>stages-=callback);}public void Emit(SharedState state,int previous)=>stages?.Invoke(new EntityWorldStageUpdateEventMessage{StageComp=state,PreState=previous});
        }
        private sealed class Lease:IDisposable{private Action dispose;public Lease(Action value){dispose=value;}public void Dispose(){var old=dispose;dispose=null;old?.Invoke();}}
        private sealed class Factory:IStoryPlayableInstanceFactory{private readonly MyPlayableCenter center;private readonly Transform staging;public int Created;public Factory(MyPlayableCenter center,Transform staging){this.center=center;this.staging=staging;}public GameObject Instantiate(GameObject prefab,Transform parent,bool worldPositionStays){var root=UnityEngine.Object.Instantiate(prefab,staging,false);foreach(var playable in root.GetComponentsInChildren<MyPlayable>(true))playable.MyPlayableCenter=center;root.transform.SetParent(parent,worldPositionStays);Created++;return root;}}
        [Serializable]private sealed class Source{public Entry[] inputs,outputs,tools;}
        [Serializable]private sealed class Entry{public string path,sha256;}
        [Serializable]private sealed class Proof{public string source_sha256,editor,backend;public int entities,original_objects,ordered_views,pooled_performances,rendered_pixels;public bool play_mode;}
    }
}
