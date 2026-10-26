using System;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    /// <summary>Exercises the original elemental-status input through the live battle loop.</summary>
    public sealed class OfficialBattleElementConfrontationAcceptance
    {
        private int counter;
        private FightBattleDamageEvent baseline;

        public static OfficialBattleElementConfrontationAcceptance Install(OfficialBattleRuntimeController controller)
        {
            var probe = new OfficialBattleElementConfrontationAcceptance();
            var assets = OfficialCombatAssetCatalog.LoadDefault();
            FightBattleCheckpoint checkpoint = controller.Checkpoint;
            var player = checkpoint.Units.Single(unit => unit.Id == 1);
            var target = checkpoint.Units.Single(unit => unit.MonsterId > 0);
            target.Hp = target.MaxHp = target.Turn.Hp = 20000;
            int maxHp = assets.LoadDamageFormula().PropertyId("MaxHp");
            foreach (var properties in new[] { target.Properties, target.BaseProperties })
                if (properties != null)
                    foreach (var property in properties.Where(value => value.Id == maxHp)) property.Value = 20000;
            foreach (var skill in player.Skills)
            {
                skill.ElementType = 101;
                skill.HasCustomDamage = true;
                skill.CustomDamage = 100;
            }
            var control = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            var runner = assets.RestoreBattle(control);
            for (int i = 0; i < 32 && !control.Finished; i++)
            {
                var step = runner.Step(control.Sequence, control.Counter + 1);
                probe.baseline = step.Event.DamageEvents.FirstOrDefault(hit => hit.SourceId == player.Id
                    && hit.TargetId == target.Id && hit.Damage < 0 && !hit.IsCure && !hit.IsSkillCost);
                if (probe.baseline == null) continue;
                probe.counter = control.Counter;
                break;
            }
            Require(probe.baseline != null && !probe.baseline.IsElementAffect,
                "Element fixture has no unmodified player hit.");
            Require(!target.Statuses.Any(status => status.ClassId >= 25051 && status.ClassId <= 25060),
                "Element fixture already has another resistance state.");
            target.Statuses.Add(assets.Load().CreateStatus(25051, target.Id, target.Id,
                ++checkpoint.StatusInstanceSerial));
            return probe;
        }

        public bool HasExecuted(FightBattleCheckpoint checkpoint) => checkpoint.Counter >= counter;

        public void Verify(FightBattleCheckpoint checkpoint)
        {
            var hit = checkpoint.Receipts.Single(receipt => receipt.Counter == counter).Event.DamageEvents
                .Single(value => value.SourceId == baseline.SourceId && value.TargetId == baseline.TargetId
                    && value.SkillEntityId == baseline.SkillEntityId && value.HitEntityId == baseline.HitEntityId);
            Require(hit.IsElementAffect && hit.ElementConfrontation == 0.2f
                && !hit.ElementConfrontationUnresolved && hit.Damage < 0 && hit.Damage > baseline.Damage
                && hit.BeforeHp - hit.AfterHp == -hit.Damage,
                "The live battle did not bind the original elemental status to actual HP settlement.");
            Debug.Log("Element-confrontation Play Mode passed: original state 25051, incoming element 101, value="
                + hit.ElementConfrontation + ", baseline HP loss=" + -baseline.Damage + ", actual HP loss="
                + -hit.Damage + ", counter=" + counter + ".");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
