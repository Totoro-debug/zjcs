using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private void RestoreElementHarmonyDefinition(FightBattleStatusState status)
        {
            if (status == null || status.ClassId != 25111 || status.Components?.Start != null) return;
            var template = statusCatalog.CreateStatus(status.ClassId, status.OwnerId, status.CreatorId,
                status.InstanceId, status.RootSkillId, status.ApplyingSkillItemId, status.SourceSkillRank,
                status.SourceSkillLevel, status.SourceSubRank, status.SourceProperties,
                replacementProperties: status.ReplacementProperties, blessPropertyScales: status.BlessPropertyScales);
            if (status.Components == null) status.Components = new FightBattleStatusComponentState();
            status.Components.Start = template.Components?.Start?.Copy();
        }

        private static bool IsPendingElementHarmonyStart(FightBattleUnitState owner,
            FightBattleStatusState status) => owner != null && !owner.Ended && status != null
                && status.ClassId == 25111 && status.Components?.Start != null
                && !status.ElementHarmonyStartChecked;

        private void StartElementHarmony(FightBattleUnitState owner, FightBattleStatusState status)
        {
            if (!IsPendingElementHarmonyStart(owner, status) || movementStartContext == null) return;
            FightStatusLifecycle.Initialize(status, StatusClockRound(status));
            ApplyElementHarmony(owner, status);
        }

        // User-confirmed offline rule: include an equipped skill whenever its
        // original hit graph actually deals damage, including debuff skills.
        private void ApplyElementHarmony(FightBattleUnitState owner, FightBattleStatusState source)
        {
            if (source.ElementHarmonyStartChecked) return;
            source.ElementHarmonyStartChecked = true;
            var elements = new HashSet<int>();
            foreach (FightBattleSkillDefinition skill in owner.Skills ?? new List<FightBattleSkillDefinition>())
            {
                if (skill == null || skill.Slot < 0 || skill.ElementType == 0
                    || owner.SkillState?.Slots == null
                    || !owner.SkillState.Slots.Exists(slot => slot != null && slot.Slot == skill.Slot)
                    || !HasConfiguredDamage(skill, new HashSet<int>(), new HashSet<int>())) continue;
                elements.Add(skill.ElementType);
            }
            foreach (FightBattleStatusStateTrigger trigger in source.Components.Start.Statuses)
            {
                if (trigger == null || trigger.EntityId <= 0) continue;
                FightBattleUnitState target = trigger.ApplyTarget == 1 ? ResolveUnit(source.CreatorId) : owner;
                if (target == null) continue;
                int count = Math.Min(4, elements.Count);
                var origin = FightBattleStatusOrigin.Resolve(source);
                var existing = target.Statuses?.Find(status => status != null && status.ClassId == trigger.EntityId
                    && FightBattleStatusOrigin.Matches(FightBattleStatusOrigin.Resolve(status), origin));
                if (existing != null)
                {
                    if (count == 0) FightStatusLifecycle.End(target, existing);
                    else FightStatusLifecycle.AddStack(target, existing, count - existing.StackCount);
                    continue;
                }
                for (int i = 0; i < count; i++)
                    if (RollComponent(trigger.BasePercent))
                        ApplyComponentStatus(source, trigger.EntityId, target, null,
                            trigger.StatusReplacementProperties);
            }
        }

        private bool HasConfiguredDamage(FightBattleSkillDefinition skill, HashSet<int> visited,
            HashSet<int> visitedStatuses)
        {
            if (skill == null || skill.EntityId > 0 && !visited.Add(skill.EntityId)) return false;
            if (skill.HasCustomDamage) return true;
            foreach (FightBattleHitDefinition hit in skill.Hits ?? new List<FightBattleHitDefinition>())
            {
                if (hit == null) continue;
                if (hit.ActionType == 4 && (hit.HasCustomDamage || hit.HasFixedDamage && hit.DamageType != 0)) return true;
                if (StatusApplicationsHaveDamage(hit.AppliedStatuses, skill, visited, visitedStatuses)) return true;
                if (hit.ChildSkillId > 0 && hit.ChildSkillRate > 0f
                    && ChildHasConfiguredDamage(hit.ChildSkillId, skill, visited, visitedStatuses)) return true;
            }
            foreach (FightBattleHitEntity hit in skill.HitGraphEntities ?? new List<FightBattleHitEntity>())
            {
                if (hit == null) continue;
                if (hit.ActionType == 4 && hit.HasFixedDamage && hit.DamageType != 0) return true;
                if (StatusApplicationsHaveDamage(hit.AppliedStatuses, skill, visited, visitedStatuses)) return true;
                if (hit.ChildSkillId > 0 && hit.ChildSkillRate > 0f
                    && ChildHasConfiguredDamage(hit.ChildSkillId, skill, visited, visitedStatuses)) return true;
            }
            foreach (FightBattleSkillExtra extra in skill.ExtraSkills ?? new List<FightBattleSkillExtra>())
                if (extra != null && extra.SkillId > 0 && extra.Rate > 0f
                    && ChildHasConfiguredDamage(extra.SkillId, skill, visited, visitedStatuses)) return true;
            foreach (int id in skill.ComboSkills ?? new List<int>())
                if (id > 0 && ChildHasConfiguredDamage(id, skill, visited, visitedStatuses)) return true;
            foreach (int id in skill.FallbackSkills ?? new List<int>())
                if (id > 0 && ChildHasConfiguredDamage(id, skill, visited, visitedStatuses)) return true;
            return false;
        }

        private bool StatusApplicationsHaveDamage(List<FightBattleStatusApplication> applications,
            FightBattleSkillDefinition parent, HashSet<int> visited, HashSet<int> visitedStatuses)
        {
            foreach (FightBattleStatusApplication application in applications ?? new List<FightBattleStatusApplication>())
            {
                int id = application?.StatusId ?? 0;
                if (id <= 0 || application.Chance <= 0f || !visitedStatuses.Add(id)
                    || !statusCatalog.ContainsEntity(id) || !statusCatalog.HasComponent(id, "FightStatusComponent")) continue;
                var status = statusCatalog.CreateStatus(id, 1, 1, 1, parent.EntityId, parent.ItemId,
                    parent.Rank, parent.Level, parent.SubRank, parent.Properties);
                if (!status.HasMoveNear || status.MoveNearRate <= 0f) continue;
                if (status.MoveNearSkillId > 0
                    && ChildHasConfiguredDamage(status.MoveNearSkillId, parent, visited, visitedStatuses)) return true;
                foreach (FightBattleStatusStateTrigger trigger in status.MoveNearSkills ?? new List<FightBattleStatusStateTrigger>())
                    if (trigger != null && trigger.EntityId > 0 && trigger.BasePercent > 0f
                        && ChildHasConfiguredDamage(trigger.EntityId, parent, visited, visitedStatuses)) return true;
            }
            return false;
        }

        private bool ChildHasConfiguredDamage(int id, FightBattleSkillDefinition parent, HashSet<int> visited,
            HashSet<int> visitedStatuses)
        {
            if (visited.Contains(id) || statusCatalog == null || !statusCatalog.ContainsEntity(id)
                || !statusCatalog.HasComponent(id, "FightSkillComponent")) return false;
            var child = statusCatalog.CreateSkill(id, -1, parent.ItemId, parent.Rank, parent.Level,
                parent.SubRank, parent.Properties);
            return HasConfiguredDamage(child, visited, visitedStatuses);
        }
    }
}
