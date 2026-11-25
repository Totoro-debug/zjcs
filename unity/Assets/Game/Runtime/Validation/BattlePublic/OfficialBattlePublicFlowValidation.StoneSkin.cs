using System;
using System.Collections;
using System.IO;
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
        public static IEnumerator StoneSkinSteps(Action<string> capture)
        {
            var root = new GameObject("Stone skin public battle verification");
            var world = new GameObject("Battle world");
            world.transform.SetParent(root.transform, false);
            var cameraObject = new GameObject("Battle verification camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform, false);
            var camera = cameraObject.GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 6;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var canvas = new GameObject("Battle verification canvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(root.transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
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
                var profile = new OfflinePlayerProgress { InitialWeaponType = 200 };
                profile.Wardrobe.Sex = (int)Sex.Male; profile.Wardrobe.Profession = 3;
                foreach (var skin in resolver.BuildDefaultAppearanceData(appearance, Sex.Male, (Profession)3).SkinMap)
                    profile.Wardrobe.BaseSkins.Add(new OfflineAppearanceSkin { Type = (int)skin.Key, Id = skin.Value.SkinId });
                string beforeProfile = JsonUtility.ToJson(profile);
                var assets = OfficialCombatAssetCatalog.LoadDefault();
                var rules = assets.Load();
                var state = new FightBattleCheckpoint { StageId = 1001, RandomSeed = 17, MaxRound = 5,
                    Grid = new FightBattleGrid { Width = 8, Height = 8 }, ResultRules = new FightBattleResultRules() };
                var player = Unit(1, 1, 1, 2, 2, 900);
                player.RoleEntityId = 3100; player.Profession = 3; player.SubRank = 5;
                var enemy = Unit(2, 2, 2, 3, 2, 1000);
                enemy.MonsterId = enemy.MonsterTemplateId = enemy.RoleEntityId = 3206;
                var attack = rules.CreateSkill(10600, 0, 10008, 7, 20, 5,
                    rules.CalculateSkillProps(10600, 7, 20, 5)
                        .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
                enemy.Skills.Add(attack); enemy.SkillState.Slots.Add(rules.CreateSkillSlot(attack, 0));
                var response = rules.CreateSkill(10600, 0, 10008, 7, 20, 5,
                    rules.CalculateSkillProps(10600, 7, 20, 5)
                        .Select(pair => new FightPropertyValue { Id = pair.Key, Value = pair.Value }).ToList());
                player.Skills.Add(response); player.SkillState.Slots.Add(rules.CreateSkillSlot(response, 0));
                var listener = rules.CreateStatus(25033, 1, 1, 725033, 25033, 10542, 7, 30, 5, player.Properties);
                player.Statuses.Add(listener);
                var initial = rules.CreateStatus(24229, 1, 1, 724229, 25033, 10542, 7, 30, 5, player.Properties);
                initial.StackCount = 8; player.Statuses.Add(initial);
                state.Units.Add(player); state.Units.Add(enemy);
                var photography = CharacterPortraitPhotography.LoadBattleHead();
                var model = CharacterCreationNativeModelFactory.CreateArenaGuardModel(0, 3, null, photography.excludes);
                try
                {
                    model.transform.position = new Vector3(-1024, -1024, -1024);
                    Require(OfficialBattleCharacterAnimationCatalog.TryResolve(model.GetComponent<Animator>().runtimeAnimatorController,
                        photography.animation, out string pose), "Original head photography pose is unavailable.");
                    head = CharacterNativePortraitCapture.Capture(model, photography, pose);
                    Require(head && head.GetPixels32().Count(pixel => pixel.a > 128) > 1000,
                        "Original battle portrait is empty.");
                    portrait = Sprite.Create(head, new Rect(0, 0, 100, 120), new Vector2(.5f, .5f), 100);
                }
                finally { UnityEngine.Object.Destroy(model); }
                controller.Begin(new OfficialBattleRequest {
                    Checkpoint = state, WorldRoot = world.transform, HudParent = canvas.transform,
                    InitialStepDelay = 0, CombatAssets = assets,
                    TimelinePortrait = unit => unit.Id == player.Id ? portrait : null,
                    PlayerAppearance = new OfficialBattlePlayerAppearanceSource(1, 17, () => profile,
                        _ => profile.InitialWeaponType), EnvironmentGameId = 1000,
                    HostPresentation = new OfficialBattleHostPresentation(camera, world.transform, null),
                    Options = new OfficialBattleSessionOptions { SkipPrepareStatus = false }
                });
                var mapper = new OfficialBattleGridMapper(world.transform, state.Grid);
                camera.transform.position = mapper.GridToWorld(new FightGridPoint(3, 2)) + new Vector3(0, 0, -20);
                Button start = null;
                float deadline = Time.realtimeSinceStartup + 180f;
                while (!start && Time.realtimeSinceStartup < deadline)
                {
                    start = canvas.transform.Find(OfficialBattleHudView.StartButtonPath.TrimStart('/'))?.GetComponent<Button>();
                    yield return null;
                }
                Require(start && start.IsInteractable() && controller.Phase == FightBattlePhase.Preparing,
                    "Stone skin battle start button is unavailable.");
                capture("stone-skin-public-preparing");
                yield return null;
                bool capped = false, expired = false, playable = false;
                controller.StepCompleted += step => {
                    var holder = step.Checkpoint.Units.Single(value => value.Id == 1);
                    var buff = holder.Statuses.SingleOrDefault(value => value.ClassId == 24229);
                    if (!capped && step.Event.ActorId == enemy.Id)
                    {
                        Require(step.Event.DamageEvents.Any(value => value.SkillEntityId == 10600 && value.Damage < 0)
                            && buff != null && buff.StackCount == 9 && buff.SourceSkillRank == 7
                            && buff.SourceSkillLevel == 30 && buff.SourceSubRank == 5,
                            "Received original damage did not reach the stone skin cap.");
                        string path = Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "stone-skin-public-checkpoint.json");
                        File.WriteAllText(path, FightBattleCheckpointSerializer.Save(step.Checkpoint));
                        var saved = FightBattleCheckpointSerializer.Load(File.ReadAllText(path)).Units.Single(value => value.Id == 1)
                            .Statuses.Single(value => value.ClassId == 24229);
                        Require(saved.StackCount == buff.StackCount && saved.CurDurationRound == buff.CurDurationRound,
                            "Stone skin checkpoint lost its stack or duration.");
                        capped = true;
                    }
                    if (step.Event.SkillCasts.Any(value => value.ActorId == 1 && value.Slot >= 0))
                    {
                        Require(capped && buff == null, "Stone skin did not expire on the next holder turn.");
                        expired = true;
                    }
                };
                yield return Click(eventObject.GetComponent<EventSystem>(), start);
                Require(controller.Phase == FightBattlePhase.Play, "Simulated pointer did not start stone skin battle.");
                deadline = Time.realtimeSinceStartup + 180f;
                while (Time.realtimeSinceStartup < deadline && !(capped && expired && playable))
                {
                    controller.Tick(Time.deltaTime);
                    if (!playable && world.GetComponentsInChildren<Transform>(true)
                        .Any(value => value.name.StartsWith("playable24229", StringComparison.Ordinal)))
                    {
                        playable = true;
                        capture("stone-skin-public-playable");
                    }
                    yield return null;
                }
                Require(capped && expired && playable, "Stone skin hit, expiration or original playable was not observed.");
                Require(JsonUtility.ToJson(profile) == beforeProfile, "Battle verification changed the character profile.");
                capture("stone-skin-public-expired");
                yield return null;
                Debug.Log("PUBLIC_STONE_SKIN_PASSED original HUD pointer, received hit, nine stacks, native playable, expiration and checkpoint");
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
