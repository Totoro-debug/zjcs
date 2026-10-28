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
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StorySourceFade;
using Zjcs.Official.StorySounds;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.World.NativeMap13;
using Zjcs.Offline.World.NativeMap14EntityAssets;
using GAction=Zjcs.Official.StoryPlayables.GAction;

namespace Zjcs.Editor.Map14
{
    // Explicit isolated bridge/entity/handler inputs. The playback manager,
    // pool, fade controller, native Sprite and appearance prefab are real.
    public static class OfficialMap14EntityPlayableViewAcceptance
    {
        private const string Key="Map14.EntityPlayableView";
        private static bool started;
        private static int errors;
        private static string Output=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        [InitializeOnLoadMethod] private static void Hook()
        {
            if(!SessionState.GetBool(Key,false))return;
            started=false;errors=0;EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged-=Mode;EditorApplication.playModeStateChanged+=Mode;
        }
        public static void Run()
        {
            if(Environment.GetEnvironmentVariable("ZJCS_ISOLATED_ENTITY_VIEWS")=="1")
                Debug.Log("Explicit entity-view assembly isolation: full ProjectTools.Validate is not executed in this run.");
            else
            {
                var tools=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Zjcs.Editor.ProjectTools")).First(t=>t!=null);
                tools.GetMethod("Validate",BindingFlags.Static|BindingFlags.Public).Invoke(null,null);
            }
            SessionState.SetString(Key+".pipeline",AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
            SessionState.SetString(Key+".quality",AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
            SessionState.SetInt(Key+".color",(int)PlayerSettings.colorSpace);
            GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");
            QualitySettings.renderPipeline=null;PlayerSettings.colorSpace=ColorSpace.Linear;
            SessionState.SetBool(Key,true);SessionState.SetInt(Key+".result",1);
            SessionState.SetFloat(Key+".deadline",(float)EditorApplication.timeSinceStartup+240);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Hook();EditorApplication.isPlaying=true;
        }
        private static void Mode(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key,false))return;
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Observe;
            GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key+".pipeline",""));
            QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key+".quality",""));
            PlayerSettings.colorSpace=(ColorSpace)SessionState.GetInt(Key+".color",0);EditorApplication.Exit(SessionState.GetInt(Key+".result",1));
        }
        private static void Tick()
        {
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".deadline",0)){Debug.LogError("Entity playable view validation timed out.");EditorApplication.Exit(1);return;}
            if(started||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            started=true;Application.logMessageReceived+=Observe;new GameObject("Entity playable view validation").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static void Observe(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private static IEnumerator Verify()
        {
            var steps=Steps();while(true){object value;try{if(!steps.MoveNext())break;value=steps.Current;}catch(Exception e){Debug.LogException(e);EditorApplication.isPlaying=false;yield break;}yield return value;}
            if(errors==0){SessionState.SetInt(Key+".result",0);Debug.Log("Map14 original entity playable view Play Mode passed: real native appearance -> pooled loop, source binding, handlers, static/new state, visibility/fade gates and release. Bridge/entity/state handlers are isolated inputs; formal entity prefab/map/input/save/Player remain pending.");}EditorApplication.isPlaying=false;
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException("Entity playable view: "+message);}
        private static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        private static MyPlayable Current(ECPlayableSupporterView view)=>(MyPlayable)typeof(ECPlayableSupporterView).GetField("mCurrentPlayable",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
        private static IEnumerator Steps()
        {
            Application.runInBackground=true;Application.targetFrameRate=60;
            var camera=new GameObject("Original appearance probe camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=12;camera.aspect=450f/800;camera.transform.position=new Vector3(0,6,-100);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.08f,.08f,0);camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var illumination=new GameObject("Original Sprite illumination").AddComponent<Light2D>();illumination.lightType=Light2D.LightType.Global;illumination.intensity=1;
            var catalog=Resources.Load<NativeMap14EntitySpriteCatalog>("Map14/Native/entity-sprite-catalog");
            using(var sprites=new NativeMap14EntitySpriteLibrary(catalog))
            {
                var source=new GameObject("Isolated original source entity");
                var bone=new GameObject("CommonEffectSlot");bone.transform.SetParent(source.transform,false);source.AddComponent<AttachAgent>().GenerateBoneMap();
                var renderer=source.AddComponent<SpriteRenderer>();renderer.sprite=sprites.Require(catalog.sprites.Single(s=>s.originalName=="building_statue_doushi_TaoYuan").key);
                renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Official/World/Map/14/NativeEntityMaterials/08c41716e96c66ddcf71bd56dd72f6e2/1419505037369228759.mat");
                var list=source.AddComponent<SpriteListInput>();list.mySpriteList.Add(renderer);
                var fade=new EntityFadeAnimationSupporter(list);var fadeBridge=source.AddComponent<FadeBridge>();fadeBridge.Supporter=fade;
                var owner=new GameObject("Isolated entity view scope");owner.transform.SetParent(source.transform,false);
                var scope=new GameObject("Original pooled playable scope");var staging=new GameObject("Inactive injection staging");staging.SetActive(false);
                var center=new MyPlayableCenter();var factory=new Factory(center,staging.transform);var manager=new MyPlayableManager{ScopeRoot=scope,InstanceFactory=factory,MyPlayableCenter=center,LowMemory=new GAction(),ActivePublisher=_=>{},InactivePublisher=_=>{}};
                var oldFactory=StoryPlayableInstantiation.Resolve;StoryPlayableInstantiation.Resolve=root=>root.IsChildOf(scope.transform)?factory:null;manager.Initialize();manager.OnActivate();
                var oldSound=OfficialWwiseEvent.Host;var sound=new SoundProbe();OfficialWwiseEvent.Host=sound;
                var prefab=Resources.Load<NativeMap13AssetCatalog>("Map14/Native/native-chaos-appearance-playable-catalog").Load<GameObject>("dd56c0b7ddba41545a66512acc8f5ea1");
                Require(prefab.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"native appearance has unbound dynamic script dependencies");
                var handler=new HandlerInput();var view=new ECPlayableSupporterView{MyPlayableManager=manager,ServiceProvider=new Services()};
                Set(view,"mIsIgnoreFadeOut",true);Set(view,"mAppearPlayable",prefab);Set(view,"mLoopPlayable",prefab);Set(view,"mECPlayableHandlers",new List<ECPlayableBaseHandlerData>{new ECPlayableBaseHandlerData{Handler=handler}});
                var visibility=new ConstantClientVisibilitySupporter(list,null,owner.transform);var bridge=new BridgeInput(owner,visibility,fadeBridge,view);var lifecycle=(IECView)view;lifecycle.Init(bridge);lifecycle.Load(new DataInput(),false);int updates=0;view.OnPlayableSupporterUpdated+=()=>updates++;
                Require(handler.Initialized==1&&ReferenceEquals(handler.Supporter,view)&&view.IsNewEntity&&!view.IsVisible&&((IECPlayableSupporter)view).Source==source,"load/source/new state or handler injection differs");
                Require(((IECPlayableSupporter)view).GetECView<ECPlayableSupporterView>()==view&&((IECPlayableSupporter)view).GetComponent<string>()=="explicit component","original entity/view lookup forwarding differs");
                Require(view.PlayPlayable(null,source,source)==null,"original null prefab guard differs");lifecycle.OnShow();Require(Current(view)==null&&!view.IsVisible,"not-started client visibility gate differs");((IMyEntityStartPostEventReceiver)visibility).MyEntity_OnStartPost(null);lifecycle.OnShow();var appear=Current(view);
                Require(appear&&appear.mySource==source&&appear.myTarget==source&&ReferenceEquals(appear.NeedStopObj,visibility)&&!view.IsVisible&&handler.Shows==0,"appear must precede handler visibility and forward stop dependency");
                float elapsed=0;bool captured=false;int peak=0;
                while(elapsed<3.1f){yield return null;float delta=Time.deltaTime;elapsed+=delta;center.Update(delta);center.LateUpdate();int live=source.GetComponentsInChildren<ParticleSystem>(true).Sum(p=>p.particleCount)+scope.GetComponentsInChildren<ParticleSystem>(true).Sum(p=>p.particleCount);peak=Math.Max(peak,live);if(!captured&&live>0&&elapsed>.25f){Capture(camera,"entity-playable-appearance.png");captured=true;}}
                Require(captured&&peak>0&&view.IsVisible&&!view.IsNewEntity&&handler.Shows==1&&updates==1&&Current(view)&&factory.Created==2,"native appearance did not enter original pooled loop exactly once");
                var loop=Current(view);Require(loop!=appear&&sound.Events.Count==2,"appear stop must start loop before appear returns to pool");
                view.StartPlayableHandler<HandlerInput>();view.StopPlayableHandler<HandlerInput>();Require(handler.Shows==2&&handler.Hides==1&&view.GetPlayableHandler<HandlerInput>()==handler,"typed handler operations differ");
                lifecycle.OnHide();Require(!view.IsVisible&&!view.IsNewEntity&&Current(view)==null&&handler.Hides==2&&updates==2,"hide stop/handler/update order differs");
                // Original immediate Stop sets mShouldEnd; DoStop and pool
                // return happen when the center processes the next frame.
                for(int i=0;i<4&&loop.myStatus!=MyPlayable.PlayableStatus.Stop;i++){yield return null;center.Update(Time.deltaTime);center.LateUpdate();}
                Require(loop.myStatus==MyPlayable.PlayableStatus.Stop&&updates==2&&handler.Shows==2,"deferred original stop retained an appear-to-loop callback");
                lifecycle.OnShow();Require(view.IsVisible&&handler.Shows==3&&Current(view)!=null,"reshow must skip appear and start loop immediately; visible="+view.IsVisible+"; shows="+handler.Shows+"; sounds="+sound.Events.Count);
                float soundAt=((MyPlayableSequence)Current(view)).myPlayables.Single(c=>c.myPlayable is MyPlayableSound).myTime;
                float soundWait=0;while(soundWait<soundAt+.25f&&sound.Events.Count<3){yield return null;soundWait+=Time.deltaTime;center.Update(Time.deltaTime);center.LateUpdate();}
                Require(sound.Events.Count==3,"reshown original sequence did not dispatch its configured sound event; configuredTime="+soundAt+"; count="+sound.Events.Count);
                var unloading=Current(view);lifecycle.Unload();Require(Current(view)==null&&!view.IsVisible&&handler.Released==1,"unload did not stop/release handlers");
                for(int i=0;i<4&&unloading.myStatus!=MyPlayable.PlayableStatus.Stop;i++){yield return null;center.Update(Time.deltaTime);center.LateUpdate();}
                Require(unloading.myStatus==MyPlayable.PlayableStatus.Stop,"unload stop was not consumed by the original frame driver");
                manager.RemoveAllPlayable(true);manager.ForceReleaseObjectPool();
                // A separate original view with no visual playables isolates
                // fade subscriptions from the appearance prefab's own fades.
                var gateHandler=new HandlerInput();var gate=new ECPlayableSupporterView{MyPlayableManager=manager,ServiceProvider=new Services()};Set(gate,"mECPlayableHandlers",new List<ECPlayableBaseHandlerData>{new ECPlayableBaseHandlerData{Handler=gateHandler}});
                var gateBridge=new BridgeInput(owner,visibility,fadeBridge,gate);var gateLifecycle=(IECView)gate;gateLifecycle.Init(gateBridge);fade.ImmediateFadeOut(false);gateLifecycle.Load(new DataInput(),true);gateLifecycle.OnShow();
                Require(!gate.IsVisible&&!gate.IsNewEntity&&gateHandler.Shows==0,"fade-out/static gate differs");
                fade.ImmediateFadeIn();Require(gate.IsVisible&&gateHandler.Shows==1,"fade-in completion must start handlers");
                fade.ImmediateFadeOut();Require(!gate.IsVisible&&gateHandler.Hides==1,"fade-out start must hide handlers");
                fade.ImmediateFadeIn();Require(gate.IsVisible&&gateHandler.Shows==2,"fade-in restart differs");gateLifecycle.OnHide();gateLifecycle.Unload();int shown=gateHandler.Shows;fade.ImmediateFadeOut();fade.ImmediateFadeIn();Require(gateHandler.Shows==shown&&gateHandler.Released==1,"unloaded view retained fade callbacks");
                manager.OnDeactivate();manager.Dispose();center.RemoveAll();StoryPlayableInstantiation.Resolve=oldFactory;OfficialWwiseEvent.Host=oldSound;
                Debug.Log("Original entity playable view verified: native particle peak="+peak+"; pooled creations="+factory.Created+"; fade/handler/release paths passed.");
                UnityEngine.Object.Destroy(scope);UnityEngine.Object.Destroy(staging);UnityEngine.Object.Destroy(source);yield return null;
            }
            UnityEngine.Object.Destroy(camera.gameObject);UnityEngine.Object.Destroy(illumination);Require(errors==0,"unexpected runtime errors");
        }
        private static void Capture(Camera camera,string name)
        {
            var surface=new RenderTexture(450,800,24);surface.Create();var previous=RenderTexture.active;camera.targetTexture=surface;camera.Render();RenderTexture.active=surface;var image=new Texture2D(450,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,450,800),0,0);image.Apply();var pixels=image.GetPixels32();var background=pixels[0];int colored=pixels.Count(p=>Math.Abs(p.r-background.r)>2||Math.Abs(p.g-background.g)>2||Math.Abs(p.b-background.b)>2);File.WriteAllBytes(Path.Combine(Output,name),image.EncodeToPNG());Require(colored>100,"native GPU RGB frame is empty (additive FX need not write alpha)");RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.Destroy(image);surface.Release();UnityEngine.Object.Destroy(surface);
        }
        private sealed class DataInput:ECViewEntityData{public override int ClassId=>6650;public override EcEntityIdentifier Id=>new EcEntityIdentifier(14,101);public override T GetComponent<T>()=>(T)(object)"explicit component";}
        private sealed class Services:IServiceProvider{public object GetService(Type type)=>null;}
        private sealed class BridgeInput:IECViewBridge
        {
            private readonly ECPlayableSupporterView view;
            public BridgeInput(GameObject owner,IClientVisibilitySupporter visible,IEntityFadeAnimationBridge fade,ECPlayableSupporterView view){GameObject=owner;ClientVisibilitySupporter=visible;EntityFadeAnimationBridge=fade;this.view=view;}
            public IServiceProvider ServiceProvider{get;}=new Services();public CancellationToken DestroyCancellationToken=>CancellationToken.None;public GameObject GameObject{get;}public IClientVisibilitySupporter ClientVisibilitySupporter{get;}public IEntityFadeAnimationBridge EntityFadeAnimationBridge{get;}
            public long StartCoroutine(IEnumerator value)=>throw new InvalidOperationException("This original view does not start bridge coroutines.");public void StopCoroutine(long id)=>throw new InvalidOperationException("Unexpected bridge coroutine stop.");public T GetView<T>() where T:class,IECView=>view as T;
        }
        private sealed class HandlerInput:ECPlayableBaseHandler
        {public int Initialized,Shows,Hides,Released;public IECPlayableSupporter Supporter;public override void Init(IECPlayableSupporter view,IServiceProvider provider){base.Init(view,provider);Supporter=view;Initialized++;}public override void Visible(){Require(ServiceProvider!=null,"handler lost provider");Shows++;}public override void Invisible()=>Hides++;public override void Release()=>Released++;}
        private sealed class Factory:IStoryPlayableInstanceFactory
        {private readonly MyPlayableCenter center;private readonly Transform staging;public int Created;public Factory(MyPlayableCenter center,Transform staging){this.center=center;this.staging=staging;}public GameObject Instantiate(GameObject prefab,Transform parent,bool worldPositionStays){var instance=UnityEngine.Object.Instantiate(prefab,staging,false);foreach(var p in instance.GetComponentsInChildren<MyPlayable>(true))p.MyPlayableCenter=center;instance.transform.SetParent(parent,worldPositionStays);Created++;return instance;}}
        private sealed class FadeBridge:MonoBehaviour,IEntityFadeAnimationBridge{public IEntityFadeAnimationSupporter Supporter;public IEntityFadeAnimationSupporter EntityFadeAnimationSupporter=>Supporter;}
        private sealed class SpriteListInput:SpriteRendererList
        {public SpriteListInput(){mySpriteList=new List<SpriteRenderer>();FadeInCompleteActiveGO=new List<GameObject>();typeof(SpriteRendererList).GetField("mFordingSpriteList",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(this,new List<FordingSpriteRenderer>());typeof(SpriteRendererList).GetField("mCustomFadeList",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(this,new List<CustomRendererFade>());}}
        private sealed class SoundProbe:IProfessionSoundHost{public readonly List<uint> Events=new List<uint>();public uint PostEvent(uint id,GameObject emitter){Events.Add(id);return(uint)Events.Count;}}
        private sealed class Runner:MonoBehaviour{}
    }
}
