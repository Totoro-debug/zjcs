using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.WorldUI;
using Zjcs.Editor.Combat;
using Zjcs.Presentation.Inventory;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static class OfficialMap14HandInAcceptance
    {
        private const string Key = "Zjcs.Map14.HandInAcceptance";
        private static bool started;
        private static int errors;
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_SIMULATED", "1");
            OfficialMap11UiTools.ImportMap14HandIn();
            if (!Environment.GetCommandLineArgs().Contains("-map14HandInQuick")) ProjectTools.Validate();
            SessionState.SetBool(Key,true);
            SessionState.SetInt(Key+".result",1);
            SessionState.SetFloat(Key+".deadline",(float)EditorApplication.timeSinceStartup+150);
            BattleGameViewInput.Focus();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying=true;
        }
        [InitializeOnLoadMethod] private static void Restore() { if(SessionState.GetBool(Key,false)) Hook(); }
        private static void Hook()
        {
            started=false; errors=0;
            EditorApplication.update-=Tick; EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged-=Mode; EditorApplication.playModeStateChanged+=Mode;
        }
        private static void Mode(PlayModeStateChange mode)
        {
            if(mode!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key,false)) return;
            Application.logMessageReceived-=Observe;
            SessionState.SetBool(Key,false);
            EditorApplication.update-=Tick; EditorApplication.playModeStateChanged-=Mode;
            EditorApplication.Exit(SessionState.GetInt(Key+".result",1));
        }
        private static void Observe(string message,string trace,LogType type)
        { if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) errors++; }
        private static void Tick()
        {
            if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Key+".deadline",0))
            { Debug.LogError("Map14 hand-in acceptance timed out.");EditorApplication.isPlaying=false;return; }
            if(started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started=true;Application.logMessageReceived+=Observe;
            new GameObject("Map14HandInAcceptance").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static IEnumerator Verify()
        {
            var stack=new Stack<IEnumerator>(); stack.Push(Steps());
            while(true)
            {
                object next;
                try { if(!stack.Peek().MoveNext()){stack.Pop(); if(stack.Count==0) break; continue;} next=stack.Peek().Current; if(next is IEnumerator child){stack.Push(child);continue;} }
                catch(Exception e) {Debug.LogException(e);EditorApplication.isPlaying=false;yield break;}
                yield return next;
            }
            if(errors==0)
            {
                SessionState.SetInt(Key+".result",0);
                Debug.Log("Map14 hand-in Play Mode passed: original 58 nodes, UI input through simulated mouse, official disabled-gray shader, original UI sounds, disabled confirmation, cancel, interrupt, atomic configured submission, replay/limit, rollback and profile cold reload. Mainline host is not covered by this isolated fixture.");
            }
            EditorApplication.isPlaying=false;
        }
        private static IEnumerator Steps()
        {
            Application.runInBackground = true;
            var events = new GameObject("HandInEventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            var input=events.AddComponent<SimulatedMouse>();
            events.GetComponent<StandaloneInputModule>().inputOverride=input;
            EventSystem.current.sendNavigationEvents=false;
            var canvas=new GameObject("HandInCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=.5f;
            var camera=new GameObject("HandInCamera",typeof(Camera)).GetComponent<Camera>();
            camera.gameObject.AddComponent<AudioListener>();
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            string saveRoot=Path.GetFullPath("../.tools/map14-handin-acceptance-"+Guid.NewGuid().ToString("N"));
            SessionState.SetString(Key+".saveRoot",saveRoot);
            var slots=new ProfileSlotCatalog(saveRoot);string slot=slots.CreateSlot();
            var session=OfflineMainlineProgressSession.OpenCharacter(slots,slot);
            var catalog=JsonUtility.FromJson<OfficialMap11UiData>(Resources.Load<TextAsset>("Map14/UI/HandIn/catalog").text);
            string Text(string key) => catalog.texts.Single(t=>t.key==key).value;
            var assets=OfficialInventoryAssetCatalog.LoadDefault();
            var prefab=Resources.Load<GameObject>("Map14/UI/HandIn/DialogHandInItemDialog");
            Require(prefab!=null,"Original hand-in prefab loads");
            int confirms=0,cancels=0,interrupts=0,normalSounds=0,backSounds=0; float maxSoundDuration=0;
            var requirements=new Dictionary<int,long>{{2208,1}};
            OfficialMap14HandInDialogView Open()
            {
                var view=UnityEngine.Object.Instantiate(prefab,canvas.transform,false).GetComponent<OfficialMap14HandInDialogView>();
                view.Initialize(assets,2208,3,1,session.InventoryCount(2208),Text,()=>{
                    confirms++;
                    Require(session.SubmitHandInItem(4,1,requirements,100,"map14:test:hand-in").IsApplied,"Apply original configured hand-in");
                },()=>cancels++,()=>interrupts++);
                void ObserveSound(AudioClip clip, GameObject emitter)
                {
                    Map14HandInUiAudio.Post(clip,emitter);
                    maxSoundDuration=Mathf.Max(maxSoundDuration,clip.length);
                    if (clip.name.StartsWith("Main_Cursor_Normal", StringComparison.Ordinal)) normalSounds++;
                    else if (clip.name.StartsWith("Main_Cursor_Back", StringComparison.Ordinal)) backSounds++;
                    else throw new InvalidDataException("Unexpected original hand-in audio: " + clip.name);
                    Require(UnityEngine.Object.FindObjectsOfType<AudioSource>().Any(source => source.clip == clip && source.isPlaying),
                        "Resolved original Wwise event plays through native audio");
                }
                foreach (var sound in view.GetComponentsInChildren<WorldButtonSound>(true)) sound.Post=ObserveSound;
                foreach (var sound in view.GetComponentsInChildren<WorldPopupWindowSound>(true)) sound.Post=ObserveSound;
                return view;
            }
            yield return null;
            var missing=Open();yield return new WaitForSeconds(.4f);
            Require(!missing.mComfirmBtn.interactable,"Missing item disables confirmation");
            Require(missing.transform.childCount==2,"Original hand-in top-level hierarchy remains intact");
            yield return Capture("before-click");
            yield return Click(input,missing.mComfirmBtn);
            Require(normalSounds==0 && backSounds==0,"Disabled confirmation does not post button or close sound");
            Require(confirms==0 && session.HandInItemCount(4)==0,"Disabled confirmation does not mutate state");
            yield return Capture("missing");
            yield return Click(input,missing.mCancelBtn);yield return new WaitForSeconds(.4f);
            Require(cancels==1 && confirms==0 && interrupts==0 && session.HandInItemCount(4)==0,"Cancel routes once without a transaction");
            Require(normalSounds==1 && backSounds==1,"Cancel posts original button and popup close events once");
            var externallyClosed=Open();yield return new WaitForSeconds(.4f);
            externallyClosed.GetComponent<WorldPopupWindow>().Hide(null);yield return new WaitForSeconds(.4f);
            Require(interrupts==1 && session.HandInItemCount(4)==0,"Popup manager close routes interrupt at original OnHide without consuming inventory");
            var interrupted=Open();yield return new WaitForSeconds(.4f);
            UnityEngine.Object.Destroy(interrupted.gameObject);yield return null;
            Require(interrupts==2 && session.HandInItemCount(4)==0,"External close routes interrupt without consuming inventory");
            Require(session.ApplyInventoryTransaction("seed",save=>{
                Require(OfflineInventoryOperations.Grant(save.Player,2208,1,100).IsApplied,"Seed isolated configured item");
            }).IsApplied,"Persist isolated inventory seed");
            var ready=Open();yield return new WaitForSeconds(.4f);
            Require(ready.mComfirmBtn.interactable,"Owned item enables confirmation");
            var left=ready.transform.Find("BgDialog1/Content/ScrollRect/ViewPort/Content/ItemIconDefault(Clone)/Content/Progress/BgCountText/LeftTxt").GetComponent<TMP_Text>();
            Require(left.text=="1","Original requirement progress displays owned count");
            yield return Capture("ready");
            yield return Click(input,ready.mComfirmBtn);yield return new WaitForSeconds(.4f);
            Require(confirms==1 && cancels==1 && interrupts==2 && session.InventoryCount(2208)==0 && session.HandInItemCount(4)==1,"Confirm consumes one item and increments one time");
            Require(session.SubmitHandInItem(4,1,requirements,100,"map14:test:hand-in").Status==ProgressionResultStatus.Unchanged,"Duplicate receipt is idempotent");
            Require(session.SubmitHandInItem(4,1,requirements,100,"map14:test:limit").Status==ProgressionResultStatus.InvalidState,"Configured one-time limit enforced");
            var reloaded=OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(saveRoot),slot);
            Require(reloaded.InventoryCount(2208)==0 && reloaded.HandInItemCount(4)==1,"Cold save restores atomic item consumption and count");
            Require(normalSounds==2 && backSounds==3,"Confirmation posts original button and popup close events once");
            yield return new WaitForSecondsRealtime(maxSoundDuration+.1f);
            Require(UnityEngine.Object.FindObjectsOfType<Map14HandInUiAudio>().Length==0,"Completed original audio releases its temporary sources");
            string rollbackSlot=slots.CreateSlot();var rollback=OfflineMainlineProgressSession.OpenCharacter(slots,rollbackSlot);
            rollback.ApplyInventoryTransaction("seed-rollback",save=>OfflineInventoryOperations.Grant(save.Player,2208,1,100));
            Require(rollback.SubmitHandInItem(4,1,new Dictionary<int,long>{{2208,1},{2209,1}},100,"rollback").Status==ProgressionResultStatus.InvalidState
                && rollback.InventoryCount(2208)==1 && rollback.HandInItemCount(4)==0,"A later missing fixture ingredient rolls back earlier consumption");
            yield return Capture("closed");
            Require(errors==0,"No rendering, lifecycle or input errors");
            Debug.Log("Map14 hand-in fixture save root: "+saveRoot);
        }
        private static IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            string dir=Path.GetFullPath("../.tools/map14-handin-screens");Directory.CreateDirectory(dir);
            var image=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(dir,name+".png"),image.EncodeToPNG());UnityEngine.Object.Destroy(image);
        }
        private static IEnumerator Click(SimulatedMouse input,Button target)
        {
            Canvas.ForceUpdateCanvases();
            var rect=target.GetComponent<RectTransform>();
            input.point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            Require(input.point.x>0 && input.point.x<Screen.width && input.point.y>0 && input.point.y<Screen.height,"Original button stays inside the portrait viewport");
            var pointer=new PointerEventData(EventSystem.current){position=input.point};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Require(hits.Count>0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==target.gameObject,"Simulated pointer reaches original button without occlusion: "+target.name);
            yield return null;
            input.down=true;input.held=true;yield return null;
            input.down=false;input.up=true;input.held=false;yield return null;
            input.up=false;yield return null;
        }
        private static void Require(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
        private sealed class Runner:MonoBehaviour { }
        public sealed class SimulatedMouse:BaseInput
        {
            public Vector2 point;public bool down,held,up;
            public override bool mousePresent=>true;
            public override Vector2 mousePosition=>point;
            public override Vector2 mouseScrollDelta=>Vector2.zero;
            public override bool GetMouseButtonDown(int button)=>button==0&&down;
            public override bool GetMouseButtonUp(int button)=>button==0&&up;
            public override bool GetMouseButton(int button)=>button==0&&held;
            public override float GetAxisRaw(string axisName)=>0;
            public override bool GetButtonDown(string buttonName)=>false;
        }
    }
}
