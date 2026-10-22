using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightSkillSlotState
    {
        public int Slot;
        public long Cooldown;
        public int LastRound;
        public bool HasInitializedLastRound;
        public int ReleasedTimes;
        public int LimitedTimes = -1;
        public bool ResetCooldownAtStart;
    }

    [Serializable]
    public sealed class FightSkillAgentState
    {
        public int Round;
        public bool IsDriving;
        public List<FightSkillSlotState> Slots = new List<FightSkillSlotState>();
    }

    public sealed class FightSkillState
    {
        private readonly FightSkillAgentState state;

        public FightSkillState(FightSkillAgentState state)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            if (state.Slots == null) state.Slots = new List<FightSkillSlotState>();
            state.Slots.Sort((left, right) => left.Slot.CompareTo(right.Slot));
        }

        public FightSkillAgentState State => state;

        public void Initialize(int cooldownChangeAtStart)
        {
            foreach (FightSkillSlotState skill in state.Slots)
            {
                if (skill == null || skill.HasInitializedLastRound) continue;
                int change = skill.ResetCooldownAtStart ? Int32(-Int32((int)skill.Cooldown)) : 0;
                change = Int32(change + cooldownChangeAtStart);
                SetLastRound(skill, Int32(skill.LastRound + change));
            }
        }

        public void BeginRound(int round)
        {
            int value = Int32(round);
            if (state.Round == value) return;
            state.Round = value;
            state.IsDriving = true;
        }

        public void FinishRound()
        {
            state.IsDriving = false;
        }

        public bool CanRelease(int slot)
        {
            FightSkillSlotState skill = Find(slot);
            return skill != null && (skill.LimitedTimes < 0 || skill.ReleasedTimes < skill.LimitedTimes);
        }

        public long LeftCooldown(int slot)
        {
            FightSkillSlotState skill = Find(slot);
            if (skill == null) return 0;
            int current = Int32(state.Round + (state.IsDriving ? 0 : 1));
            int elapsed = Int32(current - skill.LastRound);
            return Math.Min(skill.Cooldown, skill.Cooldown - elapsed + 1L);
        }

        public void RunSkill(int slot)
        {
            FightSkillSlotState skill = FindRequired(slot);
            skill.ReleasedTimes = Int32(skill.ReleasedTimes + 1);
            SetLastRound(skill, state.Round);
        }

        public bool ChangeCooldown(int slot, int change)
        {
            FightSkillSlotState skill = Find(slot);
            if (change == 0 || skill == null) return false;
            SetLastRound(skill, Int32(skill.LastRound + change));
            return true;
        }

        public List<int> ResetAllCooldowns(bool restart)
        {
            var changed = new List<int>();
            foreach (FightSkillSlotState skill in state.Slots)
            {
                if (skill == null) continue;
                int offset = restart && skill.ResetCooldownAtStart
                    ? Int32(-Int32((int)skill.Cooldown))
                    : 0;
                SetLastRound(skill, Int32(state.Round + offset));
                changed.Add(skill.Slot);
            }
            return changed;
        }

        public bool Replay(int slot, bool present)
        {
            FightSkillSlotState skill = Find(slot);
            if (slot < 0 || !present || skill == null) return false;
            int offset = Int32(-Int32((int)skill.Cooldown) - 1);
            SetLastRound(skill, Int32(state.Round + offset));
            return true;
        }

        public void StopCooldownForRound()
        {
            foreach (FightSkillSlotState skill in state.Slots)
                if (skill != null) SetLastRound(skill, Int32(skill.LastRound + 1));
        }

        public bool PermanentlyReduceCooldown(int slot, int reduction)
        {
            FightSkillSlotState skill = Find(slot);
            if (skill == null) return false;
            skill.Cooldown = Math.Max(0L, skill.Cooldown - reduction);
            return true;
        }

        public bool ResetCooldown(int slot, long configuredCooldown)
        {
            FightSkillSlotState skill = Find(slot);
            if (skill == null) return false;
            skill.Cooldown = configuredCooldown;
            return true;
        }

        public bool ResetReleasedTimes(int slot)
        {
            FightSkillSlotState skill = Find(slot);
            if (skill == null) return false;
            skill.ReleasedTimes = 0;
            return true;
        }

        public bool RemoveSlot(int slot)
        {
            for (int i = 0; i < state.Slots.Count; i++)
                if (state.Slots[i] != null && state.Slots[i].Slot == slot)
                {
                    state.Slots.RemoveAt(i);
                    return true;
                }
            return false;
        }

        private FightSkillSlotState Find(int slot)
        {
            for (int i = 0; i < state.Slots.Count; i++)
                if (state.Slots[i] != null && state.Slots[i].Slot == slot) return state.Slots[i];
            return null;
        }

        private FightSkillSlotState FindRequired(int slot)
        {
            FightSkillSlotState skill = Find(slot);
            if (skill == null) throw new KeyNotFoundException("Fight skill slot is not equipped: " + slot);
            return skill;
        }

        private static void SetLastRound(FightSkillSlotState skill, int round)
        {
            skill.LastRound = round;
            skill.HasInitializedLastRound = true;
        }

        private static int Int32(int value)
        {
            return unchecked(value);
        }
    }
}
