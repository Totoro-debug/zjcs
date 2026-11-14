using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    // Scene-private scheduling; the native graph clock owns the same boundaries
    // as hit arrival. The executor's settled roster is never changed here.
    internal sealed class OfficialBattleUnitSpawnPlayback
    {
        private sealed class Pending
        {
            public FightBattleHitSelectionSnapshot.SpawnedUnit Receipt;
            public float Remaining;
        }
        private readonly List<Pending> pending = new List<Pending>();
        private readonly HashSet<int> seen = new HashSet<int>();
        private readonly Action<FightBattleHitSelectionSnapshot.SpawnedUnit> create;
        private readonly Func<int, bool> present;

        internal OfficialBattleUnitSpawnPlayback(Action<FightBattleHitSelectionSnapshot.SpawnedUnit> create,
            Func<int, bool> present)
        {
            this.create = create ?? throw new ArgumentNullException(nameof(create));
            this.present = present ?? throw new ArgumentNullException(nameof(present));
        }
        internal bool IsPending(int id) => pending.Exists(value => value.Receipt.UnitId == id);
        internal float DelayFor(int id) => Math.Max(0f, pending.Find(value => value.Receipt.UnitId == id)?.Remaining ?? 0f);
        internal float NextDelay => pending.Count == 0 ? float.PositiveInfinity : Math.Max(0f, pending[0].Remaining);

        internal void Enqueue(FightBattleStepEvent step)
        {
            if (step?.SkillCasts == null) return;
            foreach (var cast in step.SkillCasts)
            {
                if (cast?.HitSelections == null) continue;
                foreach (var hit in cast.HitSelections)
                {
                    if (hit?.SpawnedUnits == null) continue;
                    foreach (var receipt in hit.SpawnedUnits)
                    {
                        if (receipt == null || seen.Contains(receipt.UnitId)) continue;
                        float delay = cast.Delay + receipt.Delay;
                        if (float.IsNaN(delay) || float.IsInfinity(delay))
                            throw new InvalidOperationException("Summon appearance time must be finite.");
                        seen.Add(receipt.UnitId);
                        if (present(receipt.UnitId)) continue;
                        var copy = new FightBattleHitSelectionSnapshot.SpawnedUnit {
                            UnitId = receipt.UnitId, X = receipt.X, Y = receipt.Y,
                            EffectCenterIndex = receipt.EffectCenterIndex, ScopeIndex = receipt.ScopeIndex,
                            Delay = receipt.Delay
                        };
                        var value = new Pending { Receipt = copy, Remaining = Math.Max(0f, delay) };
                        int index = pending.FindIndex(other => other.Remaining > value.Remaining);
                        if (index < 0) pending.Add(value); else pending.Insert(index, value);
                    }
                }
            }
        }
        internal void AdvanceTimers(float elapsed)
        {
            if (float.IsNaN(elapsed) || float.IsInfinity(elapsed) || elapsed < 0f)
                throw new ArgumentOutOfRangeException(nameof(elapsed));
            foreach (var value in pending) value.Remaining -= elapsed;
        }
        internal void DispatchDue()
        {
            while (pending.Count > 0 && pending[0].Remaining <= 0f)
            {
                var value = pending[0]; pending.RemoveAt(0);
                if (!present(value.Receipt.UnitId)) create(value.Receipt);
            }
        }
        internal void Clear() { pending.Clear(); seen.Clear(); }
    }
}
