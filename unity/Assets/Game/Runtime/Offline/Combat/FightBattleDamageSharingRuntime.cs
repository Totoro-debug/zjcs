using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    // Confirmed offline scheduling for DamageReduceBeforeFinal. Numeric properties,
    // trigger roles and the child skill remain supplied by the original status.
    public static class FightBattleDamageSharingRuntime
    {
        public static void Collect(FightBattleUnitState attacker, FightBattleUnitState receiver,
            FightBattleSkillDefinition skill, FightBattleTransientDamageContext damage,
            IReadOnlyList<FightBattleUnitState> units, FightDamageFormula formula,
            List<FightBattleStatusSkillInvocation> pending)
        {
            if (receiver == null || damage == null || damage.IsCure || damage.Damage >= 0
                || skill?.DamageSharingTransfer == true || receiver.Statuses == null) return;
            foreach (var status in new List<FightBattleStatusState>(receiver.Statuses))
            {
                var rule = status?.Components?.DamageReduceBeforeFinal;
                if (rule == null || status.ForbiddenType != 0 || rule.CustomDamageSkillId <= 0
                    || rule.IsCheckSkillTrigger && !FightSkillTriggerPolicy.AllowsStatus(skill, status)) continue;
                FightBattleUnitState source = TriggerUnit(rule.CustomDamageSource, attacker, receiver, status, units);
                FightBattleUnitState guardian = TriggerUnit(rule.CustomDamageTarget, attacker, receiver, status, units);
                if (source == null || !FightBattleTargeting.IsSelectable(guardian) || guardian == receiver) continue;
                if (rule.CustomDamageTargetPosition != 1)
                    throw new NotSupportedException("Damage sharing requires a verified trigger-target position.");

                var properties = Properties(status.ResolvedProperties ?? status.StatusProperties ?? status.StatusConfigProperties);
                decimal fraction = (decimal)Math.Max(0.0, Math.Min(1.0,
                    formula.ReadProperty(properties, rule.ReduceDamagePropertyId)));
                long diverted = (long)(-(decimal)damage.Damage * fraction);
                if (diverted == 0) continue;
                long transfer = -formula.CalculateStatusDamage(properties, rule.CustomDamagePropertyId,
                    formula.PropertyId("FixedDamageByDamage"), diverted);
                damage.Damage += diverted;
                damage.AbsoluteDamage = ReduceSigned(damage.AbsoluteDamage, fraction);
                var invocation = new FightBattleStatusSkillInvocation
                {
                    Trigger = new FightBattleStatusStateTrigger { EntityId = rule.CustomDamageSkillId }.WithSource(status),
                    SourceUnitId = source.Id, TargetUnitId = guardian.Id, TargetPositionUnitId = guardian.Id,
                    HasCustomDamage = true, DamageSharingBaseDamage = transfer,
                    DamageSharingStatus = status, DamageSharingTransfer = true,
                    // The original attacker may die during the receiver's settlement.
                    AllowDeadSource = true
                };
                invocation.CustomDamage = ResolveTransferredDamage(invocation, units, formula);
                pending.Add(invocation);
            }
        }

        public static long ResolveTransferredDamage(FightBattleStatusSkillInvocation pending,
            IReadOnlyList<FightBattleUnitState> units, FightDamageFormula formula)
        {
            var status = pending.DamageSharingStatus;
            return (long)(pending.DamageSharingBaseDamage
                * DefenceScale(status, status.Components.DamageReduceBeforeFinal, units, formula));
        }

        private static long ReduceSigned(long value, decimal fraction)
        {
            return value < 0 ? value + (long)(-(decimal)value * fraction)
                : value - (long)((decimal)value * fraction);
        }

        private static decimal DefenceScale(FightBattleStatusState status,
            FightBattleStatusDamageReduceBeforeFinalState rule,
            IReadOnlyList<FightBattleUnitState> units, FightDamageFormula formula)
        {
            if (rule.UnitPropertyId <= 0 || rule.UnitPercent <= 0) return 1m;
            var source = PropertyUnit(status, rule.UnitPropertySource, units);
            var target = PropertyUnit(status, rule.UnitPropertyTarget, units);
            if (source == null || target == null) return 1m;
            decimal sourceValue = (decimal)formula.ReadProperty(Properties(source.Properties), rule.UnitPropertyId);
            decimal targetValue = (decimal)formula.ReadProperty(Properties(target.Properties), rule.UnitPropertyId);
            if (sourceValue <= targetValue) return 1m;
            if (targetValue <= 0) return rule.ResultPercent < 0 ? 0m : 1m;
            decimal steps = decimal.Floor((sourceValue - targetValue) * 10000m
                / targetValue / rule.UnitPercent);
            return Math.Max(0m, 1m + steps * rule.ResultPercent / 10000m);
        }

        private static FightBattleUnitState PropertyUnit(FightBattleStatusState status, int kind,
            IReadOnlyList<FightBattleUnitState> units)
        {
            int id = FightStatusLifecycle.ResolveTargetUnitId(status, kind, units);
            // An adventurer without an explicit master is its own property source.
            if (id == 0 && kind == FightStatusLifecycle.TargetCreatorMaster) id = status.CreatorId;
            if (id == 0 && kind == FightStatusLifecycle.TargetApplicatorMaster) id = status.OwnerId;
            return Find(units, id);
        }

        private static FightBattleUnitState TriggerUnit(int kind, FightBattleUnitState attacker,
            FightBattleUnitState receiver, FightBattleStatusState status,
            IReadOnlyList<FightBattleUnitState> units)
        {
            switch (kind)
            {
                case 1: return attacker;
                case 2: case 5: return receiver;
                case 3: return Find(units, status.CreatorId);
                case 8: return PropertyUnit(status, FightStatusLifecycle.TargetApplicatorMaster, units);
                default: throw new NotSupportedException("Damage sharing trigger role is not verified: " + kind);
            }
        }

        private static FightBattleUnitState Find(IReadOnlyList<FightBattleUnitState> units, int id)
        {
            if (units != null)
                foreach (var unit in units) if (unit != null && unit.Id == id) return unit;
            return null;
        }

        private static Dictionary<int, long> Properties(IList<FightPropertyValue> values)
        {
            var result = new Dictionary<int, long>();
            foreach (var value in values ?? Array.Empty<FightPropertyValue>())
                if (value != null) result[value.Id] = value.Value;
            return result;
        }
    }
}
