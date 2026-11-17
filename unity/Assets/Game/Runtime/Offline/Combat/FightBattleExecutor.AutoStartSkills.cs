using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private bool HasPendingAutoStartSkills()
        {
            if (checkpoint.Counter != 0 || skillItems == null || statusCatalog == null) return false;
            return units.Any(owner => owner != null && !owner.Ended && owner.Skills != null
                && owner.SkillState?.Slots != null && owner.Skills.Any(skill => IsPendingAutoStart(owner, skill)));
        }

        private bool IsPendingAutoStart(FightBattleUnitState owner, FightBattleSkillDefinition skill)
        {
            if (skill == null || skill.Slot < 0 || skillItems == null) return false;
            int itemId = skill.UseItemId > 0 ? skill.UseItemId : skill.ItemId;
            if (!skillItems.TryGet(itemId, out FightOfficialSkillItem item) || (item.TryAtStartType & 2) == 0)
                return false;
            var slot = owner.SkillState.Slots.Find(value => value != null && value.Slot == skill.Slot);
            return slot != null && !slot.AutoStartChecked;
        }

        private void ExecuteAutoStartSkills(MovementStartContext context, ref FightBattleUnitState firstOwner)
        {
            if (!HasPendingAutoStartSkills()) return;
            // User-confirmed offline order: opening passives settle first, then
            // effective speed and equipped slot order; stable ties use roster order.
            var owners = units.Where(owner => owner != null && !owner.Ended)
                .OrderByDescending(owner => (long)Math.Max(1, owner.Turn.Speed)
                    * Math.Max(1, owner.Turn.RankSpeedScale)).ToArray();
            foreach (var owner in owners)
            {
                if (owner.Skills == null || owner.SkillState?.Slots == null) continue;
                foreach (var configured in owner.Skills.Where(skill => IsPendingAutoStart(owner, skill))
                    .OrderBy(skill => skill.Slot).ToArray())
                {
                    var slot = owner.SkillState.Slots.Find(value => value != null && value.Slot == configured.Slot);
                    slot.AutoStartChecked = true;
                    if (!FightBattleTargeting.IsSelectable(owner)) continue;
                    var excluded = new HashSet<int>(owner.SkillState.Slots.Where(value => value != null
                        && value.Slot != configured.Slot).Select(value => value.Slot));
                    int round = owner.SkillState.Round;
                    bool driving = owner.SkillState.IsDriving;
                    FightBattleSkillDefinition ready;
                    try
                    {
                        ready = FightBattleTargeting.FindReadySkill(owner, units, round, excludedSlots: excluded,
                            globalRound: turns.GlobalRound(),
                            configuredCureAvailability: candidate => ConfiguredCureAvailability(owner, candidate),
                            ignoreCooldown: true);
                    }
                    finally { owner.SkillState.Round = round; owner.SkillState.IsDriving = driving; }
                    if (ready == null) continue;
                    var targets = FightBattleTargeting.SelectInSkillRange(owner, units, ready, random: random);
                    if (targets.Count == 0 && ready.TargetType != 5
                        && !FightBattleTargeting.HasCasterAreaTargets(owner, units, ready)) continue;
                    var prepared = SkillCasts.Prepare(owner, ready, targets, null);
                    var skill = prepared.Skill; var cast = prepared.Cast;
                    int castStart = context.Casts.Count, damageStart = context.DamageEvents.Count;
                    int movementStart = context.Movements.Count;
                    float startOffset = context.Duration, previousActionDuration = context.ActionDuration;
                    context.Duration = 0f; context.ActionDuration = -1f;
                    new FightSkillState(owner.SkillState).RunSkill(skill.Slot);
                    ExecuteSkill(owner, skill, targets, context.Casts, context.DamageEvents,
                        context.Movements, out float lastDelay, out float startDuration,
                        out float startActionDuration, presentationCast: cast);
                    checkpoint.Statistics.RecordCast(owner.Id, skill.EntityId, skill.ItemId);
                    CompleteCastPresentation(cast, skill, lastDelay);
                    context.Casts.Add(cast);
                    float cooldownDuration = 0f, cooldownActionDuration = -1f;
                    ExecutePendingCooldownModifySkills(context.Casts, context.DamageEvents,
                        context.Movements, ref cooldownDuration, ref cooldownActionDuration);
                    CollectStatusCustomActions(currentStepStatusCustomActions);
                    float duration = Math.Max(context.Duration, Math.Max(cooldownDuration,
                        Math.Max(ResolveDuration(skill, lastDelay), startDuration)));
                    float actionDuration = Math.Max(context.ActionDuration, Math.Max(cooldownActionDuration,
                        Math.Max(ResolveActionDuration(skill, lastDelay), startActionDuration)));
                    OffsetSkillEvents(context.Casts, castStart, context.DamageEvents, damageStart,
                        context.Movements, movementStart, startOffset);
                    context.ActionDuration = previousActionDuration;
                    if (actionDuration >= 0f)
                        context.ActionDuration = Math.Max(context.ActionDuration, startOffset + actionDuration);
                    context.Duration = startOffset + duration;
                    if (firstOwner == null) firstOwner = owner;
                }
            }
        }
    }
}
