using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Presentation.Combat;

namespace Zjcs.Validation.Combat
{
    public static partial class OfficialBattlePublicFlowValidation
    {
        // Exercise the same public battle, HUD, executor and native graph without
        // retaining unrelated home scenes and their assets during this diagnostic.
        public static IEnumerator OpeningSkillSteps(Action<string> capture)
        {
            var source = OfficialBattleGraphCapabilityValidation.FirstPromotionSource();
            var root = new GameObject("Opening skill public battle verification");
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
                var profile = new OfflinePlayerProgress { InitialWeaponType = source.InitialWeaponType };
                profile.Wardrobe.Sex = (int)Sex.Male;
                profile.Wardrobe.Profession = source.Profession;
                foreach (var skin in resolver.BuildDefaultAppearanceData(appearance, Sex.Male,
                    (Profession)source.Profession).SkinMap)
                    profile.Wardrobe.BaseSkins.Add(new OfflineAppearanceSkin { Type = (int)skin.Key, Id = skin.Value.SkinId });
                string beforeProfile = JsonUtility.ToJson(profile);
                var assets = OfficialCombatAssetCatalog.LoadDefault();
                var rules = assets.Load();
                var state = new FightBattleCheckpoint { StageId = 1001, RandomSeed = 43,
                    Grid = new FightBattleGrid { Width = 8, Height = 8 }, ResultRules = new FightBattleResultRules() };
                var player = Unit(1, 1, 1, 2, 2, 100);
                player.RoleEntityId = 3100;
                player.Profession = source.Profession;
                var enemy = Unit(2, 2, 2, 6, 4, 1);
                enemy.MonsterId = enemy.MonsterTemplateId = enemy.RoleEntityId = 3206;
                int initialEntity = assets.LoadSkillItems().Required(source.InitialSkillItemId).EntityId;
                var initial = rules.CreateSkill(initialEntity, 0, source.InitialSkillItemId, 1, 1, 1,
                    rules.CalculateSkillProps(initialEntity, 1, 1, 1)
                        .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
                player.Skills.Add(initial);
                player.SkillState.Slots.Add(rules.CreateSkillSlot(initial, 0));
                state.Units.Add(player);
                state.Units.Add(enemy);
                var probe = OfficialBattleGraphCapabilityValidation.Configure(state);
                var photography = CharacterPortraitPhotography.LoadBattleHead();
                var model = CharacterCreationNativeModelFactory.CreateArenaGuardModel(0, source.Profession,
                    null, photography.excludes);
                try
                {
                    model.transform.position = new Vector3(-1024, -1024, -1024);
                    Require(OfficialBattleCharacterAnimationCatalog.TryResolve(
                        model.GetComponent<Animator>().runtimeAnimatorController, photography.animation,
                        out string pose), "Original head photography pose is unavailable.");
                    head = CharacterNativePortraitCapture.Capture(model, photography, pose);
                    Require(head && head.GetPixels32().Count(pixel => pixel.a > 128) > 1000,
                        "Original opening battle portrait is empty.");
                    portrait = Sprite.Create(head, new Rect(0, 0, 100, 120), new Vector2(.5f, .5f), 100);
                }
                finally { UnityEngine.Object.Destroy(model); }
                controller.Begin(new OfficialBattleRequest {
                    Checkpoint = state, WorldRoot = world.transform, HudParent = canvasObject.transform,
                    InitialStepDelay = 0, CombatAssets = assets,
                    TimelinePortrait = unit => unit.Id == player.Id ? portrait : null,
                    PlayerAppearance = new OfficialBattlePlayerAppearanceSource(1, 17, () => profile,
                        _ => profile.InitialWeaponType),
                    EnvironmentGameId = 1000,
                    HostPresentation = new OfficialBattleHostPresentation(camera, world.transform, null),
                    Options = new OfficialBattleSessionOptions { SkipPrepareStatus = false }
                });
                probe.BindPresentation(controller);
                controller.StepCompleted += result => probe.ObserveStep(controller);
                var mapper = new OfficialBattleGridMapper(world.transform, state.Grid);
                camera.transform.position = mapper.GridToWorld(new FightGridPoint(4, 3)) + new Vector3(0, 0, -20);
                Button start = null;
                float deadline = Time.realtimeSinceStartup + 180f;
                while (!start && Time.realtimeSinceStartup < deadline)
                {
                    start = canvasObject.transform.Find(OfficialBattleHudView.StartButtonPath.TrimStart('/'))
                        ?.GetComponent<Button>();
                    yield return null;
                }
                Require(start && start.IsInteractable() && controller.Phase == FightBattlePhase.Preparing,
                    "Original opening battle start button is unavailable.");
                capture("opening-skill-preparing");
                yield return null;
                yield return Click(eventObject.GetComponent<EventSystem>(), start);
                Require(controller.Phase == FightBattlePhase.Play, "Simulated pointer did not start opening battle.");
                deadline = Time.realtimeSinceStartup + 180f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    controller.Tick(Time.deltaTime);
                    probe.ObserveFrame(controller);
                    if (!string.IsNullOrEmpty(probe.CaptureName)) capture(probe.CaptureName);
                    if (controller.Checkpoint.Finished) break;
                    yield return null;
                }
                probe.Verify(controller.Checkpoint);
                Require(JsonUtility.ToJson(profile) == beforeProfile, "Battle verification changed the character profile.");
                capture("opening-skill-settled");
                yield return null;
                Debug.Log("PUBLIC_OPENING_SKILL_PASSED original HUD pointer, native graph, action, opening cost and checkpoint");
            }
            finally
            {
                controller.DisposeSession();
                if (portrait) UnityEngine.Object.Destroy(portrait);
                if (head) UnityEngine.Object.Destroy(head);
                Time.captureDeltaTime = previousCaptureDelta;
                UnityEngine.Object.Destroy(root);
            }
            yield return null;
        }
    }
}
