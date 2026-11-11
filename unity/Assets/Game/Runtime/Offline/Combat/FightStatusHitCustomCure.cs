using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightBattleStatusHitCustomCureState
    {
        public bool DefinitionResolved;
        public bool Supported;
        public FightBattleStatusHitSkillComponentState Filter;

        public FightBattleStatusHitCustomCureState Copy() => new FightBattleStatusHitCustomCureState {
            DefinitionResolved = DefinitionResolved, Supported = Supported, Filter = Filter?.Copy()
        };
    }

    public sealed partial class FightOfficialCombatCatalog
    {
        private static FightBattleStatusHitCustomCureState ParseHitCustomCure(Dictionary<string, object> component)
        {
            if (component == null) return null;
            var filter = new FightBattleStatusHitSkillComponentState {
                HitSourceType = Int(component, "HitSourceType"), Rate = Float(component, "Rate", 1f),
                Block = Bool(component, "Block"), Crit = Bool(component, "Crit"),
                Damage = Bool(component, "Damage"), Cure = Bool(component, "Cure"),
                IsTrueDamage = Bool(component, "IsTrueDamage"), IsApplyOneHit = Bool(component, "IsApplyOneHit"),
                HitSkillTypes = new List<int>(IntArray(component, "HitSkillTypes")),
                TargetActionTypes = new List<int>(IntArray(component, "TargetActionTypes")),
                HitSkillElementTypes = new List<int>(IntArray(component, "HitSkillElementTypes")),
                HitSkillEntityClassIds = new List<int>(IntArray(component, "HitSkillEntityClassIds")),
                ConditionCount = Int(component, "ConditionCount", 1), MaxInvokeCount = Int(component, "MaxInvokeCount", -1),
                AutoRemove = Bool(component, "AutoRemove"), RoundResetCount = Int(component, "RoundResetCount", -1),
                SkillId = Int(component, "SkillId"), IsSkillTargetPos = Bool(component, "IsSkillTargetPos", true)
            };
            // Only the confirmed independent-hit, self-position variant is bound.
            return new FightBattleStatusHitCustomCureState {
                DefinitionResolved = true, Filter = filter,
                Supported = filter.HitSourceType == 2 && filter.Damage && !filter.Cure
                    && !filter.IsApplyOneHit && !filter.IsSkillTargetPos && filter.RoundResetCount < 0
                    && !Bool(component, "ResetAtRoundStart") && Int(component, "SuckHpLimitPropType") == 0
                    && Float(component, "SuckHpLimitPercent", 0f) == 0f
            };
        }

        internal void RestoreHitCustomCureDefinition(FightBattleStatusState status)
        {
            var previous = status.Components?.HitCustomCure;
            if (previous != null && previous.DefinitionResolved || !entities.TryGetValue(status.ClassId, out var components)) return;
            var resolved = ResolveStatusParameters(components, status.ReplacementProperties);
            var definition = ParseHitCustomCure(Component(resolved, "FightStatusHitCustomCureComponent"));
            if (definition?.Filter != null && previous?.Filter != null) {
                definition.Filter.ConditionProgress = previous.Filter.ConditionProgress;
                definition.Filter.InvokeCount = previous.Filter.InvokeCount;
            }
            if (status.Components == null) status.Components = new FightBattleStatusComponentState();
            status.Components.HitCustomCure = definition;
        }
    }

    public sealed partial class FightBattleExecutor
    {
        private void CollectHitCustomCures(FightBattleUnitState actor, FightBattleUnitState target,
            FightBattleSkillDefinition skill, FightBattleHitDefinition hit, FightBattleHitResult result,
            float delay, List<FightBattleStatusSkillInvocation> output)
        {
            if (actor == null || target == null || skill == null || hit == null || result?.Settlement == null
                || result.IsCure || result.Type.Dodge || result.Type.Blinding || result.Type.Immunity) return;
            foreach (var status in new List<FightBattleStatusState>(actor.Statuses))
            {
                var component = status?.Components?.HitCustomCure;
                var filter = component?.Filter;
                if (component == null || !component.DefinitionResolved || !component.Supported || filter == null
                    || !actor.Statuses.Contains(status) || status.ForbiddenType != 0 || status.StackCount <= 0
                    || !FightSkillTriggerPolicy.AllowsStatus(skill, status)
                    || !HitRuleMatches(filter.Rate, filter.Block, filter.Crit, filter.Damage, filter.Cure,
                        filter.IsTrueDamage, filter.HitSkillTypes, filter.HitSkillElementTypes,
                        filter.HitSkillEntityClassIds, filter.TargetActionTypes, skill, hit, result, target)
                    || !AdvanceCounter(filter.Rate, filter.ConditionCount, filter.MaxInvokeCount,
                        ref filter.ConditionProgress, ref filter.InvokeCount, random)) continue;

                // DamageValue retains the post-shield HP amount before remaining-HP clipping;
                // RealDamageValue is intentionally excluded from this confirmed rule.
                var master = actor.MasterId > 0 ? ResolveUnit(actor.MasterId) : null;
                long cure = formula.CalculateSuckHp(new FightSuckHpFormulaRequest {
                    SourceProps = ToPropertyDictionary(actor.Properties), SourceType = actor.CharacterType,
                    SourceMasterType = master?.CharacterType ?? 0,
                    SkillProps = ToPropertyDictionary(status.StatusProperties),
                    SuckPropType = formula.PropertyId("SuckHpByDamage"), FixedSuckPropType = formula.PropertyId("FixedSuckHp"),
                    Damage = Math.Min(0L, result.Settlement.DamageValue),
                    DamageSkillProps = ToPropertyDictionary(skill.Properties), DamageHitPropType = hit.DamageProperty
                });
                var invocation = new FightBattleStatusSkillInvocation {
                    Trigger = new FightBattleStatusStateTrigger {EntityId = filter.SkillId}.WithSource(status),
                    SourceUnitId = actor.Id, TargetUnitId = actor.Id, TargetPositionUnitId = actor.Id,
                    Delay = delay, HasCustomCure = true, CustomCure = cure
                };
                output.Add(invocation);
                if (filter.AutoRemove && filter.MaxInvokeCount >= 0 && filter.InvokeCount >= filter.MaxInvokeCount)
                    FightStatusLifecycle.End(actor, status);
            }
        }
    }
}
