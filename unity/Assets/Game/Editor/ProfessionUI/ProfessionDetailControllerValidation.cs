using System;
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
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using SimulatedMouse = Zjcs.Editor.ProfessionUI.ProfessionItemIconValidation.SimulatedMouse;

namespace Zjcs.Editor.ProfessionUI
{
    // Actual controller lifecycle/input checks with explicit service and child
    // panel fixtures. These controls are not the original detail-window layout.
    [InitializeOnLoad]
    public static class ProfessionDetailControllerValidation
    {
        private const string Active = "Zjcs.ProfessionDetailControllers.Active";
        private static bool ready, initialized;
        private static int frame, phase, cycle;
        private static GameObject root;
        private static ProfessionPanel panel;
        private static ProfessionChangeJumpBtn jump;
        private static Button jumpButton;
        private static InfoEndpoint info;
        private static UpgradeEndpoint upgrade;
        private static State state;
        private static Context context;
        private static Camera camera;
        private static RenderTexture target;
        private static EventSystem events;
        private static SimulatedMouse mouse;
        private static GameObject clickTarget;
        public sealed class InfoEndpoint : MonoBehaviour, IProfessionDetailInfoPanel
        {
            public int Count, Profession;
            public GameObjectPool<ProfessionPlayerPropItem> Pool;
            public void Init(int profession, GameObjectPool<ProfessionPlayerPropItem> pool)
            { Count++; Profession = profession; Pool = pool; Require(state.Value == 0, "state precedes info initialization"); }
        }
        public sealed class UpgradeEndpoint : MonoBehaviour, IProfessionDetailUpgradePanel
        {
            public int Count, Profession;
            public void Init(int profession)
            { Count++; Profession = profession; Require(state.Value == 1 || state.Value == 2, "state precedes upgrade initialization"); }
        }
        private sealed class State : IWorldStateConfig
        {
            public int Value;
            public readonly List<int> Values = new List<int>();
            public void SetState(WorldStateConfigValue value) { Value = value; Values.Add(value); }
            public void Clear() { }
        }
        private sealed class Skill : IProfessionDetailSkill
        {
            public int Id { get; set; }
            public int Rank;
            public int GetRank() => Rank;
        }
        private sealed class Handle<T> : IProfessionAssetOperation<T> where T : Object
        {
            public T Result { get; set; }
            public bool IsValid => !Disposed;
            public int Status => 2;
            public bool Disposed;
            public int Disposals;
            public void Dispose() { Disposed = true; Disposals++; }
            public event Action<IProfessionAssetOperation<T>> Completed { add { value(this); } remove { } }
        }
        private sealed class Context : IProfessionDetailBindings, IProfessionDetailPlayer, IProfessionImageResourceLoader
        {
            public IProfessionDetailPlayer MainPlayer => this;
            public IProfessionImageResourceLoader ResourceLoader => this;
            public int Profession => 10;
            public int HighestRank => 3;
            public int Level = 3, Subscribers, Claims, Popups, MaxLevel = 5;
            private Action<int, int> upgraded;
            public event Action<int, int> OnProfessionUpgrade
            { add { upgraded += value; Subscribers++; } remove { upgraded -= value; Subscribers--; } }
            public void Emit(int profession, int level) { if (profession == 10) Level = level; upgraded?.Invoke(profession, level); }
            public readonly List<string> Calls = new List<string>();
            public readonly List<Handle<Sprite>> Handles = new List<Handle<Sprite>>();
            public readonly HashSet<int> Open = new HashSet<int> { 10, 11 };
            public readonly Dictionary<int, int> Last = new Dictionary<int, int> { [101] = 1, [102] = 2, [103] = 1, [999] = 0 };
            public readonly List<IProfessionDetailSkill> Skills = new List<IProfessionDetailSkill>
            { new Skill { Id = 101, Rank = 3 }, new Skill { Id = 999, Rank = 9 }, new Skill { Id = 102, Rank = 3 }, new Skill { Id = 103, Rank = 1 } };
            public Sprite Male, Female;
            public int[] Claimed;
            public (int itemId, int rank)[] Rows;
            public Action<Dictionary<int, int>> Pending;
            public int GetProfessionLevel(int profession) => Level;
            public int GetLastRewardedSkillRank(int skill) => Last[skill];
            public IReadOnlyCollection<IProfessionDetailSkill> FindSkillItems() => Skills;
            public ProfessionDetailDefinition GetProfession(int profession)
            {
                Calls.Add("definition:" + profession);
                return new ProfessionDetailDefinition { Profession = profession, Rank = profession == 0 ? 0 : profession - 9,
                    PrePro = profession == 10 ? 0 : profession - 1, BadgeClassId = 700,
                    Skills = new List<int> { 101, 102, 103 }, TutorEntityTag = "tutor-" + profession };
            }
            public List<int> GetSuccessor(int profession)
            { Calls.Add("successor:" + profession); return profession == 10 ? new List<int> { 11, 99 } : profession == 11 ? new List<int> { 12 } : null; }
            public string GetSecondIconPath(int profession) => "icon/" + profession;
            public string GetFullBodyGuid(int profession, Sex sex) => "body/" + sex;
            public string GetProfessionNameKey(int profession) => "profession-" + profession;
            public Color GetPanelFadeColor(int profession) => new Color(.3f, .4f, .5f, .6f);
            public Color GetPanelLineColor(int profession) => new Color(.8f, .7f, .6f, .5f);
            public bool HasLevelCost(int profession, int level) { Calls.Add("cost:" + profession + ":" + level); return level <= MaxLevel; }
            public bool IsProfessionTutorUnlock(int profession) { Calls.Add("open:" + profession); return Open.Contains(profession); }
            public string GetTutorHeadGuid(int profession) => "head/" + profession;
            public bool JumpSucceeds = true;
            public bool TryGotoTutor(string source) { Calls.Add("jump:" + source); return JumpSucceeds; }
            public void ShowSystemMessage(string key) => Calls.Add("message:" + key);
            public int GetRankBadge(int rank) => new[] { 0, 10, 30, 60 }[rank];
            public void ClaimSkillRankReward(List<int> skills, Action<Dictionary<int, int>> success)
            { Claims++; Claimed = skills.ToArray(); Pending = success; }
            public void CompleteClaim()
            { foreach (int id in Claimed) Last[id] = Skills.First(s => s.Id == id).GetRank(); var callback = Pending; Pending = null; callback(new Dictionary<int, int>()); }
            public void PopupSkillBadge(UIBase container, List<(int itemId, int rank)> rows, int badge, int amount)
            { Require(container == panel && badge == 700 && amount == 80, "original popup owner, badge and cumulative rank difference"); Popups++; Rows = rows.ToArray(); }
            public IProfessionAssetOperation<T> LoadAsset<T>(string path) where T : Object => Load<T>(path);
            public IProfessionAssetOperation<T> LoadAssetAsync<T>(string path) where T : Object => throw new InvalidOperationException("Unexpected async controller request");
            public IProfessionAssetOperation<T> LoadAssetByGuid<T>(string guid) where T : Object => Load<T>(guid);
            public IProfessionAssetOperation<T> LoadAssetAsyncByGuid<T>(string guid) where T : Object => throw new InvalidOperationException("Unexpected async controller request");
            private IProfessionAssetOperation<T> Load<T>(string key) where T : Object
            {
                Require(typeof(T) == typeof(Sprite), "source requests only sprites"); Calls.Add("load:" + key);
                var handle = new Handle<Sprite> { Result = key == "body/" + Sex.Female ? Female : Male };
                Handles.Add(handle); return (IProfessionAssetOperation<T>)(object)handle;
            }
        }
        static ProfessionDetailControllerValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredPlayMode) ready = true; };
        }
        public static void Run()
        {
            try
            {
                ProfessionValidationWorkspace.AssertIsolatedProject();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
                Application.runInBackground = true; EditorApplication.isPaused = false;
                SessionState.SetBool(Active, true);
                SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 180));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Active + ".Deadline", 0)) throw new TimeoutException("Detail controller validation timed out");
                if (!ready || !EditorApplication.isPlaying) return;
                if (!initialized) { Initialize(); return; }
                if (Time.frameCount <= frame + (phase == 0 ? 3 : 0)) return;
                frame = Time.frameCount;
                if (phase == 0)
                {
                    Canvas.ForceUpdateCanvases(); mouse.Point = RectTransformUtility.WorldToScreenPoint(camera, clickTarget.transform.position);
                    var hits = new List<RaycastResult>(); events.RaycastAll(new PointerEventData(events) { position = mouse.Point }, hits);
                    Require(hits.Count > 0 && hits[0].gameObject == clickTarget, "unoccluded simulated pointer reaches actual tab/button control");
                    if (cycle == 0) Capture();
                    phase = 1;
                }
                else if (phase == 1) { mouse.Down = mouse.Held = true; phase = 2; }
                else if (phase == 2) { mouse.Down = mouse.Held = false; mouse.Up = true; phase = 3; }
                else
                {
                    mouse.Up = false; CheckClick(); cycle++; phase = 0;
                }
            }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        private static void Initialize()
        {
            initialized = true; frame = Time.frameCount;
            context = new Context
            {
                Male = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Official/Character/Art/sprites/ProfessionPortrait_3ecf1ed67553-627d3a309f.png"),
                Female = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Official/Character/Art/sprites/ProfessionPortrait_c3e3bd683cc4-02b495919b.png")
            };
            Require(context.Male && context.Female && context.Male != context.Female, "two real original portrait assets");
            camera = new GameObject("DetailControllerFixtureCamera").AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 128;
            camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture = target;
            var canvas = new GameObject("DetailControllerFixtures", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            root = new GameObject("OriginalDetailControllerWithServiceFixtures", typeof(RectTransform)); root.SetActive(false); root.transform.SetParent(canvas.transform, false);
            panel = root.AddComponent<ProfessionPanel>(); panel.Configure(context);
            panel.ToggleGroup = Child("Tabs", root.transform).AddComponent<WorldTabGroup>();
            panel.InfoToggle = Tab("Info", -45, panel.ToggleGroup); panel.UpgradeToggle = Tab("Upgrade", 45, panel.ToggleGroup);
            panel.InfoToggle.SetIsOnWithoutNotify(true);
            panel.PropItemPrefab = Child("UnusedPropPoolPrefab", root.transform); panel.PropItemPrefab.AddComponent<ProfessionPlayerPropItem>(); panel.PropItemPrefab.SetActive(false);
            panel.FrontProfessionIcon = HiddenImage("FrontIcon"); panel.BackProfessionIcon = HiddenImage("BackIcon");
            panel.MaleRole = Locator("Male"); panel.FemaleRole = Locator("Female");
            panel.BgImg = HiddenImage("Background"); panel.SplitLineImg = HiddenImage("Line");
            panel.LevelText = Label("Level", new Vector2(-30, 50), "{0}"); panel.NextLevelText = Label("NextLevel", new Vector2(30, 50), "{0}");
            panel.ProfessionTitle = Label("Title", new Vector2(0, 85), "profession");
            info = Child("InfoEndpointFixture", root.transform).AddComponent<InfoEndpoint>(); panel.InfoPanel = info;
            upgrade = Child("UpgradeEndpointFixture", root.transform).AddComponent<UpgradeEndpoint>(); panel.UpgradePanel = upgrade;
            var tipGo = Child("TipEndpointFixture", root.transform); var tip = tipGo.AddComponent<TipDotNodeLinker>(); tip.enabled = false;
            Set(tip, "mGraphNodeSelector", new TipDotGraphNodeSelector()); panel.TipDotLinker = tip;
            state = new State(); var field = typeof(ProfessionPanel).GetField("mStateSwitcher", BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(panel, field.FieldType.GetMethod("FromBindings", BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[] { root, new IWorldStateConfig[] { state } }));
            var jumpRoot = Child("JumpControllerFixture", root.transform); jump = jumpRoot.AddComponent<ProfessionChangeJumpBtn>(); jump.Configure(context);
            jumpButton = Button("Jump", jumpRoot.transform, new Vector2(0, -55), new Vector2(110, 32));
            Set(jump, "mJumpBtn", jumpButton); Set(jump, "mTutorHeadImg", HiddenImage("TutorHead")); Set(panel, "mJumpBtn", jump);
            root.SetActive(true); Require(context.Handles.Count == 0 && context.Subscribers == 0, "original UIBase waits for Init");
            panel.Init(10);
            Require(info.Count == 1 && info.Profession == 10 && info.Pool != null && state.Value == 0 && context.Subscribers == 1, "initial info reset and original shared property pool");
            Require(panel.FrontProfessionIcon.sprite == context.Male && panel.BackProfessionIcon.sprite == context.Male && panel.MaleRole.Image.sprite == context.Male && panel.FemaleRole.Image.sprite == context.Female, "original path/GUID sprite assignments");
            Require(panel.LevelText.Text == "3" && panel.NextLevelText.Text == "4" && panel.ProfessionTitle.Text == "profession-10" && panel.BgImg.color == context.GetPanelFadeColor(10) && panel.SplitLineImg.color == context.GetPanelLineColor(10), "source level/name/settings presentation");
            Require(!jumpRoot.activeSelf && context.Claims == 1 && context.Popups == 0 && context.Claimed.SequenceEqual(new[] { 101, 102 }), "current profession hides jump and badge popup waits for success");
            context.CompleteClaim(); Require(context.Popups == 1 && context.Rows.SequenceEqual(new[] { (102, 3), (101, 3), (101, 2) }), "descending rank/item sort and all unclaimed ranks");
            context.Emit(11, 7); Require(panel.LevelText.Text == "3", "unrelated profession upgrade is ignored");
            foreach (var previous in Object.FindObjectsOfType<EventSystem>(true)) { previous.enabled = false; foreach (var module in previous.GetComponents<BaseInputModule>()) module.enabled = false; }
            var eventGo = new GameObject("DetailControllerFixtureEvents", typeof(EventSystem), typeof(StandaloneInputModule)); events = eventGo.GetComponent<EventSystem>();
            mouse = eventGo.AddComponent<SimulatedMouse>(); var input = eventGo.GetComponent<StandaloneInputModule>(); input.inputOverride = mouse; input.forceModuleActive = true;
            clickTarget = panel.UpgradeToggle.gameObject;
        }
        private static void CheckClick()
        {
            if (cycle == 0)
            {
                Require(state.Value == 1 && upgrade.Count == 1 && upgrade.Profession == 10, "simulated upgrade tab selects normal state exactly once");
                context.Emit(10, 5);
                Require(state.Value == 2 && upgrade.Count == 2 && panel.LevelText.Text == "5" && panel.NextLevelText.Text == "6", "matching max-level event changes state before updating labels");
                clickTarget = panel.InfoToggle.gameObject;
            }
            else if (cycle == 1)
            {
                Require(state.Value == 0 && info.Count == 2, "simulated info tab returns to original info state");
                var initial = context.Handles.Take(3).ToArray(); root.SetActive(false);
                Require(context.Subscribers == 0 && initial.All(h => h.Disposals == 1) && context.Handles[3].Disposals == 0, "disable releases exactly window handles while tutor handle lives to destruction");
                context.Emit(10, 4); Require(panel.LevelText.Text == "5", "disabled window is unsubscribed");
                root.SetActive(true);
                Require(context.Subscribers == 1 && info.Count == 3 && context.Claims == 1 && panel.LevelText.Text == "4", "reenable reinitializes, resubscribes once and does not reclaim badge");
                panel.Init(11); Require(context.Subscribers == 1 && info.Profession == 11 && jump.gameObject.activeSelf && context.Claims == 1, "active InitWrapper cycles old lifetime and updates alternate profession jump");
                context.Calls.Clear(); jump.SetProfession(10);
                Require(context.Calls.SequenceEqual(new[] { "definition:10", "successor:10", "open:11", "definition:11", "successor:11", "open:12", "open:11", "load:head/11" }), "original first-successor traversal stops at highest unlocked tutor");
                clickTarget = jumpButton.gameObject;
            }
            else if (cycle == 2)
            {
                Require(context.Calls.Last() == "jump:tutor-11", "simulated tutor button routes exact original entity tag");
                context.Open.Remove(11); context.Calls.Clear(); jump.SetProfession(11);
                Require(context.Calls.SequenceEqual(new[] { "definition:11", "successor:11", "open:12", "open:11", "definition:10", "successor:10", "open:11", "open:10", "load:head/10" }), "unavailable current tutor follows original predecessor branch");
                context.JumpSucceeds = false;
            }
            else if (cycle == 3)
            {
                Require(context.Calls.TakeLast(3).SequenceEqual(new[] { "definition:10", "jump:tutor-10", "message:ui_profession_tutor_not_found" }), "failed simulated jump shows original key");
                context.Calls.Clear(); jump.SetProfession(0); context.Calls.Clear();
            }
            else
            {
                Require(context.Calls.SequenceEqual(new[] { "definition:0", "message:ui_profession_tutor_not_found" }), "None tutor short-circuits map movement");
                root.SetActive(false); Require(context.Subscribers == 0, "final window unsubscribe");
                Object.DestroyImmediate(root);
                Require(context.Handles.All(h => h.Disposals == 1), "all retained portrait/icon/tutor requests disposed exactly once");
                Debug.Log("Original profession detail controllers Play Mode passed: UIBase init/active reinit/re-enable, source three states, shared property pool, exact sprite/color/level assignment and release ownership; simulated info/upgrade/tutor clicks, max-level event and unrelated/disabled filtering; deferred-success badge difference 80, descending rows, no duplicate claim; first successor, predecessor and None/failure tutor branches. Service/child-panel controls are fixtures, not the original detail layout or a complete profession page.");
                Finish(0);
            }
        }
        private static GameObject Child(string name, Transform parent)
        { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return go; }
        private static Button Button(string name, Transform parent, Vector2 position, Vector2 size)
        { var go = Child(name, parent); var image = go.AddComponent<Image>(); image.color = new Color(.8f, .83f, .24f); var rect = (RectTransform)go.transform; rect.anchoredPosition = position; rect.sizeDelta = size; return go.AddComponent<Button>(); }
        private static WorldTabItem Tab(string name, float x, WorldTabGroup group)
        { var go = Child(name, group.transform); var image = go.AddComponent<Image>(); image.color = new Color(.8f, .83f, .24f); var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(70, 30); rect.anchoredPosition = new Vector2(x, 0); var item = go.AddComponent<WorldTabItem>(); item.targetGraphic = image; item.SetGroup(group); return item; }
        private static Image HiddenImage(string name)
        { var go = Child(name, root.transform); var image = go.AddComponent<Image>(); image.raycastTarget = false; go.SetActive(false); return image; }
        private static ImageLocator Locator(string name)
        { var image = HiddenImage(name); var locator = image.gameObject.AddComponent<ImageLocator>(); Set(locator, "mImage", image); return locator; }
        private static WorldMultiLanguageText Label(string name, Vector2 position, string key)
        {
            var go = Child(name, root.transform); var text = go.AddComponent<TextMeshProUGUI>(); text.raycastTarget = false; text.fontSize = 16; text.alignment = TextAlignmentOptions.Center;
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Official/ProfessionUI/Native/TextMesh Pro/Assets/Fonts & Materials/FZY4K SDF - dynamic.asset");
            ((RectTransform)go.transform).anchoredPosition = position; ((RectTransform)go.transform).sizeDelta = new Vector2(160, 30);
            var label = go.AddComponent<WorldMultiLanguageText>(); label.Localizer = k => k; label.myKey = key; return label;
        }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void Capture()
        {
            camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(512, 512, TextureFormat.RGBA32, false, true); image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply(); RenderTexture.active = previous;
            Require(image.GetPixels32().Count(p => p.r > 100 && p.g > 100) > 1000, "control fixture renders on the GPU");
            File.WriteAllBytes(ProfessionValidationWorkspace.Output("detail-controller-fixture.png"), image.EncodeToPNG()); Object.Destroy(image);
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Profession detail: " + message); }
        private static void Finish(int code)
        {
            SessionState.SetBool(Active, false);
            if (camera) camera.targetTexture = null; if (target) { target.Release(); Object.Destroy(target); }
            EditorApplication.Exit(code);
        }
    }
}
