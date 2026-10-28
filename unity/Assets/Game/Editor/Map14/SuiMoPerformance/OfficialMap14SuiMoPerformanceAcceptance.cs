using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cinemachine;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Timeline;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryAnimation;
using Zjcs.Official.StoryAnimatorTimeline;
using Zjcs.Official.StoryNodes;
using Zjcs.Official.World.NativeEnvironment;
using Zjcs.Official.World.NativeTerrain;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Offline.World.NativeEnvironment;
using Zjcs.Offline.World.NativeMap13;
using Zjcs.Offline.World.NativePartition;
using AnimationController = Zjcs.Official.StoryAnimation.AnimationController;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14SuiMoPerformanceAcceptance
    {
        private const string Key="Zjcs.Map14.SuiMoNativePerformance";
        private const string PrefabPath="Assets/Official/World/Map/14/NativeSuiMoPerformance/Prefabs/Playables/DisplayPackage/Story/Timeline/Map14/SuiMo_StoryShow.prefab";
        private const string CatalogPath="Assets/Game/Resources/Map14/Native/native-suimo-performance-catalog.asset";
        private static bool started;private static int errors;
        [Serializable] private sealed class Shared {public string pipeline_catalog,environment_data;}
        [Serializable] private sealed class NativeOutput {public string path;}
        [Serializable] private sealed class Mip {public int level;public string rgba_sha256;}
        [Serializable] private sealed class NativeTexture {public bool reused;public NativeOutput output;public Mip[] mips;}
        [Serializable] private sealed class NativeSource {public NativeTexture[] textures;}
        private static string Repository=>Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT")??throw new InvalidOperationException("Explicit validation repository is required.");
        private static string Output=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR")??throw new InvalidOperationException("Explicit validation workspace is required.");
        private static T SharedAsset<T>(bool pipeline) where T:UnityEngine.Object
        {
            var source=JsonUtility.FromJson<Shared>(File.ReadAllText(Path.Combine(Repository,"assets/reference/official-map/map14-shared-environment-source-manifest.json")));
            return AssetDatabase.LoadAssetAtPath<T>((pipeline?source.pipeline_catalog:source.environment_data).Substring(6));
        }
        public static void Run()
        {
            try
            {
                if(!Path.GetFullPath(Output).Replace('\\','/').Contains("/output/validation/"))throw new InvalidOperationException("Use a named output validation workspace.");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                Require(prefab && prefab.GetComponentsInChildren<Transform>(true).Length==19,"original native prefab imports with all 19 transforms");
                Require(prefab.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"all original scripts are explicitly bound");
                var catalog=AssetDatabase.LoadAssetAtPath<NativeMap13AssetCatalog>(CatalogPath);
                if(!catalog){catalog=ScriptableObject.CreateInstance<NativeMap13AssetCatalog>();AssetDatabase.CreateAsset(catalog,CatalogPath);}
                catalog.Bindings=new[]{new NativeMap13AssetCatalog.Binding{OfficialPath="Assets/Prefabs/Playables/DisplayPackage/Story/Timeline/Map14/SuiMo_StoryShow.prefab",Asset=prefab}};
                EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
                Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor",true).GetMethod("Validate").Invoke(null,null);
                SessionState.SetString(Key+".pipeline",AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
                SessionState.SetString(Key+".quality",AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
                SessionState.SetInt(Key+".color",(int)PlayerSettings.colorSpace);PlayerSettings.colorSpace=ColorSpace.Linear;
                GraphicsSettings.defaultRenderPipeline=SharedAsset<NativeMap13AssetCatalog>(true).Load<UniversalRenderPipelineAsset>("globalgamemanagers.assets:22209");
                QualitySettings.renderPipeline=null;
                SessionState.SetBool(Key,true);SessionState.SetInt(Key+".result",1);SessionState.SetFloat(Key+".deadline",(float)EditorApplication.timeSinceStartup+300);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Hook();EditorApplication.isPlaying=true;
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        [InitializeOnLoadMethod] private static void Hook()
        {
            if(!SessionState.GetBool(Key,false))return;started=false;errors=0;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged-=Mode;EditorApplication.playModeStateChanged+=Mode;
        }
        private static void Mode(PlayModeStateChange mode)
        {
            if(mode!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key,false))return;
            Application.logMessageReceived-=Observe;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=Mode;
            GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key+".pipeline",""));
            QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key+".quality",""));
            PlayerSettings.colorSpace=(ColorSpace)SessionState.GetInt(Key+".color",0);EditorApplication.Exit(SessionState.GetInt(Key+".result",1));
        }
        private static void Observe(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private static void Tick()
        {
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".deadline",0)){Debug.LogError("Original SuiMo performance timed out.");EditorApplication.isPlaying=false;return;}
            if(started||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;started=true;Application.logMessageReceived+=Observe;
            new GameObject("Original native SuiMo validation").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            var stack=new Stack<IEnumerator>();stack.Push(Steps());
            while(stack.Count>0)
            {
                object value;
                try{if(!stack.Peek().MoveNext()){stack.Pop();continue;}value=stack.Peek().Current;if(value is IEnumerator child){stack.Push(child);continue;}}
                catch(Exception e){Debug.LogException(e);EditorApplication.isPlaying=false;yield break;}
                yield return value;
            }
            if(errors==0){SessionState.SetInt(Key+".result",0);Debug.Log("Map14 original SuiMo native performance Play Mode passed: original hierarchy/dependencies, native Timeline camera/animation/control tracks, particle rendering over original map terrain, original inactive trail bindings, original sound event dispatch and player affirmative parameter. Player model/animation, sound-bank playback, formal story/map/save/Windows Player remain pending.");}
            EditorApplication.isPlaying=false;
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException("Original SuiMo: "+message);}
        private sealed class PlayerServices:IPlayerAnimatorTimelineServices {public GameObject PlayerEntity{get;set;}}
        private sealed class SoundProbe:IProfessionSoundHost
        {
            public readonly List<uint> Events=new List<uint>();
            public uint PostEvent(uint eventId,GameObject emitter){Require(emitter,"original sound emitter is bound");Events.Add(eventId);return (uint)Events.Count;}
        }
        private static IEnumerator Steps()
        {
            var source=JsonUtility.FromJson<NativeSource>(File.ReadAllText(Path.Combine(Repository,"assets/reference/official-map/map14-suimo-native-performance-source-manifest.json")));
            foreach(var record in source.textures.Where(t=>!t.reused))
            {
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(record.output.path.Substring(6));Require(texture,"original new effect texture imports");
                foreach(var mip in record.mips)
                {
                    var request=AsyncGPUReadback.Request(texture,mip.level);while(!request.done)yield return null;
                    Require(!request.hasError,"native effect texture mip GPU readback");
                    using(var hash=System.Security.Cryptography.SHA256.Create())
                        Require(BitConverter.ToString(hash.ComputeHash(request.GetData<byte>().ToArray())).Replace("-","").ToLowerInvariant()==mip.rgba_sha256,"all original decoded effect mip bytes reach the GPU unchanged");
                }
            }
            var oldPlayer=PlayerAnimatorTimelineServices.Current;var oldSound=OfficialWwiseEvent.Host;
            var sound=new SoundProbe();OfficialWwiseEvent.Host=sound;
            // Parameter fixture only; the original player model is a separate migration requirement.
            var player=new GameObject("Current player Animator parameter fixture");var animator=player.AddComponent<Animator>();
            var native=new AnimatorController{name="SuiMo player parameter fixture"};native.AddLayer("Base Layer");native.AddParameter("affirmative",AnimatorControllerParameterType.Trigger);
            var machine=native.layers[0].stateMachine;var idle=machine.AddState("Idle");machine.defaultState=idle;var affirmative=machine.AddState("affirmative");
            var transition=idle.AddTransition(affirmative);transition.duration=0;transition.hasExitTime=false;transition.AddCondition(AnimatorConditionMode.If,0,"affirmative");
            animator.runtimeAnimatorController=native;var animation=player.AddComponent<AnimationController>();animation.myAnimator=animator;animation.NotCreateGraph=true;animation.Init();animator.Update(0);
            PlayerAnimatorTimelineServices.Current=new PlayerServices{PlayerEntity=player};
            var cameraObject=new GameObject("WorldCamera");cameraObject.tag="MainCamera";var camera=cameraObject.AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=12;camera.transform.position=new Vector3(0,6,-100);camera.farClipPlane=1000;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.1f,.12f,1);camera.allowHDR=false;camera.allowMSAA=false;
            cameraObject.AddComponent<UniversalAdditionalCameraData>().SetRenderer(0);var brain=cameraObject.AddComponent<CinemachineBrain>();
            var image=new RenderTexture(450,800,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);image.Create();camera.targetTexture=image;
            var environment=CreateEnvironment();yield return null;yield return null;
            var staging=new GameObject("Original SuiMo binding staging");staging.SetActive(false);
            var catalog=Resources.Load<NativeMap13AssetCatalog>("Map14/Native/native-suimo-performance-catalog");
            var prefab=catalog.Load<GameObject>("Assets/Prefabs/Playables/DisplayPackage/Story/Timeline/Map14/SuiMo_StoryShow.prefab");
            var instance=UnityEngine.Object.Instantiate(prefab,staging.transform,false);var director=instance.GetComponent<PlayableDirector>();director.playOnAwake=false;
            var binder=instance.GetComponent<CinemachineTrackStoryBinder>();var nodes=new StoryNodeRegisterMgr();nodes.RegisterNode("WorldCamera",cameraObject);binder.NodeRegisterManager=nodes;
            var timeline=(TimelineAsset)director.playableAsset;Require(Math.Abs(timeline.duration-5.5)<.000001,"original Timeline fixed duration 5.5");
            Require(timeline.GetOutputTracks().Count()==8,"all eight original Timeline tracks are bound");
            var renderers=instance.GetComponentsInChildren<Renderer>(true);
            var originalRenderers=prefab.GetComponentsInChildren<Renderer>(true);int originalNullMaterials=0;
            Require(renderers.Length==originalRenderers.Length,"original native renderer order");
            for(int rendererIndex=0;rendererIndex<renderers.Length;rendererIndex++)
            {
                var renderer=renderers[rendererIndex];var expectedMaterials=originalRenderers[rendererIndex].sharedMaterials;
                Require(renderer.sharedMaterials.SequenceEqual(expectedMaterials),"original material bindings and null slots");
                Require((renderer.lightmapIndex&65535)==65535 && (renderer.realtimeLightmapIndex&65535)==65535,"original no-lightmap runtime sentinel");
                Require(renderer.lightmapScaleOffset==new Vector4(1,1,0,0)&&renderer.realtimeLightmapScaleOffset==new Vector4(1,1,0,0),"original runtime lightmap defaults");
                foreach(var material in renderer.sharedMaterials)
                {
                    if(!material){originalNullMaterials++;continue;}
                    Require(material.shader&&material.shader.isSupported&&material.shader.name!="Hidden/InternalErrorShader",
                        "original converted material shader: "+renderer.name+" / "+(material?material.name:"null material")+" / "+(material&&material.shader?material.shader.name:"null shader"));
                }
            }
            Require(originalNullMaterials==3,"three original null material slots are preserved");
            instance.transform.SetParent(null,false);UnityEngine.Object.Destroy(staging);
            Require(timeline.GetOutputTracks().OfType<CinemachineTrack>().All(t=>director.GetGenericBinding(t)==brain),"original binder fills unbound Cinemachine track");
            director.Play();var samples=new[]{.5f,1.5f,2.5f,3.5f,4.5f,5.25f};var particles=0;var trailPositions=0;var distinct=new HashSet<string>();
            foreach(float time in samples)
            {
                while(director.time<time && director.state==PlayState.Playing)
                {
                    particles=Math.Max(particles,instance.GetComponentsInChildren<ParticleSystem>(true).Sum(p=>p.particleCount));
                    trailPositions=Math.Max(trailPositions,instance.GetComponentsInChildren<TrailRenderer>(true).Sum(t=>t.positionCount));
                    yield return null;
                }
                yield return new WaitForEndOfFrame();
                particles=Math.Max(particles,instance.GetComponentsInChildren<ParticleSystem>(true).Sum(p=>p.particleCount));
                trailPositions=Math.Max(trailPositions,instance.GetComponentsInChildren<TrailRenderer>(true).Sum(t=>t.positionCount));
                if(Mathf.Abs(time-1.5f)<.01f)
                {
                    Debug.Log("Original SuiMo clock: scale="+Time.timeScale+"; delta="+Time.deltaTime);
                    foreach(var a in instance.GetComponentsInChildren<Animator>(true))Debug.Log("Original SuiMo Animator sample: "+a.name+"; position="+a.transform.localPosition+"; culling="+a.cullingMode+"; rootMotion="+a.applyRootMotion);
                    foreach(var trail in instance.GetComponentsInChildren<TrailRenderer>(true))Debug.Log("Original SuiMo trail sample: "+trail.name+"; active="+trail.gameObject.activeInHierarchy+"; enabled="+trail.enabled+"; emitting="+trail.emitting+"; count="+trail.positionCount+"; world="+trail.transform.position);
                }
                Require(brain.ActiveVirtualCamera!=null,"native Timeline drives the original virtual camera");
                var before=RenderTexture.active;RenderTexture.active=image;var shot=new Texture2D(image.width,image.height,TextureFormat.RGBA32,false,true);
                shot.ReadPixels(new Rect(0,0,image.width,image.height),0,0);shot.Apply();RenderTexture.active=before;
                byte[] png=shot.EncodeToPNG();File.WriteAllBytes(Path.Combine(Output,"suimo-"+time.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+".png"),png);
                using(var hash=System.Security.Cryptography.SHA256.Create())distinct.Add(Convert.ToBase64String(hash.ComputeHash(png)));
                UnityEngine.Object.Destroy(shot);
            }
            Require(particles>0,"original native Timeline emits particles");Require(distinct.Count>=3,"original camera/effect frames change over the native timeline");
            Require(sound.Events.Contains(2318116725),"original Map_Entity_Activate_6304_3 event dispatch");
            Require(animator.GetCurrentAnimatorStateInfo(0).IsName("affirmative"),"original player track triggers affirmative");
            Debug.Log("Original SuiMo native sample peaks: particles="+particles+"; trailPositions="+trailPositions+"; changingFrames="+distinct.Count+"; soundEvents="+sound.Events.Count);
            director.Stop();UnityEngine.Object.Destroy(instance);yield return null;
            UnityEngine.Object.Destroy(environment.Root);yield return null;environment.Labels.Dispose();UnityEngine.Object.Destroy(environment.Catalog);
            camera.targetTexture=null;image.Release();UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(cameraObject);UnityEngine.Object.Destroy(player);UnityEngine.Object.Destroy(native);
            OfficialWwiseEvent.Host=oldSound;PlayerAnimatorTimelineServices.Current=oldPlayer;
        }
        private sealed class EnvironmentFixture {public GameObject Root;public IDisposable Labels;public NativeMap13AssetCatalog Catalog;}
        private static EnvironmentFixture CreateEnvironment()
        {
            var names=new[]{"native-terrain-catalog","native-scene-catalog","native-environment-scene-catalog","native-region-labels-catalog"};
            var catalogs=names.Select(n=>Resources.Load<NativeMap13AssetCatalog>("Map14/Native/"+n)).ToArray();Require(catalogs.All(c=>c),"original map native catalogs");
            var catalog=ScriptableObject.CreateInstance<NativeMap13AssetCatalog>();catalog.Bindings=catalogs.SelectMany(c=>c.Bindings).GroupBy(b=>b.OfficialPath).Select(g=>g.First()).ToArray();
            var original=catalog.Load<PartitionData>("Assets/MonoBehaviour/partition_data");var data=UnityEngine.Object.Instantiate(original);
            var host=new GameObject("Original Map14 performance environment");host.SetActive(false);
            var instance=UnityEngine.Object.Instantiate(catalog.Load<GameObject>("Assets/Partition/MapGround_14/MapGround_14_bare"),host.transform,false);var root=instance.GetComponent<MapGroundRoot>();
            // Reuse the already verified environment/label fixtures, without changing product rules or user saves.
            var type=typeof(OfficialMap14LabeledEnvironmentAcceptance);var flags=BindingFlags.NonPublic;
            var serviceType=type.GetNestedType("Services",flags);var labelType=type.GetNestedType("LabelFixture",flags);
            var services=Activator.CreateInstance(serviceType,new object[]{root,SharedAsset<MapEnvData>(false),MapRegionStatus.Public,data.RegionDatas.Length});
            var labels=Activator.CreateInstance(labelType,new[]{services});var bind=labelType.GetMethod("Bind");
            Action<GameObject> bindInstance=child=>{NativeMap13EnvironmentBinding.Bind(child,(IMapEnvironmentServices<MapGroundRoot,MapEnvData>)services);bind.Invoke(labels,new object[]{child});};
            bindInstance(instance);var resources=host.AddComponent<NativeMap13ResourceLoader>();resources.Catalog=catalog;resources.BindInstance=bindInstance;
            var controller=root.PartitionController;controller.PartitionData=data;controller.Context=(IPartitionWorldContext<MapRegionStatus>)services;
            var loader=controller.Loader;loader.ResourceLoader=resources;loader.ServiceProvider=resources;loader.IsCameraReady=()=>true;loader.IsOtherGameRunning=()=>false;loader.SetIgnoreCamera(true);
            host.SetActive(true);for(int y=data.MinY;y<=data.MaxY;y++)for(int x=data.MinX;x<=data.MaxX;x++)loader.LoadVicinity(x,y);
            return new EnvironmentFixture{Root=host,Labels=(IDisposable)labels,Catalog=catalog};
        }
        private sealed class Runner:MonoBehaviour{}
    }
}
