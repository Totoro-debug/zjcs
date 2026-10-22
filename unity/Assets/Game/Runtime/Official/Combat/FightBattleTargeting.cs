using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public static class FightBattleTargeting
    {
        /// <summary>
        /// A unit can be chosen, occupied and hit only while it is alive, targetable and actually
        /// present in the battle. Units of later waves stay <see cref="FightBattleUnitState.Ended"/>
        /// until <c>FightBattleExecutor</c> spawns their wave, so they must not leak into targeting,
        /// hit collection or movement occupancy.
        /// </summary>
        public static bool IsSelectable(FightBattleUnitState unit)
        {
            return unit != null && unit.Hp > 0 && unit.Targetable && !unit.Ended;
        }

        public static List<FightBattleUnitState> Select(FightBattleUnitState actor,
            IReadOnlyList<FightBattleUnitState> units, int targetType, int ridiculeTargetId = 0,
            bool confused = false, bool releaseWhileConfused = false,
            FightBattleSkillDefinition skill = null)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (units == null) throw new ArgumentNullException(nameof(units));
            var candidates = new List<FightBattleUnitState>();
            foreach (FightBattleUnitState unit in units)
            {
                if (!IsSelectable(unit)) continue;
                if (targetType == 1 && ridiculeTargetId > 0 && unit.Id != ridiculeTargetId) continue;
                if (targetType == 1 && confused && !releaseWhileConfused && unit.Id != actor.Id)
                {
                    candidates.Add(unit);
                    continue;
                }

                bool sameSide = unit.Side == actor.Side;
                switch (targetType)
                {
                    case 0: if (unit.Id == actor.Id) candidates.Add(unit); break;
                    case 1: if (!sameSide) candidates.Add(unit); break;
                    case 2: if (sameSide) candidates.Add(unit); break;
                    case 3: if (sameSide && unit.Id != actor.Id) candidates.Add(unit); break;
                    case 8: if (sameSide && unit.Id != actor.Id && unit.CharacterType == 1) candidates.Add(unit); break;
                    case 9: if (sameSide && (unit.CharacterType == 1 || unit.CharacterType == 4)) candidates.Add(unit); break;
                    case 4: candidates.Add(unit); break;
                    case 6: if (unit.Id == actor.MasterId) candidates.Add(unit); break;
                    case 7: if (unit.MasterId == actor.Id) candidates.Add(unit); break;
                }
            }

            candidates.Sort((left, right) => CompareCandidates(actor, left, right, skill));
            if (targetType == 1 || targetType == 2 || targetType == 3 || targetType == 8 || targetType == 9)
                if (candidates.Count > 1) candidates.RemoveRange(1, candidates.Count - 1);
            return candidates;
        }

        public static FightBattleSkillDefinition FindReadySkill(FightBattleUnitState actor,
            IReadOnlyList<FightBattleUnitState> units, int round, bool driving = true,
            ISet<int> excludedSlots = null)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (units == null) throw new ArgumentNullException(nameof(units));
            if (actor.SkillState == null || actor.Skills == null) return null;
            var state = new FightSkillState(actor.SkillState);
            state.BeginRound(round);
            foreach (FightSkillSlotState slotState in actor.SkillState.Slots)
            {
                if (slotState == null || excludedSlots != null && excludedSlots.Contains(slotState.Slot)
                    || !state.CanRelease(slotState.Slot)
                    || state.LeftCooldown(slotState.Slot) > 0) continue;
                FightBattleSkillDefinition skill = FindSkill(actor.Skills, slotState.Slot);
                if (skill == null || !skill.RangeVerified) continue;
                List<FightBattleUnitState> targets = Select(actor, units, skill.TargetType,
                    skill: skill);
                if (targets.Count == 0 && skill.TargetType != 5) continue;
                if (skill.SkillType == 3)
                {
                    bool needsHealing = false;
                    foreach (FightBattleUnitState target in targets)
                        if (target.Hp < target.MaxHp) { needsHealing = true; break; }
                    if (!needsHealing) continue;
                }
                return skill;
            }
            return null;
        }

        public static List<FightBattleUnitState> SelectInSkillRange(FightBattleUnitState actor,
            IReadOnlyList<FightBattleUnitState> units, FightBattleSkillDefinition skill,
            int ridiculeTargetId = 0, bool confused = false, bool releaseWhileConfused = false)
        {
            List<FightBattleUnitState> candidates = Select(actor, units, skill.TargetType,
                ridiculeTargetId, confused, releaseWhileConfused, skill);
            if (skill.Range == null || skill.Range.Count == 0) return candidates;

            var inRange = new List<FightBattleUnitState>();
            for (int i = 0; i < candidates.Count; i++)
            {
                FightBattleUnitState target = candidates[i];
                int dx = target.X - actor.X;
                int dy = target.Y - actor.Y;
                for (int rangeIndex = 0; rangeIndex < skill.Range.Count; rangeIndex++)
                    if (skill.Range[rangeIndex].X == dx && skill.Range[rangeIndex].Y == dy)
                    {
                        inRange.Add(target);
                        break;
                    }
            }
            return inRange;
        }

        public static List<FightBattleUnitState> SelectHitTargets(IReadOnlyList<FightBattleUnitState> candidates,
            int targetMode, int randomCount, FightRandomStream random)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            var available = new List<FightBattleUnitState>();
            foreach (FightBattleUnitState candidate in candidates)
                if (IsSelectable(candidate)) available.Add(candidate);
            if (available.Count == 0) return available;
            if (targetMode == 1) return available;
            if (targetMode != 2) return new List<FightBattleUnitState> { available[0] };
            if (random == null) throw new ArgumentNullException(nameof(random));

            int count = Math.Min(Math.Max(1, randomCount), available.Count);
            var selected = new List<FightBattleUnitState>(count);
            for (int i = 0; i < count; i++)
            {
                int index = Math.Min((int)(random.Next() * available.Count), available.Count - 1);
                selected.Add(available[index]);
                available.RemoveAt(index);
            }
            return selected;
        }

        /// <summary>
        /// Official <c>FightSkillComponentInfo.Range</c> is ordered from near to far and targeting
        /// depends on that order to prefer closer targets. An explicit AI priority property still
        /// wins; in-range candidates are preferred over out-of-range ones so movement planning can
        /// use them, and all other ties fall back to the existing comparison.
        /// </summary>
        private static int CompareCandidates(FightBattleUnitState actor,
            FightBattleUnitState left, FightBattleUnitState right, FightBattleSkillDefinition skill)
        {
            if (skill == null || skill.PriorityProperty > 0)
                return Compare(left, right, skill);
            if (skill.Range != null && skill.Range.Count > 0)
            {
                int leftIndex = RangeIndex(actor, left, skill.Range);
                int rightIndex = RangeIndex(actor, right, skill.Range);
                if (leftIndex >= 0 && rightIndex >= 0)
                {
                    if (leftIndex != rightIndex) return leftIndex.CompareTo(rightIndex);
                }
                else if (leftIndex >= 0) return -1;
                else if (rightIndex >= 0) return 1;
            }
            return Compare(left, right, skill);
        }

        private static int RangeIndex(FightBattleUnitState actor, FightBattleUnitState target,
            IReadOnlyList<FightGridPoint> range)
        {
            int dx = target.X - actor.X;
            int dy = target.Y - actor.Y;
            for (int i = 0; i < range.Count; i++)
                if (range[i].X == dx && range[i].Y == dy) return i;
            return -1;
        }

        private static int Compare(FightBattleUnitState left, FightBattleUnitState right,
            FightBattleSkillDefinition skill)
        {
            bool hasPriority = skill != null && skill.PriorityProperty > 0;
            if (hasPriority)
            {
                long leftProperty = Property(left, skill.PriorityProperty);
                long rightProperty = Property(right, skill.PriorityProperty);
                if (leftProperty != rightProperty)
                    return skill.PriorityFindMax
                        ? rightProperty.CompareTo(leftProperty)
                        : leftProperty.CompareTo(rightProperty);
                return left.Id.CompareTo(right.Id);
            }
            double leftHp = left.Hp / (double)Math.Max(1L, left.MaxHp);
            double rightHp = right.Hp / (double)Math.Max(1L, right.MaxHp);
            int hp = leftHp.CompareTo(rightHp);
            return hp != 0 ? hp : left.Id.CompareTo(right.Id);
        }

        private static long Property(FightBattleUnitState target, int propertyId)
        {
            if (target.Properties == null) return 0;
            for (int i = 0; i < target.Properties.Count; i++)
                if (target.Properties[i] != null && target.Properties[i].Id == propertyId)
                    return target.Properties[i].Value;
            return 0;
        }

        private static FightBattleSkillDefinition FindSkill(List<FightBattleSkillDefinition> skills, int slot)
        {
            for (int i = 0; i < skills.Count; i++)
                if (skills[i] != null && skills[i].Slot == slot) return skills[i];
            return null;
        }
    }
}
