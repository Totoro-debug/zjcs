using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private static void InitializePetSupportSkills(FightBattleUnitState owner)
        {
            if (owner?.PetEnergy?.SupportSkillState != null)
                new FightSkillState(owner.PetEnergy.SupportSkillState).Initialize(0);
        }

        // Confirmed single-player schedule: extra support at owner-round start.
        // Original skill definitions, targeting, CD and LimitedTimes remain shared.
        private void ExecutePetSupportSkills(FightBattleUnitState owner, int round,
            int globalRound, List<FightBattleSkillCastEvent> casts,
            List<FightBattleDamageEvent> damageEvents, List<FightBattleMovementEvent> movements,
            ref float stepDuration, ref float stepActionDuration, out float supportDuration)
        {
            supportDuration = 0f;
            FightBattlePetEnergyState pet = owner?.PetEnergy;
            if (!FightBattleTargeting.IsSelectable(owner) || pet?.SupportSkills == null
                || pet.SupportSkills.Count == 0) return;
            var support = new FightBattleUnitState
            {
                Id = owner.Id, MasterId = owner.Id, Side = owner.Side, CharacterType = 5,
                X = owner.X, Y = owner.Y, Hp = owner.Hp, MaxHp = owner.MaxHp,
                Properties = owner.Properties, Statuses = owner.Statuses,
                Skills = pet.SupportSkills, SkillState = pet.SupportSkillState
            };
            var state = new FightSkillState(pet.SupportSkillState);
            state.BeginRound(round);
            var checkedSlots = new HashSet<int>();
            try
            {
                while (owner.Hp > 0 && !owner.Ended)
                {
                    support.X = owner.X; support.Y = owner.Y;
                    support.Hp = owner.Hp; support.MaxHp = owner.MaxHp;
                    support.Properties = owner.Properties;
                    FightBattleSkillDefinition skill = FightBattleTargeting.FindReadySkill(
                        support, units, round, excludedSlots: checkedSlots, globalRound: globalRound);
                    if (skill == null) break;
                    checkedSlots.Add(skill.Slot);
                    List<FightBattleUnitState> targets = FightBattleTargeting.SelectInSkillRange(
                        support, units, skill, random: random);
                    // An out-of-range support neither moves the owner nor consumes CD/count.
                    if (targets.Count == 0 && skill.TargetType != 5
                        && !FightBattleTargeting.HasCasterAreaTargets(support, units, skill)) continue;
                    var cast = new FightBattleSkillCastEvent
                    {
                        ActorId = owner.Id, Slot = skill.Slot, IsPetSupport = true,
                        SkillEntityId = skill.EntityId, SkillItemId = skill.ItemId,
                        Presentation = FightBattleSkillCastPresentation.Capture(owner, skill, targets),
                        TargetIds = targets.ConvertAll(target => target.Id)
                    };
                    int castStart = casts.Count, damageStart = damageEvents.Count,
                        movementStart = movements.Count;
                    state.RunSkill(skill.Slot);
                    casts.Add(cast);
                    checkpoint.Statistics.RecordCast(owner.Id, skill.EntityId, skill.ItemId);
                    ExecuteSkill(owner, skill, targets, casts, damageEvents, movements,
                        out float delay, out float duration, out float actionDuration);
                    CompleteCastPresentation(cast, skill, delay);
                    OffsetSkillEvents(casts, castStart, damageEvents, damageStart,
                        movements, movementStart, supportDuration);
                    float lifetime = Math.Max(ResolveDuration(skill, delay), duration);
                    supportDuration += Math.Max(0f, lifetime);
                    stepDuration = Math.Max(stepDuration, supportDuration);
                    if (actionDuration >= 0f)
                        stepActionDuration = Math.Max(stepActionDuration,
                            cast.Delay + actionDuration);
                    ExecutePendingCooldownModifySkills(casts, damageEvents, movements,
                        ref stepDuration, ref stepActionDuration);
                }
            }
            finally { state.FinishRound(); }
        }
    }
}
