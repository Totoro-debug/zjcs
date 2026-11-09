using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private interface IFightCureInterceptor
        {
            int Order { get; }
            void Apply(FightBattleExecutor context, FightBattleUnitState source,
                FightBattleUnitState target, FightBattleSkillDefinition skill, FightCureFormulaResult cure);
        }

        private sealed class StatusCureReduction : IFightCureInterceptor
        {
            private readonly FightBattleStatusState status;
            private readonly FightBattleStatusCureReduceState configuration;
            public int Order => configuration.ExecuteOrder;

            internal StatusCureReduction(FightBattleStatusState status)
            { this.status = status; configuration = status.Components.CureReduce; }

            public void Apply(FightBattleExecutor context, FightBattleUnitState source,
                FightBattleUnitState target, FightBattleSkillDefinition skill, FightCureFormulaResult cure)
            {
                if (cure.Cure <= 0 || !target.Statuses.Contains(status) || status.ForbiddenType != 0
                    || status.StackCount <= 0 || !configuration.DefinitionResolved || configuration.HasUnsupportedFilter
                    || configuration.IsCheckSkillTrigger && !FightSkillTriggerPolicy.AllowsStatus(skill, status)) return;
                // This capability's approved binding treats an absent tag component
                // as an empty set. Other tag-filter capabilities retain their own rules.
                long[] tags = skill?.GameplayTags;
                if (tags != null && tags.Length != 4) return;
                if (configuration.SourceSkillTagQuery != null)
                    foreach (var clause in configuration.SourceSkillTagQuery)
                        if (clause == null || clause.Tags == null || clause.Tags.Length != 4 || !clause.Matches(tags)) return;
                if (float.IsNaN(configuration.ReduceCurePer) || configuration.ReduceCurePer <= 0f
                    || !context.RollComponent(configuration.Rate)) return;
                decimal factor = (decimal)Math.Min(1f, configuration.ReduceCurePer);
                long removed = (long)decimal.Truncate(cure.Cure * factor);
                if (removed <= 0) return;
                cure.Cure -= removed;
                cure.AbsoluteCure = Math.Max(0L, cure.AbsoluteCure - removed);
                context.ApplyComponentStatuses(status, configuration.StatusTriggers, source, target, removed);
            }
        }

        private void ApplyCureInterceptors(FightBattleUnitState source, FightBattleUnitState target,
            FightBattleSkillDefinition skill, FightCureFormulaResult cure)
        {
            if (target?.Statuses == null || cure == null || cure.Cure <= 0 || statusCatalog == null) return;
            var interceptors = new List<IFightCureInterceptor>();
            foreach (var status in target.Statuses)
                if (status?.Components?.CureReduce != null) interceptors.Add(new StatusCureReduction(status));
            // OrderBy is stable: equal original priorities keep application order.
            foreach (var interceptor in interceptors.OrderBy(value => value.Order))
                interceptor.Apply(this, source, target, skill, cure);
            if (interceptors.Count > 0) RebuildBattleProperties();
        }
    }
}
