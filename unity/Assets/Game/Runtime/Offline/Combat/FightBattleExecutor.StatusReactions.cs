using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private const int FreezeStatusId = 24215;
        private const int IceCrystalEndListenerId = 24487;
        private int targetStatusReactionDepth;
        private readonly Dictionary<FightBattleStatusState, Dictionary<int, int>> targetStatusReactionCounts =
            new Dictionary<FightBattleStatusState, Dictionary<int, int>>();

        private void ApplyTargetStatusReactions(FightBattleUnitState target, FightBattleStatusState applied)
        {
            if (movementStartContext == null || statusCatalog == null || applied == null
                || !FightBattleTargeting.IsSelectable(target) || !target.Statuses.Contains(applied)) return;

            targetStatusReactionDepth++;
            try
            {
                // The last successful freeze application owns the eventual thaw. Keep the existing
                // status durations and drop the previous source's listener even if the new source has no secret.
                if (applied.ClassId == FreezeStatusId)
                    foreach (var previous in target.Statuses.Where(value => value?.ClassId == IceCrystalEndListenerId).ToArray())
                        FightStatusLifecycle.End(target, previous);

                var source = ResolveUnit(applied.CreatorId);
                if (!FightBattleTargeting.IsSelectable(source) || source.Side == target.Side) return;
                foreach (var listener in source.Statuses.ToArray())
                {
                    if (listener == null || listener.ForbiddenType != 0 || !source.Statuses.Contains(listener)
                        || !statusCatalog.TryReadTargetStatusReaction(listener.ClassId, applied.ClassId,
                            out int statusId, out float rate, out int perTargetLimit)
                        || !FightSkillTriggerPolicy.AllowsStatus(statusCatalog.GetStatusApplicationSkillContext(applied.RootSkillId), listener))
                        continue;
                    targetStatusReactionCounts.TryGetValue(listener, out var targets);
                    int count = targets != null && targets.TryGetValue(target.Id, out int previousCount) ? previousCount : 0;
                    if (count >= perTargetLimit || !RollComponent(rate)) continue;
                    if (targets == null)
                        targetStatusReactionCounts.Add(listener, targets = new Dictionary<int, int>());
                    // Original MaxCountPerTargetOnceTrigger guards the entire synchronous
                    // application chain, including a newly applied status matching this filter.
                    targets[target.Id] = count + 1;
                    ApplyComponentStatus(listener, statusId, target, null);
                }
            }
            finally
            {
                if (--targetStatusReactionDepth == 0) targetStatusReactionCounts.Clear();
            }
        }

        private void ExecuteStatusEndReactions(FightBattleUnitState owner, FightBattleStatusState ended)
        {
            var context = movementStartContext;
            if (context == null || statusCatalog == null || ended == null
                || !FightBattleTargeting.IsSelectable(owner)) return;

            var invocations = new List<FightBattleStatusSkillInvocation>();
            foreach (var listener in owner.Statuses.ToArray())
            {
                if (listener == null || listener.ForbiddenType != 0 || !owner.Statuses.Contains(listener)
                    || !statusCatalog.TryReadStatusEndReaction(listener.ClassId, ended.ClassId,
                        out int[] statusIds, out var triggers)) continue;
                if (listener.ClassId == IceCrystalEndListenerId && owner.Statuses.Any(value =>
                    value != null && value.ForbiddenType == 0 && statusIds.Contains(value.ClassId))) continue;

                var source = ResolveUnit(listener.CreatorId);
                if (!FightBattleTargeting.IsSelectable(source)) continue;
                int previousCount = invocations.Count;
                foreach (var trigger in triggers)
                    AppendComponentInvocation(listener, trigger, owner, owner, invocations, true);
                if (invocations.Count == previousCount) continue;
                RecordStatusView(owner, listener, FightBattleStatusViewEventKind.Invoke);
                FightStatusLifecycle.End(owner, listener);
            }
            if (invocations.Count == 0) return;

            float delay = Math.Max(0f, context.StatusStartTime);
            ExecuteConfiguredStatusSkills(invocations, context.Casts, context.DamageEvents,
                context.Movements, ref delay, out float duration, out float actionDuration);
            context.Duration = Math.Max(context.Duration, Math.Max(delay, duration));
            context.ActionDuration = Math.Max(context.ActionDuration, actionDuration);
        }
    }

    public sealed partial class FightOfficialCombatCatalog
    {
        internal FightBattleSkillDefinition GetStatusApplicationSkillContext(int skillId)
        {
            if (!entities.TryGetValue(skillId, out var entity) || Component(entity, "FightSkillComponent") == null)
                return null;
            return CreateSkillTriggerContext(skillId);
        }

        internal bool TryReadTargetStatusReaction(int listenerId, int appliedId, out int statusId,
            out float rate, out int perTargetLimit)
        {
            statusId = 0;
            rate = 0;
            perTargetLimit = 0;
            if (!entities.TryGetValue(listenerId, out var entity)) return false;
            var reaction = Component(entity, "StatusApplyTargetHandleComponent");
            // A round reset has no effect on an unlimited (-1) lifetime count.
            if (reaction == null || Int(reaction, "MaxCount") != -1
                || Int(reaction, "MaxCountPerTargetOnceTrigger", 1) <= 0 || ArrayCount(reaction, "StatusReplacePropertyValues") != 0
                || ArrayCount(reaction, "HandleSettings") != 1) return false;
            var filter = Component(reaction, "FilterCondition");
            if (!MatchesAppliedStatusReactionFilter(filter, appliedId)) return false;
            var handler = Object(Array(reaction["HandleSettings"], "StatusApplyTargetHandleComponent.HandleSettings")[0], "target status application handler");
            if (!IsReactionType(handler, "StatusApplyTargetAddStatusHandleSetting") || Int(handler, "HandleType") != 0
                || Bool(handler, "AddByStatusSource") || Bool(handler, "AffectedByProp")) return false;
            var resolver = Component(handler, "StatusIdResolver");
            if (!IsReactionType(resolver, "FixedStatusIdResolver")) return false;
            statusId = Int(resolver, "StatusClassId");
            rate = Float(handler, "Rate", 1f);
            perTargetLimit = Int(reaction, "MaxCountPerTargetOnceTrigger", 1);
            return statusId > 0 && rate > 0f;
        }

        private bool MatchesAppliedStatusReactionFilter(Dictionary<string, object> filter, int appliedId)
        {
            if (filter == null) return true;
            if (IsReactionType(filter, "StatusClassIdFilter")) return Int(filter, "StatusClassId") == appliedId;
            var clauses = new List<FightGameplayTagClause>();
            if (!ReadSourceSkillTagFilter(filter, clauses)
                || !entities.TryGetValue(appliedId, out var applied)) return false;
            long[] tags = ReadGameplayTags(applied);
            return clauses.TrueForAll(clause => clause.Matches(tags));
        }

        internal bool TryReadStatusEndReaction(int listenerId, int endedId, out int[] statusIds,
            out List<FightBattleStatusStateTrigger> triggers)
        {
            statusIds = System.Array.Empty<int>();
            triggers = null;
            if (!entities.TryGetValue(listenerId, out var entity)) return false;
            var reaction = Component(entity, "FightStatusOnEndComponent");
            if (reaction == null || Int(reaction, "MaxCount") != 1 || !Bool(reaction, "DestoryOnTriggerMaxCount")
                || ArrayCount(reaction, "StatusTypes") != 0 || ArrayCount(reaction, "StatusTriggerStatusCfgs") != 0
                || ArrayCount(reaction, "StatusReplacePropertyValues") != 0) return false;
            statusIds = IntArray(reaction, "StatusClassIds");
            if (!statusIds.Contains(endedId)) return false;
            triggers = new List<FightBattleStatusStateTrigger>();
            AddHpAccTriggerEntries(reaction, "StatusTriggerSkillCfgs", "SkillId", triggers, true);
            return triggers.Count > 0 && triggers.TrueForAll(trigger => trigger.Source == 3 && trigger.Target == 2
                && trigger.TargetPos == 1 && !trigger.AffectedByProp && !trigger.UseAI && trigger.PropTarget == null
                && !trigger.StackedCountToDamageFactor && trigger.TargetWithoutStatusIds.Length == 0);
        }

        private static bool IsReactionType(Dictionary<string, object> value, string type)
            => value != null && value.TryGetValue("_type", out object found) && found is string name && name == type;
    }
}
