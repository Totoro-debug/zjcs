#if UNITY_EDITOR || DEVELOPMENT_BUILD || ZJCS_MAINLINE_MODULE_VALIDATION
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Official.World.Map13MiniMap;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.CharacterCreation;

namespace Zjcs.Presentation.World
{
    /// <summary>Isolated regression of the shared modules in both Play Mode and a Windows development Player.</summary>
    public sealed class OfficialMainlineMapModuleProbe : MonoBehaviour
    {
        public const string Argument = "-verify-mainline-modules";
        private OfficialMainlineMapRuntime runtime;
        private string output, root, slot;
        private Action<bool> completed;
        private bool finished;
        private float deadline;
        private Transform canvasRoot;
        private int mapId;
        private bool escape;
        private OfficialWorldMapSession mapSession;
        private OfflineMainlineProgressSession observedProgress;
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
            probe.deadline = Time.realtimeSinceStartup + 600f;
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
            observedProgress?.Reload();
            if (finished || Time.realtimeSinceStartup < deadline) return;
            File.WriteAllText(Path.Combine(output, "failed.txt"), "Shared map probe exceeded its 600 second deadline.");
            Complete(false);
        }

        private IEnumerator Guarded(IEnumerator sequence)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(sequence);
            while (stack.Count > 0)
            {
                if (finished) yield break;
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
            if (finished) yield break;
            File.WriteAllText(Path.Combine(output, "passed.txt"),
                Environment.GetCommandLineArgs().Contains("-verify-story-playable-materials")
                    ? "Map16 original disappearance prefabs: 17 imported material shader bindings and rendered particle samples passed."
                : Environment.GetCommandLineArgs().Contains("-verify-visibility-maps")
                    ? "Map10/14 original visibility/fog, pointer movement, exact disk cold restore and read-only visited-map snapshots passed."
                    : Environment.GetCommandLineArgs().Contains("-verify-exploration-only")
                    ? "Exploration pointer flow: native HUD, confirmation, probability/detail/history, token scrolling/selection/cost, guide chest reward without stamina, ten-times exploration and cold reload passed."
                    : Environment.GetCommandLineArgs().Contains("-verify-map15-only")
                    ? "Whale Island verified data binding, Map15 pointer navigation and cold reload, shared statue visit transaction and cold reload passed."
                    : "Map10/11 shared runtime: region conditions, pointer navigation, HUD, minimap tabs/filter, goddess activation, hero UI/permanent rewards/daily effects, stamina gathering/award animation/respawn, exploration and cold save reload passed.");
            Debug.Log("Shared mainline map modules passed: " + output);
            Complete(true);
        }

        public static void ValidateWhaleIslandStoryCondition()
        {
            const string tag = "金宝箱_鲸岛神龟";
            var evaluator = new Map13OfflinePlayerConditionEvaluator(Array.Empty<Map13RuntimePlayerCondition>(),
                _ => 0, _ => 0, _ => 0, _ => 0, (_, _) => 0, value => value == tag);
            bool Evaluate(string value, int not) => evaluator.Evaluate(
                "MapLogicEntityEndByTag", new Map13RuntimePlayerConditionData { Tag = value }, null, not);
            if (!Evaluate(tag, 0) || Evaluate(tag, 1) || Evaluate("another-tag", 0) || !Evaluate("another-tag", 1))
                throw new InvalidDataException("Shared mainline completed-tag story condition changed.");
        }

        public static void RunInteractionCatalogs()
        {
            for (int map = 10; map <= 15; map++)
            {
                var heroes = OfficialMainlineInteractionCatalog.Load(map).heroes;
                if (heroes.Length != 4) throw new InvalidDataException("Historical hero selection changed: " + map);
            }
            var catalog = OfficialMainlineInteractionCatalog.Load(16);
            int[] ids = catalog.heroes.Select(hero => hero.EntityClassId).ToArray();
            if (!ids.SequenceEqual(new[] { 6570, 6571, 6572, 6573, 6574, 6575 }))
                throw new InvalidDataException("Whale Island profile did not load all six original hero variants.");
            var regions = catalog.regions;
            if (regions.Length != 43 || regions.Any(region => region.MapClassId != 16))
                throw new InvalidDataException("Whale Island profile did not load its 43 original interaction regions.");
            foreach (object[] invalid in new[] {
                new object[] { "Map16/interactions", "871_156598", 16, 43 },
                new object[] { "Map16/interactions", "884_156692", 13, 43 },
                new object[] { "Map16/interactions", "884_156692", 16, 42 } })
            {
                try { OfficialMainlineInteractionCatalog.Load((string)invalid[0], (string)invalid[1], (int)invalid[2], (int)invalid[3]); }
                catch (InvalidDataException) { continue; }
                throw new InvalidDataException("The shared interaction loader accepted another source identity.");
            }
            Debug.Log("Shared interaction catalog validation passed: historical maps 10-15, six Whale Island heroes and source identity guards.");
        }

        private IEnumerator VerifyStoryPlayableMaterials()
        {
            var catalog = Resources.Load<Zjcs.Offline.World.NativeMap13.NativeMap13AssetCatalog>(
                "Map16/Native/native-story-playables-catalog");
            Require(catalog != null && catalog.Bindings.Length == 2, "Original Map16 playable bindings");
            var pipeline = Resources.Load<Zjcs.Presentation.Combat.OfficialBattleRenderPipelineAssets>(
                "Combat/OfficialBattleRenderPipelineAssets");
            Require(pipeline != null, "Original particle preview pipeline");
            Camera[] previousCameras = FindObjectsOfType<Camera>().Where(value => value.enabled).ToArray();
            foreach (Camera previous in previousCameras) previous.enabled = false;
            var preview = new GameObject("Original particle preview").AddComponent<Camera>();
            preview.orthographic = true;
            preview.clearFlags = CameraClearFlags.SolidColor;
            preview.backgroundColor = new Color(.15f, .18f, .2f);
            var materials = new HashSet<Material>();
            using (pipeline.Enter())
            try
            {
                foreach (var binding in catalog.Bindings)
                {
                    var prefab = catalog.Load<GameObject>(binding.OfficialPath);
                    var instance = Instantiate(prefab);
                    try
                    {
                        instance.SetActive(true);
                        var renderers = instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
                        // The original renderer arrays also contain empty trail slots.
                        foreach (Material material in renderers.SelectMany(value => value.sharedMaterials).Where(value => value != null))
                        {
                            Require(material != null && material.shader != null && material.shader.isSupported
                                && material.shader.name == "Zjcs/Official/World/Map16/StoryPlayables/Noise_Mask_Pro",
                                "Original Map16 material shader resolves through ShaderImporter: "
                                + (material == null ? "null material" : material.name + " / "
                                    + (material.shader == null ? "null shader" : material.shader.name + " / supported=" + material.shader.isSupported)));
                            materials.Add(material);
                        }
                        // This is an isolated material raster sample, separate from
                        // the unfinished story graph's activation and actor binding.
                        var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
                        foreach (var system in systems)
                        {
                            for (Transform node = system.transform; node != instance.transform; node = node.parent)
                                node.gameObject.SetActive(true);
                        }
                        int particles = 0;
                        for (int sample = 0; sample < 40 && particles == 0; sample++)
                            foreach (var system in systems)
                            {
                                system.Simulate(.05f, false, sample == 0, true);
                                system.Pause(false);
                                particles += system.particleCount;
                            }
                        Require(particles > 0, "Original disappearance prefab produces particles");
                        yield return null;
                        Bounds bounds = renderers[0].bounds;
                        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                        preview.transform.position = bounds.center + Vector3.back * 50f;
                        preview.orthographicSize = Mathf.Max(1f, bounds.extents.y, bounds.extents.x / preview.aspect) * 1.15f;
                        yield return Capture("map-16-" + prefab.name + "-materials.png");
                    }
                    finally { Destroy(instance); }
                    yield return null;
                }
                Require(materials.Count == 17, "All 17 original Map16 playable materials are bound");
            }
            finally
            {
                Destroy(preview.gameObject);
                foreach (Camera previous in previousCameras) if (previous != null) previous.enabled = true;
            }
        }

        private IEnumerator Verify()
        {
            WorldMapAssets whaleIsland = OfficialWorldMapAssetCatalog.LoadDefault().LoadMap(16);
            Require(whaleIsland.Definition.ClassId == 16 && whaleIsland.Definition.Entities.Length == 1021
                && whaleIsland.Definition.Navigation.Width == 140 && whaleIsland.Definition.Navigation.Height == 120
                && !OfficialMainlineMapProfile.Get(16).IsRuntimeEntryReady,
                "Load Whale Island's verified public data binding without opening its unfinished entry");
            yield return null;
            // Editor startup may already have mounted the normal opening screen.
            foreach (string name in new[] { "CharacterCreationFlow", "CharacterCreationCanvas" })
            {
                GameObject startup = GameObject.Find(name);
                if (startup != null) Destroy(startup);
            }
            yield return null;
            if (Environment.GetCommandLineArgs().Contains("-verify-story-playable-materials"))
            {
                yield return VerifyStoryPlayableMaterials();
                yield break;
            }
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
            var characterAssets = OfficialCharacterAssetCatalog.LoadDefault();
            OfflineCharacterProgressionService.Create(progress, new CharacterProgressionInputs(characterAssets.Load(),
                Zjcs.Offline.Equipment.OfficialEquipmentAssetCatalog.LoadDefault().Load(), characterAssets.LoadSkillRules()))
                .InitializeWardrobe("shared-map-probe-wardrobe");
            if (Environment.GetCommandLineArgs().Contains("-verify-visibility-maps"))
            {
                yield return VerifyVisibilityMaps(canvas.transform, slots);
                Destroy(canvas);
                yield break;
            }
            if (Environment.GetCommandLineArgs().Contains("-verify-exploration-only"))
            {
                Open(11, canvas.transform);
                yield return new WaitForSecondsRealtime(1);
                progress = Progress;
                foreach (int region in new[] { 0, 1, 12 })
                {
                    progress.SetMapRegionProgress(11, region, 10000);
                    Require(progress.RecordGoddessActivated(11, region, "probe-explore-public-" + region).IsApplied,
                        "Public exploration region fixture");
                }
                yield return Reopen();
                yield return VerifyExploration();
                yield return ExitMap();
                yield return null;
                Open(11, canvas.transform);
                yield return new WaitForSecondsRealtime(1);
                Require(Progress.Save.World.Exploration.TotalCount == 12,
                    "Pointer exploration result survives a cold runtime reload");
                yield return Capture("map-11-explore-cold");
                yield return ExitMap();
                Destroy(canvas);
                yield break;
            }
            bool map15Only = Environment.GetCommandLineArgs().Contains("-verify-map15-only");
            if (!map15Only)
            {
                File.WriteAllText(Path.Combine(slots.GetSlotDirectory(slot), "map-10-save.json"),
                    JsonUtility.ToJson(new Map10LocalSave { slotId = slot, completedStories = new[] { "FirstEnterMap", "FirstBattle" } }));

                foreach (int id in new[] { 10, 11 })
                {
                    Open(id, canvas.transform);
                    yield return new WaitForSecondsRealtime(1);
                    var session = mapSession;
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
                    Require(SceneRoot("WorldCanvas").activeInHierarchy, "Map HUD mounted");
                    yield return VerifyPointerMovement(session);
                    yield return Capture("map-" + id + "-hud");
                    if (id == 11)
                    {
                        yield return VerifyNativeUi();
                        yield return VerifyHeroStatues();
                        yield return VerifyStaminaGathering();
                        yield return VerifyExploration();
                    }
                    MapGridPoint position = Position;
                    // Movement and interactions persist through the normal host lifecycle.
                    yield return ExitMap();
                    yield return null;
                    Open(id, canvas.transform);
                    Require(Position.Equals(position), "Map position survives a cold runtime reload");
                    if (id == 11)
                    {
                        Require(Progress.Save.World.ActivatedGoddessRegions.Count == 3,
                            "Goddess activation survives a cold reload");
                        VerifyHeroMiniMapVisibility(true);
                        var restoredResources = FindObjectOfType<MiniMapResourcePointManager>();
                        Require(restoredResources.IsBoardActive(1), "Native resource selection restores on map entry");
                        // Original MapUIBase.Start initializes the board's region visibility.
                        yield return null;
                        Require(restoredResources.GetSelectResourcePointDatas(1, 0).Active,
                            "Native public resource area restores before reopening the minimap");
                        Require(runtime.GetComponentInChildren<OfficialMainlineRegionFog>().VisibleShadowCount == 70,
                            "Cold map entry keeps all three public region shadows cleared");
                        yield return Capture("map-11-region-fog-cold");
                    }
                    yield return ExitMap();
                    yield return null;
                }
            }
            progress = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            Require(progress.SetPlayerProgress(save.Player.Level, 5, save.Player.SubRank,
                save.Player.MaxProfessionRank, save.Player.GuildName ?? string.Empty,
                progress.Save.Player.UnlockedMapClassIds.Concat(new[] { 15 })).IsApplied,
                "Map15 offline gate fixture");
            Open(15, canvas.transform);
            yield return new WaitForSecondsRealtime(2);
            var map15Session = mapSession;
            Require(map15Session.Definition.ClassId == 15
                && map15Session.Definition.Navigation.Width == 25
                && map15Session.Definition.Navigation.Height == 30,
                "Map15 official definition and navigation are loaded");
            var map15Entities = runtime.GetComponentInChildren<OfficialMap10EntityView>();
            Require(map15Entities != null && map15Entities.EntityInfos.Count == 26,
                "Map15 official entity catalog is mounted");
            yield return VerifyPointerMovement(map15Session);
            yield return Capture("map-15-entry");
            MapGridPoint map15Position = Position;
            // Movement and interactions persist through the normal host lifecycle.
            yield return ExitMap();
            yield return null;
            Open(15, canvas.transform);
            Require(Position.Equals(map15Position),
                "Map15 position survives a cold runtime reload");
            yield return Capture("map-15-entry-cold");
            yield return ExitMap();
            yield return null;
            progress = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            string[] effectsBeforeVisit = progress.Save.Player.PendingStatueBattleEffects
                .Select(value => JsonUtility.ToJson(value)).ToArray();
            Require(progress.ApplyStatueVisit("20261025", "witch", 11, 2801, "probe-witch", -1f, new[] { 28500 },
                Array.Empty<OfflineItemStack>(), 1).IsApplied, "Witch visit transaction");
            Require(progress.ApplyStatueVisit("20261025", "hero", 10, 9002, "probe-hero", 0f, Array.Empty<int>(),
                Array.Empty<OfflineItemStack>(), 1).Status == ProgressionResultStatus.NotReady, "Hero and witch share one daily visit");
            progress = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            var effects = progress.Save.Player.PendingStatueBattleEffects;
            Require(progress.HasVisitedStatueOn("20261025") && effects.Count == effectsBeforeVisit.Length + 1
                && effects.Take(effectsBeforeVisit.Length).Select(value => JsonUtility.ToJson(value)).SequenceEqual(effectsBeforeVisit)
                && effects.Last().EventId == "probe-witch" && effects.Last().HpPercentChange == -1f
                && effects.Last().StatusEffectClassIds.SequenceEqual(new[] { 28500 }),
                "Daily visit and battle effects survive cold reload");
            Destroy(canvas);
        }

        private IEnumerator VerifyVisibilityMaps(Transform canvas, ProfileSlotCatalog slots)
        {
            // Existing saves have no fog/history fields. Rebuild only from the
            // current position and activated regions, without inventing history.
            File.WriteAllText(Path.Combine(slots.GetSlotDirectory(slot), "map-10-save.json"),
                JsonUtility.ToJson(new Map10LocalSave { slotId = slot, gridX = 24, gridY = 23,
                    completedStories = new[] { "FirstEnterMap", "FirstBattle" } }));
            var snapshots = new MainlineMapMiniMapSnapshots(OfficialWorldMapAssetCatalog.LoadDefault(), slots, slot);
            var saved = new Dictionary<int, byte[]>();
            foreach (int id in new[] { 10, 14 })
            {
                Open(id, canvas);
                yield return new WaitForSecondsRealtime(3);
                var session = mapSession;
                var visibility = new MainlineMapMiniMapSnapshots(OfficialWorldMapAssetCatalog.LoadDefault(), slots, slot);
                var entrySnapshot = visibility.Read(id);
                Require(entrySnapshot.Width == session.Definition.Navigation.Width
                    && entrySnapshot.Height == session.Definition.Navigation.Height,
                    "Formal map entry persists valid exploration visibility");
                var position = Position;
                Require(position.Equals(id == 10 ? new MapGridPoint(24, 23) : new MapGridPoint(247, 115)),
                    "Visibility binding preserves legacy position and original Map14 entry");
                Require(runtime.GetComponentsInChildren<Zjcs.Official.MapRootFog.CloudController2>(true).Length == 1,
                    "Formal map entry mounts exactly one original fog subtree");
                byte[] before = visibility.Read(id).GridVisibility;
                yield return Capture("map-" + id + "-visibility-entry");
                yield return VerifyPointerMovement(session);
                position = Position;
                Require(Zjcs.Official.World.Map13GridVisibility.GridVisibilityCompressUtils
                    .DoMyUnzip(visibility.Read(id).GridVisibility).GetPosValue(position.X, position.Y),
                    "Pointer movement reveals the actual player cell");
                // Movement and interactions persist through the normal host lifecycle.
                saved[id] = visibility.Read(id).GridVisibility;
                Require(!before.SequenceEqual(saved[id]), "Pointer movement expands the original revealed-cell payload");
                int[] discovered = MapSave.exploredEntityPlacements;
                string file = Path.Combine(slots.GetSlotDirectory(slot), "map-" + id + "-save.json");
                string persisted = File.ReadAllText(file);
                var snapshot = snapshots.Read(id);
                Require(saved[id].SequenceEqual(snapshot.GridVisibility) && persisted == File.ReadAllText(file),
                    "Offline minimap reads exact exploration without writing the map save");
                yield return Capture("map-" + id + "-visibility-moved");
                yield return ExitMap();
                yield return null;
                Open(id, canvas);
                yield return new WaitForSecondsRealtime(3);
                visibility = new MainlineMapMiniMapSnapshots(OfficialWorldMapAssetCatalog.LoadDefault(), slots, slot);
                Require(Position.Equals(position)
                    && saved[id].SequenceEqual(visibility.Read(id).GridVisibility)
                    && discovered.SequenceEqual(MapSave.exploredEntityPlacements),
                    "Formal cold entry preserves position, original fog bytes and discovered entities");
                yield return Capture("map-" + id + "-visibility-cold");
                yield return ExitMap();
                yield return null;
            }
            foreach (int id in new[] { 10, 14 })
                Require(saved[id].SequenceEqual(snapshots.Read(id).GridVisibility),
                    "Both visited maps remain readable after their live scenes are disposed");
        }

        private void Open(int id, Transform canvas)
        {
            GameObject model = CharacterCreationNativeModelFactory.CreateWorldModel(slot, root, out string name);
            runtime = new GameObject("SharedMap" + id).AddComponent<OfficialMainlineMapRuntime>();
            runtime.MapMusicVolume = runtime.MapVoiceVolume = () => 0;
            runtime.PointerPressProvider = () =>
            {
                Vector2? result = worldPress.HasValue ? (Vector2?)WorldCamera.WorldToScreenPoint(worldPress.Value) : press;
                press = null;
                worldPress = null;
                return result;
            };
            canvasRoot = canvas; mapId = id;
            mapSession = OfficialWorldMapSession.Load(OfficialWorldMapAssetCatalog.LoadDefault(), id);
            observedProgress ??= OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(root), slot);
            runtime.KeyDownProvider = key => { bool result = key == KeyCode.Escape && escape; escape = false; return result; };
            runtime.Begin(id, slot, root, canvas, model, name, null);
            ValidatePublicMapModule(id);
        }

        private void ValidatePublicMapModule(int id)
        {
            IMainlineMapModule module = runtime;
            MainlineMapStateSnapshot state = module.ReadState();
            Require(state.MapClassId == id && state.RegionProgress != null
                && state.Statues != null && state.ResourcePoints != null,
                "Public mainline map module exposes a complete state projection");
            Require((module.Profile.Capabilities & MainlineMapFeatures.MapModel) != 0
                && (module.Profile.Capabilities & MainlineMapFeatures.Regions) != 0
                && (module.Profile.Capabilities & MainlineMapFeatures.Statues) != 0
                && (module.Profile.Capabilities & MainlineMapFeatures.Exploration) != 0,
                "Map profile advertises the shared mainline capabilities");
            if (state.GridVisibility.Length > 0)
            {
                byte original = state.GridVisibility[0];
                state.GridVisibility[0] ^= 0xFF;
                Require(module.ReadState().GridVisibility[0] == original,
                    "Map state snapshot does not mutate the live fog save");
            }
        }

        private IEnumerator VerifyPointerMovement(OfficialWorldMapSession session)
        {
            MapGridPoint start = Position;
            var camera = WorldCamera;
            var entities = runtime.GetComponentInChildren<OfficialMap10EntityView>();
            foreach (MapGridPoint offset in new[] { new MapGridPoint(1, 0), new MapGridPoint(-1, 0), new MapGridPoint(0, 1), new MapGridPoint(0, -1) })
            {
                MapGridPoint target = start + offset;
                if (!session.Movement.IsFit(target) || !session.Movement.CanMoveToNext(start, target)) continue;
                Vector3 world = GridToWorld(target);
                Vector2 screen = camera.WorldToScreenPoint(world);
                if (Hits(screen).Count != 0 || entities.EntityInfos.Any(value => value.Active
                    && value.Grid[0] == target.X && value.Grid[1] == target.Y)) continue;
                worldPress = world;
                float deadline = Time.realtimeSinceStartup + 5;
                while (!Position.Equals(target) && Time.realtimeSinceStartup < deadline) yield return null;
                MapGridPoint actual = Position;
                Require(actual.Equals(target), "Simulated map press expected " + target.X + "," + target.Y + "; got " + actual.X + "," + actual.Y);
                Debug.Log("Shared map pointer moved to " + target.X + "," + target.Y);
                yield break;
            }
            throw new InvalidOperationException("No visible unoccupied neighbor for pointer navigation regression.");
        }

        private IEnumerator VerifyNativeUi()
        {
            var data = JsonUtility.FromJson<OfficialMap11UiData>(Resources.Load<TextAsset>("Map11/UI/catalog").text);
            var mini = SceneRoot("MiniMapMaskedUI");
            string buttonPath = data.components.Single(value => value.panel == "MiniMapMaskedUI" && value.type == "MiniMapMaskedUI").fields.MiniMapBtn;
            yield return Click(mini.transform.Find(buttonPath.Substring("/MiniMapMaskedUI/".Length)).GetComponent<Button>());
            Require(SceneRoot("MiniMapFullScreenPanel") != null, "Pointer opens the original full minimap");

            yield return Click(SceneRoot("MiniMapTab_10").GetComponent<Button>());
            Require(!SceneRoot("MinimapCharacter").activeSelf, "Pointer switches to visited Map10");
            yield return Click(SceneRoot("MiniMapTab_11").GetComponent<Button>());
            yield return Capture("map-11-minimap");
            var fields = data.components.Single(value => value.panel == "MiniMapFullScreenPanel" && value.type == "MiniMapMarkUIFilter").fields;
            var full = SceneRoot("MiniMapFullScreenPanel");
            yield return Click(full.transform.Find(fields.mEntryBtn.Substring("/MiniMapFullScreenPanel/".Length)).GetComponent<Button>());
            bool before = FilterSelection("MiniMapGoddess");
            yield return Click(FilterItem("MiniMapGoddess").GetComponentInChildren<Button>(true));
            Require(FilterSelection("MiniMapGoddess") != before, "Pointer changes the original mark filter");
            // Close through the original button; reopening cold-reads the durable selection.
            yield return ExitMap();
            Open(11, canvasRoot);
            yield return new WaitForSecondsRealtime(1);
            mini = SceneRoot("MiniMapMaskedUI");
            yield return Click(mini.transform.Find(buttonPath.Substring("/MiniMapMaskedUI/".Length)).GetComponent<Button>());
            Require(FilterSelection("MiniMapGoddess") != before, "Minimap filter cold-reads from its profile save file");
            yield return Click(SceneRoot("MiniMapFullScreenPanel").transform.Find("Content/CloseBtn_dark").GetComponent<Button>());

            var progress = Progress;
            var goddess = Entity(202); // pinned Map11 lost goddess placement
            progress.SetMapRegionProgress(11, 1, 10000);
            yield return At(new MapGridPoint(goddess.Grid[0], goddess.Grid[1] + 1));
            yield return new WaitForSecondsRealtime(1);
            var fog = runtime.GetComponentInChildren<OfficialMainlineRegionFog>();
            Require(fog != null && fog.ShadowCount == 73, "All native region light meshes are mounted");
            int shadows = fog.VisibleShadowCount;
            yield return Capture("map-11-region-fog");
            yield return ClickEntity(202);
            yield return new WaitForSecondsRealtime(.5f);
            yield return Capture("map-11-goddess");
            yield return Click(FindObjectOfType<GoddessTouchDialog>().myTouchBtn);
            Require(progress.Save.World.ActivatedGoddessRegions.Count == 1, "Pointer activates the original goddess stage");
            yield return new WaitForSecondsRealtime(.7f);
            yield return Capture("map-11-region-fog-clearing");
            yield return new WaitForSecondsRealtime(1.5f);
            Require(fog.VisibleShadowCount == shadows - 1, "Native region shadow disappears after its original animation duration");
            yield return Capture("map-11-region-fog-cleared");
        }

        private void VerifyHeroMiniMapVisibility(bool expected)
        {
            var binding = UiData.minimapMarkBindings.Single(value => value.placementId == 1401);
            var marker = SceneRoot(binding.key + "_1401");
            Require((marker != null && marker.activeInHierarchy) == expected,
                "Hero minimap visibility follows current private/public state");
        }

        private IEnumerator VerifyHeroStatues()
        {
            var hero = Entity(1401);
            Progress.SetStoryProgress("OldManAlways2", 1, 2); // Original region 12 story gate.
            yield return Reopen();
            VerifyHeroMiniMapVisibility(true);
            var progress = Progress;
            yield return At(new MapGridPoint(hero.Grid[0], hero.Grid[1] + 1));
            yield return ClickEntity(1401);
            yield return new WaitForSecondsRealtime(.5f);
            var window = StatueWindow;
            yield return Capture("map-11-hero-touch");
            yield return Click(window.Find(window.Source("StageUIWithPreview").fields.myTouchBtn).GetComponent<Button>());
            window = StatueWindow;
            yield return new WaitForSecondsRealtime(1);
            var previewText = window.Root.GetComponentsInChildren<TMPro.TMP_Text>().Single(value => value.text == "生命+170");
            Require(previewText.rectTransform.rect.height > 0 && previewText.isActiveAndEnabled,
                "Native hero property preview has visible layout");
            yield return Capture("map-11-hero-first-reward");
            yield return Click(window.Find(window.Source("WorldStageInteractionPreviewDialog").fields.mCloseBtn).GetComponent<Button>());
            Require(progress.Save.Player.CharacterProperties.ExtraProperties.Count(value => value.SourceId == "hero-activation:11:1401") == 1,
                "Hero activation atomically grants its permanent original property");
            VerifyHeroMiniMapVisibility(false);
            yield return ClickEntity(1401);
            Require(StatueWindow == null, "Private completed hero waits for its region to become public");
            progress.SetMapRegionProgress(11, 12, 10000);
            Require(progress.RecordGoddessActivated(11, 12, "probe-public-hero-region").IsApplied, "Public hero fixture");
            yield return Reopen();
            VerifyHeroMiniMapVisibility(true);
            yield return At(new MapGridPoint(hero.Grid[0], hero.Grid[1] + 1));
            yield return new WaitForSecondsRealtime(1);
            yield return Capture("map-11-hero-public-marker");
            yield return ClickEntity(1401);
            window = StatueWindow;
            yield return new WaitForSecondsRealtime(.5f);
            yield return Capture("map-11-hero-daily");
            yield return Click(window.Find(window.Source("PeriodicityInteractionStageUI").fields.mInteractionBtn).GetComponent<Button>());
            yield return new WaitForSecondsRealtime(4.6f);
            window = StatueWindow;
            yield return Capture("map-11-hero-daily-reward");
            yield return Click(window.Find(window.Source("WorldStageInteractionPreviewDialog").fields.mCloseBtn).GetComponent<Button>());
            Require(progress.HasVisitedStatueOn(DateTime.Now.ToString("yyyyMMdd")), "Hero daily visit is durable");
            var cold = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(root), slot);
            Require(cold.Save.Player.CharacterProperties.ExtraProperties.Count(value => value.SourceId == "hero-activation:11:1401") == 1,
                "Hero permanent property restores exactly once");
        }

        private IEnumerator SelectNativeResourceLayers()
        {
            var data = JsonUtility.FromJson<OfficialMap11UiData>(Resources.Load<TextAsset>("Map11/UI/catalog").text);
            string button = data.components.Single(row => row.panel == "MiniMapMaskedUI"
                && row.type == "MiniMapMaskedUI").fields.MiniMapBtn;
            var openButton = SceneRoot("MiniMapMaskedUI").transform
                .Find(button.Substring("/MiniMapMaskedUI/".Length)).GetComponent<Button>();
            yield return Click(openButton);
            var full = SceneRoot("MiniMapFullScreenPanel");
            var fields = data.components.Single(row => row.panel == "MiniMapFullScreenPanel"
                && row.type == "MiniMapMarkUIFilter").fields;
            yield return Click(full.transform.Find(fields.mEntryBtn.Substring("/MiniMapFullScreenPanel/".Length)).GetComponent<Button>());
            yield return Click(full.transform.Find(fields.mButton.Substring("/MiniMapFullScreenPanel/".Length)).GetComponent<Button>());
            var manager = FindObjectOfType<MiniMapResourcePointManager>();
            Require(manager.IsBoardActive(1) && !manager.GetSelectResourcePointDatas(1, 0).Active,
                "Original resource board is selected while its private area remains hidden");
            var allButton = full.transform.Find(fields.mButton.Substring("/MiniMapFullScreenPanel/".Length)).GetComponent<Button>();
            yield return Click(allButton);
            Require(!manager.IsBoardActive(1), "Canceling the original resource filter hides its board");
            yield return Click(allButton);
            Require(manager.IsBoardActive(1), "Selecting the original resource filter restores its board");
            yield return Click(full.transform.Find("Content/CloseBtn_dark").GetComponent<Button>());
            yield return Click(openButton);
        }

        private IEnumerator VerifyStaminaGathering()
        {
            var progress = Progress;
            yield return SelectNativeResourceLayers();
            progress.SetMapRegionProgress(11, 0, 10000);
            Require(progress.RecordGoddessActivated(11, 0, "probe-public-stamina-region").IsApplied, "Public resource region fixture");
            Require(progress.ApplyInventoryTransaction("probe-map-stamina", save =>
            {
                if (!OfflineInventoryOperations.Grant(save.Player, 57, 20, DateTimeOffset.UtcNow.ToUnixTimeSeconds()).IsApplied)
                    throw new InvalidOperationException("Stamina fixture failed.");
            }).IsApplied, "Isolated gather stamina fixture");
            yield return Reopen();
            var resourceArea = FindObjectOfType<MiniMapResourcePointManager>().GetSelectResourcePointDatas(1, 0);
            Require(resourceArea.Active && resourceArea.Datas.All(row => row.Item.gameObject.activeInHierarchy),
                "The original resource board responds to the region becoming public");
            var miniMapButton = UiData.components.Single(row => row.panel == "MiniMapMaskedUI"
                && row.type == "MiniMapMaskedUI").fields.MiniMapBtn;
            yield return Click(SceneRoot("MiniMapMaskedUI").transform
                .Find(miniMapButton.Substring("/MiniMapMaskedUI/".Length)).GetComponent<Button>());
            yield return Capture("map-11-resource-layers");
            yield return Click(SceneRoot("MiniMapFullScreenPanel").transform.Find("Content/CloseBtn_dark").GetComponent<Button>());
            var entity = Entity(115);
            var grid = new MapGridPoint(entity.Grid[0], entity.Grid[1]);
            var session = mapSession;
            MapGridPoint adjacent = new[] { new MapGridPoint(1, 0), new MapGridPoint(-1, 0), new MapGridPoint(0, 1), new MapGridPoint(0, -1) }
                .Select(value => grid + value).First(value => session.Movement.IsFit(value));
            yield return At(adjacent);
            yield return new WaitForSecondsRealtime(1);
            var view = runtime.GetComponentInChildren<OfficialMap10EntityView>();
            var node = view.FindEntityObject(115);
            Require(node.activeInHierarchy, "Public resource point is visible");
            Vector3 point = node.GetComponentsInChildren<Collider2D>().First(value => value.enabled).bounds.center;
            var camera = WorldCamera;
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

        private OfficialMap11WitchWindow ExploreTop => Window(SceneRoot("Official exploration UI").transform.Cast<Transform>()
            .Where(value => value.name != "BlurMask" && value.gameObject.activeInHierarchy)
            .OrderBy(value => value.GetComponent<Canvas>()?.sortingOrder ?? 0).Last().gameObject);
        private Dictionary<string, object> ExploreFields(OfficialMap11WitchWindow window, string kind) =>
            ExploreFields(window.Key, kind);
        private Button ExploreButton(OfficialMap11WitchWindow window, string kind, string field) =>
            window.Find((string)ExploreFields(window, kind)[field]).GetComponent<Button>();
        private IEnumerator VerifyExploration()
        {
            var progress = Progress;
            Require(progress.ApplyInventoryTransaction("probe-world-explore-items", save =>
            {
                foreach (int id in new[] { 115, 4016, 2011, 2012, 2013, 2014, 2015, 2016 })
                    if (!OfflineInventoryOperations.Grant(save.Player, id, id == 115 ? 30 : 2, DateTimeOffset.UtcNow.ToUnixTimeSeconds()).IsApplied)
                        throw new InvalidOperationException("Explore inventory fixture failed.");
            }).IsApplied, "Explore inventory fixture");
            var entity = Entity(115);
            var grid = new MapGridPoint(entity.Grid[0], entity.Grid[1]);
            yield return At(grid + new MapGridPoint(0, 1));
            yield return new WaitForSecondsRealtime(1.1f);
            var data = UiData;

            var binding = data.worldBindings.exploreButtons.First(v => v.isOneTime);
            var worldFields = data.components.Single(v => v.panel == "WorldCanvas" && v.type == "WorldExploreBtn" && v.path == binding.path).fields;
            var cost = data.components.Single(v => v.panel == "WorldCanvas" && v.type == "PlayerCostMoneyContent" && v.path == worldFields.mCostMoneyContent).fields;
            Button entry = UiFind("WorldCanvas", cost.Btn).GetComponent<Button>();
            yield return Capture("map-11-explore-hud");

            Require(entry.IsActive(), "Original exploration HUD entry is visible for the isolated saved position");
            yield return Click(UiFind("WorldCanvas", cost.Btn).GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.65f);
            var confirm = ExploreTop;
            yield return Capture("map-11-explore-confirm-first");
            var firstMask = ExploreMask(confirm);
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
            yield return Click(ExploreMask(detail).GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.6f);
            Require(ExploreTop.Root == confirm.Root, "Native mask click closes the upper dialog");
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
            Require(Hits(WorldCamera.WorldToScreenPoint(chestPoint)).Count == 0, "Guide chest is exposed to world picking");
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
            yield return Click(UiFind("WorldCanvas", cost.Btn).GetComponent<Button>());
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
            Canvas detailMaskCanvas = ExploreMask(rewardDetail).GetComponent<Canvas>();
            Require(rewardCanvas.overrideSorting && detailMaskCanvas.overrideSorting
                && detailMaskCanvas.sortingOrder == rewardCanvas.sortingOrder - 1
                && detailMaskCanvas.sortingOrder > detail.Root.GetComponent<Canvas>().sortingOrder,
                "Stacked native blur mask covers the previous popup beneath its attached window: window="
                + rewardCanvas.sortingOrder + ", mask=" + detailMaskCanvas.sortingOrder
                + ", previous=" + detail.Root.GetComponent<Canvas>().sortingOrder);
            yield return Capture("map-11-explore-reward-detail");
            yield return Click(ExploreMask(rewardDetail).GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.6f);
            var detailFields = ExploreFields(detail, detail.Key);
            var tabs = (List<object>)detailFields["mTabItems"];
            var historyTab = (Dictionary<string, object>)tabs.Single(v => Convert.ToInt32(((Dictionary<string, object>)v)["State"]) == 1);
            yield return Click(detail.Find((string)historyTab["TabItem"]).GetComponent<Button>());
            yield return Capture("map-11-explore-history");
            yield return Click(ExploreMask(detail).GetComponent<Button>());
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
            yield return Capture("map-11-explore-selected-token");
            var tokensBefore = new[] { 2011, 2012, 2013, 2014, 2015, 2016 }.ToDictionary(id => id, id => progress.InventoryCount(id));
            food = progress.InventoryCount(115);
            yield return Click(ExploreButton(confirm, "ConfirmDialog", "ConfirmBtn"));
            yield return new WaitForSecondsRealtime(.8f);
            Require(progress.Save.World.Exploration.TotalCount == 2 && progress.InventoryCount(115) == food - 1
                && tokensBefore.Count(pair => progress.InventoryCount(pair.Key) == pair.Value - 1) == 1 && progress.InventoryCount(56) == guide + 1,
                "Token exploration consumes one native token and fruit without repeating the guide award");
            // Locate an open original area for the ten-result fixture. The
            // spawn service keeps its native distance and occupancy checks.
            var map = mapSession;
            var occupied = new HashSet<MapGridPoint>(runtime.GetComponentInChildren<OfficialMap10EntityView>().EntityInfos
                .Select(value => new MapGridPoint(value.Grid[0], value.Grid[1])));
            OfficialMapNavigationView.TryGetCell(map.Definition.Navigation, Position.X, Position.Y, out int currentArea, out _);
            var open = new List<KeyValuePair<MapGridPoint, int>>();
            for (int y = 3; y < map.Definition.Navigation.Height - 3; y++)
                for (int x = 3; x < map.Definition.Navigation.Width - 3; x++)
                {
                    if (!OfficialMapNavigationView.TryGetCell(map.Definition.Navigation, x, y, out int area, out int ground)
                        || area != currentArea || !map.Movement.IsFit(new MapGridPoint(x, y))) continue;
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
            yield return At(open.OrderByDescending(v => v.Value).First().Key);
            yield return new WaitForSecondsRealtime(.8f);
            // The pre-refactor module fixture selected seed 16 for this original
            // area. Keep its input fixed instead of inspecting the live host.
            Require(progress.ApplyInventoryTransaction("probe-ten-seed", save => save.Player.InventoryRandomState = 16).IsApplied,
                "Deterministic isolated exploration seed");
            yield return Reopen();
            var manyBinding = data.worldBindings.exploreButtons.Single(v => !v.isOneTime);
            var manyWorld = data.components.Single(v => v.panel == "WorldCanvas" && v.type == "WorldExploreBtn" && v.path == manyBinding.path).fields;
            var manyCost = data.components.Single(v => v.panel == "WorldCanvas" && v.type == "PlayerCostMoneyContent" && v.path == manyWorld.mCostMoneyContent).fields;
            yield return Click(UiFind("WorldCanvas", manyCost.Btn).GetComponent<Button>());
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
            yield return Click(UiFind("WorldCanvas", cost.Btn).GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.65f);
            confirm = ExploreTop;
            yield return Click(ExploreButton(confirm, confirm.Key, "mProbabilityBtn"));
            yield return new WaitForSecondsRealtime(.65f);
            detail = ExploreTop;
            yield return Click(detail.Find((string)historyTab["TabItem"]).GetComponent<Button>());
            var historyFields = ExploreFields(detail.Key, "ExploreHistoryContent", ExploreFields(detail, detail.Key)["mExploreHistoryContent"]);
            var paging = ExploreFields(detail.Key, "TurnPageUI", historyFields["mTurnPageUI"]);
            var nextPage = ExploreFields(detail.Key, "TurnPageBtn", paging["mNextBtn"]);
            yield return Click(detail.Find((string)nextPage["Btn"]).GetComponent<Button>());
            Require(detail.Find((string)paging["mCurPageText"]).GetComponent<TMPro.TMP_Text>().text.Contains("2"),
                "Native history pagination advances to the second page");
            yield return Capture("map-11-explore-history-second-page");
            yield return Click(ExploreMask(detail).GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.6f);
            yield return Click(ExploreButton(confirm, "ConfirmDialog", "CancelBtn"));
            yield return new WaitForSecondsRealtime(.6f);
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
            Require(button != null && button.IsActive() && button.IsInteractable(), "Pointer target is active and interactable: "
                + (button == null ? "missing" : button.name));
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

        private OfflineMainlineProgressSession Progress { get { observedProgress.Reload(); return observedProgress; } }
        private Map10LocalSave MapSave => MainlineMapLocalSaveFile.Read(
            Path.Combine(new ProfileSlotCatalog(root).GetSlotDirectory(slot), "map-" + mapId + "-save.json"), slot, mapId,
            mapSession.Definition.Navigation.Width, mapSession.Definition.Navigation.Height);
        private MapGridPoint Position => new MapGridPoint(MapSave.gridX, MapSave.gridY);
        private Camera WorldCamera => runtime.GetComponentsInChildren<Camera>(true).First(value => (value.cullingMask & (1 << 30)) == 0);
        private OfficialMap11UiData UiData => JsonUtility.FromJson<OfficialMap11UiData>(Resources.Load<TextAsset>("Map11/UI/catalog").text);
        private static GameObject SceneRoot(string name) => Resources.FindObjectsOfTypeAll<Transform>()
            .FirstOrDefault(value => value.gameObject.scene.IsValid() && (value.name == name || value.name == name + "(Clone)"))?.gameObject;
        private GameObject UiFind(string panel, string path) => path == "/" + panel ? SceneRoot(panel)
            : SceneRoot(panel).transform.Find(path.Substring(panel.Length + 2)).gameObject;
        private OfficialMap10EntityInfo Entity(int id) => runtime.GetComponentInChildren<OfficialMap10EntityView>().EntityInfos.Single(value => value.PlacementId == id);
        private Vector3 GridToWorld(MapGridPoint grid)
        {
            var value = OfficialMapNavigationView.GridToWorld(mapSession.Definition.Navigation, grid.X, grid.Y);
            return new Vector3(value.X, value.Y, value.Z - .1f);
        }
        private OfficialMap11WitchWindow Window(GameObject node) => node == null ? null : new OfficialMap11WitchWindow(node.name, node, UiData, "Map11/UI/");
        private OfficialMap11WitchWindow StatueWindow => Window(new[] { "WorldTouchDialogWithPreview", "WorldStageInteractionPreviewDialogHeroStatue", "WorldTouchDialogPeriodicityInteraction", "WorldStageInteractionPreparePlayableDialog" }
            .Select(SceneRoot).FirstOrDefault(value => value && value.activeInHierarchy));
        private Dictionary<string, object> ExploreFields(string panel, string kind, object path = null) =>
            (Dictionary<string, object>)((List<object>)((Dictionary<string, object>)Zjcs.Offline.Equipment.OfficialEquipmentJson.Parse(
                Resources.Load<TextAsset>("Map11/UI/catalog").text, true))["components"])
                .Cast<Dictionary<string, object>>().Single(value => (string)value["panel"] == panel && (string)value["type"] == kind
                    && (path == null || (string)value["path"] == (string)path))["fields"];
        private GameObject ExploreMask(OfficialMap11WitchWindow window) => window.Root.transform.parent.Cast<Transform>()
            .Single(value => value.name == "BlurMask" && value.GetComponent<Canvas>().sortingOrder == window.Root.GetComponent<Canvas>().sortingOrder - 1).gameObject;
        private GameObject FilterItem(string key)
        {
            int index = Array.FindIndex(UiData.minimapMarks, value => value.name == key);
            var fields = UiData.components.Single(value => value.panel == "MiniMapFullScreenPanel" && value.type == "MiniMapMarkUIFilter").fields;
            return UiFind("MiniMapFullScreenPanel", fields.mFilterContent).transform.GetChild(index).gameObject;
        }
        private bool FilterSelection(string key) => new MainlineMiniMapFilterPersistentState(new ProfileSlotCatalog(root), slot).State.FilterState.TryGetValue(key, out bool selected) ? selected : !int.TryParse(key, out _);
        private IEnumerator ExitMap()
        {
            for (int attempt = 0; runtime != null && attempt < 12; attempt++)
            {
                escape = true;
                yield return new WaitForSecondsRealtime(.4f);
            }
            Require(runtime == null, "Simulated Escape closes the map through its normal lifecycle");
        }
        private IEnumerator Reopen()
        {
            int id = mapId;
            var canvas = canvasRoot;
            yield return ExitMap();
            Open(id, canvas);
            yield return new WaitForSecondsRealtime(1);
        }
        private IEnumerator At(MapGridPoint point)
        {
            int id = mapId;
            var canvas = canvasRoot;
            yield return ExitMap();
            var save = MapSave; save.gridX = point.X; save.gridY = point.Y;
            File.WriteAllText(Path.Combine(new ProfileSlotCatalog(root).GetSlotDirectory(slot), "map-" + id + "-save.json"), JsonUtility.ToJson(save));
            Open(id, canvas);
            yield return new WaitForSecondsRealtime(1);
        }
        private IEnumerator ClickEntity(int id)
        {
            var node = runtime.GetComponentInChildren<OfficialMap10EntityView>().FindEntityObject(id);
            Require(node != null && node.activeInHierarchy, "Original world entity is visible for pointer input: " + id);
            worldPress = node.GetComponentsInChildren<Collider2D>().First(value => value.enabled).bounds.center;
            yield return new WaitForSecondsRealtime(.7f);
        }
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
