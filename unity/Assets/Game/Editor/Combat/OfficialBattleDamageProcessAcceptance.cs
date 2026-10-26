using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    /// <summary>Opt-in numeric fixture; preserves the real skill graph, input and presentation loop.</summary>
    public sealed class OfficialBattleDamageProcessAcceptance
    {
        private int counter;
        private FightBattleDamageEvent baseline;
        private FightBattleStatusState mark;
        private long expectedLoss;

        public static OfficialBattleDamageProcessAcceptance Install(OfficialBattleRuntimeController controller)
        {
            var probe = new OfficialBattleDamageProcessAcceptance();
            var assets = OfficialCombatAssetCatalog.LoadDefault();
            FightBattleCheckpoint checkpoint = controller.Checkpoint;
            var player = checkpoint.Units.Single(value => value.Id == 1);
            var owner = checkpoint.Units.Single(value => value.MonsterId > 0);
            owner.Hp = owner.MaxHp = owner.Turn.Hp = 20000;
            int maxHpId = assets.LoadDamageFormula().PropertyId("MaxHp");
            foreach (var properties in new[] { owner.Properties, owner.BaseProperties })
                if (properties != null)
                    foreach (var property in properties.Where(value => value.Id == maxHpId)) property.Value = 20000;
            // Keep the first unmodified hit below lethal so the actual screen flow proves the bonus.
            foreach (var skill in player.Skills)
            {
                skill.HasCustomDamage = true;
                skill.CustomDamage = 100;
            }
            var deadCreator = new FightBattleUnitState
            {
                Id = 3, Side = player.Side, CharacterType = 2, MonsterId = owner.MonsterId,
                Hp = 0, MaxHp = 1000, Ended = true, Targetable = false, Level = 1, SubRank = 1
            };
            deadCreator.Turn.Id = deadCreator.Id;
            deadCreator.Turn.Hp = 0;
            checkpoint.Units.Add(deadCreator);
            var pending = assets.Load().CreateStatus(24062, owner.Id, deadCreator.Id,
                ++checkpoint.StatusInstanceSerial, 0, 0, 1, 1, 1);
            pending.AccumulatedDamage.HpDamage = 100;
            pending.AccumulatedDamage.Contributions.Add(new FightBattleDamageContribution
                { SourceId = 3, SkillEntityId = 10012, HpDamage = 100 });
            owner.EndedAccumulatedDamageStatuses.Add(pending);
            var clone = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            var runner = assets.RestoreBattle(clone);
            for (int i = 0; i < 32 && !clone.Finished; i++)
            {
                var step = runner.Step(clone.Sequence, clone.Counter + 1);
                probe.baseline = step.Event.DamageEvents.FirstOrDefault(value =>
                    value.SourceId == player.Id && value.TargetId == owner.Id && value.Damage < 0
                    && !value.IsCure && !value.IsSkillCost);
                if (probe.baseline == null) continue;
                probe.counter = clone.Counter;
                break;
            }
            Require(probe.baseline != null && -probe.baseline.Damage < probe.baseline.BeforeHp,
                "The damage-process baseline needs a nonlethal player hit: "
                + (probe.baseline == null ? "no player hit" : probe.baseline.Damage + "/" + probe.baseline.BeforeHp));
            probe.mark = assets.Load().CreateStatus(24062, owner.Id, player.Id,
                ++checkpoint.StatusInstanceSerial, 0, 0, 1, 1, 1);
            // Only this test prolongs the status so an earlier monster turn cannot expire the fixture.
            probe.mark.DurationRound = -1;
            owner.Statuses.Add(probe.mark);
            FightStatusPropertyRuntime.RebuildAll(checkpoint.Units, assets.Load(), assets.LoadDamageFormula());
            Require(probe.mark.ResolvedProperties.Single(value => value.Id == 231).Value == 2000
                && probe.mark.ResolvedProperties.Single(value => value.Id == 235).Value == 4789,
                "The Play Mode fixture did not resolve the original mark properties.");
            probe.expectedLoss = Math.Min(probe.baseline.BeforeHp, (long)(-probe.baseline.Damage * 1.2 + 4789));
            Require(probe.expectedLoss > -probe.baseline.Damage, "The fixture cannot expose a visible damage increase.");
            // Restore the complete fixture as a saved battle, retaining the dead creator in the
            // executor's entity dictionary without creating a living turn or presentation model.
            typeof(OfficialBattleRuntimeController).GetField("executor", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, assets.RestoreBattle(checkpoint));
            return probe;
        }

        public bool HasExecuted(FightBattleCheckpoint checkpoint) => checkpoint.Counter >= counter;

        public void Verify(FightBattleCheckpoint checkpoint)
        {
            var hit = checkpoint.Receipts.Single(value => value.Counter == counter).Event.DamageEvents
                .FirstOrDefault(value => value.SourceId == baseline.SourceId && value.TargetId == baseline.TargetId
                    && value.SkillEntityId == baseline.SkillEntityId && value.HitEntityId == baseline.HitEntityId);
            Require(hit != null && hit.Damage == -expectedLoss && hit.BeforeHp - hit.AfterHp == expectedLoss,
                "The actual battle loop did not apply percent then fixed damage before HP settlement.");
            Require(mark.AccumulatedDamage.HpDamage >= expectedLoss,
                "The actual battle loop did not accumulate the boosted HP damage.");
            var deadHit = checkpoint.Receipts.SelectMany(value => value.Event.DamageEvents)
                .FirstOrDefault(value => value.SourceId == 3 && value.SkillEntityId == 13148);
            Require(deadHit != null && deadHit.Damage == -30 && checkpoint.Units.Single(value => value.Id == 3).Hp == 0
                && checkpoint.Statistics.Skill(3, 10012, 0).AdditionalAttributedDamage == 30
                && checkpoint.Statistics.Skill(3, 13148, 0).Damage == 30,
                "The actual presentation loop lost dead-creator damage or attribution, or revived the source.");
            Debug.Log("Damage-process Play Mode passed: original 20% + 4789, baseline HP damage="
                + -baseline.Damage + ", actual HP damage=" + expectedLoss + ", counter=" + counter
                + "; dead creator retained independent 30 HP hit and 30 source credit without revival.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
