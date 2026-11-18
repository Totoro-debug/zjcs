using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Zjcs.Combat.Official
{
    /// <summary>A cast whose creation-time data can be copied while the step is suspended.</summary>
    public sealed class FightBattlePreparedCast
    {
        internal FightBattleImpactLatch Owner;
        internal bool Taken, Completed;
        public FightBattleSkillCastEvent Cast { get; internal set; }
    }

    /// <summary>A selected hit held while its current values are copied by the host.</summary>
    public sealed class FightBattleSelectedHit
    {
        internal FightBattleImpactLatch Owner;
        internal bool Taken, Completed;
        public FightBattleSkillCastEvent Cast { get; internal set; }
        public FightBattleHitSelectionSnapshot Selection { get; internal set; }
    }

    /// <summary>A settled hit held while its current values are copied by the host.</summary>
    public sealed class FightBattleSettledImpact
    {
        internal FightBattleImpactLatch Owner;
        internal bool Taken, Completed;
        public FightBattlePendingImpact Impact { get; internal set; }
        public FightBattleDamageEvent Damage { get; internal set; }
    }

    /// <summary>
    /// One step's thread-safe rendezvous between original hit-view arrivals and local settlement.
    /// The step runs on a worker; the presentation thread takes requests without blocking and
    /// records actual arrivals. A single scope arrival may affect multiple target occurrences.
    /// </summary>
    public sealed class FightBattleImpactLatch : IFightBattleImpactGate, IFightBattleCastPreparationObserver, IFightBattleHitSelectionObserver, IFightBattleImpactSettlementObserver, IDisposable
    {
        private readonly struct Signal : IEquatable<Signal>
        {
            private readonly FightBattleSkillCastEvent cast;
            private readonly FightBattleHitSelectionSnapshot selection;
            private readonly int center, scope;

            public Signal(FightBattleSkillCastEvent cast, FightBattleHitSelectionSnapshot selection,
                int center, int scope)
            { this.cast = cast; this.selection = selection; this.center = center; this.scope = scope; }

            public bool Equals(Signal other) => ReferenceEquals(cast, other.cast)
                && ReferenceEquals(selection, other.selection) && center == other.center && scope == other.scope;
            public override bool Equals(object value) => value is Signal other && Equals(other);
            public override int GetHashCode() => unchecked((((RuntimeHelpers.GetHashCode(cast) * 397)
                ^ RuntimeHelpers.GetHashCode(selection)) * 397 ^ center) * 397 ^ scope);
        }

        private readonly object sync = new object();
        private readonly HashSet<Signal> arrived = new HashSet<Signal>();
        private readonly Queue<FightBattlePendingImpact> requests = new Queue<FightBattlePendingImpact>();
        private readonly Queue<FightBattlePreparedCast> preparations = new Queue<FightBattlePreparedCast>();
        private readonly Queue<FightBattleSelectedHit> selections = new Queue<FightBattleSelectedHit>();
        private readonly Queue<FightBattleSettledImpact> settlements = new Queue<FightBattleSettledImpact>();
        private bool disposed;

        public void CastPrepared(FightBattleSkillCastEvent cast)
        {
            if (cast == null) throw new ArgumentNullException(nameof(cast));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                var preparation = new FightBattlePreparedCast { Owner = this, Cast = cast };
                preparations.Enqueue(preparation);
                while (!disposed && !preparation.Completed) Monitor.Wait(sync);
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
            }
        }

        /// <summary>
        /// Takes a prepared cast without blocking. Copy needed fields before completing it;
        /// its final presentation offsets and duration are not necessarily known yet.
        /// </summary>
        public bool TryTakePreparation(out FightBattlePreparedCast preparation)
        {
            lock (sync)
            {
                if (preparations.Count == 0) { preparation = null; return false; }
                preparation = preparations.Dequeue(); preparation.Taken = true;
                return true;
            }
        }

        public void CompletePreparation(FightBattlePreparedCast preparation)
        {
            if (preparation == null) throw new ArgumentNullException(nameof(preparation));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (!ReferenceEquals(preparation.Owner, this) || !preparation.Taken || preparation.Completed)
                    throw new InvalidOperationException("The cast preparation is not awaiting this host.");
                preparation.Completed = true;
                Monitor.PulseAll(sync);
            }
        }

        public void HitSelected(FightBattleSkillCastEvent cast, FightBattleHitSelectionSnapshot selection)
        {
            if (cast == null) throw new ArgumentNullException(nameof(cast));
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                var selected = new FightBattleSelectedHit { Owner = this, Cast = cast, Selection = selection };
                selections.Enqueue(selected);
                while (!disposed && !selected.Completed) Monitor.Wait(sync);
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
            }
        }

        /// <summary>Copies must be made before completion; later effects may modify the selection.</summary>
        public bool TryTakeSelection(out FightBattleSelectedHit selected)
        {
            lock (sync)
            {
                if (selections.Count == 0) { selected = null; return false; }
                selected = selections.Dequeue(); selected.Taken = true;
                return true;
            }
        }

        public void CompleteSelection(FightBattleSelectedHit selected)
        {
            if (selected == null) throw new ArgumentNullException(nameof(selected));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (!ReferenceEquals(selected.Owner, this) || !selected.Taken || selected.Completed)
                    throw new InvalidOperationException("The hit selection is not awaiting this host.");
                selected.Completed = true;
                Monitor.PulseAll(sync);
            }
        }

        public void ImpactSettled(FightBattlePendingImpact impact, FightBattleDamageEvent damage)
        {
            if (impact == null) throw new ArgumentNullException(nameof(impact));
            if (damage == null) throw new ArgumentNullException(nameof(damage));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                var settlement = new FightBattleSettledImpact { Owner = this, Impact = impact, Damage = damage };
                settlements.Enqueue(settlement);
                while (!disposed && !settlement.Completed) Monitor.Wait(sync);
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
            }
        }

        /// <summary>Copies must be made before completion; later effects may modify the event.</summary>
        public bool TryTakeSettlement(out FightBattleSettledImpact settlement)
        {
            lock (sync)
            {
                if (settlements.Count == 0) { settlement = null; return false; }
                settlement = settlements.Dequeue(); settlement.Taken = true;
                return true;
            }
        }

        public void CompleteSettlement(FightBattleSettledImpact settlement)
        {
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (!ReferenceEquals(settlement.Owner, this) || !settlement.Taken || settlement.Completed)
                    throw new InvalidOperationException("The hit settlement is not awaiting this host.");
                settlement.Completed = true;
                Monitor.PulseAll(sync);
            }
        }

        public void AwaitImpact(FightBattlePendingImpact impact)
        {
            Signal signal = SignalFor(impact);
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                requests.Enqueue(impact);
                while (!disposed && !arrived.Contains(signal)) Monitor.Wait(sync);
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
            }
        }

        public bool TryTake(out FightBattlePendingImpact impact)
        {
            lock (sync)
            {
                if (requests.Count == 0) { impact = null; return false; }
                impact = requests.Dequeue();
                return true;
            }
        }

        /// <summary>Records an actual hit-centre (-1) or authored scope playback arrival.</summary>
        public bool RecordArrival(FightBattleSkillCastEvent cast, FightBattleHitSelectionSnapshot selection,
            int centerIndex, int scopeIndex)
        {
            if (cast == null) throw new ArgumentNullException(nameof(cast));
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            if (centerIndex < 0 || selection.EffectCenters == null || centerIndex >= selection.EffectCenters.Count)
                throw new ArgumentOutOfRangeException(nameof(centerIndex));
            if (scopeIndex < -1) throw new ArgumentOutOfRangeException(nameof(scopeIndex));
            lock (sync)
            {
                if (disposed) return false;
                bool added = arrived.Add(new Signal(cast, selection, centerIndex, scopeIndex));
                Monitor.PulseAll(sync);
                return added;
            }
        }

        private static Signal SignalFor(FightBattlePendingImpact impact)
        {
            if (impact?.Cast == null || impact.HitSelection == null)
                throw new InvalidOperationException("A pending impact needs its actual cast and hit selection.");
            if (impact.ScopeIdentityUnresolved)
                throw new InvalidOperationException("An aggregate hit has no verified target-to-scope identity.");
            var selection = impact.HitSelection;
            if (selection.EffectCenters == null || impact.EffectCenterIndex < 0
                || impact.EffectCenterIndex >= selection.EffectCenters.Count
                || selection.Targets == null || impact.OccurrenceIndex < 0
                || impact.OccurrenceIndex >= selection.Targets.Count)
                throw new InvalidOperationException("The pending impact selection identity is invalid.");
            var occurrence = selection.Targets[impact.OccurrenceIndex];
            if (occurrence == null || occurrence.TargetId != impact.TargetId
                || occurrence.EffectCenterIndex != impact.EffectCenterIndex
                || occurrence.HasScopeIndex != impact.HasScopeIndex
                || impact.HasScopeIndex && (impact.ScopeIndex < 0 || occurrence.ScopeIndex != impact.ScopeIndex))
                throw new InvalidOperationException("The pending impact does not match its selected occurrence.");
            return new Signal(impact.Cast, selection, impact.EffectCenterIndex,
                impact.HasScopeIndex ? impact.ScopeIndex : -1);
        }

        /// <summary>
        /// Cancels waiters. A host that interrupts a running step must discard its partial state
        /// and restore a checkpoint before continuing the battle.
        /// </summary>
        public void Dispose()
        {
            lock (sync)
            {
                if (disposed) return;
                disposed = true; arrived.Clear(); requests.Clear(); preparations.Clear(); selections.Clear(); settlements.Clear();
                Monitor.PulseAll(sync);
            }
        }
    }
}
