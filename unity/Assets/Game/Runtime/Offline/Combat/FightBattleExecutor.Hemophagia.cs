using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightOfficialCombatCatalog
    {
        public int HemophagiaSkillEntityId { get; private set; }
    }

    public sealed partial class FightBattleExecutor
    {
        private void ApplyHemophagia(FightBattleUnitState actor, FightBattleHitDefinition hit,
            FightBattleDamageEvent applied, float delay, FightBattleSkillDefinition sourceSkill,
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> events,
            List<FightBattleMovementEvent> movements, ref float lastDelay,
            ref float duration, ref float actionDuration)
        {
            if (hit.HemophagiaProperty <= 0 || applied == null || applied.IsCure) return;
            // The confirmed offline basis is this occurrence's actual HP removal, frozen
            // before any status response, sharing, revival or subsequent hit changes HP.
            long removedHp = Math.Max(0L, applied.BeforeHp - applied.AfterHp);
            long property = GetProperty(actor, hit.HemophagiaProperty);
            if (removedHp <= 0 || property <= 0) return;
            decimal rate = property / 10000m;
            long cure = removedHp >= long.MaxValue / rate ? long.MaxValue
                : (long)decimal.Truncate(removedHp * rate);
            if (cure <= 0) return;
            if (statusCatalog == null || statusCatalog.HemophagiaSkillEntityId <= 0)
                throw new InvalidOperationException("Original Battle.HemophagiaEntityId is unavailable.");
            var trigger = new FightBattleStatusStateTrigger {
                EntityId = statusCatalog.HemophagiaSkillEntityId,
                SourceSkillItemId = sourceSkill.ItemId,
                SourceSkillRank = sourceSkill.Rank, SourceSkillLevel = sourceSkill.Level,
                SourceSubRank = sourceSkill.SubRank,
                Origin = FightBattleStatusOrigin.Resolve(sourceSkill, actor.Id).Copy(),
                BlessPropertyScales = FightStatusPropertyModifiers.Copy(sourceSkill.BlessPropertyScales)
            };
            var invocation = new FightBattleStatusSkillInvocation {
                Trigger = trigger, SourceUnitId = actor.Id, TargetUnitId = actor.Id,
                TargetPositionUnitId = actor.Id, HasCustomCure = true, CustomCure = cure,
                SourceRootSkillId = sourceSkill.RootEntityId > 0 ? sourceSkill.RootEntityId : sourceSkill.EntityId,
                Delay = delay
            };
            // The original cure skill owns settlement, status filters, HP listeners and
            // its native cast/hit/damage lifecycle, even when actual recovery is zero.
            ExecuteConfiguredStatusSkills(new[] { invocation }, casts, events, movements,
                ref lastDelay, out float nestedDuration, out float nestedActionDuration);
            duration = Math.Max(duration, nestedDuration);
            actionDuration = Math.Max(actionDuration, nestedActionDuration);
        }
    }
}
