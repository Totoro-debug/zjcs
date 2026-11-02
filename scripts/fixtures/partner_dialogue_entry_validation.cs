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
using Zjcs.Offline.PartnersUI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PartnerDialogueEntryValidation
{
    const string Key = "PartnerDialogueEntryValidation.Active";
    static PartnerDialogueEntryValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                new GameObject("Dialogue entry fixture").AddComponent<PartnerDialogueEntryRunner>();
        };
        EditorApplication.update += () => {
            if (SessionState.GetBool(Key, false) && EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".Deadline", float.MaxValue))
                Finish(new TimeoutException("Dialogue fixture did not finish"));
        };
    }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + ".Deadline", (float)EditorApplication.timeSinceStartup + 120);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false);
        string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        File.WriteAllText(Path.Combine(output, "dialogue-entry-result.json"), JsonUtility.ToJson(new Result { success = error == null, error = error?.ToString() }, true));
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    [Serializable] sealed class Result { public bool success; public string error; }
}

public sealed class PartnerDialogueEntryRunner : MonoBehaviour, IPartnerDialogueUiServices, IProfessionSoundHost
{
    [Serializable] sealed class TextData { public int id; public string key, text; }
    TextData text;
    int errors, sounds, popups, listeners;
    bool unlocked = true;
    Camera cameraUi;
    RenderTexture target;
    EventSystem events;
    PartnerDialogueData expected;
    Action<PartnerDialogueData> update;
    AudioClip click;
    public bool MainPlayerAvailable => true;
    public string PlayerName => "冒险者";
    public int DialogueStartNodeId => 1;
    public IReadOnlyDictionary<long, PartnerDialogueNpcData> Npcs { get; } = new Dictionary<long, PartnerDialogueNpcData>();
    public IReadOnlyDictionary<long, PartnerDialogueData> Dialogues { get; } = new Dictionary<long, PartnerDialogueData>();
    public event Action<PartnerDialogueData> DialogueAdded { add { } remove { } }
    public event Action<PartnerDialogueData> DialogueRemoved { add { } remove { } }
    public event Action<PartnerDialogueData> DialogueUpdated { add { update += value; listeners++; } remove { update -= value; listeners--; } }
    public IReadOnlyList<int> GetNextNodes(int dialogue, int node) => node == 1 ? new[] { 2 } : Array.Empty<int>();
    public bool IsNodeUnlocked(int dialogue, int node) => unlocked;
    public GameObject Instantiate(GameObject prefab, Transform parent, bool resetTransformOnly) => throw new NotSupportedException("Entry fixture does not create list items");
    public void InitializeNpcIcon(Component icon, PartnerDialogueNpcData npc) => throw new NotSupportedException("Entry fixture has no portrait");
    public void ShowError(int code) => throw new InvalidOperationException("Unexpected dialogue error " + code);
    public void ShowDialogue(PartnerDialogueData value) { Require(ReferenceEquals(expected, value), "Clicked entry retains exact dialogue identity"); popups++; }
    public uint PostEvent(uint id, GameObject emitter)
    {
        Require(id == 3772241779, "Original dialogue click event identity");
        var source = emitter.AddComponent<AudioSource>(); source.playOnAwake = false; source.clip = click; source.Play(); sounds++; return (uint)sounds;
    }
    void Start() { StartCoroutine(Guard(Verify())); }
    IEnumerator Guard(IEnumerator steps)
    {
        while (true)
        {
            object current;
            try { if (!steps.MoveNext()) break; current = steps.Current; }
            catch (Exception error) { PartnerDialogueEntryValidation.Finish(error); yield break; }
            yield return current;
        }
        PartnerDialogueEntryValidation.Finish(null);
    }
    IEnumerator Verify()
    {
        Application.logMessageReceived += Observe;
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        text = JsonUtility.FromJson<TextData>(File.ReadAllText("Assets/Editor/dialogue-text.json"));
        var font = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>("Assets/Editor/FZY4K.ttf"));
        WorldMultiLanguageText.LanguageFont = () => font;
        WorldMultiLanguageText.Localize = key => key == text.key ? text.text : key;
        cameraUi = new GameObject("Original entry viewport", typeof(Camera)).GetComponent<Camera>();
        cameraUi.orthographic = true; cameraUi.transform.position = new Vector3(0, 0, -10); cameraUi.clearFlags = CameraClearFlags.SolidColor;
        cameraUi.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
        target = new RenderTexture(450, 800, 24); target.Create(); cameraUi.targetTexture = target;
        var canvas = new GameObject("Entry viewport canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cameraUi; canvas.planeDistance = 1;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 1920); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        events = new GameObject("Simulated mouse", typeof(EventSystem)).GetComponent<EventSystem>();
        var staging = new GameObject("Inactive injection", typeof(RectTransform)); staging.SetActive(false);
        var layout = new GameObject("Original dialogue parent layout", typeof(RectTransform), typeof(VerticalLayoutGroup));
        layout.transform.SetParent(canvas.transform, false);
        var layoutRect = layout.GetComponent<RectTransform>(); layoutRect.anchorMin = layoutRect.anchorMax = new Vector2(0.5f, 0.5f); layoutRect.sizeDelta = new Vector2(1080, 100);
        EditorJsonUtility.FromJsonOverwrite(File.ReadAllText("Assets/Editor/dialogue-parent-layout.json"), layout.GetComponent<VerticalLayoutGroup>());
        var catalog = OfficialPartnerDialogueAssetCatalog.LoadDefault();
        var prefab = catalog.EntryPrefab;
        Require(catalog && prefab, "Installed original Resources catalog resolves exact entry prefab");
        Require(prefab, "Recovered original 66-object dialogue entry prefab imports");
        var root = Object.Instantiate(prefab, staging.transform, false);
        var entry = root.GetComponent<NpcPanelDialogueListDialogueItem>(); entry.Configure(this);
        root.SetActive(false); root.transform.SetParent(layout.transform, false); root.SetActive(true);
        click = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Official/Partners" }).Select(id => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(id))).Single();
        foreach (var sound in root.GetComponentsInChildren<WorldButtonSound>(true))
            sound.Post = (clip, emitter) => { Require(clip == click, "Original button sound retains verified PCM binding"); PostEvent(3772241779, emitter); };
        OfficialWwiseEvent.Host = this;
        expected = new PartnerDialogueData { NpcId = 42, DialogueId = 99, DialogueClassId = text.id, CurrentNode = 1, DateTime = DateTime.UtcNow };
        entry.Init(expected, true); Require(listeners == 1, "Original initialized enable subscribes once");
        entry.Show(); yield return new WaitForSeconds(0.3f);
        var labels = Get<List<WorldMultiLanguageText>>(entry, "mTexts");
        Require(labels.All(label => label.Text == text.text), "Original dialogue start-node language key and text");
        Require(Get<GameObject>(entry, "mRedDot").activeSelf, "One unlocked next node shows red dot");
        Capture(Path.Combine(work, "dialogue-entry-in-progress.png"));
        var button = Get<Button>(entry, "mButton"); Click(button.gameObject); yield return null;
        Require(popups == 1 && sounds == 1, "Original pointer click opens exact dialogue and plays original PCM");
        unlocked = false; expected.DialogueStatus = PartnerDialogueStatus.Pause; update(expected); yield return null;
        Require(!Get<GameObject>(entry, "mRedDot").activeSelf && Get<WorldStateSwitcher<PartnerDialogueStatus>>(entry, "mStateSwitcher").State == PartnerDialogueStatus.Pause, "Locked continuation removes red dot and selects original pause state");
        expected.CurrentNode = 2; expected.DialogueStatus = PartnerDialogueStatus.End; update(expected); yield return null;
        Require(!Get<GameObject>(entry, "mRedDot").activeSelf, "Terminal dialogue has no continuation dot");
        Capture(Path.Combine(work, "dialogue-entry-ended.png"));
        root.SetActive(false); Require(listeners == 0, "Disable releases update subscription");
        root.SetActive(true); Require(listeners == 1, "Re-enable subscribes once");
        Click(button.gameObject); yield return null; Require(popups == 2 && sounds == 2, "Recycled entry has exactly one click listener");
        entry.Hide(); Require(!Get<GameObject>(entry, "mContentRoot").activeSelf, "Original sequence hide only hides content root");
        entry.Show(); yield return null;
        var newer = new PartnerDialogueData { DialogueId = 100, DateTime = expected.DateTime.AddMinutes(1), DialogueStatus = PartnerDialogueStatus.End };
        Require(PartnerDialogueListRules.CompareDialogues(expected, newer) > 0, "Ended dialogue order uses newest time first");
        newer.DialogueStatus = PartnerDialogueStatus.InProgress;
        Require(PartnerDialogueListRules.CompareDialogues(newer, expected) < 0, "Dialogue status precedes time");
        Require(PartnerDialogueListRules.Compare(long.MaxValue, -1, ComparerOrderType.Asc) < 0, "Retain original unchecked subtraction boundary");
        Require(new PartnerDialoguePlayerNameProcessor("冒险者").Process("<PN>、<PN>") == "冒险者、冒险者", "Original player-name placeholder replacement");
        root.SetActive(false); Require(listeners == 0 && errors == 0, "Final release and no runtime errors");
        Application.logMessageReceived -= Observe;
    }
    static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); Debug.Log("PASS " + message); }
    void Observe(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
    void Click(GameObject go)
    {
        Canvas.ForceUpdateCanvases(); var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left, position = RectTransformUtility.WorldToScreenPoint(cameraUi, go.transform.position) };
        var hits = new List<RaycastResult>(); events.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && (hits[0].gameObject == go || hits[0].gameObject.transform.IsChildOf(go.transform)), "Actual pointer raycast reaches dialogue button");
        pointer.pointerCurrentRaycast = hits[0]; ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler); ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }
    void Capture(string path)
    {
        Canvas.ForceUpdateCanvases(); cameraUi.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
        var image = new Texture2D(450, 800, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 450, 800), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); RenderTexture.active = previous; Object.Destroy(image);
    }
}
