using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.ProfessionUI;
using Object = UnityEngine.Object;
using SimulatedMouse = Zjcs.Editor.ProfessionUI.ProfessionItemIconValidation.SimulatedMouse;

namespace Zjcs.Editor.ProfessionUI
{
    // Control fixtures exercise consumed service boundaries, not a replacement
    // profession screen. Native prefab layout/appearance integration is separate.
    [InitializeOnLoad]
    public static class ProfessionAuxiliaryValidation
    {
        private const string Active = "Zjcs.ProfessionAuxiliary.Active";
        private static bool ready, initialized;
        private static int frame, phase, cycle;
        private static GameObject wikiRoot;
        private static Button button;
        private static SimulatedMouse mouse;
        private static EventSystem events;
        private static WikiContext context;
        private static Camera camera;
        private static RenderTexture target;
        private sealed class Currency : IProfessionCurrencyItem
        {
            public Component Component { get; set; }
            public readonly List<bool> Stops = new List<bool>();
            public void SetStopUpdateCount(bool isStop) => Stops.Add(isStop);
        }
        private sealed class BarManager : ITopBarManager
        {
            public TopBarItem Bar;
            public int Borrows, Returns, Updates;
            public List<CurrencyItemData> Last;
            public TopBarItem BorrowTopBar(List<CurrencyItemData> data) { Borrows++; Last = data.ToList(); return Bar; }
            public void ReturnTopBar(TopBarItem bar) { Require(bar == Bar, "same bar returned"); Returns++; }
            public void UpdateTopBar(TopBarItem bar, List<CurrencyItemData> data) { Require(bar == Bar, "same bar updated"); Updates++; Last = data.ToList(); }
        }
        private sealed class WikiContext : IProfessionWikiContext, IProfessionWikiPlayer, IProfessionWikiCondition
        {
            public readonly List<string> Calls = new List<string>();
            public bool Server, Limited, MissingInfo, MissingCondition, Met = true, EmptyUrl;
            public OfflineProfessionWikiTable Original;
            public Dictionary<string, object> Report;
            public bool ServerWikiDisabled { get { Calls.Add("server"); return Server; } }
            public IProfessionWikiPlayer Player => this;
            public bool IsWikiLimited { get { Calls.Add("player"); return Limited; } }
            public string GetShuShuAccountId() => "fixture-id";
            public string Account => "fixture-account";
            public int ServerId => 3;
            public long Id => 7;
            public string Name => "fixture-name";
            public int Level => 5;
            public ProfessionWikiInfo GetWikiInfo(string type)
            {
                Calls.Add("info"); Require(type == "Profession", "source wiki type");
                return Original != null ? Original.GetWikiInfo(type) : MissingInfo ? null : new ProfessionWikiInfo(type, 0, 3010, "fixture://profession");
            }
            public string GetURL(string type) { Calls.Add("url"); return EmptyUrl ? "" : "fixture://profession"; }
            public string GetTitleKey(string type) { Calls.Add("title"); return "wiki_title_" + type; }
            public IProfessionWikiCondition GetCondition(int id) { Calls.Add("condition"); Require(id == 3010, "source condition id"); return MissingCondition ? null : this; }
            public bool IsMet(IProfessionWikiPlayer player) { Calls.Add("met"); Require(ReferenceEquals(player, this), "same player reaches condition"); return Met; }
            public string Localize(string key) { Calls.Add("localize:" + key); return key; }
            public void SystemMsg(string message) => Calls.Add("message:" + message);
            public Task<string> OpenHtml(string url, string orientation, string navigation, string close)
            {
                Calls.Add("open"); Require(url == "fixture://profession" && orientation == "2" && navigation == "2" && close == "1", "original SDK arguments preserved");
                return Task.FromResult("fixture-result");
            }
            public Dictionary<string, object> CreateEventData(string name, string accountId, string account)
            { Calls.Add("eventdata"); Require(name == "ClickWiki" && accountId == GetShuShuAccountId() && account == Account, "original report account arguments"); return new Dictionary<string, object>(); }
            public void ReportEvent(string name, Dictionary<string, object> fields) { Calls.Add("report"); Require(name == "ClickWiki", "original report name"); Report = fields; }
            public void PublishWikiClick() => Calls.Add("publish");
        }
        static ProfessionAuxiliaryValidation()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredPlayMode) ready = true; };
        }
        public static void Run()
        {
            try
            {
                Require(Application.dataPath.Replace('\\', '/').Contains("/.tools/profession-ui-validation/unity/"), "isolated project");
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
                Application.runInBackground = true;
                EditorApplication.isPaused = false;
                SessionState.SetBool(Active, true);
                SessionState.SetFloat(Active + ".Deadline", (float)(EditorApplication.timeSinceStartup + 180));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Update()
        {
            if (!SessionState.GetBool(Active, false)) return;
            if (EditorApplication.timeSinceStartup >= SessionState.GetFloat(Active + ".Deadline", float.MaxValue))
            { Debug.LogError($"Profession auxiliary control validation timed out: ready={ready}, initialized={initialized}, playing={EditorApplication.isPlaying}, paused={EditorApplication.isPaused}, phase={phase}, cycle={cycle}, frame={Time.frameCount}, previousFrame={frame}."); Finish(1); return; }
            if (!ready || !EditorApplication.isPlaying) return;
            try
            {
                if (!initialized) { Initialize(); return; }
                if (Time.frameCount <= frame + (phase == 0 ? 3 : 0)) return;
                frame = Time.frameCount;
                if (phase == 0)
                {
                    Canvas.ForceUpdateCanvases(); mouse.Point = RectTransformUtility.WorldToScreenPoint(camera, button.transform.position);
                    var hits = new List<RaycastResult>(); events.RaycastAll(new PointerEventData(events) { position = mouse.Point }, hits);
                    Require(hits.Count > 0 && hits[0].gameObject == button.gameObject, "simulated pointer targets the control fixture");
                    if (cycle == 0) CaptureFixture();
                    context.Calls.Clear(); phase = 1;
                }
                else if (phase == 1) { mouse.Down = true; mouse.Held = true; phase = 2; }
                else if (phase == 2) { mouse.Down = false; mouse.Held = false; mouse.Up = true; phase = 3; }
                else
                {
                    mouse.Up = false;
                    if (cycle < 2)
                    {
                        Require(context.Calls.SequenceEqual(new[] { "server", "player", "info", "condition", "met", "url", "title", "open", "message:fixture-result", "eventdata", "report", "publish" }), "original click, SDK result, report and notification order exactly once");
                        Require(context.Report.Count == 5 && (string)context.Report["WikiEntry"] == "Profession" && (int)context.Report["ServerId"] == 3 && (long)context.Report["PlayerId"] == 7 && (string)context.Report["PlayerName"] == context.Name && (int)context.Report["PlayerLevel"] == 5, "original report metadata preserved");
                        wikiRoot.SetActive(false); context.Calls.Clear(); button.onClick.Invoke(); Require(context.Calls.Count == 0, "disable removes original button subscription");
                        wikiRoot.SetActive(true);
                    }
                    else if (cycle == 2)
                        Require(context.Calls.SequenceEqual(new[] { "server", "localize:Wiki_Disabled", "message:Wiki_Disabled" }), "click rechecks a changed server guard before all later effects");
                    else
                    {
                        Require(context.Calls.SequenceEqual(new[] { "server", "player", "info", "condition", "met", "url", "localize:Wiki_Url_Empty", "message:Wiki_Url_Empty" }), "empty URL reports the original message and skips SDK/report/notification");
                        Debug.Log("Profession original top-bar guard/parenting/popup/stop-count lifecycle and WikiEntry guard order, CN filtering, simulated mouse clicks, reenable subscription, SDK/report/event ordering passed. Control fixtures do not validate full profession native layout or appearance.");
                        Finish(0); return;
                    }
                    cycle++; phase = 0;
                    if (cycle == 2) context.Server = true;
                    if (cycle == 3) { context.Server = false; context.EmptyUrl = true; }
                }
            }
            catch (Exception exception) { Debug.LogException(exception); Finish(1); }
        }
        private static void Initialize()
        {
            initialized = true; frame = Time.frameCount;
            Application.runInBackground = true;
            VerifyTopBar();
            var table = new OfflineProfessionWikiTable(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Official/ProfessionUI/Config/ProfessionWiki.json"));
            Require(table.GetWikiInfo("Profession") == null && table.GetURL("Profession") == null && table.GetTitleKey("Profession") == "wiki_title_Profession", "actual complete source table has no CN profession entry");
            foreach (WikiContext probe in new[] { new WikiContext { Server = true }, new WikiContext { Limited = true }, new WikiContext { Original = table }, new WikiContext { MissingInfo = true }, new WikiContext { MissingCondition = true }, new WikiContext { Met = false } })
            {
                GameObject root = MakeWiki(probe, out Button child); Require(!child.gameObject.activeSelf, "original guards hide the button");
                string[] expected = probe.Server ? new[] { "server" } : probe.Limited ? new[] { "server", "player" } : probe.Original != null || probe.MissingInfo ? new[] { "server", "player", "info" } : probe.MissingCondition ? new[] { "server", "player", "info", "condition" } : new[] { "server", "player", "info", "condition", "met" };
                Require(probe.Calls.SequenceEqual(expected), "enable guard short-circuit order"); root.SetActive(false); Object.Destroy(root);
            }
            var canvas = new GameObject("AuxiliaryControlFixtures", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            camera = new GameObject("AuxiliaryFixtureCamera").AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 128;
            camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            target = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32); target.Create(); camera.targetTexture = target;
            var ui = canvas.GetComponent<Canvas>(); ui.renderMode = RenderMode.ScreenSpaceCamera; ui.worldCamera = camera; ui.planeDistance = 1;
            context = new WikiContext(); wikiRoot = MakeWiki(context, out button, canvas.transform);
            foreach (var previous in Object.FindObjectsOfType<EventSystem>(true)) { previous.enabled = false; foreach (var module in previous.GetComponents<BaseInputModule>()) module.enabled = false; }
            var eventGo = new GameObject("AuxiliaryFixtureEvents", typeof(EventSystem), typeof(StandaloneInputModule));
            events = eventGo.GetComponent<EventSystem>(); mouse = eventGo.AddComponent<SimulatedMouse>();
            var input = eventGo.GetComponent<StandaloneInputModule>(); input.inputOverride = mouse; input.forceModuleActive = true;
        }
        private static GameObject MakeWiki(WikiContext data, out Button target, Transform parent = null)
        {
            var root = new GameObject("WikiControlFixture", typeof(RectTransform)); root.SetActive(false); root.transform.SetParent(parent, false);
            var child = new GameObject("ButtonFixture", typeof(RectTransform), typeof(Image), typeof(Button)); child.transform.SetParent(root.transform, false);
            ((RectTransform)child.transform).sizeDelta = new Vector2(200, 80); target = child.GetComponent<Button>();
            var wiki = root.AddComponent<WikiEntry>(); Set(wiki, "WikiType", "Profession"); Set(wiki, "mButtonGo", child); Set(wiki, "mButton", target); Set(wiki, "mClickClearNewTip", true); wiki.Bind(data); root.SetActive(true); return root;
        }
        private static void VerifyTopBar()
        {
            var slot = new GameObject("TopBarFixtureSlot", typeof(RectTransform));
            var barGo = new GameObject("OriginalTopBarControlFixture", typeof(RectTransform)); var bar = barGo.AddComponent<TopBarItem>(); Set(bar, "mSlot", barGo.transform);
            var currency = new Currency { Component = new GameObject("CurrencyServiceFixture", typeof(RectTransform)).transform };
            bar.SetCurrencyItem(currency); Require(currency.Component.transform.parent == barGo.transform && currency.Component.transform.localPosition == Vector3.zero && currency.Component.transform.localScale == Vector3.one, "original currency placement");
            var popup = new GameObject("PopupLifecycleFixture").AddComponent<WorldPopupWindow>();
            var root = new GameObject("TopBarHandlerFixture"); root.SetActive(false); var handler = root.AddComponent<TopBarHandler>(); var manager = new BarManager { Bar = bar };
            Set(handler, "mShowTypeList", new List<PlayerItemType>()); Set(handler, "mShowItemIdList", new List<int>()); Set(handler, "mSlot", slot.transform); Set(handler, "mPopupWindow", popup); Set(handler, "mShowWaitEnterAnim", true); Set(handler, "mHideBeforeExitAnim", true); handler.Bind(manager);
            root.SetActive(true); Require(manager.Borrows == 0, "source empty currency lists do not borrow a bar");
            handler.UpdateCurrencyData(PlayerItemType.Diamon, 5); Require(manager.Borrows == 1 && manager.Updates == 1 && manager.Last.Count == 1 && manager.Last[0].Type == PlayerItemType.Diamon && manager.Last[0].ItemIdProivder.ItemId == 5, "original data/provider and update order");
            Require(barGo.transform.parent == slot.transform && ((RectTransform)barGo.transform).anchoredPosition == Vector2.zero && barGo.transform.localPosition.z == 0 && barGo.transform.localScale == Vector3.one, "original handler restores parent, anchored position, depth and scale");
            root.SetActive(false); Require(manager.Returns == 1 && currency.Stops.Last() == false, "disable clears stop-count before returning bar");
            handler.SetStopUpdateCount(true); root.SetActive(true); Require(manager.Borrows == 2 && !barGo.activeSelf && currency.Stops.Last(), "reenable waits for popup enter and retains stop-count");
            popup.Show(); Require(barGo.activeSelf, "original popup show event activates borrowed bar");
            popup.Hide(null); Require(!barGo.activeSelf, "original popup hiding event hides borrowed bar");
            root.SetActive(false); Require(manager.Returns == 2 && !currency.Stops.Last(), "second disable returns once");
            Object.Destroy(root); Object.Destroy(popup.gameObject); Object.Destroy(slot); Object.Destroy(barGo);
        }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void CaptureFixture()
        {
            camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
            var pixels = new Texture2D(512, 512, TextureFormat.RGBA32, false, true); pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); pixels.Apply(); RenderTexture.active = previous;
            Require(pixels.GetPixels32().Count(p => p.r > 100) > 100, "control fixture is actually rendered at the pointer target");
            string repository = Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "../../.."));
            File.WriteAllBytes(Path.Combine(repository, "output/profession-ui-native-20261028/auxiliary-control-fixture.png"), pixels.EncodeToPNG()); Object.Destroy(pixels);
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Profession auxiliary: " + message); }
        private static void Finish(int code)
        {
            SessionState.SetBool(Active, false);
            if (camera != null) camera.targetTexture = null;
            if (target != null) { target.Release(); Object.Destroy(target); }
            EditorApplication.Exit(code);
        }
    }
}
