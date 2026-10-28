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
using Zjcs.Official.StorySourceFade;
using Zjcs.Official.StorySounds;
using Zjcs.Official.ProfessionUI;
using System.Collections.Generic;
using Zjcs.Official.StoryPlayables;
using AnimationController=Zjcs.Official.StoryAnimation.AnimationController;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14PlayableManagerAcceptance
    {
        private const string Key="Zjcs.Map14.PlayableManager";
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
            if(errors==0){SessionState.SetInt(Key+".result",0);Debug.Log("Map14 original playable manager Play Mode passed: native chaos appearance through original FIFO pool, three plays, original return/reset/order, preload, low-memory release, removal guard and activation messages. Actor source is isolated; formal story/map/actor/audio/save and Windows Player remain pending.");}EditorApplication.isPlaying=false;
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException("Native chaos appearance playable: "+message);}
        private static IEnumerator Steps()
        {
            Application.runInBackground=true;typeof(OfficialMap14BubbleAcceptance).GetMethod("SetView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{450,800});yield return null;yield return null;
            var cameraObject=new GameObject("Original terrain validation camera");cameraObject.tag="MainCamera";var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=12;camera.transform.position=new Vector3(0,6,-100);camera.farClipPlane=1000;cameraObject.AddComponent<UniversalAdditionalCameraData>().SetRenderer(0);
            var environment=typeof(OfficialMap14SuiMoPerformanceAcceptance).GetMethod("CreateEnvironment",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);yield return null;yield return null;
            var source=new GameObject("Isolated actor source for original pooled appearance");source.transform.position=new Vector3(0,6,0);var bone=new GameObject("CommonEffectSlot");bone.transform.SetParent(source.transform,false);source.AddComponent<AttachAgent>().GenerateBoneMap();
            var fade=source.AddComponent<FadeListProbe>();var bridge=source.AddComponent<FadeBridge>();bridge.Supporter=new EntityFadeAnimationSupporter(fade);
            var scope=new GameObject("Isolated original playable scope");var staging=new GameObject("Inactive dependency-injection staging");staging.SetActive(false);var center=new MyPlayableCenter();var factory=new Factory(center,staging.transform);var lowMemory=new Zjcs.Official.StoryPlayables.GAction();
            var oldFactory=StoryPlayableInstantiation.Resolve;StoryPlayableInstantiation.Resolve=root=>root.IsChildOf(scope.transform)?factory:null;
            int activated=0,deactivated=0;var manager=new MyPlayableManager{ScopeRoot=scope,InstanceFactory=factory,MyPlayableCenter=center,LowMemory=lowMemory,
                ActivePublisher=info=>{Require(info.MyPlayableManager!=null,"original activation payload");activated++;},InactivePublisher=info=>{Require(info.MyPlayableManager!=null,"original deactivation payload");deactivated++;}};
            manager.Initialize();manager.OnActivate();Require(activated==1&&manager.PlayableSlot.parent==scope.transform,"original activation and scope parenting");Require(manager.Play(null,source,null)==null,"original null-prefab early return");
            var prefab=Resources.Load<NativeMap13AssetCatalog>("Map14/Native/native-chaos-appearance-playable-catalog").Load<GameObject>(Guids[0]);
            manager.PrepareLoadObjects(new[]{new PreLoadData{Prefab=prefab,Count=2},new PreLoadData{Prefab=null,Count=9},new PreLoadData{Prefab=prefab,Count=0}});Require(factory.Created==2&&factory.Instances.All(x=>!x.activeInHierarchy),"original preload extends exactly the configured valid count into inactive pools");
            var oldSound=OfficialWwiseEvent.Host;var sound=new SoundProbe();OfficialWwiseEvent.Host=sound;
            var ids=new List<int>();int stops=0;bool rendered=false;int peak=0;
            for(int replay=0;replay<3;replay++)
            {
                var parameters=new Dictionary<string,object>{{"source-order-probe",replay}};var needStop=new NeedStopProbe();bridge.Supporter.ResetFadeAnimation();var playable=manager.Play(prefab,source,source,1,scope,parameters,needStop);ids.Add(playable.GetInstanceID());
                Require(playable.Prefab==prefab&&playable.mySource==source&&playable.myTarget==source&&playable.myParentGo==scope&&ReferenceEquals(playable.myParam,parameters)&&ReferenceEquals(playable.NeedStopObj,needStop),"original manager forwards source, target, parent, parameters and stop object");
                Action<MyPlayable> stopped=p=>{Require(p.mySource==source&&p.myStatus==MyPlayable.PlayableStatus.Stop,"original OnStop precedes return/reset");stops++;};playable.OnStop+=stopped;
                // The time-zero original attachment already reparents the FX
                // beneath the source bone before manager.Play returns.
                var particles=playable.GetComponentsInChildren<ParticleSystem>(true).Concat(source.GetComponentsInChildren<ParticleSystem>(true)).Distinct().ToArray();float elapsed=0;
                while(elapsed<2.7f)
                {
                    yield return null;var delta=UnityEngine.Time.deltaTime;elapsed+=delta;center.Update(delta);center.LateUpdate();int live=particles.Sum(p=>p?p.particleCount:0);peak=Math.Max(peak,live);
                    if(!rendered&&live>0){Capture(camera,"pooled-chaos-appearance.png");rendered=true;}
                }
                playable.OnStop-=stopped;Require(playable.myStatus==MyPlayable.PlayableStatus.Stop&&!playable.gameObject.activeSelf&&!playable.myInited&&playable.mySource==null&&playable.myTarget==null&&playable.myParam==null&&playable.NeedStopObj==null,"original natural stop resets data before deactivate and return");
                Require(fade.Alpha==1&&fade.IsIn,"original source fade completes through pooled play");
            }
            Require(stops==3&&ids[0]!=ids[1]&&ids[0]==ids[2]&&factory.Created==2,"original FIFO reuses preloaded instances without allocating a third object");Require(rendered&&peak>0,"pooled native original particles actually render");Require(sound.Events.SequenceEqual(new uint[]{2969657999,2969657999,2969657999}),"original sound is dispatched once on each pooled reuse");
            manager.RemoveAllPlayable(true);Require(factory.Instances.All(x=>x),"original empty-active early return does not release idle objects");lowMemory.Invoke();yield return null;Require(factory.Instances.All(x=>!x),"original low-memory event releases queued instances");
            var first=manager.Play(prefab,source,source);var second=manager.Play(prefab,source,source);Require(factory.Created==4&&first!=second,"original empty pool creates two concurrent instances");bool guardObserved=false;Action<MyPlayable> guard=p=>{guardObserved=true;Require(manager.Play(prefab,source,null)==null,"original removal rejects reentrant playback");};first.OnStop+=guard;
            manager.RemoveAllPlayable(false);first.OnStop-=guard;Require(guardObserved&&!first.gameObject.activeSelf&&!second.gameObject.activeSelf&&first.mySource==null&&second.mySource==null,"original guarded bulk removal returns all active playables");
            manager.ForceReleaseObjectPool();yield return null;Require(!first&&!second,"explicit original pool release destroys returned objects");
            manager.OnDeactivate();Require(deactivated==1,"original deactivation payload");manager.Dispose();lowMemory.Invoke();center.RemoveAll();OfficialWwiseEvent.Host=oldSound;StoryPlayableInstantiation.Resolve=oldFactory;
            Debug.Log("Original playable manager verified: preload=2; plays=3; FIFO IDs="+string.Join(",",ids)+"; peak native particles="+peak+"; sound IDs="+string.Join(",",sound.Events)+"; low-memory and removal guard passed.");
            UnityEngine.Object.Destroy(scope);UnityEngine.Object.Destroy(staging);UnityEngine.Object.Destroy(source);var envType=environment.GetType();UnityEngine.Object.Destroy((GameObject)envType.GetField("Root").GetValue(environment));yield return null;((IDisposable)envType.GetField("Labels").GetValue(environment)).Dispose();UnityEngine.Object.Destroy((UnityEngine.Object)envType.GetField("Catalog").GetValue(environment));UnityEngine.Object.Destroy(cameraObject);
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
