using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        private static StatusReplayProbe ConfigureStatusReplayProbe(FightBattleCheckpoint checkpoint)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_STATUS_REPLAY_ACCEPTANCE") != "1") return null;
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var skill = player.Skills.First(value => value.Slot == 0);
            skill.HasReplay = false;
            skill.CanTriggerChild = true;
            skill.ReplayMaxCountEachRound = -1;
            skill.ReplayReplacementEntities.Clear();
            for (int i = 0; i < 2; i++)
                player.Statuses.Add(new FightBattleStatusState { InstanceId = 9901101 + i, ClassId = 9901101 + i,
                    OwnerId = 1, CreatorId = 1, StartRound = 0, DurationRound = -1, CurDurationRound = -1,
                    SkillReplay = new FightBattleStatusReplayState { Rate = 1f, MaxCountEachRound = 1 } });
            var probe = new StatusReplayProbe();
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_STATUS_PARAMETERS_ACCEPTANCE") == "1")
            {
                var values = new List<FightStatusReplacementValue> {
                    new FightStatusReplacementValue { PropertyId = 5001, RawValue = 10000, IsFloat = true } };
                var status = OfficialCombatAssetCatalog.LoadDefault().Load().CreateStatus(60170, 1, 1,
                    9901103, replacementProperties: values);
                Require(status.RoundStartRulesSupported && status.RoundStartRate == 1f,
                    "The original equipment status did not resolve its typed round-start parameter.");
                player.Statuses.Add(status);
                probe.ParameterStatusInstanceId = status.InstanceId;
            }
            return probe;
        }

        private sealed class StatusReplayProbe
        {
            public int ParameterStatusInstanceId;

            public void Observe(FightBattleCheckpoint checkpoint)
            {
                foreach (var status in checkpoint.Units.Single(value => value.Id == 1).Statuses
                    .Where(value => value.InstanceId == 9901101 || value.InstanceId == 9901102))
                    Require(status.SkillReplay.CountEachRound <= 1, "A passive replay exceeded its independent round cap.");
            }

            public void Verify(string name, FightBattleCheckpoint checkpoint)
            {
                Observe(checkpoint);
                var replays = checkpoint.Receipts.SelectMany(value => value.Event.SkillCasts)
                    .Where(value => value.ActorId == 1 && value.IsReplay
                        && (value.ReplayStatusInstanceId == 9901101 || value.ReplayStatusInstanceId == 9901102)).ToArray();
                Require(replays.Length >= 2 && replays[0].ReplayStatusInstanceId == 9901101
                    && replays[1].ReplayStatusInstanceId == 9901102,
                    name + " did not execute both independent passive replays in FIFO order.");
                var player = checkpoint.Units.Single(value => value.Id == 1);
                Require(player.SkillState.Slots.First(value => value.Slot == 0).ReleasedTimes
                    >= replays.Length, name + " did not include passive replays in its release count.");
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                    name + " did not preserve passive replay progress and provenance in its checkpoint.");
                Debug.Log("[Status replay hosts] " + name + ": independent FIFO replays=" + replays.Length
                    + ", round caps, release counts and checkpoint passed.");
                if (ParameterStatusInstanceId > 0)
                {
                    int count = checkpoint.Receipts.SelectMany(value => value.Event.SkillCasts)
                        .Count(value => value.ActorId == 1 && value.SkillEntityId == 60171);
                    Require(count > 0, name + " did not execute the original parameterized round-start child skill.");
                    Debug.Log("[Status parameter hosts] " + name + ": original 60170 -> 60171 casts=" + count
                        + ", typed probability and checkpoint passed.");
                }
            }
        }
    }
}
