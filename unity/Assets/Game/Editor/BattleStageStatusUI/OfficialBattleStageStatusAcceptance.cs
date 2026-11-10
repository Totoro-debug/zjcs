using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;
using Zjcs.Official.BattleStageStatusUI;
using Zjcs.Official.WorldUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Zjcs.Editor.BattleStageStatus
{
    public static class OfficialBattleStageStatusAcceptance
    {
        private const string Key = "Zjcs.BattleStageStatusUI.Acceptance";
        private static bool started;
        private static int errors;
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_CREATION_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            RunIsolated();
        }
        public static void RunIsolated()
        {
            Environment.SetEnvironmentVariable("ZJCS_CREATION_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Type.GetType("Zjcs.Editor.Home.HomeAcceptanceLauncher, Zjcs.Editor", true)
                .GetMethod("ConfigurePortraitGameView", System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, new object[] { 450, 975 });
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 300);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Hook()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.playModeStateChanged += Mode;
        }
        private static void Mode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            Application.logMessageReceived -= Observe;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            {
                Debug.LogError("Native stage-status dialog timed out.");
                EditorApplication.isPlaying = false;
                return;
            }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            Application.logMessageReceived += Observe;
            new GameObject("Native stage-status verification runner").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static void Observe(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        }
        private static IEnumerator Verify()
        {
            var stack = new Stack<IEnumerator>();
            Action<string> capture = name => ScreenCapture.CaptureScreenshot(
                Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name + ".png"));
            stack.Push(Steps(capture));
            bool passed = false;
            try
            {
                while (stack.Count > 0)
                {
                    var current = stack.Peek();
                    if (!current.MoveNext())
                    {
                        (stack.Pop() as IDisposable)?.Dispose();
                        continue;
                    }
                    if (current.Current is IEnumerator nested) stack.Push(nested);
                    else yield return current.Current;
                }
                passed = errors == 0;
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                SessionState.SetInt(Key + ".result", passed ? 0 : 1);
                EditorApplication.isPlaying = false;
            }
        }
  private static void Require(bool ok,string reason) { if(!ok) throw new InvalidOperationException(reason); }
  private static IEnumerator Click(EventSystem events, Vector2 pos, Transform expected)
  {
   var data=new PointerEventData(events){position=pos,button=PointerEventData.InputButton.Left};
   var hits=new List<RaycastResult>();events.RaycastAll(data,hits);
   Require(hits.Count>0 && (hits[0].gameObject.transform==expected || hits[0].gameObject.transform.IsChildOf(expected)),"Original UI pointer is obscured: "+expected.name+" hit "+(hits.Count>0?hits[0].gameObject.name:"none"));
   var go=hits[0].gameObject;data.pointerCurrentRaycast=hits[0];data.pointerPressRaycast=hits[0];
   var pressed=ExecuteEvents.ExecuteHierarchy(go,data,ExecuteEvents.pointerDownHandler);
   if(pressed==null) pressed=ExecuteEvents.GetEventHandler<IPointerClickHandler>(go);
   data.pointerPress=pressed;data.rawPointerPress=go;
   yield return null;
   ExecuteEvents.Execute(pressed,data,ExecuteEvents.pointerUpHandler);
   ExecuteEvents.Execute(pressed,data,ExecuteEvents.pointerClickHandler);
   yield return null;
  }
  public static IEnumerator Steps(Action<string> capture)
  {
   var root=new GameObject("Stage status HUD verification");
   try
   {
    var cameraObject=new GameObject("Camera",typeof(Camera));cameraObject.transform.SetParent(root.transform,false);
    var camera=cameraObject.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=6;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.15f,.24f,.28f);
    var canvasObject=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvasObject.transform.SetParent(root.transform,false);
    canvasObject.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
    var scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
    var eventObject=new GameObject("EventSystem",typeof(EventSystem));eventObject.transform.SetParent(root.transform,false);var events=eventObject.GetComponent<EventSystem>();
    var catalog=OfficialBattleHudCatalog.LoadDefault();var hud=new OfficialBattleHudView(catalog);hud.Mount(canvasObject.transform,camera);
    int[] ids={26030,26004,26005,26006,26007,26008,26009,26010,26011,26012,26013,26014,26015,26016};
    var state=new FightBattleCheckpoint{StageId=1001,PreparationPending=true,StageStatus=new FightBattleStageStatusConfig()};state.StageStatus.Status.AddRange(ids);
    hud.RefreshStageStatus(state);hud.RefreshPhase(FightBattlePhase.Preparing,new OfficialBattleSessionOptions());hud.Refresh(state);
    for(int i=0;i<60;i++)yield return null;
    var strip=canvasObject.transform.Find("FightMainPanel/Content/Top/FixedContent/StageStatus");
    if(strip==null) strip=canvasObject.GetComponentsInChildren<Button>(true).Select(b=>b.transform).First(t=>t.name=="StageStatus");
    var rect=(RectTransform)strip;
    yield return Click(events,RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),strip);
    OfficialBattleStageStatusDialogView view=null;
    float readyDeadline=Time.unscaledTime+5;
    while(Time.unscaledTime<readyDeadline){view=canvasObject.GetComponentInChildren<OfficialBattleStageStatusDialogView>();if(view!=null&&view.IsReady)break;yield return null;}
    Require(view!=null&&view.IsReady,"Native dialog/mask/show clip did not finish.");
    var scroll=view.GetComponentInChildren<LoopVerticalScrollRect>();
    Require(scroll!=null&&scroll.totalCount==ids.Length,"Original loop data source/count differs.");
    var rows=scroll.content.GetComponentsInChildren<FightStageStatusDialogItemUI>();
    Require(rows.Length>0&&rows.Length<ids.Length,"The original list did not virtualize native rows.");
    var description=rows[0].transform.Find("Labels/ContentText").GetComponent<TMP_Text>();
    capture("stage-status-initial");yield return null;
    Require(description.text.Contains("战斗开始后计算具体效果。"),"Original preparing scale description is missing: "+description.text);
    Require(view.GetComponent<WorldPopupWindow>().mShowClip!=null&&view.GetComponent<WorldPopupWindow>().mHideClip!=null,"Original popup clips lost their references.");
    Require(view.transform.Find("OutsideClickBlocker")==null,"Handmade blocker is still present.");
    capture("stage-status-preparing");yield return null;
    string[] before=rows.Select(x=>x.transform.Find("Labels/NameText").GetComponent<TMP_Text>().text).ToArray();
    var scrollPos=RectTransformUtility.WorldToScreenPoint(null,((RectTransform)scroll.transform).TransformPoint(((RectTransform)scroll.transform).rect.center));
    for(int i=0;i<20;i++){var data=new PointerEventData(events){position=scrollPos,scrollDelta=new Vector2(0,-40)};ExecuteEvents.Execute(scroll.gameObject,data,ExecuteEvents.scrollHandler);yield return null;}
    for(int i=0;i<30;i++)yield return null;
    var after=scroll.content.GetComponentsInChildren<FightStageStatusDialogItemUI>().Select(x=>x.transform.Find("Labels/NameText").GetComponent<TMP_Text>().text).ToArray();
    Require(!before.SequenceEqual(after),"Simulated wheel input did not change native visible rows.");
    capture("stage-status-scrolled");yield return null;
    float clickAt=Time.time+.6f;while(Time.time<clickAt) yield return null;
    var allMaskHits=new List<RaycastResult>();var outside=new Vector2(Screen.width*.97f,Screen.height*.9f);events.RaycastAll(new PointerEventData(events){position=outside},allMaskHits);
    Require(allMaskHits.Count>0&&allMaskHits[0].gameObject.GetComponent<Button>()!=null,"Native blur mask did not intercept outside input.");
    Require(allMaskHits[0].gameObject.GetComponent<Button>().IsInteractable(),"Original mask button is not interactable.");
    yield return Click(events,outside,allMaskHits[0].gameObject.transform);
    Require(view==null || view.GetComponent<WorldPopupWindow>().State==WorldPopupWindow.EState.Hiding || view.GetComponent<WorldPopupWindow>().State==WorldPopupWindow.EState.Hide,"Native mask pointer did not request closing; state="+view.GetComponent<WorldPopupWindow>().State+" timescale="+Time.timeScale);
    float closeDeadline=Time.unscaledTime+5;
    while(view!=null&&Time.unscaledTime<closeDeadline)yield return null;
    Require(view==null,"Native hide animation did not release the dialog and mask.");
    state.PreparationPending=false;hud.RefreshPhase(FightBattlePhase.Play,new OfficialBattleSessionOptions());hud.RefreshStageStatus(state);
    yield return Click(events,RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),strip);
    readyDeadline=Time.unscaledTime+5;
    while(Time.unscaledTime<readyDeadline){view=canvasObject.GetComponentInChildren<OfficialBattleStageStatusDialogView>();if(view!=null&&view.IsReady)break;yield return null;}
    Require(view!=null&&view.IsReady,"Native dialog could not reopen after close; state="+(view==null?"missing":view.GetComponent<WorldPopupWindow>().State.ToString()));
    rows=view.GetComponentInChildren<LoopVerticalScrollRect>().content.GetComponentsInChildren<FightStageStatusDialogItemUI>();
    description=rows[0].transform.Find("Labels/ContentText").GetComponent<TMP_Text>();
    Require(description.text.Contains("本场战斗无衰减。"),"Original factor=1 description is missing: "+description.text);
    capture("stage-status-unscaled");yield return null;
    view.Refresh(state,.875d);Require(description.text.Contains("87.5%"),"Original percent formatter/update subscription differs: "+description.text);
    capture("stage-status-scale-updated");yield return null;
    Debug.Log("Native stage-status dialog passed: public HUD pointer open, original loop virtualization/wheel, preparing description, scale notification, blur-mask pointer close, original clips and reopen.");
   }
   finally {UnityEngine.Object.Destroy(root);}
   yield return null;
  }
        private sealed class Runner : MonoBehaviour { }
    }
}
