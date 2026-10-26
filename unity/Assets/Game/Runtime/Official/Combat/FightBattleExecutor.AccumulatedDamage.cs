using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private bool executingAccumulatedDamageEnds;

        private void ExecuteAccumulatedDamageEnds(List<FightBattleSkillCastEvent> casts,
            List<FightBattleDamageEvent> damageEvents, List<FightBattleMovementEvent> movements,
            ref float lastDelay, ref float duration, ref float actionDuration)
        {
            if (statusCatalog == null || executingAccumulatedDamageEnds) return;
            executingAccumulatedDamageEnds = true;
            try
            {
                foreach (FightBattleUnitState owner in new List<FightBattleUnitState>(units))
                {
                    while (owner.EndedAccumulatedDamageStatuses.Count > 0)
                    {
                        FightBattleStatusState status = owner.EndedAccumulatedDamageStatuses[0];
                        owner.EndedAccumulatedDamageStatuses.RemoveAt(0);
                        if (status.AccumulatedDamage == null || !status.AccumulatedDamage.RulesSupported
                            || !unitById.TryGetValue(status.CreatorId, out FightBattleUnitState source)
                            || !FightBattleTargeting.IsSelectable(source) || !FightBattleTargeting.IsSelectable(owner)) continue;
                        int rank = Math.Max(1, status.SourceSkillRank);
                        int level = Math.Max(1, status.SourceSkillLevel);
                        int subRank = Math.Max(1, status.SourceSubRank);
                        int skillId = status.AccumulatedDamage.SkillEntityId;
                        FightBattleSkillDefinition skill = statusCatalog.CreateSkill(skillId, -1, 0, rank,
                            level, subRank, ToPropertyList(statusCatalog.CalculateSkillProps(skillId, rank, level, subRank)));
                        skill.HasCustomDamage = true;
                        skill.CustomDamage = FightStatusAccumulatedDamageRuntime.CustomDamage(status);
                        var targets = new List<FightBattleUnitState> { owner };
                        var cast = new FightBattleSkillCastEvent { ActorId = source.Id, Slot = -1,
                            SkillEntityId = skill.EntityId, SkillItemId = skill.ItemId };
                        cast.TargetIds.Add(owner.Id);
                        casts.Add(cast);
                        checkpoint.Statistics.RecordCast(source.Id, skill.EntityId, skill.ItemId);
                        triggerDepth++;
                        try
                        {
                            int firstDamage = damageEvents.Count;
                            ExecuteSkill(source, skill, targets, casts, damageEvents, movements,
                                out float delay, owner);
                            long hpDamage = 0;
                            for (int i = firstDamage; i < damageEvents.Count; i++)
                            {
                                FightBattleDamageEvent hit = damageEvents[i];
                                if (hit.SourceId == source.Id && hit.TargetId == owner.Id
                                    && hit.SkillEntityId == skill.EntityId && !hit.IsCure && !hit.IsSkillCost)
                                    hpDamage = checked(hpDamage + Math.Max(0L, hit.BeforeHp - hit.AfterHp));
                            }
                            FightStatusAccumulatedDamageRuntime.RecordAdditionalDamage(status, hpDamage,
                                checkpoint.Statistics);
                            lastDelay = Math.Max(lastDelay, delay);
                            duration = Math.Max(duration, ResolveDuration(skill, delay));
                            float action = ResolveActionDuration(skill, delay);
                            if (action >= 0f) actionDuration = Math.Max(actionDuration, action);
                        }
                        finally { triggerDepth--; }
                    }
                }
            }
            finally { executingAccumulatedDamageEnds = false; }
        }
    }
}
