using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Equipment;
using UnityEditor.Animations;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Zjcs.Official.StoryAnimatorTimeline;
using Zjcs.Official.StoryAnimation;
using AnimationController = Zjcs.Official.StoryAnimation.AnimationController;

namespace Zjcs.Editor.Map14
{
    public static class OfficialMap14AnimatorTimelineAcceptance
    {
        private const string Key = "Zjcs.Map14.OriginalAnimatorTimeline";
        private static bool started;
        private static int errors;
        public static void Run()
        {
            try
            {
                Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            }
            catch(Exception error) {Debug.LogException(error);EditorApplication.Exit(1);return;}
            SessionState.SetBool(Key, true); SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook(); EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Hook()
        {
            if (!SessionState.GetBool(Key, false)) return;
            started = false; errors = 0;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode; EditorApplication.playModeStateChanged += Mode;
        }
        private static void Mode(PlayModeStateChange mode)
        {
            if (mode != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            Application.logMessageReceived -= Observe; SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick; EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Observe(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            { Debug.LogError("Map14 original bubble verification timed out."); EditorApplication.isPlaying = false; return; }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true; Application.logMessageReceived += Observe;
            new GameObject("Original bubble verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(Steps());
            while (stack.Count > 0)
            {
                object next;
                try
                {
                    if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                    next = stack.Peek().Current;
                    if (next is IEnumerator child) { stack.Push(child); continue; }
                }
                catch (Exception e) { Debug.LogException(e); EditorApplication.isPlaying = false; yield break; }
                yield return next;
            }
            if (errors == 0)
            {
                SessionState.SetInt(Key + ".result", 0);
                Debug.Log("Map14 original Animator Timeline Play Mode passed: six native class bindings, parameter snapshot/restore, controller priority, template isolation, original entity triggers, current-player-only lookup, optional clip override and unbound player Timeline affirmative trigger. Original controller/model rendering and full entity Timeline/map/save/release remain pending.");
            }
            EditorApplication.isPlaying = false;
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidDataException("Original bubble: " + message); }
        private static AnimatorController MakeController()
        {
            var native=new AnimatorController{name="Isolated original Timeline parameter probe"};native.AddLayer("Base Layer");
            var machine=native.layers[0].stateMachine;var idle=machine.AddState("Idle");machine.defaultState=idle;
            native.AddParameter("flag",AnimatorControllerParameterType.Bool);
            native.AddParameter("index",AnimatorControllerParameterType.Int);
            native.AddParameter("value",AnimatorControllerParameterType.Float);
            foreach(string key in new[]{"land_02","float_02","rise","rise_02","affirmative"})
            {
                native.AddParameter(key,AnimatorControllerParameterType.Trigger);
                var state=machine.AddState(key);var transition=idle.AddTransition(state);
                transition.hasExitTime=false;transition.duration=0;transition.AddCondition(AnimatorConditionMode.If,0,key);
            }
            return native;
        }
        private static void Values(Animator a,bool b,int i,float f,string message)
        {Require(a.GetBool("flag")==b && a.GetInteger("index")==i && Mathf.Abs(a.GetFloat("value")-f)<.00001f,message);}
        private static IEnumerator Steps()
        {
            foreach(var type in new[]{typeof(TlAnimatorParamBehaviour),typeof(TlAnimatorParamClip),typeof(TlAnimatorParamTrack),
                typeof(TlPlayerAnimatorParamBehaviour),typeof(TlPlayerAnimatorParamClip),typeof(TlPlayerAnimatorParamTrack)})
            {
                var script=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Game/Runtime/Official/StoryAnimatorTimeline/"+type.Name+".cs");
                Require(script && script.GetClass()==type,"original MonoScript binding "+type.Name);
            }
            var native=MakeController();var root=new GameObject("Animator Timeline direct binding");
            var animator=root.AddComponent<Animator>();animator.runtimeAnimatorController=native;animator.Update(0);
            animator.SetInteger("index",1);animator.SetFloat("value",.25f);
            var graph=PlayableGraph.Create("Original Timeline snapshot probe");
            var template=new TlAnimatorParamBehaviour{BoolKey="flag",BoolValue=true,IntKey="index",IntValue=7,FloatKey="value",FloatValue=.75f,TriggerKey="rise",EndResume=true};
            var playable=ScriptPlayable<TlAnimatorParamBehaviour>.Create(graph,template);var behaviour=playable.GetBehaviour();
            behaviour.ProcessFrame(playable,default,root);Values(animator,true,7,.75f,"first frame applies original parameters");
            Require(animator.GetCurrentAnimatorStateInfo(0).IsName("rise"),"trigger calls native Update(0)");
            animator.SetInteger("index",9);behaviour.ProcessFrame(playable,default,root);
            Require(animator.GetInteger("index")==9,"later frames do not repeat the write");
            behaviour.OnBehaviourPause(playable,default);Values(animator,false,1,.25f,"pause restores the captured parameters");
            behaviour.ProcessFrame(playable,default,root);Values(animator,true,7,.75f,"pause resets first-frame guard for re-entry");
            behaviour.OnBehaviourPause(playable,default);
            Require(template.IntValue==7 && template.BoolValue,"ScriptPlayable clone keeps the original public template");
            var keep=ScriptPlayable<TlAnimatorParamBehaviour>.Create(graph,new TlAnimatorParamBehaviour{BoolKey="",IntKey="index",IntValue=4,FloatKey="",TriggerKey="",EndResume=false});
            keep.GetBehaviour().ProcessFrame(keep,default,root);keep.GetBehaviour().OnBehaviourPause(keep,default);
            Require(animator.GetInteger("index")==4,"EndResume false preserves applied parameters");
            var preferred=new GameObject("Preferred AnimationController animator").AddComponent<Animator>();preferred.runtimeAnimatorController=native;preferred.Update(0);
            var controller=root.AddComponent<AnimationController>();controller.NotCreateGraph=true;controller.myAnimator=preferred;
            var priority=ScriptPlayable<TlAnimatorParamBehaviour>.Create(graph,new TlAnimatorParamBehaviour{BoolKey="",IntKey="index",IntValue=11,FloatKey="",TriggerKey=""});
            priority.GetBehaviour().ProcessFrame(priority,default,root);
            Require(preferred.GetInteger("index")==11 && animator.GetInteger("index")==4,"original AnimationController takes priority over direct Animator");
            priority.GetBehaviour().OnBehaviourPause(priority,default);
            UnityEngine.Object.Destroy(controller);yield return null;
            graph.Destroy();
            // The following graph uses all four parameter templates from the original entity Timelines.
            var data=(Dictionary<string,object>)OfficialEquipmentJson.Parse(File.ReadAllText("Assets/Official/World/Map/14/Source/entity-timelines.json"),true);
            var configs=((List<object>)data["timelines"]).Cast<Dictionary<string,object>>()
                .SelectMany(t=>((List<object>)t["objects"]).Cast<Dictionary<string,object>>())
                .Where(o=>(string)o["type"]=="TlAnimatorParamClip").Select(o=>(Dictionary<string,object>)((Dictionary<string,object>)o["fields"])["template"]).ToArray();
            Require(configs.Length==4,"four original native clip configurations");
            foreach(var config in configs)
            {
                animator.Play("Idle",0,0);animator.Update(0);
                var timeline=ScriptableObject.CreateInstance<TimelineAsset>();var track=timeline.CreateTrack<TlAnimatorParamTrack>();
                var clip=track.CreateClip<TlAnimatorParamClip>();clip.start=.1;clip.duration=.5;
                var asset=(TlAnimatorParamClip)clip.asset;var value=asset.template;
                value.EndResume=Convert.ToBoolean(config["EndResume"]);value.BoolKey=(string)config["BoolKey"];value.BoolValue=Convert.ToBoolean(config["BoolValue"]);
                value.IntKey=(string)config["IntKey"];value.IntValue=Convert.ToInt32(config["IntValue"]);
                value.FloatKey=(string)config["FloatKey"];value.FloatValue=Convert.ToSingle(config["FloatValue"]);value.TriggerKey=(string)config["TriggerKey"];
                var director=root.AddComponent<PlayableDirector>();director.timeUpdateMode=DirectorUpdateMode.Manual;director.playableAsset=timeline;
                director.SetGenericBinding(track,root);director.Play();director.time=.2;director.Evaluate();yield return null;
                Require(animator.GetCurrentAnimatorStateInfo(0).IsName(value.TriggerKey),"original native Timeline trigger "+value.TriggerKey);
                director.time=.7;director.Evaluate();director.Stop();UnityEngine.Object.Destroy(director);UnityEngine.Object.Destroy(timeline);yield return null;
            }
            yield return PlayerSteps(native,root,animator);
            UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(preferred.gameObject);UnityEngine.Object.Destroy(native);yield return null;
        }
        private sealed class PlayerServices:IPlayerAnimatorTimelineServices
        {public GameObject PlayerEntity{get;set;}}
        private sealed class PlayerLookup:TlPlayerAnimatorParamBehaviour
        {public Animator Resolve(object value){return GetAnimator(value);}}
        private static IEnumerator PlayerSteps(AnimatorController native,GameObject decoy,Animator decoyAnimator)
        {
            var old=PlayerAnimatorTimelineServices.Current;var services=new PlayerServices();
            var player=new GameObject("Original current-player lookup probe");
            var sourceClip=new AnimationClip{name="Original player override probe"};
            sourceClip.SetCurve("",typeof(Transform),"m_LocalPosition.x",AnimationCurve.Linear(0,0,1,1));
            var replacement=new AnimationClip{name="Replacement player override probe"};
            replacement.SetCurve("",typeof(Transform),"m_LocalPosition.x",AnimationCurve.Linear(0,0,1,2));
            var idle=native.layers[0].stateMachine.states.First(s=>s.state.name=="Idle").state;
            idle.motion=sourceClip;
            var animator=player.AddComponent<Animator>();animator.runtimeAnimatorController=native;animator.Update(0);
            var controller=player.AddComponent<AnimationController>();controller.myAnimator=animator;
            controller.NotCreateGraph=true;controller.CreateOverrideController=true;controller.Init();
            try
            {
                PlayerAnimatorTimelineServices.Current=services;
                var lookup=new PlayerLookup{OriginClipName=sourceClip.name,OverrideClip=replacement};
                Require(!lookup.Resolve(decoy),"missing current player does not fall back to playerData");
                services.PlayerEntity=decoy;
                Require(!lookup.Resolve(player),"current player without AnimationController does not fall back to direct Animator");
                services.PlayerEntity=player;
                Require(lookup.Resolve(decoy)==animator,"playerData is ignored in favour of current player controller");
                var overrides=(AnimatorOverrideController)controller.RuntimeAnimatorController;
                Require(overrides[sourceClip.name]==replacement,"original optional clip override is applied before returning Animator");
                lookup.OverrideClip=null;lookup.Resolve(decoy);
                Require(overrides[sourceClip.name]==replacement,"null override leaves current clip unchanged");
                overrides[sourceClip.name]=sourceClip;lookup.OverrideClip=replacement;lookup.OriginClipName="";lookup.Resolve(decoy);
                Require(overrides[sourceClip.name]==sourceClip,"empty original name skips override");
                var data=(Dictionary<string,object>)OfficialEquipmentJson.Parse(File.ReadAllText("Assets/Official/World/Map/14/Source/native-story-playable-components.json"),true);
                var rows=((List<object>)data["components"]).Cast<Dictionary<string,object>>();
                var configs=rows.Where(o=>(string)o["type"]=="TlPlayerAnimatorParamClip").ToArray();
                Require(configs.Length==1 && (string)configs[0]["cab"]=="CAB-5d4c2c5d1454464f2497d6b3e36e10f9"
                    && (string)configs[0]["path_id"]=="-3612460611401424567","original SuiMo player clip identity");
                var config=(Dictionary<string,object>)((Dictionary<string,object>)configs[0]["fields"])["Template"];
                var timeline=ScriptableObject.CreateInstance<TimelineAsset>();var track=timeline.CreateTrack<TlPlayerAnimatorParamTrack>();
                var clip=track.CreateClip<TlPlayerAnimatorParamClip>();clip.start=.1;clip.duration=.5;
                var template=((TlPlayerAnimatorParamClip)clip.asset).Template;
                template.EndResume=Convert.ToBoolean(config["EndResume"]);template.BoolKey=(string)config["BoolKey"];
                template.BoolValue=Convert.ToBoolean(config["BoolValue"]);template.IntKey=(string)config["IntKey"];
                template.IntValue=Convert.ToInt32(config["IntValue"]);template.FloatKey=(string)config["FloatKey"];
                template.FloatValue=Convert.ToSingle(config["FloatValue"]);template.TriggerKey=(string)config["TriggerKey"];
                template.OriginClipName=(string)config["OriginClipName"];
                Require(Convert.ToInt64(((Dictionary<string,object>)config["OverrideClip"])["m_PathID"])==0,"original player clip has no override asset");
                animator.Play("Idle",0,0);animator.Update(0);decoyAnimator.Play("Idle",0,0);decoyAnimator.Update(0);
                var director=player.AddComponent<PlayableDirector>();director.timeUpdateMode=DirectorUpdateMode.Manual;director.playableAsset=timeline;
                Require(director.GetGenericBinding(track)==null,"player track has no scene binding");
                director.Play();director.time=.2;director.Evaluate();yield return null;
                Require(animator.GetCurrentAnimatorStateInfo(0).IsName("affirmative"),"unbound player Timeline triggers current player affirmative");
                Require(decoyAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle"),"player Timeline leaves unrelated Animator unchanged");
                director.Stop();UnityEngine.Object.Destroy(timeline);
            }
            finally
            {
                PlayerAnimatorTimelineServices.Current=old;UnityEngine.Object.Destroy(player);
                UnityEngine.Object.Destroy(sourceClip);UnityEngine.Object.Destroy(replacement);idle.motion=null;
            }
            yield return null;
        }
        private sealed class Runner:MonoBehaviour{}
    }
}
