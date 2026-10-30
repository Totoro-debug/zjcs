#if DEVELOPMENT_BUILD || ZJCS_MAINLINE_MODULE_VALIDATION
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;
using Zjcs.Offline.World;

namespace Zjcs.Presentation.World
{
    public sealed class Map11PlayerValidation : MonoBehaviour
    {
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags InstanceMethods = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private string root, slot, resultPath;
        private OfficialMainlineMapRuntime runtime;
        private int awardType = 4;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--zjcs-validate-map11");
            if (index < 0 || index + 1 >= args.Length) return;
            Application.runInBackground = true;
            var runner = new GameObject("Map11PlayerValidation").AddComponent<Map11PlayerValidation>();
            DontDestroyOnLoad(runner.gameObject);
            runner.root = Path.GetFullPath(args[index + 1]);
            runner.resultPath = Path.Combine(runner.root, "map11-player-result.json");
            int awardTypeIndex = Array.IndexOf(args, "--zjcs-map11-award-type");
            if (awardTypeIndex >= 0 && awardTypeIndex + 1 < args.Length
                && int.TryParse(args[awardTypeIndex + 1], out int requestedType)
                && requestedType >= 2 && requestedType <= 4)
                runner.awardType = requestedType;
            Directory.CreateDirectory(runner.root);
            runner.StartCoroutine(runner.Run());
        }

        private IEnumerator Run()
        {
            IEnumerator scenario = RunScenario();
            while (true)
            {
                object next = null;
                Exception failure = null;
                bool hasNext = false;
                try { hasNext = scenario.MoveNext(); if (hasNext) next = scenario.Current; }
                catch (Exception error) { failure = error; }
                if (failure != null)
                {
                    Debug.LogException(failure);
                    WriteResult(false, failure.ToString());
                    Application.Quit(1);
                    yield break;
                }
                if (!hasNext) yield break;
                yield return next;
            }
        }

        private IEnumerator RunScenario()
        {
                DisableStartupUi();
                var slots = new ProfileSlotCatalog(root);
                slot = slots.CreateSlot();
                var save = new OfflineMainlineSave { CreatedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
                OfficialCharacterAssetCatalog characterAssets = OfficialCharacterAssetCatalog.LoadDefault();
                OfficialCharacterCatalog character = characterAssets.Load();
                OfflineSkillRules skillRules = characterAssets.LoadSkillRules();
                int validationSkillId = skillRules.Skills.First(value => value.MaximumRank > 1).ClassId;
                int validationSubRank = character.LevelLimits.Where(value => value.PlayerBlessLevel == 0 && value.PlayerLevel > 20)
                    .Min(value => value.SubRank);
                save.Player.CharacterCreated = true;
                save.Player.CharacterName = "森之国 Player 验证";
                save.Player.InitialWeaponType = 200;
                save.Player.Level = 20;
                save.Player.Experience = character.CumulativeLevelExperience[20];
                save.Player.SubRank = validationSubRank;
                save.Player.Rank = save.Player.SeasonRank = character.RankOfSubRank(validationSubRank);
                save.Player.Wardrobe.Profession = 1;
                save.Player.Skills.Skills.Add(new Zjcs.Offline.Skills.OfflineOwnedSkill { ClassId = validationSkillId, Rank = 1 });
                save.Player.UnlockedMapClassIds.Add(11);
                save.Tasks.ClaimedMainlineTaskIds.AddRange(OfficialMainlineTaskCatalog.LoadDefault().OrderedTasks
                    .Where(value => value.classId <= 1040).Select(value => value.classId));
                File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
                OfflineMainlineProgressSession profile = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
                new OfflineCharacterProgressionService(profile, OfficialCharacterAssetCatalog.LoadDefault().Load())
                    .InitializeWardrobe("map11-player-validation-wardrobe");

                GameObject avatar = CharacterCreationNativeModelFactory.CreateWorldModel(slot, root, out string name);
                var canvasObject = new GameObject("Map11PlayerCanvas", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster));
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = .5f;
                if (EventSystem.current == null) new GameObject("Map11PlayerEvents", typeof(EventSystem), typeof(StandaloneInputModule));
                runtime = new GameObject("Map11PlayerRuntime").AddComponent<OfficialMainlineMapRuntime>();
                runtime.MapMusicVolume = runtime.MapVoiceVolume = () => 0f;
                runtime.Begin(11, slot, root, canvasObject.transform, avatar, name, null);
                yield return new WaitForSecondsRealtime(2);

                profile = GetField<OfflineMainlineProgressSession>(runtime, "progressionSave");
                var service = new OfflineWorldExplorationService(profile);
                OfflineCharacterProgressionService progression = GetField<OfflineCharacterProgressionService>(runtime, "progression");
                var lostGoddessWeights = (System.Collections.Generic.Dictionary<string, object>)progression.Catalog
                    .Rows("game_settings").Single()["LostGoddnessAwardWeights"];
                foreach (string key in lostGoddessWeights.Keys.ToArray())
                    lostGoddessWeights[key] = key == awardType.ToString(System.Globalization.CultureInfo.InvariantCulture) ? 100 : 0;
                var group = service.Catalog.Data.entityGroups.First(value => value.Entitys.Contains(8364));
                OfficialWorldMapSession map = GetField<OfficialWorldMapSession>(runtime, "session");
                MapGridPoint current = GetField<MapGridPoint>(runtime, "currentGrid");
                OfficialMapNavigationView.TryGetCell(map.Definition.Navigation, current.X, current.Y, out int area, out _);
                MapGridPoint spawn = new[] { new MapGridPoint(current.X + 1, current.Y), new MapGridPoint(current.X - 1, current.Y),
                    new MapGridPoint(current.X, current.Y + 1), new MapGridPoint(current.X, current.Y - 1) }.First(map.Movement.IsFit);
                int serial = profile.Save.World.Exploration.NextSerial;
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                ProgressionResult result = profile.ApplyInventoryTransaction("map11-player-validation-fixture", state =>
                {
                    state.World.Exploration.Entities.Add(new OfflineExploreEntity
                    {
                        Serial = state.World.Exploration.NextSerial++, MapClassId = 11, Area = area, Region = 0,
                        X = spawn.X, Y = spawn.Y, Group = group.Group, Variant = group.Id, EntityClassId = 8364,
                        State = service.Catalog.Parameters(8364).PublicDefaultState, CreatedAt = now, ExpiresAt = now + 600
                    });
                });
                Require(result.IsApplied, "Player validation fixture persisted");
                object exploration = GetField<object>(runtime, "exploration");
                exploration.GetType().GetMethod("SynchronizeExploreEntities", InstanceMethods).Invoke(exploration, null);
                runtime.GetType().GetMethod("RefreshEntityActivity", InstanceMethods).Invoke(runtime, null);
                int placement = 1000000000 + serial;
                GameObject generated = runtime.GetComponentInChildren<OfficialMap10EntityView>().FindEntityObject(placement);
                Require(generated != null && generated.activeInHierarchy, "Player Map11 generated entity displayed");
                yield return SimulateEntityClick(placement);

                OfficialLostGoddessResultPopup popup = null;
                float deadline = Time.realtimeSinceStartup + 15;
                while (Time.realtimeSinceStartup < deadline)
                {
                    popup = FindObjectOfType<OfficialLostGoddessResultPopup>();
                    if (popup != null && popup.IsShown) break;
                    yield return null;
                }
                Require(popup != null && popup.IsShown, "Player official lost goddess result displayed");
                Require(profile.Save.World.Exploration.Entities.Single(value => value.Serial == serial).Completed,
                    "Player reward persisted before closing result");
                Require(profile.Save.World.Exploration.Entities.Single(value => value.Serial == serial).LostGoddessAward.Type == awardType,
                    "Player official result branch matches the isolated reward fixture");
                Capture("map11-player-lost-goddess-" + awardType.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".png");
                yield return new WaitForEndOfFrame();
                Button close = popup.GetComponentsInChildren<Button>(true).FirstOrDefault(button =>
                    button.gameObject.activeInHierarchy && button.gameObject.name.StartsWith("CloseObj", StringComparison.Ordinal));
                Require(close != null, "Player original result close button exists");
                bool closeRaycastClear = ClickBlankArea(close);
                Require(closeRaycastClear, "Player result closed by a raycast-confirmed screen-coordinate pointer click");
                yield return new WaitForSecondsRealtime(1);
                Require(FindObjectOfType<OfficialLostGoddessResultPopup>() == null, "Player result closed through UGUI pointer input");
                OfflineMainlineProgressSession cold = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
                OfflineExploreEntity restored = cold.Save.World.Exploration.Entities.Single(value => value.Serial == serial);
                Require(restored.Completed && restored.LostGoddessAward != null && restored.LostGoddessAward.Type == awardType,
                    "Player cold save retained reward type and completion");
                WriteResult(true, "Player loaded Map11, collected generated entity 8364, displayed reward type "
                    + awardType.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + ", closed the original result window through a raycast-confirmed pointer click, and cold-loaded the persisted award.", closeRaycastClear);
                Application.Quit(0);
        }

        private IEnumerator SimulateEntityClick(int placement)
        {
            Camera camera = GetField<Camera>(runtime, "worldCamera");
            GameObject actor = GetField<GameObject>(runtime, "avatar");
            float deadline = Time.realtimeSinceStartup + 15;
            while (GetField<object>(runtime, "movementPath") != null
                || Vector2.Distance(camera.transform.position, actor.transform.position) > .005f)
            {
                Require(Time.realtimeSinceStartup < deadline, "Player map camera settled");
                yield return null;
            }
            Collider2D collider = runtime.GetComponentInChildren<OfficialMap10EntityView>()
                .FindEntityObject(placement).GetComponentInChildren<Collider2D>();
            Vector2 point = camera.WorldToScreenPoint(collider.bounds.center);
            bool consumed = false;
            runtime.PointerPressProvider = () =>
            {
                if (consumed) return null;
                consumed = true;
                return point;
            };
            deadline = Time.realtimeSinceStartup + 2;
            while (!consumed && Time.realtimeSinceStartup < deadline) yield return null;
            runtime.PointerPressProvider = null;
            Require(consumed, "Player map entity received simulated pointer input");
            yield return new WaitForSecondsRealtime(.5f);
        }

        private static bool ClickBlankArea(Button button)
        {
            Canvas.ForceUpdateCanvases();
            var raycaster = EventSystem.current;
            Require(raycaster != null, "Player result has an EventSystem");
            var hits = new System.Collections.Generic.List<RaycastResult>();
            for (int y = 8; y < Screen.height - 8; y += 24)
            for (int x = 8; x < Screen.width - 8; x += 24)
            {
                Vector2 point = new Vector2(x, y);
                var pointer = new PointerEventData(raycaster) { button = PointerEventData.InputButton.Left, position = point };
                hits.Clear();
                raycaster.RaycastAll(pointer, hits);
                if (hits.Count == 0 || (hits[0].gameObject != button.gameObject
                    && !hits[0].gameObject.transform.IsChildOf(button.transform)))
                {
                    continue;
                }
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
                return true;
            }
            return false;
        }

        private static void DisableStartupUi()
        {
            foreach (string name in new[] { "CharacterCreationFlow", "CharacterCreationCanvas", "OfficialStartup" })
            {
                GameObject current = GameObject.Find(name);
                if (current != null) current.SetActive(false);
            }
            GameObject selection = GameObject.Find("OfficialSaveSelectionPanel");
            if (selection != null) selection.transform.root.gameObject.SetActive(false);
        }

        private static T GetField<T>(object target, string name)
        {
            object value;
            if (!FindField(target, name, 0, out value)) throw new MissingFieldException(target.GetType().FullName, name);
            return (T)value;
        }

        private static bool FindField(object target, string name, int depth, out object value)
        {
            value = null;
            if (target == null || depth > 2) return false;
            FieldInfo field = target.GetType().GetField(name, InstanceFields);
            if (field != null)
            {
                value = field.GetValue(target);
                return true;
            }
            foreach (FieldInfo childField in target.GetType().GetFields(InstanceFields))
            {
                if (childField.FieldType.IsPrimitive || childField.FieldType.IsEnum || childField.FieldType == typeof(string)) continue;
                object child = childField.GetValue(target);
                if (child == null) continue;
                if (FindField(child, name, depth + 1, out value)) return true;
            }
            return false;
        }

        private void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(resultPath), name));

        private void WriteResult(bool passed, string message) => WriteResult(passed, message, false);

        private void WriteResult(bool passed, string message, bool resultRaycastClear)
        {
            File.WriteAllText(resultPath, JsonUtility.ToJson(new PlayerValidationResult
            {
                passed = passed, raycastClear = resultRaycastClear, message = message
            }, true));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        [Serializable]
        private sealed class PlayerValidationResult { public bool passed; public bool raycastClear; public string message; }
    }
}
#endif
