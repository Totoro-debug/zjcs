using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    internal sealed partial class FightHitChainExecution
    {
        private readonly Dictionary<FightBattleHitDefinition, List<FightBattleHitDefinition>> customChildren
            = new Dictionary<FightBattleHitDefinition, List<FightBattleHitDefinition>>();

        private FightHitTargetSelection SelectCustom(Step step, FightHitTargetSelectionContext context,
            FightBattleHitTiming.Flight flight)
        {
            var center = FightHitAnchorExtensions.ResolveCenter(step.Hit.Anchor,
                context.Source, context.Primary, step.Cast);
            var offsets = FightBattleTargeting.TransformScope(step.Hit.Scope,
                step.Hit.ScopeTransform, context.Source, step.Cast);
            customChildren.TryGetValue(step.Original, out var children);
            // The parent only discovers cells. Each original HitCfg owns a separate
            // child entity; no damage, probability draw or AI count belongs to this scan.
            if (children != null && offsets != null)
                for (int index = 0; index < offsets.Count; index++)
                {
                    var cell = center + offsets[index];
                    if (context.Grid != null && !context.Grid.Contains(cell)) continue;
                    float scopeDelay = step.Hit.ScopeEntries != null && index < step.Hit.ScopeEntries.Count
                        ? step.Hit.ScopeEntries[index].Delay : 0f;
                    // Snapshot before settlement/summoning can alter the live roster.
                    foreach (var target in new List<FightBattleUnitState>(context.Units))
                    {
                        if (!FightBattleTargeting.IsEligibleSkillTarget(context.TargetingSource ?? context.Source,
                            target, context.Skill) || !FightBattleTargeting.OccupiesPosition(target, cell)) continue;
                        float trigger = FightBattleHitTiming.EffectDelay(step.Hit, step.Cast, center, flight) + scopeDelay;
                        foreach (var child in children)
                            pending.Add(new Step { Original = child,
                                Hit = child.AtDelay(trigger + child.CustomLinkDelay),
                                Cast = step.Cast.WithHitOrigin(center), CustomTarget = target, CustomCell = cell,
                                Order = ++serial });
                    }
                }
            return new FightHitTargetSelection(new List<FightBattleUnitState>(),
                new List<FightHitOccurrence>(), new List<FightGridPoint>(), new List<FightGridPoint> { center });
        }
    }
}
