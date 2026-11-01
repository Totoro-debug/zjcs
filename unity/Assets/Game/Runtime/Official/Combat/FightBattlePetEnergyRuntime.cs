using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    /// <summary>
    /// Offline execution of FightRolePetTransferEntity energy timing. The client only displays
    /// the server result, so the confirmed local rule is kept in this small state transition layer.
    /// </summary>
    public static class FightBattlePetEnergyRuntime
    {
        public static void Initialize(FightBattleUnitState unit)
        {
            FightBattlePetEnergyState state = unit?.PetEnergy;
            if (state == null || !state.Enabled || state.Initialized) return;
            state.CurrentEnergy = Math.Max(0d, state.InitialEnergy);
            state.OwnerDamageAccumulator = Math.Max(0d, state.OwnerDamageAccumulator);
            state.MasterLoseHpTriggerRecover = NormalizeRate(state.MasterLoseHpTriggerRecover);
            state.Initialized = true;
        }

        public static void RecoverAtPetRoundStart(FightBattleUnitState pet)
        {
            FightBattlePetEnergyState state = pet?.PetEnergy;
            if (state == null || !state.Enabled || state.NaturalRecoverEnergy <= 0d) return;
            state.CurrentEnergy += state.NaturalRecoverEnergy;
        }

        public static void RecordOwnerDamage(IReadOnlyList<FightBattleUnitState> units,
            int ownerId, long actualHpLoss)
        {
            if (units == null || ownerId <= 0 || actualHpLoss <= 0) return;
            foreach (FightBattleUnitState unit in units)
            {
                FightBattlePetEnergyState state = unit?.PetEnergy;
                if (state == null || !state.Enabled || state.OwnerUnitId != ownerId
                    || state.MasterLoseHpRecoverEnergy <= 0d) continue;
                double threshold = OwnerDamageThreshold(units, ownerId, state);
                if (threshold <= 0d) continue;
                state.OwnerDamageAccumulator += actualHpLoss;
                while (state.OwnerDamageAccumulator >= threshold)
                {
                    state.OwnerDamageAccumulator -= threshold;
                    state.CurrentEnergy += state.MasterLoseHpRecoverEnergy;
                }
            }
        }

        public static bool CanRelease(FightBattlePetEnergyState state)
        {
            return state != null && state.Enabled && state.SkillEntityId > 0
                && state.NeedEnergy > 0d && state.CurrentEnergy >= state.NeedEnergy;
        }

        public static bool ConsumeForRelease(FightBattlePetEnergyState state)
        {
            if (!CanRelease(state)) return false;
            state.CurrentEnergy -= state.NeedEnergy;
            if (state.CurrentEnergy < 0d) state.CurrentEnergy = 0d;
            return true;
        }

        private static double OwnerDamageThreshold(IReadOnlyList<FightBattleUnitState> units,
            int ownerId, FightBattlePetEnergyState state)
        {
            FightBattleUnitState owner = null;
            foreach (FightBattleUnitState unit in units)
                if (unit != null && unit.Id == ownerId) { owner = unit; break; }
            if (owner == null || owner.MaxHp <= 0) return 0d;
            return owner.MaxHp * NormalizeRate(state.MasterLoseHpTriggerRecover);
        }

        private static double NormalizeRate(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) || value <= 0d ? 0.01d : value;
        }
    }
}
