using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PartnerUiHelperValidation
{
    static PartnerUiHelperValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("PartnerHelpers", false))
                new GameObject("PartnerHelperValidation").AddComponent<PartnerUiHelperRunner>();
        };
    }

    public static void Run()
    {
        SessionState.SetBool("PartnerHelpers", true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    public static void Finish(Exception error)
    {
        SessionState.SetBool("PartnerHelpers", false);
        if (error != null) Debug.LogException(error);
        else Debug.Log("PARTNER_UI_HELPERS_PASSED: simulated pointer raycasts, original callbacks, filter cache/defaults, sequence lifecycle");
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}

public sealed class PartnerUiHelperRunner : MonoBehaviour
{
    private EventSystem events;
    private RectTransform canvas;
    private Camera uiCamera;
    private RenderTexture renderTarget;
    private GameObject root;
    private int failures;

    private IEnumerator Start()
    {
        IEnumerator run = Check();
        while (true)
        {
            object next;
            try
            {
                if (!run.MoveNext()) break;
                next = run.Current;
            }
            catch (Exception error) { PartnerUiHelperValidation.Finish(error); yield break; }
            yield return next;
        }
        PartnerUiHelperValidation.Finish(failures == 0 ? null : new Exception("Unexpected Unity errors: " + failures));
    }

    private IEnumerator Check()
    {
        Application.logMessageReceived += Log;
        events = new GameObject("SimulatedInput", typeof(EventSystem)).GetComponent<EventSystem>();
        uiCamera = new GameObject("FixtureRenderCamera", typeof(Camera)).GetComponent<Camera>();
        uiCamera.transform.position = new Vector3(0, 0, -10);
        uiCamera.clearFlags = CameraClearFlags.SolidColor;
        uiCamera.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
        renderTarget = new RenderTexture(1024, 768, 24); uiCamera.targetTexture = renderTarget;
        canvas = new GameObject("HelperFixtureOnly", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<RectTransform>();
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceCamera;
        canvas.GetComponent<Canvas>().worldCamera = uiCamera;
        canvas.GetComponent<Canvas>().planeDistance = 1;
        root = new GameObject("OfficialHelperBindingsFixture", typeof(RectTransform));
        root.SetActive(false);
        root.transform.SetParent(canvas, false);
        var rect = (RectTransform)root.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
        var panel = root.AddComponent<FilterSortPanel>();
        panel.enabled = false;
        panel.SortGroups = new List<BaseSortGroup>();
        panel.FilterGroups = new List<BaseFilterGroup>();
        panel.SwitchGroups = new List<BaseSwitchGroup>();
        panel.FilterSortDialog = Node("Dialog", root.transform, 0, 100);
        panel.FilterSortDialog.SetActive(false);
        panel.StateSwitcher = State<int>(root, panel.FilterSortDialog);
        Set(panel, "mIsFiltingSwitcher", WorldStateSwitcher<bool>.FromBindings(root, new IWorldStateConfig[0]));
        panel.SwitchBtn = Button("Open", root.transform, -180, 210);
        panel.CloseBtn = Button("Close", root.transform, 180, 210);
        panel.ResetFilterBtn = Button("Reset", root.transform, 0, -210);
        var groupObject = Node("Sort", root.transform, 0, 0);
        var tabs = groupObject.AddComponent<WorldTabGroup>();
        var sort = groupObject.AddComponent<CollectSortGroup>();
        sort.ToggleGroup = tabs;
        panel.SortGroups.Add(sort);
        var first = SortItem(groupObject.transform, tabs, CollectSortType.Friendship, -180);
        var second = SortItem(groupObject.transform, tabs, CollectSortType.ActivateTime, 180);
        first.GetComponent<WorldTabItem>().SetIsOnWithoutNotify(true);
        var qualityObject = Node("Quality", root.transform, 0, -100);
        var quality = qualityObject.AddComponent<FilterGroupForQuality>();
        quality.ItemContainer = qualityObject.transform;
        panel.FilterGroups.Add(quality);
        var white = FilterItem(qualityObject.transform, Quality.White, -180);
        var blue = FilterItem(qualityObject.transform, Quality.Blue, 180);
        quality.InitFilterDefault(new List<Quality> { Quality.Blue });
        var callbackOrder = new List<string>();
        panel.OnSortOptionChanged += (type, on, reverse) => callbackOrder.Add(type + ":" + on + ":" + reverse);
        int filterChanges = 0, opened = 0;
        panel.OnFilterOptionChanged += result => filterChanges++;
        panel.OnFilterSortPanelShow += () => opened++;
        root.SetActive(true);
        yield return null;
        panel.enabled = true;
        yield return null;
        Debug.Log("PARTNER_UI_HELPERS: initialized");
        Require(!panel.FilterSortDialog.activeSelf, "Filter dialog initially closed");
        Require(blue.IsSelected && !white.IsSelected && panel.FilterResult[CommonFilterType.Quality].Contains(Quality.Blue), "Original default selection/reset");
        Click(panel.SwitchBtn.gameObject);
        yield return null;
        Require(panel.FilterSortDialog.activeSelf && opened == 1, "Simulated open button");
        Click(white.Buton.gameObject);
        yield return null;
        Require(white.IsSelected && panel.FilterResult[CommonFilterType.Quality].Count == 2 && filterChanges == 1, "First filter selection forwards once");
        Click(white.Buton.gameObject);
        yield return null;
        Require(!white.IsSelected && panel.FilterResult[CommonFilterType.Quality].Count == 1 && filterChanges == 2, "Second click removes only selected filter");
        Click(first.ReverseBtn.gameObject);
        yield return null;
        Require(first.IsReverse && callbackOrder[0] == "Friendship:True:True", "Reverse forwards selected sort");
        Click(second.gameObject);
        yield return null;
        Require(callbackOrder.Count == 3 && callbackOrder[1] == "Friendship:False:False" && callbackOrder[2] == "ActivateTime:True:False", "Original old-off/new-on ordering and reverse reset");
        Click(panel.CloseBtn.gameObject);
        yield return null;
        Require(!panel.FilterSortDialog.activeSelf, "Simulated close button");
        root.SetActive(false);
        panel.SelecteFilterGroup(CommonFilterType.Quality, new HashSet<object> { Quality.White });
        root.SetActive(true);
        yield return null;
        Require(white.IsSelected && !blue.IsSelected && panel.FilterResult[CommonFilterType.Quality].SetEquals(new object[] { Quality.White, Quality.Blue }) && panel.CacheSelecteParam.Count == 0,
            "Original cached visuals replace selection while result adds to retained values; cache clears");
        Click(panel.ResetFilterBtn.gameObject);
        yield return null;
        Require(blue.IsSelected && !white.IsSelected && panel.FilterResult[CommonFilterType.Quality].SetEquals(new object[] { Quality.Blue }), "Reset returns original defaults");
        Debug.Log("PARTNER_UI_HELPERS: simulated filter/sort input passed");

        var seqObject = new GameObject("OfficialListSequence");
        seqObject.SetActive(false);
        var sequence = seqObject.AddComponent<ListItemShowSequence>();
        Set(sequence, "mIsAutoShow", false); Set(sequence, "mShowInterval", 0.03f); Set(sequence, "mFirstShowDelay", 0.08f);
        seqObject.SetActive(true);
        var order = new List<int>();
        var a = new SequenceItem(1, order); var b = new SequenceItem(2, order);
        sequence.AddItem(a); sequence.AddItem(b); sequence.AddItem(a);
        Require(a.Hidden == 2 && b.Hidden == 1, "Duplicate queued cards hide and move to tail");
        sequence.StartShow();
        yield return new WaitForSeconds(0.05f);
        Require(order.Count == 0, "First-show delay retained");
        yield return new WaitForSeconds(0.2f);
        Require(order.Count == 2 && order[0] == 2 && order[1] == 1 && !sequence.CanShow(), "Original queue ordering without duplicate show");
        sequence.AddItem(a); sequence.StartShow(); sequence.StopShow();
        yield return new WaitForSeconds(0.1f);
        Require(sequence.CanShow() && order.Count == 2, "Stop retains pending queue");
        seqObject.SetActive(false); seqObject.SetActive(true);
        Require(!sequence.CanShow(), "Disable clears queue and coroutine");
        sequence.AddItem(b); sequence.RemoveItem(b);
        Require(!sequence.CanShow(), "Remove queued card");
        Canvas.ForceUpdateCanvases();
        uiCamera.Render();
        RenderTexture previous = RenderTexture.active; RenderTexture.active = renderTarget;
        var screenshot = new Texture2D(renderTarget.width, renderTarget.height, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, renderTarget.width, renderTarget.height), 0, 0); screenshot.Apply();
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "helper-fixture.png"), screenshot.EncodeToPNG());
        RenderTexture.active = previous; Object.Destroy(screenshot);
        yield return null;
        Application.logMessageReceived -= Log;
    }

    private static WorldStateSwitcher<T> State<T>(GameObject owner, GameObject target) where T : struct, IConvertible
        => WorldStateSwitcher<T>.FromBindings(owner, new IWorldStateConfig[] { WorldStateGameObjectConfig.FromBindings(new WorldStateGameObjectData {
            GoConfig = new List<WorldStateGameObjectData.StateGameObjectKV> {
                new WorldStateGameObjectData.StateGameObjectKV { State = 0, GameObjects = new List<GameObject>() },
                new WorldStateGameObjectData.StateGameObjectKV { State = 1, GameObjects = new List<GameObject> { target } } } }) });

    private CollectSortGroupItem SortItem(Transform parent, WorldTabGroup group, CollectSortType type, float x)
    {
        var item = Node(type.ToString(), parent, x, 0);
        item.AddComponent<Image>().color = new Color(0.3f, 0.6f, 0.8f);
        var tab = item.AddComponent<WorldTabItem>(); tab.SetGroup(group);
        item.AddComponent<MultiTextsControler>().MultiTextList = new List<WorldMultiLanguageText>();
        var sort = item.AddComponent<CollectSortGroupItem>(); sort.SortType = type;
        sort.ReverseStateSwitch = WorldStateSwitcher<bool>.FromBindings(item, new IWorldStateConfig[0]);
        sort.ReverseBtn = Button("Reverse", item.transform, 0, 50);
        return sort;
    }

    private SwitchShowBtn FilterItem(Transform parent, Quality type, float x)
    {
        var button = Button(type.ToString(), parent, x, 0);
        var item = button.gameObject.AddComponent<FilterGroupItemForRank>(); item.FilterType = type;
        var state = button.gameObject.AddComponent<SwitchShowBtn>();
        state.Buton = button; state.MultiTextList = new List<WorldMultiLanguageText>();
        state.SwitchShowList = new List<GameObject> { Node("Normal", button.transform, 0, 0), Node("Selected", button.transform, 0, 0) };
        return state;
    }

    private static GameObject Node(string name, Transform parent, float x, float y)
    {
        var node = new GameObject(name, typeof(RectTransform)); node.transform.SetParent(parent, false);
        var rect = (RectTransform)node.transform; rect.sizeDelta = new Vector2(130, 38); rect.anchoredPosition = new Vector2(x, y);
        return node;
    }
    private static Button Button(string name, Transform parent, float x, float y)
    {
        var node = Node(name, parent, x, y); node.AddComponent<Image>().color = new Color(0.7f, 0.5f, 0.3f); return node.AddComponent<Button>();
    }

    private void Click(GameObject target)
    {
        Canvas.ForceUpdateCanvases();
        var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left,
            position = RectTransformUtility.WorldToScreenPoint(uiCamera, target.transform.position) };
        var hits = new List<RaycastResult>(); events.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && (hits[0].gameObject == target || hits[0].gameObject.transform.IsChildOf(target.transform)), "Raycast reaches " + target.name);
        pointer.pointerCurrentRaycast = hits[0];
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
    private void Log(string message, string stack, LogType type) { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) failures++; }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    private static void Require(bool pass, string message) { if (!pass) throw new InvalidOperationException(message); }
    private sealed class SequenceItem : IShowSequenceItem
    {
        private readonly int id; private readonly List<int> order; public int Hidden;
        public SequenceItem(int id, List<int> order) { this.id = id; this.order = order; }
        public void Hide() { Hidden++; } public void Show() { order.Add(id); }
    }
}
