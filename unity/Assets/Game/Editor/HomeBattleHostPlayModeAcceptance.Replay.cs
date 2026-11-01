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
            return new ReplayProbe(skill.EntityId);
        }
        private sealed class ReplayProbe
        {
            private readonly int entity;
            private int replayed;
            public ReplayProbe(int entity) { this.entity = entity; }
            public void Observe(FightBattleCheckpoint checkpoint)
            {
                var player = checkpoint.Units.Single(value => value.Id == 1);
                Require(player.SkillState.ReplayCounts.All(value => value.Count <= 2), "Replay exceeded configured round limit.");
                replayed = checkpoint.Receipts.SelectMany(value => value.Event.SkillCasts)
                    .Count(value => value.ActorId == 1 && value.SkillEntityId == entity && value.IsReplay);
            }
            public void Verify(string name, FightBattleCheckpoint checkpoint)
            {
                Observe(checkpoint);
                Require(replayed == 2, name + " did not consume twenty stacks through exactly two consecutive replays: " + replayed);
                Require(!checkpoint.Units.Single(value => value.Id == 1).Statuses.Any(value => value.ClassId == 9900991), name + " retained consumed replay stacks.");
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                    name + " did not retain replay counts and results in the checkpoint.");
                Debug.Log("[Replay hosts] " + name + ": two configured consecutive replays, stack consumption, caps and checkpoint passed.");
            }
        }
    }
}
