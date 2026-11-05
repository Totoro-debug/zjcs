using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    // A configured initial-status fixture inside the actual shared host. It does
    // not claim that a player's normal equipment grants this isolated status.
    public static class OfficialBattleStackCountHostAcceptance
    {
        private const string Flag = "ZJCS_BATTLE_STACK_COUNT_HOST";
        public static void Run()
        {
            Environment.SetEnvironmentVariable(Flag, "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_HOSTS_ONLY", "weekly");
            OfficialBattlePetHostAcceptance.Run();
        }

        public static void Observe(OfficialBattleRuntimeController controller)
        {
            if (Environment.GetEnvironmentVariable(Flag) != "1") return;
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var owner = controller.Checkpoint.Units.Single(unit => unit.Id == 1);
            owner.Hp = owner.Turn.Hp = Math.Max(1, owner.Hp / 2);
            var status = catalog.CreateStatus(24145, 1, 1, 9924145, 13100, 13100,
                4, owner.Level, owner.SubRank, owner.Properties);
            FightStatusLifecycle.Apply(owner, status, owner.SkillState.Round, status.InstanceId);
            bool checkedStartup = false;
            int ordinary = 0, steps = 0;
            controller.StepCompleted += result =>
            {
                steps++;
                ordinary += result.Event.SkillCasts.Count(c => c.ActorId == 1 && c.Slot >= 0);
                if (checkedStartup) return;
                var ids = result.Event.SkillCasts.Select(c => c.SkillEntityId).ToArray();
                Require(ids.SequenceEqual(new[] {13110,13115}), "Actual host lost original threshold skill order.");
                Require(owner.Statuses.All(s => s.ClassId != 24145)
                    && owner.SkillState.Slots.All(s => s.ReleasedTimes == 0),
                    "Actual startup retained its exhausted status or consumed ordinary actions.");
                Require(result.Event.DamageEvents.Count > 0, "Actual threshold bypassed settlement.");
                string json = FightBattleCheckpointSerializer.Save(result.Checkpoint);
                var restored = FightBattleCheckpointSerializer.Load(json);
                Require(restored.Units.Single(u => u.Id == 1).Statuses.All(s => s.ClassId != 24145),
                    "Actual threshold checkpoint restored a removed source.");
                checkedStartup = true;
                ScreenCapture.CaptureScreenshot(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "stack-threshold-weekly.png"));
                Debug.Log("BATTLE_STACK_COUNT_STARTUP_PASSED original=24145 skills=13110,13115");
            };
            controller.BattleCompleted += _ =>
            {
                Require(checkedStartup && ordinary > 0 && steps > 1, "Threshold fixture did not complete the actual battle.");
                Debug.Log("BATTLE_STACK_COUNT_HOST_PASSED steps=" + steps + " ordinary=" + ordinary);
            };
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
    }
}
