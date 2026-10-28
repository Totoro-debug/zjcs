using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Zjcs.Offline.World.NativeMap13;
using Zjcs.Official.StoryAnimation;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryFollowBone;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayableRequests;
using Zjcs.Official.StoryNodes;
using Cysharp.Threading.Tasks;
using System.Threading;
using Zjcs.Official.StorySourceFade;
using Zjcs.Official.StorySounds;
using Zjcs.Official.ProfessionUI;
using System.Collections.Generic;
using Zjcs.Official.StoryPlayables;
using AnimationController=Zjcs.Official.StoryAnimation.AnimationController;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14PlayableRequestsAcceptance
    {
        private const string Key="Zjcs.Map14.PlayableRequests";
        private const string CatalogPath="Assets/Game/Resources/Map14/Native/native-chaos-appearance-playable-catalog.asset";
        private const string Folder="Assets/Official/World/Map/14/NativeChaosAppearancePlayable/Prefabs/Playables/DisplayPackage/Story/";
        private static readonly string[] Names={"playable_chaos_appear"};
        private static readonly string[] Guids={"dd56c0b7ddba41545a66512acc8f5ea1"};
        private static bool started;private static int errors;
        [Serializable] private sealed class NativeOutput {public string path;}
        [Serializable] private sealed class Mip {public int level;public string rgba_sha256;}
        [Serializable] private sealed class NativeTexture {public bool reused;public NativeOutput output;public Mip[] mips;}
        [Serializable] private sealed class NativeSource {public NativeTexture[] textures;}
        private static string Output=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR")??throw new InvalidOperationException("Explicit validation workspace required.");
        public static void Run()
        {
            try
            {
                Require(Path.GetFullPath(Output).Replace('\\','/').Contains("/output/validation/"),"named isolated validation workspace");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var catalog=AssetDatabase.LoadAssetAtPath<NativeMap13AssetCatalog>(CatalogPath);
                if(!catalog){catalog=ScriptableObject.CreateInstance<NativeMap13AssetCatalog>();AssetDatabase.CreateAsset(catalog,CatalogPath);}
                catalog.Bindings=Names.Select((name,i)=>new NativeMap13AssetCatalog.Binding{OfficialPath=Guids[i],Asset=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+name+".prefab")}).ToArray();
                Require(catalog.Bindings.All(b=>b.Asset),"original chaos appearance playable imports");
                foreach(var binding in catalog.Bindings)
                {
                    var prefab=(GameObject)binding.Asset;Require(prefab.GetComponent<MyPlayableSequence>(),"original native sequence root");
                    Require(prefab.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"all original component script bindings");
                }
                EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
                Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor",true).GetMethod("Validate").Invoke(null,null);
                SessionState.SetString(Key+".pipeline",AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));SessionState.SetString(Key+".quality",AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));SessionState.SetInt(Key+".color",(int)PlayerSettings.colorSpace);
                var shared=typeof(OfficialMap14SuiMoPerformanceAcceptance).GetMethod("SharedAsset",BindingFlags.Static|BindingFlags.NonPublic).MakeGenericMethod(typeof(NativeMap13AssetCatalog));
                var pipeline=(NativeMap13AssetCatalog)shared.Invoke(null,new object[]{true});PlayerSettings.colorSpace=ColorSpace.Linear;GraphicsSettings.defaultRenderPipeline=pipeline.Load<UniversalRenderPipelineAsset>("globalgamemanagers.assets:22209");QualitySettings.renderPipeline=null;
                SessionState.SetBool(Key,true);SessionState.SetInt(Key+".result",1);SessionState.SetFloat(Key+".deadline",(float)EditorApplication.timeSinceStartup+240);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Hook();EditorApplication.isPlaying=true;
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        [InitializeOnLoadMethod] private static void Hook()
        {
            if(!SessionState.GetBool(Key,false))return;started=false;errors=0;EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.playModeStateChanged-=Mode;EditorApplication.playModeStateChanged+=Mode;
        }
        private static void Mode(PlayModeStateChange mode)
        {
            if(mode!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key,false))return;
            Application.logMessageReceived-=Observe;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=Mode;
            GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key+".pipeline",""));QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key+".quality",""));PlayerSettings.colorSpace=(ColorSpace)SessionState.GetInt(Key+".color",0);EditorApplication.Exit(SessionState.GetInt(Key+".result",1));
        }
        private static void Observe(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private static void Tick()
        {
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".deadline",0)){Debug.LogError("Native chaos appearance playable verification timed out.");EditorApplication.isPlaying=false;return;}
            if(started||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;started=true;Application.logMessageReceived+=Observe;new GameObject("Native chaos appearance playable verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            var steps=Steps();while(true){object value;try{if(!steps.MoveNext())break;value=steps.Current;}catch(Exception e){Debug.LogException(e);EditorApplication.isPlaying=false;yield break;}yield return value;}
            if(errors==0){SessionState.SetInt(Key+".result",0);Debug.Log("Map14 original story playable request bodies Play Mode passed: original per-index sources/targets, pooled native FX, actual stop waits, duration override, skip/cancel/timeout, selector and StoryPlayable callback/lease lifecycle. Graph lifecycle and actor views are isolated explicit bindings; formal mainline/map/actor/audio/save and Windows Player remain pending.");}EditorApplication.isPlaying=false;
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException("Native chaos appearance playable: "+message);}
        private static readonly List<string> Trace=new List<string>();
        private static IEnumerator Steps()
        {
            Application.runInBackground=true;typeof(OfficialMap14BubbleAcceptance).GetMethod("SetView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{450,800});yield return null;yield return null;
            var cameraObject=new GameObject("Original terrain validation camera");cameraObject.tag="MainCamera";var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=12;camera.transform.position=new Vector3(0,6,-100);camera.farClipPlane=1000;cameraObject.AddComponent<UniversalAdditionalCameraData>().SetRenderer(0);
            var environment=typeof(OfficialMap14SuiMoPerformanceAcceptance).GetMethod("CreateEnvironment",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);yield return null;yield return null;
            var first=Source("first",new Vector3(-1,6,0));var second=Source("second",new Vector3(1,6,0));var scope=new GameObject("Isolated story request scope");var staging=new GameObject("Inactive dependency injection staging");staging.SetActive(false);var center=new MyPlayableCenter();var factory=new Factory(center,staging.transform);var lowMemory=new Zjcs.Official.StoryPlayables.GAction();var host=new StoryHandleProxy();
            var oldFactory=StoryPlayableInstantiation.Resolve;StoryPlayableInstantiation.Resolve=root=>root.IsChildOf(scope.transform)?factory:null;
            var manager=new MyPlayableManager{ScopeRoot=scope,InstanceFactory=factory,MyPlayableCenter=center,LowMemory=lowMemory,ActivePublisher=host.BindMyPlayableManager,InactivePublisher=host.UnbindMyPlayableManager};manager.Initialize();manager.OnActivate();
            var prefab=Resources.Load<NativeMap13AssetCatalog>("Map14/Native/native-chaos-appearance-playable-catalog").Load<GameObject>(Guids[0]);var resources=new Loader(prefab);var oldSound=OfficialWwiseEvent.Host;var sound=new SoundProbe();OfficialWwiseEvent.Host=sound;
            Trace.Clear();var node=EntityNode(first,second,host,resources);node.IsWaitPlayableStop=true;node.IsStopAllPlayableWhenStoryFinish=true;node.IsStopEntitesPlayableSupportBeforeNodeRun=true;node.IsStartEntitiesPlayableSupportAfterNodeRun=true;node.OnStart();
            Require(Trace.SequenceEqual(new[]{"context-start","first-visible-True","second-visible-True","second-visible-True","first-visible-True","first-stop","second-stop","second-stop","first-stop"}),"original force-visible and per-index world-stage stop order, with duplicate entities preserved");
            var run=node.RunAsync(default).Preserve();Require(run.Status==UniTaskStatus.Pending,"original wait does not continue before actual stop");var active=Active(manager);Require(active.Count==2&&active.Any(p=>p.mySource==first.gameObject&&p.myTarget==second.gameObject)&&active.Any(p=>p.mySource==second.gameObject&&p.myTarget==first.gameObject),"original indexed source and target ports bind each actual native playable");
            var particles=first.GetComponentsInChildren<ParticleSystem>(true).Concat(second.GetComponentsInChildren<ParticleSystem>(true)).Distinct().ToArray();bool rendered=false;float elapsed=0;
            while(run.Status==UniTaskStatus.Pending&&elapsed<8)
            {
                yield return null;var delta=UnityEngine.Time.deltaTime;elapsed+=delta;center.Update(delta);center.LateUpdate();if(!rendered&&particles.Any(p=>p&&p.particleCount>0)){Capture(camera,"story-playable-request.png");rendered=true;}
            }
            Require(run.GetAwaiter().GetResult()==StoryRunNodeResultCode.OK&&rendered&&Active(manager).Count==0,"original waiting node continues only after both pooled native sequences stop");Trace.Clear();node.OnEnd();Require(Trace.SequenceEqual(new[]{"context-end","first-visible-False","second-visible-False","second-visible-False","first-visible-False","first-start","second-start","second-start","first-start","lease-1","lease-2"}),"original end/visibility/restart/lease-disposal order");node.CleanWhileStoryAllEnd();
            Trace.Clear();node=EntityNode(first,second,host,resources);node.IsWaitPlayableStop=false;node.IsStopAllPlayableWhenStoryFinish=false;node.OnStart();Require(node.RunAsync(default).GetAwaiter().GetResult()==StoryRunNodeResultCode.OK&&Active(manager).Count==2,"original non-waiting node continues while native playables run");node.OnEnd();node.CleanWhileStoryAllEnd();Require(Active(manager).Count==2,"original story-finish false retains non-waiting playables");manager.RemoveAllPlayable(false);
            node=EntityNode(first,second,host,resources);node.IsWaitPlayableStop=true;node.IsRunWhenSkip=true;node.PlayableDura=.25f;node.OnStart();Require(node.SkipAsync(default).GetAwaiter().GetResult()==StoryRunNodeResultCode.OK,"original run-when-skip branch");yield return null;center.Update(UnityEngine.Time.deltaTime);center.LateUpdate();Require(Active(manager).Count==0,"original waited skip requests stop and the next scheduler phase returns instances");node.OnEnd();node.CleanWhileStoryAllEnd();
            node=EntityNode(first,second,host,resources);node.IsWaitPlayableStop=true;node.PlayableDura=.25f;node.OnStart();var durationRun=node.RunAsync(default).Preserve();elapsed=0;while(durationRun.Status==UniTaskStatus.Pending&&elapsed<2){yield return null;elapsed+=UnityEngine.Time.deltaTime;center.Update(UnityEngine.Time.deltaTime);center.LateUpdate();}Require(durationRun.GetAwaiter().GetResult()==StoryRunNodeResultCode.OK&&elapsed<2,"original positive duration overrides native duration without replacing stop events");node.OnEnd();node.CleanWhileStoryAllEnd();
            node=EntityNode(first,second,host,resources);node.IsWaitPlayableStop=true;node.OnStart();var cancellation=new CancellationTokenSource();var cancelled=node.RunAsync(cancellation.Token).Preserve();node.TransitionToSkipAsync(default).GetAwaiter().GetResult();cancellation.Cancel();yield return null;bool wasCancelled=false;try{cancelled.GetAwaiter().GetResult();}catch(OperationCanceledException){wasCancelled=true;}Require(wasCancelled,"original run-to-skip cancellation propagates instead of synthesizing success");node.OnEnd();node.CleanWhileStoryAllEnd();manager.RemoveAllPlayable(false);cancellation.Dispose();
            node=EntityNode(first,second,host,resources);node.IsWaitPlayableStop=true;node.OnStart();float oldScale=UnityEngine.Time.timeScale;UnityEngine.Time.timeScale=20;var timeout=node.RunAsync(default).Preserve();float timeoutStart=UnityEngine.Time.time;while(timeout.Status==UniTaskStatus.Pending&&UnityEngine.Time.time-timeoutStart<30)yield return null;UnityEngine.Time.timeScale=oldScale;bool timedOut=false;try{timeout.GetAwaiter().GetResult();}catch(TimeoutException){timedOut=true;}Require(timedOut&&node.TimeoutResult==StoryRunNodeResultCode.OK,"original 20 scaled-second wait timeout and entity-node timeout result");node.OnEnd();node.CleanWhileStoryAllEnd();manager.RemoveAllPlayable(false);
            var custom=new PrefabProxy(prefab);var predefined=new PrefabProxy(prefab);var unused=new PrefabProxy(prefab);host.RegisterProxyLookup=key=>key=="native-chaos"?predefined:null;
            var gameNode=new PlayPlayableForGameObjectNode{StoryHandleProxy=host,IsWaitPlayableStop=true,Playables=new List<StoryPlayableSelect>{new StoryPlayableSelect{Type=EStoryPlayableSelectType.None,CustomAssetProxy=unused},new StoryPlayableSelect{Type=EStoryPlayableSelectType.Custom,CustomAssetProxy=custom},new StoryPlayableSelect{Type=EStoryPlayableSelectType.PreDefine,PreDefineKey="native-chaos",CustomAssetProxy=unused},new StoryPlayableSelect{Type=EStoryPlayableSelectType.PreDefine,PreDefineKey="missing"}}};BindContext(gameNode);gameNode.InputValue=key=>key.EndsWith("2")?second.gameObject:first.gameObject;gameNode.OnStart();var gameRun=gameNode.RunAsync(default).Preserve();elapsed=0;while(gameRun.Status==UniTaskStatus.Pending&&elapsed<8){yield return null;elapsed+=UnityEngine.Time.deltaTime;center.Update(UnityEngine.Time.deltaTime);center.LateUpdate();}Require(gameRun.GetAwaiter().GetResult()==StoryRunNodeResultCode.OK&&custom.Loads==1&&predefined.Loads==1&&unused.Loads==0,"original none/custom/predefined/missing selector branches use exactly their configured source and asset");gameNode.OnEnd();gameNode.CleanWhileStoryAllEnd();Require(custom.Unloads==1&&predefined.Unloads==1&&unused.Unloads==1&&gameNode.TimeoutResult==StoryRunNodeResultCode.SafeEnd,"original selector cleanup unloads both cached and custom proxies; none skips cleanup");
            var registry=new StoryNodeRegisterMgr();registry.RegisterNode("request-source",first.gameObject);registry.RegisterNode("request-target",second.gameObject);int completed=0;var wrapper=new StoryPlayable{NodeRegisterManager=registry,ResourceLoader=resources,SourceKey="request-source",TargetKey="request-target",Playable=GuidProxy(),IsWaitPlayableStop=true,PlayableDura=.25f};wrapper.Init(host,()=>completed++);wrapper.RunPlayable();Require(completed==0,"original StoryPlayable waits for actual OnStop callback");elapsed=0;while(completed==0&&elapsed<2){yield return null;elapsed+=UnityEngine.Time.deltaTime;center.Update(UnityEngine.Time.deltaTime);center.LateUpdate();}Require(completed==1,"original callback wrapper completes exactly once");wrapper.Clear();registry.UnregisterNode("request-source");registry.UnregisterNode("request-target");
            manager.RemoveAllPlayable(false);manager.ForceReleaseObjectPool();manager.OnDeactivate();manager.Dispose();lowMemory.Invoke();center.RemoveAll();OfficialWwiseEvent.Host=oldSound;StoryPlayableInstantiation.Resolve=oldFactory;
            Debug.Log("Original story request waits, indexed ports, stop/restart ordering, lease disposal, skip, cancellation, scaled timeout, selectors and callback wrapper passed; native sound dispatches="+sound.Events.Count);
            UnityEngine.Object.Destroy(scope);UnityEngine.Object.Destroy(staging);UnityEngine.Object.Destroy(first.gameObject);UnityEngine.Object.Destroy(second.gameObject);var envType=environment.GetType();UnityEngine.Object.Destroy((GameObject)envType.GetField("Root").GetValue(environment));yield return null;((IDisposable)envType.GetField("Labels").GetValue(environment)).Dispose();UnityEngine.Object.Destroy((UnityEngine.Object)envType.GetField("Catalog").GetValue(environment));UnityEngine.Object.Destroy(cameraObject);
        }
        private static PlayPlayableNode EntityNode(StoryPlayableEntityBinding first,StoryPlayableEntityBinding second,StoryHandleProxy host,Loader loader)
        {
            var node=new PlayPlayableNode{StoryHandleProxy=host,ResourceLoader=loader,PlayablesProxy=new List<AssetGuidProxy<GameObject>>{GuidProxy(),GuidProxy()}};BindContext(node);node.InputValue=key=>key.StartsWith("SourceEntitys")?(key.EndsWith("0")?first:second):(key.EndsWith("0")?second:first);node.InputValues=key=>key=="SourceEntitys"?new[]{first,second}:new[]{second,first};node.GetWorldStageSupport=entity=>new WorldSupport(entity.gameObject.name);return node;
        }
        private static void BindContext(StoryPlayableRequestNode node){node.StartContext=()=>Trace.Add("context-start");node.EndContext=()=>Trace.Add("context-end");node.CleanContext=()=>Trace.Add("context-clean");}
        private static AssetGuidProxy<GameObject> GuidProxy()=>JsonUtility.FromJson<AssetGuidProxy<GameObject>>("{\"mAssetGuid\":\"dd56c0b7ddba41545a66512acc8f5ea1\"}");
        private static List<MyPlayable> Active(MyPlayableManager manager)=>((HashSet<MyPlayable>)typeof(MyPlayableManager).GetField("mActivedPlayables",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(manager)).ToList();
        private static StoryPlayableEntityBinding Source(string name,Vector3 position)
        {
            var source=new GameObject(name);source.transform.position=position;var bone=new GameObject("CommonEffectSlot");bone.transform.SetParent(source.transform,false);source.AddComponent<AttachAgent>().GenerateBoneMap();var fade=source.AddComponent<FadeListProbe>();source.AddComponent<FadeBridge>().Supporter=new EntityFadeAnimationSupporter(fade);var binding=source.AddComponent<StoryPlayableEntityBinding>();binding.ClientVisibilitySupporter=new Visibility(name);return binding;
        }
        private sealed class Visibility:IStoryPlayableEntityVisibility{private readonly string name;public Visibility(string name){this.name=name;}public void SetForceVisible(bool value)=>Trace.Add(name+"-visible-"+value);}
        private sealed class WorldSupport:IStoryWorldStagePlayableSupport{private readonly string name;public WorldSupport(string name){this.name=name;}public void Stop()=>Trace.Add(name+"-stop");public void Start()=>Trace.Add(name+"-start");}
        private sealed class PrefabProxy:IStoryPrefabAssetProxy{private readonly GameObject prefab;public int Loads,Unloads;public PrefabProxy(GameObject prefab){this.prefab=prefab;}public GameObject Load(){Loads++;return prefab;}public void Unload(){Unloads++;}}
        private sealed class Loader:IProfessionImageResourceLoader
        {
            private readonly GameObject prefab;private int next;public Loader(GameObject prefab){this.prefab=prefab;}
            public IProfessionAssetOperation<T> LoadAssetByGuid<T>(string guid)where T:UnityEngine.Object{Require(guid==Guids[0],"original explicit native prefab GUID");return new Lease<T>((T)(UnityEngine.Object)prefab,++next);}
            public IProfessionAssetOperation<T> LoadAsset<T>(string path)where T:UnityEngine.Object=>throw new InvalidOperationException("GUID loading required");public IProfessionAssetOperation<T> LoadAssetAsync<T>(string path)where T:UnityEngine.Object=>throw new InvalidOperationException();public IProfessionAssetOperation<T> LoadAssetAsyncByGuid<T>(string guid)where T:UnityEngine.Object=>throw new InvalidOperationException();
        }
        private sealed class Lease<T>:IProfessionAssetOperation<T>where T:UnityEngine.Object
        {
            private readonly T result;private readonly int id;public Lease(T value,int id){result=value;this.id=id;}public bool IsValid=>true;public int Status=>2;public T Result=>result;public event Action<IProfessionAssetOperation<T>> Completed{add{}remove{}}public void Dispose()=>Trace.Add("lease-"+id);
        }
        private static void Capture(Camera camera,string name)
        {
            var surface=new RenderTexture(450,800,24,RenderTextureFormat.ARGB32);surface.Create();var previous=RenderTexture.active;camera.targetTexture=surface;camera.Render();RenderTexture.active=surface;var image=new Texture2D(450,800,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,450,800),0,0);image.Apply();RenderTexture.active=previous;camera.targetTexture=null;File.WriteAllBytes(Path.Combine(Output,name),image.EncodeToPNG());UnityEngine.Object.Destroy(image);surface.Release();UnityEngine.Object.Destroy(surface);
        }
        private sealed class Factory:IStoryPlayableInstanceFactory
        {
            private readonly MyPlayableCenter center;private readonly Transform staging;public int Created;public readonly List<GameObject> Instances=new List<GameObject>();
            public Factory(MyPlayableCenter center,Transform staging){this.center=center;this.staging=staging;}
            public GameObject Instantiate(GameObject prefab,Transform parent,bool worldPositionStays)
            {
                var instance=UnityEngine.Object.Instantiate(prefab,staging,false);foreach(var playable in instance.GetComponentsInChildren<MyPlayable>(true))playable.MyPlayableCenter=center;instance.transform.SetParent(parent,worldPositionStays);Created++;Instances.Add(instance);return instance;
            }
        }
        private sealed class NeedStopProbe:IMyPlayableNeedStop{public bool NeedStop()=>false;}
        private sealed class FadeBridge:MonoBehaviour,IEntityFadeAnimationBridge{public IEntityFadeAnimationSupporter Supporter;public IEntityFadeAnimationSupporter EntityFadeAnimationSupporter=>Supporter;}
        private sealed class FadeListProbe:SpriteRendererList
        {
            public FadeListProbe(){mySpriteList=new List<SpriteRenderer>();FadeInCompleteActiveGO=new List<GameObject>();typeof(SpriteRendererList).GetField("mFordingSpriteList",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(this,new List<FordingSpriteRenderer>());typeof(SpriteRendererList).GetField("mCustomFadeList",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(this,new List<CustomRendererFade>());}
        }
        private sealed class SoundProbe:IProfessionSoundHost{public readonly List<uint> Events=new List<uint>();public uint PostEvent(uint id,GameObject emitter){Events.Add(id);return(uint)Events.Count;}}
        private sealed class Runner:MonoBehaviour{}
    }
}
