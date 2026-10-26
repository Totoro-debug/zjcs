#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
            probe.deadline = Time.realtimeSinceStartup + 120f;
            Directory.CreateDirectory(probe.output);
            probe.StartCoroutine(probe.Guarded(probe.Verify()));
        }

        private void Update()
        {
            if (finished || Time.realtimeSinceStartup < deadline) return;
            File.WriteAllText(Path.Combine(output, "failed.txt"), "Shared map probe exceeded its 120 second deadline.");
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
                "Map10/11 shared runtime: region conditions, pointer navigation, HUD, minimap tabs/filter, goddess activation, statue transactions and cold save reload passed.");
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
            save.Player.Level = 20;
            save.Player.Experience = OfficialCharacterAssetCatalog.LoadDefault().Load().CumulativeLevelExperience[20];
            save.Player.Rank = save.Player.SubRank = save.Player.SeasonRank = 1;
            save.Player.Wardrobe.Profession = 1;
            save.Tasks.ClaimedMainlineTaskIds.AddRange(OfficialMainlineTaskCatalog.LoadDefault().OrderedTasks
                .Where(value => value.classId <= 1040).Select(value => value.classId));
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save));
            var progress = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            new OfflineCharacterProgressionService(progress, OfficialCharacterAssetCatalog.LoadDefault().Load())
                .InitializeWardrobe("shared-map-probe-wardrobe");
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
                if (id == 11) yield return VerifyNativeUi();
                MapGridPoint position = Field<MapGridPoint>("currentGrid");
                Invoke("SaveLocalProgress");
                Destroy(runtime.gameObject);
                yield return null;
                Open(id, canvas.transform);
                Require(Field<MapGridPoint>("currentGrid").Equals(position), "Map position survives a cold runtime reload");
                if (id == 11)
                    Require(Field<OfflineMainlineProgressSession>("progressionSave").Save.World.ActivatedGoddessRegions.Count == 1,
                        "Goddess activation survives a cold reload");
                Destroy(runtime.gameObject);
                yield return null;
            }
            progress = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            Require(progress.ApplyStatueVisit("20261025", "witch", 11, 2801, "probe-witch", -1f, new[] { 28500 },
                Array.Empty<OfflineItemStack>(), 1).IsApplied, "Witch visit transaction");
            Require(progress.ApplyStatueVisit("20261025", "hero", 10, 9002, "probe-hero", 0f, Array.Empty<int>(),
                Array.Empty<OfflineItemStack>(), 1).Status == ProgressionResultStatus.NotReady, "Hero and witch share one daily visit");
            progress = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            Require(progress.HasVisitedStatueOn("20261025") && progress.Save.Player.PendingStatueBattleEffects.Count == 1,
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
                Vector3 world = (Vector3)typeof(OfficialMainlineMapRuntime).GetMethod("GridToWorld", Private).Invoke(runtime, new object[] { target });
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
            yield return Click(mini.transform.Find(buttonPath.Substring("/MiniMapMaskedUI/".Length)).GetComponent<Button>());
            Require(filters["MiniMapGoddess"] != before, "Minimap filter restores from JSON");
            yield return Click(Field<GameObject>("fullMiniMapPanel").transform.Find("Content/CloseBtn_dark").GetComponent<Button>());

            var progress = Field<OfflineMainlineProgressSession>("progressionSave");
            var entities = Field<IDictionary>("mapEntities");
            object goddess = entities[202]; // pinned Map11 lost goddess placement
            progress.SetMapRegionProgress(11, 1, 10000);
            Invoke("ShowGoddessDialog", goddess); // dialog fixture; activation itself uses pointer events
            yield return new WaitForSecondsRealtime(.5f);
            yield return Capture("map-11-goddess");
            yield return Click(Field<GoddessTouchDialog>("goddessDialog").myTouchBtn);
            Require(progress.Save.World.ActivatedGoddessRegions.Count == 1, "Pointer activates the original goddess stage");
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
            Require(target == button.gameObject, "Original button receives the topmost raycast at " + button.name);
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

        private T Field<T>(string name) => (T)typeof(OfficialMainlineMapRuntime).GetField(name, Private).GetValue(runtime);
        private void Invoke(string name, params object[] args) => typeof(OfficialMainlineMapRuntime).GetMethod(name, Private).Invoke(runtime, args);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private void Complete(bool success)
        {
            if (finished) return;
            finished = true;
            if (runtime != null) Destroy(runtime.gameObject);
            completed?.Invoke(success);
        }
    }
}
#endif
