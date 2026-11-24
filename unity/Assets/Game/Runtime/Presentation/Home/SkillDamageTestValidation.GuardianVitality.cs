using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        private sealed partial class AcquiredGuardianProbe
        {
            public int VitalityCureCount { get; private set; }
            private bool vitalityHealed;

            private void ObserveVitalitySpring(FightBattleStepEvent step, FightBattleCheckpoint checkpoint)
            {
                var actor = checkpoint.Units.Single(unit => unit.Id == 1);
                var listener = actor.Statuses.Single(status => status.ClassId == 25040
                    && status.ApplyingSkillItemId == item && status.SourceSkillLevel == 2);
                int remaining = FightStatusLifecycle.RoundStartRemainingCount(listener);
                Require(listener.RoundStartRulesSupported && listener.RoundStartSkills.Count == 1
                    && listener.RoundStartSkills[0].EntityId == 11560 && remaining == -1,
                    "The actual vitality spring lost its original unlimited round-start self-cure.");
                var invokes = step.StatusViewEvents.Where(view => view.OwnerId == actor.Id
                    && view.Status.ClassId == listener.ClassId && view.Status.CreatorId == actor.Id
                    && view.Kind == FightBattleStatusViewEventKind.Invoke).ToArray();
                var cures = step.DamageEvents.Where(hit => hit.SkillEntityId == 11560).ToArray();
                if (invokes.Length == 0 && cures.Length == 0) return;
                Require(step.ActorId == actor.Id && invokes.Length == 1 && cures.Length == 1
                    && listener.RoundStartTriggeredCount > 0 && invokes[0].Status.InstanceId == listener.InstanceId
                    && invokes[0].Status.HasRoundStart && invokes[0].Status.StackCount == listener.StackCount
                    && invokes[0].Status.RoundStartRemainingCount == remaining,
                    "The original unlimited vitality spring Invoke or cultivated source changed.");
                var formula = assets.LoadDamageFormula();
                var source = actor.Properties.ToDictionary(value => value.Id, value => value.Value);
                var target = actor.Properties.ToDictionary(value => value.Id, value => value.Value);
                foreach (var pair in assets.Load().CalculateSkillProps(11560, listener.SourceSkillRank, 2, actor.SubRank))
                    source[pair.Key] = (source.TryGetValue(pair.Key, out long previous) ? previous : 0L) + pair.Value;
                long expected = formula.CalculateCure(source, target,
                    formula.PropertyId("SkillCureByHp"), formula.PropertyId("SkillFixedCure")).Cure;
                var cure = cures[0];
                Require(cure.SourceId == actor.Id && cure.TargetId == actor.Id && cure.SkillItemId == item
                    && cure.HitEntityId == 11561 && cure.DamageEntityId == 11562 && cure.IsCure
                    && cure.HasDamageValue && cure.DamageValue > 0 && cure.Damage == expected
                    && cure.AfterHp - cure.BeforeHp == Math.Min(expected, actor.MaxHp - cure.BeforeHp)
                    && cure.AfterHp >= cure.BeforeHp && cure.AfterHp <= actor.MaxHp,
                    "The cultivated vitality spring changed its original formula, cure source or capped HP settlement.");
                vitalityHealed |= cure.AfterHp > cure.BeforeHp;
                string saved = FightBattleCheckpointSerializer.Save(checkpoint);
                var restored = FightBattleCheckpointSerializer.Load(saved);
                var replay = assets.RestoreBattle(restored).Step(restored.Sequence, restored.Counter);
                Require(replay.Replayed && FightBattleCheckpointSerializer.Save(restored) == saved,
                    "Restoring vitality spring changed its HP, unlimited trigger state or settled receipt.");
                File.WriteAllText(Path.Combine(output, "companion-vitality-spring-checkpoint.json"), saved);
                File.WriteAllText(Path.Combine(output, "companion-vitality-spring-status-events.json"),
                    JsonUtility.ToJson(new GuardianVitalityReceipt { invokes = invokes.Length, cures = cures.Length,
                        expected = expected, actual = cure.Damage, remaining = remaining }, true));
                VitalityCureCount += cures.Length;
                Debug.Log("[Acquired passive combat] 10572 cultivated vitality spring round-start Invoke, cure, formula and restore replay passed.");
            }
        }

        [Serializable]
        private sealed class GuardianVitalityReceipt
        {
            public int invokes, cures, remaining;
            public long expected, actual;
        }
    }
}
