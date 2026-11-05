using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattlePetPassiveHostAcceptance
    {
        private const string Flag = "ZJCS_BATTLE_PET_PASSIVE_HOST";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const string Pet = "/FightMainPanel/Content/Bottom/Anim/SkillListContent/BgSlot/GamePetSkillIconItem";
        public static bool Enabled => Environment.GetEnvironmentVariable(Flag) == "1";

        public static void Run()
        {
            Environment.SetEnvironmentVariable(Flag, "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_HOSTS_ONLY", "weekly");
            OfficialBattlePetHostAcceptance.Run();
        }

        public static void Configure(OfflinePlayerProgress player)
        {
            var pet = OfflinePetProgress.AddPet(player.Pets, 1614, 4);
            pet.EvolutionPhase = 1;
            pet.Level = 1;
            pet.Status = 2;
            player.Skills.Plans[player.Skills.SelectedPlan].PetIndex = pet.Index;
            Require(OfflinePetProgress.IsValid(player.Pets, player.Skills), "1614 host fixture violates save invariants.");
        }

        public static void Observe(OfficialBattleRuntimeController controller)
        {
            var scene = (OfficialBattleSceneView)typeof(OfficialBattleRuntimeController).GetField("sceneView", Private).GetValue(controller);
            var hud = (OfficialBattleHudView)typeof(OfficialBattleRuntimeController).GetField("hudView", Private).GetValue(controller);
            var initial = controller.Checkpoint.Units.Single(unit => unit.Id == 1);
            Require(initial.PetEnergy.PetClassId == 1614 && !initial.PetEnergy.Enabled
                && initial.PetEnergy.SupportSkills.Single().EntityId == 50165,
                "Actual host failed to assemble the childhood passive skill.");
            int steps = 0, casts = 0, ordinary = 0, nativeCasts = 0, fullStacks = 0;
            bool captured = false;
            controller.StepCompleted += result =>
            {
                steps++;
                var owner = result.Checkpoint.Units.Single(unit => unit.Id == 1);
                var follower = scene.TryGetPetTransform(1);
                var model = follower ? follower.GetComponentInChildren<BattleNativeMonsterRenderer>(true) : null;
                Require(model && model.Animator.runtimeAnimatorController.name.Contains("9215"),
                    "1614 did not retain its original childhood model/controller.");
                Require(hud.Find(Pet).activeInHierarchy
                    && hud.Find(Pet + "/Content/Bg/Anim/Mask/PetIcon").GetComponent<Image>().sprite
                    && hud.Find(Pet + "/SupportSkillRoot").activeSelf,
                    "1614 original pet HUD is unbound.");
                foreach (var cast in result.Event.SkillCasts.Where(value => value.ActorId == 1))
                {
                    if (!cast.IsPetSupport) { if (cast.Slot >= 0) ordinary++; continue; }
                    casts++;
                    Require(cast.SkillEntityId == 50165 && cast.SkillItemId == 16014
                        && cast.Slot < 0 && cast.TargetIds.SequenceEqual(new[] {1}),
                        "Actual passive support changed the original skill/master target.");
                    if (cast.Delay <= 0 && cast.Presentation.Duration > 0)
                    {
                        var entity = follower.GetComponent<MyEntity>();
                        var role = follower.GetComponent<MyFightRolePet>();
                        int active = (int)typeof(MyFightRolePet).GetField("mCurrentSkillCounter", Private).GetValue(role);
                        Require(entity && entity.Entity.ClassId == 9215 && active > 0,
                            "Actual passive support graph did not use the original pet source/lifecycle.");
                        nativeCasts++;
                    }
                }
                Require(owner.PetEnergy.SupportSkillState.Slots.Single().ReleasedTimes == casts && casts <= 4,
                    "Actual passive support exceeded its original four-release limit.");
                var stack = owner.Statuses.SingleOrDefault(status => status.ClassId == 24138);
                if (stack != null && stack.StackCount == 4) fullStacks++;
                if (!captured && casts >= 4)
                {
                    captured = true;
                    ScreenCapture.CaptureScreenshot(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "pet1614-four-stacks.png"));
                }
            };
            controller.BattleCompleted += checkpoint =>
            {
                Require(steps > 4 && casts == 4 && ordinary > 4 && nativeCasts > 0 && fullStacks > 0,
                    "Actual host did not exercise limited passive support and continuing owner actions.");
                string json = FightBattleCheckpointSerializer.Save(checkpoint);
                var copy = FightBattleCheckpointSerializer.Load(json);
                var expected = checkpoint.Units.Single(unit => unit.Id == 1).PetEnergy;
                var actual = copy.Units.Single(unit => unit.Id == 1).PetEnergy;
                Require(actual.SupportSkillState.Slots.Single().ReleasedTimes == 4
                    && actual.SupportSkillState.Slots.Single().LastRound == expected.SupportSkillState.Slots.Single().LastRound,
                    "Actual passive support checkpoint lost independent CD/count state.");
                Debug.Log("BATTLE_PET_1614_HOST_PASSED steps=" + steps + " casts=" + casts
                    + " ordinary=" + ordinary + " nativeCasts=" + nativeCasts + " fullStackSamples=" + fullStacks);
            };
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
    }
}
