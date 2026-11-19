using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Zjcs.Combat.Official;

public static class BattleImpactGateHarness
{
    private sealed class Pending
    {
        public FightBattlePendingImpact Impact;
        public ManualResetEventSlim Released = new ManualResetEventSlim(false);
    }
    private sealed class Gate : IFightBattleImpactGate, IFightBattleCastPreparationObserver, IFightBattleImpactSettlementObserver, IFightBattleImpactStatusResolutionObserver
    {
        public readonly ConcurrentQueue<(int SkillItemId, bool HasStatusResult, bool StatusTriggered)> Settlements =
            new ConcurrentQueue<(int, bool, bool)>();
        public void ImpactSettled(FightBattlePendingImpact impact, FightBattleDamageEvent damage)
            => Settlements.Enqueue((impact.SkillItemId, damage.HasDamageTriggerStatus, damage.IsDamageTriggerStatus));
        public readonly ConcurrentQueue<(FightBattlePendingImpact Impact, int DamageId, bool Known, bool Triggered)> StatusResults =
            new ConcurrentQueue<(FightBattlePendingImpact, int, bool, bool)>();
        public void ImpactStatusResolved(FightBattlePendingImpact impact, FightBattleDamageEvent damage)
            => StatusResults.Enqueue((impact, damage.DamageId, damage.HasDamageTriggerStatus, damage.IsDamageTriggerStatus));
        public readonly BlockingCollection<Pending> Items = new BlockingCollection<Pending>();
        public readonly ConcurrentQueue<FightBattleSkillCastEvent> Prepared = new ConcurrentQueue<FightBattleSkillCastEvent>();
        public void CastPrepared(FightBattleSkillCastEvent cast) { Prepared.Enqueue(cast); }
        public void AwaitImpact(FightBattlePendingImpact impact)
        {
            var pending = new Pending { Impact = impact };
            Items.Add(pending);
            if (!pending.Released.Wait(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("The impact was never released by the presentation.");
        }
        public Pending Next()
        {
            if (!Items.TryTake(out var pending, TimeSpan.FromSeconds(20)))
                throw new TimeoutException("The expected impact was not emitted.");
            return pending;
        }
    }
    private static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Impact gate: " + message); }
    private static FightBattleUnitState Unit(int id, int side, int x, int y, FightDamageFormula formula)
    {
        var unit = new FightBattleUnitState { Id = id, Side = side, CharacterType = 1, Profession = 1,
            Level = 50, SubRank = 5, X = x, Y = y, Hp = 1000000, MaxHp = 1000000,
            LastMaxHp = 1000000, MoveDist = 0, MoveMask = 5 };
        unit.Turn.Id = id; unit.Turn.Hp = unit.Hp; unit.Turn.Speed = id == 1 ? 1000 : 10;
        foreach (var value in new Dictionary<int, long> { {1, 1000}, {2, 10}, {3, unit.MaxHp}, {4, unit.Turn.Speed} })
            unit.Properties.Add(new FightPropertyValue { Id = value.Key, Value = value.Value });
        foreach (var name in new[] { "BaseBlockPercentValue", "BaseBlockAvoidPercentValue", "BaseCritRatePercentValue",
            "BaseCritAvoidPercentValue", "BaseEffectRate", "BaseEffectDodge" })
            unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId(name), Value = 10000 });
        unit.Properties.Add(new FightPropertyValue { Id = formula.PropertyId("CritAvoidPercent"), Value = 10000 });
        return unit;
    }
    public static string Run(FightOfficialCombatCatalog catalog, FightDamageFormula formula, FightOfficialSkillItemCatalog items)
    {
        {
            using var latch = new FightBattleImpactLatch();
            var parent = new FightBattleSkillCastEvent(); var child = new FightBattleSkillCastEvent();
            int workerThread = 0, commandThread = 0;
            var worker = Task.Run(() => {
                latch.BindWorker(); workerThread = Thread.CurrentThread.ManagedThreadId;
                try { latch.CastPrepared(parent); return true; }
                finally { latch.FinishWorker(); }
            });
            FightBattlePreparedCast held = null;
            Check(SpinWait.SpinUntil(() => latch.TryTakePreparation(out held), 20000), "Worker command fixture missing its parent");
            Check(latch.TryQueueWorkerCommand(() => {
                commandThread = Thread.CurrentThread.ManagedThreadId;
                latch.CastPrepared(child); return true;
            }, out var command), "A held worker did not accept its command");
            FightBattlePreparedCast nested = null;
            Check(SpinWait.SpinUntil(() => latch.TryTakePreparation(out nested), 20000)
                && ReferenceEquals(nested.Cast, child), "A worker command lost its nested handoff");
            Check(!latch.TryQueueWorkerCommand(() => true, out _) && !command.IsCompleted && !worker.IsCompleted,
                "A second command overtook a nested worker command");
            latch.CompletePreparation(nested);
            Check(command.Wait(20000) && command.Result && commandThread == workerThread
                && commandThread != Thread.CurrentThread.ManagedThreadId && !worker.IsCompleted,
                "A worker command ran on the host or released its parent handoff");
            latch.CompletePreparation(held);
            Check(worker.Wait(20000) && worker.Result, "The held parent did not resume after its command");
        }
        {
            using var latch = new FightBattleImpactLatch();
            var cast = new FightBattleSkillCastEvent();
            var selection = new FightBattleHitSelectionSnapshot(); selection.EffectCenters.Add(new FightGridPoint(1, 1));
            selection.Targets.Add(new FightBattleHitSelectionSnapshot.TargetOccurrence { TargetId = 2 });
            var impact = new FightBattlePendingImpact { Cast = cast, HitSelection = selection, TargetId = 2 };
            var child = new FightBattleSkillCastEvent();
            var worker = Task.Run(() => {
                latch.BindWorker();
                try { latch.AwaitImpact(impact); return true; }
                finally { latch.FinishWorker(); }
            });
            Check(SpinWait.SpinUntil(() => latch.TryTake(out _), 20000), "Worker command impact fixture missing");
            Check(latch.TryQueueWorkerCommand(() => { latch.CastPrepared(child); return true; }, out var command),
                "Impact wait did not accept a worker command");
            FightBattlePreparedCast nested = null;
            Check(SpinWait.SpinUntil(() => latch.TryTakePreparation(out nested), 20000), "Nested impact command missing");
            Check(latch.RecordArrival(cast, selection, 0, -1, out long earlyRevision) && earlyRevision == 0,
                "A parent arrival must not release an unrelated nested handoff");
            latch.CompletePreparation(nested);
            Check(command.Wait(20000) && command.Result && worker.Wait(20000) && worker.Result,
                "A parent arrival was lost while the nested command ran");
        }
        {
            using var latch = new FightBattleImpactLatch();
            using var started = new ManualResetEventSlim(); using var resume = new ManualResetEventSlim();
            var cast = new FightBattleSkillCastEvent();
            var selection = new FightBattleHitSelectionSnapshot(); selection.EffectCenters.Add(new FightGridPoint(1, 1));
            selection.Targets.Add(new FightBattleHitSelectionSnapshot.TargetOccurrence { TargetId = 2 });
            var impact = new FightBattlePendingImpact { Cast = cast, HitSelection = selection, TargetId = 2 };
            var worker = Task.Run(() => {
                latch.BindWorker();
                try { latch.AwaitImpact(impact); return true; }
                finally { latch.FinishWorker(); }
            });
            Check(SpinWait.SpinUntil(() => latch.TryTake(out _), 20000), "Concurrent arrival fixture missing");
            Check(latch.TryQueueWorkerCommand(() => { started.Set(); return resume.Wait(20000); }, out var command)
                && started.Wait(20000), "Worker command did not start");
            Check(latch.RecordArrival(cast, selection, 0, -1, out long revision) && revision > 0,
                "An arrival during the command lost its worker fence");
            resume.Set(); latch.WaitForWorkerBoundary(revision);
            Check(command.Wait(20000) && command.Result && worker.Wait(20000) && worker.Result,
                "The fenced arrival did not resume after the worker command");
        }
        {
            using var latch = new FightBattleImpactLatch { ObserveMovements = true };
            using var started = new ManualResetEventSlim(); using var resume = new ManualResetEventSlim();
            var move = new FightBattleMovementEvent();
            var worker = Task.Run(() => {
                latch.BindWorker();
                try { latch.BlockedMovementPrepared(null, move, new FightGridPoint(1, 2)); latch.AwaitBlockedContact(move); return true; }
                finally { latch.FinishWorker(); }
            });
            FightBattlePreparedMovement movement = null;
            Check(SpinWait.SpinUntil(() => latch.TryTakeMovement(out movement), 20000), "Concurrent collision fixture missing");
            latch.CompleteMovement(movement, out long movementRevision); latch.WaitForWorkerBoundary(movementRevision);
            Check(latch.TryQueueWorkerCommand(() => { started.Set(); return resume.Wait(20000); }, out var command)
                && started.Wait(20000), "Collision clock command did not start");
            Check(latch.RecordBlockedContact(move, out long revision) && revision > 0,
                "A collision during the command lost its worker fence");
            resume.Set(); latch.WaitForWorkerBoundary(revision);
            Check(command.Wait(20000) && command.Result && worker.Wait(20000) && worker.Result,
                "The fenced collision did not resume after the worker command");
        }
        {
            using var latch = new FightBattleImpactLatch();
            var worker = Task.Run(() => {
                latch.BindWorker();
                try { latch.CastPrepared(new FightBattleSkillCastEvent()); }
                finally { latch.FinishWorker(); }
            });
            Check(SpinWait.SpinUntil(() => latch.TryTakePreparation(out _), 20000), "Failed command fixture missing");
            Check(latch.TryQueueWorkerCommand(() => throw new InvalidOperationException("clock failure"), out var command),
                "Failed command fixture did not queue");
            bool commandFailed = false, workerFailed = false;
            try { command.GetAwaiter().GetResult(); } catch (InvalidOperationException error) { commandFailed = error.Message == "clock failure"; }
            try { worker.GetAwaiter().GetResult(); } catch (InvalidOperationException error) { workerFailed = error.InnerException?.Message == "clock failure"; }
            Check(commandFailed && workerFailed, "A failed command must fail the isolated worker as well");
        }
        {
            var latch = new FightBattleImpactLatch();
            var worker = Task.Run(() => {
                latch.BindWorker();
                try { latch.CastPrepared(new FightBattleSkillCastEvent()); }
                finally { latch.FinishWorker(); }
            });
            Check(SpinWait.SpinUntil(() => latch.TryTakePreparation(out _), 20000), "Canceled command fixture missing");
            Check(latch.TryQueueWorkerCommand(() => { latch.CastPrepared(new FightBattleSkillCastEvent()); return true; }, out var command),
                "Canceled command did not queue");
            Check(SpinWait.SpinUntil(() => latch.TryTakePreparation(out _), 20000), "Canceled nested command did not start");
            latch.Dispose();
            bool commandCanceled = false, workerCanceled = false;
            try { command.GetAwaiter().GetResult(); } catch (OperationCanceledException) { commandCanceled = true; }
            try { worker.GetAwaiter().GetResult(); } catch (InvalidOperationException error) { workerCanceled = error.InnerException is OperationCanceledException; }
            Check(commandCanceled && workerCanceled, "Disposal must release a command suspended in a nested handoff");
        }
        {
            using var blocked = new FightBattleImpactLatch { ObserveMovements = true };
            var cast = new FightBattleSkillCastEvent { ActorId = 1 };
            var move = new FightBattleMovementEvent();
            var collisionCast = new FightBattleSkillCastEvent { SkillEntityId = 10100, ActorId = 1 };
            collisionCast.TargetIds.Add(2);
            var selection = new FightBattleHitSelectionSnapshot { HitEntityId = 10101 };
            selection.EffectCenters.Add(new FightGridPoint(4, 5));
            selection.Targets.Add(new FightBattleHitSelectionSnapshot.TargetOccurrence { TargetId = 2 });
            selection.Targets.Add(new FightBattleHitSelectionSnapshot.TargetOccurrence { TargetId = 3 });
            collisionCast.HitSelections.Add(selection);
            var collisionImpact = new FightBattlePendingImpact { Cast = collisionCast, HitSelection = selection,
                SkillEntityId = 10100, HitEntityId = 10101, DamageEntityId = 10006, SourceId = 1, TargetId = 2 };
            var worker = Task.Run(() => {
                blocked.BindWorker();
                try {
                    blocked.BlockedMovementPrepared(cast, move, new FightGridPoint(4, 5));
                    blocked.AwaitBlockedContact(move);
                    var wrongSource = new FightBattleSkillCastEvent { ActorId = 9, SkillEntityId = 10100 };
                    wrongSource.TargetIds.Add(2);
                    bool rejectedWrongSource = false;
                    try { blocked.BindBlockedImpact(move, wrongSource, 2); }
                    catch (InvalidOperationException) { rejectedWrongSource = true; }
                    blocked.BindBlockedImpact(move, collisionCast, 2);
                    var wrong = new FightBattlePendingImpact { Cast = collisionCast, HitSelection = selection,
                        SkillEntityId = 10100, HitEntityId = 10101, DamageEntityId = 10006,
                        SourceId = 1, TargetId = 3, OccurrenceIndex = 1 };
                    bool rejectedWrongTarget = false, rejectedDuplicate = false, rejectedContact = false;
                    try { blocked.AwaitImpact(wrong); }
                    catch (InvalidOperationException) { rejectedWrongTarget = true; }
                    blocked.AwaitImpact(collisionImpact);
                    try { blocked.BindBlockedImpact(move, collisionCast, 2); }
                    catch (InvalidOperationException) { rejectedDuplicate = true; }
                    try { blocked.AwaitBlockedContact(move); }
                    catch (InvalidOperationException) { rejectedContact = true; }
                    return rejectedWrongSource && rejectedWrongTarget && rejectedDuplicate && rejectedContact;
                }
                finally { blocked.FinishWorker(); }
            });
            FightBattlePreparedMovement prepared = null;
            Check(SpinWait.SpinUntil(() => blocked.TryTakeMovement(out prepared), 20000)
                && prepared.HasBlockedPosition && prepared.BlockedPosition.Equals(new FightGridPoint(4, 5))
                && ReferenceEquals(prepared.Cast, cast) && ReferenceEquals(prepared.Movement, move),
                "Blocked movement must retain its exact cast, action and obstacle position");
            blocked.CompleteMovement(prepared, out long startRevision);
            blocked.WaitForWorkerBoundary(startRevision);
            Check(!worker.IsCompleted, "Collision rules must wait for actual contact");
            bool rejected = false;
            try { blocked.BindBlockedImpact(move, collisionCast, 2); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "A child cannot bind to a collision before actual contact");
            rejected = false;
            try { blocked.RecordBlockedContact(new FightBattleMovementEvent(), out _); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected && !worker.IsCompleted, "Another movement cannot release a blocked action");
            Check(blocked.RecordBlockedContact(move, out long contactRevision) && contactRevision > 0,
                "The actual collision did not release its waiting worker");
            Check(!blocked.RecordBlockedContact(move, out _), "Repeated contact must not be delivered twice");
            blocked.WaitForWorkerBoundary(contactRevision);
            Check(worker.GetAwaiter().GetResult(), "The blocked movement must consume its contact exactly once");
        }
        {
            using var blocked = new FightBattleImpactLatch { ObserveMovements = true };
            var move = new FightBattleMovementEvent();
            var worker = Task.Run(() => {
                blocked.BindWorker();
                try {
                    blocked.BlockedMovementPrepared(null, move, new FightGridPoint(2, 3));
                    blocked.AwaitBlockedContact(move);
                    return true;
                }
                finally { blocked.FinishWorker(); }
            });
            FightBattlePreparedMovement prepared = null;
            Check(SpinWait.SpinUntil(() => blocked.TryTakeMovement(out prepared), 20000), "Early collision fixture missing");
            Check(blocked.RecordBlockedContact(move, out long earlyRevision) && earlyRevision == 0 && !worker.IsCompleted,
                "Contact arriving during action start must be retained without releasing preparation");
            blocked.CompleteMovement(prepared, out long startRevision);
            blocked.WaitForWorkerBoundary(startRevision);
            Check(worker.GetAwaiter().GetResult(), "Early contact must release its own later wait");
        }
        {
            var blocked = new FightBattleImpactLatch { ObserveMovements = true };
            var move = new FightBattleMovementEvent();
            var worker = Task.Run(() => {
                blocked.BindWorker();
                try { blocked.BlockedMovementPrepared(null, move, new FightGridPoint(1, 2)); blocked.AwaitBlockedContact(move); return false; }
                catch (OperationCanceledException) { return true; }
                finally { blocked.FinishWorker(); }
            });
            FightBattlePreparedMovement prepared = null;
            Check(SpinWait.SpinUntil(() => blocked.TryTakeMovement(out prepared), 20000), "Canceled collision fixture missing");
            blocked.CompleteMovement(prepared, out long startRevision);
            blocked.WaitForWorkerBoundary(startRevision);
            blocked.Dispose();
            Check(worker.GetAwaiter().GetResult(), "Canceling the collision wait must release the worker");
        }
        {
            using var blocked = new FightBattleImpactLatch { ObserveMovements = true };
            var move = new FightBattleMovementEvent();
            var nested = new FightBattleSkillCastEvent();
            var worker = Task.Run(() => {
                blocked.BindWorker();
                try {
                    blocked.BlockedMovementPrepared(null, move, new FightGridPoint(1, 2));
                    blocked.CastPrepared(nested);
                    blocked.AwaitBlockedContact(move);
                    return true;
                }
                finally { blocked.FinishWorker(); }
            });
            FightBattlePreparedMovement movement = null;
            Check(SpinWait.SpinUntil(() => blocked.TryTakeMovement(out movement), 20000), "Nested collision fixture missing");
            blocked.CompleteMovement(movement, out long startRevision);
            blocked.WaitForWorkerBoundary(startRevision);
            FightBattlePreparedCast child = null;
            Check(blocked.TryTakePreparation(out child) && ReferenceEquals(child.Cast, nested), "Action-start child must keep its own boundary");
            Check(blocked.RecordBlockedContact(move, out long contactRevision) && contactRevision == 0 && !worker.IsCompleted,
                "Contact during a nested action must not release its unrelated preparation");
            blocked.CompletePreparation(child, out long childRevision);
            blocked.WaitForWorkerBoundary(childRevision);
            Check(worker.GetAwaiter().GetResult(), "Nested action must retain the earlier collision signal");
        }
        foreach (bool contactWhileNextHeld in new[] { false, true })
        {
            using var deferredContact = new FightBattleImpactLatch { ObserveMovements = true };
            var cast = new FightBattleSkillCastEvent { ActorId = 1 };
            var next = new FightBattleSkillCastEvent { ActorId = 1 };
            var child = new FightBattleSkillCastEvent { ActorId = 1, SkillEntityId = 10100 };
            child.TargetIds.Add(2);
            var move = new FightBattleMovementEvent();
            int contacts = 0, nextFinished = 0;
            var worker = Task.Run(() => {
                deferredContact.BindWorker();
                try {
                    Check(deferredContact.TryDeferBlockedContact(cast, move, () => {
                        deferredContact.BindBlockedImpact(move, child, 2); contacts++;
                        deferredContact.CastPrepared(child);
                    }), "A started blocked action was not deferred");
                    deferredContact.BlockedMovementPrepared(cast, move, new FightGridPoint(1, 2));
                    deferredContact.CastPrepared(next); nextFinished++;
                    deferredContact.AwaitDeferredContacts(cast);
                    return true;
                }
                finally { deferredContact.FinishWorker(); }
            });
            FightBattlePreparedMovement movement = null;
            Check(SpinWait.SpinUntil(() => deferredContact.TryTakeMovement(out movement), 20000),
                "Deferred collision fixture did not start its action");
            deferredContact.CompleteMovement(movement, out long startRevision);
            deferredContact.WaitForWorkerBoundary(startRevision);
            FightBattlePreparedCast nextPreparation = null;
            Check(deferredContact.TryTakePreparation(out nextPreparation) && ReferenceEquals(nextPreparation.Cast, next)
                && contacts == 0, "A later hit could not proceed while collision was pending");
            Check(ReferenceEquals(deferredContact.CurrentWorkerHandoff, nextPreparation),
                "The later preparation must identify the currently suspended rule work");
            if (!contactWhileNextHeld)
            {
                deferredContact.CompletePreparation(nextPreparation, out long nextRevision);
                deferredContact.WaitForWorkerBoundary(nextRevision);
                Check(nextFinished == 1 && !worker.IsCompleted && contacts == 0,
                    "The cast must wait for its still-pending collision only after the later hit");
            }
            Check(deferredContact.RecordBlockedContact(move, out long contactRevision) && contactRevision > 0,
                "Actual contact did not schedule the deferred collision on the worker");
            deferredContact.WaitForWorkerBoundary(contactRevision);
            FightBattlePreparedCast childPreparation = null;
            Check(deferredContact.TryTakePreparation(out childPreparation) && ReferenceEquals(childPreparation.Cast, child)
                && contacts == 1 && !deferredContact.RecordBlockedContact(move, out _),
                "The collision child must start exactly once after actual contact");
            Check(ReferenceEquals(deferredContact.CurrentWorkerHandoff, childPreparation),
                "A nested collision must replace the outer handoff until it finishes");
            deferredContact.CompletePreparation(childPreparation, out long childRevision);
            deferredContact.WaitForWorkerBoundary(childRevision);
            if (contactWhileNextHeld)
            {
                Check(nextFinished == 0 && !worker.IsCompleted, "A nested collision released another held hit");
                Check(ReferenceEquals(deferredContact.CurrentWorkerHandoff, nextPreparation),
                    "The outer handoff must become current again after the collision child finishes");
                deferredContact.CompletePreparation(nextPreparation, out long nextRevision);
                deferredContact.WaitForWorkerBoundary(nextRevision);
            }
            Check(worker.GetAwaiter().GetResult() && contacts == 1 && nextFinished == 1,
                "Deferred collision did not finish the original cast safely");
        }
        {
            using var statusContact = new FightBattleImpactLatch { ObserveMovements = true };
            var cast = new FightBattleSkillCastEvent { ActorId = 1 };
            var child = new FightBattleSkillCastEvent { ActorId = 1, SkillEntityId = 10100 };
            child.TargetIds.Add(2);
            var move = new FightBattleMovementEvent();
            var owner = Unit(1, 1, 7, 7, formula);
            var status = new FightBattleStatusState { ClassId = 24269, InstanceId = 1 };
            int contacts = 0, statusFinished = 0;
            var worker = Task.Run(() => {
                statusContact.BindWorker();
                try {
                    Check(statusContact.TryDeferBlockedContact(cast, move, () => {
                        statusContact.BindBlockedImpact(move, child, 2); contacts++;
                        statusContact.CastPrepared(child);
                    }), "Status overlap could not defer the collision");
                    statusContact.BlockedMovementPrepared(cast, move, new FightGridPoint(1, 2));
                    statusContact.SecondsStatusChanged(owner, status, true); statusFinished++;
                    statusContact.AwaitDeferredContacts(cast);
                    return true;
                }
                finally { statusContact.FinishWorker(); }
            });
            FightBattlePreparedMovement movement = null;
            Check(SpinWait.SpinUntil(() => statusContact.TryTakeMovement(out movement), 20000),
                "Status overlap did not prepare its movement");
            statusContact.CompleteMovement(movement, out long movementRevision);
            statusContact.WaitForWorkerBoundary(movementRevision);
            FightBattleSecondsStatusNotice notice = null;
            Check(statusContact.TryTakeSecondsStatus(out notice) && ReferenceEquals(statusContact.CurrentWorkerHandoff, notice),
                "The original status handoff was not current");
            Check(statusContact.RecordBlockedContact(move, out long contactRevision) && contactRevision > 0,
                "A collision during a status handoff was not scheduled");
            statusContact.WaitForWorkerBoundary(contactRevision);
            FightBattlePreparedCast collision = null;
            Check(statusContact.TryTakePreparation(out collision) && ReferenceEquals(collision.Cast, child)
                && ReferenceEquals(statusContact.CurrentWorkerHandoff, collision) && statusFinished == 0,
                "The collision child must suspend the unrelated status handoff");
            statusContact.CompletePreparation(collision, out long childRevision);
            statusContact.WaitForWorkerBoundary(childRevision);
            Check(ReferenceEquals(statusContact.CurrentWorkerHandoff, notice) && statusFinished == 0,
                "The original status handoff did not resume after collision");
            statusContact.CompleteSecondsStatus(notice, out long statusRevision);
            statusContact.WaitForWorkerBoundary(statusRevision);
            Check(worker.GetAwaiter().GetResult() && contacts == 1 && statusFinished == 1,
                "Status and collision did not finish exactly once");
        }
        {
            using var early = new FightBattleImpactLatch { ObserveMovements = true };
            var cast = new FightBattleSkillCastEvent { ActorId = 1 };
            var move = new FightBattleMovementEvent();
            int contacts = 0;
            var worker = Task.Run(() => {
                early.BindWorker();
                try {
                    Check(early.TryDeferBlockedContact(cast, move, () => contacts++), "Early contact could not be deferred");
                    early.BlockedMovementPrepared(cast, move, new FightGridPoint(1, 2));
                    early.AwaitDeferredContacts(cast);
                    return true;
                }
                finally { early.FinishWorker(); }
            });
            FightBattlePreparedMovement preparation = null;
            Check(SpinWait.SpinUntil(() => early.TryTakeMovement(out preparation), 20000), "Early deferred action missing");
            Check(early.RecordBlockedContact(move, out long contactRevision) && contactRevision == 0,
                "A contact during movement preparation must be retained");
            early.CompleteMovement(preparation, out long startRevision);
            early.WaitForWorkerBoundary(startRevision);
            Check(worker.GetAwaiter().GetResult() && contacts == 1, "Early deferred contact was not consumed once");
        }
        {
            using var queued = new FightBattleImpactLatch { ObserveMovements = true };
            using var running = new ManualResetEventSlim();
            using var proceed = new ManualResetEventSlim();
            var cast = new FightBattleSkillCastEvent { ActorId = 1 };
            var next = new FightBattleSkillCastEvent { ActorId = 1 };
            var child = new FightBattleSkillCastEvent { ActorId = 1, SkillEntityId = 10100 };
            child.TargetIds.Add(2); var move = new FightBattleMovementEvent(); int contacts = 0;
            var worker = Task.Run(() => {
                queued.BindWorker();
                try {
                    Check(queued.TryDeferBlockedContact(cast, move, () => {
                        queued.BindBlockedImpact(move, child, 2); contacts++; queued.CastPrepared(child);
                    }), "Busy-worker contact did not defer");
                    queued.BlockedMovementPrepared(cast, move, new FightGridPoint(1, 2));
                    running.Set();
                    if (!proceed.Wait(20000)) throw new TimeoutException("The fixture did not resume its worker.");
                    queued.CastPrepared(next); queued.AwaitDeferredContacts(cast); return true;
                }
                finally { queued.FinishWorker(); }
            });
            FightBattlePreparedMovement movement = null;
            Check(SpinWait.SpinUntil(() => queued.TryTakeMovement(out movement), 20000), "Busy-worker action missing");
            queued.CompleteMovement(movement, out _);
            Check(running.Wait(20000), "The worker did not leave movement preparation");
            Check(queued.RecordBlockedContact(move, out long revision) && revision == 0 && contacts == 0,
                "A contact while the worker runs must be queued without a false wait revision");
            proceed.Set();
            FightBattlePreparedCast outer = null, inner = null;
            Check(SpinWait.SpinUntil(() => queued.TryTakePreparation(out outer), 20000)
                && ReferenceEquals(outer.Cast, next), "The outer preparation was lost");
            Check(SpinWait.SpinUntil(() => queued.TryTakePreparation(out inner), 20000)
                && ReferenceEquals(inner.Cast, child) && contacts == 1, "The queued contact lost its wakeup");
            queued.CompletePreparation(inner, out long childRevision); queued.WaitForWorkerBoundary(childRevision);
            queued.CompletePreparation(outer, out long outerRevision); queued.WaitForWorkerBoundary(outerRevision);
            Check(worker.GetAwaiter().GetResult() && contacts == 1, "Queued contact did not complete safely");
        }
        {
            using var nestedContacts = new FightBattleImpactLatch { ObserveMovements = true };
            var cast = new FightBattleSkillCastEvent { ActorId = 1 };
            var moves = new[] { new FightBattleMovementEvent(), new FightBattleMovementEvent() };
            var children = new[] { new FightBattleSkillCastEvent { ActorId = 1, SkillEntityId = 10100 },
                new FightBattleSkillCastEvent { ActorId = 1, SkillEntityId = 10100 } };
            children[0].TargetIds.Add(2); children[1].TargetIds.Add(3);
            var contacts = new List<int>();
            var worker = Task.Run(() => {
                nestedContacts.BindWorker();
                try {
                    for (int i = 0; i < 2; i++) {
                        int index = i;
                        Check(nestedContacts.TryDeferBlockedContact(cast, moves[index], () => {
                            nestedContacts.BindBlockedImpact(moves[index], children[index], index + 2);
                            contacts.Add(index); nestedContacts.CastPrepared(children[index]);
                        }), "A concurrent contact did not defer");
                        nestedContacts.BlockedMovementPrepared(cast, moves[index], new FightGridPoint(index + 1, 2));
                    }
                    nestedContacts.AwaitDeferredContacts(cast); return true;
                }
                finally { nestedContacts.FinishWorker(); }
            });
            for (int i = 0; i < 2; i++) {
                FightBattlePreparedMovement movement = null;
                Check(SpinWait.SpinUntil(() => nestedContacts.TryTakeMovement(out movement), 20000)
                    && ReferenceEquals(movement.Movement, moves[i]), "Concurrent blocked action missing");
                nestedContacts.CompleteMovement(movement, out long revision); nestedContacts.WaitForWorkerBoundary(revision);
            }
            Check(nestedContacts.RecordBlockedContact(moves[0], out long firstRevision) && firstRevision > 0,
                "First concurrent contact did not reach the worker");
            nestedContacts.WaitForWorkerBoundary(firstRevision);
            FightBattlePreparedCast first = null;
            Check(nestedContacts.TryTakePreparation(out first) && ReferenceEquals(first.Cast, children[0]),
                "First collision child was not prepared");
            Check(nestedContacts.RecordBlockedContact(moves[1], out long secondRevision) && secondRevision > 0,
                "A second contact could not interrupt the first child handoff");
            nestedContacts.WaitForWorkerBoundary(secondRevision);
            FightBattlePreparedCast second = null;
            Check(nestedContacts.TryTakePreparation(out second) && ReferenceEquals(second.Cast, children[1]),
                "Nested collision child was not prepared");
            nestedContacts.CompletePreparation(second, out long childRevision); nestedContacts.WaitForWorkerBoundary(childRevision);
            Check(!worker.IsCompleted && contacts.SequenceEqual(new[] { 0, 1 }),
                "The first collision must remain held while its nested contact finishes");
            nestedContacts.CompletePreparation(first, out long firstChildRevision); nestedContacts.WaitForWorkerBoundary(firstChildRevision);
            Check(worker.GetAwaiter().GetResult(), "Nested contacts did not complete their parent cast");
        }
        {
            var canceled = new FightBattleImpactLatch { ObserveMovements = true };
            var cast = new FightBattleSkillCastEvent { ActorId = 1 };
            var move = new FightBattleMovementEvent(); int contacts = 0;
            var worker = Task.Run(() => {
                canceled.BindWorker();
                try {
                    canceled.BlockedMovementPrepared(cast, move, new FightGridPoint(1, 2));
                    Check(canceled.TryDeferBlockedContact(cast, move, () => contacts++), "Canceled contact did not defer");
                    canceled.AwaitDeferredContacts(cast); return false;
                }
                catch (OperationCanceledException) { return true; }
                finally { canceled.FinishWorker(); }
            });
            FightBattlePreparedMovement preparation = null;
            Check(SpinWait.SpinUntil(() => canceled.TryTakeMovement(out preparation), 20000), "Canceled deferred action missing");
            canceled.CompleteMovement(preparation, out long startRevision);
            canceled.WaitForWorkerBoundary(startRevision); canceled.Dispose();
            Check(worker.GetAwaiter().GetResult() && contacts == 0,
                "Disposing a pending contact must not execute its collision");
        }
        {
            using var unobserved = new FightBattleImpactLatch();
            var move = new FightBattleMovementEvent();
            unobserved.BlockedMovementPrepared(null, move, new FightGridPoint(1, 1));
            unobserved.AwaitBlockedContact(move);
            Check(!unobserved.TryTakeMovement(out _), "Opted-out rule execution must retain its existing behavior");
        }
        {
            using var deferred = new FightBattleImpactLatch();
            var impact = new FightBattlePendingImpact();
            var damage = new FightBattleDamageEvent { BeforeHp = 100, AfterHp = 70 };
            var child = new FightBattleSkillCastEvent();
            var worker = Task.Run(() => {
                deferred.ImpactSettled(impact, damage);
                deferred.CastPrepared(child);
                damage.IsDamageTriggerStatus = true;
                damage.HasDamageTriggerStatus = true;
                deferred.ImpactStatusResolved(impact, damage);
            });
            FightBattleSettledImpact initial = null;
            Check(SpinWait.SpinUntil(() => deferred.TryTakeSettlement(out initial), 20000)
                && !initial.Damage.HasDamageTriggerStatus, "The actual unresolved result was not offered");
            deferred.DeferStatusResolution(initial, out _);
            FightBattlePreparedCast nested = null;
            Check(SpinWait.SpinUntil(() => deferred.TryTakePreparation(out nested), 20000)
                && ReferenceEquals(nested.Cast, child) && !deferred.TryTakeSettlement(out _)
                && !damage.HasDamageTriggerStatus,
                "A nested skill before status resolution must not acquire an invented result");
            deferred.CompletePreparation(nested);
            FightBattleSettledImpact resolved = null;
            Check(SpinWait.SpinUntil(() => deferred.TryTakeSettlement(out resolved), 20000)
                && resolved.IsStatusResolution && ReferenceEquals(resolved.Impact, impact)
                && ReferenceEquals(resolved.Damage, damage) && damage.HasDamageTriggerStatus
                && damage.IsDamageTriggerStatus && !worker.IsCompleted,
                "The deferred outcome was not handed back at its actual completion");
            deferred.CompleteSettlement(resolved);
            Check(worker.Wait(20000), "Completing the actual status result did not resume its worker");
        }
        {
            var cast = new FightBattleSkillCastEvent { SkillEntityId = 10100 };
            using var preparationLatch = new FightBattleImpactLatch();
            var prepare = Task.Run(() => preparationLatch.CastPrepared(cast));
            FightBattlePreparedCast preparation = null;
            Check(SpinWait.SpinUntil(() => preparationLatch.TryTakePreparation(out preparation), 20000)
                && ReferenceEquals(preparation.Cast, cast), "The host did not receive the actual cast preparation");
            Check(!prepare.Wait(50), "The executor continued while the host was copying cast data");
            preparationLatch.CompletePreparation(preparation);
            Check(prepare.Wait(20000), "Completing the cast preparation did not resume the executor");
            bool duplicate = false;
            try { preparationLatch.CompletePreparation(preparation); }
            catch (InvalidOperationException) { duplicate = true; }
            Check(duplicate, "One cast preparation was completed more than once");
            var canceled = Task.Run(() => {
                try { preparationLatch.CastPrepared(new FightBattleSkillCastEvent()); return false; }
                catch (OperationCanceledException) { return true; }
            });
            Check(SpinWait.SpinUntil(() => preparationLatch.TryTakePreparation(out _), 20000),
                "The pending preparation never reached the host");
            preparationLatch.Dispose();
            Check(canceled.Wait(20000) && canceled.Result, "Disposal did not cancel cast preparation");
        }
        {
            using var completionLatch = new FightBattleImpactLatch();
            completionLatch.CastCompleted(new FightBattleSkillCastEvent());
            Check(!completionLatch.TryTakeCastCompletion(out _), "A rejected cast was reported as started");
            var cast = new FightBattleSkillCastEvent();
            var prepare = Task.Run(() => completionLatch.CastPrepared(cast));
            FightBattlePreparedCast preparation = null;
            Check(SpinWait.SpinUntil(() => completionLatch.TryTakePreparation(out preparation), 20000),
                "The completion fixture did not prepare its cast");
            completionLatch.CompletePreparation(preparation);
            Check(prepare.Wait(20000), "The completion fixture did not release preparation");
            var canceled = Task.Run(() => {
                try { completionLatch.CastCompleted(cast); return false; }
                catch (OperationCanceledException) { return true; }
            });
            Check(SpinWait.SpinUntil(() => completionLatch.TryTakeCastCompletion(out _), 20000),
                "The cast completion did not reach the host");
            completionLatch.Dispose();
            Check(canceled.Wait(20000) && canceled.Result, "Disposal did not cancel cast completion");
        }
        {
            using var selectionLatch = new FightBattleImpactLatch();
            var cast = new FightBattleSkillCastEvent();
            var selection = new FightBattleHitSelectionSnapshot();
            var canceled = Task.Run(() => {
                try { selectionLatch.HitSelected(cast, selection); return false; }
                catch (OperationCanceledException) { return true; }
            });
            FightBattleSelectedHit selected = null;
            Check(SpinWait.SpinUntil(() => selectionLatch.TryTakeSelection(out selected), 20000)
                && ReferenceEquals(selected.Cast, cast) && ReferenceEquals(selected.Selection, selection)
                && !canceled.Wait(50), "The selected hit was not held for its host");
            selectionLatch.Dispose();
            Check(canceled.Wait(20000) && canceled.Result, "Disposal did not cancel hit selection");
        }
        {
            using var settlementLatch = new FightBattleImpactLatch();
            var impact = new FightBattlePendingImpact();
            var damage = new FightBattleDamageEvent { BeforeHp = 100, AfterHp = 70 };
            var notify = Task.Run(() => settlementLatch.ImpactSettled(impact, damage));
            FightBattleSettledImpact settlement = null;
            Check(SpinWait.SpinUntil(() => settlementLatch.TryTakeSettlement(out settlement), 20000)
                && ReferenceEquals(settlement.Impact, impact) && ReferenceEquals(settlement.Damage, damage),
                "The host did not receive the actual settled hit");
            Check(!notify.Wait(50), "The executor continued while the host was copying the settled hit");
            settlementLatch.CompleteSettlement(settlement);
            Check(notify.Wait(20000), "Completing the settlement did not resume the executor");
            bool duplicate = false;
            try { settlementLatch.CompleteSettlement(settlement); }
            catch (InvalidOperationException) { duplicate = true; }
            Check(duplicate, "One settlement was completed more than once");
            var canceled = Task.Run(() => {
                try { settlementLatch.ImpactSettled(impact, damage); return false; }
                catch (OperationCanceledException) { return true; }
            });
            Check(SpinWait.SpinUntil(() => settlementLatch.TryTakeSettlement(out _), 20000),
                "The pending settlement never reached the host");
            settlementLatch.Dispose();
            Check(canceled.Wait(20000) && canceled.Result, "Disposal did not cancel settlement");
        }
        {
            var cast = new FightBattleSkillCastEvent();
            var otherCast = new FightBattleSkillCastEvent();
            var selection = new FightBattleHitSelectionSnapshot();
            selection.EffectCenters.Add(new FightGridPoint(4, 5));
            selection.Targets.Add(new FightBattleHitSelectionSnapshot.TargetOccurrence { TargetId = 2, HasScopeIndex = true, ScopeIndex = 0 });
            selection.Targets.Add(new FightBattleHitSelectionSnapshot.TargetOccurrence { TargetId = 3, HasScopeIndex = true, ScopeIndex = 0 });
            selection.Targets.Add(new FightBattleHitSelectionSnapshot.TargetOccurrence { TargetId = 2, HasScopeIndex = true, ScopeIndex = 1 });
            selection.Targets.Add(new FightBattleHitSelectionSnapshot.TargetOccurrence { TargetId = 4 });
            FightBattlePendingImpact Pending(int index) => new FightBattlePendingImpact {
                Cast = cast, HitSelection = selection, TargetId = selection.Targets[index].TargetId,
                EffectCenterIndex = 0, OccurrenceIndex = index,
                HasScopeIndex = selection.Targets[index].HasScopeIndex, ScopeIndex = selection.Targets[index].ScopeIndex };
            var latch = new FightBattleImpactLatch();
            var unresolved = Pending(3); unresolved.ScopeIdentityUnresolved = true;
            bool rejected = false;
            try { latch.AwaitImpact(unresolved); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected && !latch.TryTake(out _), "An unresolved aggregate scope must not settle at the centre");
            var one = Task.Run(() => latch.AwaitImpact(Pending(0)));
            var two = Task.Run(() => latch.AwaitImpact(Pending(1)));
            var queued = new List<FightBattlePendingImpact>();
            Check(SpinWait.SpinUntil(() => { while (latch.TryTake(out var item)) queued.Add(item); return queued.Count == 2; }, 20000),
                "The host did not receive both pending targets");
            Check(latch.RecordArrival(otherCast, selection, 0, 0) && latch.RecordArrival(cast, selection, 0, 2),
                "Independent arrivals were not recorded");
            Check(!one.Wait(50) && !two.Wait(50), "A different cast or scope released the pending targets");
            Check(latch.RecordArrival(cast, selection, 0, 0) && !latch.RecordArrival(cast, selection, 0, 0),
                "A repeated scope arrival must remain one event");
            Check(Task.WaitAll(new[] { one, two }, 20000), "A shared scope arrival did not release both affected targets");
            Check(latch.RecordArrival(cast, selection, 0, 1), "An early scope arrival was not recorded");
            var early = Task.Run(() => latch.AwaitImpact(Pending(2)));
            Check(early.Wait(20000), "An arrival before the target request was lost");
            var center = Task.Run(() => latch.AwaitImpact(Pending(3)));
            Check(!center.Wait(50) && latch.RecordArrival(cast, selection, 0, -1) && center.Wait(20000),
                "A centre hit must wait for its own original arrival");
            var waitingCast = new FightBattleSkillCastEvent();
            var canceled = Task.Run(() => {
                var pending = Pending(0); pending.Cast = waitingCast;
                try { latch.AwaitImpact(pending); return false; }
                catch (OperationCanceledException) { return true; }
            });
            Check(SpinWait.SpinUntil(() => { while (latch.TryTake(out var item)) if (ReferenceEquals(item.Cast, waitingCast)) return true; return false; }, 20000),
                "The cancellation fixture never reached its wait");
            latch.Dispose();
            Check(canceled.Wait(20000) && canceled.Result && !latch.RecordArrival(cast, selection, 0, 0),
                "Disposing the step must cancel its waiter and ignore late arrivals");
        }
        int impacts = 0;
        foreach (var setup in new[] { new[] {10011,10630,1,3}, new[] {10015,10670,2,1}, new[] {10028,10800,3,1} })
        {
            var checkpoint = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 7, 7, formula); actor.Direction = 3;
            var target = Unit(2, 2, 7 + setup[2], 7, formula);
            var skill = catalog.CreateSkill(setup[1], 0, setup[0], 2, 20, 1,
                catalog.CalculateSkillProps(setup[1], 2, 20, 1).Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            checkpoint.Units.AddRange(new[] { actor, target }); var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); checkpoint.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(checkpoint, formula, new FightDamageSettlement(), catalog, items);
            var baseline = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            var ordinary = FightBattleExecutor.Restore(baseline, formula, new FightDamageSettlement(), catalog, items);
            ordinary.Step(0, 1);
            var gate = new Gate(); executor.ImpactGate = gate;
            var step = Task.Run(() => executor.Step(0, 1));
            long previousHp = target.Hp;
            for (int index = 0; index < setup[3]; index++)
            {
                var pending = gate.Next();
                try
                {
                    var impact = pending.Impact;
                    Check(impact.SourceId == 1 && impact.TargetId == 2 && impact.SkillItemId == setup[0]
                        && impact.SkillEntityId == setup[1] && impact.HitEntityId > 0 && impact.DamageEntityId > 0
                        && impact.Cast != null && impact.HitSelection != null,
                        "The native hit lacks its original cast, hit, target or location");
                    Check(gate.Prepared.Any(cast => ReferenceEquals(cast, impact.Cast)),
                        "The impact was requested before its cast preparation notification");
                    Check(impact.OccurrenceIndex >= 0 && impact.OccurrenceIndex < impact.HitSelection.Targets.Count
                        && impact.HitSelection.Targets[impact.OccurrenceIndex].TargetId == impact.TargetId
                        && impact.HitSelection.Targets[impact.OccurrenceIndex].EffectCenterIndex == impact.EffectCenterIndex,
                        "The pending impact lost its selected occurrence or effect centre");
                    Check(target.Hp == previousHp, "HP changed before the presentation released the pending impact");
                    Check(!step.Wait(50) && target.Hp == previousHp, "A paused impact settled without its presentation event");
                }
                finally { pending.Released.Set(); }
                if (index + 1 < setup[3])
                {
                    var next = gate.Next();
                    Check(target.Hp < previousHp, "A released hit did not settle before the next impact");
                    previousHp = target.Hp;
                    gate.Items.Add(next);
                }
                impacts++;
            }
            Check(step.Wait(TimeSpan.FromSeconds(20)), "The final impact did not finish the action");
            var actual = step.Result;
            if (setup[0] == 10015 || setup[0] == 10028)
            {
                var direct = gate.Settlements.Where(value => value.SkillItemId == setup[0]).ToArray();
                Check(direct.Length == 1 && direct[0].HasStatusResult == (setup[0] == 10028)
                    && !direct[0].StatusTriggered,
                    "Only an empty original status list has a known result at the impact boundary");
            }
            Check(actual.Event.DamageEvents.Count(d => d.SkillItemId == setup[0]) == setup[3], "A hit was lost or duplicated");
            var resolved = gate.StatusResults.Where(value => value.Impact.SkillItemId == setup[0]).ToArray();
            Check(resolved.Length == setup[3] && resolved.All(value => value.Known
                && actual.Event.DamageEvents.Any(d => d.DamageId == value.DamageId
                    && d.TargetId == value.Impact.TargetId && d.IsDamageTriggerStatus == value.Triggered)),
                "A damage's completed status result was omitted, guessed or associated with another hit");
            Check(FightBattleCheckpointSerializer.Save(checkpoint) == FightBattleCheckpointSerializer.Save(baseline),
                "Gating changed the original deterministic result");
        }
        {
            var source = Unit(1, 1, 3, 3, formula);
            var target = Unit(2, 2, 4, 3, formula);
            var point = new FightGridPoint(0, 0);
            var hit = new FightBattleHitDefinition { ActionType = 4, TargetMode = 1,
                Scope = new List<FightGridPoint> { point, point }, ScopeEntries = new List<FightBattleHitScopeEntry> {
                    new FightBattleHitScopeEntry { Position = point, Delay = .1f },
                    new FightBattleHitScopeEntry { Position = point, Delay = .2f } } };
            var skill = new FightBattleSkillDefinition { SkillType = 0, TargetType = 1 };
            var occurrences = FightBattleHitPatterns.For(hit).Plan(new FightHitPatternContext {
                Source = source, Units = new[] { source, target }, Skill = skill, Hit = hit,
                Targets = new List<FightBattleUnitState> { target }, Primary = target,
                Cast = FightBattleSkillCastPresentation.Capture(source, skill, new List<FightBattleUnitState> { target }),
                Random = new FightRandomStream(7) });
            Check(occurrences.Count == 2 && occurrences[0].ScopeIndex == 0 && occurrences[1].ScopeIndex == 1
                && occurrences[0].GridPosition.Equals(occurrences[1].GridPosition),
                "Repeated authored scope cells must retain separate occurrence identities");
        }
        {
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 24, Height = 24, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 10, 10, formula); actor.Direction = 3;
            var skill = catalog.CreateSkill(10590, 0, 10043, 2, 20, 1,
                catalog.CalculateSkillProps(10590, 2, 20, 1).Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            cp.Units.Add(actor); int unitId = 2;
            foreach (var offset in new[] { new[] {0,0}, new[] {-1,0}, new[] {1,0}, new[] {0,-1}, new[] {0,1} })
                cp.Units.Add(Unit(unitId++, 2, 13 + offset[0], 10 + offset[1], formula));
            var wave = new FightBattleWaveState(); wave.UnitIds.AddRange(cp.Units.Where(u => u.Side == 2).Select(u => u.Id)); cp.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            using var latch = new FightBattleImpactLatch();
            executor.ImpactGate = latch; var task = Task.Run(() => executor.Step(0, 1));
            FightBattlePreparedCast preparation = null;
            Check(SpinWait.SpinUntil(() => latch.TryTakePreparation(out preparation), 20000)
                && preparation.Cast.SkillItemId == 10043 && !task.IsCompleted,
                "The original cross-scope cast was not prepared before its hits");
            latch.CompletePreparation(preparation);
            FightBattleSelectedHit selected = null;
            Check(SpinWait.SpinUntil(() => latch.TryTakeSelection(out selected), 20000)
                && ReferenceEquals(selected.Cast, preparation.Cast) && selected.Selection.HitEntityId == 10591
                && selected.Selection.Targets.Count == 5 && !task.IsCompleted,
                "The host did not receive the original five-target selection before settlement");
            latch.CompleteSelection(selected);
            bool duplicateSelection = false;
            try { latch.CompleteSelection(selected); }
            catch (InvalidOperationException) { duplicateSelection = true; }
            Check(duplicateSelection, "One hit selection was completed more than once");
            var seen = new HashSet<int>();
            var scopeIndices = new HashSet<int>();
            for (int index = 0; index < 5; index++)
            {
                FightBattlePendingImpact impact = null;
                Check(SpinWait.SpinUntil(() => latch.TryTake(out impact), 20000), "A cross-scope impact was not offered to the host");
                Check(ReferenceEquals(impact.HitSelection, selected.Selection),
                    "The pending hit did not belong to the selected original view");
                var occurrence = impact.HitSelection.Targets[impact.OccurrenceIndex];
                Check(impact.SkillItemId == 10043 && seen.Add(impact.TargetId)
                    && occurrence.TargetId == impact.TargetId && occurrence.EffectCenterIndex == impact.EffectCenterIndex
                    && impact.HasScopeIndex && occurrence.HasScopeIndex && occurrence.ScopeIndex == impact.ScopeIndex
                    && scopeIndices.Add(impact.ScopeIndex)
                    && occurrence.HasGridPosition && occurrence.GridX == impact.GridX && occurrence.GridY == impact.GridY,
                    "A cross-scope impact lost the actual target, body cell or centre identity");
                Check(cp.Units.Single(u => u.Id == impact.TargetId).Hp == 1000000,
                    "A cross-scope target took damage before its own impact");
                Check(latch.RecordArrival(impact.Cast, impact.HitSelection, impact.EffectCenterIndex, impact.ScopeIndex),
                    "The matching scope arrival was not accepted");
                FightBattleSettledImpact settled = null;
                Check(SpinWait.SpinUntil(() => latch.TryTakeSettlement(out settled), 20000)
                    && ReferenceEquals(settled.Impact, impact) && settled.Damage.TargetId == impact.TargetId
                    && settled.Damage.BeforeHp == 1000000 && settled.Damage.AfterHp < 1000000
                    && cp.Units.Single(u => u.Id == impact.TargetId).Hp == settled.Damage.AfterHp
                    && !task.IsCompleted, "The host did not observe actual HP at the matching impact");
                latch.CompleteSettlement(settled);
            }
            FightBattleCompletedCast completedCast = null;
            Check(SpinWait.SpinUntil(() => latch.TryTakeCastCompletion(out completedCast), 20000)
                && ReferenceEquals(completedCast.Cast, preparation.Cast) && completedCast.Cast.Presentation.Duration >= 0f
                && !task.IsCompleted, "The completed cast duration was not offered before the step finished");
            latch.CompleteCast(completedCast);
            bool duplicateCompletion = false;
            try { latch.CompleteCast(completedCast); }
            catch (InvalidOperationException) { duplicateCompletion = true; }
            Check(duplicateCompletion, "One cast completion was applied more than once");
            Check(task.Wait(TimeSpan.FromSeconds(20)) && task.Result.Event.DamageEvents.Count(d => d.SkillItemId == 10043) == 5,
                "Cross-scope impacts were lost or duplicated");
            var saved = FightBattleCheckpointSerializer.Save(cp);
            var restored = FightBattleCheckpointSerializer.Load(saved);
            var recorded = restored.Receipts.Last().Event.SkillCasts.Single(c => c.SkillItemId == 10043)
                .HitSelections.Single(s => s.HitEntityId == 10591);
            Check(recorded.Targets.Count == 5 && recorded.Targets.All(t => t.HasScopeIndex)
                && recorded.Targets.Select(t => t.ScopeIndex).Distinct().Count() == 5,
                "Original scope identities did not survive the checkpoint");
            string legacy = System.Text.RegularExpressions.Regex.Replace(saved,
                ",\\\"HasScopeIndex\\\":(?:true|false),\\\"ScopeIndex\\\":-?[0-9]+", "");
            Check(legacy != saved, "Legacy scope fixture did not remove the new fields");
            var oldRecorded = FightBattleCheckpointSerializer.Load(legacy).Receipts.Last().Event.SkillCasts
                .Single(c => c.SkillItemId == 10043).HitSelections.Single(s => s.HitEntityId == 10591);
            Check(oldRecorded.Targets.All(t => !t.HasScopeIndex),
                "A legacy checkpoint invented scope indices that were not recorded");
        }
        {
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 7, 7, formula); var target = Unit(2, 2, 10, 7, formula);
            var skill = catalog.CreateSkill(10800, 0, 10028, 2, 20, 1,
                catalog.CalculateSkillProps(10800, 2, 20, 1).Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            cp.Units.AddRange(new[] { actor, target }); var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
            FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var clock = new FightBattleStatusSeconds(cp, catalog, new FightRandomStream(cp.RandomSeed));
            clock.Enqueue(0, 2, true, false, false, catalog.CreateStatus(24269, 2, 1, 1, 10580, 10006, 2, 20, 1));
            string committed = FightBattleCheckpointSerializer.Save(cp);
            FightBattleExecutor privateExecutor = null; int callbackThread = 0;
            using (var isolated = new FightBattleIsolatedStep(cp, state => {
                privateExecutor = FightBattleExecutor.Restore(state, formula, new FightDamageSettlement(), catalog, items);
                privateExecutor.StatusSecondsChanged += (_, __, ___) => callbackThread = Thread.CurrentThread.ManagedThreadId;
                return privateExecutor;
            }, 0, 1)) {
                FightBattlePreparedCast held = null;
                Check(SpinWait.SpinUntil(() => isolated.Latch.TryTakePreparation(out held), 20000),
                    "The timed isolated step did not reach its cast handoff");
                Check(isolated.TryAdvanceStatusSeconds(0, out var launch), "The isolated worker did not accept its status clock");
                FightBattleSecondsStatusNotice notice = null;
                Check(SpinWait.SpinUntil(() => isolated.Latch.TryTakeSecondsStatus(out notice), 20000)
                    && notice.Event.OwnerId == 2 && notice.Event.Kind == FightBattleStatusViewEventKind.Start
                    && notice.Event.Status.ClassId == 24269 && !launch.IsCompleted,
                    "The original airborne start was not held for the host");
                var launchSnapshot = notice.Event.Status;
                isolated.Latch.CompleteSecondsStatus(notice, out _);
                Check(launch.Wait(20000) && launch.Result, "The isolated worker did not apply its due airborne state");
                Check(callbackThread != 0 && callbackThread != Thread.CurrentThread.ManagedThreadId
                    && privateExecutor.State.Units[1].Statuses.Any(status => status.ClassId == 24269)
                    && FightBattleCheckpointSerializer.Save(cp) == committed,
                    "The status clock escaped its worker or changed the committed checkpoint");
                Task<bool> recovery = null;
                Check(SpinWait.SpinUntil(() => isolated.TryAdvanceStatusSeconds(.7, out recovery), 20000),
                    "The isolated worker did not accept the recovery clock");
                Check(SpinWait.SpinUntil(() => isolated.Latch.TryTakeSecondsStatus(out notice), 20000)
                    && notice.Event.Kind == FightBattleStatusViewEventKind.End && notice.Event.Status.ClassId == 24269,
                    "The airborne end lost its host handoff");
                isolated.Latch.CompleteSecondsStatus(notice, out _);
                Check(SpinWait.SpinUntil(() => isolated.Latch.TryTakeSecondsStatus(out notice), 20000)
                    && notice.Event.Kind == FightBattleStatusViewEventKind.Start && notice.Event.Status.ClassId == 24270,
                    "The recovery start did not follow airborne end");
                isolated.Latch.CompleteSecondsStatus(notice, out _);
                Check(recovery.Wait(20000) && recovery.Result && launchSnapshot.ClassId == 24269
                    && privateExecutor.State.Units[1].Statuses.Any(status => status.ClassId == 24270),
                    "The isolated worker did not advance to the original recovery state");
            }
            Check(FightBattleCheckpointSerializer.Save(cp) == committed,
                "Canceling a timed isolated step published its partial state");
        }
        {
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 7, 7, formula); var target = Unit(2, 2, 10, 7, formula);
            var skill = catalog.CreateSkill(10800, 0, 10028, 2, 20, 1,
                catalog.CalculateSkillProps(10800, 2, 20, 1).Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            cp.Units.AddRange(new[] { actor, target }); var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
            FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            string committed = FightBattleCheckpointSerializer.Save(cp);
            var ordinary = FightBattleCheckpointSerializer.Load(committed);
            FightBattleExecutor.Restore(ordinary, formula, new FightDamageSettlement(), catalog, items).Step(0, 1);
            FightBattleExecutor Restore(FightBattleCheckpoint state) =>
                FightBattleExecutor.Restore(state, formula, new FightDamageSettlement(), catalog, items);
            using (var isolated = new FightBattleIsolatedStep(cp, Restore, 0, 1))
            {
                FightBattlePreparedCast preparation = null;
                Check(SpinWait.SpinUntil(() => isolated.Latch.TryTakePreparation(out preparation), 20000),
                    "The isolated step did not offer the actual cast");
                Check(FightBattleCheckpointSerializer.Save(cp) == committed && target.Hp == 1000000,
                    "A running step changed its committed checkpoint");
                isolated.Latch.CompletePreparation(preparation);
                FightBattleSelectedHit selected = null;
                Check(SpinWait.SpinUntil(() => isolated.Latch.TryTakeSelection(out selected), 20000),
                    "The isolated step did not offer the selected hit");
                isolated.Latch.CompleteSelection(selected);
                FightBattlePendingImpact impact = null;
                Check(SpinWait.SpinUntil(() => isolated.Latch.TryTake(out impact), 20000),
                    "The isolated step did not await the impact");
                isolated.Latch.RecordArrival(impact.Cast, impact.HitSelection, impact.EffectCenterIndex,
                    impact.HasScopeIndex ? impact.ScopeIndex : -1);
                FightBattleSettledImpact settled = null;
                Check(SpinWait.SpinUntil(() => isolated.Latch.TryTakeSettlement(out settled), 20000)
                    && settled.Damage.AfterHp < 1000000 && target.Hp == 1000000
                    && FightBattleCheckpointSerializer.Save(cp) == committed,
                    "Impact settlement escaped into the committed checkpoint");
                isolated.Latch.CompleteSettlement(settled);
                FightBattleCompletedCast castCompletion = null;
                Check(SpinWait.SpinUntil(() => isolated.Latch.TryTakeCastCompletion(out castCompletion), 20000)
                    && ReferenceEquals(castCompletion.Cast, preparation.Cast),
                    "The isolated cast did not report its final duration");
                isolated.Latch.CompleteCast(castCompletion);
                FightBattleExecutor completedExecutor = null; FightBattleExecutionResult completed = null;
                Check(SpinWait.SpinUntil(() => isolated.TryTakeCompleted(out completedExecutor, out completed), 20000)
                    && completed != null && ReferenceEquals(completedExecutor.State, completed.Checkpoint)
                    && completedExecutor.ImpactGate == null && FightBattleCheckpointSerializer.Save(cp) == committed
                    && FightBattleCheckpointSerializer.Save(completed.Checkpoint) == FightBattleCheckpointSerializer.Save(ordinary),
                    "The isolated completed step changed deterministic results or its original checkpoint");
                bool duplicateTransfer = false;
                try { isolated.TryTakeCompleted(out _, out _); }
                catch (InvalidOperationException) { duplicateTransfer = true; }
                Check(duplicateTransfer, "The isolated result was transferred more than once");
            }
            bool aliased = false;
            try { using var invalid = new FightBattleIsolatedStep(cp, _ => Restore(cp), 0, 1); }
            catch (InvalidOperationException) { aliased = true; }
            Check(aliased && FightBattleCheckpointSerializer.Save(cp) == committed,
                "An executor bound to the committed checkpoint was accepted");
            using (var canceled = new FightBattleIsolatedStep(cp, Restore, 0, 1))
            {
                Check(SpinWait.SpinUntil(() => canceled.Latch.TryTakePreparation(out _), 20000),
                    "The canceled isolated step did not reach a safe rendezvous");
                long revision = canceled.Latch.WorkerWaitRevision;
                var fence = Task.Run(() => {
                    try { canceled.Latch.WaitForWorkerBoundary(revision); return false; }
                    catch (OperationCanceledException) { return true; }
                });
                canceled.Dispose();
                Check(fence.Wait(20000) && fence.Result && SpinWait.SpinUntil(() => canceled.IsCompleted, 20000)
                    && FightBattleCheckpointSerializer.Save(cp) == committed,
                    "Canceling a partial isolated step did not release its fence or preserve the checkpoint");
            }
        }
        foreach (bool removed in new[] { false, true })
        {
            var cp = new FightBattleCheckpoint { StageId = 1001, MaxRound = 8, RandomSeed = 1012348,
                Grid = new FightBattleGrid { Width = 20, Height = 20, DefaultGroundType = 2 } };
            var actor = Unit(1, 1, 7, 7, formula); var target = Unit(2, 2, 8, 7, formula);
            var skill = catalog.CreateSkill(10600, 0, 10008, 2, 20, 1,
                catalog.CalculateSkillProps(10600, 2, 20, 1).Select(p => new FightPropertyValue { Id = p.Key, Value = p.Value }).ToList());
            skill.CostType = 1; skill.SkillCost = 100;
            skill.LimitedTimes = 1; actor.Skills.Add(skill); actor.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            cp.Units.AddRange(new[] { actor, target }); var wave = new FightBattleWaveState(); wave.UnitIds.Add(2); cp.Waves.Add(wave);
            var executor = FightBattleExecutor.Begin(cp, formula, new FightDamageSettlement(), catalog, items);
            var gate = new Gate(); executor.ImpactGate = gate; var task = Task.Run(() => executor.Step(0, 1));
            var pending = gate.Next();
            Check(gate.Prepared.TryPeek(out var started) && ReferenceEquals(started, pending.Impact.Cast)
                && started.HasSkillCostHp && started.SkillCostBeforeHp == 1000000
                && started.SkillCostAfterHp == 999900 && actor.Hp == 999900,
                "Cast preparation must follow original HP cost and precede the pending hit");
            target.Hp = target.Turn.Hp = removed ? 0 : 500000;
            pending.Released.Set();
            Check(task.Wait(TimeSpan.FromSeconds(20)), "Changed target did not finish the action");
            var hits = task.Result.Event.DamageEvents.Where(d => d.SkillItemId == 10008 && !d.IsSkillCost).ToArray();
            Check(removed ? hits.Length == 0 : hits.Length == 1 && hits[0].BeforeHp == 500000 && hits[0].AfterHp < 500000,
                "The impact did not use the target's live state when the presentation reached it");
        }
        foreach (bool cancel in new[] { false, true })
        using (var latch = new FightBattleImpactLatch { ObserveDisperses = true })
        {
            var receipt = new FightBattleDamageEvent { TargetId = 2, HasDisperseNotification = true };
            var worker = Task.Run(() => {
                try { latch.DisperseResolved(receipt); return false; }
                catch (OperationCanceledException) { return true; }
            });
            FightBattleDisperseNotice notice = null;
            Check(SpinWait.SpinUntil(() => latch.TryTakeDisperse(out notice), 20000)
                && ReferenceEquals(notice.Damage, receipt) && !worker.IsCompleted,
                "A successful dispel must wait for its exact presentation receipt");
            if (cancel) latch.Dispose();
            else
            {
                latch.CompleteDisperse(notice, out _);
                bool duplicate = false;
                try { latch.CompleteDisperse(notice, out _); }
                catch (InvalidOperationException) { duplicate = true; }
                Check(duplicate, "A successful dispel presentation cannot be completed twice");
            }
            Check(worker.Wait(20000) && worker.Result == cancel,
                "The dispel worker was not released or canceled at its presentation boundary");
        }
        return "Impact gate passed: " + impacts + " held impacts across three-hit combo, jump slash and fireball; five cross-scope identities, skill cost and exact ungated results verified.";
    }
}
