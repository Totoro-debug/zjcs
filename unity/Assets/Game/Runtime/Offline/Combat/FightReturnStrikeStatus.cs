namespace Zjcs.Combat.Official
{
    internal static class FightReturnStrikeStatus
    {
        // Offline interpretation of the confirmed stacking description: add each layer's
        // original increments. The unavailable server formula is not claimed as recovered.
        // Keep the original unscaled status dictionaries and all other statuses unchanged.
        internal static long HitProperty(FightBattleStatusState status, FightPropertyValue property, double scale)
        {
            if (status.ClassId == 24268 && (property.Id == 231 || property.Id == 235))
                scale *= System.Math.Max(0, status.StackCount);
            return (long)(property.Value * scale);
        }
    }

    public sealed partial class FightOfficialCombatCatalog
    {
        // Only this recovered received-damage configuration has a confirmed offline gate.
        // Unimplemented range, movement, filters or extra effects must not run as a subset.
        internal bool SupportsReturnStrikeStatus(FightBattleStatusState status)
        {
            if (status == null || status.ClassId != 25003
                || !entities.TryGetValue(status.ClassId, out var components)) return false;
            var resolved = ResolveStatusParameters(components, status.ReplacementProperties);
            var cfg = Component(resolved, "FightStatusDamageSkillComponent");
            if (cfg == null) return false;
            var range = Component(cfg, "Range");
            var randomSkill = Component(Component(cfg, "RandomStandPosSkillCfg"), "SkillCfg");
            var remove = Component(cfg, "AutoRemove");
            var stack = Component(cfg, "ChangeStackCfg");
            return Bool(cfg, "Damage") && !Bool(cfg, "Cure")
                && Int(cfg, "StatusId") == 24268 && Int(cfg, "SourceStatusId") == 0
                && Int(cfg, "SkillId") == 0 && !Bool(cfg, "IsAttackerTarget")
                && !Bool(cfg, "IsMutexByBetter") && Component(cfg, "FilterCondistion") == null
                && Int(cfg, "RangeCondition") == 0 && Int(cfg, "SkillTargetType") == 1
                && Int(cfg, "DamageMove") == 0 && Float(cfg, "SkillCustomDamageByHpChangeValue", 0f) == 0f
                && ArrayCount(cfg, "SkillCfgs") == 0 && ArrayCount(cfg, "StatusCfgs") == 0
                && (range == null || Int(range, "RangeSource") == 0 && Int(range, "RangeTarget") == 0
                    && ArrayCount(range, "Range") == 0)
                && (randomSkill == null || Int(randomSkill, "SkillId") == 0)
                && (remove == null || Float(remove, "Duration", -1f) < 0f
                    && Int(remove, "MaxCount", -1) < 0 && Int(remove, "RemoveTarget") == 0
                    && !Bool(remove, "ResetTimerWhenInvoke") && ArrayCount(remove, "RemoveStatusCfgs") == 0)
                && (stack == null || Float(stack, "BasePercent", 0f) == 0f && Int(stack, "ChangeUnit") == 0);
        }
    }

    public sealed partial class FightBattleExecutor
    {
        // Returns whether the original 25003 component belongs to this explicit adapter,
        // including rejected hits. It must never fall through to the old attacker listener.
        private bool TryApplyReturnStrikeStatus(FightBattleStatusState status,
            FightBattleUnitState owner, FightBattleUnitState target,
            FightBattleSkillDefinition skill, FightBattleHitDefinition hit, FightBattleHitResult result)
        {
            if (status.ClassId != 25003) return false;
            var component = status.Components?.DamageSkill;
            if (owner != target || status.OwnerId != target.Id || owner.Hp <= 0 || owner.Ended
                || owner.Statuses.Exists(value => value != null && value.ActionType == 3)
                || status.ForbiddenType != 0 || status.StackCount <= 0
                || component == null || status.Components.Seconds != null
                || !statusCatalog.SupportsReturnStrikeStatus(status) || result == null
                || result.Formula == null && result.Settlement == null
                || result.Type.Dodge || result.Type.Blinding || result.Type.Immunity
                || !HitRuleMatches(component.Rate, component.Block, component.Crit,
                    component.Damage, component.Cure, false, null, component.ElementTypes,
                    null, null, skill, hit, result, target)
                || component.GreaterThanMaxHpPer >= 0f
                    && System.Math.Abs(result.Formula?.AbsoluteDamage ?? 0)
                        <= target.MaxHp * component.GreaterThanMaxHpPer
                || component.EachRoundMaxCount >= 0
                    && component.RoundTriggeredCount >= component.EachRoundMaxCount) return true;

            // User-confirmed successful received damage includes full shield absorption.
            // Advance only after every deterministic gate; preserve the existing saved counters.
            if (AdvanceCounter(component.Rate, component.ConditionCount, component.MaxCount,
                ref component.ConditionProgress, ref component.RoundTriggeredCount, random))
            {
                ApplyComponentStatus(status, component.StatusId, target, null);
                if (component.AutoDestroy && component.MaxCount >= 0
                    && component.RoundTriggeredCount >= component.MaxCount)
                    FightStatusLifecycle.End(owner, status);
            }
            return true;
        }
    }
}
