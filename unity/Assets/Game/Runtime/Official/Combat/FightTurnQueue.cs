using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    [Serializable]
    public sealed class FightTurnUnit
    {
        public int Id;
        public int MasterId;
        public long Hp;
        public int Speed;
        public int RankSpeedScale = 1;
        public int RoundType;
        public int RoundOffset;
        public int ActionRound;
        public long IntervalTicks;
        public bool Ended;
    }

    [Serializable]
    public sealed class FightTurnEntry
    {
        public int Id;
        public long Pre;
        public long Next;

        public FightTurnEntry Copy()
        {
            return new FightTurnEntry { Id = Id, Pre = Pre, Next = Next };
        }
    }

    [Serializable]
    public sealed class FightTurnQueueState
    {
        public List<FightTurnEntry> Entries = new List<FightTurnEntry>();
        public int Acting;
        public int Round;
        public long Current;
        public List<FightRoundIntervalChange> PendingChanges = new List<FightRoundIntervalChange>();
    }

    [Serializable]
    public sealed class FightRoundIntervalChange
    {
        public int UnitId;
        public float Percent;
    }

    public sealed class FightTurnQueue
    {
        private readonly FightTurnQueueState state;
        private readonly Dictionary<int, FightTurnUnit> units;
        private readonly List<FightTurnUnit> unitOrder = new List<FightTurnUnit>();

        public FightTurnQueue(FightTurnQueueState state, IEnumerable<FightTurnUnit> roster)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            if (state.Entries == null) state.Entries = new List<FightTurnEntry>();
            if (state.PendingChanges == null) state.PendingChanges = new List<FightRoundIntervalChange>();
            units = new Dictionary<int, FightTurnUnit>();
            foreach (FightTurnUnit unit in roster)
            {
                if (unit == null || unit.Id <= 0 || units.ContainsKey(unit.Id))
                    throw new ArgumentException("Fight roster has a null or duplicate unit id.", nameof(roster));
                units.Add(unit.Id, unit);
                unitOrder.Add(unit);
            }
        }

        public FightTurnQueueState State => state;

        public void RegisterUnit(FightTurnUnit unit, bool immediate = false)
        {
            if (unit == null || unit.Id <= 0 || units.ContainsKey(unit.Id))
                throw new ArgumentException("Fight turn unit identity is null or already registered.",
                    nameof(unit));
            units.Add(unit.Id, unit);
            unitOrder.Add(unit);
            if (unit.RoundType != 1) Insert(unit, immediate);
        }

        public void InsertUnit(FightTurnUnit unit, bool immediate = false)
        {
            if (unit == null || !units.ContainsKey(unit.Id))
                throw new ArgumentException("Fight turn unit is not part of this roster.", nameof(unit));
            Insert(unit, immediate);
        }

        public void Initialize()
        {
            state.Entries.Clear();
            foreach (FightTurnUnit unit in unitOrder)
                if (unit.RoundType != 1) Insert(unit, false);
            foreach (FightRoundIntervalChange change in state.PendingChanges.ToArray())
                ChangeInterval(change.UnitId, change.Percent, true);
            state.PendingChanges.Clear();
        }

        public FightTurnUnit Next()
        {
            while (state.Entries.Count != 0)
            {
                FightTurnEntry entry = state.Entries[0];
                FightTurnUnit unit;
                if (!units.TryGetValue(entry.Id, out unit) || unit.Hp <= 0 || unit.Ended)
                {
                    state.Entries.RemoveAt(0);
                    continue;
                }

                if (state.Acting != unit.Id) state.Acting = unit.Id;
                state.Current = entry.Next;
                return unit;
            }
            return null;
        }

        public void Finish(FightTurnUnit unit)
        {
            if (unit == null) throw new ArgumentNullException(nameof(unit));
            RemoveFirst(unit.Id);
            if (unit.Hp <= 0 || unit.Ended)
            {
                ClearPending(unit.Id);
                return;
            }

            long interval = IntervalTicks(unit);
            unit.IntervalTicks = interval;
            AddSorted(new FightTurnEntry { Id = unit.Id, Pre = state.Current, Next = state.Current + interval });
            ApplyPending(unit.Id);
        }

        public void ChangeInterval(int unitId, float percent, bool force = false)
        {
            FightTurnUnit unit;
            if (!units.TryGetValue(unitId, out unit)) return;
            FightTurnEntry entry = Find(unitId);
            if (entry == null) return;
            if (!force && state.Acting == unitId)
            {
                state.PendingChanges.Add(new FightRoundIntervalChange { UnitId = unitId, Percent = percent });
                return;
            }

            long old = entry.Next;
            long interval = unit.IntervalTicks > 0 ? unit.IntervalTicks : IntervalTicks(unit);
            entry.Next = old + ChangeValue(state.Current, old, interval, percent);
            RemoveFirst(unitId);
            AddSorted(entry);
        }

        public int GlobalRound()
        {
            int round = int.MaxValue;
            foreach (FightTurnUnit unit in unitOrder)
                if (unit.Hp > 0 && !unit.Ended && unit.RoundType != 1 && unit.MasterId == 0)
                    round = Math.Min(round, unit.RoundOffset + Math.Max(0, unit.ActionRound));
            return round == int.MaxValue ? 1 : round + 1;
        }

        public void ResetClock()
        {
            state.Current = 0;
            List<FightTurnEntry> previous = new List<FightTurnEntry>(state.Entries);
            state.Entries.Clear();
            foreach (FightTurnEntry entry in previous)
            {
                FightTurnUnit unit = units[entry.Id];
                entry.Pre = 0;
                entry.Next = IntervalTicks(unit);
                AddSorted(entry);
            }
        }

        private void Insert(FightTurnUnit unit, bool immediate)
        {
            unit.RoundOffset = Math.Max(0, state.Round - 1);
            long interval = IntervalTicks(unit);
            unit.IntervalTicks = interval;
            AddSorted(new FightTurnEntry { Id = unit.Id, Pre = state.Current, Next = immediate ? state.Current : state.Current + interval });
        }

        private long IntervalTicks(FightTurnUnit unit)
        {
            double speed = Math.Max(1, unit.Speed) * (double)Math.Max(1, unit.RankSpeedScale);
            double milliseconds = 100000.0 / Math.Sqrt(speed);
            return Math.Max(1L, (long)Math.Floor(milliseconds * 1000.0 + 0.5));
        }

        private static long ChangeValue(long current, long next, long interval, float percent)
        {
            float value = (float)percent;
            if (value == 0f) return Math.Min(0L, current - next);
            return (long)Math.Round(-(double)value * interval, MidpointRounding.AwayFromZero);
        }

        private FightTurnEntry Find(int unitId)
        {
            for (int i = 0; i < state.Entries.Count; i++)
                if (state.Entries[i].Id == unitId) return state.Entries[i];
            return null;
        }

        private bool RemoveFirst(int unitId)
        {
            for (int i = 0; i < state.Entries.Count; i++)
                if (state.Entries[i].Id == unitId)
                {
                    state.Entries.RemoveAt(i);
                    return true;
                }
            return false;
        }

        private void AddSorted(FightTurnEntry entry)
        {
            for (int i = 0; i < state.Entries.Count; i++)
                if (state.Entries[i].Next > entry.Next)
                {
                    state.Entries.Insert(i, entry);
                    return;
                }
            state.Entries.Add(entry);
        }

        private void ApplyPending(int unitId)
        {
            for (int i = state.PendingChanges.Count - 1; i >= 0; i--)
            {
                FightRoundIntervalChange change = state.PendingChanges[i];
                if (change.UnitId != unitId) continue;
                state.PendingChanges.RemoveAt(i);
                ChangeInterval(unitId, change.Percent, true);
            }
        }

        private void ClearPending(int unitId)
        {
            for (int i = state.PendingChanges.Count - 1; i >= 0; i--)
                if (state.PendingChanges[i].UnitId == unitId) state.PendingChanges.RemoveAt(i);
        }
    }
}
