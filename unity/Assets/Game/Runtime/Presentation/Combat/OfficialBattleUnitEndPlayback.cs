using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Retains already visible units until their recorded entity-removal boundary.</summary>
    public sealed class OfficialBattleUnitEndPlayback
    {
        private sealed class Pending { public int Id; public float Remaining; }
        private readonly List<Pending> pending = new List<Pending>();
        private readonly HashSet<int> seen = new HashSet<int>();
        private readonly Action<int> remove;
        private readonly Func<int, bool> present;

        public OfficialBattleUnitEndPlayback(Action<int> remove, Func<int, bool> present)
        {
            this.remove = remove ?? throw new ArgumentNullException(nameof(remove));
            this.present = present ?? throw new ArgumentNullException(nameof(present));
        }

        public bool Retains(int id) => pending.Exists(value => value.Id == id);
        public float NextDelay
        {
            get
            {
                float next = float.PositiveInfinity;
                foreach (var value in pending) next = Math.Min(next, Math.Max(0f, value.Remaining));
                return next;
            }
        }

        public void Enqueue(IReadOnlyList<FightBattleUnitEndEvent> events)
        {
            if (events == null) return;
            foreach (var value in events)
            {
                if (value == null || seen.Contains(value.UnitId) || !present(value.UnitId)) continue;
                if (float.IsNaN(value.Delay) || float.IsInfinity(value.Delay) || value.Delay < 0f)
                    throw new InvalidOperationException("Unit removal time must be finite and nonnegative.");
                seen.Add(value.UnitId);
                pending.Add(new Pending { Id = value.UnitId, Remaining = value.Delay });
            }
        }

        public void AdvanceTimers(float elapsed)
        {
            if (float.IsNaN(elapsed) || float.IsInfinity(elapsed) || elapsed < 0f)
                throw new ArgumentOutOfRangeException(nameof(elapsed));
            foreach (var value in pending) value.Remaining -= elapsed;
        }

        public void DispatchDue()
        {
            for (int index = 0; index < pending.Count;)
            {
                var value = pending[index];
                if (value.Remaining > 0f) { index++; continue; }
                pending.RemoveAt(index);
                remove(value.Id);
            }
        }

        public void Clear() { pending.Clear(); seen.Clear(); }
    }
}
