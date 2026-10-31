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
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;

// Synthetic controls test the original controller; this is not the official panel layout.
[InitializeOnLoad]
public static class PartnerPanelValidation
{
    private const string Active = "PartnerPanelValidation.Active";
    static PartnerPanelValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("PartnerPanelValidation").AddComponent<PartnerPanelRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Active, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0))
                Finish(new TimeoutException("Partner panel validation timed out"));
        };
    }
    public static void Run()
    {
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        if (string.IsNullOrEmpty(work) || !File.Exists(Path.Combine(work, ".zjcs-validation.json")) ||
            !Path.GetFullPath(Application.dataPath).StartsWith(Path.GetFullPath(work) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Use an owned isolated Unity project");
        SessionState.SetBool(Active, true);
        SessionState.SetFloat(Active + ".Deadline", (float)EditorApplication.timeSinceStartup + 120);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Active, false);
        if (error != null) Debug.LogException(error);
        else Debug.Log("PARTNER_PANEL_PASSED: original gates, simulated tab clicks, callback order, title, fallback, disable reset and listener lifetime; synthetic controls only");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class PartnerPanelRunner : MonoBehaviour, IPartnerPanelServices
{
    private readonly HashSet<int> limited = new HashSet<int>();
    private readonly List<int> gateCalls = new List<int>();
    private readonly List<int> stateCalls = new List<int>();
    private readonly List<string> titleCalls = new List<string>();
    private readonly Dictionary<NpcPanelTabType, WorldTabItem> tabs = new Dictionary<NpcPanelTabType, WorldTabItem>();
    private EventSystem events;
    private Camera cameraUi;
    private RenderTexture target;
    private GameObject root, bar, dialogue;
    private NpcPanel panel;
    private WorldTabGroup group;
    private WorldStateSwitcher<NpcPanelTabType> state;
    private WorldMultiLanguageText title;
    private int dialogueCalls, errors, pointerClicks;

    private sealed class StateRecorder : IWorldStateConfig
    {
        private readonly List<int> values;
        public StateRecorder(List<int> values) { this.values = values; }
        public void SetState(WorldStateConfigValue value) => values.Add(value);
        public void Clear() { }
    }
    private IEnumerator Start()
    {
        var run = Check();
        while (true)
        {
            object next;
            try { if (!run.MoveNext()) break; next = run.Current; }
            catch (Exception error) { PartnerPanelValidation.Finish(error); yield break; }
            yield return next;
        }
        PartnerPanelValidation.Finish(errors == 0 ? null : new Exception("Unexpected Unity errors: " + errors));
    }
    private IEnumerator Check()
    {
        Application.logMessageReceived += Log;
        var font = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>("Assets/Editor/FZY4K.ttf"));
        Require(font, "Official fixture font");
        WorldMultiLanguageText.Localize = key => key;
        WorldMultiLanguageText.LanguageFont = () => font;
        events = new GameObject("SimulatedPointer", typeof(EventSystem)).GetComponent<EventSystem>();
        cameraUi = new GameObject("PanelFixtureCamera", typeof(Camera)).GetComponent<Camera>();
        cameraUi.transform.position = new Vector3(0, 0, -10);
        cameraUi.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
        cameraUi.clearFlags = CameraClearFlags.SolidColor;
        target = new RenderTexture(768, 1024, 24); cameraUi.targetTexture = target;
        var canvas = new GameObject("SyntheticControllerFixtureOnly", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cameraUi; canvas.planeDistance = 1;
        root = Node("Panel", canvas.transform, 0, 0); root.SetActive(false);
        group = root.AddComponent<WorldTabGroup>();
        bar = Node("Tabs", root.transform, 0, -240);
        var dictionary = new MonoDictionary<NpcPanelTabType, WorldTabItem>();
        foreach (NpcPanelTabType type in Enum.GetValues(typeof(NpcPanelTabType)))
        {
            var go = Node(type.ToString(), bar.transform, ((int)type - 1) * 220, 0);
            var image = go.AddComponent<Image>(); image.color = Color.gray;
            var tab = go.AddComponent<WorldTabItem>(); tab.targetGraphic = image; tab.SetGroup(group);
            tabs.Add(type, tab); dictionary.Add(type, tab);
            var label = Label(type.ToString(), go.transform, font, 0, 0); label.SetKey(type.ToString());
        }
        title = Label("Title", root.transform, font, 0, 240);
        title.OnTextRefresh += () => titleCalls.Add(title.myKey);
        var panelTitle = title.gameObject.AddComponent<PanelTitle>(); panelTitle.TitleTxt = title;
        var list = Node("Roster", root.transform, 0, 0);
        dialogue = Node("Dialogue", root.transform, 0, 0);
        state = WorldStateSwitcher<NpcPanelTabType>.FromBindings(root, new IWorldStateConfig[] { new StateRecorder(stateCalls) });
        panel = root.AddComponent<NpcPanel>(); panel.Configure(this);
        Set(panel, "mPanelTitle", panelTitle); Set(panel, "mNpcListGo", list); Set(panel, "mNpcDialogueListGo", dialogue);
        Set(panel, "mTabGroupGo", bar); Set(panel, "mTabGroup", group);
        Set(panel, "mTabType2TabItemDict", dictionary); Set(panel, "mTabSwitcher", state);
        root.SetActive(true); yield return null;
        Require(gateCalls.SequenceEqual(new[] { 65, 57, 174 }) && bar.activeSelf, "Original three function gates and visible multi-tab bar");
        Require(state.State == NpcPanelTabType.NpcList && title.myKey == "ui_npc_panel_addressbook", "Original default roster/title");
        stateCalls.Clear(); titleCalls.Clear();
        Click(tabs[NpcPanelTabType.Dialogue].gameObject); yield return null;
        Require(stateCalls.SequenceEqual(new[] { 0, 1 }) && dialogueCalls == 1 && titleCalls.SequenceEqual(new[] { "ui_npc_panel_dialogue" }), "Off roster precedes on dialogue; title only follows on callback");
        stateCalls.Clear(); titleCalls.Clear();
        Click(tabs[NpcPanelTabType.StarGod].gameObject); yield return null;
        Require(stateCalls.SequenceEqual(new[] { 1, 2 }) && dialogueCalls == 2 && titleCalls.SequenceEqual(new[] { "ui_npc_star_god_title" }), "Dialogue initializes on off callback before star tab state/title");
        root.SetActive(false); gateCalls.Clear(); root.SetActive(true); yield return null;
        Require(state.State == NpcPanelTabType.NpcList && tabs[NpcPanelTabType.NpcList].isOn, "Disable resets original default to roster");
        root.SetActive(false); limited.Add(57); panel.SetDefaultShowTab(NpcPanelTabType.Dialogue); root.SetActive(true); yield return null;
        Require(!tabs[NpcPanelTabType.Dialogue].gameObject.activeSelf && bar.activeSelf && state.State == NpcPanelTabType.NpcList, "Closed requested tab falls back to first active tab");
        root.SetActive(false); limited.Add(65); panel.SetDefaultShowTab(NpcPanelTabType.NpcList); root.SetActive(true); yield return null;
        Require(!bar.activeSelf && tabs[NpcPanelTabType.StarGod].gameObject.activeSelf && state.State == NpcPanelTabType.StarGod, "Single allowed tab hides bar and remains selected");
        root.SetActive(false); limited.Clear(); panel.SetDefaultShowTab(NpcPanelTabType.Dialogue); root.SetActive(true); yield return null;
        Require(state.State == NpcPanelTabType.Dialogue && title.myKey == "ui_npc_panel_dialogue", "Explicit allowed default selects dialogue");
        Click(tabs[NpcPanelTabType.NpcList].gameObject); yield return null;
        Require(state.State == NpcPanelTabType.NpcList && pointerClicks == 3, "Simulated pointer returns to roster");
        Capture();
        root.SetActive(false); limited.UnionWith(new[] { 65, 57, 174 }); root.SetActive(true); yield return null;
        Require(!bar.activeSelf && tabs.Values.All(tab => !tab.gameObject.activeSelf) && state.State == NpcPanelTabType.NpcList, "All gates closed retain original default fallback without invented branch");
        root.SetActive(false); int stateCount = stateCalls.Count, titleCount = titleCalls.Count, initCount = dialogueCalls;
        Object.DestroyImmediate(panel);
        group.OnTabItemChanged.Invoke(tabs[NpcPanelTabType.Dialogue], true);
        Require(stateCalls.Count == stateCount && titleCalls.Count == titleCount && dialogueCalls == initCount, "Destroy removes panel listener");
        Application.logMessageReceived -= Log;
    }
    public bool IsFunctionLimit(int functionType) { gateCalls.Add(functionType); return limited.Contains(functionType); }
    public void InitDialogueList(GameObject owner)
    {
        Require(owner == dialogue && state.State == NpcPanelTabType.Dialogue, "Exact dialogue owner and state precede initialization");
        dialogueCalls++;
    }
    private void Click(GameObject go)
    {
        Canvas.ForceUpdateCanvases();
        var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(cameraUi, go.transform.position) };
        var hits = new List<RaycastResult>(); events.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && hits[0].gameObject == go, "Unoccluded simulated pointer reaches tab " + go.name);
        pointer.pointerCurrentRaycast = hits[0];
        ExecuteEvents.ExecuteHierarchy(go, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(go, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(go, pointer, ExecuteEvents.pointerClickHandler); pointerClicks++;
    }
    private static GameObject Node(string name, Transform parent, float x, float y)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform; rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(200, 55); return go;
    }
    private static WorldMultiLanguageText Label(string name, Transform parent, TMP_FontAsset font, float x, float y)
    {
        var go = Node(name, parent, x, y); var text = go.AddComponent<TextMeshProUGUI>(); text.font = font;
        text.fontSize = 16; text.raycastTarget = false; return go.AddComponent<WorldMultiLanguageText>();
    }
    private static void Set(object owner, string field, object value)
        => owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    private static void Require(bool pass, string message) { if (!pass) throw new InvalidOperationException(message); }
    private void Log(string message, string stack, LogType type) { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) errors++; }
    private void Capture()
    {
        Canvas.ForceUpdateCanvases(); cameraUi.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
        Require(image.GetPixels32().Count(p => p.r > 100 && p.g > 100) > 1000, "Fixture controls render on GPU");
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "panel-controller-fixture.png"), image.EncodeToPNG());
        RenderTexture.active = previous; Object.Destroy(image);
    }
}
