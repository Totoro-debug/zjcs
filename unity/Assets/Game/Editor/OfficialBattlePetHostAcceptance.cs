using System;
using System.Linq;
using System.Collections;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityLifecycle;

namespace Zjcs.Editor.Combat
{
    /// <summary>Exercises the shared pet HUD through the existing real host/input scenario.</summary>
    public static class OfficialBattlePetHostAcceptance
    {
        private const string Flag = "ZJCS_BATTLE_PET_HOST";
        private const string Pet = "/FightMainPanel/Content/Bottom/Anim/SkillListContent/BgSlot/GamePetSkillIconItem";

        public static void Run()
        {
            var portraits = Resources.Load<OfficialBattleMonsterPortraitCatalog>("Combat/OfficialBattlePetPortraitCatalog");
            Require(portraits && portraits.Count == 19, "Original pet timeline portrait catalog is incomplete.");
            foreach (int id in new[] {9301,9302,9303,9304,9305,9306,9307,9308,9309,9310,9311,9312,9314,9315,9316,9317,9318,9320,9322})
            {
                var sprite = portraits.Find(id);
                Require(sprite && sprite.name == "G_" + id && sprite.vertices.Length > 0 && sprite.triangles.Length > 0,
                    "Original pet timeline Sprite mesh is unbound: " + id);
            }
            Environment.SetEnvironmentVariable(Flag, "1");
            Environment.SetEnvironmentVariable("ZJCS_SKILLS_AUTORUN", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_SIMULATED", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_HOSTS_ACCEPTANCE", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_HOSTS_DIRECT", "1");
            HomeBattleHostPlayModeAcceptance.Run();
        }

        public static void Configure(OfflinePlayerProgress player)
        {
            if (Environment.GetEnvironmentVariable(Flag) != "1") return;
            OfficialBattlePetFollowerAcceptance.Verify();
            var pet = OfflinePetProgress.AddPet(player.Pets, 1600, 4);
            pet.EvolutionPhase = 2;
            pet.Level = 1;
            player.Skills.Plans[player.Skills.SelectedPlan].PetIndex = pet.Index;
            pet.Status = 2; // Existing save invariant: present in a battle plan, outside formation/follow.
            Require(OfflinePetProgress.IsValid(player.Pets, player.Skills), "Pet host fixture violates save invariants.");
        }

        public static void Observe(OfficialBattleRuntimeController controller)
        {
            if (Environment.GetEnvironmentVariable(Flag) != "1") return;
            var field = typeof(OfficialBattleRuntimeController).GetField("hudView", BindingFlags.NonPublic | BindingFlags.Instance);
            var view = (OfficialBattleHudView)field.GetValue(controller);
            Check(controller.Checkpoint, view);
            int samples = 0, modelSamples = 0, supportCasts = 0, ordinaryCasts = 0, followerSamples = 0, nativePetCasts = 0, releasedSamples = 0;
            bool captured = false;
            var scene = (OfficialBattleSceneView)typeof(OfficialBattleRuntimeController)
                .GetField("sceneView", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);
            controller.StepCompleted += result =>
            {
                Check(result.Checkpoint, view); samples++;
                var follower = scene.TryGetPetTransform(1);
                var followerRenderer = follower ? follower.GetComponentInChildren<BattleNativeMonsterRenderer>(true) : null;
                Require(followerRenderer && follower.GetComponent<GridTransformFollower>() && follower.GetComponent<MyFightRolePet>(),
                    "Actual host did not create the original childhood follower/model/lifecycle.");
                Require(followerRenderer.Animator.runtimeAnimatorController.name.Contains("9201"),
                    "Actual childhood follower selected another original pet controller.");
                followerSamples++;
                var role = follower.GetComponent<MyFightRolePet>();
                int currentSkills = (int)typeof(MyFightRolePet).GetField("mCurrentSkillCounter", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(role);
                Require(currentSkills >= 0, "Original pet skill lifecycle counter became negative.");
                if (supportCasts > 0 && currentSkills == 0)
                {
                    string state = typeof(GridTransformFollower).GetField("mMoveState", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(follower.GetComponent<GridTransformFollower>()).ToString();
                    Require(state != "Pause", "Native pet skill ended without releasing its follower pause.");
                    releasedSamples++;
                }
                foreach (var cast in result.Event.SkillCasts.Where(value => value.ActorId == 1))
                {
                    if (cast.IsPetSupport)
                    {
                        Require(cast.SkillItemId == 16300 && cast.Slot < 0,
                            "Actual pet support did not use the equipped evolution skill.");
                        supportCasts++;
                        if (cast.Delay <= 0 && cast.Presentation.Duration > 0)
                        {
                            var entity = follower.GetComponent<MyEntity>();
                            int active = (int)typeof(MyFightRolePet).GetField("mCurrentSkillCounter", BindingFlags.Instance | BindingFlags.NonPublic)
                                .GetValue(follower.GetComponent<MyFightRolePet>());
                            Require(entity && entity.Entity.ClassId == 9201 && active > 0
                                && entity.myContainer is IEntityContainerLocalPosition,
                                "Actual support graph did not bind the pet source and pause its original follower lifecycle.");
                            nativePetCasts++;
                        }
                    }
                    else if (cast.Slot >= 0) ordinaryCasts++;
                }
                foreach (var pet in result.Checkpoint.Units.Where(unit => unit.MonsterId == 9301
                    && !unit.Ended && unit.Hp > 0))
                {
                    var root = scene.TryGetUnitTransform(pet.Id);
                    var renderer = root ? root.GetComponentInChildren<BattleNativeMonsterRenderer>(true) : null;
                    Require(renderer && renderer.Animator && renderer.Animator.runtimeAnimatorController,
                        "Actual pet entity has no original native skeleton/controller in the common scene.");
                    var card = UnityEngine.Object.FindObjectsOfType<OfficialBattleSceneTimelineView>()
                        .Select(timeline => timeline.transform.Find("Slot/GameRoundCharacterItem_" + pet.Id))
                        .SingleOrDefault(value => value != null);
                    Require(card, "Actual pet has no shared timeline card.");
                    var expected = Resources.Load<OfficialBattleMonsterPortraitCatalog>("Combat/OfficialBattlePetPortraitCatalog").Find(pet.MonsterId);
                    Require(card.Find("IconGo/MonsterIconImg").GetComponent<SpriteRenderer>().sprite == expected
                        && card.Find("IconGo/MonsterIconImg/MonsterHurtImg").GetComponent<SpriteRenderer>().sprite == expected,
                        "Actual pet timeline normal/hurt portraits differ from GetPetIcon(PetEntityClassId).");
                    modelSamples++;
                    if (!captured)
                    {
                        captured = true;
                        controller.StartCoroutine(CaptureEntity(result.Checkpoint.StageId));
                    }
                }
            };
            controller.BattleCompleted += checkpoint =>
            {
                Require(samples > 0, "Host did not advance its actual pet battle.");
                Require(supportCasts > 0 && ordinaryCasts > 0,
                    "Actual host did not run both pet support and ordinary owner actions.");
                Require(followerSamples > 0 && nativePetCasts > 0 && releasedSamples > 0,
                    "Actual host did not exercise native childhood pet support casts.");
                var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
                var expected = checkpoint.Units.Single(unit => unit.Id == 1).PetEnergy;
                var actual = copy.Units.Single(unit => unit.Id == 1).PetEnergy;
                Require(actual.CurrentEnergy == expected.CurrentEnergy && actual.NeedEnergy == expected.NeedEnergy
                    && actual.SupportSkillsApplied == expected.SupportSkillsApplied,
                    "Pet battle checkpoint did not preserve its actual energy/support state.");
                Require(actual.SupportSkills.Count == expected.SupportSkills.Count
                    && actual.SupportSkillState.Slots.Single().ReleasedTimes == supportCasts
                    && actual.SupportSkillState.Slots.Single().LastRound == expected.SupportSkillState.Slots.Single().LastRound,
                    "Actual pet support checkpoint lost CD/count state.");
                if (Environment.GetEnvironmentVariable("ZJCS_PET_REQUIRE_ENTITY") == "1")
                    Require(modelSamples > 0, "Actual host never displayed an entityized pet.");
                Debug.Log("BATTLE_PET_HOST_PASSED stage=" + checkpoint.StageId + " steps=" + samples
                    + " modelSamples=" + modelSamples + " supportCasts=" + supportCasts
                    + " ordinaryCasts=" + ordinaryCasts);
                Debug.Log("BATTLE_PET_FOLLOWER_HOST_PASSED followerSamples=" + followerSamples + " nativePetCasts=" + nativePetCasts + " releasedSamples=" + releasedSamples);
            };
        }

        private static IEnumerator CaptureEntity(int stage)
        {
            yield return new WaitForSecondsRealtime(.2f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"),
                "battle-pet-entity-" + stage + ".png"));
        }

        private static void Check(FightBattleCheckpoint checkpoint, OfficialBattleHudView view)
        {
            var state = checkpoint.Units.Single(unit => unit.Id == 1).PetEnergy;
            Require(state != null && state.Enabled && state.PetClassId == 1600 && state.Initialized,
                "Actual host did not assemble and initialize the equipped pet.");
            var root = view.Find(Pet);
            var portrait = view.Find(Pet + "/Content/Bg/Anim/Mask/PetIcon").GetComponent<Image>();
            Require(root.activeInHierarchy && portrait.isActiveAndEnabled && portrait.sprite,
                "Actual host did not show the equipped pet's original portrait.");
            Require(!view.Find(Pet + "/BgNone").activeSelf,
                "Actual equipped pet retained the empty placeholder.");
            float percent = Math.Abs(state.NeedEnergy) < double.Epsilon ? 0f : (float)(state.CurrentEnergy / state.NeedEnergy);
            Require(Mathf.Abs(view.Find(Pet + "/Content/Bg/Energy").GetComponent<Image>().fillAmount
                - Mathf.Clamp01(percent)) < .00001f
                && view.Find(Pet + "/Content/Bg/EnergyTxt").GetComponent<TMP_Text>().text
                    == WorldBattleReportNumberFormat.Percent(percent),
                "Shared host pet HUD diverged from its actual checkpoint energy.");
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
