using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private sealed class PendingSkillReplay
        {
            public FightBattleUnitState Actor;
            public FightBattleSkillDefinition Skill;
            public int ReplayType;
            public int StatusInstanceId;
        }

        private readonly Queue<PendingSkillReplay> pendingSkillReplays = new Queue<PendingSkillReplay>();
        private bool executingSkillReplays;

        private bool ReplayReleaseCountAllows(FightBattleUnitState actor, FightBattleSkillDefinition skill, bool check)
        {
            if (!check) return true;
            var slot = actor.SkillState.Slots.Find(value => value.Slot == skill.Slot);
            int limit = slot == null ? skill.LimitedTimes : slot.LimitedTimes;
            if (limit < 0) return true;
            int used = slot == null ? 0 : slot.ReleasedTimes;
            foreach (var queued in pendingSkillReplays)
                if (queued.Actor == actor && queued.Skill.Slot == skill.Slot) used++;
            return used < limit;
        }

        private bool QueueSkillReplay(FightBattleUnitState actor, FightBattleSkillDefinition selected,
            int replayType, bool checkReleaseCount, FightBattleStatusState consumeStatus = null,
            int consumeCount = 0, int triggerStatusId = 0)
        {
            var counter = ReplayCounter(actor, selected);
            if (!CanReplay(selected, counter) || !ReplayReleaseCountAllows(actor, selected, checkReleaseCount)) return false;
            var next = selected;
            if (selected.ReplayReplacementEntities.Count > 0)
            {
                if (counter.Count >= selected.ReplayReplacementEntities.Count)
                    throw new NotSupportedException("Original ordered replay replacement array exhausted; verify the configured continuation.");
                int entity = selected.ReplayReplacementEntities[counter.Count];
                if (entity != selected.EntityId)
                {
                    if (statusCatalog == null) throw new InvalidOperationException("Replay replacement requires the original catalog.");
                    next = statusCatalog.CreateSkill(entity, selected.Slot, selected.ItemId, selected.Rank,
                        selected.Level, selected.SubRank, ToPropertyList(statusCatalog.CalculateSkillProps(entity,
                            selected.Rank, selected.Level, selected.SubRank)));
                    next.RootEntityId = selected.RootEntityId;
                    next.UseItemId = selected.UseItemId;
                    next.SkillSkinId = selected.SkillSkinId;
                    next.ProfessionAvailability = selected.ProfessionAvailability;
                    next.SkillCost = selected.SkillCost;
                    InheritBlessingScales(next, selected.BlessPropertyScales);
                }
            }
            var available = FightBattleTargeting.Select(actor, units, next.TargetType, 0, false, false, next, null);
            if (available.Count == 0 && !FightBattleTargeting.HasCasterAreaTargets(actor, units, next)) return false;
            if (consumeStatus != null && consumeCount > 0)
            {
                int previous = consumeStatus.StackCount;
                consumeStatus.StackCount -= consumeCount;
                FightStatusPropertyRuntime.OnStackChanged(consumeStatus, previous);
                if (consumeStatus.StackCount <= 0) FightStatusLifecycle.End(actor, consumeStatus);
                RebuildBattleProperties();
            }
            counter.Count = checked(counter.Count + 1);
            pendingSkillReplays.Enqueue(new PendingSkillReplay { Actor = actor, Skill = next, ReplayType = replayType, StatusInstanceId = triggerStatusId });
            return true;
        }

        private void CollectDirectSkillReplay(FightBattleUnitState actor, FightBattleSkillDefinition skill)
        {
            if (!skill.HasReplay || !CanReplay(skill, ReplayCounter(actor, skill))
                || !ReplayReleaseCountAllows(actor, skill, true)) return;
            var status = actor.Statuses.Find(value => value != null && value.ClassId == skill.ReplayStatusId);
            if (!skill.ReplayUsesRate && (status == null || !(skill.ReplayRelation == 1
                ? status.StackCount >= skill.ReplayStackCount : status.StackCount == skill.ReplayStackCount))) return;
            if (skill.ReplayUsesRate && (skill.ReplayChance <= 0d
                || (skill.ReplayChance < 1d && random.Next() >= skill.ReplayChance))) return;
            var selected = skill;
            if (skill.ReplaySelectMatchingSlot)
            {
                var candidates = actor.Skills.FindAll(value => value.Slot >= 0
                    && skill.ReplaySlotElementTypes.Contains(value.ElementType)
                    && CanReplay(value, ReplayCounter(actor, value)) && ReplayReleaseCountAllows(actor, value, true));
                if (candidates.Count == 0) return;
                selected = candidates[(int)(random.Next() * candidates.Count)];
            }
            QueueSkillReplay(actor, selected, skill.ReplayType, true,
                skill.ReplayConsumeStack ? status : null, skill.ReplayConsumeCount);
        }

        private void CollectStatusSkillReplays(FightBattleUnitState actor, FightBattleSkillDefinition skill)
        {
            // Original ReplaySkill requires an equipped slot. Each responding status owns its rate and cap.
            if (skill.Slot < 0) return;
            int gameStatus = checkpoint.Finished ? 9 : 2; // GameStatus.End / Play at this executor boundary.
            foreach (var status in new List<FightBattleStatusState>(actor.Statuses))
            {
                var replay = status?.SkillReplay;
                if (replay == null || !FightSkillTriggerPolicy.AllowsStatus(skill, status)
                    || replay.FilterTypes.Contains(skill.ActionType)
                    || (replay.TriggerInGameStatusList.Count > 0 && !replay.TriggerInGameStatusList.Contains(gameStatus))
                    || !CanReplay(skill, ReplayCounter(actor, skill))
                    || !ReplayReleaseCountAllows(actor, skill, replay.IsCheckSkillLimitedTimes)) continue;
                long[] tags = skill.GameplayTags;
                if (tags == null && statusCatalog != null && statusCatalog.ContainsEntity(skill.EntityId))
                    skill.GameplayTags = tags = statusCatalog.SkillGameplayTags(skill.EntityId);
                if (replay.TagQuery.Exists(clause => !clause.Matches(tags))) continue;
                int count = replay.CountRound == actor.SkillState.Round ? replay.CountEachRound : 0;
                if (replay.MaxCountEachRound >= 0 && count >= replay.MaxCountEachRound) continue;
                float rate = replay.Rate + replay.ExtraRate;
                if (rate <= 0f || (rate < 1f && random.Next() >= rate))
                {
                    replay.ExtraRate += replay.AddRateWhenNotHit;
                    continue;
                }
                if (!QueueSkillReplay(actor, skill, skill.ReplayType, replay.IsCheckSkillLimitedTimes, triggerStatusId: status.InstanceId)) continue;
                replay.ExtraRate = 0f;
                replay.CountRound = actor.SkillState.Round;
                replay.CountEachRound = checked(count + 1);
            }
        }

        // User-confirmed FIFO: collect every responding status before running any queued replay.
        // A replay's own end processing appends its newly triggered casts behind existing requests.
        private void ApplySkillReplay(FightBattleUnitState actor, FightBattleSkillDefinition skill,
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> damageEvents,
            List<FightBattleMovementEvent> movements, ref float lastDelay)
        {
            if (actor.Hp <= 0 || actor.Ended) return;
            CollectDirectSkillReplay(actor, skill);
            CollectStatusSkillReplays(actor, skill);
            if (executingSkillReplays) return;
            executingSkillReplays = true;
            try
            {
                while (pendingSkillReplays.Count > 0)
                {
                    var request = pendingSkillReplays.Dequeue();
                    var source = request.Actor;
                    var next = request.Skill;
                    if (source.Hp <= 0 || source.Ended) continue;
                    var targets = FightBattleTargeting.Select(source, units, next.TargetType, 0, false, false, next, random);
                    if (targets.Count == 0 && !FightBattleTargeting.HasCasterAreaTargets(source, units, next)) continue;
                    var slot = source.SkillState.Slots.Find(value => value.Slot == next.Slot);
                    if (slot != null) slot.ReleasedTimes = checked(slot.ReleasedTimes + 1);
                    checkpoint.Statistics.RecordCast(source.Id, next.EntityId, next.ItemId);
                    var cast = new FightBattleSkillCastEvent { ActorId = source.Id, Slot = next.Slot,
                        SkillEntityId = next.EntityId, SkillItemId = next.ItemId, IsReplay = true, ReplayType = request.ReplayType, ReplayStatusInstanceId = request.StatusInstanceId };
                    foreach (var target in targets) cast.TargetIds.Add(target.Id);
                    casts.Add(cast);
                    ExecuteSkill(source, next, targets, casts, damageEvents, movements, out float delay);
                    lastDelay = Math.Max(lastDelay, delay);
                }
            }
            finally
            {
                executingSkillReplays = false;
                pendingSkillReplays.Clear();
            }
        }
    }
}
