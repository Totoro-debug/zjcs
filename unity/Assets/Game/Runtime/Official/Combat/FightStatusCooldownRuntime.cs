using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public static class FightStatusCooldownRuntime
    {
        public static void Initialize(FightBattleUnitState unit, FightBattleStatusState status,
            FightRandomStream random)
        {
            if (status == null || status.SkillCooldownModifyInitialized
                || (!status.HasSkillCooldownModify && !status.HasPermanentSkillCooldownReduction)) return;
            if (unit == null || unit.SkillState == null) return;
            ApplyPermanentReduction(unit, status);
            if (status.HasSkillCooldownModify)
            {
                ApplySkillCooldownModify(new FightSkillState(unit.SkillState), status, random);
                status.SkillCooldownModifySkillsPending = HasTriggerSkills(status);
                status.SkillCooldownActionPendingCount++;
            }
            status.SkillCooldownModifyInitialized = true;
        }

        public static void Invoke(FightBattleUnitState unit, FightBattleStatusState status,
            FightRandomStream random)
        {
            if (unit == null || status == null || unit.SkillState == null) return;
            ApplyPermanentReduction(unit, status);
            if (status.HasSkillCooldownModify)
            {
                status.SkillCooldownModifyInitialized = true;
                status.SkillCooldownModifyPending = true;
                status.SkillCooldownModifySkillsPending = HasTriggerSkills(status);
                status.SkillCooldownActionPendingCount++;
            }
        }

        public static void ProcessPending(FightBattleUnitState unit, FightBattleStatusState status,
            FightRandomStream random)
        {
            if (unit == null || status == null || !status.SkillCooldownModifyPending) return;
            ApplySkillCooldownModify(new FightSkillState(unit.SkillState), status, random);
            status.SkillCooldownModifyPending = false;
        }

        private static void ApplyPermanentReduction(FightBattleUnitState unit,
            FightBattleStatusState status)
        {
            if (!status.HasPermanentSkillCooldownReduction
                || status.PermanentSkillCooldownReductionApplied) return;
            int reduction = Math.Max(0, status.PermanentSkillCooldownReduction);
            var skillState = new FightSkillState(unit.SkillState);
            foreach (FightSkillSlotState slot in unit.SkillState.Slots)
                if (slot != null) skillState.PermanentlyReduceCooldown(slot.Slot, reduction);
            status.PermanentSkillCooldownReductionApplied = true;
        }

        private static bool HasTriggerSkills(FightBattleStatusState status)
        {
            return status.SkillCooldownModifySkills != null
                && status.SkillCooldownModifySkills.Count > 0;
        }

        private static void ApplySkillCooldownModify(FightSkillState skillState,
            FightBattleStatusState status, FightRandomStream random)
        {
            if (status.SkillCooldownModify == 0) return;
            FightSkillAgentState state = skillState.State;
            int round = unchecked(state.Round + (state.IsDriving ? 0 : 1));
            var eligible = new List<FightSkillSlotState>();
            foreach (FightSkillSlotState slot in state.Slots)
            {
                if (slot == null) continue;
                // The official tooltip uses this comparison to identify a skill still in CD.
                long elapsed = (long)round - slot.LastRound + status.SkillCooldownModifyOffset;
                if (elapsed <= slot.Cooldown) eligible.Add(slot);
            }

            int count = status.SkillCooldownModifyCount < 0
                ? eligible.Count
                : Math.Min(status.SkillCooldownModifyCount, eligible.Count);
            for (int i = 0; i < count; i++)
            {
                int index = status.SkillCooldownModifyIsRandom && random != null
                    ? Math.Min(eligible.Count - 1, (int)(random.Next() * eligible.Count))
                    : 0;
                FightSkillSlotState selected = eligible[index];
                skillState.ChangeCooldown(selected.Slot, status.SkillCooldownModify);
                eligible.RemoveAt(index);
            }
        }
    }
}
