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
using UnityEngine.UI;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.World.NativeMap13;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryAnimatorTimeline;
using Zjcs.Official.StoryBubbles;
using Zjcs.Official.StoryBubbleTimeline;

namespace Zjcs.Editor.Map14
{
    // Native asset/graph integration fixture. Binding objects below are isolated
    // probes; they do not certify original character, camera or effect prefabs.
    public static class OfficialMap14EntityTimelineAcceptance
    {
        private const string Key="Zjcs.Map14.NativeEntityTimeline";
        private const string Folder="Assets/Official/World/Map/14/NativeEntityTimelines/Timeline/";
        private const string CatalogPath="Assets/Game/Resources/Map14/Native/native-entity-timelines-catalog.asset";
        private static readonly string[] Names={"GriffinShakeTimeline 4","SuiMo_StoryShow02Timeline","SuiMo_StoryShow05","XianHaiYunZhou"};
        private static readonly string[] Identities={"CAB-6c22157d398ea5508505e1faae752ea9:7246930292191139513","CAB-d4a275944e5ce6c6a8c2dace2c139fa0:5140903139780379000","CAB-8d1ec42ccd8ba61414b8626e0ce853cc:-3322158653568776960","CAB-40e45028ffef078012c1f7224d8fa63f:-4861394986059470401"};
        private static bool started;private static int errors;
        private static string Output=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR")??throw new InvalidOperationException("Explicit validation directory required.");
        public static void Run()
        {
            try
            {
                if(!Path.GetFullPath(Output).Replace('\\','/').Contains("/output/validation/"))throw new InvalidOperationException("Use an isolated validation workspace.");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var catalog=AssetDatabase.LoadAssetAtPath<NativeMap13AssetCatalog>(CatalogPath);
                if(!catalog){catalog=ScriptableObject.CreateInstance<NativeMap13AssetCatalog>();AssetDatabase.CreateAsset(catalog,CatalogPath);}
                catalog.Bindings=Names.Select((name,i)=>new NativeMap13AssetCatalog.Binding{OfficialPath=Identities[i],Asset=AssetDatabase.LoadAssetAtPath<TimelineAsset>(Folder+name+".playable")}).ToArray();
                Require(catalog.Bindings.All(b=>b.Asset),"all four original Timeline assets import");
                foreach(var path in AssetDatabase.FindAssets("",new[]{"Assets/Official/World/Map/14/NativeEntityTimelines"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>!AssetDatabase.IsValidFolder(p)))
                    Require(AssetDatabase.LoadAllAssetsAtPath(path).All(a=>a),"all native dependency subassets and original scripts import: "+path);
                EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
                Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor",true).GetMethod("Validate").Invoke(null,null);
                SessionState.SetString(Key+".pipeline",AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline));
                SessionState.SetString(Key+".quality",AssetDatabase.GetAssetPath(QualitySettings.renderPipeline));
                SessionState.SetInt(Key+".color",(int)PlayerSettings.colorSpace);
                var shared=typeof(OfficialMap14SuiMoPerformanceAcceptance).GetMethod("SharedAsset",BindingFlags.Static|BindingFlags.NonPublic).MakeGenericMethod(typeof(NativeMap13AssetCatalog));
                var pipeline=(NativeMap13AssetCatalog)shared.Invoke(null,new object[]{true});
                PlayerSettings.colorSpace=ColorSpace.Linear;GraphicsSettings.defaultRenderPipeline=pipeline.Load<UniversalRenderPipelineAsset>("globalgamemanagers.assets:22209");QualitySettings.renderPipeline=null;
                SessionState.SetBool(Key,true);SessionState.SetInt(Key+".result",1);SessionState.SetFloat(Key+".deadline",(float)EditorApplication.timeSinceStartup+240);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Hook();EditorApplication.isPlaying=true;
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        [InitializeOnLoadMethod] private static void Hook()
        {
            if(!SessionState.GetBool(Key,false))return;started=false;errors=0;
            EditorApplication.update-=Tick;EditorApplication.update+=Tick;EditorApplication.playModeStateChanged-=Mode;EditorApplication.playModeStateChanged+=Mode;
        }
        private static void Mode(PlayModeStateChange mode)
        {
            if(mode!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key,false))return;
            Application.logMessageReceived-=Observe;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=Mode;
            GraphicsSettings.defaultRenderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key+".pipeline",""));QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(SessionState.GetString(Key+".quality",""));
            PlayerSettings.colorSpace=(ColorSpace)SessionState.GetInt(Key+".color",0);EditorApplication.Exit(SessionState.GetInt(Key+".result",1));
        }
        private static void Observe(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private static void Tick()
        {
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".deadline",0)){Debug.LogError("Native entity Timeline verification timed out.");EditorApplication.isPlaying=false;return;}
            if(started||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;started=true;Application.logMessageReceived+=Observe;
            new GameObject("Native entity Timeline verification").AddComponent<Runner>().StartCoroutine(Verify());
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
            if(errors==0){SessionState.SetInt(Key+".result",0);Debug.Log("Map14 native entity Timeline Play Mode passed: four original assets import and create native graphs; original SuiMo02 graph dispatches all six source events and four native bubbles at source times. Original actors/camera/effect bindings, sound playback, fifth skeleton Timeline, formal story/map/save and Windows Player remain pending.");}
            EditorApplication.isPlaying=false;
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException("Native entity Timeline: "+message);}
        private sealed class SoundProbe:IProfessionSoundHost
        {
            public readonly List<uint> Events=new List<uint>();
            public readonly List<double> Times=new List<double>();public Func<double> Clock;
            public uint PostEvent(uint id,GameObject emitter){Require(emitter,"original event owner is assigned");Events.Add(id);Times.Add(Clock());Debug.Log("Native entity Timeline event "+id+" at "+Times.Last());return (uint)Events.Count;}
        }
        private static IEnumerable<TrackAsset> Tracks(TimelineAsset timeline)=>timeline.GetRootTracks().SelectMany(Descendants);
        private static IEnumerable<TrackAsset> Descendants(TrackAsset track){yield return track;foreach(var child in track.GetChildTracks().SelectMany(Descendants))yield return child;}
        private static IEnumerator Steps()
        {
            Application.runInBackground=true;
            typeof(OfficialMap14BubbleAcceptance).GetMethod("SetView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{450,800});yield return null;yield return null;
            var cameraObject=new GameObject("Native graph fixture camera");cameraObject.tag="MainCamera";var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=12;camera.transform.position=new Vector3(0,6,-100);camera.farClipPlane=1000;
            cameraObject.AddComponent<UniversalAdditionalCameraData>().SetRenderer(0);var brain=cameraObject.AddComponent<CinemachineBrain>();
            var environment=typeof(OfficialMap14SuiMoPerformanceAcceptance).GetMethod("CreateEnvironment",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
            var canvas=new GameObject("Native original bubble UI host",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=.5f;
            var data=(Dictionary<string,object>)OfficialEquipmentJson.Parse(File.ReadAllText("Assets/Official/World/Map/14/Source/entity-timelines.json"),true);
            var hostType=typeof(OfficialMap14BubbleTimelineAcceptance).GetNestedType("Host",BindingFlags.NonPublic);var host=Activator.CreateInstance(hostType,new object[]{data,canvas.transform});
            var popup=(IBubblePopupServices)host;var oldBubbles=BubbleServices.Current;BubbleServices.Current=popup;
            var entities=(List<BubbleEntityBinding>)hostType.GetField("Entities").GetValue(host);
            foreach(int id in new[]{8451,8452,8453}){var entity=new GameObject("Isolated binding probe "+id).AddComponent<BubbleEntityBinding>();entity.ClassId=id;entity.transform.position=new Vector3((id-8452)*3,6,0);entities.Add(entity);}
            var complete=hostType.GetMethod("CompletePending");var created=hostType.GetField("Created");var closed=hostType.GetField("Closed");var active=hostType.GetField("Active");
            var oldSound=OfficialWwiseEvent.Host;var sound=new SoundProbe();OfficialWwiseEvent.Host=sound;
            var catalog=Resources.Load<NativeMap13AssetCatalog>("Map14/Native/native-entity-timelines-catalog");
            foreach(int i in new[]{0,2,3,1})
            {
                var timeline=catalog.Load<TimelineAsset>(Identities[i]);var owner=new GameObject("Original native graph: "+Names[i]);var director=owner.AddComponent<PlayableDirector>();director.playOnAwake=false;director.playableAsset=timeline;director.timeUpdateMode=DirectorUpdateMode.Manual;
                var controller=new AnimatorController();controller.AddLayer("Base Layer");controller.layers[0].stateMachine.defaultState=controller.layers[0].stateMachine.AddState("Probe");
                foreach(var asset in Tracks(timeline).SelectMany(t=>t.GetClips()).Select(c=>c.asset).OfType<TlAnimatorParamClip>())
                {var t=asset.template;if(!string.IsNullOrEmpty(t.TriggerKey)&&!controller.parameters.Any(p=>p.name==t.TriggerKey))controller.AddParameter(t.TriggerKey,AnimatorControllerParameterType.Trigger);}
                var animator=owner.AddComponent<Animator>();animator.runtimeAnimatorController=controller;
                var virtualCamera=new GameObject("Isolated camera binding probe").AddComponent<CinemachineVirtualCamera>();virtualCamera.transform.position=camera.transform.position;virtualCamera.m_Lens.OrthographicSize=12;
                foreach(var track in Tracks(timeline))
                {
                    if(track is CinemachineTrack)director.SetGenericBinding(track,brain);
                    if(track is AnimationTrack)director.SetGenericBinding(track,animator);
                    if(track is TlAnimatorParamTrack)director.SetGenericBinding(track,owner);
                    if(track is EntityTrack)
                    {var first=(EntityTalkAsset)track.GetClips().First().asset;var info=popup.GetDialogInfosById(first.Template.DialogId).First(r=>r.Order==first.Template.Order);director.SetGenericBinding(track,entities.Single(e=>e.ClassId==info.EntityClassId));}
                    foreach(var clip in track.GetClips())if(clip.asset is CinemachineShot shot)director.SetReferenceValue(shot.VirtualCamera.exposedName,virtualCamera);
                }
                sound.Clock=()=>director.time;director.Play();director.Evaluate();Require(director.playableGraph.IsValid(),"native graph creates: "+Names[i]);
                if(i==1)
                {
                    int screenshots=0;
                    for(double time=0;time<timeline.duration;time+=1.0/60)
                    {
                        director.time=time;director.Evaluate();complete.Invoke(host,null);
                        if(time<10){yield return null;}
                        var window=(GameObject)active.GetValue(host);
                        if(window && (int)created.GetValue(host)>screenshots)
                        {
                            yield return new WaitForSeconds(.12f);yield return new WaitForEndOfFrame();
                            var text=window.GetComponentInChildren<TMP_Text>();Require(text && !string.IsNullOrEmpty(text.text),"native bubble localizes");
                            var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(Output,"entity-timeline-bubble-"+screenshots+".png"),image.EncodeToPNG());UnityEngine.Object.Destroy(image);screenshots++;
                        }
                    }
                    Require((int)created.GetValue(host)==4,"four original bubbles execute from the imported native Timeline");
                    Require(sound.Events.SequenceEqual(new uint[]{2366889146,2366889145,1061687478,1061687477,3438568527,3438568524}),"all six original source events execute once in order, including split chant");
                    var eventTimes=new double[]{0,2.6166666666666667,3.0166666666666666,4.117061882116843,5.516666666666667,7.733333333333333};
                    for(int n=0;n<eventTimes.Length;n++)Require(Math.Abs(sound.Times[n]-eventTimes[n])<=1.0/60+.000001,"native event begins at original clip start within one manual sample");
                    Require(screenshots==4,"all four original bubble frames captured");
                }
                director.Stop();UnityEngine.Object.Destroy(owner);UnityEngine.Object.Destroy(virtualCamera.gameObject);UnityEngine.Object.Destroy(controller);yield return null;
            }
            Require((int)closed.GetValue(host)==4,"all original bubbles close after native graph completion");
            OfficialWwiseEvent.Host=oldSound;BubbleServices.Current=oldBubbles;
            var envType=environment.GetType();UnityEngine.Object.Destroy((GameObject)envType.GetField("Root").GetValue(environment));yield return null;
            ((IDisposable)envType.GetField("Labels").GetValue(environment)).Dispose();UnityEngine.Object.Destroy((UnityEngine.Object)envType.GetField("Catalog").GetValue(environment));
            foreach(var entity in entities)UnityEngine.Object.Destroy(entity.gameObject);UnityEngine.Object.Destroy(canvas);UnityEngine.Object.Destroy(cameraObject);
        }
        private sealed class Runner:MonoBehaviour{}
    }
}
