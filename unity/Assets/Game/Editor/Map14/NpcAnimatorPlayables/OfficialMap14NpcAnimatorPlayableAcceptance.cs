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
using Zjcs.Official.StoryPlayables;
using AnimationController=Zjcs.Official.StoryAnimation.AnimationController;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14NpcAnimatorPlayableAcceptance
    {
        private const string Key="Zjcs.Map14.NativeNpcAnimatorPlayables";
        private const string CatalogPath="Assets/Game/Resources/Map14/Native/native-npc-animator-playables-catalog.asset";
        private const string Folder="Assets/Official/World/Map/14/NativeNpcAnimatorPlayables/Prefabs/Playables/DisplayPackage/Story/NPCAnimator/";
        private static readonly string[] Names={"playable_npc_idle","playable_npc_run","playable_npc_float_02"};
        private static readonly string[] Guids={"bb73792b166b5cb46bdcff12ba4582ee","136cd8f04e65aa0429355f8bf5df97d9","4ee644fd004372245be51d12a3d7cf51"};
        private static bool started;private static int errors;
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
                Require(catalog.Bindings.All(b=>b.Asset),"all three original NPC Animator playables import");
                foreach(var binding in catalog.Bindings)
                {
                    var prefab=(GameObject)binding.Asset;Require(prefab.GetComponentsInChildren<Transform>(true).Length==2,"original two-node hierarchy");
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
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".deadline",0)){Debug.LogError("Native NPC Animator playable verification timed out.");EditorApplication.isPlaying=false;return;}
            if(started||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;started=true;Application.logMessageReceived+=Observe;new GameObject("Native NPC Animator playable verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            var steps=Steps();while(true){object value;try{if(!steps.MoveNext())break;value=steps.Current;}catch(Exception e){Debug.LogException(e);EditorApplication.isPlaying=false;yield break;}yield return value;}
            if(errors==0){SessionState.SetInt(Key+".result",0);Debug.Log("Map14 native NPC Animator playable Play Mode passed: three original prefabs, explicit original GUID catalog, original source/controller binding, bool restore, finite/infinite duration, sequence stop and pool replay. Parameter controllers are isolated fixtures; original NPC models/animation rendering, full story/map/save and Windows Player remain pending.");}EditorApplication.isPlaying=false;
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException("Native NPC Animator playable: "+message);}
        private static IEnumerator Steps()
        {
            Application.runInBackground=true;typeof(OfficialMap14BubbleAcceptance).GetMethod("SetView",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{450,800});yield return null;yield return null;
            var cameraObject=new GameObject("Original terrain validation camera");cameraObject.tag="MainCamera";var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=12;camera.transform.position=new Vector3(0,6,-100);camera.farClipPlane=1000;cameraObject.AddComponent<UniversalAdditionalCameraData>().SetRenderer(0);
            var environment=typeof(OfficialMap14SuiMoPerformanceAcceptance).GetMethod("CreateEnvironment",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);yield return null;yield return null;
            var source=new GameObject("Isolated native parameter controller, original NPC appearance pending");var animator=source.AddComponent<Animator>();var native=new AnimatorController();native.AddLayer("Base Layer");native.AddParameter("idle_01",AnimatorControllerParameterType.Bool);native.AddParameter("run",AnimatorControllerParameterType.Bool);native.AddParameter("float_02",AnimatorControllerParameterType.Trigger);
            var machine=native.layers[0].stateMachine;var idle=machine.AddState("Probe");machine.defaultState=idle;var floating=machine.AddState("float_02");var transition=idle.AddTransition(floating);transition.duration=0;transition.hasExitTime=false;transition.AddCondition(AnimatorConditionMode.If,0,"float_02");animator.runtimeAnimatorController=native;
            var controller=source.AddComponent<AnimationController>();controller.myAnimator=animator;controller.NotCreateGraph=true;controller.Init();animator.Update(0);
            var target=new GameObject("Unused target binding probe");var catalog=Resources.Load<NativeMap13AssetCatalog>("Map14/Native/native-npc-animator-playables-catalog");
            foreach(int index in new[]{0,1,2})
            {
                var prefab=catalog.Load<GameObject>(Guids[index]);var instance=UnityEngine.Object.Instantiate(prefab);var sequence=instance.GetComponent<MyPlayableSequence>();var leaf=instance.GetComponentInChildren<MyPlayableAnimator>(true);var center=new MyPlayableCenter();
                Require(sequence&&leaf&&sequence.myPlayables.Count==1&&sequence.myPlayables[0].myPlayable==leaf&&sequence.myPlayables[0].myTime==0,"original sequence points to its original child at zero");
                Require(leaf.myDuration==(index==2?.5f:-1f)&&leaf.myEndResumeParam,"original finite/infinite duration and restoration flags");
                foreach(var playable in instance.GetComponentsInChildren<MyPlayable>(true))playable.MyPlayableCenter=center;
                for(int replay=0;replay<2;replay++)
                {
                    animator.Play("Probe",0,0);animator.Update(0);sequence.Init(source,target);sequence.TryPlay();Require(leaf.myApplyComponent==controller,"original ApplyBySource picks the source AnimationController");
                    if(index<2)Require(animator.GetBool(index==0?"idle_01":"run"),"original bool value applies from prefab");
                    for(int frame=0;frame<45;frame++){yield return null;center.Update(1f/60);center.LateUpdate();animator.Update(1f/60);}
                    if(index==2)Require(sequence.myStatus==MyPlayable.PlayableStatus.Stop&&leaf.myStatus==MyPlayable.PlayableStatus.Stop&&animator.GetCurrentAnimatorStateInfo(0).IsName("float_02"),"original half-second child and sequence complete after triggering native Animator");
                    else{Require(sequence.myStatus!=MyPlayable.PlayableStatus.Stop&&leaf.myStatus!=MyPlayable.PlayableStatus.Stop,"original infinite playable persists until stopped");sequence.Stop(true);Require(!animator.GetBool(index==0?"idle_01":"run"),"original stop restores source bool");}
                    yield return null;center.Update(0);center.LateUpdate();sequence.ResetData();
                }
                center.RemoveAll();UnityEngine.Object.Destroy(instance);yield return null;
            }
            yield return new WaitForEndOfFrame();var image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(Output,"npc-animator-original-terrain.png"),image.EncodeToPNG());UnityEngine.Object.Destroy(image);
            UnityEngine.Object.Destroy(source);UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(native);var envType=environment.GetType();UnityEngine.Object.Destroy((GameObject)envType.GetField("Root").GetValue(environment));yield return null;((IDisposable)envType.GetField("Labels").GetValue(environment)).Dispose();UnityEngine.Object.Destroy((UnityEngine.Object)envType.GetField("Catalog").GetValue(environment));UnityEngine.Object.Destroy(cameraObject);
        }
        private sealed class Runner:MonoBehaviour{}
    }
}
