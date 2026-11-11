using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightBattleStatusHitScopeState
    {
        public bool DefinitionResolved;
        public List<int> SkillElementTypes = new List<int>();
        public int ConditionCount;
        public float Rate;
        public int MaxCount;
        public int SkillId;
        public int ConditionProgress;
        public int TriggeredCount;

        public FightBattleStatusHitScopeState Copy()
        {
            var copy = (FightBattleStatusHitScopeState)MemberwiseClone();
            copy.SkillElementTypes = new List<int>(SkillElementTypes ?? new List<int>());
            return copy;
        }
    }

    public static class FightStatusHitScope
    {
        // Confirmed offline event binding: each successful target occurrence checks
        // the original component once, before any resulting movement changes its cell.
        public static void Collect(FightBattleUnitState owner, FightBattleUnitState target,
            FightBattleSkillDefinition skill, FightBattleHitResult result, float delay,
            List<FightBattleStatusSkillInvocation> output, FightRandomStream random)
        {
            if (owner == null || target == null || skill == null || result == null || output == null
                || result.Type.Dodge || result.Type.Immunity || result.Type.Blinding) return;
            foreach (var status in new List<FightBattleStatusState>(owner.Statuses))
            {
                var component = status?.Components?.HitScope;
                if (component == null || !component.DefinitionResolved || component.SkillId <= 0
                    || !owner.Statuses.Contains(status) || status.ForbiddenType != 0 || status.StackCount <= 0
                    || !FightSkillTriggerPolicy.AllowsStatus(skill, status)
                    || component.SkillElementTypes.Count > 0 && !component.SkillElementTypes.Contains(skill.ElementType)
                    || component.MaxCount >= 0 && component.TriggeredCount >= component.MaxCount) continue;
                component.ConditionProgress++;
                if (component.ConditionProgress < Math.Max(1, component.ConditionCount)) continue;
                component.ConditionProgress = 0;
                if (component.Rate <= 0f || component.Rate < 1f && random != null && random.Next() > component.Rate) continue;
                component.TriggeredCount++;
                var cast = FightBattleSkillCastPresentation.Capture(owner, skill,
                    new List<FightBattleUnitState> { target });
                output.Add(new FightBattleStatusSkillInvocation {
                    Trigger = new FightBattleStatusStateTrigger { EntityId = component.SkillId }.WithSource(status),
                    SourceUnitId = owner.Id, TargetUnitId = target.Id, TargetPositionUnitId = target.Id,
                    HitOrigin = new FightGridPoint(target.X, target.Y),
                    HitFacing = cast.FacingIndex, HitFlipX = cast.FlipX,
                    Delay = Math.Max(0f, delay), AllowDeadTarget = true
                });
            }
        }
    }

    public sealed partial class FightOfficialCombatCatalog
    {
        private static FightBattleStatusHitScopeState ParseHitScope(Dictionary<string, object> component)
        {
            if (component == null) return null;
            return new FightBattleStatusHitScopeState {
                DefinitionResolved = true, SkillElementTypes = new List<int>(IntArray(component, "SkillEleTypes")),
                ConditionCount = Int(component, "ConditionCount"), Rate = Float(component, "Rate", 0f),
                MaxCount = Int(component, "MaxCount", -1), SkillId = Int(component, "SkillId")
            };
        }

        internal void RestoreHitScopeStatusDefinition(FightBattleStatusState status)
        {
            var previous = status.Components?.HitScope;
            if (previous != null && previous.DefinitionResolved || !entities.TryGetValue(status.ClassId, out var components)) return;
            if (status.Components == null) status.Components = new FightBattleStatusComponentState();
            var resolved = ResolveStatusParameters(components, status.ReplacementProperties);
            var definition = ParseHitScope(Component(resolved, "FightStatusHitScopeComponent"));
            if (definition != null && previous != null) {
                definition.ConditionProgress = previous.ConditionProgress;
                definition.TriggeredCount = previous.TriggeredCount;
            }
            status.Components.HitScope = definition;
        }
    }
}
