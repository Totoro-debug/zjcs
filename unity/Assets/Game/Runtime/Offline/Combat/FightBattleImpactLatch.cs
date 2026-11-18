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

    /// <summary>A movement whose path and action fields can be copied while the step is suspended.</summary>
    public sealed class FightBattlePreparedMovement
    {
        internal FightBattleImpactLatch Owner;
        internal bool Taken, Completed, ContactConsumed;
        public FightBattleSkillCastEvent Cast { get; internal set; }
        public FightBattleMovementEvent Movement { get; internal set; }
        public bool HasBlockedPosition { get; internal set; }
        public FightGridPoint BlockedPosition { get; internal set; }
    }

    /// <summary>A cast whose final duration can be copied while the step is suspended.</summary>
    public sealed class FightBattleCompletedCast
    {
        internal FightBattleImpactLatch Owner;
        internal bool Taken, Completed;
        public FightBattleSkillCastEvent Cast { get; internal set; }
    }

    /// <summary>A selected hit held while its current values are copied by the host.</summary>
    public sealed class FightBattleSelectedHit
    {
        internal FightBattleImpactLatch Owner;
        internal bool Taken, Completed, Delivering;
        public FightBattleSkillCastEvent Cast { get; internal set; }
        public FightBattleHitSelectionSnapshot Selection { get; internal set; }
    }

    /// <summary>A settled hit held while its current values are copied by the host.</summary>
    public sealed class FightBattleSettledImpact
    {
        internal FightBattleImpactLatch Owner;
        internal bool Taken, Completed, Delivering;
        public bool IsStatusResolution { get; internal set; }
        public FightBattlePendingImpact Impact { get; internal set; }
        public FightBattleDamageEvent Damage { get; internal set; }
    }

    /// <summary>
    /// One step's thread-safe rendezvous between original hit-view arrivals and local settlement.
    /// The step runs on a worker; the presentation thread takes requests without blocking and
    /// records actual arrivals. A single scope arrival may affect multiple target occurrences.
    /// </summary>
    public sealed class FightBattleImpactLatch : IFightBattleImpactGate, IFightBattleCastPreparationObserver, IFightBattleMovementPreparationObserver, IFightBattleBlockedMovementObserver, IFightBattleCastCompletionObserver, IFightBattleHitSelectionObserver, IFightBattleImpactSettlementObserver, IFightBattleImpactStatusResolutionObserver, IDisposable
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
        private readonly HashSet<FightBattleSkillCastEvent> preparedCasts = new HashSet<FightBattleSkillCastEvent>();
        private readonly Queue<FightBattlePendingImpact> requests = new Queue<FightBattlePendingImpact>();
        private readonly Queue<FightBattlePreparedCast> preparations = new Queue<FightBattlePreparedCast>();
        private readonly Queue<FightBattlePreparedMovement> movements = new Queue<FightBattlePreparedMovement>();
        private readonly Dictionary<FightBattleMovementEvent, FightBattlePreparedMovement> blockedMovements =
            new Dictionary<FightBattleMovementEvent, FightBattlePreparedMovement>();
        private readonly HashSet<FightBattleMovementEvent> blockedContacts = new HashSet<FightBattleMovementEvent>();
        private readonly Queue<FightBattleCompletedCast> completedCasts = new Queue<FightBattleCompletedCast>();
        private readonly Queue<FightBattleSelectedHit> selections = new Queue<FightBattleSelectedHit>();
        private readonly Queue<FightBattleSettledImpact> settlements = new Queue<FightBattleSettledImpact>();
        private readonly Dictionary<FightBattleDamageEvent, FightBattlePendingImpact> deferredStatusResults =
            new Dictionary<FightBattleDamageEvent, FightBattlePendingImpact>();
        private bool disposed;
        private int workerThread;
        private long waitRevision;
        private bool workerWaiting, workerStopped, hasWaitingImpact;
        private Signal waitingImpact;
        private FightBattleMovementEvent waitingBlockedMovement;
        public bool ObserveMovements { get; internal set; }

        // The isolated step owns exactly one worker. Other latch users retain the
        // original independent wait semantics and do not participate in its fence.
        internal void BindWorker()
        {
            lock (sync)
            {
                if (workerThread != 0) throw new InvalidOperationException("The impact latch already has a worker.");
                workerThread = Thread.CurrentThread.ManagedThreadId;
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
            }
        }

        internal void FinishWorker()
        {
            lock (sync) { workerStopped = true; workerWaiting = false; hasWaitingImpact = false; waitingBlockedMovement = null; Monitor.PulseAll(sync); }
        }

        private void ParkWorker()
        {
            bool tracked = workerThread == Thread.CurrentThread.ManagedThreadId;
            if (tracked) { workerWaiting = true; waitRevision++; Monitor.PulseAll(sync); }
            try { Monitor.Wait(sync); }
            finally { if (tracked) workerWaiting = false; }
        }

        /// <summary>Captures the current isolated-worker wait before releasing a handoff.</summary>
        public long WorkerWaitRevision { get { lock (sync) return waitRevision; } }

        /// <summary>Waits for that worker to park at its next rule boundary or finish.</summary>
        public void WaitForWorkerBoundary(long previousRevision)
        {
            lock (sync)
            {
                while (!disposed && !workerStopped && (!workerWaiting || waitRevision <= previousRevision))
                    Monitor.Wait(sync);
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
            }
        }

        public void CastPrepared(FightBattleSkillCastEvent cast)
        {
            if (cast == null) throw new ArgumentNullException(nameof(cast));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (!preparedCasts.Add(cast))
                    throw new InvalidOperationException("The cast was already prepared for this step.");
                var preparation = new FightBattlePreparedCast { Owner = this, Cast = cast };
                preparations.Enqueue(preparation);
                while (!disposed && !preparation.Completed) ParkWorker();
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
            => CompletePreparation(preparation, out _);

        public void CompletePreparation(FightBattlePreparedCast preparation, out long releasedRevision)
        {
            releasedRevision = 0;
            if (preparation == null) throw new ArgumentNullException(nameof(preparation));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (!ReferenceEquals(preparation.Owner, this) || !preparation.Taken || preparation.Completed)
                    throw new InvalidOperationException("The cast preparation is not awaiting this host.");
                releasedRevision = waitRevision;
                preparation.Completed = true;
                Monitor.PulseAll(sync);
            }
        }

        public void MovementPrepared(FightBattleSkillCastEvent cast, FightBattleMovementEvent movement)
        {
            if (movement == null) throw new ArgumentNullException(nameof(movement));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (!ObserveMovements) return;
                var preparation = new FightBattlePreparedMovement { Owner = this, Cast = cast, Movement = movement };
                movements.Enqueue(preparation);
                while (!disposed && !preparation.Completed) ParkWorker();
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
            }
        }

        public void BlockedMovementPrepared(FightBattleSkillCastEvent cast, FightBattleMovementEvent movement,
            FightGridPoint blockedPosition)
        {
            if (movement == null) throw new ArgumentNullException(nameof(movement));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (!ObserveMovements) return;
                var preparation = new FightBattlePreparedMovement { Owner = this, Cast = cast, Movement = movement,
                    HasBlockedPosition = true, BlockedPosition = blockedPosition };
                if (blockedMovements.ContainsKey(movement))
                    throw new InvalidOperationException("The blocked movement is already registered.");
                blockedMovements.Add(movement, preparation);
                movements.Enqueue(preparation);
                while (!disposed && !preparation.Completed) ParkWorker();
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
            }
        }

        public void AwaitBlockedContact(FightBattleMovementEvent movement)
        {
            if (movement == null) throw new ArgumentNullException(nameof(movement));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (!ObserveMovements) return;
                if (!blockedMovements.TryGetValue(movement, out var preparation) || !preparation.Completed)
                    throw new InvalidOperationException("The blocked movement has not started.");
                if (preparation.ContactConsumed)
                    throw new InvalidOperationException("The blocked movement contact was already consumed.");
                while (!disposed && !blockedContacts.Contains(movement))
                {
                    bool tracked = workerThread == Thread.CurrentThread.ManagedThreadId;
                    if (tracked) waitingBlockedMovement = movement;
                    try { ParkWorker(); }
                    finally { if (tracked) waitingBlockedMovement = null; }
                }
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (preparation.ContactConsumed)
                    throw new InvalidOperationException("The blocked movement contact was already consumed.");
                preparation.ContactConsumed = true;
            }
        }

        /// <summary>Records contact from this movement's actual collision track.</summary>
        public bool RecordBlockedContact(FightBattleMovementEvent movement, out long releasedRevision)
        {
            releasedRevision = 0;
            if (movement == null) throw new ArgumentNullException(nameof(movement));
            lock (sync)
            {
                if (disposed) return false;
                if (!blockedMovements.TryGetValue(movement, out var preparation) || !preparation.Taken)
                    throw new InvalidOperationException("The blocked movement is not held by this host.");
                bool added = blockedContacts.Add(movement);
                if (added && workerWaiting && ReferenceEquals(waitingBlockedMovement, movement))
                    releasedRevision = waitRevision;
                Monitor.PulseAll(sync);
                return added;
            }
        }

        /// <summary>Copies must be made before completion; enclosing action offsets can still change.</summary>
        public bool TryTakeMovement(out FightBattlePreparedMovement preparation)
        {
            lock (sync)
            {
                if (movements.Count == 0) { preparation = null; return false; }
                preparation = movements.Dequeue(); preparation.Taken = true;
                return true;
            }
        }

        public void CompleteMovement(FightBattlePreparedMovement preparation, out long releasedRevision)
        {
            if (preparation == null) throw new ArgumentNullException(nameof(preparation));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (!ReferenceEquals(preparation.Owner, this) || !preparation.Taken || preparation.Completed)
                    throw new InvalidOperationException("The movement preparation is not awaiting this host.");
                releasedRevision = waitRevision;
                preparation.Completed = true;
                Monitor.PulseAll(sync);
            }
        }

        public void CastCompleted(FightBattleSkillCastEvent cast)
        {
            if (cast == null) throw new ArgumentNullException(nameof(cast));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                // A rejected cast can still have its duration finalized without ever starting.
                if (!preparedCasts.Remove(cast)) return;
                var completed = new FightBattleCompletedCast { Owner = this, Cast = cast };
                completedCasts.Enqueue(completed);
                while (!disposed && !completed.Completed) ParkWorker();
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
            }
        }

        /// <summary>Copy final duration before completion; enclosing step offsets may still change.</summary>
        public bool TryTakeCastCompletion(out FightBattleCompletedCast completed)
        {
            lock (sync)
            {
                if (completedCasts.Count == 0) { completed = null; return false; }
                completed = completedCasts.Dequeue(); completed.Taken = true;
                return true;
            }
        }

        public void CompleteCast(FightBattleCompletedCast completed) => CompleteCast(completed, out _);

        public void CompleteCast(FightBattleCompletedCast completed, out long releasedRevision)
        {
            releasedRevision = 0;
            if (completed == null) throw new ArgumentNullException(nameof(completed));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (!ReferenceEquals(completed.Owner, this) || !completed.Taken || completed.Completed)
                    throw new InvalidOperationException("The cast completion is not awaiting this host.");
                releasedRevision = waitRevision;
                completed.Completed = true;
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
                while (!disposed && !selected.Completed) ParkWorker();
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
            => CompleteSelection(selected, null);

        /// <summary>Delivers a selection while its executor remains suspended.</summary>
        public void CompleteSelection(FightBattleSelectedHit selected,
            Action<FightBattleSkillCastEvent, FightBattleHitSelectionSnapshot> deliver)
            => CompleteSelection(selected, deliver, out _);

        public void CompleteSelection(FightBattleSelectedHit selected,
            Action<FightBattleSkillCastEvent, FightBattleHitSelectionSnapshot> deliver, out long releasedRevision)
        {
            releasedRevision = 0;
            if (selected == null) throw new ArgumentNullException(nameof(selected));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (!ReferenceEquals(selected.Owner, this) || !selected.Taken || selected.Completed || selected.Delivering)
                    throw new InvalidOperationException("The hit selection is not awaiting this host.");
                selected.Delivering = true;
                try { deliver?.Invoke(selected.Cast, selected.Selection); }
                finally { selected.Delivering = false; }
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                releasedRevision = waitRevision;
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
                while (!disposed && !settlement.Completed) ParkWorker();
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
            }
        }

        /// <summary>Releases an incomplete receipt solely to compute its real status result.</summary>
        public void DeferStatusResolution(FightBattleSettledImpact settlement, out long releasedRevision)
        {
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (!ReferenceEquals(settlement.Owner, this) || !settlement.Taken || settlement.Completed
                    || settlement.Delivering || settlement.IsStatusResolution || settlement.Damage.HasDamageTriggerStatus
                    || deferredStatusResults.ContainsKey(settlement.Damage))
                    throw new InvalidOperationException("The hit has no unresolved status result to defer.");
                deferredStatusResults.Add(settlement.Damage, settlement.Impact);
                releasedRevision = waitRevision;
                settlement.Completed = true;
                Monitor.PulseAll(sync);
            }
        }

        public void ImpactStatusResolved(FightBattlePendingImpact impact, FightBattleDamageEvent damage)
        {
            if (impact == null) throw new ArgumentNullException(nameof(impact));
            if (damage == null) throw new ArgumentNullException(nameof(damage));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (!deferredStatusResults.TryGetValue(damage, out var expectedImpact)) return;
                if (!ReferenceEquals(impact, expectedImpact))
                    throw new InvalidOperationException("The status result belongs to another impact.");
                if (!damage.HasDamageTriggerStatus)
                    throw new InvalidOperationException("The deferred status result is still unknown.");
                deferredStatusResults.Remove(damage);
                var settlement = new FightBattleSettledImpact { Owner = this, Impact = impact, Damage = damage,
                    IsStatusResolution = true };
                settlements.Enqueue(settlement);
                while (!disposed && !settlement.Completed) ParkWorker();
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
            => TryCompleteSettlement(settlement, (impact, damage) => true);

        /// <summary>Delivers the actual result while the executor is suspended; false leaves it waiting.</summary>
        public bool TryCompleteSettlement(FightBattleSettledImpact settlement,
            Func<FightBattlePendingImpact, FightBattleDamageEvent, bool> deliver)
            => TryCompleteSettlement(settlement, deliver, out _);

        public bool TryCompleteSettlement(FightBattleSettledImpact settlement,
            Func<FightBattlePendingImpact, FightBattleDamageEvent, bool> deliver, out long releasedRevision)
        {
            releasedRevision = 0;
            if (settlement == null) throw new ArgumentNullException(nameof(settlement));
            if (deliver == null) throw new ArgumentNullException(nameof(deliver));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (!ReferenceEquals(settlement.Owner, this) || !settlement.Taken
                    || settlement.Completed || settlement.Delivering)
                    throw new InvalidOperationException("The hit settlement is not awaiting this host.");
                bool delivered;
                settlement.Delivering = true;
                try { delivered = deliver(settlement.Impact, settlement.Damage); }
                finally { settlement.Delivering = false; }
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (!delivered) return false;
                releasedRevision = waitRevision;
                settlement.Completed = true;
                Monitor.PulseAll(sync);
                return true;
            }
        }

        public void AwaitImpact(FightBattlePendingImpact impact)
        {
            Signal signal = SignalFor(impact);
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                requests.Enqueue(impact);
                while (!disposed && !arrived.Contains(signal))
                {
                    bool tracked = workerThread == Thread.CurrentThread.ManagedThreadId;
                    if (tracked) { waitingImpact = signal; hasWaitingImpact = true; }
                    try { ParkWorker(); }
                    finally { if (tracked) hasWaitingImpact = false; }
                }
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
            int centerIndex, int scopeIndex) => RecordArrival(cast, selection, centerIndex, scopeIndex, out _);

        /// <summary>Also returns the waiting worker revision when this exact arrival releases it.</summary>
        public bool RecordArrival(FightBattleSkillCastEvent cast, FightBattleHitSelectionSnapshot selection,
            int centerIndex, int scopeIndex, out long releasedRevision)
        {
            releasedRevision = 0;
            if (cast == null) throw new ArgumentNullException(nameof(cast));
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            if (centerIndex < 0 || selection.EffectCenters == null || centerIndex >= selection.EffectCenters.Count)
                throw new ArgumentOutOfRangeException(nameof(centerIndex));
            if (scopeIndex < -1) throw new ArgumentOutOfRangeException(nameof(scopeIndex));
            lock (sync)
            {
                if (disposed) return false;
                var signal = new Signal(cast, selection, centerIndex, scopeIndex);
                bool added = arrived.Add(signal);
                if (workerWaiting && hasWaitingImpact && waitingImpact.Equals(signal)) releasedRevision = waitRevision;
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
                disposed = true; waitingBlockedMovement = null; arrived.Clear(); preparedCasts.Clear(); requests.Clear(); preparations.Clear(); movements.Clear(); blockedMovements.Clear(); blockedContacts.Clear(); completedCasts.Clear(); selections.Clear(); settlements.Clear(); deferredStatusResults.Clear();
                Monitor.PulseAll(sync);
            }
        }
    }
}
