using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Combat
{
    /// <summary>
    /// Coordinates one isolated rule step with the original cast, hit and damage views.
    /// Use from one presentation thread. The host decides when a prepared cast actually
    /// starts and advances this clock with the same scaled time as native presentation.
    /// </summary>
    public sealed class OfficialBattleLiveImpactSession : IDisposable
    {
        public delegate bool StartCast(FightBattleSkillCastEvent cast, out EcEntityIdentifier skillId);
        public delegate bool StartMovement(FightBattleSkillCastEvent cast, FightBattleMovementEvent movement);

        private sealed class ActiveCast
        {
            public EcEntityIdentifier Id;
            public float Started;
        }

        private readonly FightBattleIsolatedStep step;
        private readonly OfficialBattleSkillEntityPlayback views;
        private readonly OfficialBattleImpactViewBridge bridge;
        private readonly Dictionary<FightBattleSkillCastEvent, ActiveCast> casts =
            new Dictionary<FightBattleSkillCastEvent, ActiveCast>();
        private readonly HashSet<FightBattleHitSelectionSnapshot> selectedHits = new HashSet<FightBattleHitSelectionSnapshot>();
        private readonly Dictionary<int, HashSet<int>> jumpSlashScopes = new Dictionary<int, HashSet<int>>();
        private FightBattlePreparedCast preparation;
        private FightBattlePreparedMovement movement;
        private FightBattleSelectedHit selection;
        private FightBattleSettledImpact settlement;
        private FightBattleSettledImpact resolvingStatus;
        private FightBattleCompletedCast completion;
        private float clock;
        private bool disposed, transferred, pumping;
        private StartCast lastStartCast;
        private StartMovement lastStartMovement;
        private Action<FightBattleDamageEvent> lastAfterDamageStarted;
        private FightBattleSkillCastEvent currentGraphCast;
        private EcEntityIdentifier currentGraphSkillId;

        public bool IsCompleted => step.IsCompleted;
        public FightBattleSettledImpact PendingSettlement => settlement ?? resolvingStatus;
        public FightBattlePreparedMovement PendingMovement => movement;
        /// <summary>A nested presentation interrupted status resolution; the host must not advance this step.</summary>
        public bool StatusResolutionBlocked => resolvingStatus != null && settlement == null;
        public event Action<FightBattleSkillCastEvent, EcEntityIdentifier, GameObject, MyPlayable> SkillGraphPlayableStarted;

        public OfficialBattleLiveImpactSession(FightBattleCheckpoint checkpoint,
            Func<FightBattleCheckpoint, FightBattleExecutor> restore,
            OfficialBattleSkillEntityPlayback views, int sequence, int counter, bool observeMovements = false)
        {
            this.views = views ?? throw new ArgumentNullException(nameof(views));
            if (checkpoint?.Units != null) foreach (var unit in checkpoint.Units)
            {
                if (unit?.Skills == null) continue;
                foreach (var skill in unit.Skills)
                {
                    if (skill?.EntityId != 10670 || skill.HitGraphEntities == null) continue;
                    foreach (var hit in skill.HitGraphEntities)
                    {
                        if (hit?.EntityId != 10671 || hit.ScopeEntries == null) continue;
                        var scopes = new HashSet<int>();
                        for (int index = 0; index < hit.ScopeEntries.Count; index++)
                            if (hit.ScopeEntries[index] != null && hit.ScopeEntries[index].Delay == 0f) scopes.Add(index);
                        if (scopes.Count > 0) jumpSlashScopes[unit.Id] = scopes;
                    }
                }
            }
            step = new FightBattleIsolatedStep(checkpoint, restore, sequence, counter, observeMovements);
            bridge = new OfficialBattleImpactViewBridge(views, step.Latch);
            bridge.WorkerReleasedByArrival += OnWorkerReleasedByArrival;
            views.SkillGraphPlayableStarted += OnSkillGraphPlayableStarted;
        }

        /// <summary>
        /// Processes handoffs through the next presentation boundary. The host waits for
        /// local rule work after releasing a handoff, so an impact and its damage share
        /// the same frame. Declined starts and incomplete damage remain held.
        /// </summary>
        public bool Pump(float elapsed, StartCast startCast, Action<FightBattleDamageEvent> afterDamageStarted = null,
            StartMovement startMovement = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleLiveImpactSession));
            if (transferred) throw new InvalidOperationException("The live step was already transferred.");
            if (float.IsNaN(elapsed) || float.IsInfinity(elapsed) || elapsed < 0f)
                throw new ArgumentOutOfRangeException(nameof(elapsed));
            if (startCast == null) throw new ArgumentNullException(nameof(startCast));
            if (pumping) throw new InvalidOperationException("The live step is already being pumped.");
            lastStartCast = startCast;
            lastStartMovement = startMovement;
            lastAfterDamageStarted = afterDamageStarted;
            pumping = true;
            try
            {
                clock += elapsed;
                if (float.IsInfinity(clock)) throw new InvalidOperationException("The live presentation clock overflowed.");
                bool progressed = false;
                while (true)
                {
                    // Arrivals are keyed independently and may precede target requests.
                    while (step.Latch.TryTake(out _)) { }
                    if (resolvingStatus != null)
                    {
                        if (!step.Latch.TryTakeSettlement(out settlement)) return progressed;
                        if (!settlement.IsStatusResolution || !ReferenceEquals(settlement.Damage, resolvingStatus.Damage)
                            || !ReferenceEquals(settlement.Impact, resolvingStatus.Impact))
                            throw new InvalidOperationException("The completed status result belongs to another impact.");
                        resolvingStatus = null;
                    }
                    if (preparation == null) step.Latch.TryTakePreparation(out preparation);
                    if (preparation != null)
                    {
                        if (!startCast(preparation.Cast, out var id)) return progressed;
                        casts.Add(preparation.Cast, new ActiveCast { Id = id, Started = clock });
                        step.Latch.CompletePreparation(preparation, out long revision);
                        preparation = null; progressed = true;
                        step.Latch.WaitForWorkerBoundary(revision);
                        continue;
                    }
                    if (movement == null) step.Latch.TryTakeMovement(out movement);
                    if (movement != null)
                    {
                        if (startMovement == null || !startMovement(movement.Cast, movement.Movement)) return progressed;
                        step.Latch.CompleteMovement(movement, out long revision);
                        movement = null; progressed = true;
                        step.Latch.WaitForWorkerBoundary(revision);
                        continue;
                    }
                    if (selection == null) step.Latch.TryTakeSelection(out selection);
                    if (selection != null)
                    {
                        var active = Find(selection.Cast);
                        selectedHits.Add(selection.Selection);
                        long revision = bridge.QueueSelection(selection, active.Id, clock - active.Started);
                        selection = null; progressed = true;
                        step.Latch.WaitForWorkerBoundary(revision);
                        continue;
                    }
                    if (settlement == null) step.Latch.TryTakeSettlement(out settlement);
                    if (settlement != null)
                    {
                        var active = Find(settlement.Impact.Cast);
                        if (!settlement.Damage.HasDamageTriggerStatus)
                        {
                            resolvingStatus = settlement;
                            step.Latch.DeferStatusResolution(settlement, out long deferredRevision);
                            settlement = null; progressed = true;
                            step.Latch.WaitForWorkerBoundary(deferredRevision);
                            continue;
                        }
                        if (!bridge.TryQueueSettlement(settlement, active.Id, out long revision, afterDamageStarted)) return progressed;
                        settlement = null; progressed = true;
                        step.Latch.WaitForWorkerBoundary(revision);
                        continue;
                    }
                    if (completion == null) step.Latch.TryTakeCastCompletion(out completion);
                    if (completion != null)
                    {
                        var active = Find(completion.Cast);
                        views.CompleteLiveCast(completion.Cast, active.Id, clock - active.Started);
                        step.Latch.CompleteCast(completion, out long revision);
                        casts.Remove(completion.Cast);
                        completion = null; progressed = true;
                        step.Latch.WaitForWorkerBoundary(revision);
                        continue;
                    }
                    return progressed;
                }
            }
            finally { pumping = false; }
        }

        private void OnWorkerReleasedByArrival(long revision)
        {
            if (disposed || transferred || pumping || lastStartCast == null) return;
            step.Latch.WaitForWorkerBoundary(revision);
            Pump(0f, lastStartCast, lastAfterDamageStarted, lastStartMovement);
        }

        private void OnSkillGraphPlayableStarted(FightBattleSkillCastEvent cast, EcEntityIdentifier id,
            GameObject prefab, MyPlayable playable)
        {
            var previousCast = currentGraphCast;
            var previousId = currentGraphSkillId;
            currentGraphCast = cast; currentGraphSkillId = id;
            try { SkillGraphPlayableStarted?.Invoke(cast, id, prefab, playable); }
            finally
            {
                try { RouteJumpSlashImpact(cast, prefab, playable); }
                finally { currentGraphCast = previousCast; currentGraphSkillId = previousId; }
            }
        }

        private void RouteJumpSlashImpact(FightBattleSkillCastEvent cast, GameObject prefab, MyPlayable playable)
        {
            // The original jump's second effect is its visible landing strike. Only cells
            // authored as simultaneous can share that actual graph-playback boundary.
            if (cast?.SkillEntityId != 10670 || prefab == null || prefab.name != "playable10670"
                || playable == null || playable.name != "effect (2)" || !jumpSlashScopes.TryGetValue(cast.ActorId, out var scopes)
                || cast.HitSelections == null || !casts.TryGetValue(cast, out var active)
                || !active.Id.Equals(currentGraphSkillId)) return;
            foreach (var hit in new List<FightBattleHitSelectionSnapshot>(cast.HitSelections))
            {
                if (hit?.HitEntityId != 10671 || !selectedHits.Contains(hit) || hit.Targets == null) continue;
                var sent = new HashSet<(int Center, int Scope)>();
                foreach (var target in new List<FightBattleHitSelectionSnapshot.TargetOccurrence>(hit.Targets))
                {
                    if (!casts.ContainsKey(cast)) return;
                    if (target == null || !target.HasScopeIndex || !scopes.Contains(target.ScopeIndex)
                        || !sent.Add((target.EffectCenterIndex, target.ScopeIndex))) continue;
                    RecordGraphImpact(cast, hit, target.EffectCenterIndex, target.ScopeIndex);
                }
            }
        }

        /// <summary>Associates a verified original graph leaf with its selected hit during that leaf's playback callback.</summary>
        public void RecordGraphImpact(FightBattleSkillCastEvent cast, FightBattleHitSelectionSnapshot hit,
            int centerIndex, int scopeIndex)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleLiveImpactSession));
            if (transferred) throw new InvalidOperationException("The live step was already transferred.");
            if (cast == null || !ReferenceEquals(currentGraphCast, cast) || hit == null || !selectedHits.Contains(hit)
                || cast.HitSelections == null || !cast.HitSelections.Contains(hit)
                || !casts.TryGetValue(cast, out var active) || !active.Id.Equals(currentGraphSkillId))
                throw new InvalidOperationException("The impact does not belong to the currently playing selected cast.");
            bool selected = false;
            if (hit.Targets != null) foreach (var target in hit.Targets)
                if (target != null && target.EffectCenterIndex == centerIndex
                    && (scopeIndex < 0 ? !target.HasScopeIndex : target.HasScopeIndex && target.ScopeIndex == scopeIndex))
                { selected = true; break; }
            if (!selected) throw new InvalidOperationException("The graph impact has no matching selected occurrence.");
            bridge.RecordArrival(cast, hit, centerIndex, scopeIndex);
        }

        /// <summary>Lets the host deliver an unsupported result while the worker remains held.</summary>
        public bool TryCompletePendingSettlement(Func<FightBattlePendingImpact, FightBattleDamageEvent, bool> deliver)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleLiveImpactSession));
            if (transferred) throw new InvalidOperationException("The live step was already transferred.");
            if (pumping) throw new InvalidOperationException("The live step is already being pumped.");
            if (deliver == null) throw new ArgumentNullException(nameof(deliver));
            if (settlement == null) return false;
            bool delivered = false;
            pumping = true;
            try
            {
                if (!step.Latch.TryCompleteSettlement(settlement, deliver, out long revision)) return false;
                settlement = null;
                step.Latch.WaitForWorkerBoundary(revision);
                delivered = true;
            }
            finally { pumping = false; }
            if (delivered && lastStartCast != null) Pump(0f, lastStartCast, lastAfterDamageStarted, lastStartMovement);
            return delivered;
        }

        private ActiveCast Find(FightBattleSkillCastEvent cast)
        {
            if (cast == null || !casts.TryGetValue(cast, out var active))
                throw new InvalidOperationException("The live event has no started original cast.");
            return active;
        }

        public bool TryTakeCompleted(out FightBattleExecutor executor, out FightBattleExecutionResult result)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleLiveImpactSession));
            if (transferred) throw new InvalidOperationException("The live step was already transferred.");
            if (pumping) throw new InvalidOperationException("The live step is already being pumped.");
            if (!step.TryTakeCompleted(out executor, out result)) return false;
            bridge.WorkerReleasedByArrival -= OnWorkerReleasedByArrival;
            views.SkillGraphPlayableStarted -= OnSkillGraphPlayableStarted;
            bridge.Dispose();
            transferred = true;
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            bridge.WorkerReleasedByArrival -= OnWorkerReleasedByArrival;
            views.SkillGraphPlayableStarted -= OnSkillGraphPlayableStarted;
            bridge.Dispose();
            step.Dispose();
            casts.Clear(); selectedHits.Clear(); jumpSlashScopes.Clear();
        }
    }
}
