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
using Zjcs.Presentation.World.NativeMap13;
using Zjcs.Official.StoryAnimation;
using Zjcs.Official.StoryAttachment;
using Zjcs.Official.StoryFollowBone;
using Zjcs.Official.StorySounds;
using Zjcs.Official.ProfessionUI;
using System.Collections.Generic;
using Zjcs.Official.StoryPlayables;
using AnimationController=Zjcs.Official.StoryAnimation.AnimationController;

namespace Zjcs.Editor.Map13NpcAppearancePlayables
{
    public static class OfficialMap13NpcAppearancePlayableAcceptance
    {
        private const string Key="Zjcs.Map13.NativeNpcAppearancePlayables";
        private const string CatalogPath="Assets/Game/Resources/Map13/Native/native-npc-appearance-playables-catalog.asset";
        private const string Folder="Assets/Official/World/Map/14/NativeNpcAppearancePlayables/Prefabs/Playables/DisplayPackage/Story/NPCAnimator/";
        private static readonly string[] Names={"playable_FeiLe_disappear","playable_FeiLe_appear","playable_npc_disappear","playable_npc_appear"};
        private static readonly string[] Guids={"5fd5bec6c4f91f54799a4b823888f49a","91ab22ab272abaf4eb63daaea8bb0d29","9b248cde91299ee4287533b25b7dd468","f4d2e846f96581348aa326c7686926cc"};
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
                Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
                Require(Path.GetFullPath(Output).Replace('\\','/').Contains("/output/validation/"),"named isolated validation workspace");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var catalog=AssetDatabase.LoadAssetAtPath<NativeMap13AssetCatalog>(CatalogPath);
                if(!catalog){catalog=ScriptableObject.CreateInstance<NativeMap13AssetCatalog>();AssetDatabase.CreateAsset(catalog,CatalogPath);}
                catalog.Bindings=Names.Select((name,i)=>new NativeMap13AssetCatalog.Binding{OfficialPath=Guids[i],Asset=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+name+".prefab")}).ToArray();
                Require(catalog.Bindings.All(b=>b.Asset),"all four original NPC appearance playables import");
                foreach(var binding in catalog.Bindings)
                {
                    var prefab=(GameObject)binding.Asset;Require(prefab.GetComponent<MyPlayableSequence>(),"original native sequence root");
                    Require(prefab.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"all original component script bindings");
                }
                EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
                Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor",true).GetMethod("Validate").Invoke(null,null);
                SessionState.SetString(Key+".pipeline",AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));SessionState.SetString(Key+".quality",AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));SessionState.SetInt(Key+".color",(int)PlayerSettings.colorSpace);
                PlayerSettings.colorSpace=ColorSpace.Linear;GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset");QualitySettings.renderPipeline=null;
                SessionState.SetBool(Key,true);SessionState.SetInt(Key+".result",1);SessionState.SetFloat(Key+".deadline",(float)EditorApplication.timeSinceStartup+240);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Hook();EditorApplication.isPlaying=true;
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        [InitializeOnLoadMethod] private static void Hook()
        {
            if(!SessionState.GetBool(Key,false))return;started=false;errors=0;Application.logMessageReceived-=Observe;Application.logMessageReceived+=Observe;EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.playModeStateChanged-=Mode;EditorApplication.playModeStateChanged+=Mode;
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
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".deadline",0)){Debug.LogError("Native NPC appearance playable verification timed out.");EditorApplication.isPlaying=false;return;}
            if(started||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;started=true;new GameObject("Native NPC appearance playable verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            var steps=Steps();while(true){object value;try{if(!steps.MoveNext())break;value=steps.Current;}catch(Exception e){Debug.LogException(e);EditorApplication.isPlaying=false;yield break;}yield return value;}
            if(errors==0){SessionState.SetInt(Key+".result",0);Debug.Log("Map13 shared original NPC appearance playable Play Mode passed: four original native appear/disappear prefabs, original sequence and follow/visibility callbacks, native particles/materials, two replays, original sound dispatch and rendered frames. The NPC parameter/bone source is isolated; original NPC model/skeleton animation, actual audio/DSP, formal story/map/save and Windows Player remain pending.");}EditorApplication.isPlaying=false;
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException("Native NPC appearance playable: "+message);}
        private static IEnumerator Steps()
        {
            var textureSource=JsonUtility.FromJson<NativeSource>(File.ReadAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT"),"assets/reference/official-map/map14-native-npc-appearance-playables-source-manifest.json")));int gpuMips=0;
            foreach(var record in textureSource.textures.Where(t=>!t.reused))
            {
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(record.output.path.Substring(6));Require(texture,"original new NPC effect texture imports");
                foreach(var mip in record.mips)
                {
                    var request=AsyncGPUReadback.Request(texture,mip.level);while(!request.done)yield return null;Require(!request.hasError,"original NPC effect mip GPU readback");
                    using(var hash=System.Security.Cryptography.SHA256.Create())Require(BitConverter.ToString(hash.ComputeHash(request.GetData<byte>().ToArray())).Replace("-","").ToLowerInvariant()==mip.rgba_sha256,"all decoded NPC effect mip bytes reach GPU unchanged");gpuMips++;
                }
            }
            Debug.Log("Original NPC appearance decoded GPU mips verified: "+gpuMips);
            Application.runInBackground=true;
            var cameraObject=new GameObject("Original Map13 effect validation camera");cameraObject.tag="MainCamera";var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=12;camera.transform.position=new Vector3(4.5f,74.5f,-500);camera.farClipPlane=1000;cameraObject.AddComponent<UniversalAdditionalCameraData>().SetRenderer(0);
            var worldRoot=new GameObject("Original Map13 ground and labels");
            var environment=OfficialMap13NativeWorldView.Create(worldRoot.transform,camera,new State());
            float deadline=Time.realtimeSinceStartup+120;while(!environment.IsReady&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(environment.IsReady,"original Map13 native partitions load for rendered effects");
            yield return null;yield return null;
            var source=new GameObject("Isolated NPC parameter/bone source, original skeleton pending");source.transform.position=new Vector3(4.5f,74.5f,0);var agent=source.AddComponent<AttachAgent>();agent.GenerateBoneMap();var animator=source.AddComponent<Animator>();var native=new AnimatorController();native.AddLayer("Base Layer");var state=native.layers[0].stateMachine.AddState("Probe");native.layers[0].stateMachine.defaultState=state;
            var catalog=Resources.Load<NativeMap13AssetCatalog>("Map13/Native/native-npc-appearance-playables-catalog");
            foreach(var key in Guids)foreach(var leaf in catalog.Load<GameObject>(key).GetComponentsInChildren<MyPlayableAnimator>(true))
            {
                if(!string.IsNullOrEmpty(leaf.myBoolParam)&&!native.parameters.Any(x=>x.name==leaf.myBoolParam))native.AddParameter(leaf.myBoolParam,AnimatorControllerParameterType.Bool);
                if(!string.IsNullOrEmpty(leaf.myFloatParam)&&!native.parameters.Any(x=>x.name==leaf.myFloatParam))native.AddParameter(leaf.myFloatParam,AnimatorControllerParameterType.Float);
                if(!string.IsNullOrEmpty(leaf.IntParam)&&!native.parameters.Any(x=>x.name==leaf.IntParam))native.AddParameter(leaf.IntParam,AnimatorControllerParameterType.Int);
                if(!string.IsNullOrEmpty(leaf.myTriggerParam)&&!native.parameters.Any(x=>x.name==leaf.myTriggerParam))native.AddParameter(leaf.myTriggerParam,AnimatorControllerParameterType.Trigger);
            }
            animator.runtimeAnimatorController=native;var controller=source.AddComponent<AnimationController>();controller.myAnimator=animator;controller.NotCreateGraph=true;controller.Init();animator.Update(0);
            var oldSound=OfficialWwiseEvent.Host;var sound=new SoundProbe();OfficialWwiseEvent.Host=sound;var expectedSound=new List<uint>();
            for(int index=0;index<Guids.Length;index++)
            {
                var prefab=catalog.Load<GameObject>(Guids[index]);var instance=UnityEngine.Object.Instantiate(prefab);var sequence=instance.GetComponent<MyPlayableSequence>();var center=new MyPlayableCenter();foreach(var playable in instance.GetComponentsInChildren<MyPlayable>(true))playable.MyPlayableCenter=center;
                foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    Require((renderer.lightmapIndex&65535)==65535&&(renderer.realtimeLightmapIndex&65535)==65535&&renderer.lightmapScaleOffset==new Vector4(1,1,0,0)&&renderer.realtimeLightmapScaleOffset==new Vector4(1,1,0,0),"original no-lightmap descriptors reach native renderer");
                    foreach(var material in renderer.sharedMaterials)if(material)Require(material.shader&&material.shader.isSupported&&material.shader.name!="Hidden/InternalErrorShader","original NPC effect shader compiles and binds");
                }
                Require(instance.GetComponentsInChildren<MyPlayableFollowBone>(true).All(f=>f.ScaleByGridSizeType==MyPlayableFollowBone.EScaleByGridSizeType.None),"original appearance effects do not request grid scaling");
                int peak=0;
                for(int replay=0;replay<2;replay++)
                {
                    expectedSound.AddRange(sequence.myPlayables.OrderBy(x=>x.myTime).Select(x=>x.myPlayable).OfType<MyPlayableSound>().Where(s=>s.SoundEvent.IsValid).Select(s=>s.SoundEvent.Id));
                    source.SetActive(Names[index].EndsWith("disappear"));sequence.Init(source,source);sequence.TryPlay();
                    int captureIndex=0;float elapsed=0;var captureTimes=new[]{.1f,.3f,.6f,1f};
                    while(elapsed<2.5f)
                    {
                        yield return null;float delta=UnityEngine.Time.deltaTime;elapsed+=delta;center.Update(delta);center.LateUpdate();
                        int live=instance.GetComponentsInChildren<ParticleSystem>(true).Sum(s=>s.particleCount);peak=Math.Max(peak,live);
                        if(replay==0&&captureIndex<captureTimes.Length&&elapsed>=captureTimes[captureIndex])
                        {
                            var surface=new RenderTexture(450,800,24,RenderTextureFormat.ARGB32);surface.Create();var previous=RenderTexture.active;camera.targetTexture=surface;camera.Render();RenderTexture.active=surface;var image=new Texture2D(450,800,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,450,800),0,0);image.Apply();RenderTexture.active=previous;camera.targetTexture=null;File.WriteAllBytes(Path.Combine(Output,"npc-appearance-"+index+"-"+captureIndex+".png"),image.EncodeToPNG());UnityEngine.Object.Destroy(image);surface.Release();UnityEngine.Object.Destroy(surface);
                            captureIndex++;
                        }
                    }
                    if(replay==0)Require(captureIndex==captureTimes.Length,"all original effect timeline frames captured: "+Names[index]);
                    Require(sequence.myStatus==MyPlayable.PlayableStatus.Stop,"original sequence naturally completes: "+Names[index]);Require(source.activeSelf==!Names[index].EndsWith("disappear"),"original actor visibility persists after appearance sequence: "+Names[index]);sequence.ResetData();yield return null;
                }
                Require(peak>0,"original native particles emit: "+Names[index]);Debug.Log("Original NPC appearance: "+Names[index]+"; peakParticles="+peak);center.RemoveAll();UnityEngine.Object.Destroy(instance);yield return null;
            }
            Require(sound.Events.Count>0&&sound.Events.SequenceEqual(expectedSound),"all original appearance sounds dispatch once in original sequence order");Debug.Log("Original NPC appearance sound IDs: "+string.Join(",",sound.Events));
            var following=VerifyFollowingAndFade();while(following.MoveNext())yield return following.Current;
            OfficialWwiseEvent.Host=oldSound;UnityEngine.Object.Destroy(source);UnityEngine.Object.Destroy(native);environment.Dispose();UnityEngine.Object.Destroy(worldRoot);yield return null;UnityEngine.Object.Destroy(cameraObject);
        }
        private static IEnumerator VerifyFollowingAndFade()
        {
            var source=new GameObject("Isolated original following source");source.transform.position=new Vector3(4,5,0);source.transform.localScale=new Vector3(2,3,1);var size=source.AddComponent<MySizeCfg>();size.myAutoGridSize=false;size.myManualGridSize=6;Require(size.GetGridSize()==6,"original manual grid size");size.myAutoGridSize=true;size.BodyRangeInfoCount=()=>null;Require(size.GetGridSize()==6,"original missing grid info fallback");size.BodyRangeInfoCount=()=>10;Require(size.GetGridSize()==3,"original rounded square root of body range count");
            var follower=new GameObject("Isolated followed transform");follower.transform.position=new Vector3(2,4,0);var probe=new GameObject("Original follow callback fixture");probe.SetActive(false);var playable=probe.AddComponent<MyPlayableFollowBone>();playable.myAttach=new AttachPosRotCfg{AttachTo=EAttachTo.Source,myOffset=new Vector3(1,2,0),myScale=.5f};playable.myDuration=-1;playable.myFollowDuration=-1;playable.myApplyComponent=follower.transform;playable.myTriggerList=new List<MyPlayableTriggerTimeCfg>();playable.myFadeoutTrigerList=new List<MyPlayableTrigger>();playable.myEndTrigerList=new List<MyPlayableTrigger>();var so=new SerializedObject(playable);so.FindProperty("mIsReset").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();var center=new MyPlayableCenter();playable.MyPlayableCenter=center;probe.SetActive(true);playable.Init(source,null);playable.TryPlay();
            Require(Vector3.Distance(follower.transform.position,new Vector3(6,11,0))<.0001f&&Vector3.Distance(follower.transform.localScale,new Vector3(1,1.5f,.5f))<.0001f,"original scaled transform following");source.transform.position=new Vector3(7,8,0);yield return null;center.Update(1f/60);center.LateUpdate();Require(Vector3.Distance(follower.transform.position,new Vector3(9,14,0))<.0001f,"original late-frame following observes source movement");playable.Stop(true);yield return null;center.Update(0);center.LateUpdate();Require(Vector3.Distance(follower.transform.position,new Vector3(2,4,0))<.0001f&&follower.transform.localScale==Vector3.one,"original stop resets captured local transform");center.RemoveAll();
            var effectObject=new GameObject("Original fade color fixture without substitute art");var sprite=effectObject.AddComponent<SpriteRenderer>();var originalColor=new Color(.8f,.6f,.4f,.75f);sprite.color=originalColor;var effect=effectObject.AddComponent<MyEffectFadeOut>();effect.myParticles=Array.Empty<ParticleSystem>();effect.myRenderers=new[]{sprite};effect.mTMPText=Array.Empty<TMPro.TMP_Text>();effect.FadeOut(.05f);yield return new WaitForSeconds(.12f);Require(sprite.color.maxColorComponent<.001f,"original fade curve reaches zero color");effect.Resume(playable);Require(sprite.color==originalColor,"original fade resume restores captured color");
            UnityEngine.Object.Destroy(effectObject);UnityEngine.Object.Destroy(probe);UnityEngine.Object.Destroy(follower);UnityEngine.Object.Destroy(source);yield return null;Debug.Log("Original dynamic follow, reset, body-size binding and asynchronous fade callbacks passed.");
        }
        private sealed class SoundProbe:IProfessionSoundHost
        {
            public readonly List<uint> Events=new List<uint>();
            public uint PostEvent(uint eventId,GameObject emitter){Events.Add(eventId);return (uint)Events.Count;}
        }
        private sealed class State:IMap13NativeWorldState
        {
            public Vector3 PlayerWorldPosition=>new Vector3(4.5f,74.5f,0);
            public int PlayerLevel=>90;public int PlayerSubRank=>12;public long PlayerCombatRating=>100000;
            public bool IsPublicRegion(int region)=>true;
            public int GetRegionProgress(int region)=>0;public int GetRegionGoddessId(int region)=>0;
            public void UnlockAllRegionPositions(int region){}
        }
        private sealed class Runner:MonoBehaviour{}
    }
}
