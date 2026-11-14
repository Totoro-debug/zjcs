using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    // Observe confirmed Die transitions, after same-hit revival. The settled
    // checkpoint remains authoritative and is never changed by this clock.
    internal sealed class OfficialBattleUnitLifePlayback
    {
        private sealed class Pending
        {
            public FightBattleUnitLifeEvent Event;
            public float Remaining;
        }
        private readonly List<Pending> pending = new List<Pending>();
        private readonly Dictionary<int, FightBattleUnitLifeEvent> projected = new Dictionary<int, FightBattleUnitLifeEvent>();
        private readonly HashSet<long> seen = new HashSet<long>();
        private readonly Action<FightBattleUnitLifeEvent> apply;

        internal OfficialBattleUnitLifePlayback(Action<FightBattleUnitLifeEvent> apply)
            => this.apply = apply ?? throw new ArgumentNullException(nameof(apply));
        internal float NextDelay => pending.Count == 0 ? float.PositiveInfinity : Math.Max(0f, pending[0].Remaining);
        internal FightBattleUnitState Project(FightBattleUnitState state)
            => state != null && projected.TryGetValue(state.Id, out var value)
                ? state.WithPresentationHealth(value.Hp, value.MaxHp) : state;

        internal void Enqueue(FightBattleCheckpoint checkpoint, FightBattleStepEvent step)
        {
            if (step?.HasUnitLifeEvents != true) return;
            long key = ((long)checkpoint.Sequence << 32) | (uint)checkpoint.Counter;
            if (!seen.Add(key)) return;
            foreach (var value in step.UnitLifeEvents)
            {
                if (value == null) continue;
                if (float.IsNaN(value.Delay) || float.IsInfinity(value.Delay))
                    throw new InvalidOperationException("Unit life time must be finite.");
                var copy = new FightBattleUnitLifeEvent { UnitId = value.UnitId, IsDead = value.IsDead,
                    BeforeHp = value.BeforeHp, BeforeMaxHp = value.BeforeMaxHp,
                    Hp = value.Hp, MaxHp = value.MaxHp, Delay = value.Delay };
                if (!projected.ContainsKey(copy.UnitId))
                    projected.Add(copy.UnitId, new FightBattleUnitLifeEvent { Hp = copy.BeforeHp, MaxHp = copy.BeforeMaxHp });
                var item = new Pending { Event = copy, Remaining = Math.Max(0f, copy.Delay) };
                int index = pending.FindIndex(other => other.Remaining > item.Remaining);
                if (index < 0) pending.Add(item); else pending.Insert(index, item);
            }
        }
        internal void AdvanceTimers(float elapsed)
        {
            if (float.IsNaN(elapsed) || float.IsInfinity(elapsed) || elapsed < 0f)
                throw new ArgumentOutOfRangeException(nameof(elapsed));
            foreach (var item in pending) item.Remaining -= elapsed;
        }
        internal void DispatchDue()
        {
            while (pending.Count > 0 && pending[0].Remaining <= 0f)
            {
                var item = pending[0]; pending.RemoveAt(0);
                projected[item.Event.UnitId] = item.Event;
                apply(item.Event);
                if (!pending.Exists(other => other.Event.UnitId == item.Event.UnitId)) projected.Remove(item.Event.UnitId);
            }
        }
        internal void Cancel(int id)
        {
            pending.RemoveAll(item => item.Event.UnitId == id);
            projected.Remove(id);
        }
        internal void Clear() { pending.Clear(); projected.Clear(); seen.Clear(); }
    }
}
