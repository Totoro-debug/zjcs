using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap12PlayModeAcceptance
    {
        public static void RunBuildingUi() => RunCore(false, false, verifyBuildingUi: true);

        public static void ImportAndRunBuildingUi()
        {
            try
            {
                OfficialMap12BuildingPreviewTools.Import();
                OfficialMap11UiTools.ImportForMap(12, new[] { "WorldFantasyLadderDialog", "6103_DialogIcon", "6302_DialogIcon" });
                RunCore(false, false, verifyBuildingUi: true);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }

        private sealed class BuildingOperationProbe : IWorldStageOperationContext
        {
            public readonly List<string> Events = new List<string>();
            public readonly Dictionary<int, bool?> Conditions = new Dictionary<int, bool?>();
            public Action StoryEnd;
            public Action<int> WindowClick;
            public WorldStageInteractionOperation[] Operations;
            public bool IsStageActive { get; set; } = true;
            public void ReportError(string message) => Events.Add("error:" + message);
            public WorldStageInteractionOperation ResolveOperation(long id) => id <= 0 ? null : Operations.Single(v => v.Id == id);
            public bool? Condition(int id) => Conditions.TryGetValue(id, out bool? value) ? value : null;
            public void StartStage(int subState) => Events.Add("stage:" + subState);
            public void PlayStory(int scenario, Action completed) { Events.Add("story:" + scenario); StoryEnd = completed; }
            public void OpenWindow(string name, bool report, int panelId, Action<int> click)
            {
                if (report || panelId != 0) throw new InvalidDataException("Original building window reporting changed.");
                Events.Add("window:" + name); WindowClick = click;
            }
        }

        private static void VerifyBuildingOperationGraphs()
        {
            var data = JsonUtility.FromJson<OfficialMap12BuildingData>(Resources.Load<TextAsset>("Map12/building-operations").text);
            if (data.manifest != "871_156598" || data.Buildings.Length != 2 || data.Conditions.Length != 5)
                throw new InvalidDataException("Original building operation closure is missing.");
            var iron = data.Buildings.Single(v => v.EntityClassId == 6103);
            var lift = data.Buildings.Single(v => v.EntityClassId == 6302);
            for (int mask = 0; mask < 32; mask++)
            {
                var probe = new BuildingOperationProbe { Operations = iron.Operations };
                for (int i = 0; i < 5; i++) probe.Conditions[4102 + i] = (mask & (1 << i)) != 0;
                probe.ResolveOperation(iron.States[0].OperationId).DoOper(probe);
                string expected = (mask & 1) == 0 ? "story:3" : (mask & 2) == 0 ? "story:1,stage:0" : "stage:0";
                if (string.Join(",", probe.Events) != expected) throw new InvalidDataException("IronMount original sequence/condition order: " + mask);
                probe.StoryEnd?.Invoke(); probe.Events.Clear();
                probe.ResolveOperation(iron.States[1].OperationId).DoOper(probe);
                expected = (mask & 14) == 0 ? "story:3" : "window:DailyDungeon/DailyDungeonDialogPrivateMap";
                if (string.Join(",", probe.Events) != expected) throw new InvalidDataException("IronMount active-story gate: " + mask);
                probe.StoryEnd?.Invoke(); probe.Events.Clear(); probe.StoryEnd = null;
                probe.Operations = lift.Operations;
                probe.ResolveOperation(lift.States[0].OperationId).DoOper(probe);
                expected = (mask & 16) == 0 ? "story:6" : "window:WorldMap/WorldFantasyLadderDialog";
                if (string.Join(",", probe.Events) != expected) throw new InvalidDataException("MountLift original preview gate: " + mask);
                if ((mask & 16) != 0)
                {
                    probe.Events.Clear(); probe.WindowClick(0);
                    if (string.Join(",", probe.Events) != "story:1") throw new InvalidDataException("MountLift preview click did not start the original scenario.");
                }
                probe.StoryEnd?.Invoke();
            }
            foreach (var building in data.Buildings)
                for (int state = building.EntityClassId == 6103 ? 2 : 1; state < building.States.Length; state++)
                {
                    var probe = new BuildingOperationProbe { Operations = building.Operations };
                    probe.ResolveOperation(building.States[state].OperationId).DoOper(probe);
                    string expected = state == building.CompleteState ? "WorldMap/StageEndTipUI"
                        : building.EntityClassId == 6103 ? "DailyDungeon/DailyDungeonDialog" : "DemonBattlefield/StageDemonBattlefieldDialog";
                    if (probe.Events.Single() != "window:" + expected) throw new InvalidDataException("Original public/completed building operation mismatch.");
                }
            Debug.Log("Map12 original building graph fixtures passed: all 32 condition combinations, synchronous story/click ordering, preview click and completed/public windows.");
        }

        private static IEnumerator VerifyBuildingUiSteps()
        {
            Application.runInBackground = true;
            VerifyBuildingOperationGraphs();
            foreach (MonoBehaviour startup in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>()
                .Where(c => c.GetType().FullName == "Zjcs.Offline.Startup.OfflineStartupFlow"))
            { startup.StopAllCoroutines(); UnityEngine.Object.Destroy(startup.gameObject); }
            foreach (Canvas boot in UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c => c.isRootCanvas && c.name.StartsWith("BootSplash")))
                UnityEngine.Object.Destroy(boot.gameObject);
            var data = JsonUtility.FromJson<OfficialMap11UiData>(Resources.Load<TextAsset>("Map12/UI/catalog").text);
            var operations = JsonUtility.FromJson<OfficialMap12BuildingData>(Resources.Load<TextAsset>("Map12/building-operations").text);
            var preview = operations.MountLiftRewardPreview(50, 6);
            if (preview.Count != 2 || preview[1] != 2490 || preview[110] != 1
                || operations.MountLiftRewardPreview(50, 0)[1] != 600
                || operations.MountLiftRewardPreview(50, 100)[1] != 72000)
                throw new InvalidDataException("Original stage-102 fixed reward scale/endpoint clamp changed.");
            var previousLocalize = WorldMultiLanguageText.Localize;
            WorldMultiLanguageText.Localize = key => data.texts.FirstOrDefault(v => v.key == key)?.value ?? key;
            if (EventSystem.current == null) new GameObject("Map12BuildingSimulatedInput", typeof(EventSystem));
            var camera = new GameObject("Map12BuildingUiCamera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 10; camera.backgroundColor = Color.black;
            string output = Path.GetFullPath("../.tools/map12-building-ui-render-session"); Directory.CreateDirectory(output);
            foreach (int width in new[] { 450, 720 })
            {
                int height = width == 450 ? 800 : 1280;
                SetUiGameViewSize(width, height);
                float deadline = Time.realtimeSinceStartup + 10;
                while (Screen.width != width || Screen.height != height)
                { if (Time.realtimeSinceStartup > deadline) throw new InvalidDataException("Building portrait resolution did not update."); yield return null; }
                var overlay = new GameObject("OriginalBuildingWindow", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                ConfigureCanvas(overlay, camera, data);
                var root = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Map12/UI/WorldFantasyLadderDialog"), overlay.transform, false);
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true)) canvas.worldCamera = camera;
                var events = new List<string>();
                var dialog = root.GetComponent<StageDialog>();
                var context = new WorldStageUiContext
                {
                    NameKey = "entity_6302_name", DescriptionKey = "entity_6302_desc", TouchButtonKey = "ui_challenge_text",
                    DialogIconPrefab = Resources.Load<GameObject>("Map12/UI/6302_DialogIcon"),
                    Background = AssetDatabase.LoadAssetAtPath<Sprite>(data.sprites.Single(v => v.uri.Contains("/map_12_stage_bg-")).unityAssetPath),
                    Level = 1, IsActive = () => true,
                    StartStage = index => events.Add("stage:" + index),
                    ShowRewardPreview = slot =>
                    {
                        events.Add("reward-slot:" + slot.childCount);
                        OfficialMap12BuildingRewardView.Populate(slot, operations, 50, 6,
                            Resources.Load<OfficialMap11UiAssets>("Map12/UI/DynamicItems"), "Map12/UI/", WorldMultiLanguageText.Localize);
                    },
                    Close = () => { events.Add("close"); root.GetComponent<WorldPopupWindow>().Hide(() => UnityEngine.Object.Destroy(root)); }
                };
                // Explicit preview arguments exercise the source formula. The
                // map's entity-level provider is verified by the runtime flow.
                dialog.Init(context);
                var rewardIcons = root.GetComponent<WorldCommonStageUI>().myAwardSlot.GetComponentsInChildren<WorldItemIcon>();
                if (rewardIcons.Length != 2 || rewardIcons[0].myText.text != "2490" || rewardIcons[1].myText.text != "1")
                    throw new InvalidDataException("Original money/item preview order and counts were not bound.");
                var graphics = root.GetComponentsInChildren<NativeMap12BuildingGraphic>(true);
                if (graphics.Length != 3 || graphics.Sum(v => v.setupMesh.vertexCount) != 144
                    || graphics.Any(v => v.canvasRenderer.cull || v.atlas.mipmapCount < 1))
                    throw new InvalidDataException("Original lift preview setup geometry/texture bindings are missing.");
                if (events.Single() != "reward-slot:0" || root.GetComponent<StageTouchUI>().myTouchBtn != null)
                    throw new InvalidDataException("Ladder original empty touch button / reward slot was not preserved.");
                events.Clear();
                var clicks = root.GetComponents<WorldStageOperationWindowClickUi>();
                if (clicks.Length != 2) throw new InvalidDataException("Both original window click components must be retained.");
                clicks[0].InitWindowClick(type => events.Add("window-click:" + type));
                root.GetComponent<WorldPopupWindow>().Show();
                yield return new WaitForSecondsRealtime(.6f); yield return new WaitForEndOfFrame();
                if (graphics.Any(g => g.canvasRenderer.cull || g.canvasRenderer.GetMesh() == null))
                    throw new InvalidDataException("Original lift preview geometry was culled before rendering.");
                Texture2D visible = ScreenCapture.CaptureScreenshotAsTexture();
                foreach (var graphic in graphics) graphic.enabled = false;
                yield return new WaitForEndOfFrame();
                Texture2D hidden = ScreenCapture.CaptureScreenshotAsTexture();
                Color32[] before = visible.GetPixels32(), after = hidden.GetPixels32(); int changed = 0;
                for (int y = (int)(visible.height * .56f); y < visible.height * .87f; y++)
                    for (int x = visible.width / 10; x < visible.width * 9 / 10; x++)
                    {
                        int index = y * visible.width + x;
                        if (Math.Abs(before[index].r - after[index].r) + Math.Abs(before[index].g - after[index].g)
                            + Math.Abs(before[index].b - after[index].b) > 24) changed++;
                    }
                UnityEngine.Object.Destroy(visible); UnityEngine.Object.Destroy(hidden);
                foreach (var graphic in graphics) graphic.enabled = true;
                if (changed < 1000) throw new InvalidDataException("Original lift preview did not draw visible pixels: " + changed);
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output, width + "-ladder.png"));
                SimulateUiClick(root.GetComponent<WorldCommonStageUI>().myBattleBtn, camera);
                if (string.Join(",", events) != "stage:0,close,window-click:0")
                    throw new InvalidDataException("Ladder native battle/close/first-window-click listener order: " + string.Join(",", events));
                yield return new WaitForSecondsRealtime(.3f);
                if (root != null) throw new InvalidDataException("Ladder original close animation did not finish.");
                UnityEngine.Object.Destroy(overlay); yield return null;
            }
            WorldMultiLanguageText.Localize = previousLocalize;
            Debug.Log("Map12 original ladder UI fixture passed: 450x800/720x1280, original icon and serialized references, simulated raycast/button input, battle/close/window click order. Real battles, rewards and building state persistence remain separate.");
        }
    }
}
