#if UNITY_EDITOR
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
using Zjcs.Official.HomeFunctionMenuUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.HomeFunctionMenuUI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Official.HomeScreenUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class HomeFunctionMenuOwnerValidation
{
    const string Active="HomeFunctionMenuOwnerValidation";
    static HomeFunctionMenuOwnerValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active,false))
                new GameObject("HomeFunctionMenuOwnerValidation").AddComponent<HomeFunctionMenuOwnerRunner>(); };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Active,false) && EditorApplication.timeSinceStartup>SessionState.GetFloat(Active+"Deadline",float.MaxValue))
                Finish(new TimeoutException("Native function menu owner timeout.")); };
    }
    public static void Run()
    {
        string work=Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!Path.GetFullPath(Application.dataPath).StartsWith(work+Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned isolated project required.");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active,true);
        SessionState.SetFloat(Active+"Deadline",(float)EditorApplication.timeSinceStartup+180);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Active,false);
        if (error != null) Debug.LogException(error);
        else Debug.Log("HOME_FUNCTION_MENU_OWNER_PASSED");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed partial class HomeFunctionMenuOwnerRunner : MonoBehaviour
{
    OfflineHomeFunctionMenu view;
    FontAssetFontService fonts;
    MenuServices host;
    Camera camera;
    RenderTexture target;
    IDisposable metrics;
    ScreenAdaptService screen;
    sealed class TargetMetrics : IScreenMetrics
    {
        public int Width=>608;
        public int Height=>1080;
        public ScreenOrientation Orientation=>ScreenOrientation.Portrait;
        public Rect SafeArea=>new Rect(0,0,608,1080);
    }
    IEnumerator Start()
    {
        Application.runInBackground = true; AudioListener.volume = 0;
        var steps = new Stack<IEnumerator>(); steps.Push(Check());
        while (steps.Count > 0)
        {
            object current;
            try { if (!steps.Peek().MoveNext()) { steps.Pop(); continue; } current = steps.Peek().Current; }
            catch (Exception error) { view?.Dispose(); fonts?.Dispose(); host?.Dispose(); HomeFunctionMenuOwnerValidation.Finish(error); yield break; }
            if (current is IEnumerator nested) { steps.Push(nested); continue; }
            yield return current;
        }
        HomeFunctionMenuOwnerValidation.Finish(null);
    }
    IEnumerator Check()
    {
        var cameraObject = new GameObject("UI Camera", typeof(Camera), typeof(AudioListener));
        camera = cameraObject.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.transform.position = new Vector3(0,0,-10);
        target = new RenderTexture(608,1080,24); camera.targetTexture=target;
        camera.backgroundColor = new Color(.1f,.12f,.14f); camera.orthographic = true;
        var canvasObject = new GameObject("Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera; canvas.planeDistance = 1;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080,1920); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        new GameObject("EventSystem", typeof(EventSystem));
        metrics=ScreenMetrics.Bind(new TargetMetrics()); screen=new ScreenAdaptService(); screen.Initialize();
        var hudAssets = OfflineHomeHudAssets.LoadDefault(); fonts = hudAssets.CreateFontService(); fonts.Initialize();
        var assets = OfflineHomeFunctionMenuAssets.LoadDefault();
        Require(assets.prefab && assets.tipSourceObjects && assets.sounds.All(x=>x.clip), "explicit original asset bindings");
        Require(assets.Text("{0}") == "{0}", "original literal format placeholder");
        host = new MenuServices(); var tips = new MenuTips();
        view = new OfflineHomeFunctionMenu(canvas.transform, new HomeFunctionMenuBindings {
            Camera=camera, Screen=screen, Menu=host, Activities=host, Wiki=host, Download=host, Time=host,
            Font=hudAssets.defaultFont, TipDots=tips, TipController=tips, PostSound=host.Play });
        host.Menu = view.Root;
        Require(!view.IsOpen && host.LevelSubscribers==0 && host.StateSubscribers==0, "construct inactive before original lifecycle");
        Require(view.Root.GetComponentsInChildren<MonoBehaviour>(true).All(x=>x), "all original components retained");
        Require(view.Root.GetComponent<SafeArea>() && !view.Root.GetComponent<WorldSafeArea>(), "complete original SafeArea binding");
        view.Init(); yield return WaitState(WorldPopupWindow.EState.Show);
        Require(host.OpenCount==1 && host.CloseCount==1 && host.LevelSubscribers==1 && host.RankSubscribers==1,
            "one original activity/player subscription set");
        Require(host.StateSubscribers==1 && tips.Listeners.Count>0, "download and original tip addresses bound");
        Debug.Log("MENU_GEOMETRY screen="+Screen.width+"x"+Screen.height+" safe="+Screen.safeArea+" camera="+camera.pixelRect+
            " parent="+((RectTransform)canvas.transform).rect+" root="+((RectTransform)view.Root.transform).rect+
            " local="+view.Root.transform.localPosition+" task="+view.Root.GetComponentInChildren<FunctionEntryTaskBtn>(true).Button.transform.position);
        yield return new WaitForEndOfFrame(); Capture("function-menu-owner-open.png");
        Click(view.Root.GetComponentInChildren<FunctionEntryTaskBtn>(true).Button);
        Require(host.Calls.Last()=="Task:null:null", "native task navigation arguments");
        var settings=view.Root.GetComponentInChildren<SettingsBtn>(true);
        Click(settings.Btn); Require(host.Calls.Last()=="Settings:Normal", "native settings callback");
        var skin=view.Root.GetComponentInChildren<HomeSkinBtn>(true);
        Click(skin.Button);
        Require(host.Calls.Contains("Init:HomeSkin") && view.Popup.State==WorldPopupWindow.EState.Hiding,
            "native skin dispatch and parent close use the real owner");
        yield return WaitState(WorldPopupWindow.EState.Hide);
        Require(!view.IsOpen && host.OpenCount==0 && host.CloseCount==0 && host.LevelSubscribers==0
            && host.RankSubscribers==0 && tips.Listeners.Count==0, "native hide releases active observers");
        Require(host.StateSubscribers==1, "download observer remains until destruction");
        view.Init(); yield return WaitState(WorldPopupWindow.EState.Show);
        Require(host.OpenCount==1 && host.CloseCount==1 && host.LevelSubscribers==1 && host.StateSubscribers==1,
            "reopen does not duplicate observers");
        yield return new WaitForEndOfFrame(); Capture("function-menu-owner-reopened.png");
        var close=view.Root.GetComponentsInChildren<CloseWindowBtn>(true).Single(x=>x.name=="CloseBtn");
        Click(close.myButton);
        Require(host.AudioPosts>=5 && host.PlayingAudio, "original popup and click sound events play while muted");
        yield return WaitState(WorldPopupWindow.EState.Hide);
        view.Dispose(); yield return null;
        Require(host.StateSubscribers==0 && host.OpenCount==0 && host.LevelSubscribers==0 && tips.Listeners.Count==0,
            "dispose releases remaining native observers");
        host.Dispose(); fonts.Dispose(); camera.targetTexture=null; target.Release(); Object.Destroy(target);
        screen.Dispose(); metrics.Dispose();
    }
    IEnumerator WaitState(WorldPopupWindow.EState state)
    {
        float deadline=Time.realtimeSinceStartup+5;
        while(view.Popup.State!=state && Time.realtimeSinceStartup<deadline) yield return null;
        Require(view.Popup.State==state, "original popup completes "+state);
        yield return null; Canvas.ForceUpdateCanvases();
    }
    static void Require(bool value,string message) { if(!value)throw new InvalidOperationException(message); }
    void Click(Button button)
    {
        Require(button && button.gameObject.activeInHierarchy && button.interactable, "clickable native button");
        Canvas.ForceUpdateCanvases();
        var rect=(RectTransform)button.transform;
        var point=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
        var input=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(input,hits);
        Require(hits.Count>0, "native button receives raycast");
        var target=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        Require(target==button.gameObject, "native button is not occluded");
        ExecuteEvents.Execute(target,input,ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target,input,ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target,input,ExecuteEvents.pointerClickHandler);
    }
    void Capture(string name)
    {
        Canvas.ForceUpdateCanvases(); camera.Render();
        var previous=RenderTexture.active; RenderTexture.active=target;
        var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();RenderTexture.active=previous;
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),name),texture.EncodeToPNG());
        Object.Destroy(texture);
    }
}
#endif
