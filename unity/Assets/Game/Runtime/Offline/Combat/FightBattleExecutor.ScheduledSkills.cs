using System;
using System.Collections.Generic;
using System.Linq;
namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private const int ChaosInvasionBossEntityId = 3803;
        private const int ChaosInvasionStageId = 4800;
        private const int ChaosInvasionStageType = 14;
        private const int ChaosInvasionPhaseTwoSkillEntityId = 18130;
        private const int ChaosInvasionPhaseTwoRound = 20;

        private bool IsChaosInvasionPhaseSkill(FightBattleUnitState actor,
            FightBattleSkillDefinition skill)
        {
            return checkpoint.StageId == ChaosInvasionStageId
                && checkpoint.StageType == ChaosInvasionStageType
                && turns.GlobalRound() == ChaosInvasionPhaseTwoRound
                && actor != null
                && (actor.MonsterId == ChaosInvasionBossEntityId
                    || actor.MonsterTemplateId == ChaosInvasionBossEntityId)
                && skill != null && skill.EntityId == ChaosInvasionPhaseTwoSkillEntityId;
        }

        private List<FightBattleUnitState> ChaosInvasionPlayerTargets(FightBattleUnitState actor)
        {
            return units.Where(unit => unit != null && unit.Side != actor.Side
                && unit.CharacterType == 1 && unit.Hp > 0 && !unit.Ended)
                .OrderBy(unit => unit.Id).ToList();
        }

        private int PrepareChaosInvasionPhaseSkill(FightBattleUnitState actor,
            int globalRound, FightSkillState skillState)
        {
            if (checkpoint.StageId != ChaosInvasionStageId
                || checkpoint.StageType != ChaosInvasionStageType
                || actor == null || globalRound != ChaosInvasionPhaseTwoRound
                || actor.MonsterId != ChaosInvasionBossEntityId
                    && actor.MonsterTemplateId != ChaosInvasionBossEntityId) return -1;
            if (statusCatalog == null || actor.SkillState == null)
                throw new InvalidOperationException("Chaos Invasion 3803 requires the official skill catalog and state.");

            actor.Skills ??= new List<FightBattleSkillDefinition>();
            actor.SkillState.Slots ??= new List<FightSkillSlotState>();
            FightBattleSkillDefinition skill = actor.Skills.FirstOrDefault(value =>
                value != null && value.EntityId == ChaosInvasionPhaseTwoSkillEntityId);
            if (skill != null)
            {
                FightSkillSlotState existingSlot = actor.SkillState.Slots.FirstOrDefault(value =>
                    value != null && value.Slot == skill.Slot);
                if (existingSlot == null)
                    throw new InvalidOperationException("Chaos Invasion 18130 has no saved skill slot.");
                skill.LimitedTimes = 1;
                skill.ResetReleasedTimeOnNextWave = false;
                existingSlot.LimitedTimes = 1;
                existingSlot.ResetCooldownAtStart = false;
                return skill.Slot;
            }

            int slot = -1;
            foreach (FightSkillSlotState existing in actor.SkillState.Slots)
                if (existing != null) slot = Math.Max(slot, existing.Slot);
            if (slot == int.MaxValue) throw new InvalidOperationException("No free skill slot remains for Chaos Invasion 18130.");
            slot++;

            const int itemId = 14234;
            const int rank = 6;
            int level = Math.Max(1, actor.Level);
            int subRank = Math.Max(1, actor.SubRank);
            skill = statusCatalog.CreateSkill(ChaosInvasionPhaseTwoSkillEntityId, slot, itemId,
                rank, level, subRank, ToPropertyList(statusCatalog.CalculateSkillProps(
                    ChaosInvasionPhaseTwoSkillEntityId, rank, level, subRank)));
            if (skill.LimitedTimes != 1)
                throw new InvalidOperationException("Official Chaos Invasion 18130 must remain once per battle.");
            skill.ResetReleasedTimeOnNextWave = false;
            actor.Skills.Add(skill);
            actor.SkillState.Slots.Add(statusCatalog.CreateSkillSlot(skill, 0,
                lastRound: skillState.State.Round));
            return slot;
        }

        private static void CompleteChaosInvasionPhaseSkillAttempt(FightBattleUnitState actor,
            int slot, FightSkillState skillState)
        {
            if (slot < 0) return;
            FightSkillSlotState state = actor.SkillState.Slots.FirstOrDefault(value =>
                value != null && value.Slot == slot);
            if (state == null) throw new InvalidOperationException("Chaos Invasion 18130 skill slot was lost during its turn.");
            if (state.ReleasedTimes == 0) skillState.RunSkill(slot);
        }
    }
}
