using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Common;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.HomeStoryDialog;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.HomeStory;
using Zjcs.Offline.HomeStoryDialog;
using Zjcs.Offline.HomeUI;

[InitializeOnLoad]
public static class HomeFruitTreeDialogueValidation
{
    private const string Active = "Zjcs.FruitTreeDialogueValidation";
    static HomeFruitTreeDialogueValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("FruitTreeDialogueValidation").AddComponent<HomeFruitTreeDialogueRunner>();
        };
    }
    public static void ConfigureView() => Type.GetType("Zjcs.Editor.Home.HomeAcceptanceLauncher, Zjcs.Editor", true)
        .GetMethod("ConfigurePortraitGameView", BindingFlags.Static | BindingFlags.NonPublic)
        .Invoke(null, new object[] { 450, 800 });
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        ConfigureView();
        SessionState.SetBool(Active, true);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false);
        if (error == null) Debug.Log("FRUIT_TREE_DIALOGUE_PASSED: all original lines, native portraits, pointer clicks, voices and window cleanup.");
        else Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class HomeFruitTreeDialogueRunner : MonoBehaviour
{
    private sealed class NodeHost : HomeFurnitureStoryGraph.IHost
    {
        public int Target;
        public HomeFurnitureStoryGraph.Node Found;
        public void Execute(string story, HomeFurnitureStoryGraph.Node node, Action<int?> completed)
        {
            if (node.Id == Target) Found = node;
            else completed(null);
        }
    }
    private sealed class Environment : IHomeStoryDialogEnvironment
    {
        private readonly HomeStoryDialogAssets assets;
        private readonly AudioSource audio;
        public readonly List<string> Voices = new List<string>();
        public Environment(HomeStoryDialogAssets assets, GameObject host)
        {
            this.assets = assets;
            audio = host.AddComponent<AudioSource>();
            audio.playOnAwake = false;
        }
        public string PlayerName => "测试角色";
        public int PlayerRoleId => 100001;
        public RegionCodeEnum Region() => RegionCodeEnum.CN;
        public DateTime Now() => DateTime.Now;
        public bool IsConditionMet(int type, string parameters) => throw new InvalidOperationException("Unexpected option condition");
        public bool ContainsText(string key) => false;
        public string Localize(string key) => key;
        public WorldPopupWindow LastWindowIgnoringTips() => null;
        public bool ActivePanelHasSceneGraphic() => false;
        public void CloseWindow(GameObject root) => root.GetComponent<WorldPopupWindow>().Hide(null);
        public void PostVoice(string key, AudioClip clip, GameObject emitter)
        {
            audio.Stop();
            if (!clip) return;
            Require(assets.Voices.Single(row => row.EventKey == key).Clip == clip && clip.length > 0,
                "Original fruit-tree voice binding: " + key);
            audio.clip = clip;
            audio.Play();
            Require(audio.isPlaying, "Original voice is playing in the muted test process");
            Voices.Add(key);
        }
        public MyPlayable PlayWorldNpcPlayable(int id, string path) => null;
        public void ReleasePlayableResources() { }
    }
    private int errors;
    private Canvas validationCanvas;
    private void LateUpdate()
    {
        if (!validationCanvas) return;
        foreach (var canvas in UnityEngine.Object.FindObjectsOfType<Canvas>())
            if (!canvas.transform.IsChildOf(validationCanvas.transform) && canvas.gameObject.scene.IsValid()) canvas.gameObject.SetActive(false);
    }
    private IEnumerator Start()
    {
        AudioListener.volume = 0;
        Application.runInBackground = true;
        Application.logMessageReceived += Record;
        var steps = Check();
        while (true)
        {
            object current;
            try { if (!steps.MoveNext()) break; current = steps.Current; }
            catch (Exception error) { HomeFruitTreeDialogueValidation.Finish(error); yield break; }
            yield return current;
        }
        Application.logMessageReceived -= Record;
        HomeFruitTreeDialogueValidation.Finish(errors == 0 ? null : new Exception("Runtime errors: " + errors));
    }
    private void Record(string text, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
    }
    private IEnumerator Check()
    {
        yield return new WaitForEndOfFrame();
        HomeFruitTreeDialogueValidation.ConfigureView();
        yield return new WaitForEndOfFrame();
        var camera = new GameObject("DialogueCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.backgroundColor = new Color(.08f, .12f, .17f);
        new GameObject("EventSystem", typeof(EventSystem));
        var canvas = new GameObject("DialogueCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        validationCanvas = canvas;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        var hud = OfflineHomeHudAssets.LoadDefault();
        var fonts = hud.CreateFontService();
        fonts.Initialize();
        var assets = Resources.Load<HomeStoryDialogAssets>("Home/HomeFruitTreeStoryAssets");
        Require(assets && assets.Voices.Length == 8 && assets.OptionTalkDialog == HomeStoryDialogAssets.LoadDefault().OptionTalkDialog,
            "Fruit-tree assets share the original complete dialogue prefab");
        var environment = new Environment(assets, gameObject);
        using var window = new HomeStoryDialogWindow(canvas, assets, HomeStoryPortraitAssets.LoadDefault(),
            HomeStoryOptionAssets.LoadDefault(), HomeStorySettings.LoadDefault(), hud.defaultFont, environment, (clip, owner) => { });
        using var runner = new HomeFurnitureStoryDialogNode(this, window);
        var nodeHost = new NodeHost();
        var graph = new HomeFurnitureStoryGraph(assets.StorySource, nodeHost);
        foreach (string story in new[] { "FruitTreeUnlock1", "FruitTreeUnlock2" })
        foreach (int id in story == "FruitTreeUnlock1" ? new[] { 8, 21, 27 } : new[] { 39, 27 })
        {
            nodeHost.Target = id;
            nodeHost.Found = null;
            graph.Play(story, null);
            Require(nodeHost.Found != null, "Original dialogue node is reachable");
            graph.Cancel();
            int dialogId = (int)nodeHost.Found.Data["DialogId"];
            var rows = window.Services.GetDialogInfos(story, dialogId);
            int before = environment.Voices.Count;
            bool completed = false;
            runner.Begin(story, nodeHost.Found, selected => completed = true);
            var panel = window.Root.GetComponent<OptionTalkDialogPanel>();
            var popup = window.Root.GetComponent<WorldPopupWindow>();
            float showDeadline = Time.realtimeSinceStartup + 8;
            while (popup.State != WorldPopupWindow.EState.Show && Time.realtimeSinceStartup < showDeadline) yield return null;
            Require(popup.State == WorldPopupWindow.EState.Show, "Original show animation finishes before pointer input");
            Type.GetType("Zjcs.Presentation.Home.HomeAcceptanceDriver, Zjcs.Presentation.Home", true)
                .GetMethod("HideForeignCanvases", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            var next = (Button)typeof(TalkDialog).GetField("mNextBtn", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
            var typing = typeof(TalkDialog).GetField("mInTypeWriting", BindingFlags.Instance | BindingFlags.NonPublic);
            var ready = typeof(TalkDialog).GetField("mWaitClickForNext", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var row in rows)
            {
                int index = rows.IndexOf(row);
                float deadline = Time.realtimeSinceStartup + 15;
                while ((environment.Voices.Count <= before + index || !next.gameObject.activeInHierarchy)
                    && Time.realtimeSinceStartup < deadline) yield return null;
                Require(environment.Voices.Count == before + index + 1
                    && environment.Voices.Last() == row.VoiceEventKey, "Original line and voice order");
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(System.Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                    "before-" + story + "-" + dialogId + "-" + row.Order + ".png"));
                yield return null;
                if ((bool)typing.GetValue(panel)) { Click(next); yield return null; }
                deadline = Time.realtimeSinceStartup + 8;
                while (!(bool)ready.GetValue(panel) && Time.realtimeSinceStartup < deadline) yield return null;
                Require((bool)ready.GetValue(panel), "Original typewriter completes");
                var content = (WorldMultiLanguageText)typeof(TalkDialog).GetField("mTalkContentTxt", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
                var text = content.GetComponent<TMP_Text>();
                Require(text.text == window.Services.Localize($"dialog_{story}_{dialogId}_{row.Order}"),
                    "Original localized dialogue text");
                deadline = Time.realtimeSinceStartup + 8;
                while (text.canvasRenderer.GetInheritedAlpha() < .95f && Time.realtimeSinceStartup < deadline) yield return null;
                Debug.Log("FRUIT_DIALOG_TEXT: active=" + text.isActiveAndEnabled + "; alpha=" + text.canvasRenderer.GetInheritedAlpha()
                    + "; groups=" + string.Join(",", text.GetComponentsInParent<CanvasGroup>(true).Select(group => group.name + ":" + group.alpha))
                    + "; animators=" + string.Join(",", panel.GetComponentsInChildren<Animator>(true).Select(animator => animator.name + ":" + animator.GetCurrentAnimatorStateInfo(0).normalizedTime)));
                Require(text.isActiveAndEnabled && text.canvasRenderer.GetInheritedAlpha() >= .95f,
                    "Original dialogue text finishes its visible animation before capture");
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(System.Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                    story + "-" + dialogId + "-" + row.Order + ".png"));
                Click(next);
                yield return null;
            }
            float finishDeadline = Time.realtimeSinceStartup + 8;
            while (!completed && Time.realtimeSinceStartup < finishDeadline) yield return null;
            Require(completed, "Original dialogue completion reaches the graph node callback");
            window.Close();
            finishDeadline = Time.realtimeSinceStartup + 8;
            while (window.Root && Time.realtimeSinceStartup < finishDeadline) yield return null;
            Require(!window.Root, "Native dialogue and attached mask release between nodes");
        }
        Require(environment.Voices.Count == 8, "Every original fruit-tree line and voice was exercised");
        fonts.Dispose();
    }
    private static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)button.transform;
        var pointer = new PointerEventData(EventSystem.current) {
            position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)),
            button = PointerEventData.InputButton.Left
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Debug.Log("FRUIT_DIALOG_POINTER: button=" + button.name + "; ancestors="
            + string.Join("/", button.GetComponentsInParent<Transform>(true).Select(value => value.name))
            + "; rect=" + rect.rect + "; hits=" + string.Join(",", hits.Select(hit => hit.gameObject.name)));
        Require(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(button.transform),
            "Original next button receives scene-local pointer: " + pointer.position + "; "
                + string.Join(",", hits.Select(hit => hit.gameObject.name)));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
    private static void Require(bool value, string text)
    {
        if (!value) throw new InvalidOperationException(text);
    }
}
