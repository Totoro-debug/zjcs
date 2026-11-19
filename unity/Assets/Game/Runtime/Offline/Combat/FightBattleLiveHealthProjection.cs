using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    /// <summary>Health shown during an isolated step; the committed checkpoint is never mutated.</summary>
    public sealed class FightBattleLiveHealthProjection
    {
        private readonly Dictionary<int, FightBattleUnitState> units = new Dictionary<int, FightBattleUnitState>();
        private readonly HashSet<FightBattleSkillCastEvent> costs = new HashSet<FightBattleSkillCastEvent>();
        private readonly HashSet<(int DamageId, int TargetId)> damages = new HashSet<(int, int)>();

        public FightBattleLiveHealthProjection(FightBattleCheckpoint checkpoint)
        {
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            if (checkpoint.Units != null) foreach (var unit in checkpoint.Units) Add(unit);
            if (checkpoint.GridItems != null) foreach (var item in checkpoint.GridItems)
                if (item != null && item.HasRuntime) Add(item.Runtime);
        }

        private void Add(FightBattleUnitState unit)
        {
            if (unit == null) return;
            if (unit.Id <= 0 || units.ContainsKey(unit.Id))
                throw new InvalidOperationException("The presentation snapshot has a duplicate or invalid unit.");
            units.Add(unit.Id, unit.WithPresentationHealth(unit.Hp, unit.MaxHp));
        }

        public void AddLiveUnit(FightBattleUnitState unit) => Add(unit);

        public FightBattleUnitState Find(int unitId) => units.TryGetValue(unitId, out var unit) ? unit : null;

        /// <summary>Applies only the actual cost captured when this cast was prepared.</summary>
        public bool TryApplyCost(FightBattleSkillCastEvent cast)
        {
            if (!CanApplyCost(cast)) return false;
            var unit = units[cast.ActorId];
            units[unit.Id] = unit.WithPresentationHealth(cast.SkillCostAfterHp, unit.MaxHp);
            costs.Add(cast);
            return true;
        }

        public bool CanApplyCost(FightBattleSkillCastEvent cast) => cast != null && cast.HasSkillCostHp
            && !costs.Contains(cast) && units.TryGetValue(cast.ActorId, out var unit)
            && unit.Hp == cast.SkillCostBeforeHp && cast.SkillCostAfterHp >= 0
            && cast.SkillCostAfterHp <= unit.MaxHp;

        /// <summary>Applies one actual damage or healing receipt in its observed order.</summary>
        public bool TryApplyDamage(FightBattleDamageEvent damage)
        {
            if (!CanApplyDamage(damage)) return false;
            var unit = units[damage.TargetId];
            units[unit.Id] = unit.WithPresentationHealth(damage.AfterHp, damage.MaxHp);
            damages.Add((damage.DamageId, damage.TargetId));
            return true;
        }

        public bool CanApplyDamage(FightBattleDamageEvent damage) => damage != null && !damage.IsSkillCost
            && damage.HasDamageValue && damage.DamageId > 0 && units.TryGetValue(damage.TargetId, out var unit)
            && unit.Hp == damage.BeforeHp && damage.MaxHp >= 0 && damage.AfterHp >= 0
            && damage.AfterHp <= damage.MaxHp && !damages.Contains((damage.DamageId, damage.TargetId));
    }
}
