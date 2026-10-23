using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Presentation.Home;
using Zjcs.Presentation.Wishes;

namespace Zjcs.Editor.Wishes
{
    public static class OfficialWishPlayModeAcceptance
    {
        private const string ActiveKey = "Zjcs.Wishes.VisibleAcceptance.Active";
        private const string ResultKey = "Zjcs.Wishes.VisibleAcceptance.Result";
        private const string RootKey = "Zjcs.Wishes.VisibleAcceptance.Root";
        private const string SlotKey = "Zjcs.Wishes.VisibleAcceptance.Slot";
        private const string CapturePrefixKey = "Zjcs.Wishes.VisibleAcceptance.CapturePrefix";
        private const string TicketBaselineKey = "Zjcs.Wishes.VisibleAcceptance.TicketBaseline";
        private static bool probeStarted;

        [MenuItem("Zjcs/Validate/Wishes Visible Play Mode")]
        public static void Run()
        {
            if (Application.isBatchMode) throw new InvalidOperationException("Visible wishes acceptance requires a graphical Editor.");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before starting wishes acceptance.");

            string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(),
                "zjcs-wishes-visible-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
            try
            {
                var slots = new ProfileSlotCatalog(root);
                string slotId = slots.CreateSlot();
                OfflineMainlineProgressSession profile = OfflineMainlineProgressSession.OpenCharacter(slots, slotId);
                ProgressionResult result = profile.ApplyCharacterMutation(player =>
                {
                    player.CharacterName = "祈愿 Play Mode 验收";
                    player.CharacterCreated = true;
                    player.InitialWeaponType = 200;
                    player.Experience = 100000;
                    player.Level = 20;
                    player.SubRank = 3;
                    player.Rank = 1;
                    player.MaxProfessionRank = 1;
                    player.Wardrobe.Profession = 1;
                    if (!player.UnlockedMapClassIds.Contains(11)) player.UnlockedMapClassIds.Add(11);
                }, "acceptance:wish-visible:profile:" + slotId);
                if (result.Status != ProgressionResultStatus.Applied
                    && result.Status != ProgressionResultStatus.Unchanged
                    && result.Status != ProgressionResultStatus.Ready)
                    throw new InvalidOperationException("Could not create the isolated wishes profile: " + result.Status);

                string capturePrefix = Path.Combine(Path.GetTempPath(),
                    "zjcs-wishes-visible-" + Guid.NewGuid().ToString("N"));
                SessionState.SetString(RootKey, root);
                SessionState.SetString(SlotKey, slotId);
                SessionState.SetString(CapturePrefixKey, capturePrefix);
                SessionState.SetString(TicketBaselineKey, profile.InventoryCount(110).ToString());
                SessionState.SetInt(ResultKey, 2);
                SessionState.SetBool(ActiveKey, true);
                probeStarted = false;
                ConfigurePortraitGameView();
                HookPlayModeEvents();
                EditorApplication.isPlaying = true;
            }
            catch
            {
                DeleteTemporaryRoot(root);
                throw;
            }
        }

        [InitializeOnLoadMethod]
        private static void RestoreAfterDomainReload()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            HookPlayModeEvents();
            if (EditorApplication.isPlaying) EditorApplication.delayCall += StartProbe;
        }

        private static void HookPlayModeEvents()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                EditorApplication.delayCall -= StartProbe;
                EditorApplication.delayCall += StartProbe;
                return;
            }
            if (state != PlayModeStateChange.EnteredEditMode) return;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            bool passed = SessionState.GetInt(ResultKey, 1) == 0;
            SessionState.SetBool(ActiveKey, false);
            DeleteTemporaryRoot(SessionState.GetString(RootKey, string.Empty));
            if (Environment.GetEnvironmentVariable("ZJCS_WISH_PLAYMODE_AUTOMATION") == "1")
                EditorApplication.Exit(passed ? 0 : 1);
        }

        private static void StartProbe()
        {
            if (probeStarted || !SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            probeStarted = true;
            var host = new GameObject("WishVisibleAcceptance");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<ProbeRunner>();
        }

        internal static void Finish(bool passed, Exception error)
        {
            if (SessionState.GetInt(ResultKey, 2) != 2) return;
            if (error != null)
            {
                Debug.LogException(error);
                Debug.LogError("[WishPlayMode] visible acceptance failed: " + error.Message);
            }
            else
            {
                string captureOnly = Environment.GetEnvironmentVariable("ZJCS_WISH_PLAYMODE_CAPTURE_ONLY");
                Debug.Log(captureOnly == "1"
                    ? "[WishPlayMode] direct wishes page and guide reward were rendered; mouse input was not tested."
                    : "[WishPlayMode] real OS clicks completed the wish guide, regular draw, and exchange shop checks.");
            }
            SessionState.SetInt(ResultKey, passed ? 0 : 1);
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }

        private sealed class ProbeRunner : MonoBehaviour
        {
            private HomeFeatureRuntime home;
            private OfflineMainlineProgressSession profile;

            private void Start() => StartCoroutine(GuardedRun());

            private IEnumerator GuardedRun()
            {
                var stack = new Stack<IEnumerator>();
                stack.Push(RunScenario());
                while (true)
                {
                    object wait;
                    try
                    {
                        if (stack.Count == 0) break;
                        IEnumerator current = stack.Peek();
                        if (!current.MoveNext())
                        {
                            stack.Pop();
                            continue;
                        }
                        wait = current.Current;
                        if (wait is IEnumerator nested)
                        {
                            stack.Push(nested);
                            continue;
                        }
                    }
                    catch (Exception error)
                    {
                        OfficialWishPlayModeAcceptance.Finish(false, error);
                        yield break;
                    }
                    yield return wait;
                }
                OfficialWishPlayModeAcceptance.Finish(true, null);
            }

            private IEnumerator RunScenario()
            {
                string root = SessionState.GetString(RootKey, string.Empty);
                string slotId = SessionState.GetString(SlotKey, string.Empty);
                string capturePrefix = SessionState.GetString(CapturePrefixKey, string.Empty);
                if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(slotId) || string.IsNullOrEmpty(capturePrefix))
                    throw new InvalidOperationException("The visible wishes profile or capture path is missing.");

                var slots = new ProfileSlotCatalog(root);
                profile = OfflineMainlineProgressSession.OpenCharacter(slots, slotId);
                if (Environment.GetEnvironmentVariable("ZJCS_WISH_PLAYMODE_DIRECT_UI") != "1")
                {
                    try
                    {
                        home = HomeFeatureRuntime.Begin(slotId, root, null, null);
                    }
                    catch (InvalidDataException error)
                    {
                        Debug.LogWarning("[WishPlayMode] Home entry unavailable; opening wishes directly: " + error.Message);
                        GameObject failedHome = GameObject.Find("HomeGameRuntime");
                        if (failedHome != null) Destroy(failedHome);
                    }
                }
                HideProfileEntryCanvases();
                yield return null;
                yield return null;
                string homeCapture = capturePrefix + (home == null ? "-wish-entry.png" : "-home.png");
                WishScreenController wishes = null;
                float deadline;
                string repairCapture = string.Empty;
                if (home == null)
                {
                    wishes = WishScreenController.Open(profile, slotId, null, null, null);
                    yield return null;
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                }
                else
                {
                    if (FurnitureClassId(home, 5) != 4004 || OwnsFurniture(home, 5))
                        throw new InvalidOperationException("The isolated profile did not load the unrepaired official goddess statue.");

                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(homeCapture);
                    yield return null;
                    yield return null;
                    if (!File.Exists(homeCapture) || new FileInfo(homeCapture).Length < 1000)
                        throw new InvalidOperationException("The home Play Mode capture was not written.");

                    Vector2 statueScreen = StatueScreenPoint(home);
                    yield return ClickScreen(statueScreen);
                    Button repair = FindButton("RepaireBtn");
                    deadline = Time.realtimeSinceStartup + 8f;
                    while (repair == null && Time.realtimeSinceStartup < deadline)
                    {
                        yield return null;
                        repair = FindButton("RepaireBtn");
                    }
                    if (repair == null || !home.Hud.AnyModalOpen)
                        throw new InvalidOperationException("A real statue click did not open its repair dialog.");

                    repairCapture = capturePrefix + "-repair.png";
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(repairCapture);
                    yield return null;
                    yield return null;
                    if (!File.Exists(repairCapture) || new FileInfo(repairCapture).Length < 1000)
                        throw new InvalidOperationException("The repair dialog capture was not written.");

                    yield return ClickButton(repair);
                    deadline = Time.realtimeSinceStartup + 8f;
                    while (!OwnsFurniture(home, 5) && Time.realtimeSinceStartup < deadline) yield return null;
                    if (!OwnsFurniture(home, 5))
                        throw new InvalidOperationException("The real repair button did not activate the goddess statue.");

                    yield return ClickScreen(statueScreen);
                    deadline = Time.realtimeSinceStartup + 8f;
                    while (wishes == null && Time.realtimeSinceStartup < deadline)
                    {
                        yield return null;
                        wishes = FindObjectOfType<WishScreenController>();
                    }
                }
                if (wishes == null || !wishes.IsOpen)
                    throw new InvalidOperationException("The wishes entry did not open its screen.");

                if (!File.Exists(homeCapture))
                {
                    yield return new WaitForEndOfFrame();
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(homeCapture);
                    yield return null;
                    yield return null;
                }
                if (!File.Exists(homeCapture) || new FileInfo(homeCapture).Length < 1000)
                    throw new InvalidOperationException("The wishes entry capture was not written.");
                if (!wishes.Service.GuideRewardClaimed)
                    throw new InvalidOperationException("Entering wishes did not record the first-entry tutorial reward.");

                profile.Reload();
                long baseline = long.TryParse(SessionState.GetString(TicketBaselineKey, "0"), out long parsed)
                    ? parsed : 0;
                if (profile.InventoryCount(110) != baseline + 5)
                    throw new InvalidOperationException("The first-entry wish tutorial did not grant exactly five tickets.");

                GameObject modal = WishModal(wishes);
                bool hasGuideTitle = modal != null && modal.GetComponentsInChildren<TMP_Text>(true)
                    .Any(value => value != null && value.gameObject.activeInHierarchy
                        && value.text.IndexOf("祈愿引导奖励", StringComparison.Ordinal) >= 0);
                bool hasGuideTicket = modal != null && modal.GetComponentsInChildren<TMP_Text>(true)
                    .Any(value => value != null && value.gameObject.activeInHierarchy
                        && value.text.IndexOf("星之愿 ×5", StringComparison.Ordinal) >= 0);
                bool hasVisibleCancel = modal != null && modal.GetComponentsInChildren<Button>(true)
                    .Any(value => value != null && value.gameObject.activeInHierarchy && value.name == "CancelBtn");
                if (!hasGuideTitle || !hasGuideTicket || hasVisibleCancel)
                    throw new InvalidOperationException("The guide reward dialog did not show its title, five tickets, and a single close action.");
                Button closeGuide = modal == null ? null : modal.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(value => value != null && value.gameObject.activeInHierarchy
                        && value.interactable && (value.name.IndexOf("CloseBtn", StringComparison.OrdinalIgnoreCase) >= 0
                            || value.name == "WishAction_关闭"
                            || value.GetComponentInChildren<TMP_Text>(true)?.text == "关闭"));
                string guideCapture = capturePrefix + "-guide.png";
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(guideCapture);
                yield return null;
                yield return null;
                if (!File.Exists(guideCapture) || new FileInfo(guideCapture).Length < 1000)
                    throw new InvalidOperationException("The guide reward capture was not written.");
                if (Environment.GetEnvironmentVariable("ZJCS_WISH_PLAYMODE_CAPTURE_ONLY") == "1")
                {
                    Debug.Log("[WishPlayMode] before guide mask: " + WishRenderState(wishes));
                    GameObject guideModal = WishModal(wishes);
                    if (guideModal != null) guideModal.SetActive(false);
                    yield return null;
                    Debug.Log("[WishPlayMode] after guide mask: " + WishRenderState(wishes));
                    string pageCapture = capturePrefix + "-page.png";
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(pageCapture);
                    yield return null;
                    yield return null;
                    if (!File.Exists(pageCapture) || new FileInfo(pageCapture).Length < 1000)
                        throw new InvalidOperationException("The unobscured wishes page capture was not written.");
                    Debug.Log("[WishPlayMode] captured direct wishes entry " + homeCapture + ", guide reward "
                        + guideCapture + " and unobscured page " + pageCapture + "; mouse input was not tested.");
                    yield break;
                }
                if (closeGuide == null) throw new InvalidOperationException("The first-entry guide reward has no close button.");
                yield return ClickButton(closeGuide);
                deadline = Time.realtimeSinceStartup + 5f;
                while (WishModal(wishes) != null && Time.realtimeSinceStartup < deadline) yield return null;
                if (WishModal(wishes) != null) throw new InvalidOperationException("The guide reward close button did not dismiss its modal.");

                Button draw = FindRegularSingleDraw();
                if (draw == null || !draw.interactable)
                    throw new InvalidOperationException("The regular single-draw button is not interactable after the tutorial reward.");
                long poolBefore = wishes.Service.PoolCount(0);
                long ticketsBefore = profile.InventoryCount(110);
                yield return ClickButton(draw);
                bool audioObserved = false;
                deadline = Time.realtimeSinceStartup + 10f;
                while (wishes.Service.PoolCount(0) == poolBefore && Time.realtimeSinceStartup < deadline)
                {
                    audioObserved |= FindObjectsOfType<AudioSource>().Any(value => value != null && value.isPlaying);
                    yield return null;
                }
                if (wishes.Service.PoolCount(0) != poolBefore + 1)
                    throw new InvalidOperationException("The real single-draw button did not commit one regular draw.");
                audioObserved |= FindObjectsOfType<AudioSource>().Any(value => value != null && value.isPlaying);
                profile.Reload();
                if (profile.InventoryCount(110) != ticketsBefore - 1
                    || wishes.Service.History(0, DateTimeOffset.Now.ToUnixTimeSeconds(), 0, 10).Count != 1)
                    throw new InvalidOperationException("The visible draw result did not match the committed ticket and history state.");

                GameObject resultModal = WishModal(wishes);
                if (resultModal == null || FindDeep(resultModal.transform, "OfficialWishgacha_result") == null)
                    throw new InvalidOperationException("The regular draw did not open its official result panel.");
                yield return new WaitForSecondsRealtime(2f);
                string resultCapture = capturePrefix + "-result.png";
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(resultCapture);
                yield return null;
                yield return null;
                if (!File.Exists(resultCapture) || new FileInfo(resultCapture).Length < 1000)
                    throw new InvalidOperationException("The animated result capture was not written.");

                Button closeResult = resultModal.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(value => value != null && value.gameObject.activeInHierarchy
                        && value.interactable && value.name.IndexOf("CloseBtn", StringComparison.OrdinalIgnoreCase) >= 0);
                if (closeResult == null) throw new InvalidOperationException("The result panel has no close button.");
                yield return ClickButton(closeResult);
                deadline = Time.realtimeSinceStartup + 5f;
                while (WishModal(wishes) != null && Time.realtimeSinceStartup < deadline) yield return null;
                if (WishModal(wishes) != null) throw new InvalidOperationException("The result close button did not dismiss its modal.");

                Button shopTab = FindButton("HomeShopPanelTabBottomBtn (3)");
                if (shopTab == null) throw new InvalidOperationException("The official exchange tab was not found.");
                yield return ClickButton(shopTab);
                Transform shop = FindDeep(GameObject.Find("OfficialWishPanel")?.transform, "ShopContent");
                if (shop == null || !shop.gameObject.activeSelf)
                    throw new InvalidOperationException("The exchange tab did not show the shop page.");
                Button dailyShop = FindButton("Shop5");
                if (dailyShop == null) throw new InvalidOperationException("The daily nine-slot shop tab was not found.");
                yield return ClickButton(dailyShop);
                IReadOnlyList<WishShopStockView> stocks = wishes.Service.ShopGoods(5, 1, DateTime.Now);
                if (stocks.Count != 9) throw new InvalidOperationException("The visible daily shop is missing configured stock slots.");
                Debug.Log("[WishPlayMode] home=" + homeCapture + " repair=" + repairCapture + " guide="
                    + guideCapture + " result=" + resultCapture + " resultAudioObserved=" + audioObserved);
            }

            private static Vector2 StatueScreenPoint(HomeFeatureRuntime runtime)
            {
                FieldInfo field = typeof(HomeFeatureRuntime).GetField("scene", BindingFlags.Instance | BindingFlags.NonPublic);
                HomeSceneView scene = field?.GetValue(runtime) as HomeSceneView;
                if (scene == null || scene.Camera == null) throw new InvalidOperationException("The official home scene camera is missing.");
                foreach (HomeQuad region in runtime.Catalog.Scene.click_regions.Where(value => value != null
                    && value.path != null && value.path.StartsWith("/StatueOfGod/", StringComparison.Ordinal)))
                {
                    float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                    for (int index = 0; index + 1 < region.vertices.Length; index += 2)
                    {
                        minX = Mathf.Min(minX, region.vertices[index]);
                        maxX = Mathf.Max(maxX, region.vertices[index]);
                        minY = Mathf.Min(minY, region.vertices[index + 1]);
                        maxY = Mathf.Max(maxY, region.vertices[index + 1]);
                    }
                    for (int sample = 0; sample < 5; sample++)
                    {
                        float fraction = 0.3f + sample * 0.1f;
                        var world = new Vector2(Mathf.Lerp(minX, maxX, fraction), Mathf.Lerp(minY, maxY, 0.5f));
                        if (scene.HitTest(world) != 5) continue;
                        Vector3 screen = scene.Camera.WorldToScreenPoint(world);
                        if (screen.z <= 0 || screen.x < 0 || screen.y < 0
                            || screen.x >= Screen.width || screen.y >= Screen.height) continue;
                        return new Vector2(screen.x, screen.y);
                    }
                }
                throw new InvalidOperationException("No visible point in the official goddess statue click region maps to furniture 5.");
            }

            private static void HideProfileEntryCanvases()
            {
                foreach (Canvas canvas in Resources.FindObjectsOfTypeAll<Canvas>())
                {
                    if (canvas == null || canvas.name == "Home Canvas") continue;
                    string name = canvas.name;
                    if (name.IndexOf("CharacterCreation", StringComparison.OrdinalIgnoreCase) < 0
                        && name.IndexOf("Opening", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    canvas.gameObject.SetActive(false);
                }
            }

            private static int FurnitureClassId(HomeFeatureRuntime runtime, int type)
            {
                object rules = typeof(HomeFeatureRuntime).GetProperty("Rules").GetValue(runtime);
                object record = rules.GetType().GetMethod("FurnitureOf").Invoke(rules, new object[] { type });
                return record == null ? 0 : (int)record.GetType().GetField("classId").GetValue(record);
            }

            private static bool OwnsFurniture(HomeFeatureRuntime runtime, int type)
            {
                object session = typeof(HomeFeatureRuntime).GetProperty("Session").GetValue(runtime);
                return (bool)session.GetType().GetMethod("OwnsFurniture").Invoke(session, new object[] { type });
            }

            private static Button FindRegularSingleDraw()
            {
                Transform panel = GameObject.Find("OfficialWishPanel")?.transform;
                if (panel == null) return null;
                return panel.GetComponentsInChildren<Button>(true).FirstOrDefault(value => value != null
                    && value.name == "CommonBtn_CostItem" && value.gameObject.activeInHierarchy
                    && value.interactable && HierarchyPath(value.transform).Contains("GachaOneTimesBtn"));
            }

            private static Button FindButton(string suffix) => UnityEngine.Object.FindObjectsOfType<Button>(true)
                .FirstOrDefault(value => value != null && value.gameObject.activeInHierarchy && value.interactable
                    && value.name.EndsWith(suffix, StringComparison.Ordinal));

            private static GameObject WishModal(WishScreenController wishes)
            {
                FieldInfo field = typeof(WishScreenController).GetField("modal", BindingFlags.Instance | BindingFlags.NonPublic);
                return field?.GetValue(wishes) as GameObject;
            }

            private static string WishRenderState(WishScreenController wishes)
            {
                Canvas canvas = wishes == null ? null : wishes.GetComponentInChildren<Canvas>(true);
                GameObject panel = GameObject.Find("OfficialWishPanel");
                Image[] images = panel == null ? Array.Empty<Image>() : panel.GetComponentsInChildren<Image>(true);
                TMP_Text[] texts = panel == null ? Array.Empty<TMP_Text>() : panel.GetComponentsInChildren<TMP_Text>(true);
                if (panel == null || images.Length == 0 || texts.Length == 0)
                    throw new InvalidOperationException("The wishes shell did not instantiate its official image and text components.");
                string visible = string.Join(",", images.Where(value => value != null && value.isActiveAndEnabled
                    && value.color.a > 0.01f && value.sprite != null).Take(8).Select(value => value.name));
                RectTransform rect = panel == null ? null : panel.GetComponent<RectTransform>();
                return "surface=" + Screen.width + "x" + Screen.height
                    + " canvas=" + (canvas == null ? "missing" : canvas.renderMode + "/" + canvas.enabled
                        + "/order=" + canvas.sortingOrder + "/scale=" + canvas.transform.lossyScale)
                    + " panel=" + (panel == null ? "missing" : panel.activeInHierarchy + "/" + panel.activeSelf
                        + "/rect=" + (rect == null ? "missing" : rect.rect.ToString()))
                    + " images=" + images.Length + " texts=" + texts.Length + " visible=" + visible;
            }

            private static Transform FindDeep(Transform root, string name)
            {
                if (root == null) return null;
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == name) return child;
                return null;
            }

            private static string HierarchyPath(Transform node)
            {
                var parts = new Stack<string>();
                for (Transform current = node; current != null; current = current.parent) parts.Push(current.name);
                return string.Join("/", parts.ToArray());
            }

            private static IEnumerator ClickButton(Button button)
            {
                if (button == null || !button.interactable) throw new InvalidOperationException("A visible acceptance button is unavailable.");
                Canvas canvas = button.GetComponentInParent<Canvas>();
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(canvas == null ? null : canvas.worldCamera,
                    ((RectTransform)button.transform).position);
                yield return ClickScreen(screen);
            }

            private static IEnumerator ClickScreen(Vector2 screen)
            {
                FocusGameView();
                yield return null;
                Rect view = GameViewWindow();
                if (view.width <= 1 || view.height <= 1 || Screen.width <= 0 || Screen.height <= 0)
                    throw new InvalidOperationException("The visible Unity Game view has no usable screen rectangle.");
                float dpiScale = DesktopDpiScale();
                float gameViewScale = Mathf.Min(view.width / Screen.width, view.height / Screen.height);
                float contentWidth = Screen.width * gameViewScale;
                float contentHeight = Screen.height * gameViewScale;
                float contentLeft = view.x + (view.width - contentWidth) * 0.5f;
                float contentTop = view.y + (view.height - contentHeight) * 0.5f;
                int x = Mathf.RoundToInt((contentLeft + screen.x * gameViewScale) * dpiScale);
                int y = Mathf.RoundToInt((contentTop + (Screen.height - screen.y) * gameViewScale) * dpiScale);
                int desktopLeft = GetSystemMetrics(76);
                int desktopTop = GetSystemMetrics(77);
                int desktopWidth = GetSystemMetrics(78);
                int desktopHeight = GetSystemMetrics(79);
                if (x < desktopLeft || y < desktopTop
                    || x >= desktopLeft + desktopWidth || y >= desktopTop + desktopHeight)
                    throw new InvalidOperationException("A tested UI point maps outside the desktop: " + x + "," + y);
                FocusMainWindow();
                uint currentProcess = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
                uint foregroundProcess = 0;
                float focusDeadline = Time.realtimeSinceStartup + 90f;
                bool loggedFocusWait = false;
                while (Time.realtimeSinceStartup < focusDeadline)
                {
                    IntPtr foregroundWindow = GetForegroundWindow();
                    GetWindowThreadProcessId(foregroundWindow, out foregroundProcess);
                    if (foregroundProcess == currentProcess) break;
                    if (!loggedFocusWait)
                    {
                        Debug.LogWarning("[WishPlayMode] waiting for the isolated Unity Editor to receive foreground input.");
                        loggedFocusWait = true;
                    }
                    FocusMainWindow();
                    yield return null;
                }
                if (foregroundProcess != currentProcess)
                    throw new InvalidOperationException("The isolated Unity Editor did not receive foreground input; no click was sent.");
                if (!SetCursorPos(x, y)) throw new InvalidOperationException("Windows could not place the cursor over the Game view.");
                var point = new NativePoint { X = x, Y = y };
                uint hitProcess;
                IntPtr hitWindow = WindowFromPoint(point);
                GetWindowThreadProcessId(hitWindow, out hitProcess);
                if (hitProcess != currentProcess)
                    throw new InvalidOperationException("The mapped UI point is covered by another window; no click was sent.");
                Debug.Log("[WishPlayMode] OS click=" + x + "," + y + " game=" + screen
                    + " window=" + view + " content=" + contentLeft + "," + contentTop + "," + contentWidth + "x" + contentHeight
                    + " viewScale=" + gameViewScale + " dpiScale=" + dpiScale + " surface=" + Screen.width + "x" + Screen.height
                    + " hitWindow=" + hitWindow + " hitPid=" + hitProcess + " foreground=" + GetForegroundWindow());
                mouse_event(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
                yield return null;
                mouse_event(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
                yield return null;
            }

            private static void FocusGameView()
            {
                FocusMainWindow();
                Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (type == null) return;
                foreach (EditorWindow window in Resources.FindObjectsOfTypeAll(type))
                {
                    window.Focus();
                    window.Repaint();
                    return;
                }
            }

            private static void FocusMainWindow()
            {
                IntPtr window = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                if (window == IntPtr.Zero) return;
                uint foregroundProcess;
                uint foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out foregroundProcess);
                uint currentThread = GetCurrentThreadId();
                bool attached = foregroundThread != 0 && foregroundThread != currentThread
                    && AttachThreadInput(currentThread, foregroundThread, true);
                try
                {
                    ShowWindow(window, 9);
                    BringWindowToTop(window);
                    SetForegroundWindow(window);
                }
                finally
                {
                    if (attached) AttachThreadInput(currentThread, foregroundThread, false);
                }
            }

            private static Rect GameViewWindow()
            {
                Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (type == null) return new Rect(0, 0, 0, 0);
                foreach (EditorWindow window in Resources.FindObjectsOfTypeAll(type))
                    return window.position;
                return new Rect(0, 0, 0, 0);
            }

            private static float DesktopDpiScale()
            {
                IntPtr window = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                uint dpi = window == IntPtr.Zero ? 0 : GetDpiForWindow(window);
                return dpi > 0 ? dpi / 96f : 1f;
            }

            private const int MouseEventLeftDown = 0x0002;
            private const int MouseEventLeftUp = 0x0004;

            [DllImport("user32.dll")]
            private static extern bool SetCursorPos(int x, int y);

            [DllImport("user32.dll")]
            private static extern uint GetDpiForWindow(IntPtr hwnd);

            [DllImport("user32.dll")]
            private static extern int GetSystemMetrics(int index);

            [StructLayout(LayoutKind.Sequential)]
            private struct NativePoint { public int X, Y; }

            [DllImport("user32.dll")]
            private static extern IntPtr WindowFromPoint(NativePoint point);

            [DllImport("user32.dll")]
            private static extern IntPtr GetForegroundWindow();

            [DllImport("user32.dll")]
            private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

            [DllImport("kernel32.dll")]
            private static extern uint GetCurrentThreadId();

            [DllImport("user32.dll")]
            private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);

            [DllImport("user32.dll")]
            private static extern bool ShowWindow(IntPtr hwnd, int command);

            [DllImport("user32.dll")]
            private static extern bool BringWindowToTop(IntPtr hwnd);

            [DllImport("user32.dll")]
            private static extern bool SetForegroundWindow(IntPtr hwnd);

            [DllImport("user32.dll")]
            private static extern void mouse_event(int flags, int dx, int dy, int data, UIntPtr extraInfo);
        }

        private static void ConfigurePortraitGameView()
        {
            Assembly editor = typeof(UnityEditor.Editor).Assembly;
            Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance").GetValue(null);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes,
                new[] { Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeGroupType"), 1) });
            Type sizeType = editor.GetType("UnityEditor.GameViewSize");
            Type kind = editor.GetType("UnityEditor.GameViewSizeType");
            object size = Activator.CreateInstance(sizeType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { Enum.ToObject(kind, 1), (object)450, 800, "Wishes visible acceptance" }, null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
            Type viewType = editor.GetType("UnityEditor.GameView");
            EditorWindow view = EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(view, index);
        }

        private static void DeleteTemporaryRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) return;
            string fullPath = Path.GetFullPath(root);
            string tempPrefix = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(tempPrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing to remove a wishes acceptance path outside the temp directory.");
            if (Directory.Exists(fullPath)) Directory.Delete(fullPath, true);
        }
    }
}
