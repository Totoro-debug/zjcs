using System;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static ReplayProbe ConfigureReplayProbe(FightBattleCheckpoint checkpoint)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_REPLAY_ACCEPTANCE") != "1") return null;
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var skill = player.Skills.First(value => value.Slot == 0);
            skill.HasReplay = true; skill.ReplayUsesRate = false; skill.ReplayStatusId = 9900991;
            skill.ReplayStackCount = 10; skill.ReplayRelation = 1;
            skill.ReplayConsumeStack = true; skill.ReplayConsumeCount = 10;
            skill.ReplayMaxCountEachRound = 2; skill.ReplayReplacementEntities.Clear();
            player.Statuses.Add(new FightBattleStatusState { InstanceId = 9900991, ClassId = 9900991,
                OwnerId = 1, CreatorId = 1, StackCount = 20, DurationRound = -1,
                CurDurationRound = -1, StartRound = 0 });
            bool observeConsumption = Environment.GetEnvironmentVariable("ZJCS_BATTLE_STACK_CONSUMPTION_ACCEPTANCE") == "1";
            if (observeConsumption)
            {
                skill.CanTriggerChild = true;
                var catalog = Zjcs.Presentation.Combat.OfficialCombatAssetCatalog.LoadDefault().Load();
                var listener = catalog.CreateStatus(25278, 1, 1, 9900992);
                // The fixture supplies twenty charges; test the original consumption listener
                // independently of its fourteen-charge round-start grant.
                listener.HasRoundStartTrigger = false;
                listener.ConsumeStackCountTargetStatusId = 9900991;
                player.Statuses.Add(listener);
                var actionExist = catalog.CreateStatus(60220, 1, 1, 9900995);
                actionExist.ActionExistConditions.Add(56);
                player.Statuses.Add(actionExist);
                for (int i = 0; i < 2; i++)
                    player.Statuses.Add(new FightBattleStatusState { InstanceId = 9900993 + i,
                        ClassId = 9900993 + i, OwnerId = 1, CreatorId = 1,
                        ActionType = i == 0 ? 60 : 56, StackCount = 1, DurationRound = -1,
                        CurDurationRound = -1, StartRound = 0 });
            }
            return new ReplayProbe(skill.EntityId, observeConsumption);
        }
        private sealed class ReplayProbe
        {
            private readonly int entity;
            private readonly bool observeConsumption;
            private bool observedConsumptionBuff;
            private int actionExistPhase;
            private int replayed;
            public ReplayProbe(int entity, bool observeConsumption)
            { this.entity = entity; this.observeConsumption = observeConsumption; }
            public void Observe(FightBattleCheckpoint checkpoint)
            {
                var player = checkpoint.Units.Single(value => value.Id == 1);
                Require(player.SkillState.ReplayCounts.All(value => value.Count <= 2), "Replay exceeded configured round limit.");
                replayed = checkpoint.Receipts.SelectMany(value => value.Event.SkillCasts)
                    .Count(value => value.ActorId == 1 && value.SkillEntityId == entity && value.IsReplay);
                if (observeConsumption)
                {
                    var listener = player.Statuses.Single(value => value.InstanceId == 9900992);
                    Require(listener.ConsumeStackCountProgress == 0,
                        "Replay costs did not preserve the cumulative consumption remainder.");
                    observedConsumptionBuff |= player.Statuses.Any(value => value.ClassId == 24187
                        && value.StackCount == 7);
                    if (actionExistPhase == 0)
                    {
                        Require(player.Statuses.Any(value => value.ClassId == 60221
                            && value.ActionExistSourceInstanceId == 9900995),
                            "The original action-existence passive did not create its status.");
                        // End one of two fixture actions; the other remains present. The next
                        // executor step must remove the generated effect through normal rebuilds.
                        FightStatusLifecycle.End(player, player.Statuses.Single(value => value.InstanceId == 9900994));
                        actionExistPhase = 1;
                    }
                    else
                    {
                        Require(!player.Statuses.Any(value => value.ClassId == 60221
                            && value.ActionExistSourceInstanceId == 9900995),
                            "Action-existence retained its effect with only one required action.");
                        actionExistPhase = 2;
                    }
                }
            }
            public void Verify(string name, FightBattleCheckpoint checkpoint)
            {
                Observe(checkpoint);
                Require(replayed == 2, name + " did not consume twenty stacks through exactly two consecutive replays: " + replayed);
                Require(!checkpoint.Units.Single(value => value.Id == 1).Statuses.Any(value => value.ClassId == 9900991), name + " retained consumed replay stacks.");
                Require(!observeConsumption || observedConsumptionBuff,
                    name + " did not execute the original cumulative-consumption passive during replay.");
                Require(!observeConsumption || actionExistPhase == 2,
                    name + " did not verify the action-existence transition across real executor steps.");
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                    name + " did not retain replay counts and results in the checkpoint.");
                Debug.Log("[Replay hosts] " + name + ": two configured consecutive replays, stack consumption, caps and checkpoint passed.");
            }
        }
    }
}
