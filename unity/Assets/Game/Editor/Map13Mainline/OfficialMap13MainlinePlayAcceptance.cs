using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Editor.Combat;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Presentation.World;
using Zjcs.Presentation.World.NativeMap13;
using Zjcs.Official.World;

namespace Zjcs.Editor.Map13Mainline
{
    [InitializeOnLoad]
    public static class OfficialMap13MainlinePlayAcceptance
    {
        private const string Key="Map13.FormalNativeEntry";
        private static bool started,failed;
        private static string Output=>Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR")??throw new InvalidDataException("Explicit validation directory required.");
        static OfficialMap13MainlinePlayAcceptance()
        {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Key,false))return;
                SessionState.SetBool(Key,false);PlayerSettings.colorSpace=(ColorSpace)SessionState.GetInt(Key+".color",0);
                EditorApplication.Exit(SessionState.GetInt(Key+".result",1));
            };
        }
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION","1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_SIMULATED","1");
            SessionState.SetInt(Key+".color",(int)PlayerSettings.colorSpace);PlayerSettings.colorSpace=ColorSpace.Linear;
            ShaderUtil.allowAsyncCompilation=false;
            SessionState.SetBool(Key,true);SessionState.SetInt(Key+".result",1);
            SessionState.SetFloat(Key+".deadline",(float)EditorApplication.timeSinceStartup+900);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;
        }
        public static void RunRules()
        {
            try
            {
                var data=JsonUtility.FromJson<Map10RuntimeStoryData>(Resources.Load<TextAsset>("Map13/stories").text);
                Require(data.puzzleRules.Length==2690,"Original puzzle placement closure is incomplete.");
                Require(data.puzzleRules.Count(r=>r.TerminalPuzzleType==6)==6,"Original step-grid terminal count differs.");
                Require(data.puzzleRules.Count(r=>r.TerminalPuzzleType==2)==5,"Original road-switch terminal count differs.");
                Require(data.puzzleRules.Single(r=>r.PlacementId==101).InitialState==1,"Original entry state was lost.");
                var lockRule=data.puzzleRules.Single(r=>r.PlacementId==10901);
                Require(lockRule.LockConditions.Single().PlacementId==10903&&lockRule.LockConditions.Single().State==1
                    &&lockRule.ConditionEntityInactiveInUnlock,"Original numeric dictionary lock was lost.");
                var session=new OfflineMapPuzzleSession(data.puzzleRules,Array.Empty<MapPuzzleEntityState>());
                session.RefreshLocks();
                var snapshot=session.Save();
                var restored=new OfflineMapPuzzleSession(data.puzzleRules,snapshot);
                Require(JsonUtility.ToJson(new SavedPuzzles{states=snapshot})==JsonUtility.ToJson(new SavedPuzzles{states=restored.Save()}),
                    "All original puzzle states failed their local cold restore.");
                File.WriteAllText(Path.Combine(Output,"map13-formal-puzzle-rules.json"),JsonUtility.ToJson(new SavedPuzzles{states=snapshot},true));
                Debug.Log("Map13 original puzzle rules passed: 2690 placements, 6 step-grid / 5 road-switch terminals, original numeric locks and full local state restore.");
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        [Serializable] private sealed class SavedPuzzles{public MapPuzzleEntityState[] states;}
        private static void Tick()
        {
            if(!SessionState.GetBool(Key,false))return;
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".deadline",0)){Debug.LogError("Map13 formal entry verification timed out.");EditorApplication.Exit(1);return;}
            if(EditorApplication.isPlaying)EditorApplication.QueuePlayerLoopUpdate();
            if(started||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            started=true;new GameObject("Map13 formal entry verification").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            Application.logMessageReceived+=Observe;var stack=new Stack<IEnumerator>();stack.Push(Steps());
            while(stack.Count>0)
            {
                object next;
                try{var current=stack.Peek();if(!current.MoveNext()){stack.Pop();continue;}next=current.Current;if(next is IEnumerator nested&&!(next is CustomYieldInstruction)){stack.Push(nested);continue;}}
                catch(Exception e){Debug.LogException(e);Application.logMessageReceived-=Observe;EditorApplication.isPlaying=false;yield break;}
                yield return next;
            }
            if(!failed)SessionState.SetInt(Key+".result",0);
            Application.logMessageReceived-=Observe;EditorApplication.isPlaying=false;
        }
        private static void Observe(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)failed=true;}
        private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).GetValue(value);
        private static object Call(object value,string name,params object[] arguments)=>value.GetType().GetMethod(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Invoke(value,arguments);
        private static IEnumerator Steps()
        {
            Application.runInBackground=true;Application.targetFrameRate=60;BattleGameViewInput.Focus();yield return new WaitForSecondsRealtime(.5f);
            if(EventSystem.current==null)new GameObject("Map13 simulated pointer",typeof(EventSystem));
            var canvas=new GameObject("Map13 formal HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            string saves=Path.Combine(Output,"formal-map13-profile-"+Guid.NewGuid().ToString("N"));
            var slots=new ProfileSlotCatalog(saves);string slot=slots.CreateSlot();
            var save=new OfflineMainlineSave{CreatedAtUnixSeconds=DateTimeOffset.UtcNow.ToUnixTimeSeconds()};
            save.Player.CharacterCreated=true;save.Player.CharacterName="泽之国验证";save.Player.InitialWeaponType=201;
            save.Player.Level=90;save.Player.Experience=OfficialCharacterAssetCatalog.LoadDefault().Load().CumulativeLevelExperience[90];
            save.Player.Rank=save.Player.SubRank=save.Player.SeasonRank=12;save.Player.Wardrobe.Profession=1;
            save.Player.UnlockedMapClassIds.AddRange(new[]{11,12,13});
            save.Tasks.ClaimedMainlineTaskIds.AddRange(OfficialMainlineTaskCatalog.LoadDefault().OrderedTasks.Where(t=>t.classId<=1040).Select(t=>t.classId));
            File.WriteAllText(slots.GetProgressPath(slot),JsonUtility.ToJson(save,true));
            var progression=OfflineMainlineProgressSession.OpenCharacter(slots,slot);
            new OfflineCharacterProgressionService(progression,OfficialCharacterAssetCatalog.LoadDefault().Load()).InitializeWardrobe("map13-formal-entry-wardrobe");
            var runtime=Open(slot,saves,canvas.transform);
            var world=Field<OfficialMap13NativeWorldView>(runtime,"nativeMap13World");
            float deadline=Time.realtimeSinceStartup+120;while(!world.IsReady&&Time.realtimeSinceStartup<deadline)yield return null;
            Require(world.IsReady,"Formal Map13 original partitions did not become ready.");
            var navigation=Field<object>(runtime,"navigation");var initial=Field<MapGridPoint>(navigation,"currentGrid");
            Require(initial.Equals(new MapGridPoint(115,220)),"Map13 original entry 101 was not used.");
            Require(Field<object>(runtime,"map13GridVisibility")!=null,"Formal original grid component is not bound.");
            yield return new WaitForSecondsRealtime(2);Canvas.ForceUpdateCanvases();
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"map13-formal-entry.png"));yield return new WaitForSecondsRealtime(.5f);
            var camera=Field<Camera>(runtime,"worldCamera");var session=Field<OfficialWorldMapSession>(runtime,"session");
            MapGridPoint target=initial;Vector2 screen=default;bool found=false;
            foreach(var offset in new[]{new MapGridPoint(1,0),new MapGridPoint(-1,0),new MapGridPoint(0,1),new MapGridPoint(0,-1),new MapGridPoint(2,0),new MapGridPoint(0,2)})
            {
                var candidate=new MapGridPoint(initial.X+offset.X,initial.Y+offset.Y);
                var path=session.FindPath(new OfficialMapPathRequest{Start=initial,End=candidate,PreciseTarget=true,BodyOffsets=new[]{new MapGridPoint(0,0)}});
                if(path==null||path.Count==0)continue;
                var position=(Vector3)Call(navigation,"GridToWorld",candidate);screen=camera.WorldToScreenPoint(position);
                var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=screen},hits);
                if(hits.Count!=0||Physics2D.OverlapPoint(position)!=null)continue;
                target=candidate;found=true;break;
            }
            Require(found,"No unobstructed nearby map pointer target is available.");
            bool sent=false;runtime.PointerPressProvider=()=>{if(sent)return null;sent=true;return screen;};
            deadline=Time.realtimeSinceStartup+15;while(!Field<MapGridPoint>(navigation,"currentGrid").Equals(target)&&Time.realtimeSinceStartup<deadline)yield return null;
            runtime.PointerPressProvider=()=>null;Require(sent&&Field<MapGridPoint>(navigation,"currentGrid").Equals(target),"Simulated map pointer did not move the formal player.");
            string mapSave=Path.Combine(slots.GetSlotDirectory(slot),"map-13-save.json");
            var before=JsonUtility.FromJson<VisibilitySave>(File.ReadAllText(mapSave));Require(!string.IsNullOrEmpty(before.map13GridVisibility),"Formal map save omitted original compressed visibility.");
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"map13-formal-moved.png"));yield return new WaitForSecondsRealtime(.5f);
            Call(runtime,"CloseMap");yield return null;yield return null;
            runtime=Open(slot,saves,canvas.transform);yield return new WaitForSecondsRealtime(3);
            Require(Field<MapGridPoint>(Field<object>(runtime,"navigation"),"currentGrid").Equals(target),"Formal map position did not survive reopen.");
            var after=JsonUtility.FromJson<VisibilitySave>(File.ReadAllText(mapSave));Require(before.map13GridVisibility==after.map13GridVisibility,"Formal visibility bytes changed on cold reopen.");
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"map13-formal-restored.png"));yield return new WaitForSecondsRealtime(.5f);
            Call(runtime,"CloseMap");yield return null;yield return null;
            Require(!failed,"Formal Map13 entry emitted runtime errors.");
            File.WriteAllText(Path.Combine(Output,"map13-formal-entry.json"),JsonUtility.ToJson(new Proof{editor=Application.unityVersion,backend=SystemInfo.graphicsDeviceType.ToString(),play_mode=true,simulated_pointer=true,save_reopen=true,entry_x=initial.X,entry_y=initial.Y,moved_x=target.X,moved_y=target.Y},true));
            Debug.Log("Map13 formal native entry passed: original environment/HUD, simulated map pointer, local grid visibility/position save and cold reopen.");
        }
        private static OfficialMainlineMapRuntime Open(string slot,string saves,Transform ui)
        {
            var model=CharacterCreationNativeModelFactory.CreateWorldModel(slot,saves,out string name);
            var runtime=new GameObject("Map13 formal runtime").AddComponent<OfficialMainlineMapRuntime>();
            runtime.MapMusicVolume=()=>0;runtime.MapVoiceVolume=()=>0;runtime.Begin(13,slot,saves,ui,model,name,null);return runtime;
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
        [Serializable]private sealed class VisibilitySave{public string map13GridVisibility;}
        [Serializable]private sealed class Proof{public string editor,backend;public bool play_mode,simulated_pointer,save_reopen;public int entry_x,entry_y,moved_x,moved_y;}
        private sealed class Runner:MonoBehaviour{}
    }
}
