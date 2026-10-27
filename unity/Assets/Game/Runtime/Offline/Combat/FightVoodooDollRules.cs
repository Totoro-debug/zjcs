using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightVoodooDollLink
    {
        public int TargetId;
        public int SyncSkillEntityId;
        public int VoodooStatusId;
        public int FollowDieStatusId;
        public int VoodooStatusInstanceId;
        public int SyncHitEntityId;
        public int SyncDamageEntityId;
        public bool DebuffsTransferred;
    }

    /// <summary>User-confirmed offline substitute rules; component IDs come from official data.</summary>
    public static class FightVoodooDollRules
    {
        public static FightBattleDamageEvent Transmit(FightBattleUnitState doll,
            FightBattleUnitState target, FightBattleDamageEvent suffered, int damageId)
        {
            if (doll?.VoodooDoll == null || target == null || target.Hp <= 0 || target.Ended
                || suffered == null || suffered.IsCure || !suffered.HasDamageValue
                || suffered.DamageValue >= 0 || suffered.TargetId != doll.Id
                || doll.VoodooDoll.TargetId != target.Id) return null;
            // Use the full settled input, including overkill; do not use clamped RealDamageValue.
            // This explicitly bypasses shields, affinity, immunity and all damage multipliers.
            long amount = suffered.DamageValue == long.MinValue ? long.MaxValue : -suffered.DamageValue;
            long before = target.Hp;
            target.Hp = amount >= before ? 0 : before - amount;
            return new FightBattleDamageEvent {
                DamageId = damageId, SourceId = doll.Id, TargetId = target.Id,
                SkillEntityId = doll.VoodooDoll.SyncSkillEntityId,
                HitEntityId = doll.VoodooDoll.SyncHitEntityId,
                DamageEntityId = doll.VoodooDoll.SyncDamageEntityId,
                DamageType = 8, TrueDamage = true, IgnoreShield = true,
                HasDamageValue = true, DamageValue = -amount, Damage = target.Hp - before,
                HasAbsoluteDamage = true, AbsoluteDamage = amount,
                BeforeHp = before, AfterHp = target.Hp, MaxHp = target.MaxHp,
                Delay = suffered.Delay
            };
        }

        public static int TransferDebuffs(FightBattleUnitState doll, FightBattleUnitState target,
            IReadOnlyList<FightBattleUnitState> units, Func<int> nextInstanceId,
            FightRandomStream random = null)
        {
            if (doll?.VoodooDoll == null || doll.VoodooDoll.DebuffsTransferred) return 0;
            doll.VoodooDoll.DebuffsTransferred = true;
            if (target == null || target.Hp <= 0 || target.Ended) return 0;
            int count = 0;
            foreach (FightBattleStatusState status in new List<FightBattleStatusState>(doll.Statuses))
            {
                if (status == null || status.StatusType < 2 || status.StatusType > 3) continue;
                int remaining = status.StartRound < 0 ? status.DurationRound
                    : FightStatusLifecycle.IconRemainingRound(status, units);
                if (remaining == 0) continue;
                // Resolve the destination clock after changing the owner. Creator/master clocks
                // retain their original identity; applicator clocks switch to the linked target.
                int oldOwner = status.OwnerId;
                status.OwnerId = target.Id;
                int clockId = FightStatusLifecycle.ResolveTargetUnitId(status, units);
                status.OwnerId = oldOwner;
                int round = 0;
                if (units != null)
                    foreach (FightBattleUnitState unit in units)
                        if (unit.Id == clockId) { round = unit.SkillState.Round; break; }
                int instanceId = nextInstanceId();
                FightBattleStatusState applied = FightStatusLifecycle.Apply(target, status, round,
                    instanceId, random);
                doll.Statuses.Remove(status);
                if (applied == null) continue;
                if (applied.InstanceId == instanceId)
                {
                    // Move the existing state rather than reconstructing its counters/snapshots.
                    // Apply above owns official parallel/replace/discard/stack behavior and events.
                    int index = target.Statuses.IndexOf(applied);
                    status.OwnerId = target.Id;
                    status.InstanceId = instanceId;
                    status.StartRound = round;
                    status.CurDurationRound = remaining;
                    target.Statuses[index] = status;
                }
                count++;
            }
            return count;
        }
    }
}
