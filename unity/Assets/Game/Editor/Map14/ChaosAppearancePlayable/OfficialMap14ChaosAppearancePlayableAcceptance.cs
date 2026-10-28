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
using Zjcs.Official.StorySourceFade;
using Zjcs.Official.StorySounds;
using Zjcs.Official.ProfessionUI;
using System.Collections.Generic;
using Zjcs.Official.StoryPlayables;
using AnimationController=Zjcs.Official.StoryAnimation.AnimationController;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14ChaosAppearancePlayableAcceptance
    {
        private const string Key="Zjcs.Map14.NativeChaosAppearancePlayable";
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
            if(errors==0){SessionState.SetInt(Key+".result",0);Debug.Log("Map14 native chaos appearance playable Play Mode passed: original native chaos appearance prefab, original sequence, source-fade callbacks and bone attachments, native particles/materials, two replays, original sound dispatch and rendered frames. The actor source/color boundary is isolated; original actor model/skeleton animation, actual audio/DSP, formal story/map/save and Windows Player remain pending.");}EditorApplication.isPlaying=false;
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException("Native chaos appearance playable: "+message);}
        private static IEnumerator Steps()
        {
            var textureSource=JsonUtility.FromJson<NativeSource>(File.ReadAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT"),"assets/reference/official-map/map14-native-chaos-appearance-playable-source-manifest.json")));int gpuMips=0;
            foreach(var record in textureSource.textures.Where(t=>!t.reused))
            {
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(record.output.path.Substring(6));Require(texture,"original new chaos effect texture imports");
                foreach(var mip in record.mips)
                {
                    var request=AsyncGPUReadback.Request(texture,mip.level);while(!request.done)yield return null;Require(!request.hasError,"original chaos effect mip GPU readback");
                    using(var hash=System.Security.Cryptography.SHA256.Create())Require(BitConverter.ToString(hash.ComputeHash(request.GetData<byte>().ToArray())).Replace("-","").ToLowerInvariant()==mip.rgba_sha256,"all decoded chaos effect mip bytes reach GPU unchanged");gpuMips++;
                }
            }
            Debug.Log("Original chaos appearance decoded GPU mips verified: "+gpuMips);
            Application.runInBackground=true;typeof(OfficialMap14BubbleAcceptance).GetMethod("SetView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{450,800});yield return null;yield return null;
            var cameraObject=new GameObject("Original terrain validation camera");cameraObject.tag="MainCamera";var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=12;camera.transform.position=new Vector3(0,6,-100);camera.farClipPlane=1000;cameraObject.AddComponent<UniversalAdditionalCameraData>().SetRenderer(0);
            var environment=typeof(OfficialMap14SuiMoPerformanceAcceptance).GetMethod("CreateEnvironment",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);yield return null;yield return null;
            var source=new GameObject("Isolated actor source; actual original actor binding pending");source.transform.position=new Vector3(0,6,0);
            var bone=new GameObject("CommonEffectSlot");bone.transform.SetParent(source.transform,false);var agent=source.AddComponent<AttachAgent>();agent.GenerateBoneMap();
            var fade=Configure(source);var bridge=source.AddComponent<FadeBridge>();bridge.Supporter=new EntityFadeAnimationSupporter(fade);
            var catalog=Resources.Load<NativeMap13AssetCatalog>("Map14/Native/native-chaos-appearance-playable-catalog");
            var oldSound=OfficialWwiseEvent.Host;var sound=new SoundProbe();OfficialWwiseEvent.Host=sound;var expectedSound=new List<uint>();
            for(int index=0;index<Guids.Length;index++)
            {
                var prefab=catalog.Load<GameObject>(Guids[index]);var instance=UnityEngine.Object.Instantiate(prefab);var sequence=instance.GetComponent<MyPlayableSequence>();var particles=instance.GetComponentsInChildren<ParticleSystem>(true);var center=new MyPlayableCenter();foreach(var playable in instance.GetComponentsInChildren<MyPlayable>(true))playable.MyPlayableCenter=center;
                foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    Require((renderer.lightmapIndex&65535)==65535&&(renderer.realtimeLightmapIndex&65535)==65535&&renderer.lightmapScaleOffset==new Vector4(1,1,0,0)&&renderer.realtimeLightmapScaleOffset==new Vector4(1,1,0,0),"original no-lightmap descriptors reach native renderer");
                    foreach(var material in renderer.sharedMaterials)if(material)Require(material.shader&&material.shader.isSupported&&material.shader.name!="Hidden/InternalErrorShader","original chaos effect shader compiles and binds");
                }
                Require(instance.GetComponentsInChildren<MyPlayableFollowBone>(true).All(f=>f.ScaleByGridSizeType==MyPlayableFollowBone.EScaleByGridSizeType.None),"original appearance effects do not request grid scaling");
                int peak=0;
                for(int replay=0;replay<2;replay++)
                {
                    expectedSound.AddRange(sequence.myPlayables.OrderBy(x=>x.myTime).Select(x=>x.myPlayable).OfType<MyPlayableSound>().Where(s=>s.SoundEvent.IsValid).Select(s=>s.SoundEvent.Id));
                    bridge.Supporter.ResetFadeAnimation();sequence.Init(source,source);sequence.TryPlay();Require(fade.Alpha==0,"original sequence starts by immediately fading source out");
                    bool captured=false;float elapsed=0;
                    while(elapsed<2.5f)
                    {
                        yield return null;float delta=UnityEngine.Time.deltaTime;elapsed+=delta;center.Update(delta);center.LateUpdate();
                        int live=particles.Sum(s=>s.particleCount);peak=Math.Max(peak,live);
                        if(replay==0&&!captured&&live>0)
                        {
                            var surface=new RenderTexture(450,800,24,RenderTextureFormat.ARGB32);surface.Create();var previous=RenderTexture.active;camera.targetTexture=surface;camera.Render();RenderTexture.active=surface;var image=new Texture2D(450,800,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,450,800),0,0);image.Apply();RenderTexture.active=previous;camera.targetTexture=null;File.WriteAllBytes(Path.Combine(Output,"chaos-appearance-"+index+".png"),image.EncodeToPNG());UnityEngine.Object.Destroy(image);surface.Release();UnityEngine.Object.Destroy(surface);
                            captured=true;
                        }
                    }
                    if(replay==0)Require(captured,"original live particle frame captured: "+Names[index]);
                    Require(sequence.myStatus==MyPlayable.PlayableStatus.Stop,"original sequence naturally completes: "+Names[index]);Require(fade.IsIn&&fade.Alpha==1,"original source fades fully back in after .43 second child dispatch");sequence.ResetData();yield return null;
                }
                Require(peak>0,"original native particles emit: "+Names[index]);Debug.Log("Original chaos appearance: "+Names[index]+"; peakParticles="+peak);center.RemoveAll();UnityEngine.Object.Destroy(instance);yield return null;
            }
            Require(sound.Events.Count>0&&sound.Events.SequenceEqual(expectedSound),"all original appearance sounds dispatch once in original sequence order");Debug.Log("Original chaos appearance sound IDs: "+string.Join(",",sound.Events));
            var fading=VerifyFadeState();while(fading.MoveNext())yield return fading.Current;
            OfficialWwiseEvent.Host=oldSound;UnityEngine.Object.Destroy(source);var envType=environment.GetType();UnityEngine.Object.Destroy((GameObject)envType.GetField("Root").GetValue(environment));yield return null;((IDisposable)envType.GetField("Labels").GetValue(environment)).Dispose();UnityEngine.Object.Destroy((UnityEngine.Object)envType.GetField("Catalog").GetValue(environment));UnityEngine.Object.Destroy(cameraObject);
        }
        private static SpriteRendererList Configure(GameObject source)
        {
            return source.AddComponent<FadeListProbe>();
        }
        private static IEnumerator VerifyFadeState()
        {
            var source=new GameObject("Original source fade state verification, no replacement actor art");var fade=Configure(source);var sprite=source.AddComponent<SpriteRenderer>();sprite.color=new Color(.8f,.6f,.4f,.7f);fade.mySpriteList.Add(sprite);
            var active=new GameObject("Original completion attachment counter probe");active.transform.SetParent(source.transform,false);var agent=source.AddComponent<AttachAgent>();agent.GenerateBoneMap();fade.FadeInCompleteActiveGO.Add(active);
            var custom=source.AddComponent<CustomFadeProbe>();typeof(SpriteRendererList).GetField("mCustomFadeList",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(fade,new List<CustomRendererFade>{custom});
            var nativeObject=new GameObject("Independent native color boundary probe");nativeObject.transform.SetParent(source.transform,false);var native=nativeObject.AddComponent<NativeColorProbe>();fade.SkeletonRenderer=native;
            var events=new List<string>();fade.OnFadeInStart+=()=>events.Add("in-start");fade.OnFadeInCompleted+=()=>events.Add("in-complete");fade.OnFadeOutStart+=()=>events.Add("out-start");fade.OnFadeOutCompleted+=()=>events.Add("out-complete");
            fade.ImmediateFadeOut();Require(fade.Alpha==0&&!fade.IsIn&&!active.activeSelf&&custom.Alpha==0&&native.Skeleton.A==0&&!native.gameObject.activeSelf,"original immediate fade propagates alpha, completion-node hide and native color boundary");
            source.SetActive(true);fade.SkeletonRenderer=null;events.Clear();typeof(SpriteRendererList).GetMethod("ResetFadeState",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(fade,null);
            fade.ImmediateFadeOut();Require(events.SequenceEqual(new[]{"out-start","out-complete"}),"original immediate-out event order");Require(!fade.ImmediateFadeOut()&&events.Count==2,"original completed out is idempotent");events.Clear();
            fade.AheadInTime=.2f;fade.FadeIn(1f);Require(events.SequenceEqual(new[]{"in-start"})&&!active.activeSelf,"original fade-in start precedes completion and attachment show");
            yield return new WaitForSeconds(1.4f);Require(fade.IsIn&&fade.Alpha==1&&active.activeSelf&&events.SequenceEqual(new[]{"in-start","in-complete"}),"original ahead-in completion emits once and restores completion attachment");
            Require(sprite.color==new Color(.8f,.6f,.4f,1),"sprite RGB preserved while alpha is updated");events.Clear();fade.FadeOut(.06f,false);yield return new WaitForSeconds(.12f);Require(fade.Alpha==0&&events.Count==0,"original no-event fade-out and coroutine completion");
            var supporter=new EntityFadeAnimationSupporter(fade);supporter.FadeOut(0,false,2);supporter.FadeIn(0,false,3);Require(fade.Alpha==0,"original lower priority fade-in is rejected");supporter.FadeIn(0,false,1);Require(fade.Alpha==1,"original higher priority fade-in restores source");
            source.SetActive(false);fade.FadeIn(.06f);Require(fade.Alpha==1,"disabled fade-in queues duration without stepping");source.SetActive(true);yield return new WaitForSeconds(.12f);Require(fade.Alpha==1&&fade.IsIn,"original queued fade-in completes after enable");
            fade.FadeOut(.4f);yield return null;source.SetActive(false);Require(fade.Alpha==1&&fade.IsIn,"original disable completes fade then resets state");source.SetActive(true);
            UnityEngine.Object.Destroy(source);yield return null;Debug.Log("Original actor fading state, event order, attachment counters, queued enable, disable reset and priorities passed.");
        }
        private sealed class FadeBridge:MonoBehaviour,IEntityFadeAnimationBridge
        {
            public IEntityFadeAnimationSupporter Supporter;public IEntityFadeAnimationSupporter EntityFadeAnimationSupporter=>Supporter;
        }
        private sealed class FadeListProbe:SpriteRendererList
        {
            // Inspector-authored lists exist before OnValidate on real assets.
            // Initialize only that serialized fixture state before AddComponent
            // invokes the original validation; do not alter its state machine.
            public FadeListProbe()
            {
                mySpriteList=new List<SpriteRenderer>();FadeInCompleteActiveGO=new List<GameObject>();
                typeof(SpriteRendererList).GetField("mFordingSpriteList",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(this,new List<FordingSpriteRenderer>());
                typeof(SpriteRendererList).GetField("mCustomFadeList",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(this,new List<CustomRendererFade>());
            }
        }
        private sealed class CustomFadeProbe:CustomRendererFade{public float Alpha;public override void SetAlpha(float alpha){Alpha=alpha;}}
        private sealed class ColorProbe:NativeActorFadeColor
        {
            private float r=1,g=1,b=1,a=1;public override float R{get=>r;set=>r=value;}public override float G{get=>g;set=>g=value;}public override float B{get=>b;set=>b=value;}public override float A{get=>a;set=>a=value;}
        }
        private sealed class NativeColorProbe:NativeActorFadeRenderer{private readonly ColorProbe color=new ColorProbe();public override NativeActorFadeColor Skeleton=>color;}
        private sealed class SoundProbe:IProfessionSoundHost
        {
            public readonly List<uint> Events=new List<uint>();
            public uint PostEvent(uint eventId,GameObject emitter){Events.Add(eventId);return (uint)Events.Count;}
        }
        private sealed class Runner:MonoBehaviour{}
    }
}
