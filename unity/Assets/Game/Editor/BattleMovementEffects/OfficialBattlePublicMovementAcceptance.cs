using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.ProfessionUI;
using Zjcs.Presentation.Combat;


namespace Zjcs.Editor
{
    public static class OfficialBattlePublicMovementAcceptance
    {
        private const string Key = "Zjcs.BattlePublicMovement.Acceptance";
        private static bool started;
        private static int errors;
        public static void Run()
        {
            Environment.SetEnvironmentVariable("ZJCS_CREATION_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Type.GetType("Zjcs.Editor.ProjectTools, Zjcs.Editor", true).GetMethod("Validate").Invoke(null, null);
            RunIsolated();
        }
        public static void RunIsolated()
        {
            Environment.SetEnvironmentVariable("ZJCS_CREATION_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Type.GetType("Zjcs.Editor.Home.HomeAcceptanceLauncher, Zjcs.Editor", true)
                .GetMethod("ConfigurePortraitGameView", System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .Invoke(null, new object[] { 450, 975 });
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 300);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod] private static void Hook()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.playModeStateChanged += Mode;
        }
        private static void Mode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            Application.logMessageReceived -= Observe;
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= Mode;
            EditorApplication.Exit(SessionState.GetInt(Key + ".result", 1));
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Key + ".deadline", 0))
            {
                Debug.LogError("Public movement battle timed out.");
                EditorApplication.isPlaying = false;
                return;
            }
            if (started || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            started = true;
            Application.logMessageReceived += Observe;
            new GameObject("Public movement verification runner").AddComponent<Runner>().StartCoroutine(Verify());
        }
        private static void Observe(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        }
        private static IEnumerator Verify()
        {
            var stack = new Stack<IEnumerator>();
            Action<string> capture = name => ScreenCapture.CaptureScreenshot(
                Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name + ".png"));
            stack.Push(Steps(capture));
            bool passed = false;
            try
            {
                while (stack.Count > 0)
                {
                    var current = stack.Peek();
                    if (!current.MoveNext())
                    {
                        (stack.Pop() as IDisposable)?.Dispose();
                        continue;
                    }
                    if (current.Current is IEnumerator nested) stack.Push(nested);
                    else yield return current.Current;
                }
                passed = errors == 0;
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                SessionState.SetInt(Key + ".result", passed ? 0 : 1);
                EditorApplication.isPlaying = false;
            }
        }
        public static IEnumerator Steps(Action<string> capture)
        {
            var root = new GameObject("Public movement battle verification");
            var world = new GameObject("Battle world");
            world.transform.SetParent(root.transform, false);
            var cameraObject = new GameObject("Battle verification camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 6;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var canvasObject = new GameObject("Battle verification canvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(root.transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var eventObject = new GameObject("Battle verification pointer", typeof(EventSystem));
            eventObject.transform.SetParent(root.transform, false);
            var events = eventObject.GetComponent<EventSystem>();
            var controller = world.AddComponent<OfficialBattleRuntimeController>();
            float previousCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60;
            try
            {
                var appearance = Resources.Load<AppearanceConfig>("Combat/SceneAppearance/AppearanceConfig");
                var resolver = new ProfessionAppearanceResolver(ProfessionAppearanceTables.Load(
                    Resources.Load<TextAsset>("Combat/SceneAppearance/appearance-tables").text));
                var profile = new OfflinePlayerProgress { InitialWeaponType = 200 };
                profile.Wardrobe.Sex = (int)Sex.Male;
                profile.Wardrobe.Profession = 1;
                foreach (var skin in resolver.BuildDefaultAppearanceData(appearance, Sex.Male, (Profession)1).SkinMap)
                    profile.Wardrobe.BaseSkins.Add(new OfflineAppearanceSkin { Type = (int)skin.Key, Id = skin.Value.SkinId });
                string beforeProfile = JsonUtility.ToJson(profile);
                var assets = OfficialCombatAssetCatalog.LoadDefault();
                var rules = assets.Load();
                var state = new FightBattleCheckpoint { StageId = 1001, RandomSeed = 43,
                    Grid = new FightBattleGrid { Width = 8, Height = 8 },
                    ResultRules = new FightBattleResultRules() };
                var player = Unit(1, 1, 1, 2, 2, 100);
                player.RoleEntityId = 3100;
                player.Properties.Add(new FightPropertyValue { Id = rules.PropertyType("MoveDist"), Value = 4 });
                Require(state.Grid.CanStand(new FightGridPoint(3, 2), player.MoveMask, null),
                    "The controlled grid must permit the original player movement mask.");
                var monster = Unit(2, 2, 2, 6, 2, 1);
                monster.MonsterId = monster.MonsterTemplateId = 3206;
                monster.RoleEntityId = 3206;
                int skillEntityId = assets.LoadSkillItems().Required(10011).EntityId;
                var skillProperties = rules.CalculateSkillProps(skillEntityId, 1, 1, 1)
                    .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList();
                var skill = rules.CreateSkill(skillEntityId, 0, 10011, 1, 1, 1, skillProperties);
                player.Skills.Add(skill);
                long cooldown = rules.CalculateSkillCooldown(skillEntityId, 1, 1, 1, player.Properties.ToDictionary(p => p.Id, p => p.Value));
                player.SkillState.Slots.Add(rules.CreateSkillSlot(skill, cooldown));
                state.Units.Add(player);
                state.Units.Add(monster);
                controller.Begin(new OfficialBattleRequest
                {
                    Checkpoint = state, WorldRoot = world.transform, HudParent = canvasObject.transform,
                    InitialStepDelay = 0, CombatAssets = assets,
                    PlayerAppearance = new OfficialBattlePlayerAppearanceSource(1, 17, () => profile,
                        _ => profile.InitialWeaponType),
                    Options = new OfficialBattleSessionOptions { SkipPrepareStatus = false }
                });
                Require(player.MoveDist == 4, "Original startup did not derive the controlled movement distance.");
                var mapper = new OfficialBattleGridMapper(world.transform, state.Grid);
                camera.transform.position = mapper.GridToWorld(new FightGridPoint(4, 3)) + new Vector3(0, 0, -20);
                for (int i = 0; i < 120; i++) yield return null;
                Require(controller.Phase == FightBattlePhase.Preparing, "Preparation was skipped.");
                var start = canvasObject.transform.Find(OfficialBattleHudView.StartButtonPath.TrimStart('/'))
                    ?.GetComponent<Button>();
                Require(start != null && start.IsInteractable(), "Original start button is unavailable.");
                capture("public-movement-preparing");
                yield return null;
                yield return Click(events, start);
                Require(controller.Phase == FightBattlePhase.Play, "Simulated pointer did not start battle.");
                FightBattleExecutionResult movementResult = null;
                long initialTargetHp = monster.Hp;
                controller.StepCompleted += result =>
                {
                    if (result.Event.Movements.Any(m => m.UnitId == 1 && m.Path.Count > 1)) movementResult = result;
                };
                for (int i = 0; i < 600 && movementResult == null; i++)
                {
                    controller.Tick(Time.deltaTime);
                    yield return null;
                }
                if (movementResult == null)
                    System.IO.File.WriteAllText(System.IO.Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                        "public-movement-no-approach.json"), FightBattleCheckpointSerializer.Save(state));
                Require(movementResult != null, "Actual executor did not produce player movement.");
                var movement = movementResult.Event.Movements.First(m => m.UnitId == 1 && m.Path.Count > 1);
                Require(movement.MoveSkillEntityId == 15300 && movement.MoveBehaviourId == 15301,
                    "Actual executor replaced the original movement identity.");
                Require(movementResult.Event.SkillCasts.Count(c => c.ActorId == 1 && c.SkillEntityId == skillEntityId) == 1,
                    "The approach step must contain exactly one original skill cast.");
                var damages = movementResult.Event.DamageEvents.Where(d => d.SourceId == 1
                    && d.TargetId == 2 && d.SkillEntityId == skillEntityId).ToArray();
                Require(damages.Length == 3 && damages.All(d => d.Damage < 0)
                    && monster.Hp == initialTargetHp + damages.Sum(d => d.Damage),
                    "The original three hits must settle exactly once against the controlled target.");
                var entity = controller.TryGetUnitTransform(1);
                Require(entity != null && entity.GetComponentInChildren<ProfessionNativeSceneRenderer>() != null,
                    "Public battle did not create its native player.");
                int counter = state.Counter;
                for (int i = 0; i < 6; i++) yield return null;
                Require(controller.IsPresentingStep, "Movement step ended before the movement could play.");
                controller.SetPaused(true);
                yield return null;
                Vector3 pausedPosition = entity.position;
                string pausedState = FightBattleCheckpointSerializer.Save(state);
                for (int i = 0; i < 12; i++)
                {
                    Require(!controller.Tick(1f), "A paused public battle advanced its executor.");
                    yield return null;
                }
                Require(entity.position == pausedPosition && state.Counter == counter
                    && FightBattleCheckpointSerializer.Save(state) == pausedState, "Pause changed movement or settled state.");
                capture("public-movement-paused");
                yield return null;
                controller.SetPaused(false);
                var nativeSkills = new HashSet<int>();
                int nativeFrames = 0;
                var slotBinding = OfficialBattleSkillSlotBindings.Load().slots[0];
                for (int i = 0; i < 600 && controller.IsPresentingStep; i++)
                {
                    foreach (var native in world.GetComponentsInChildren<MyFightSkill>())
                        if (native.myEntity.Entity.ClassId == skillEntityId)
                        {
                            Require((entity.position - mapper.GridToWorld(new FightGridPoint(player.X, player.Y))).sqrMagnitude < 1e-7f,
                                "The native skill started before the player reached the settled grid.");
                            nativeSkills.Add(native.GetInstanceID());
                            string[] selected = slotBinding.states.First(s => s.state == 2).objects;
                            foreach (string path in slotBinding.states.SelectMany(s => s.objects).Distinct())
                            {
                                var node = canvasObject.transform.Find(path.TrimStart('/'));
                                Require(node != null && node.gameObject.activeSelf == selected.Contains(path),
                                    "Original skill slot did not reflect the native skill start: " + path);
                            }
                            if (++nativeFrames == 4) capture("public-movement-skill");
                        }
                    yield return null;
                }
                Require(!controller.IsPresentingStep, "Movement step presentation did not complete.");
                Require(nativeSkills.Count == 1 && nativeFrames >= 4,
                    "The approach step did not play exactly one original skill entity with its HUD state.");
                var settledSlot = player.SkillState.Slots.Single(s => s.Slot == 0);
                Require(settledSlot.Cooldown == cooldown && settledSlot.ReleasedTimes == 1,
                    "The settled original cooldown or release count differs.");
                string[] endedObjects = slotBinding.states.First(s => s.state == 4).objects;
                foreach (string path in slotBinding.states.SelectMany(s => s.objects).Distinct())
                {
                    var node = canvasObject.transform.Find(path.TrimStart('/'));
                    Require(node != null && node.gameObject.activeSelf == endedObjects.Contains(path),
                        "Original skill slot did not reflect native skill removal: " + path);
                }
                Require((entity.position - mapper.GridToWorld(new FightGridPoint(player.X, player.Y))).sqrMagnitude < 1e-7f,
                    "Public battle player did not reach its settled grid.");
                Require((entity.localScale.x < 0) == player.FlipX && state.Counter == counter,
                    "Public battle changed final facing or advanced without a host tick.");
                Require(JsonUtility.ToJson(profile) == beforeProfile, "Movement changed the appearance profile.");
                capture("public-movement-settled");
                yield return null;
                Debug.Log("Public movement battle passed: original HUD pointer start, executor approach, native player, pause, final grid, one original skill entity, three settled hits and skill slot/CD.");
            }
            finally
            {
                controller.DisposeSession();
                Time.captureDeltaTime = previousCaptureDelta;
                UnityEngine.Object.Destroy(root);
            }
            yield return null;
        }

        private static FightBattleUnitState Unit(int id, int side, int type, int x, int y, int speed)
        {
            const long hp = 1000000;
            var value = new FightBattleUnitState { Id = id, Side = side, CharacterType = type,
                Hp = hp, MaxHp = hp, LastMaxHp = hp, Level = 1, SubRank = 1, X = x, Y = y,
                Turn = new FightTurnUnit { Id = id, Hp = hp, Speed = speed } };
            value.Properties.Add(new FightPropertyValue { Id = 1, Value = 100 });
            value.Properties.Add(new FightPropertyValue { Id = 2, Value = 100 });
            value.Properties.Add(new FightPropertyValue { Id = 3, Value = hp });
            value.Properties.Add(new FightPropertyValue { Id = 4, Value = speed });
            return value;
        }

        private static IEnumerator Click(EventSystem events, Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(events) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            events.RaycastAll(pointer, hits);
            Require(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject,
                "The original start button is occluded at its simulated pointer position.");
            pointer.pointerCurrentRaycast = hits[0];
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
            yield return null;
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Public movement battle: " + message);
        }

        private sealed class Runner : MonoBehaviour { }
    }
}
