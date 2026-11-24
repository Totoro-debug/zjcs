using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        // These descriptions require successive retries against the surviving original
        // target. Other ExtSkill components retain their independently migrated rules.
        private static bool HasSequentialExtraSkills(FightBattleSkillDefinition skill)
            => skill != null && (skill.EntityId == 12090 || skill.EntityId == 13590);

        private void ApplySequentialExtraSkills(FightBattleUnitState actor,
            FightBattleSkillDefinition root, List<FightBattleUnitState> originalTargets,
            List<FightBattleSkillCastEvent> casts, List<FightBattleDamageEvent> damageEvents,
            List<FightBattleMovementEvent> movements, float lastDelay,
            ref float maxDuration, ref float maxActionDuration)
        {
            if (!HasSequentialExtraSkills(root)) return;
            if (statusCatalog == null)
                throw new InvalidOperationException("An extra skill chain requires its official catalog.");
            if (actor.Hp <= 0 || actor.Ended) return;
            var primary = originalTargets.FirstOrDefault();
            bool wasExecutingReplays = executingSkillReplays;
            executingSkillReplays = true;
            try
            {
                // Queue the root's requests before those produced by its extra segments.
                // The confirmed offline order drains them only after this chain finishes.
                CollectDirectSkillReplay(actor, root);
                CollectStatusSkillReplays(actor, root);
                if (primary == null) return;
                var counter = ExtraSkillCounter(actor, root);
                // FightSkillExtSkillComponentInfo: trigger at the skill's Action end.
                float start = Math.Max(0f, ResolveActionDuration(root, lastDelay));
                foreach (var extra in root.ExtraSkills)
                {
                    if (counter.Count >= root.ExtraSkills.Count) break;
                    if (actor.Hp <= 0 || actor.Ended || !FightBattleTargeting.IsSelectable(primary)) break;
                    var next = CreateComboSegment(actor, root, extra.SkillId);
                    if (!ComboTargetAllowed(actor, next, primary)) break;
                    if (extra.Rate <= 0f || extra.Rate < 1f && random.Next() > extra.Rate) break;
                    if (!TryComboTargetPosition(actor, next, primary, out var position))
                        throw new InvalidOperationException("An extra skill lost its original target body anchor.");
                    var targets = new List<FightBattleUnitState> { primary };
                    counter.Count = checked(counter.Count + 1);
                    ApplyComboFacing(actor, next);
                    var prepared = SkillCasts.Prepare(actor, next, targets, position);
                    next = prepared.Skill;
                    var cast = prepared.Cast;
                    int castStart = casts.Count, damageStart = damageEvents.Count, movementStart = movements.Count;
                    casts.Add(cast);
                    ExecuteSkill(actor, next, targets, casts, damageEvents, movements,
                        out float delay, out float nestedDuration, out float nestedActionDuration,
                        targetPosition: position, presentationCast: cast);
                    CompleteCastPresentation(cast, next, delay);
                    OffsetSkillEvents(casts, castStart, damageEvents, damageStart, movements, movementStart, start);
                    checkpoint.Statistics.RecordCast(actor.Id, next.EntityId, next.ItemId);
                    maxDuration = Math.Max(maxDuration, start + Math.Max(ResolveDuration(next, delay), nestedDuration));
                    float action = ResolveActionDuration(next, delay);
                    maxActionDuration = Math.Max(maxActionDuration, start + Math.Max(action, nestedActionDuration));
                    start += Math.Max(0f, action);
                }
            }
            finally { executingSkillReplays = wasExecutingReplays; }
        }

        private static FightSkillExtraCounter ExtraSkillCounter(FightBattleUnitState actor,
            FightBattleSkillDefinition root)
        {
            var state = actor.SkillState;
            if (state.ExtraCounts == null) state.ExtraCounts = new List<FightSkillExtraCounter>();
            state.ExtraCounts.RemoveAll(value => value.Round != state.Round);
            // Appearance and source-item attribution do not grant another allowance.
            int entity = root.EntityId == 13590 ? 12090 : root.EntityId;
            var counter = state.ExtraCounts.Find(value => value.EntityId == entity);
            if (counter == null)
            {
                counter = new FightSkillExtraCounter { Round = state.Round, EntityId = entity };
                state.ExtraCounts.Add(counter);
            }
            return counter;
        }
    }
}
