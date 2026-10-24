using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Presentation.Home;

namespace Zjcs.Editor.TreasureGacha
{
    [InitializeOnLoad]
    public static class TreasureGachaPlayModeProbe
    {
        private const string ActiveKey = "Zjcs.TreasureGacha.Probe.Active";
        private const string RootKey = "Zjcs.TreasureGacha.Probe.Root";
        private const string SlotKey = "Zjcs.TreasureGacha.Probe.Slot";
        private const string OutputKey = "Zjcs.TreasureGacha.Probe.Output";

        static TreasureGachaPlayModeProbe()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Zjcs/Validate/Treasure Gacha Play Mode Probe")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before starting the treasure gacha probe.");

            ConfigurePortraitGameView();
            string root = Path.Combine(Path.GetTempPath(), "zjcs-treasure-gacha-probe-" + Guid.NewGuid().ToString("N"));
            string output = Path.Combine(root, "captures");
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(output);
            var slots = new ProfileSlotCatalog(root);
            string slotId = slots.CreateSlot();
            OfflineMainlineProgressSession profile = OfflineMainlineProgressSession.OpenCharacter(slots, slotId);
            TreasureGachaRuntimeCatalog catalog = OfficialTreasureGachaAssets.LoadDefault();
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            ProgressionResult result = profile.ApplyCharacterMutation(player =>
            {
                player.CharacterCreated = true;
                player.CharacterName = "扭蛋 Play Mode 验证";
                player.Level = 99;
                player.Rank = 99;
                player.SubRank = 3;
                player.MaxProfessionRank = 99;
                foreach (TreasureGachaSeries series in catalog.Series)
                    if (!player.UnlockedMapClassIds.Contains(series.MapClassId))
                        player.UnlockedMapClassIds.Add(series.MapClassId);
                foreach (TreasureCondition condition in catalog.Conditions)
                    if (condition.Type == "MapUnlockCondition" && condition.MapClassId > 0
                        && !player.UnlockedMapClassIds.Contains(condition.MapClassId))
                        player.UnlockedMapClassIds.Add(condition.MapClassId);
                for (int rank = 1; rank <= player.Rank; rank++)
                    if (!player.RankUnlockTimes.Any(value => value.Rank == rank))
                        player.RankUnlockTimes.Add(new OfflineRankUnlockTime
                        { Rank = rank, UnixSeconds = now - 400L * 86400 });
                AddItem(player, 1, 100000);
                AddItem(player, 2, 100000);
                AddItem(player, 301, 100000);
            }, "acceptance:treasure-gacha-probe:" + slotId);
            if (result.Status != ProgressionResultStatus.Applied)
                throw new InvalidOperationException("Could not prepare the isolated treasure gacha profile: " + result.Status);

            SessionState.SetString(RootKey, root);
            SessionState.SetString(SlotKey, slotId);
            SessionState.SetString(OutputKey, output);
            SessionState.SetBool(ActiveKey, true);
            Debug.Log("[TreasureGachaProbe] entering Play Mode with isolated profile " + slotId);
            EditorApplication.isPlaying = true;
        }

        private static void AddItem(OfflinePlayerProgress player, int itemId, long count)
        {
            OfflineItemStack stack = player.Inventory.FirstOrDefault(value => value.ItemId == itemId);
            if (stack == null) player.Inventory.Add(stack = new OfflineItemStack { ItemId = itemId });
            stack.Count = Math.Max(stack.Count, count);
        }

        private static void ConfigurePortraitGameView()
        {
            Assembly editor = typeof(UnityEditor.Editor).Assembly;
            Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance").GetValue(null);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[]
            {
                Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeGroupType"), 1),
            });
            Type sizeType = editor.GetType("UnityEditor.GameViewSize");
            Type kind = editor.GetType("UnityEditor.GameViewSizeType");
            object size = Activator.CreateInstance(sizeType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { Enum.ToObject(kind, 1), 720, 1280, "Treasure gacha acceptance" }, null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
            Type viewType = editor.GetType("UnityEditor.GameView");
            EditorWindow view = EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(view, index);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                var host = new GameObject("TreasureGachaPlayModeProbe");
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.AddComponent<TreasureGachaPlayModeRunner>();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(ActiveKey, false);
            }
        }
    }

    public sealed class TreasureGachaPlayModeRunner : MonoBehaviour
    {
        private const string RootKey = "Zjcs.TreasureGacha.Probe.Root";
        private const string SlotKey = "Zjcs.TreasureGacha.Probe.Slot";
        private const string OutputKey = "Zjcs.TreasureGacha.Probe.Output";
        private HomeFeatureRuntime runtime;
        private string output;

        private IEnumerator Start()
        {
            bool initialized = false;
                int sourceItemId = 0;
            try
            {
                string root = SessionState.GetString(RootKey, string.Empty);
                string slot = SessionState.GetString(SlotKey, string.Empty);
                output = SessionState.GetString(OutputKey, string.Empty);
                if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(slot) || string.IsNullOrEmpty(output))
                    throw new InvalidOperationException("The isolated profile information is missing.");
                HideForeignCanvases();
                runtime = HomeFeatureRuntime.Begin(slot, root, null, null);
                LogCardSourceNodes(runtime.Catalog.Panel("treasure_gacha_main")?.root);
                TreasureGachaSeries sourceSeries = runtime.TreasureGachaCatalog.SortedSeries()
                    .FirstOrDefault(value => value.ChestGachaItems.Length > 0
                        && runtime.TreasureGachaCatalog.IsSeriesOpen(value, runtime.Profile.Save.Player,
                            DateTimeOffset.Now.LocalDateTime, runtime.Profile.Save.CreatedAtUnixSeconds, out _));
                if (sourceSeries == null)
                    throw new InvalidOperationException("The source navigation probe has no open relic series.");
                sourceItemId = sourceSeries.ChestGachaItems[0];
                bool returnedToSource = false;
                TreasureGachaSourceNavigation navigation = runtime.OpenTreasureGachaFromItemSource(
                    sourceItemId, returnToSource: _ => returnedToSource = true);
                if (navigation == null || navigation.SelectedSeriesId != sourceSeries.ChestGachaId
                    || runtime.Hud.TreasureGachaSourceItemId != sourceItemId)
                    throw new InvalidOperationException("The item-source route did not preserve its item and series context.");
                runtime.Hud.CloseDialog();
                if (!returnedToSource)
                    throw new InvalidOperationException("Closing the source-opened gacha did not return to its source context.");
                initialized = true;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.isPlaying = false;
            }
            if (!initialized) yield break;

            yield return null;
            bool elementRouteVerified = false;
            try
            {
                TreasureGachaConfig elementGacha = runtime.TreasureGachaCatalog.Gachas
                    .First(value => value.CountAchievementType == 8);
                int elementSourceItemId = runtime.TreasureGacha.ProbabilityItems(elementGacha.GachaType, 0,
                    DateTimeOffset.Now.ToUnixTimeSeconds()).First().ItemId;
                int sourceElementType = runtime.TreasureGachaCatalog.Item(elementSourceItemId).ElementType;
                bool returnedFromElementSource = false;
                TreasureGachaSourceNavigation elementNavigation = runtime.OpenTreasureGachaFromElementItemSource(
                    elementSourceItemId, sourceElementType, _ => returnedFromElementSource = true);
                GameObject elementPanel = GameObject.Find("ChestGachaPanel")?.transform
                    .Find("BgPanel1/Content/MiddleGo/ElementChestPanel")?.gameObject;
                if (elementNavigation == null || elementPanel == null || !elementPanel.activeSelf
                    || runtime.TreasureGacha.State.SelectedElementGachaType != elementGacha.GachaType)
                    throw new InvalidOperationException("The element item-source route did not select and show its gacha type. navigation="
                        + (elementNavigation == null ? "null" : elementNavigation.SelectedSeriesId.ToString())
                        + ", panel=" + (elementPanel == null ? "missing" : elementPanel.activeSelf.ToString())
                        + ", selected=" + runtime.TreasureGacha.State.SelectedElementGachaType);
                runtime.Hud.CloseDialog();
                if (!returnedFromElementSource)
                    throw new InvalidOperationException("Closing the element source route did not return to its source context.");
                elementRouteVerified = true;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.isPlaying = false;
            }
            if (!elementRouteVerified) yield break;

            yield return null;
            bool mainOpened = false;
            try
            {
                runtime.Hud.ShowTreasureGacha();
                Debug.Log("[TreasureGachaProbe] precious/element source navigation and return callbacks passed");
                mainOpened = true;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.isPlaying = false;
            }
            if (!mainOpened) yield break;

            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(1f);
            try
            {
                if (!runtime.Hud.AnyModalOpen)
                    throw new InvalidOperationException("The treasure gacha dialog did not open in Play Mode.");
                string initial = Path.Combine(output, "treasure-gacha-main.png");
                ScreenCapture.CaptureScreenshot(initial);
                Debug.Log("[TreasureGachaProbe] treasure gacha dialog opened; screenshot=" + initial
                    + "; press F8 to capture current result and F10 to leave Play Mode.");
                LogPanelState("initial");
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.isPlaying = false;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F8) && !string.IsNullOrEmpty(output))
            {
                string path = Path.Combine(output, "treasure-gacha-current.png");
                ScreenCapture.CaptureScreenshot(path);
                Debug.Log("[TreasureGachaProbe] screenshot=" + path);
                LogPanelState("current");
            }
            if (Input.GetKeyDown(KeyCode.F10)) EditorApplication.isPlaying = false;
        }

        private static void HideForeignCanvases()
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

        private static void LogPanelState(string phase)
        {
            GameObject panel = GameObject.Find("ChestGachaPanel");
            if (panel == null)
            {
                Debug.LogError("[TreasureGachaProbe] ChestGachaPanel is not in the active hierarchy.");
                return;
            }
            LogNode(panel, phase, "BgPanel1/Content/Mask/GachaponMachine/BgBackSlot/Back11Bg/Map11Img");
            LogNode(panel, phase, "BgPanel1/Content/Mask/GachaponMachine/BgBackSlot/Back11Bg/ImgGround");
            LogNode(panel, phase, "BgPanel1/Content/Mask/GachaponMachine/BgBackSlot/Back18Bg/Map11Img");
            LogNode(panel, phase, "BgPanel1/Content/Mask/GachaponMachine/BgBackSlot/Back18Bg/ImgGround");
            LogNode(panel, phase, "BgPanel1/Content/Mask/GachaponMachine/BgSlot/Bg/ElementImg");
            LogNode(panel, phase, "BgPanel1/Content/Mask/GachaponMachine/BgSlot/Bg/ImgGround");
            LogNode(panel, phase, "BgPanel1/Content/Mask/GachaponMachine/MachineGo/BodySpine1");
            LogNode(panel, phase, "BgPanel1/Content/Mask/GachaponMachine/MachineGo/BodySpine2");
            LogNode(panel, phase, "BgPanel1/Content/Mask/GachaponMachine/MachineGo/GachaponInsideSlot");
            LogNode(panel, phase, "BgPanel1/Content/TopGo/PreviewBubble/Content/Text");
            LogNode(panel, phase, "BgPanel1/Content/MiddleGo/PreciousChestPanel/ChestGo/BlueCard/Panel (1)");
            LogNode(panel, phase, "BgPanel1/Content/MiddleGo/PreciousChestPanel/ChestGo/PurpleCard/Panel (2)");
            LogNode(panel, phase, "BgPanel1/Content/MiddleGo/PreciousChestPanel/ChestGo/OrangeCard/Panel (3)");
            LogNode(panel, phase, "BgPanel1/Content/MiddleGo/PreciousChestPanel/ChestGo/GoldCard/Panel (4)");
            LogNode(panel, phase, "BgPanel1/Content/MiddleGo/PreciousChestPanel/ChestGo/BlueCard/Panel (1)/ChestImg");
            LogNode(panel, phase, "BgPanel1/Content/MiddleGo/PreciousChestPanel/ChestGo/PurpleCard/Panel (2)/ChestImg");
            LogNode(panel, phase, "BgPanel1/Content/MiddleGo/PreciousChestPanel/ChestGo/OrangeCard/Panel (3)/ChestImg");
            LogNode(panel, phase, "BgPanel1/Content/MiddleGo/PreciousChestPanel/ChestGo/GoldCard/Panel (4)/ChestImg");
            LogNode(panel, phase, "BgPanel1/Content/BottomGo/TabGroup/TabGroup/ChestGachaPanelPreciousChestTabBottomBtn");
            LogNode(panel, phase, "BgPanel1/Content/BottomGo/TabGroup/TabGroup/ChestGachaPanelElementChestTabBottomBtn");
            LogNode(panel, phase, "BgPanel1/Content/BottomGo/TabGroup/TabGroup/AchievementTabBottomBtn");
        }

        private static void LogCardSourceNodes(HomeUiNode root)
        {
            string[] paths =
            {
                "/ChestGachaPanel/BgPanel1/Content/MiddleGo/PreciousChestPanel/ChestGo/BlueCard/Panel (1)",
                "/ChestGachaPanel/BgPanel1/Content/MiddleGo/PreciousChestPanel/ChestGo/BlueCard/Panel (1)/ChestImg",
                "/ChestGachaPanel/BgPanel1/Content/MiddleGo/PreciousChestPanel/ChestGo/PurpleCard/Panel (2)/ChestImg",
            };
            foreach (string path in paths)
            {
                HomeUiNode node = FindSourceNode(root, path);
                Debug.Log("[TreasureGachaProbe] source node=" + path
                    + " found=" + (node != null)
                    + " scripts=" + (node == null ? "none" : string.Join(",", node.scripts ?? Array.Empty<string>()))
                    + " sprite=" + (node?.image?.sprite?.file ?? "none")
                    + " color=" + (node?.image?.color == null ? "none" : string.Join(",", node.image.color)));
            }
        }

        private static HomeUiNode FindSourceNode(HomeUiNode root, string path)
        {
            if (root == null) return null;
            if (root.path == path) return root;
            foreach (HomeUiNode child in root.children ?? Array.Empty<HomeUiNode>())
            {
                HomeUiNode found = FindSourceNode(child, path);
                if (found != null) return found;
            }
            return null;
        }

        private static void LogNode(GameObject root, string phase, string path)
        {
            Transform node = root.transform.Find(path);
            if (node == null)
            {
                Debug.LogError("[TreasureGachaProbe] " + phase + " missing node " + path);
                return;
            }
            RectTransform rect = node as RectTransform;
            Image image = node.GetComponent<Image>();
            TreasureGachaMeshGraphic mesh = node.GetComponent<TreasureGachaMeshGraphic>();
            Button button = node.GetComponent<Button>();
            Graphic graphic = node.GetComponent<Graphic>();
            CanvasRenderer canvasRenderer = node.GetComponent<CanvasRenderer>();
            Vector2 point = rect == null ? Vector2.zero : RectTransformUtility.WorldToScreenPoint(null, rect.position);
            Debug.Log("[TreasureGachaProbe] " + phase + " node=" + path
                + " active=" + node.gameObject.activeInHierarchy
                + " rect=" + (rect == null ? "none" : rect.rect.ToString())
                + " screen=" + point
                + " sprite=" + (image == null || image.sprite == null ? "none" : image.sprite.name)
                + " imageColor=" + (image == null ? "none" : image.color.ToString())
                + " spriteTexture=" + (image == null || image.sprite == null || image.sprite.texture == null
                    ? "none" : image.sprite.texture.name + ":" + image.sprite.texture.width + "x" + image.sprite.texture.height)
                + " imageEnabled=" + (image != null && image.enabled)
                + " graphic=" + (graphic == null ? "none" : graphic.GetType().Name
                    + ":depth=" + graphic.depth
                    + ":cull=" + (canvasRenderer != null && canvasRenderer.cull)
                    + ":texture=" + (graphic.mainTexture == null ? "none" : graphic.mainTexture.name))
                + " mesh=" + DescribeMesh(mesh)
                + " button=" + (button != null));
        }

        private static string DescribeMesh(TreasureGachaMeshGraphic mesh)
        {
            if (mesh == null) return "none";
            Type type = typeof(TreasureGachaMeshGraphic);
            object animation = type.GetField("animationData", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(mesh);
            object data = type.GetField("data", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mesh);
            int frameIndex = (int)type.GetField("currentFrame", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(mesh);
            if (animation == null || data == null) return "frame=" + frameIndex + ":unbound";
            Array frames = (Array)animation.GetType().GetField("frames").GetValue(animation);
            if (frameIndex < 0 || frameIndex >= frames.Length) return "frame=" + frameIndex + ":out-of-range";
            object frame = frames.GetValue(frameIndex);
            Array parts = (Array)frame.GetType().GetField("parts").GetValue(frame);
            Array geometry = (Array)data.GetType().GetField("geometry").GetValue(data);
            int vertices = 0;
            foreach (object part in parts)
            {
                int meshIndex = (int)part.GetType().GetField("mesh").GetValue(part);
                object geometryPart = geometry.GetValue(meshIndex);
                float[] values = (float[])geometryPart.GetType().GetField("vertices").GetValue(geometryPart);
                vertices += values.Length / 2;
            }
            return "frame=" + frameIndex + ":parts=" + parts.Length + ":vertices=" + vertices;
        }
    }
}
