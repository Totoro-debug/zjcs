using System;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Editor
{
    public static partial class HomeBattleHostPlayModeAcceptance
    {
        // Only the opt-in, isolated editor fixture is changed. Shipping profiles/catalogs are untouched.
        private static TriggerChainProbe ConfigureTriggerChainProbe(FightBattleCheckpoint checkpoint)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_TRIGGER_CHAINS_ACCEPTANCE") != "1")
                return null;
            FightBattleUnitState player = checkpoint.Units.Single(value => value.Id == 1);
            FightBattleSkillDefinition skill = player.Skills.First(value => value.Slot == 0);
            foreach (FightBattleHitDefinition hit in skill.Hits)
            { hit.ChildSkillId = 12090; hit.ChildSkillRate = 1; hit.ChildSkillLessThanHpPer = -1; }
            foreach (FightBattleHitEntity hit in skill.HitGraphEntities)
            { hit.ChildSkillId = 12090; hit.ChildSkillRate = 1; hit.ChildSkillLessThanHpPer = -1; }
            var start = new FightBattleStatusState
            {
                InstanceId = 9900001, ClassId = 9900001, OwnerId = player.Id, CreatorId = player.Id,
                DurationRound = -1, CurDurationRound = -1, StartRound = 0,
                HasSkillStartTrigger = true, SkillStartRulesSupported = true, SkillStartSource = 2,
                SkillStartMaxCount = 1, SourceSkillRank = 6, SourceSkillLevel = 17, SourceSubRank = 10
            };
            start.SkillStartActionTypes.Add(skill.ActionType);
            start.SkillStartSkills.Add(new FightBattleStatusStateTrigger
            { EntityId = 12110, Source = 3, Target = 2, TargetPos = 1 });
            var hp = new FightBattleStatusState
            {
                InstanceId = 9900002, ClassId = 9900002, OwnerId = player.Id, CreatorId = player.Id,
                DurationRound = -1, CurDurationRound = -1, StartRound = 0,
                HpTriggerPer = 1, HpTriggerMaxCount = 1,
                SourceSkillRank = 6, SourceSkillLevel = 17, SourceSubRank = 10
            };
            hp.HpTriggerSkills.Add(12110);
            player.Statuses.Add(start);
            player.Statuses.Add(hp);
            return new TriggerChainProbe(start, hp);
        }

        private sealed class TriggerChainProbe
        {
            private readonly FightBattleStatusState start, hp;
            private bool observedCultivation;
            public TriggerChainProbe(FightBattleStatusState start, FightBattleStatusState hp)
            { this.start = start; this.hp = hp; }
            public void Observe(FightBattleCheckpoint checkpoint)
            {
                observedCultivation |= checkpoint.Units.Single(value => value.Id == 1).Statuses
                    .Any(value => value.ClassId == 24201 && value.SourceSkillRank == 6
                        && value.SourceSkillLevel == 17 && value.SourceSubRank == 10);
            }
            public void Verify(string name, FightBattleCheckpoint checkpoint)
            {
                FightBattleSkillCastEvent[] casts = checkpoint.Receipts.SelectMany(value => value.Event.SkillCasts).ToArray();
                int statusSkills = casts.Count(value => value.ActorId == 1 && value.SkillEntityId == 12110);
                Require(start.SkillStartTriggeredCount == 1 && hp.HpTriggeredCount == 1 && statusSkills >= 2,
                    name + " did not execute the nested skill-start -> HP -> original self-buff chain.");
                int children = casts.Count(value => value.ActorId == 1 && value.SkillEntityId == 12090);
                int extras = casts.Count(value => value.ActorId == 1
                    && (value.SkillEntityId == 12093 || value.SkillEntityId == 12094));
                Require(children > 0 && extras > 0, name + " truncated the original child's action-end extra skills.");
                Require(observedCultivation, name + " lost the nested status skill's cultivation.");
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                Require(FightBattleCheckpointSerializer.Save(FightBattleCheckpointSerializer.Load(saved)) == saved,
                    name + " did not preserve the recursive result in its checkpoint.");
                Debug.Log("[Trigger chains] " + name + " status skills=" + statusSkills
                    + " children=" + children + " extras=" + extras + "; counters/cultivation/checkpoint passed.");
            }
        }
    }
}
