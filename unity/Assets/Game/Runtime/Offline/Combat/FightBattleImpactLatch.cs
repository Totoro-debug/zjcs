using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

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

    /// <summary>A seconds-based status transition held for presentation on the host thread.</summary>
    public sealed class FightBattleSecondsStatusNotice
    {
        internal FightBattleImpactLatch Owner;
        internal bool Taken, Completed;
        public FightBattleStatusViewEvent Event { get; internal set; }
    }

    /// <summary>A character-status transition held for presentation on the host thread.</summary>
    public sealed class FightBattleStatusViewNotice
    {
        internal FightBattleImpactLatch Owner;
        internal bool Taken, Completed;
        public FightBattleStatusViewEvent Event { get; internal set; }
    }

    /// <summary>A grid-entity transition held for presentation on the host thread.</summary>
    public sealed class FightBattleGridViewNotice
    {
        internal FightBattleImpactLatch Owner;
        internal bool Taken, Completed;
        public FightBattleGridViewEvent Event { get; internal set; }
    }

    /// <summary>A successful dispel held for presentation on the host thread.</summary>
    public sealed class FightBattleDisperseNotice
    {
        internal FightBattleImpactLatch Owner;
        internal bool Taken, Completed;
        public FightBattleDamageEvent Damage { get; internal set; }
    }

    public sealed class FightBattleSummonNotice
    {
        internal FightBattleImpactLatch Owner;
        internal bool Taken, Completed;
        public FightBattleUnitState Unit { get; internal set; }
        public FightBattleHitSelectionSnapshot.SpawnedUnit Receipt { get; internal set; }
    }

    /// <summary>A confirmed unit-life transition held for presentation on the host thread.</summary>
    public sealed class FightBattleUnitLifeNotice
    {
        internal FightBattleImpactLatch Owner;
        internal bool Taken, Completed;
        public FightBattleUnitLifeEvent Event { get; internal set; }
    }

    /// <summary>
    /// One step's thread-safe rendezvous between original hit-view arrivals and local settlement.
    /// The step runs on a worker; the presentation thread takes requests without blocking and
    /// records actual arrivals. A single scope arrival may affect multiple target occurrences.
    /// </summary>
    public sealed class FightBattleImpactLatch : IFightBattleImpactGate, IFightBattleCastPreparationObserver, IFightBattleMovementPreparationObserver, IFightBattleBlockedMovementObserver, IFightBattleBlockedImpactObserver, IFightBattleDeferredBlockedImpactObserver, IFightBattleCastCompletionObserver, IFightBattleHitSelectionObserver, IFightBattleImpactSettlementObserver, IFightBattleImpactStatusResolutionObserver, IFightBattleStatusViewObserver, IFightBattleGridViewObserver, IFightBattleDisperseObserver, IFightBattleUnitLifeObserver, IFightBattleSummonObserver, IDisposable
    {
        private sealed class WorkerWait
        {
            public object Handoff;
            public Signal? Impact;
            public FightBattleMovementEvent BlockedMovement;
            public long Revision;
        }

        private sealed class WorkerCommand
        {
            public Func<bool> Execute;
            public TaskCompletionSource<bool> Completion;
            public bool AllowNested;
        }
        private sealed class DeferredContact
        {
            public FightBattleSkillCastEvent Cast;
            public Action Callback;
            public bool Queued;
        }
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
        private readonly Dictionary<FightBattleMovementEvent, DeferredContact> deferredContacts =
            new Dictionary<FightBattleMovementEvent, DeferredContact>();
        private readonly Dictionary<FightBattleSkillCastEvent, (FightBattleMovementEvent Movement, int TargetId)> blockedImpacts =
            new Dictionary<FightBattleSkillCastEvent, (FightBattleMovementEvent, int)>();
        private readonly HashSet<(FightBattleMovementEvent Movement, int TargetId)> boundBlockedTargets =
            new HashSet<(FightBattleMovementEvent, int)>();
        private readonly Queue<FightBattleCompletedCast> completedCasts = new Queue<FightBattleCompletedCast>();
        private readonly Queue<FightBattleSelectedHit> selections = new Queue<FightBattleSelectedHit>();
        private readonly Queue<FightBattleSettledImpact> settlements = new Queue<FightBattleSettledImpact>();
        private readonly Queue<FightBattleSecondsStatusNotice> secondsStatuses = new Queue<FightBattleSecondsStatusNotice>();
        private readonly Queue<FightBattleStatusViewNotice> statusViews = new Queue<FightBattleStatusViewNotice>();
        private readonly Queue<FightBattleGridViewNotice> gridViews = new Queue<FightBattleGridViewNotice>();
        private readonly Queue<FightBattleDisperseNotice> disperses = new Queue<FightBattleDisperseNotice>();
        private readonly Queue<FightBattleUnitLifeNotice> unitLives = new Queue<FightBattleUnitLifeNotice>();
        private readonly Queue<FightBattleSummonNotice> summons = new Queue<FightBattleSummonNotice>();
        private readonly Dictionary<FightBattleDamageEvent, FightBattlePendingImpact> deferredStatusResults =
            new Dictionary<FightBattleDamageEvent, FightBattlePendingImpact>();
        private readonly Queue<WorkerCommand> workerCommands = new Queue<WorkerCommand>();
        private bool disposed;
        private int workerThread;
        private long waitRevision;
        private bool workerWaiting, workerStopped, workerCommandActive;
        private WorkerWait workerWait;
        public bool ObserveMovements { get; internal set; }
        internal bool ObserveLiveStatusHits { get; set; }
        internal bool ObserveStatusViews { get; set; }
        internal bool ObserveGridViews { get; set; }
        internal bool ObserveDisperses { get; set; }
        internal bool ObserveUnitLives { get; set; }
        internal bool ObserveSummons { get; set; }
        bool IFightBattleBlockedMovementObserver.ObservesBlockedMovements => ObserveMovements;

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
            lock (sync) { workerStopped = true; workerWaiting = false; workerWait = null; CancelWorkerCommands(); Monitor.PulseAll(sync); }
        }

        private void CancelWorkerCommands()
        {
            while (workerCommands.Count > 0) workerCommands.Dequeue().Completion.TrySetCanceled();
        }

        // Only the bound rule worker executes commands, while its current handoff
        // remains held. A command may itself reach another nested handoff.
        internal bool TryQueueWorkerCommand(Func<bool> execute, out Task<bool> completion)
        {
            if (execute == null) throw new ArgumentNullException(nameof(execute));
            lock (sync)
            {
                if (disposed) throw new ObjectDisposedException(nameof(FightBattleImpactLatch));
                if (workerStopped || !workerWaiting || workerCommandActive || workerCommands.Count != 0)
                { completion = null; return false; }
                var command = new WorkerCommand { Execute = execute,
                    Completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously) };
                workerCommands.Enqueue(command); completion = command.Completion.Task;
                Monitor.PulseAll(sync);
                return true;
            }
        }

        private void ParkWorker(object handoff = null, Signal? impact = null, FightBattleMovementEvent blockedMovement = null)
        {
            bool tracked = workerThread == Thread.CurrentThread.ManagedThreadId;
            WorkerWait previous = workerWait;
            if (tracked)
            {
                workerWait = new WorkerWait { Handoff = handoff, Impact = impact, BlockedMovement = blockedMovement, Revision = ++waitRevision };
                workerWaiting = true; Monitor.PulseAll(sync);
            }
            try
            {
                if (!tracked || workerCommands.Count == 0
                    || workerCommandActive && !workerCommands.Peek().AllowNested)
                    Monitor.Wait(sync);
                if (tracked && !disposed && workerCommands.Count > 0
                    && (!workerCommandActive || workerCommands.Peek().AllowNested))
                {
                    var command = workerCommands.Dequeue();
                    bool wasCommandActive = workerCommandActive;
                    workerWaiting = false; workerCommandActive = true;
                    bool result = false; Exception failure = null;
                    Monitor.Exit(sync);
                    try { result = command.Execute(); }
                    catch (Exception error) { failure = error; }
                    finally { Monitor.Enter(sync); workerCommandActive = wasCommandActive; }
                    if (failure != null) { command.Completion.TrySetException(failure); throw new InvalidOperationException("The isolated worker command failed.", failure); }
                    command.Completion.TrySetResult(result);
                }
            }
            finally { if (tracked) { workerWaiting = false; workerWait = previous; } }
        }

        /// <summary>Captures the current isolated-worker wait before releasing a handoff.</summary>
        public long WorkerWaitRevision { get { lock (sync) return waitRevision; } }

        /// <summary>The innermost rule handoff, or null when waiting for an impact or contact.</summary>
        public object CurrentWorkerHandoff { get { lock (sync) return workerWaiting ? workerWait?.Handoff : null; } }

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

        public bool ObservesSummon(FightBattleSkillCastEvent cast, FightBattleHitDefinition definition,
            FightBattleHitSelectionSnapshot hit, int centerIndex, int scopeIndex)
        {
            if (!ObserveSummons || cast?.SkillEntityId != 10710) return false;
            if (definition == null || definition.EntityId != 10711 || hit?.HitEntityId != 10711
                || definition.SummonConfigType != "FightHitSummonMonsterCfg"
                || definition.ScopeEntries == null || definition.ScopeEntries.Count != 1
                || definition.ScopeEntries[0] == null || definition.ScopeEntries[0].Delay != 0f
                || definition.ScopeEntries[0].Position.X != 0 || definition.ScopeEntries[0].Position.Y != 2
                || definition.SummonPools == null || definition.SummonPools.Count != 1
                || definition.SummonPools[0] == null || definition.SummonPools[0].MonsterId != 3181 || !definition.SummonImmediateRound
                || scopeIndex != 0 || centerIndex < 0 || hit.EffectCenters == null || centerIndex >= hit.EffectCenters.Count)
                throw new InvalidOperationException("The original stone-chief summon configuration changed.");
            return true;
        }

        public void AwaitSummonArrival(FightBattleSkillCastEvent cast, FightBattleHitSelectionSnapshot hit, int centerIndex)
        {
            var signal = new Signal(cast, hit, centerIndex, -1);
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                while (!disposed && !arrived.Contains(signal)) ParkWorker(impact: signal);
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
            }
        }

        public void SummonCreated(FightBattleUnitState unit, FightBattleHitSelectionSnapshot.SpawnedUnit receipt)
        {
            if (unit == null || receipt == null) throw new ArgumentNullException();
            var notice = new FightBattleSummonNotice { Owner = this, Unit = FightBattleCheckpointSerializer.CopyUnit(unit),
                Receipt = new FightBattleHitSelectionSnapshot.SpawnedUnit { UnitId = receipt.UnitId, X = receipt.X, Y = receipt.Y,
                    EffectCenterIndex = receipt.EffectCenterIndex, ScopeIndex = receipt.ScopeIndex, Delay = receipt.Delay,
                    HasInitialHealth = receipt.HasInitialHealth, InitialHp = receipt.InitialHp, InitialMaxHp = receipt.InitialMaxHp } };
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                summons.Enqueue(notice);
                while (!disposed && !notice.Completed) ParkWorker(handoff: notice);
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
            }
        }
        public bool TryTakeSummon(out FightBattleSummonNotice notice)
        {
            lock (sync) { if (summons.Count == 0) { notice = null; return false; }
                notice = summons.Dequeue(); notice.Taken = true; return true; }
        }
        public void CompleteSummon(FightBattleSummonNotice notice, out long releasedRevision)
        {
            if (notice == null) throw new ArgumentNullException(nameof(notice));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (!ReferenceEquals(notice.Owner, this) || !notice.Taken || notice.Completed)
                    throw new InvalidOperationException("The summon notice is not awaiting this host.");
                releasedRevision = waitRevision; notice.Completed = true; Monitor.PulseAll(sync);
            }
        }

        public void UnitLifeChanged(FightBattleUnitLifeEvent transition)
        {
            if (transition == null) throw new ArgumentNullException(nameof(transition));
            if (!ObserveUnitLives) return;
            var notice = new FightBattleUnitLifeNotice { Owner = this, Event = transition };
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                unitLives.Enqueue(notice);
                while (!disposed && !notice.Completed) ParkWorker(handoff: notice);
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
            }
        }

        public bool TryTakeUnitLife(out FightBattleUnitLifeNotice notice)
        {
            lock (sync)
            {
                if (unitLives.Count == 0) { notice = null; return false; }
                notice = unitLives.Dequeue(); notice.Taken = true; return true;
            }
        }

        public void CompleteUnitLife(FightBattleUnitLifeNotice notice, out long releasedRevision)
        {
            if (notice == null) throw new ArgumentNullException(nameof(notice));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (!ReferenceEquals(notice.Owner, this) || !notice.Taken || notice.Completed)
                    throw new InvalidOperationException("The unit life notice is not awaiting this host.");
                releasedRevision = waitRevision; notice.Completed = true; Monitor.PulseAll(sync);
            }
        }

        public void StatusViewChanged(FightBattleStatusViewEvent transition)
        {
            if (transition?.Status == null) throw new ArgumentNullException(nameof(transition));
            if (!ObserveStatusViews) return;
            var notice = new FightBattleStatusViewNotice { Owner = this, Event = transition };
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                statusViews.Enqueue(notice);
                while (!disposed && !notice.Completed) ParkWorker(handoff: notice);
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
            }
        }

        public bool TryTakeStatusView(out FightBattleStatusViewNotice notice)
        {
            lock (sync)
            {
                if (statusViews.Count == 0) { notice = null; return false; }
                notice = statusViews.Dequeue(); notice.Taken = true; return true;
            }
        }

        public void CompleteStatusView(FightBattleStatusViewNotice notice, out long releasedRevision)
        {
            if (notice == null) throw new ArgumentNullException(nameof(notice));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (!ReferenceEquals(notice.Owner, this) || !notice.Taken || notice.Completed)
                    throw new InvalidOperationException("The status view notice is not awaiting this host.");
                releasedRevision = waitRevision; notice.Completed = true; Monitor.PulseAll(sync);
            }
        }

        public void GridViewChanged(FightBattleGridViewEvent transition)
        {
            if (transition == null) throw new ArgumentNullException(nameof(transition));
            if (!ObserveGridViews) return;
            var notice = new FightBattleGridViewNotice { Owner = this, Event = transition };
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                gridViews.Enqueue(notice);
                while (!disposed && !notice.Completed) ParkWorker(handoff: notice);
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
            }
        }

        public bool TryTakeGridView(out FightBattleGridViewNotice notice)
        {
            lock (sync)
            {
                if (gridViews.Count == 0) { notice = null; return false; }
                notice = gridViews.Dequeue(); notice.Taken = true; return true;
            }
        }

        public void CompleteGridView(FightBattleGridViewNotice notice, out long releasedRevision)
        {
            if (notice == null) throw new ArgumentNullException(nameof(notice));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (!ReferenceEquals(notice.Owner, this) || !notice.Taken || notice.Completed)
                    throw new InvalidOperationException("The grid view notice is not awaiting this host.");
                releasedRevision = waitRevision; notice.Completed = true; Monitor.PulseAll(sync);
            }
        }

        public void DisperseResolved(FightBattleDamageEvent damage)
        {
            if (damage == null || !damage.HasDisperseNotification) throw new ArgumentException("A successful dispel receipt is required.", nameof(damage));
            if (!ObserveDisperses) return;
            var notice = new FightBattleDisperseNotice { Owner = this, Damage = damage };
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                disperses.Enqueue(notice);
                while (!disposed && !notice.Completed) ParkWorker(handoff: notice);
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
            }
        }

        public bool TryTakeDisperse(out FightBattleDisperseNotice notice)
        {
            lock (sync)
            {
                if (disperses.Count == 0) { notice = null; return false; }
                notice = disperses.Dequeue(); notice.Taken = true; return true;
            }
        }

        public void CompleteDisperse(FightBattleDisperseNotice notice, out long releasedRevision)
        {
            if (notice == null) throw new ArgumentNullException(nameof(notice));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (!ReferenceEquals(notice.Owner, this) || !notice.Taken || notice.Completed)
                    throw new InvalidOperationException("The dispel notice is not awaiting this host.");
                releasedRevision = waitRevision; notice.Completed = true; Monitor.PulseAll(sync);
            }
        }

        internal void SecondsStatusChanged(FightBattleUnitState owner, FightBattleStatusState status, bool started)
            => HoldSecondsStatus(owner, status, started ? FightBattleStatusViewEventKind.Start : FightBattleStatusViewEventKind.End);

        internal void SecondsStatusInvoked(FightBattleUnitState owner, FightBattleStatusState status)
            => HoldSecondsStatus(owner, status, FightBattleStatusViewEventKind.Invoke);

        private void HoldSecondsStatus(FightBattleUnitState owner, FightBattleStatusState status, FightBattleStatusViewEventKind kind)
        {
            if (owner == null || status == null) throw new ArgumentNullException(owner == null ? nameof(owner) : nameof(status));
            var notice = new FightBattleSecondsStatusNotice { Owner = this, Event = new FightBattleStatusViewEvent {
                OwnerId = owner.Id, Kind = kind, Status = FightBattleStatusViewState.Capture(status) } };
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                secondsStatuses.Enqueue(notice);
                while (!disposed && !notice.Completed) ParkWorker(handoff: notice);
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
            }
        }

        public bool TryTakeSecondsStatus(out FightBattleSecondsStatusNotice notice)
        {
            lock (sync)
            {
                if (secondsStatuses.Count == 0) { notice = null; return false; }
                notice = secondsStatuses.Dequeue(); notice.Taken = true; return true;
            }
        }

        public void CompleteSecondsStatus(FightBattleSecondsStatusNotice notice, out long releasedRevision)
        {
            if (notice == null) throw new ArgumentNullException(nameof(notice));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (!ReferenceEquals(notice.Owner, this) || !notice.Taken || notice.Completed)
                    throw new InvalidOperationException("The status notice is not awaiting this host.");
                releasedRevision = waitRevision; notice.Completed = true; Monitor.PulseAll(sync);
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
                while (!disposed && !preparation.Completed) ParkWorker(handoff: preparation);
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
                while (!disposed && !preparation.Completed) ParkWorker(handoff: preparation);
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
                if (deferredContacts.TryGetValue(movement, out var deferred) && !ReferenceEquals(deferred.Cast, cast))
                    throw new InvalidOperationException("The deferred movement belongs to another cast.");
                if (blockedMovements.ContainsKey(movement))
                    throw new InvalidOperationException("The blocked movement is already registered.");
                blockedMovements.Add(movement, preparation);
                movements.Enqueue(preparation);
                while (!disposed && !preparation.Completed) ParkWorker(handoff: preparation);
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
                    ParkWorker(blockedMovement: movement);
                }
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (preparation.ContactConsumed)
                    throw new InvalidOperationException("The blocked movement contact was already consumed.");
                preparation.ContactConsumed = true;
            }
        }

        public void BindBlockedImpact(FightBattleMovementEvent movement, FightBattleSkillCastEvent cast, int targetId)
        {
            if (movement == null) throw new ArgumentNullException(nameof(movement));
            if (cast == null) throw new ArgumentNullException(nameof(cast));
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (!ObserveMovements || !blockedMovements.TryGetValue(movement, out var preparation)
                    || !preparation.ContactConsumed || !blockedContacts.Contains(movement))
                    throw new InvalidOperationException("The action has not consumed its actual blocked contact.");
                if (cast.SkillEntityId != 10100 || cast.ActorId <= 0 || targetId <= 0
                    || preparation.Cast != null && preparation.Cast.ActorId != cast.ActorId
                    || cast.TargetIds == null || cast.TargetIds.Count != 1 || cast.TargetIds[0] != targetId
                    || blockedImpacts.ContainsKey(cast) || !boundBlockedTargets.Add((movement, targetId)))
                    throw new InvalidOperationException("The collision child does not identify a unique contact target.");
                blockedImpacts.Add(cast, (movement, targetId));
            }
        }

        public bool TryDeferBlockedContact(FightBattleSkillCastEvent cast, FightBattleMovementEvent movement, Action onContact)
        {
            if (cast == null) throw new ArgumentNullException(nameof(cast));
            if (movement == null) throw new ArgumentNullException(nameof(movement));
            if (onContact == null) throw new ArgumentNullException(nameof(onContact));
            DeferredContact deferred;
            bool contactAlreadyReached;
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (!ObserveMovements || workerThread != Thread.CurrentThread.ManagedThreadId) return false;
                blockedMovements.TryGetValue(movement, out var preparation);
                if (preparation != null && !ReferenceEquals(preparation.Cast, cast))
                    throw new InvalidOperationException("The blocked movement belongs to another cast.");
                if (preparation?.ContactConsumed == true || deferredContacts.ContainsKey(movement))
                    throw new InvalidOperationException("The blocked contact already has a consumer.");
                deferred = new DeferredContact { Cast = cast, Callback = onContact };
                deferredContacts.Add(movement, deferred);
                contactAlreadyReached = preparation?.Completed == true && blockedContacts.Contains(movement);
                if (contactAlreadyReached) deferred.Queued = true;
            }
            if (contactAlreadyReached) RunDeferredContact(movement, deferred);
            return true;
        }

        public void AwaitDeferredContacts(FightBattleSkillCastEvent cast)
        {
            if (cast == null) throw new ArgumentNullException(nameof(cast));
            lock (sync)
            {
                if (!HasDeferredContacts(cast)) return;
                if (workerThread != Thread.CurrentThread.ManagedThreadId)
                    throw new InvalidOperationException("Only the rule worker can wait for deferred contacts.");
                while (!disposed && HasDeferredContacts(cast)) ParkWorker();
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
            }
        }

        private bool HasDeferredContacts(FightBattleSkillCastEvent cast)
        {
            foreach (var value in deferredContacts.Values)
                if (ReferenceEquals(value.Cast, cast)) return true;
            return false;
        }

        private void RunDeferredContact(FightBattleMovementEvent movement, DeferredContact deferred)
        {
            lock (sync)
            {
                if (disposed) throw new OperationCanceledException("The battle impact rendezvous was disposed.");
                if (!blockedContacts.Contains(movement) || !blockedMovements.TryGetValue(movement, out var preparation)
                    || !preparation.Completed || preparation.ContactConsumed || !deferredContacts.TryGetValue(movement, out var pending)
                    || !ReferenceEquals(pending, deferred))
                    throw new InvalidOperationException("The deferred contact is not awaiting this worker.");
                preparation.ContactConsumed = true;
            }
            try { deferred.Callback(); }
            finally
            {
                lock (sync) { deferredContacts.Remove(movement); Monitor.PulseAll(sync); }
            }
        }

        private void QueueDeferredContact(FightBattleMovementEvent movement, DeferredContact deferred)
        {
            deferred.Queued = true;
            workerCommands.Enqueue(new WorkerCommand {
                AllowNested = true,
                Completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
                Execute = () => { RunDeferredContact(movement, deferred); return true; }
            });
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
                if (added && preparation.Completed && deferredContacts.TryGetValue(movement, out var deferred) && !deferred.Queued)
                {
                    QueueDeferredContact(movement, deferred);
                    if (workerWaiting && workerWait != null) releasedRevision = workerWait.Revision;
                }
                else if (added && workerWait != null && ReferenceEquals(workerWait.BlockedMovement, movement))
                    releasedRevision = workerWait.Revision;
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
                if (preparation.HasBlockedPosition && blockedContacts.Contains(preparation.Movement)
                    && deferredContacts.TryGetValue(preparation.Movement, out var deferred) && !deferred.Queued)
                    QueueDeferredContact(preparation.Movement, deferred);
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
                while (!disposed && !completed.Completed) ParkWorker(handoff: completed);
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
                while (!disposed && !selected.Completed) ParkWorker(handoff: selected);
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
                while (!disposed && !settlement.Completed) ParkWorker(handoff: settlement);
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
                while (!disposed && !settlement.Completed) ParkWorker(handoff: settlement);
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
                if (blockedImpacts.TryGetValue(impact.Cast, out var blocked))
                {
                    if (impact.SkillEntityId != 10100 || impact.HitEntityId != 10101 || impact.DamageEntityId != 10006
                        || impact.SourceId != impact.Cast.ActorId || impact.TargetId != blocked.TargetId
                        || impact.HitSelection.HitEntityId != 10101 || impact.Cast.HitSelections == null
                        || !impact.Cast.HitSelections.Contains(impact.HitSelection)
                        || !blockedMovements.TryGetValue(blocked.Movement, out var preparation)
                        || !preparation.ContactConsumed || !blockedContacts.Contains(blocked.Movement))
                        throw new InvalidOperationException("The impact does not match its consumed collision contact.");
                    requests.Enqueue(impact);
                    blockedImpacts.Remove(impact.Cast);
                    return;
                }
                requests.Enqueue(impact);
                while (!disposed && !arrived.Contains(signal))
                {
                    ParkWorker(impact: signal);
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
                if (workerWait?.Impact is Signal waitingImpact && waitingImpact.Equals(signal)) releasedRevision = workerWait.Revision;
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
                disposed = true; workerWait = null; CancelWorkerCommands(); arrived.Clear(); preparedCasts.Clear(); requests.Clear(); preparations.Clear(); movements.Clear(); blockedMovements.Clear(); blockedContacts.Clear(); deferredContacts.Clear(); blockedImpacts.Clear(); boundBlockedTargets.Clear(); completedCasts.Clear(); selections.Clear(); settlements.Clear(); secondsStatuses.Clear(); statusViews.Clear(); gridViews.Clear(); disperses.Clear(); unitLives.Clear(); summons.Clear(); deferredStatusResults.Clear();
                Monitor.PulseAll(sync);
            }
        }
    }
}
