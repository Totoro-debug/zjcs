using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Zjcs.Combat.Official
{
    /// <summary>
    /// One step's thread-safe rendezvous between original hit-view arrivals and local settlement.
    /// The step runs on a worker; the presentation thread takes requests without blocking and
    /// records actual arrivals. A single scope arrival may affect multiple target occurrences.
    /// </summary>
    public sealed class FightBattleImpactLatch : IFightBattleImpactGate, IDisposable
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
        private bool disposed;

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
                disposed = true; arrived.Clear(); requests.Clear();
                Monitor.PulseAll(sync);
            }
        }
    }
}
