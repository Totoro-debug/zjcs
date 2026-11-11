using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightBattleStatusDarkHitState
    {
        public bool DefinitionResolved;
        public bool Supported;
        public float Rate;
        public int MaxTriggerCount;
        public int TriggerCount;
        public List<int> Elements = new List<int>();
        public List<int> IgnoredSkills = new List<int>();
        public bool IgnoreTeammate;
        public List<FightBattleStatusStateTrigger> Statuses = new List<FightBattleStatusStateTrigger>();

        public FightBattleStatusDarkHitState Copy()
        {
            var copy = (FightBattleStatusDarkHitState)MemberwiseClone();
            copy.Elements = new List<int>(Elements ?? new List<int>());
            copy.IgnoredSkills = new List<int>(IgnoredSkills ?? new List<int>());
            copy.Statuses = new List<FightBattleStatusStateTrigger>();
            foreach (var trigger in Statuses ?? new List<FightBattleStatusStateTrigger>())
                copy.Statuses.Add(trigger?.Copy());
            return copy;
        }
    }

    public sealed partial class FightOfficialCombatCatalog
    {
        private static FightBattleStatusDarkHitState ParseDarkHit(Dictionary<string, object> component)
        {
            if (component == null) return null;
            var source = Component(component, "DoDamageSourceCfg");
            var result = new FightBattleStatusDarkHitState {
                DefinitionResolved = true,
                Supported = source != null && StringField(source, "_type") == "FightStatusDoDamageStatusSourceCfg"
                    && Int(source, "HitSourceType") == 2 && Component(component, "RoundCountCfg") == null
                    && !Bool(component, "RequireDamageResult") && ArrayCount(component, "SkillCfgs") == 0,
                Rate = Float(component, "Rate", 1f), MaxTriggerCount = Int(component, "MaxTriggerCount", -1),
                IgnoreTeammate = Bool(component, "IgnoreTeammate", true),
                Elements = new List<int>(IntArray(component, "HitSkillElementTypes")),
                IgnoredSkills = new List<int>(IntArray(component, "IgnoreHitSkills"))
            };
            AddTriggerStatuses(component, "StatusCfgs", result.Statuses);
            foreach (var trigger in result.Statuses)
                result.Supported &= trigger.ApplyTarget == 5 && (trigger.PropTarget == null
                    || !trigger.PropTarget.IsEnemy && !trigger.PropTarget.IncludeApplicator
                    && !trigger.PropTarget.Highest && !trigger.PropTarget.SupportCustomFilter
                    && trigger.PropTarget.PropertyId == 0
                    && (trigger.PropTarget.RoleTypes == null || trigger.PropTarget.RoleTypes.Length == 0));
            return result;
        }

        internal void RestoreDarkHitDefinition(FightBattleStatusState status)
        {
            var previous = status.Components?.DarkHit;
            if (previous != null && previous.DefinitionResolved || !entities.TryGetValue(status.ClassId, out var components)) return;
            var resolved = ResolveStatusParameters(components, status.ReplacementProperties);
            var definition = ParseDarkHit(Component(resolved, "FightStatusDoDamageHandleComponent"));
            if (definition != null && previous != null) definition.TriggerCount = previous.TriggerCount;
            if (status.Components == null) status.Components = new FightBattleStatusComponentState();
            status.Components.DarkHit = definition;
        }
    }

    public sealed partial class FightBattleExecutor
    {
        private void ApplyDarkHitStatuses(FightBattleUnitState actor, FightBattleUnitState target,
            FightBattleSkillDefinition skill, FightBattleHitResult result)
        {
            if (actor == null || target == null || skill == null || result == null
                || result.Type.Dodge || result.Type.Blinding || result.Type.Immunity) return;
            foreach (var status in new List<FightBattleStatusState>(actor.Statuses))
            {
                var component = status?.Components?.DarkHit;
                if (component == null || !component.DefinitionResolved || !component.Supported
                    || !actor.Statuses.Contains(status) || status.ForbiddenType != 0 || status.StackCount <= 0
                    || !FightSkillTriggerPolicy.AllowsStatus(skill, status)
                    || component.IgnoreTeammate && actor.Side == target.Side
                    || component.Elements.Count > 0 && !component.Elements.Contains(skill.ElementType)
                    || component.IgnoredSkills.Contains(skill.EntityId)
                    || component.MaxTriggerCount >= 0 && component.TriggerCount >= component.MaxTriggerCount
                    || !RollComponent(component.Rate)) continue;
                component.TriggerCount++;
                foreach (var trigger in component.Statuses)
                {
                    if (trigger == null || trigger.EntityId <= 0) continue;
                    double rate = statusCatalog.CalculateStatusApplicationRate(actor, target,
                        trigger.EntityId, trigger.BasePercent, trigger.AffectedByProp, formula);
                    if (!(random.Next() <= rate)) continue;
                    ApplyComponentStatus(status, trigger.EntityId, target, null, trigger.StatusReplacementProperties);
                }
            }
        }
    }
}
