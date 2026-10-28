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
using Zjcs.Offline.World.NativeMap13StoryPlayables;
using Zjcs.Official.StoryPlayableRequests;
using Zjcs.Presentation.World;
using Cysharp.Threading.Tasks;
using Zjcs.Official.StorySounds;
using Zjcs.Official.ProfessionUI;
using System.Collections.Generic;
using Zjcs.Official.StoryPlayables;
using AnimationController=Zjcs.Official.StoryAnimation.AnimationController;

namespace Zjcs.Editor.Map13StoryPlayableRequests
{
    public static class OfficialMap13StoryPlayableRequestsAcceptance
    {
        private const string Key="Zjcs.Map13.OriginalStoryPlayableRequests";
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
                NativeMap13StoryPlayableAssets.Load();
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
            if(errors==0){SessionState.SetInt(Key+".result",0);Debug.Log("Map13 original 29 request bodies / 55 indexed source inputs passed native pooled playback, original stop waits, flags, stage-stop order, visibility lifecycle and cleanup on real Map13 ground. Isolated actor parameter boundary; original NPC art, full formal stories and Windows Player remain pending.");}EditorApplication.isPlaying=false;
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException("Native NPC appearance playable: "+message);}
        private static IEnumerator Steps()
        {
            Application.runInBackground=true;
            var cameraObject=new GameObject("Original Map13 request camera");cameraObject.tag="MainCamera";var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=12;camera.transform.position=new Vector3(4.5f,74.5f,-500);camera.farClipPlane=1000;cameraObject.AddComponent<UniversalAdditionalCameraData>().SetRenderer(0);
            var root=new GameObject("Original Map13 request ground");var world=OfficialMap13NativeWorldView.Create(root.transform,camera,new State());
            float deadline=Time.realtimeSinceStartup+120;while(!world.IsReady&&Time.realtimeSinceStartup<deadline)yield return null;Require(world.IsReady,"original ground readiness");
            var assets=NativeMap13StoryPlayableAssets.Load();Require(assets.Count==12,"twelve native assets");
            var data=JsonUtility.FromJson<Map10RuntimeStoryData>(Resources.Load<TextAsset>("Map13/stories").text);
            var sound=new SoundProbe();var previousSound=OfficialWwiseEvent.Host;OfficialWwiseEvent.Host=sound;
            int requests=0,inputs=0,waits=0,stageStops=0;bool captured=false;
            foreach(var graph in data.stories.Concat(data.subgraphs))
            foreach(var source in graph.nodes.Where(n=>n.type=="PlayPlayableNode"))
            {
                var request=assets.CreateRequest(source.sourceJson);var scopeObject=new GameObject("Map13 original request pool");var scope=scopeObject.AddComponent<NativeMap13StoryPlayableScope>();scope.Initialize();
                var proxy=new StoryHandleProxy();proxy.BindMyPlayableManager(new Zjcs.Official.StoryPlayableManager.ActivePlayableManagerInfo{MyPlayableManager=scope.Manager});request.StoryHandleProxy=proxy;
                var actors=new Dictionary<string,StoryPlayableEntityBinding>();var supports=new Dictionary<StoryPlayableEntityBinding,StageProbe>();var controllers=new List<AnimatorController>();
                foreach(var port in source.ports.Where(p=>(p.name.StartsWith("SourceEntitys ")||p.name.StartsWith("TargetEntitys "))&&p.connections.Length>0))
                {
                    Require(port.connections.Length==1,"original indexed connection");
                    var actor=new GameObject("Isolated original actor port "+port.name);actor.transform.position=new Vector3(1.5f+actors.Count*2,74.5f,0);
                    var ui=new GameObject("UI");ui.transform.SetParent(actor.transform,false);var agent=actor.AddComponent<AttachAgent>();agent.myBoneNameList.Add("UI");agent.myBoneList.Add(ui.transform);agent.Init();
                    var animator=actor.AddComponent<Animator>();var controller=new AnimatorController();controller.AddLayer("Base Layer");var state=controller.layers[0].stateMachine.AddState("Parameter boundary");controller.layers[0].stateMachine.defaultState=state;
                    foreach(var guid in request.PlayablesProxy.Select(p=>p.AssetGuid).Distinct())
                    {
                        using(var lease=assets.LoadAssetByGuid<GameObject>(guid))foreach(var leaf in lease.Result.GetComponentsInChildren<MyPlayableAnimator>(true))
                        {
                            if(!string.IsNullOrEmpty(leaf.myBoolParam)&&!controller.parameters.Any(p=>p.name==leaf.myBoolParam))controller.AddParameter(leaf.myBoolParam,AnimatorControllerParameterType.Bool);
                            if(!string.IsNullOrEmpty(leaf.myFloatParam)&&!controller.parameters.Any(p=>p.name==leaf.myFloatParam))controller.AddParameter(leaf.myFloatParam,AnimatorControllerParameterType.Float);
                            if(!string.IsNullOrEmpty(leaf.IntParam)&&!controller.parameters.Any(p=>p.name==leaf.IntParam))controller.AddParameter(leaf.IntParam,AnimatorControllerParameterType.Int);
                            if(!string.IsNullOrEmpty(leaf.myTriggerParam)&&!controller.parameters.Any(p=>p.name==leaf.myTriggerParam))controller.AddParameter(leaf.myTriggerParam,AnimatorControllerParameterType.Trigger);
                        }
                    }
                    animator.runtimeAnimatorController=controller;animator.Update(0);controllers.Add(controller);var animation=actor.AddComponent<AnimationController>();animation.myAnimator=animator;animation.NotCreateGraph=true;animation.Init();
                    var binding=actor.AddComponent<StoryPlayableEntityBinding>();binding.ClientVisibilitySupporter=new VisibilityProbe();actors.Add(port.name,binding);supports.Add(binding,new StageProbe());inputs++;
                }
                bool started=false,ended=false,cleaned=false;
                request.StartContext=()=>started=true;request.EndContext=()=>ended=true;request.CleanContext=()=>cleaned=true;
                request.InputValue=port=>actors.TryGetValue(port,out var actor)?actor:null;
                request.InputValues=name=>{var port=source.ports.Single(p=>p.name==name);Require(port.connections.Length==0,"original grouped ports are unconnected");return null;};
                request.GetWorldStageSupport=actor=>actor?supports[actor]:null;
                request.OnStart();Require(started&&actors.Values.All(a=>((VisibilityProbe)a.ClientVisibilitySupporter).Values.Count==0),"unconnected grouped port does not force visibility");
                int expectedStops=request.IsStopEntitesPlayableSupportBeforeNodeRun?actors.Count:0;Require(supports.Values.Sum(s=>s.Stops)==expectedStops,"original stage stop before dispatch");stageStops+=expectedStops;
                float began=Time.time;var pending=request.RunAsync(System.Threading.CancellationToken.None);
                if(request.IsWaitPlayableStop)waits++;
                while(pending.Status==UniTaskStatus.Pending)
                {
                    Require(Time.time-began<10,"original native callback finishes before safety timeout");
                    yield return null;
                    if(!captured&&actors.Count==4&&Time.time-began>=.3f){Capture(camera);captured=true;}
                }
                Require(pending.GetAwaiter().GetResult()==StoryRunNodeResultCode.OK,"original request result");
                if(request.IsWaitPlayableStop&&request.PlayableDura>0)Require(Time.time-began>=request.PlayableDura-.025f,"original explicit duration remains active until callback");
                request.OnEnd();Require(ended&&actors.Values.All(a=>((VisibilityProbe)a.ClientVisibilitySupporter).Values.Count==0),"unconnected grouped port leaves visibility unchanged at node end");
                request.CleanWhileStoryAllEnd();Require(cleaned,"original graph-end cleanup");
                Debug.Log("Map13 original request "+graph.key+":"+source.id+"; indexed="+actors.Count+"; wait="+request.IsWaitPlayableStop+"; elapsed="+(Time.time-began));
                scope.Dispose();UnityEngine.Object.Destroy(scopeObject);foreach(var actor in actors.Values)UnityEngine.Object.Destroy(actor.gameObject);foreach(var controller in controllers)UnityEngine.Object.Destroy(controller);requests++;yield return null;
            }
            Require(requests==29&&inputs==55&&stageStops==2&&captured,"complete original configured request/source inventory");
            File.WriteAllText(Path.Combine(Output,"story-playable-request-events.json"),JsonUtility.ToJson(new Results{requests=requests,indexedInputs=inputs,waits=waits,stageStops=stageStops,soundEvents=sound.Events.ToArray()},true));
            OfficialWwiseEvent.Host=previousSound;world.Dispose();UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(cameraObject);yield return null;
        }
        private static void Capture(Camera camera)
        {
            var surface=new RenderTexture(450,800,24,RenderTextureFormat.ARGB32);surface.Create();var previous=RenderTexture.active;camera.targetTexture=surface;camera.Render();RenderTexture.active=surface;var picture=new Texture2D(450,800,TextureFormat.RGBA32,false);picture.ReadPixels(new Rect(0,0,450,800),0,0);picture.Apply();File.WriteAllBytes(Path.Combine(Output,"story-playable-request-multi.png"),picture.EncodeToPNG());RenderTexture.active=previous;camera.targetTexture=null;surface.Release();UnityEngine.Object.Destroy(surface);UnityEngine.Object.Destroy(picture);
        }
        [Serializable] private sealed class Results{public int requests,indexedInputs,waits,stageStops;public uint[] soundEvents;}
        private sealed class VisibilityProbe:IStoryPlayableEntityVisibility{public readonly List<bool> Values=new List<bool>();public void SetForceVisible(bool visible)=>Values.Add(visible);}
        private sealed class StageProbe:IStoryWorldStagePlayableSupport{public int Stops,Starts;public void Stop()=>Stops++;public void Start()=>Starts++;}
        private sealed class SoundProbe:IProfessionSoundHost{public readonly List<uint> Events=new List<uint>();public uint PostEvent(uint id,GameObject source){Events.Add(id);return (uint)Events.Count;}}
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
