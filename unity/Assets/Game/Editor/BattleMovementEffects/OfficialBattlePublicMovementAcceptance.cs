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
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.ProfessionUI;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Official.BattleDamageUI;


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
            ConfigureBackgroundGameView();
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".result", 1);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 300);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Hook();
            EditorApplication.isPlaying = true;
        }
        private static void ConfigureBackgroundGameView()
        {
            // EditorWindow.Show/GetWindow always focus a newly created native window in
            // this editor version. Use the verified ContainerWindow entry with setFocus=false.
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var editor = typeof(EditorWindow).Assembly;
            var sizesType = editor.GetType("UnityEditor.GameViewSizes", true);
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance").GetValue(null);
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] {
                Enum.Parse(editor.GetType("UnityEditor.GameViewSizeGroupType", true), "Standalone") });
            int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null), selected = -1;
            for (int i = 0; i < count; i++)
            {
                var size = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                if ((int)size.GetType().GetProperty("width").GetValue(size) == 450
                    && (int)size.GetType().GetProperty("height").GetValue(size) == 975) { selected = i; break; }
            }
            if (selected < 0)
            {
                var size = Activator.CreateInstance(editor.GetType("UnityEditor.GameViewSize", true), flags,
                    null, new[] { Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeType", true), 1),
                        (object)450, 975, "Public battle verification" }, null);
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                selected = count;
            }
            var viewType = editor.GetType("UnityEditor.GameView", true);
            foreach (EditorWindow old in Resources.FindObjectsOfTypeAll(viewType)) old.Close();
            var view = (EditorWindow)ScriptableObject.CreateInstance(viewType);
            var bounds = EditorGUIUtility.GetMainWindowPosition();
            view.titleContent = new GUIContent("Public battle verification");
            view.position = new Rect(bounds.x + 40, bounds.y + 40, 450, 996);
            var hostType = editor.GetType("UnityEditor.HostView", true);
            var host = ScriptableObject.CreateInstance(hostType);
            hostType.GetProperty("actualView", flags).SetValue(host, view);
            var containerType = editor.GetType("UnityEditor.ContainerWindow", true);
            var container = ScriptableObject.CreateInstance(containerType);
            containerType.GetProperty("position", flags).SetValue(container, view.position);
            containerType.GetProperty("rootView", flags).SetValue(container, host);
            typeof(EditorWindow).GetMethod("MakeParentsSettingsMatchMe", flags).Invoke(view, null);
            containerType.GetMethod("Show", flags).Invoke(container, new[] {
                Enum.Parse(editor.GetType("UnityEditor.ShowMode", true), "NormalWindow"),
                (object)false, true, false });
            viewType.GetProperty("selectedSizeIndex", flags).SetValue(view, selected);
            view.Repaint();
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
            AudioListener.volume = 0f;
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
            yield return Approach(capture, false);
            yield return Approach(capture, true);
            yield return Approach(capture, false, true);
            yield return Approach(capture, false, false, true);
        }

        private static IEnumerator Approach(Action<string> capture, bool monsterActs, bool skillCost = false, bool hemophagia = false)
        {
            string prefix = hemophagia ? "public-lifesteal" : skillCost ? "public-cost" : monsterActs ? "public-monster" : "public-movement";
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
            Texture2D head = null;
            Sprite portrait = null;
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
                var player = Unit(1, 1, 1, 2, 2, monsterActs ? 1 : 100);
                player.RoleEntityId = 3100;
                var monster = Unit(2, 2, 2, 6, 4, monsterActs ? 100 : 1);
                monster.MonsterId = monster.MonsterTemplateId = 3206;
                monster.RoleEntityId = 3206;
                var actor = monsterActs ? monster : player;
                var target = monsterActs ? player : monster;
                actor.Properties.Add(new FightPropertyValue { Id = rules.PropertyType("MoveDist"), Value = 4 });
                Require(state.Grid.CanStand(new FightGridPoint(3, 2), actor.MoveMask, null),
                    "The controlled grid must permit the acting unit movement mask.");
                int itemId = monsterActs ? rules.MonsterActiveSkillItems(3206, 0).Single() : 10011;
                Require(itemId == (monsterActs ? 14004 : 10011), "The original skill item identity changed.");
                int skillEntityId = assets.LoadSkillItems().Required(itemId).EntityId;
                Require(skillEntityId == (monsterActs ? 16020 : 10630), "The original skill entity identity changed.");
                var skillProperties = rules.CalculateSkillProps(skillEntityId, 1, 1, 1)
                    .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList();
                var skill = rules.CreateSkill(skillEntityId, 0, itemId, 1, 1, 1, skillProperties);
                if (skillCost) { skill.CostType = 1; skill.SkillCost = 7; }
                if (hemophagia)
                {
                    int property = rules.PropertyType("OnceHitHemophagiaPer");
                    Require(property > 0, "Original lifesteal property is missing.");
                    actor.Properties.Add(new FightPropertyValue { Id = property, Value = 10000 });
                    foreach (var hit in skill.Hits) hit.HemophagiaProperty = property;
                    foreach (var hit in skill.HitGraphEntities) hit.HemophagiaProperty = property;
                    actor.Hp /= 2; actor.Turn.Hp = actor.Hp;
                }
                actor.Skills.Add(skill);
                long cooldown = rules.CalculateSkillCooldown(skillEntityId, 1, 1, 1, actor.Properties.ToDictionary(p => p.Id, p => p.Value));
                actor.SkillState.Slots.Add(rules.CreateSkillSlot(skill, cooldown));
                state.Units.Add(player);
                state.Units.Add(monster);
                var photography = CharacterPortraitPhotography.LoadBattleHead();
                var portraitModel = CharacterCreationNativeModelFactory.CreateArenaGuardModel(0, 1, null, photography.excludes);
                try
                {
                    portraitModel.transform.position = new Vector3(-1024, -1024, -1024);
                    Require(OfficialBattleCharacterAnimationCatalog.TryResolve(
                        portraitModel.GetComponent<Animator>().runtimeAnimatorController, photography.animation,
                        out string pose), "Original head photography pose is unavailable.");
                    head = CharacterNativePortraitCapture.Capture(portraitModel, photography, pose);
                    Require(head.width == 100 && head.height == 120
                        && head.GetPixels32().Count(pixel => pixel.a > 128) > 1000,
                        "Original player head photograph is empty or incorrectly sized.");
                    portrait = Sprite.Create(head, new Rect(0, 0, 100, 120), new Vector2(.5f, .5f), 100);
                }
                finally { UnityEngine.Object.Destroy(portraitModel); }
                controller.Begin(new OfficialBattleRequest
                {
                    Checkpoint = state, WorldRoot = world.transform, HudParent = canvasObject.transform,
                    InitialStepDelay = 0, CombatAssets = assets,
                    TimelinePortrait = unit => unit.Id == player.Id ? portrait : null,
                    PlayerAppearance = new OfficialBattlePlayerAppearanceSource(1, 17, () => profile,
                        _ => profile.InitialWeaponType),
                    Options = new OfficialBattleSessionOptions { SkipPrepareStatus = false }
                });
                Require(actor.MoveDist == 4, "Original startup did not derive the controlled movement distance.");
                var mapper = new OfficialBattleGridMapper(world.transform, state.Grid);
                camera.transform.position = mapper.GridToWorld(new FightGridPoint(4, 3)) + new Vector3(0, 0, -20);
                for (int i = 0; i < 120; i++) yield return null;
                Require(controller.Phase == FightBattlePhase.Preparing, "Preparation was skipped.");
                var start = canvasObject.transform.Find(OfficialBattleHudView.StartButtonPath.TrimStart('/'))
                    ?.GetComponent<Button>();
                Require(start != null && start.IsInteractable(), "Original start button is unavailable.");
                capture(prefix + "-preparing");
                yield return null;
                yield return Click(events, start);
                Require(controller.Phase == FightBattlePhase.Play, "Simulated pointer did not start battle.");
                FightBattleExecutionResult movementResult = null;
                long initialTargetHp = target.Hp;
                long initialActorHp = actor.Hp;
                controller.StepCompleted += result =>
                {
                    if (result.Event.Movements.Any(m => m.UnitId == actor.Id && m.Path.Count > 1)) movementResult = result;
                };
                for (int i = 0; i < 600 && movementResult == null; i++)
                {
                    controller.Tick(Time.deltaTime);
                    yield return null;
                }
                if (movementResult == null)
                    System.IO.File.WriteAllText(System.IO.Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                        prefix + "-no-approach.json"), FightBattleCheckpointSerializer.Save(state));
                Require(movementResult != null, "Actual executor did not produce player movement.");
                var movement = movementResult.Event.Movements.First(m => m.UnitId == actor.Id && m.Path.Count > 1);
                Require(movement.Path.Count == 2, "Ordinary movement must contain one original jump segment.");
                var jump = movement.Path[1] - movement.Path[0];
                Require(jump.X * jump.X + jump.Y * jump.Y <= 16
                    && Math.Abs(jump.X) + Math.Abs(jump.Y) > 4,
                    "The controlled jump must exercise a legal Euclidean landing beyond four walk steps.");
                Require(movement.MoveSkillEntityId == (monsterActs ? 19810 : 15300)
                    && movement.MoveBehaviourId == (monsterActs ? 19811 : 15301),
                    "Actual executor replaced the original movement identity.");
                Require(movementResult.Event.SkillCasts.Count(c => c.ActorId == actor.Id && c.SkillEntityId == skillEntityId) == 1,
                    "The approach step must contain exactly one original skill cast.");
                var damages = movementResult.Event.DamageEvents.Where(d => d.SourceId == actor.Id
                    && d.TargetId == target.Id && d.SkillEntityId == skillEntityId).ToArray();
                Require(damages.Length == (monsterActs ? 1 : 3) && damages.All(d => d.Damage < 0)
                    && target.Hp == initialTargetHp + damages.Sum(d => d.Damage),
                    "The original hits must settle exactly once against the controlled target.");
                var entity = controller.TryGetUnitTransform(actor.Id);
                var nativeMonster = entity?.GetComponentInChildren<BattleNativeMonsterRenderer>();
                Require(entity != null && (monsterActs
                    ? nativeMonster != null && nativeMonster.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0
                    : entity.GetComponentInChildren<ProfessionNativeSceneRenderer>() != null),
                    "Public battle did not create its acting native model.");
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
                capture(prefix + "-paused");
                yield return null;
                controller.SetPaused(false);
                var nativeSkills = new HashSet<int>();
                var castIdentities = new HashSet<EcEntityIdentifier>();
                var nativeHitIdentities = new HashSet<EcEntityIdentifier>();
                var castReceipt = movementResult.Event.SkillCasts.Single(c => c.ActorId == actor.Id
                    && c.SkillEntityId == skillEntityId);
                if (skillCost) Require(castReceipt.HasSkillCostHp && castReceipt.SkillCostBeforeHp == initialActorHp
                    && castReceipt.SkillCostAfterHp == initialActorHp - 7 && actor.Hp == initialActorHp - 7,
                    "Formal controller lost the actual cast cost or settled it twice.");
                if (hemophagia)
                {
                    var heals = movementResult.Event.DamageEvents.Where(d => d.IsCure).ToArray();
                    Require(heals.Length == 3 && heals.All(h => h.DamageEntityId == 0
                        && damages.Any(d => d.DamageId == h.DamageId))
                        && actor.Hp == initialActorHp + heals.Sum(h => h.Damage),
                        "Three actual derived heals must retain their source serials and settle exactly once: "
                        + heals.Length + " heals, HP " + initialActorHp + " -> " + actor.Hp
                        + ", recovered " + heals.Sum(h => h.Damage) + ".");
                }
                var fixedClasses = new HashSet<int>(skill.HitGraphEntities.Where(h =>
                    h.HitComponentType == "FightHitFixedComponent").Select(h => h.EntityId));
                var fixedReceipts = castReceipt.HitSelections.Where(h => fixedClasses.Contains(h.HitEntityId)).ToArray();
                int expectedNativeHits = fixedReceipts.Sum(h => h.EffectCenters.Count);
                Require(expectedNativeHits > 0, "The original controlled skill has no fixed hit receipts.");
                int nativeFrames = 0;
                bool nativeFloatSeen = false;
                bool costFloatSeen = false;
                bool derivedFloatSeen = false;
                var slotBinding = OfficialBattleSkillSlotBindings.Load().slots[0];
                for (int i = 0; i < 600 && controller.IsPresentingStep; i++)
                {
                    bool captureNativeFloat = false;
                    bool captureCostFloat = skillCost && !costFloatSeen
                        && world.GetComponentsInChildren<CharacterDamageUI>().Any(ui => ui.EntityId == new EcEntityIdentifier(1, actor.Id)
                            && ui.GetComponentsInChildren<TextMesh>(true).Any(text => text.text == "7"));
                    costFloatSeen |= captureCostFloat;
                    var compatibleFloats = root.GetComponentsInChildren<OfficialBattleDamageFloat>();
                    if (!hemophagia) Require(compatibleFloats.Length == 0,
                        "The old step damage float duplicated an original native damage notification.");
                    if (hemophagia) Require(compatibleFloats.All(view => view.TargetId == actor.Id),
                        "A direct target damage float duplicated the native notification.");
                    bool captureDerivedFloat = hemophagia && !derivedFloatSeen && compatibleFloats.Length > 0;
                    derivedFloatSeen |= captureDerivedFloat;
                    if (world.GetComponentsInChildren<CharacterDamageUI>().Any(ui => ui.EntityId == new EcEntityIdentifier(1, target.Id)))
                    {
                        captureNativeFloat = !nativeFloatSeen;
                        nativeFloatSeen = true;
                    }
                    foreach (var native in world.GetComponentsInChildren<MyFightSkill>())
                        if (native.myEntity.Entity.ClassId == skillEntityId)
                        {
                            Require((entity.position - mapper.GridToWorld(new FightGridPoint(actor.X, actor.Y))).sqrMagnitude < 1e-7f,
                                "The native skill started before the actor reached the settled grid.");
                            nativeSkills.Add(native.GetInstanceID());
                            castIdentities.Add(native.myEntity.Id);
                            string[] selected = slotBinding.states.First(s => s.state == (monsterActs ? 0 : 2)).objects;
                            foreach (string path in slotBinding.states.SelectMany(s => s.objects).Distinct())
                            {
                                var node = canvasObject.transform.Find(path.TrimStart('/'));
                                Require(node != null && node.gameObject.activeSelf == selected.Contains(path),
                                    "Original skill slot did not reflect the native skill start: " + path);
                            }
                            if (++nativeFrames == 4) capture(prefix + "-skill");
                        }
                    foreach (var hit in world.GetComponentsInChildren<MyFightHitFixed>())
                    {
                        if (!fixedClasses.Contains(hit.myEntity.Entity.ClassId)) continue;
                        Require(castIdentities.Contains(hit.Component.SkillId),
                            "A fixed hit is not associated with its actual native cast identity.");
                        var cell = hit.Component.GridTransform.Pos;
                        Require(fixedReceipts.Any(h => h.HitEntityId == hit.myEntity.Entity.ClassId
                            && h.EffectCenters.Any(p => p.X == cell.X && p.Y == cell.Y)),
                            "A native fixed hit moved away from its settled effect centre.");
                        if (nativeHitIdentities.Add(hit.myEntity.Id) && nativeHitIdentities.Count == 1)
                            capture(prefix + "-hit");
                    }
                    yield return null;
                    if (captureNativeFloat)
                    {
                        capture(prefix + "-native-damage-ui");
                        yield return null; // Unity accepts one ScreenCapture request per rendered frame.
                    }
                    if (captureCostFloat) { capture(prefix + "-native-cost-ui"); yield return null; }
                    if (captureDerivedFloat) { capture(prefix + "-derived-cure-ui"); yield return null; }
                }
                Require(!controller.IsPresentingStep, "Movement step presentation did not complete.");
                Require(nativeSkills.Count == 1 && nativeFrames >= 4,
                    "The approach step did not play exactly one original skill entity with its HUD state.");
                Require(nativeHitIdentities.Count == expectedNativeHits,
                    "The public battle did not create every settled fixed hit exactly once: "
                    + nativeHitIdentities.Count + "/" + expectedNativeHits);
                Require(nativeFloatSeen, "The common battle controller did not display the original role damage UI.");
                if (skillCost) Require(costFloatSeen, "The common battle controller did not display the original skill cost UI.");
                if (hemophagia) Require(derivedFloatSeen, "The originating native damage suppressed its derived compatible cure UI.");
                var settledSlot = actor.SkillState.Slots.Single(s => s.Slot == 0);
                Require(settledSlot.Cooldown == cooldown && settledSlot.ReleasedTimes == 1,
                    "The settled original cooldown or release count differs.");
                string[] endedObjects = slotBinding.states.First(s => s.state == (monsterActs ? 0 : 4)).objects;
                foreach (string path in slotBinding.states.SelectMany(s => s.objects).Distinct())
                {
                    var node = canvasObject.transform.Find(path.TrimStart('/'));
                    Require(node != null && node.gameObject.activeSelf == endedObjects.Contains(path),
                        "Original skill slot did not reflect native skill removal: " + path);
                }
                Require((entity.position - mapper.GridToWorld(new FightGridPoint(actor.X, actor.Y))).sqrMagnitude < 1e-7f,
                    "Public battle actor did not reach its settled grid.");
                Require((entity.localScale.x < 0) == actor.FlipX && state.Counter == counter,
                    "Public battle changed final facing or advanced without a host tick.");
                Require(JsonUtility.ToJson(profile) == beforeProfile, "Movement changed the appearance profile.");
                capture(prefix + "-settled");
                yield return null;
                Debug.Log("Public " + (monsterActs ? "monster" : "movement")
                    + " battle passed: original HUD pointer start, executor approach, native model, pause, final grid, one original skill entity, "
                    + damages.Length + " settled hits, " + nativeHitIdentities.Count
                    + " original fixed hit entities, cooldown " + cooldown + " and main-player skill slot state.");
            }
            finally
            {
                controller.DisposeSession();
                if (portrait != null) UnityEngine.Object.Destroy(portrait);
                if (head != null) UnityEngine.Object.Destroy(head);
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
