#if UNITY_EDITOR || DEVELOPMENT_BUILD || ZJCS_MAINLINE_MODULE_VALIDATION
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.CharacterCreation;

namespace Zjcs.Presentation.World
{
    /// <summary>Isolated regression of the shared modules in both Play Mode and a Windows development Player.</summary>
    public sealed class OfficialMainlineMapModuleProbe : MonoBehaviour
    {
        public const string Argument = "-verify-mainline-modules";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private OfficialMainlineMapRuntime runtime;
        private string output, root, slot;
        private Action<bool> completed;
        private bool finished;
        private float deadline;
        private Vector2? press;
        private Vector3? worldPress;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, Argument);
            if (index < 0) return;
            if (index + 1 >= args.Length) throw new ArgumentException(Argument + " requires an output directory.");
            StartVerification(args[index + 1], success =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit(success ? 0 : 1);
#endif
            });
        }

        public static void StartVerification(string outputDirectory, Action<bool> onCompleted)
        {
            Application.runInBackground = true;
            var probe = new GameObject("SharedMainlineModuleProbe").AddComponent<OfficialMainlineMapModuleProbe>();
            DontDestroyOnLoad(probe.gameObject);
            probe.output = Path.GetFullPath(outputDirectory);
            probe.root = Path.Combine(probe.output, "isolated-profile");
            probe.completed = onCompleted;
            probe.deadline = Time.realtimeSinceStartup + 240f;
            Directory.CreateDirectory(probe.output);
            Application.logMessageReceived += probe.LogFailure;
            probe.StartCoroutine(probe.Guarded(probe.Verify()));
        }

        private void LogFailure(string message, string stack, LogType type)
        {
            if (finished || type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            File.WriteAllText(Path.Combine(output, "failed.txt"), message + Environment.NewLine + stack);
            Complete(false);
        }
        private void OnDestroy() => Application.logMessageReceived -= LogFailure;

        private void Update()
        {
            if (finished || Time.realtimeSinceStartup < deadline) return;
            File.WriteAllText(Path.Combine(output, "failed.txt"), "Shared map probe exceeded its 240 second deadline.");
            Complete(false);
        }

        private IEnumerator Guarded(IEnumerator sequence)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(sequence);
            while (stack.Count > 0)
            {
                object next;
                try
                {
                    if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                    next = stack.Peek().Current;
                }
                catch (Exception error)
                {
                    Debug.LogException(error);
                    File.WriteAllText(Path.Combine(output, "failed.txt"), error.ToString());
                    Complete(false);
                    yield break;
                }
                if (next is IEnumerator child) stack.Push(child);
                else yield return next;
            }
            File.WriteAllText(Path.Combine(output, "passed.txt"),
                Environment.GetCommandLineArgs().Contains("-verify-exploration-only")
                    ? "Exploration pointer flow: native HUD, confirmation, probability/detail/history, token scrolling/selection/cost, guide chest reward without stamina, ten-times exploration and cold reload passed."
                    : "Map10/11 shared runtime: region conditions, pointer navigation, HUD, minimap tabs/filter, goddess activation, hero UI/permanent rewards/daily effects, stamina gathering/award animation/respawn, exploration and cold save reload passed.");
            Debug.Log("Shared mainline map modules passed: " + output);
            Complete(true);
        }

        private IEnumerator Verify()
        {
            yield return null;
            // Editor startup may already have mounted the normal opening screen.
            foreach (string name in new[] { "CharacterCreationFlow", "CharacterCreationCanvas" })
            {
                GameObject startup = GameObject.Find(name);
                if (startup != null) Destroy(startup);
            }
            yield return null;
            if (EventSystem.current == null) new GameObject("ProbeInput", typeof(EventSystem), typeof(StandaloneInputModule));
            var canvas = new GameObject("ProbeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = .5f;
            var slots = new ProfileSlotCatalog(root);
            slot = slots.CreateSlot();
            var save = new OfflineMainlineSave { CreatedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            save.Player.CharacterCreated = true;
            save.Player.CharacterName = "公共地图验证";
            save.Player.InitialWeaponType = 200;
            save.Player.Level = 31;
            save.Player.Experience = OfficialCharacterAssetCatalog.LoadDefault().Load().CumulativeLevelExperience[31];
            save.Player.Rank = save.Player.SubRank = save.Player.SeasonRank = 1;
            save.Player.SubRank = OfficialCharacterAssetCatalog.LoadDefault().Load().LevelLimits
                .Where(value => value.PlayerLevel >= save.Player.Level).Min(value => value.SubRank);
            save.Player.Wardrobe.Profession = 1;
            save.Tasks.ClaimedMainlineTaskIds.AddRange(OfficialMainlineTaskCatalog.LoadDefault().OrderedTasks
                .Where(value => value.classId <= 1040).Select(value => value.classId));
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save));
            var progress = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            new OfflineCharacterProgressionService(progress, OfficialCharacterAssetCatalog.LoadDefault().Load())
                .InitializeWardrobe("shared-map-probe-wardrobe");
            if (Environment.GetCommandLineArgs().Contains("-verify-exploration-only"))
            {
                Open(11, canvas.transform);
                yield return new WaitForSecondsRealtime(1);
                progress = Field<OfflineMainlineProgressSession>("progressionSave");
                foreach (int region in new[] { 0, 1, 12 })
                {
                    progress.SetMapRegionProgress(11, region, 10000);
                    Require(progress.RecordGoddessActivated(11, region, "probe-explore-public-" + region).IsApplied,
                        "Public exploration region fixture");
                }
                Invoke("RefreshEntityActivity");
                yield return VerifyExploration();
                Destroy(runtime.gameObject);
                yield return null;
                Open(11, canvas.transform);
                yield return new WaitForSecondsRealtime(1);
                Require(Field<OfflineMainlineProgressSession>("progressionSave").Save.World.Exploration.TotalCount == 12,
                    "Pointer exploration result survives a cold runtime reload");
                yield return Capture("map-11-explore-cold");
                Destroy(runtime.gameObject);
                Destroy(canvas);
                yield break;
            }
            File.WriteAllText(Path.Combine(slots.GetSlotDirectory(slot), "map-10-save.json"),
                JsonUtility.ToJson(new Map10LocalSave { slotId = slot, completedStories = new[] { "FirstEnterMap", "FirstBattle" } }));

            foreach (int id in new[] { 10, 11 })
            {
                Open(id, canvas.transform);
                yield return new WaitForSecondsRealtime(1);
                var session = Field<OfficialWorldMapSession>("session");
                var policy = new OfficialMainlineRegionPolicy(session.Definition, OfficialMainlineMapProfile.Get(id));
                var points = new Dictionary<int, int>();
                var gate = policy.regionConfigs.Values.First(value => value.AccessibleConditionId == (id == 10 ? 1201 : 1205));
                Require(!policy.IsAccessible(gate.Region, points, _ => 0), "Incomplete source region remains locked");
                points[0] = policy.regionPointLimits[0];
                Require(policy.IsAccessible(gate.Region, points, _ => 0), "Source region at 100% opens its dependent region");
                if (id == 11)
                {
                    var storyGate = policy.regionConfigs.Values.First(value => value.AccessibleConditionId == 4003);
                    Require(!policy.IsAccessible(storyGate.Region, points, _ => 0)
                        && policy.IsAccessible(storyGate.Region, points, key => key == "SenSenThanks" ? 1 : 0), "Story gate follows the pinned story count");
                }
                Require(Field<OfficialWorldUiRenderer>("ui").Find("WorldCanvas", "/WorldCanvas").activeInHierarchy, "Map HUD mounted");
                yield return VerifyPointerMovement(session);
                yield return Capture("map-" + id + "-hud");
                if (id == 11)
                {
                    yield return VerifyNativeUi();
                    yield return VerifyHeroStatues();
                    yield return VerifyStaminaGathering();
                    yield return VerifyExploration();
                }
                MapGridPoint position = Field<MapGridPoint>("currentGrid");
                Invoke("SaveLocalProgress");
                Destroy(runtime.gameObject);
                yield return null;
                Open(id, canvas.transform);
                Require(Field<MapGridPoint>("currentGrid").Equals(position), "Map position survives a cold runtime reload");
                if (id == 11)
                {
                    Require(Field<OfflineMainlineProgressSession>("progressionSave").Save.World.ActivatedGoddessRegions.Count == 3,
                        "Goddess activation survives a cold reload");
                    Require(Field<OfficialMainlineRegionFog>("regionFog").VisibleShadowCount == 70,
                        "Cold map entry keeps all three public region shadows cleared");
                }
                Destroy(runtime.gameObject);
                yield return null;
            }
            progress = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            Require(progress.ApplyStatueVisit("20261025", "witch", 11, 2801, "probe-witch", -1f, new[] { 28500 },
                Array.Empty<OfflineItemStack>(), 1).IsApplied, "Witch visit transaction");
            Require(progress.ApplyStatueVisit("20261025", "hero", 10, 9002, "probe-hero", 0f, Array.Empty<int>(),
                Array.Empty<OfflineItemStack>(), 1).Status == ProgressionResultStatus.NotReady, "Hero and witch share one daily visit");
            progress = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            Require(progress.HasVisitedStatueOn("20261025") && progress.Save.Player.PendingStatueBattleEffects.Count == 3,
                "Daily visit and battle effects survive cold reload");
            Destroy(canvas);
        }

        private void Open(int id, Transform canvas)
        {
            GameObject model = CharacterCreationNativeModelFactory.CreateWorldModel(slot, root, out string name);
            runtime = new GameObject("SharedMap" + id).AddComponent<OfficialMainlineMapRuntime>();
            runtime.MapMusicVolume = runtime.MapVoiceVolume = () => 0;
            runtime.PointerPressProvider = () =>
            {
                Vector2? result = worldPress.HasValue ? (Vector2?)Field<Camera>("worldCamera").WorldToScreenPoint(worldPress.Value) : press;
                press = null;
                worldPress = null;
                return result;
            };
            runtime.Begin(id, slot, root, canvas, model, name, null);
        }

        private IEnumerator VerifyPointerMovement(OfficialWorldMapSession session)
        {
            MapGridPoint start = Field<MapGridPoint>("currentGrid");
            var camera = Field<Camera>("worldCamera");
            var entities = runtime.GetComponentInChildren<OfficialMap10EntityView>();
            foreach (MapGridPoint offset in new[] { new MapGridPoint(1, 0), new MapGridPoint(-1, 0), new MapGridPoint(0, 1), new MapGridPoint(0, -1) })
            {
                MapGridPoint target = start + offset;
                if (!session.Movement.IsFit(target) || !session.Movement.CanMoveToNext(start, target)) continue;
                Vector3 world = (Vector3)OfficialMainlineMapInspection.Call((OfficialMainlineMapRuntime)runtime, "GridToWorld", new object[] { target });
                Vector2 screen = camera.WorldToScreenPoint(world);
                if (Hits(screen).Count != 0 || Physics2D.OverlapPoint(world) != null || entities.EntityInfos.Any(value => value.Active
                    && value.Grid[0] == target.X && value.Grid[1] == target.Y)) continue;
                worldPress = world;
                float deadline = Time.realtimeSinceStartup + 5;
                while (!Field<MapGridPoint>("currentGrid").Equals(target) && Time.realtimeSinceStartup < deadline) yield return null;
                MapGridPoint actual = Field<MapGridPoint>("currentGrid");
                Require(actual.Equals(target), "Simulated map press expected " + target.X + "," + target.Y + "; got " + actual.X + "," + actual.Y);
                Debug.Log("Shared map pointer moved to " + target.X + "," + target.Y);
                yield break;
            }
            throw new InvalidOperationException("No visible unoccupied neighbor for pointer navigation regression.");
        }

        private IEnumerator VerifyNativeUi()
        {
            var data = JsonUtility.FromJson<OfficialMap11UiData>(Resources.Load<TextAsset>("Map11/UI/catalog").text);
            var mini = Field<GameObject>("nativeMiniMap");
            string buttonPath = data.components.Single(value => value.panel == "MiniMapMaskedUI" && value.type == "MiniMapMaskedUI").fields.MiniMapBtn;
            yield return Click(mini.transform.Find(buttonPath.Substring("/MiniMapMaskedUI/".Length)).GetComponent<Button>());
            Require(Field<GameObject>("fullMiniMapPanel") != null, "Pointer opens the original full minimap");
            var tabs = Field<Dictionary<int, GameObject>>("nativeMiniMapTabs");
            yield return Click(tabs[10].GetComponent<Button>());
            Require(Field<int>("fullMiniMapClassId") == 10 && !Field<GameObject>("nativeMiniMapCharacter").activeSelf, "Pointer switches to visited Map10");
            yield return Click(tabs[11].GetComponent<Button>());
            yield return Capture("map-11-minimap");
            var fields = data.components.Single(value => value.panel == "MiniMapFullScreenPanel" && value.type == "MiniMapMarkUIFilter").fields;
            var full = Field<GameObject>("fullMiniMapPanel");
            yield return Click(full.transform.Find(fields.mEntryBtn.Substring("/MiniMapFullScreenPanel/".Length)).GetComponent<Button>());
            var filters = Field<Dictionary<string, bool>>("miniMapFilters");
            bool before = filters["MiniMapGoddess"];
            var items = Field<Dictionary<string, GameObject>>("miniMapFilterItems");
            yield return Click(items["MiniMapGoddess"].GetComponentInChildren<Button>(true));
            Require(filters["MiniMapGoddess"] != before, "Pointer changes the original mark filter");
            // Close through the original button; reopening cold-reads the durable selection.
            Invoke("SetMiniMapFilterExpanded", false);
            yield return Click(full.transform.Find("Content/CloseBtn_dark").GetComponent<Button>());
            filters.Clear();
            typeof(OfficialMainlineMapRuntime).GetNestedType("OfficialMainlineMiniMapModule", BindingFlags.NonPublic)
                .GetField("miniMapFilterState", Private).SetValue(Field<object>("miniMap"), null);
            yield return Click(mini.transform.Find(buttonPath.Substring("/MiniMapMaskedUI/".Length)).GetComponent<Button>());
            Require(filters["MiniMapGoddess"] != before, "Minimap filter cold-reads from its profile save file");
            yield return Click(Field<GameObject>("fullMiniMapPanel").transform.Find("Content/CloseBtn_dark").GetComponent<Button>());

            var progress = Field<OfflineMainlineProgressSession>("progressionSave");
            var entities = Field<IDictionary>("mapEntities");
            object goddess = entities[202]; // pinned Map11 lost goddess placement
            progress.SetMapRegionProgress(11, 1, 10000);
            var goddessGrid = (MapGridPoint)goddess.GetType().GetField("Grid").GetValue(goddess);
            Invoke("SetAvatarPosition", goddessGrid + new MapGridPoint(0, 1));
            yield return new WaitForSecondsRealtime(1);
            var fog = Field<OfficialMainlineRegionFog>("regionFog");
            Require(fog != null && fog.ShadowCount == 73, "All native region light meshes are mounted");
            int shadows = fog.VisibleShadowCount;
            yield return Capture("map-11-region-fog");
            Invoke("ShowGoddessDialog", goddess); // dialog fixture; activation itself uses pointer events
            yield return new WaitForSecondsRealtime(.5f);
            yield return Capture("map-11-goddess");
            yield return Click(Field<GoddessTouchDialog>("goddessDialog").myTouchBtn);
            Require(progress.Save.World.ActivatedGoddessRegions.Count == 1, "Pointer activates the original goddess stage");
            yield return new WaitForSecondsRealtime(.7f);
            yield return Capture("map-11-region-fog-clearing");
            yield return new WaitForSecondsRealtime(1.5f);
            Require(fog.VisibleShadowCount == shadows - 1, "Native region shadow disappears after its original animation duration");
            yield return Capture("map-11-region-fog-cleared");
        }

        private IEnumerator VerifyHeroStatues()
        {
            var entities = Field<IDictionary>("mapEntities");
            object hero = entities[1401];
            var progress = Field<OfflineMainlineProgressSession>("progressionSave");
            Invoke("InteractHero", hero);
            yield return new WaitForSecondsRealtime(.5f);
            var window = Field<OfficialMap11WitchWindow>("witchDialog");
            yield return Capture("map-11-hero-touch");
            yield return Click(window.Find(window.Source("StageUIWithPreview").fields.myTouchBtn).GetComponent<Button>());
            window = Field<OfficialMap11WitchWindow>("witchDialog");
            yield return new WaitForSecondsRealtime(1);
            var previewText = window.Root.GetComponentsInChildren<TMPro.TMP_Text>().Single(value => value.text == "生命+170");
            Require(previewText.rectTransform.rect.height > 0 && previewText.isActiveAndEnabled,
                "Native hero property preview has visible layout");
            yield return Capture("map-11-hero-first-reward");
            yield return Click(window.Find(window.Source("WorldStageInteractionPreviewDialog").fields.mCloseBtn).GetComponent<Button>());
            Require(progress.Save.Player.CharacterProperties.ExtraProperties.Count(value => value.SourceId == "hero-activation:11:1401") == 1,
                "Hero activation atomically grants its permanent original property");
            Invoke("InteractHero", hero);
            Require(Field<OfficialMap11WitchWindow>("witchDialog") == null, "Private completed hero waits for its region to become public");
            progress.SetMapRegionProgress(11, 12, 10000);
            Require(progress.RecordGoddessActivated(11, 12, "probe-public-hero-region").IsApplied, "Public hero fixture");
            Invoke("InteractHero", hero);
            window = Field<OfficialMap11WitchWindow>("witchDialog");
            yield return new WaitForSecondsRealtime(.5f);
            yield return Capture("map-11-hero-daily");
            yield return Click(window.Find(window.Source("PeriodicityInteractionStageUI").fields.mInteractionBtn).GetComponent<Button>());
            yield return new WaitForSecondsRealtime(4.6f);
            window = Field<OfficialMap11WitchWindow>("witchDialog");
            yield return Capture("map-11-hero-daily-reward");
            yield return Click(window.Find(window.Source("WorldStageInteractionPreviewDialog").fields.mCloseBtn).GetComponent<Button>());
            Require(progress.HasVisitedStatueOn(DateTime.Now.ToString("yyyyMMdd")), "Hero daily visit is durable");
            var cold = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(root), slot);
            Require(cold.Save.Player.CharacterProperties.ExtraProperties.Count(value => value.SourceId == "hero-activation:11:1401") == 1,
                "Hero permanent property restores exactly once");
        }

        private IEnumerator VerifyStaminaGathering()
        {
            var progress = Field<OfflineMainlineProgressSession>("progressionSave");
            progress.SetMapRegionProgress(11, 0, 10000);
            Require(progress.RecordGoddessActivated(11, 0, "probe-public-stamina-region").IsApplied, "Public resource region fixture");
            Require(progress.ApplyInventoryTransaction("probe-map-stamina", save =>
            {
                if (!OfflineInventoryOperations.Grant(save.Player, 57, 20, DateTimeOffset.UtcNow.ToUnixTimeSeconds()).IsApplied)
                    throw new InvalidOperationException("Stamina fixture failed.");
            }).IsApplied, "Isolated gather stamina fixture");
            Invoke("RefreshEntityActivity");
            object entity = Field<IDictionary>("mapEntities")[115];
            var grid = (MapGridPoint)entity.GetType().GetField("Grid").GetValue(entity);
            var session = Field<OfficialWorldMapSession>("session");
            MapGridPoint adjacent = new[] { new MapGridPoint(1, 0), new MapGridPoint(-1, 0), new MapGridPoint(0, 1), new MapGridPoint(0, -1) }
                .Select(value => grid + value).First(value => session.Movement.IsFit(value));
            Invoke("SetAvatarPosition", adjacent);
            yield return new WaitForSecondsRealtime(1);
            var view = runtime.GetComponentInChildren<OfficialMap10EntityView>();
            var node = view.FindEntityObject(115);
            Require(node.activeInHierarchy, "Public resource point is visible");
            Vector3 point = node.GetComponentsInChildren<Collider2D>().First(value => value.enabled).bounds.center;
            var camera = Field<Camera>("worldCamera");
            Require(Hits(camera.WorldToScreenPoint(point)).Count == 0, "Resource pointer is exposed to world picking");
            long gold = progress.InventoryCount(1), stamina = progress.InventoryCount(57);
            worldPress = point;
            float timeout = Time.realtimeSinceStartup + 3;
            while (progress.InventoryCount(57) == stamina && Time.realtimeSinceStartup < timeout) yield return null;
            Require(progress.InventoryCount(57) == stamina - 5 && progress.InventoryCount(1) > gold
                && !node.activeInHierarchy, "Simulated map click spends five stamina, grants gold and hides the point");
            yield return new WaitForSecondsRealtime(.65f);
            Require(FindObjectOfType<OfficialMapAwardList>() != null, "The original animated award list is mounted");
            yield return Capture("map-11-stamina-gold-award");
            timeout = Time.realtimeSinceStartup + 4;
            while (!node.activeInHierarchy && Time.realtimeSinceStartup < timeout) yield return null;
            Require(node.activeInHierarchy && progress.InventoryCount(57) == stamina - 5, "The point returns after its native three second interval");
            yield return Capture("map-11-stamina-respawn");
        }

        private OfficialMap11WitchWindow ExploreTop => Field<List<OfficialMap11WitchWindow>>("exploreWindows").Last();
        private Dictionary<string, object> ExploreFields(OfficialMap11WitchWindow window, string kind) =>
            (Dictionary<string, object>)OfficialMainlineMapInspection.Call((OfficialMainlineMapRuntime)runtime, "ExploreFields", new object[] { window.Key, kind, null });
        private Button ExploreButton(OfficialMap11WitchWindow window, string kind, string field) =>
            window.Find((string)ExploreFields(window, kind)[field]).GetComponent<Button>();
        private IEnumerator VerifyExploration()
        {
            var progress = Field<OfflineMainlineProgressSession>("progressionSave");
            Require(progress.ApplyInventoryTransaction("probe-world-explore-items", save =>
            {
                foreach (int id in new[] { 115, 4016, 2011, 2012, 2013, 2014, 2015, 2016 })
                    if (!OfflineInventoryOperations.Grant(save.Player, id, id == 115 ? 30 : 2, DateTimeOffset.UtcNow.ToUnixTimeSeconds()).IsApplied)
                        throw new InvalidOperationException("Explore inventory fixture failed.");
            }).IsApplied, "Explore inventory fixture");
            var entity = Field<IDictionary>("mapEntities")[115];
            var grid = (MapGridPoint)entity.GetType().GetField("Grid").GetValue(entity);
            Invoke("SetAvatarPosition", grid + new MapGridPoint(0, 1));
            yield return new WaitForSecondsRealtime(1.1f);
            var data = Field<OfficialMap11UiData>("miniMapUiData");
            var ui = Field<OfficialWorldUiRenderer>("ui");
            var binding = data.worldBindings.exploreButtons.First(v => v.isOneTime);
            var worldFields = data.components.Single(v => v.panel == "WorldCanvas" && v.type == "WorldExploreBtn" && v.path == binding.path).fields;
            var cost = data.components.Single(v => v.panel == "WorldCanvas" && v.type == "PlayerCostMoneyContent" && v.path == worldFields.mCostMoneyContent).fields;
            Button entry = ui.Find("WorldCanvas", cost.Btn).GetComponent<Button>();
            yield return Capture("map-11-explore-hud");
            var context = (OfflineExploreContext)OfficialMainlineMapInspection.Call((OfficialMainlineMapRuntime)runtime, "ExploreContext", null);
            var service = new OfflineWorldExplorationService(progress);
            Require(entry.IsActive(), "Explore HUD is visible: map=" + context.MapClassId + ", area=" + context.Area
                + ", region=" + context.Region + ", mapAllowed=" + context.MapCanExplore + ", regionForbids=" + context.RegionForbidsExplore
                + ", level=" + progress.Save.Player.Level + ", unlockItem=" + progress.InventoryCount(4016)
                + ", functionOpen=" + service.FunctionOpen(8, context) + ", canExplore=" + service.CanExplore(context)
                + ", inactiveParents=" + string.Join(",", entry.GetComponentsInParent<Transform>(true)
                    .Where(value => !value.gameObject.activeSelf).Select(value => value.name)));
            yield return Click(entry);
            yield return new WaitForSecondsRealtime(.65f);
            var confirm = ExploreTop;
            yield return Capture("map-11-explore-confirm-first");
            var firstMask = Field<Dictionary<OfficialMap11WitchWindow, GameObject>>("exploreMasks")[confirm];
            var maskImage = firstMask.GetComponent<RawImage>();
            Require(maskImage.rectTransform.rect.width > 0 && maskImage.rectTransform.rect.height > 0,
                "Original blur mask expands to the active root canvas");
            Require(confirm.Key == "ExploreConfirmDialog", "Original exploration confirmation opens from HUD");
            yield return Click(ExploreButton(confirm, confirm.Key, "mProbabilityBtn"));
            yield return new WaitForSecondsRealtime(.65f);
            var detail = ExploreTop;
            Require(detail.Key == "ExploreDetailDialog", "Original probability dialog opens");
            yield return Capture("map-11-explore-probability");
            // Close the native blur mask through an exposed top-screen point.
            var masks = Field<Dictionary<OfficialMap11WitchWindow, GameObject>>("exploreMasks");
            yield return Click(masks[detail].GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.6f);
            Require(ExploreTop == confirm, "Native mask click closes the upper dialog");
            long food = progress.InventoryCount(115), guide = progress.InventoryCount(56);
            yield return Click(ExploreButton(confirm, "ConfirmDialog", "ConfirmBtn"));
            yield return new WaitForSecondsRealtime(.8f);
            Require(progress.Save.World.Exploration.TotalCount == 1 && progress.InventoryCount(115) == food - 1
                && progress.InventoryCount(56) == guide + 1, "Pointer exploration spends original fruit and grants guide exactly once");
            var spawned = progress.Save.World.Exploration.Entities.Single();
            Require(spawned.Group == 51 && spawned.EntityClassId == 8201, "First result uses original guide entity");
            var view = runtime.GetComponentInChildren<OfficialMap10EntityView>();
            Require(view.FindEntityObject(spawned.PlacementId) != null, "Generated original map view is mounted");
            yield return Capture("map-11-explore-guide-entity");
            yield return new WaitForSecondsRealtime(2);
            long stamina = progress.InventoryCount(57);
            Vector3 chestPoint = view.FindEntityObject(spawned.PlacementId).GetComponentsInChildren<Collider2D>()
                .First(v => v.enabled).bounds.center;
            Require(Hits(Field<Camera>("worldCamera").WorldToScreenPoint(chestPoint)).Count == 0, "Guide chest is exposed to world picking");
            var beforeItems = progress.Save.Player.Inventory.ToDictionary(v => v.ItemId, v => v.Count);
            worldPress = chestPoint;
            float collectDeadline = Time.realtimeSinceStartup + 10;
            while (!progress.Save.World.Exploration.Entities.Single(v => v.Serial == spawned.Serial).Completed
                && Time.realtimeSinceStartup < collectDeadline) yield return null;
            Require(progress.Save.World.Exploration.Entities.Single(v => v.Serial == spawned.Serial).Completed
                && progress.InventoryCount(57) == stamina
                && progress.Save.Player.Inventory.Any(v => v.Count > (beforeItems.TryGetValue(v.ItemId, out long old) ? old : 0)),
                "World pointer opens guide chest, grants native rewards and consumes no stamina");
            yield return Capture("map-11-explore-guide-collected");
            yield return new WaitForSecondsRealtime(2.2f);
            yield return Click(entry);
            yield return new WaitForSecondsRealtime(.65f);
            confirm = ExploreTop;
            yield return Click(ExploreButton(confirm, confirm.Key, "mProbabilityBtn"));
            yield return new WaitForSecondsRealtime(.65f);
            detail = ExploreTop;
            var rewardIcon = detail.Root.GetComponentsInChildren<WorldItemIcon>().First(v => v.GetComponent<Button>() != null);
            yield return Click(rewardIcon.GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.65f);
            var rewardDetail = ExploreTop;
            Require(rewardDetail.Key == "ExploreRewardPreviewItemDetailDialog", "Native exploration reward detail opens from its icon");
            Canvas rewardCanvas = rewardDetail.Root.GetComponent<Canvas>();
            Canvas detailMaskCanvas = masks[rewardDetail].GetComponent<Canvas>();
            Require(rewardCanvas.overrideSorting && detailMaskCanvas.overrideSorting
                && detailMaskCanvas.sortingOrder == rewardCanvas.sortingOrder - 1
                && detailMaskCanvas.sortingOrder > detail.Root.GetComponent<Canvas>().sortingOrder,
                "Stacked native blur mask covers the previous popup beneath its attached window: window="
                + rewardCanvas.sortingOrder + ", mask=" + detailMaskCanvas.sortingOrder
                + ", previous=" + detail.Root.GetComponent<Canvas>().sortingOrder);
            yield return Capture("map-11-explore-reward-detail");
            yield return Click(masks[rewardDetail].GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.6f);
            var detailFields = ExploreFields(detail, detail.Key);
            var tabs = (List<object>)detailFields["mTabItems"];
            var historyTab = (Dictionary<string, object>)tabs.Single(v => Convert.ToInt32(((Dictionary<string, object>)v)["State"]) == 1);
            yield return Click(detail.Find((string)historyTab["TabItem"]).GetComponent<Button>());
            yield return Capture("map-11-explore-history");
            yield return Click(masks[detail].GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.6f);
            yield return Click(ExploreButton(confirm, confirm.Key, "SelectNewBtn"));
            yield return new WaitForSecondsRealtime(.65f);
            var tokens = ExploreTop;
            Require(tokens.Key == "ExploreSelectTokenDialog", "Original token dialog opens");
            yield return Capture("map-11-explore-tokens");
            var tokenFields = ExploreFields(tokens, tokens.Key);
            var scroll = tokens.Find((string)tokenFields["ScrollRect"]).GetComponent<LoopVerticalScrollRect>();
            yield return DragScroll(scroll);
            yield return Capture("map-11-explore-tokens-scrolled");
            var row = scroll.content.GetComponentsInChildren<Button>().First(v => v.name == "Btn"
                && ExecuteEvents.GetEventHandler<IPointerClickHandler>(Hits(RectTransformUtility.WorldToScreenPoint(null,
                    ((RectTransform)v.transform).TransformPoint(((RectTransform)v.transform).rect.center)))
                    .FirstOrDefault().gameObject) == v.gameObject);
            yield return Click(row);
            yield return Click(ExploreButton(tokens, tokens.Key, "ConfirmButton"));
            yield return new WaitForSecondsRealtime(.6f);
            Require(Field<int>("exploreToken") > 0, "Native token row selection is applied by pointer");
            yield return Capture("map-11-explore-selected-token");
            int selectedToken = Field<int>("exploreToken");
            long tokenBefore = progress.InventoryCount(selectedToken);
            food = progress.InventoryCount(115);
            yield return Click(ExploreButton(confirm, "ConfirmDialog", "ConfirmBtn"));
            yield return new WaitForSecondsRealtime(.8f);
            Require(progress.Save.World.Exploration.TotalCount == 2 && progress.InventoryCount(115) == food - 1
                && progress.InventoryCount(selectedToken) == tokenBefore - 1 && progress.InventoryCount(56) == guide + 1,
                "Token exploration consumes one native token and fruit without repeating the guide award");
            // Locate an open original area for the ten-result fixture. The
            // spawn service keeps its native distance and occupancy checks.
            var map = Field<OfficialWorldMapSession>("session");
            var occupied = new HashSet<MapGridPoint>(Field<IDictionary>("mapEntities").Values.Cast<object>()
                .Select(v => (MapGridPoint)v.GetType().GetField("Grid").GetValue(v)));
            var open = new List<KeyValuePair<MapGridPoint, int>>();
            for (int y = 3; y < map.Definition.Navigation.Height - 3; y++)
                for (int x = 3; x < map.Definition.Navigation.Width - 3; x++)
                {
                    if (!OfficialMapNavigationView.TryGetCell(map.Definition.Navigation, x, y, out int area, out int ground)
                        || area != context.Area || !map.Movement.IsFit(new MapGridPoint(x, y))) continue;
                    int free = 0;
                    for (int dy = -3; dy <= 3; dy++) for (int dx = -3; dx <= 3; dx++)
                    {
                        var p = new MapGridPoint(x + dx, y + dy);
                        if (dx * dx + dy * dy <= 9 && !occupied.Contains(p)
                            && OfficialMapNavigationView.TryGetCell(map.Definition.Navigation, p.X, p.Y, out int a, out int g)
                            && a == area && (g == 0 || g == 6) && map.Movement.IsFit(p)) free++;
                    }
                    open.Add(new KeyValuePair<MapGridPoint, int>(new MapGridPoint(x, y), free));
                }
            Require(open.Count > 0, "Original area contains a ten-times exploration test position");
            Invoke("SetAvatarPosition", open.OrderByDescending(v => v.Value).First().Key);
            yield return new WaitForSecondsRealtime(.8f);
            PrepareTenExploreRandomFixture(progress);
            var manyBinding = data.worldBindings.exploreButtons.Single(v => !v.isOneTime);
            var manyWorld = data.components.Single(v => v.panel == "WorldCanvas" && v.type == "WorldExploreBtn" && v.path == manyBinding.path).fields;
            var manyCost = data.components.Single(v => v.panel == "WorldCanvas" && v.type == "PlayerCostMoneyContent" && v.path == manyWorld.mCostMoneyContent).fields;
            yield return Click(ui.Find("WorldCanvas", manyCost.Btn).GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.65f);
            confirm = ExploreTop;
            yield return Capture("map-11-explore-ten-confirm");
            food = progress.InventoryCount(115);
            yield return Click(ExploreButton(confirm, "ConfirmDialog", "ConfirmBtn"));
            yield return new WaitForSecondsRealtime(.8f);
            Require(progress.Save.World.Exploration.TotalCount == 12 && progress.InventoryCount(115) == food - 10
                && progress.Save.World.Exploration.History.Count == 12 && progress.InventoryCount(56) == guide + 1,
                "Original ten-times HUD button commits ten independent results and native fruit cost; count="
                + progress.Save.World.Exploration.TotalCount + "; fruit=" + progress.InventoryCount(115));
            yield return Capture("map-11-explore-ten-results");
            yield return new WaitForSecondsRealtime(2.1f);
            yield return Click(entry);
            yield return new WaitForSecondsRealtime(.65f);
            confirm = ExploreTop;
            yield return Click(ExploreButton(confirm, confirm.Key, "mProbabilityBtn"));
            yield return new WaitForSecondsRealtime(.65f);
            detail = ExploreTop;
            yield return Click(detail.Find((string)historyTab["TabItem"]).GetComponent<Button>());
            var historyFields = (Dictionary<string, object>)OfficialMainlineMapInspection.Call((OfficialMainlineMapRuntime)runtime, "ExploreFields", new object[] { detail.Key, "ExploreHistoryContent", ExploreFields(detail, detail.Key)["mExploreHistoryContent"] });
            var paging = (Dictionary<string, object>)OfficialMainlineMapInspection.Call((OfficialMainlineMapRuntime)runtime, "ExploreFields", new object[] { detail.Key, "TurnPageUI", historyFields["mTurnPageUI"] });
            var nextPage = (Dictionary<string, object>)OfficialMainlineMapInspection.Call((OfficialMainlineMapRuntime)runtime, "ExploreFields", new object[] { detail.Key, "TurnPageBtn", paging["mNextBtn"] });
            yield return Click(detail.Find((string)nextPage["Btn"]).GetComponent<Button>());
            Require(detail.Find((string)paging["mCurPageText"]).GetComponent<TMPro.TMP_Text>().text.Contains("2"),
                "Native history pagination advances to the second page");
            yield return Capture("map-11-explore-history-second-page");
            yield return Click(masks[detail].GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.6f);
            yield return Click(ExploreButton(confirm, "ConfirmDialog", "CancelBtn"));
            yield return new WaitForSecondsRealtime(.6f);
        }

        private void PrepareTenExploreRandomFixture(OfflineMainlineProgressSession progress)
        {
            // Find a reproducible native sequence that fits the approved local
            // placement policy. Distance-one entities can otherwise arrive after
            // all four neighbouring cells were taken by distance-three results.
            // Sampling writes only a second isolated fixture, never this profile.
            var slots = new ProfileSlotCatalog(Path.Combine(output, "spawn-fixture"));
            string sampleSlot = slots.CreateSlot();
            var context = (OfflineExploreContext)OfficialMainlineMapInspection.Call((OfficialMainlineMapRuntime)runtime, "ExploreContext", null);
            long selected = 0;
            for (long seed = 1; seed <= 128; seed++)
            {
                var save = JsonUtility.FromJson<OfflineMainlineSave>(JsonUtility.ToJson(progress.Save));
                save.Player.InventoryRandomState = seed;
                File.WriteAllText(Path.Combine(slots.GetSlotDirectory(sampleSlot), "city-save.json"), JsonUtility.ToJson(save));
                var sample = OfflineMainlineProgressSession.Open(slots, sampleSlot, OfficialMainlineTaskCatalog.LoadDefault());
                var service = new OfflineWorldExplorationService(sample);
                if (service.Explore(context, 10, 0, "ten-position-fixture", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), out _).IsApplied)
                { selected = seed; break; }
            }
            Require(selected > 0, "An original ten-result sequence fits the isolated map position");
            Require(progress.ApplyInventoryTransaction("probe-ten-seed", save => save.Player.InventoryRandomState = selected).IsApplied,
                "Reproducible ten-times exploration fixture");
        }

        private static IEnumerator DragScroll(LoopVerticalScrollRect scroll)
        {
            Canvas.ForceUpdateCanvases();
            Vector2 start = RectTransformUtility.WorldToScreenPoint(null, scroll.viewport.TransformPoint(scroll.viewport.rect.center));
            var hit = Hits(start).First();
            Require(ExecuteEvents.GetEventHandler<IDragHandler>(hit.gameObject) == scroll.gameObject,
                "Native loop scroll receives pointer drag through its viewport");
            var pointer = new PointerEventData(EventSystem.current) { position = start, pressPosition = start, button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(scroll.gameObject, pointer, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(scroll.gameObject, pointer, ExecuteEvents.beginDragHandler);
            for (int step = 1; step <= 12; step++)
            {
                Vector2 next = start + Vector2.up * (step * 14);
                pointer.delta = next - pointer.position; pointer.position = next;
                ExecuteEvents.Execute(scroll.gameObject, pointer, ExecuteEvents.dragHandler);
                yield return null;
            }
            ExecuteEvents.Execute(scroll.gameObject, pointer, ExecuteEvents.endDragHandler);
            yield return new WaitForSecondsRealtime(.4f);
        }

        private static List<RaycastResult> Hits(Vector2 position)
        {
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            return hits;
        }

        private static IEnumerator Click(Button button)
        {
            Require(button != null && button.IsActive() && button.IsInteractable(), "Pointer target is active and interactable");
            Canvas.ForceUpdateCanvases();
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center));
            var hits = Hits(position);
            GameObject target = hits.Count > 0 ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) : null;
            if (target != button.gameObject)
            {
                // Full-screen close buttons are intentionally covered by the native
                // scroll viewport. Click an exposed point inside their own rectangle.
                RectTransform rect = (RectTransform)button.transform;
                foreach (Vector2 sample in new[] { new Vector2(.5f, .08f), new Vector2(.08f, .5f), new Vector2(.92f, .5f), new Vector2(.5f, .92f) })
                {
                    position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(
                        rect.rect.min + Vector2.Scale(rect.rect.size, sample)));
                    hits = Hits(position);
                    target = hits.Count > 0 ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) : null;
                    if (target == button.gameObject) break;
                }
            }
            Require(target == button.gameObject, "Original button receives the topmost raycast at " + button.name
                + "; position=" + position + "; rect=" + ((RectTransform)button.transform).rect
                + "; hits=" + string.Join(",", hits.Take(6).Select(value => value.gameObject.name)));
            var pointer = new PointerEventData(EventSystem.current) { position = position, button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
            yield return null;
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
            yield return new WaitForSecondsRealtime(.3f);
        }

        private IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
            Destroy(image);
        }

        private T Field<T>(string name) => (T)OfficialMainlineMapInspection.Get((OfficialMainlineMapRuntime)runtime, name);
        private void Invoke(string name, params object[] args) => OfficialMainlineMapInspection.Call((OfficialMainlineMapRuntime)runtime, name, args);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private void Complete(bool success)
        {
            if (finished) return;
            finished = true;
            Application.logMessageReceived -= LogFailure;
            if (runtime != null) Destroy(runtime.gameObject);
            completed?.Invoke(success);
        }
    }
}
#endif
