using System;

namespace Zjcs.Combat.Official
{
    public sealed class FightEntityState
    {
        public int Id { get; }
        public long Hp { get; private set; }
        public long MaxHp { get; }

        public FightEntityState(int id, long hp, long maxHp)
        {
            if (maxHp < 0)
                throw new ArgumentOutOfRangeException(nameof(maxHp));

            Id = id;
            MaxHp = maxHp;
            Hp = hp;
        }

        internal void SetHp(long value)
        {
            Hp = value;
        }
    }

    public sealed class FightDamageEvent
    {
        public int DamageId { get; }
        public int SourceId { get; }
        public int TargetId { get; }
        public long RealDamageValue { get; }
        public long DamageValue { get; }
        public bool KilledTarget { get; }

        internal FightDamageEvent(int damageId, int sourceId, int targetId, long realDamageValue,
            long damageValue, bool killedTarget)
        {
            DamageId = damageId;
            SourceId = sourceId;
            TargetId = targetId;
            RealDamageValue = realDamageValue;
            DamageValue = damageValue;
            KilledTarget = killedTarget;
        }
    }

    public sealed class FightDamageSettlement
    {
        public event Action<FightDamageEvent> TargetDamaged;
        public event Action<FightDamageEvent> SourceHit;
        public event Action<FightDamageEvent> BattleDamaged;

        public FightDamageEvent Apply(int damageId, FightEntityState source, FightEntityState target,
            long damageValue)
            => ApplyCore(damageId, source, target, damageValue, false);

        // Offline damage testing records the full hit while preserving monster HP.
        internal FightDamageEvent ApplyPreservingHp(int damageId, FightEntityState source,
            FightEntityState target, long damageValue) => ApplyCore(damageId, source, target, damageValue, true);

        private FightDamageEvent ApplyCore(int damageId, FightEntityState source, FightEntityState target,
            long damageValue, bool preserveHp)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            // Mirrors FightDamageComponentImplement.CalcDamageValue and Damage.
            long previousHp = target.Hp;
            long nextHp = previousHp;
            if (damageValue < 0)
                nextHp = Math.Max(0L, previousHp + damageValue);
            else if (damageValue > 0)
                nextHp = Math.Min(target.MaxHp, previousHp + damageValue);

            long realDamage = preserveHp ? damageValue : nextHp - previousHp;
            if (preserveHp) nextHp = previousHp;
            bool killed = previousHp > 0 && nextHp <= 0;
            target.SetHp(nextHp);

            var result = new FightDamageEvent(damageId, source?.Id ?? 0, target.Id,
                realDamage, damageValue, killed);

            // Preserve FireEvents ordering from the official implementation.
            TargetDamaged?.Invoke(result);
            if (source != null)
                SourceHit?.Invoke(result);
            BattleDamaged?.Invoke(result);
            return result;
        }
    }
}
