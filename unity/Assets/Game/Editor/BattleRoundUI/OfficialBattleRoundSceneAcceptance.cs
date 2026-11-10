using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;
using Zjcs.Official.BattleRoundUI;
using Zjcs.Official.WorldUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Zjcs.Editor.BattleRound
{
    public static class OfficialBattleRoundSceneAcceptance
    {
        private const string Key = "Zjcs.BattleRoundScene.Acceptance";
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
                Debug.LogError("Native round scene timed out.");
                EditorApplication.isPlaying = false;
                return;
            }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            Application.logMessageReceived += Observe;
            new GameObject("Native round scene verification runner").AddComponent<Runner>().StartCoroutine(Verify());
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
  private static IEnumerator Steps(Action<string> capture)
  {
   var root = new GameObject("Native timeline verification");
   GameRoundScene scene = null;
   IDisposable renderPipelineScope = null;
   try
   {
    var pipeline=Resources.Load<OfficialBattleRenderPipelineAssets>("Combat/OfficialBattleRenderPipelineAssets");
    Require(pipeline!=null,"Original battle render pipeline binding is missing.");
    renderPipelineScope=pipeline.Enter();
    yield return null;
    Require(GraphicsSettings.currentRenderPipeline==pipeline.Pipeline && QualitySettings.renderPipeline==null,
     "Timeline verification must use the same original render pipeline as the public battle controller.");
    var cameraObject = new GameObject("Battle camera", typeof(Camera));
    cameraObject.transform.SetParent(root.transform, false);
    cameraObject.transform.position = new Vector3(0, 0, -30);
    var camera = cameraObject.GetComponent<Camera>();
    camera.orthographic = true; camera.orthographicSize = 6;
    camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.15f,.24f,.28f);
    var canvasObject = new GameObject("Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
    canvasObject.transform.SetParent(root.transform,false);
    canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
    var scaler=canvasObject.GetComponent<CanvasScaler>();
    scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution=new Vector2(1080,1920); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
    var eventsObject=new GameObject("EventSystem",typeof(EventSystem)); eventsObject.transform.SetParent(root.transform,false);
    var events=eventsObject.GetComponent<EventSystem>();
    var hud=new OfficialBattleHudView(OfficialBattleHudCatalog.LoadDefault());
    GameObject hudRoot=hud.Mount(canvasObject.transform,camera);
    scene=UnityEngine.Object.FindObjectOfType<GameRoundScene>();
    Require(scene!=null && scene.IconRoot.name=="Slot" && scene.Line.name=="Linear", "Original scene component references were lost.");
    Require(scene.GetComponentsInChildren<Transform>(true).Length==4, "Native round scene hierarchy was reconstructed or extended.");
    Require(scene.Line.size==new Vector2(6.4f,.15f) && scene.Line.sortingOrder==-2, "Original round line geometry/order differs.");
    foreach(var renderer in scene.GetComponentsInChildren<SpriteRenderer>())
     Require(renderer.sprite!=null && renderer.sharedMaterial!=null && renderer.sharedMaterial.shader.isSupported,"Native timeline sprite/material failed: "+renderer.name);
    var state=new FightBattleCheckpoint();
    for(int id=1;id<=3;id++)
    {
     var unit=new FightBattleUnitState { Id=id, NpcClassId=new[]{8401,8411,11001}[id-1], Side=1, Hp=1, MaxHp=1 };
     unit.Turn.Id=id;unit.Turn.Hp=1;unit.Turn.Speed=100;unit.Turn.IntervalTicks=10000;
     state.Units.Add(unit);state.Timeline.Entries.Add(new FightTurnEntry {Id=id,Next=(id-1)*2500});
    }
    Func<FightBattleUnitState,Sprite> portrait=u=>OfficialBattleNpcPortraitCatalog.Load().Find(u.NpcClassId);
    hud.RefreshTimeline(state,portrait,null,false);
    hud.RefreshPhase(FightBattlePhase.Play,new OfficialBattleSessionOptions());
    yield return new WaitForSeconds(.8f);
    var anchor=(RectTransform)hudRoot.transform.Find(OfficialBattleTimelineAssets.Load().Anchor);
    void CheckProjection()
    {
     Canvas.ForceUpdateCanvases();
     var screen=RectTransformUtility.WorldToScreenPoint(null,anchor.position);
     Vector3 actual=camera.WorldToScreenPoint(scene.transform.position);
     Require(Vector2.Distance(screen,actual)<.1f && Mathf.Abs(scene.transform.position.z+16)<.00001f,"Native round scene lost its animated HUD anchor: expected="+screen+" actual="+actual+" world="+scene.transform.position);
     float expected=camera.orthographicSize/5*(scaler.referenceResolution.y/((RectTransform)canvasObject.transform).rect.height);
     Require(Mathf.Abs(scene.transform.localScale.x-expected)<.00001f,"Native round scene scale differs.");
    }
    void CheckActor(int id)
    {
     Transform card=scene.IconRoot.Find("GameRoundCharacterItem_"+id);
     float half=scene.Line.size.x*scene.Line.transform.localScale.x/2;
     Require(card!=null && Mathf.Abs(card.localPosition.x-half)<.00001f && card.GetSiblingIndex()==scene.IconRoot.childCount-1
      && card.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).IsName("UIGameRound_Acting"),"Current actor order/animation differs: "+id+" pos="+(card==null?"missing":card.localPosition.ToString())+" expectedX="+half+" sibling="+(card==null?-1:card.GetSiblingIndex())+" count="+scene.IconRoot.childCount+" state="+(card==null?0:card.GetComponent<Animator>().GetCurrentAnimatorStateInfo(0).shortNameHash));
    }
    yield return null;CheckProjection(); CheckActor(1);
    foreach(Transform card in scene.IconRoot)
    {
     var group=card.GetComponent<SortingGroup>();
     Require(group!=null && group.sortingLayerID==scene.Line.sortingLayerID
      && group.sortingOrder==1000+card.GetSiblingIndex() && group.sortingOrder>scene.Line.sortingOrder
      && card.GetComponent<GameSceneObjectOrderLayer>()!=null,"Original card sorting was lost; timeline line can cover the portrait.");
    }
    capture("round-scene-initial");yield return null;
    state.Timeline.Acting=3;state.Timeline.Entries[2].Next=20000;
    hud.RefreshTimeline(state,portrait,null,true);yield return new WaitForSeconds(.4f);
    CheckActor(3);Require(state.Timeline.Entries[0].Id==1 && state.Timeline.Entries[2].Next==20000,"Presentation modified authoritative timeline.");
    capture("round-scene-acting");yield return null;
    var restored=JsonUtility.FromJson<FightBattleCheckpoint>(JsonUtility.ToJson(state));
    restored.Timeline.Acting=2;hud.RefreshTimeline(restored,portrait,null,true);yield return new WaitForSeconds(.4f);CheckActor(2);
    camera.orthographicSize=8;yield return null;yield return null;CheckProjection();
    scaler.referenceResolution=new Vector2(1080,2160);Canvas.ForceUpdateCanvases();yield return null;yield return null;CheckProjection();
    var animation=hudRoot.GetComponent<OfficialBattleHudAnimationPlayer>();animation.enabled=false;
    Require(animation.Play("/FightMainPanel","FightMainPanel_Show"),"Original main show animation missing.");
    animation.Tick(.325f);Canvas.ForceUpdateCanvases();yield return null;yield return null;CheckProjection();
    capture("round-scene-anchor-animation");yield return null;
    animation.Tick(1);animation.enabled=true;
    scene.SetVisible(false);yield return null;
    Require(scene.transform.position==new Vector3(0,1000,0),"Hidden native scene was projected back on screen.");
    scene.SetVisible(true);yield return null;yield return null;CheckProjection();
    var alpha=scene.GetComponent<SpriteRendererCanvas>();Require(alpha!=null,"Original scene alpha component missing.");
    var colors=scene.GetComponentsInChildren<SpriteRenderer>().Select(r=>r.color).ToArray();
    alpha.Alpha=.4f;alpha.RefreshAlpha();
    Require(scene.GetComponentsInChildren<SpriteRenderer>().Select(r=>r.color).SequenceEqual(colors),"Original empty alpha references were populated by the host.");
    alpha.Alpha=1;alpha.RefreshAlpha();
    int speedClicks=0;hud.SpeedCycled+=()=>speedClicks++;
    Transform speed=hudRoot.transform.Find("FightMainPanelSpeedUI/Btn");
    yield return Click(events,RectTransformUtility.WorldToScreenPoint(null,((RectTransform)speed).TransformPoint(((RectTransform)speed).rect.center)),speed);
    Require(speedClicks==1,"Native scene blocked the original HUD speed pointer.");
    restored.Units[1].Hp=0;hud.RefreshTimeline(restored,portrait,null,true);yield return new WaitForSeconds(.8f);
    Require(scene.IconRoot.Find("GameRoundCharacterItem_2")==null,"Dead actor card was retained.");
    capture("round-scene-after-death");yield return null;
    restored.Timeline.Entries.Clear();hud.RefreshTimeline(restored,portrait,null,false);yield return null;
    Require(scene.IconRoot.childCount==0,"Empty timeline retained cards.");
    UnityEngine.Object.Destroy(hudRoot);yield return null;yield return null;
    Require(scene==null,"HUD lifetime leaked the native round scene.");
    Debug.Log("Native round scene passed: original references/materials/card sorting, public HUD, actor order/restore/death, camera/canvas/animated-anchor projection, hide/show, original empty alpha references, simulated speed pointer, empty queue and lifetime.");
   }
   finally {UnityEngine.Object.Destroy(root);if(scene!=null)UnityEngine.Object.Destroy(scene.gameObject);renderPipelineScope?.Dispose();}
   yield return null;
  }
        private sealed class Runner : MonoBehaviour { }
    }
}
