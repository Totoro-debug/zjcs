using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        public delegate bool StartBlockedMovement(FightBattleSkillCastEvent cast, FightBattleMovementEvent movement,
            FightGridPoint blockedPosition, Action contact);

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
        private readonly Dictionary<int, HashSet<int>[]> mountainScopes = new Dictionary<int, HashSet<int>[]>();
        private readonly HashSet<FightBattleSkillCastEvent> observedMountains = new HashSet<FightBattleSkillCastEvent>();
        private readonly Dictionary<FightBattleSkillCastEvent, int> reachedMountainWaves = new Dictionary<FightBattleSkillCastEvent, int>();
        private readonly List<OfficialBattleMountainWaveImpact> mountainWaves = new List<OfficialBattleMountainWaveImpact>();
        private readonly HashSet<int> slashCasters = new HashSet<int>();
        private readonly HashSet<FightBattleSkillCastEvent> observedSlashes = new HashSet<FightBattleSkillCastEvent>();
        private readonly HashSet<FightBattleSkillCastEvent> reachedSlashes = new HashSet<FightBattleSkillCastEvent>();
        private readonly HashSet<(FightBattleHitSelectionSnapshot Hit, int Center)> sentSlashes =
            new HashSet<(FightBattleHitSelectionSnapshot, int)>();
        private readonly List<OfficialBattleSlashImpact> slashEffects = new List<OfficialBattleSlashImpact>();
        private FightBattlePreparedCast preparation;
        private FightBattlePreparedMovement movement;
        private FightBattleSelectedHit selection;
        private FightBattleSettledImpact settlement;
        private FightBattleSecondsStatusNotice secondsStatus;
        private readonly Stack<FightBattleSettledImpact> resolvingStatuses = new Stack<FightBattleSettledImpact>();
        private FightBattleCompletedCast completion;
        private float clock;
        private bool disposed, transferred, pumping;
        private StartCast lastStartCast;
        private StartMovement lastStartMovement;
        private StartBlockedMovement lastStartBlockedMovement;
        private Action<FightBattleDamageEvent> lastAfterDamageStarted;
        private Func<FightBattleDamageEvent, bool> lastCanStartDamage;
        private FightBattleSkillCastEvent currentGraphCast;
        private EcEntityIdentifier currentGraphSkillId;

        public bool IsCompleted => step.IsCompleted;
        public FightBattleSettledImpact PendingSettlement => settlement ?? (resolvingStatuses.Count > 0 ? resolvingStatuses.Peek() : null);
        public FightBattlePreparedMovement PendingMovement => movement;
        public FightBattleSecondsStatusNotice PendingSecondsStatus => secondsStatus;
        /// <summary>An outer hit still awaits its status result; nested presentation may continue.</summary>
        public bool StatusResolutionBlocked => resolvingStatuses.Count > 0 && settlement == null;
        public event Action<FightBattleSkillCastEvent, EcEntityIdentifier, GameObject, MyPlayable> SkillGraphPlayableStarted;
        public event Action<FightBattleStatusViewEvent> SecondsStatusChanged;

        public bool TryAdvanceStatusSeconds(double seconds, out Task<bool> completion)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleLiveImpactSession));
            if (transferred) throw new InvalidOperationException("The live step was already transferred.");
            return step.TryAdvanceStatusSeconds(seconds, out completion);
        }

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
                    if (skill?.HitGraphEntities == null) continue;
                    foreach (var hit in skill.HitGraphEntities)
                    {
                        if (hit?.ScopeEntries == null) continue;
                        if (skill.EntityId == 10600 && hit.EntityId == 10601 && hit.ScopeEntries.Count == 1
                            && hit.ScopeEntries[0] != null && hit.ScopeEntries[0].Delay == 0f
                            && hit.ScopeEntries[0].Position.X == 0 && hit.ScopeEntries[0].Position.Y == 1)
                            slashCasters.Add(unit.Id);
                        if (skill.EntityId == 10670 && hit.EntityId == 10671)
                        {
                            var scopes = new HashSet<int>();
                            for (int index = 0; index < hit.ScopeEntries.Count; index++)
                                if (hit.ScopeEntries[index] != null && hit.ScopeEntries[index].Delay == 0f) scopes.Add(index);
                            if (scopes.Count > 0) jumpSlashScopes[unit.Id] = scopes;
                        }
                        if (skill.EntityId == 11120 && hit.EntityId == 11121)
                        {
                            var rings = new[] { new HashSet<int>(), new HashSet<int>() };
                            var positions = new HashSet<FightGridPoint>();
                            for (int index = 0; index < hit.ScopeEntries.Count; index++)
                            {
                                var entry = hit.ScopeEntries[index];
                                if (entry == null || !positions.Add(entry.Position)) continue;
                                long distance = Math.Abs((long)entry.Position.X) + Math.Abs((long)entry.Position.Y);
                                if (distance == 1 || distance == 2) rings[(int)distance - 1].Add(index);
                            }
                            if (hit.ScopeEntries.Count == 12 && rings[0].Count == 4 && rings[1].Count == 8)
                                mountainScopes[unit.Id] = rings;
                        }
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
            StartMovement startMovement = null, StartBlockedMovement startBlockedMovement = null,
            Func<FightBattleDamageEvent, bool> canStartDamage = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleLiveImpactSession));
            if (transferred) throw new InvalidOperationException("The live step was already transferred.");
            if (float.IsNaN(elapsed) || float.IsInfinity(elapsed) || elapsed < 0f)
                throw new ArgumentOutOfRangeException(nameof(elapsed));
            if (startCast == null) throw new ArgumentNullException(nameof(startCast));
            if (pumping) throw new InvalidOperationException("The live step is already being pumped.");
            lastStartCast = startCast;
            lastStartMovement = startMovement;
            lastStartBlockedMovement = startBlockedMovement;
            lastAfterDamageStarted = afterDamageStarted;
            lastCanStartDamage = canStartDamage;
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
                    if (secondsStatus == null) step.Latch.TryTakeSecondsStatus(out secondsStatus);
                    if (secondsStatus != null)
                    {
                        if (SecondsStatusChanged == null) return progressed;
                        SecondsStatusChanged(secondsStatus.Event);
                        step.Latch.CompleteSecondsStatus(secondsStatus, out long secondsRevision);
                        secondsStatus = null; progressed = true;
                        step.Latch.WaitForWorkerBoundary(secondsRevision);
                        continue;
                    }
                    if (settlement == null) step.Latch.TryTakeSettlement(out settlement);
                    if (settlement?.IsStatusResolution == true)
                    {
                        if (resolvingStatuses.Count == 0
                            || !ReferenceEquals(settlement.Damage, resolvingStatuses.Peek().Damage)
                            || !ReferenceEquals(settlement.Impact, resolvingStatuses.Peek().Impact))
                            throw new InvalidOperationException("The completed status result belongs to another impact.");
                        if (!bridge.TryResolveStatus(settlement, out long statusRevision)) return progressed;
                        resolvingStatuses.Pop(); settlement = null; progressed = true;
                        step.Latch.WaitForWorkerBoundary(statusRevision);
                        continue;
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
                        if (movement.HasBlockedPosition)
                        {
                            if (startBlockedMovement == null) return progressed;
                            var blockedMovement = movement.Movement;
                            bool armed = true, accepted = false, earlyContact = false;
                            void Contact()
                            {
                                if (!armed) return;
                                if (!accepted) { earlyContact = true; return; }
                                OnBlockedMovementContact(blockedMovement);
                            }
                            bool started;
                            try { started = startBlockedMovement(movement.Cast, blockedMovement, movement.BlockedPosition, Contact); }
                            catch { armed = false; throw; }
                            if (!started)
                            {
                                armed = false;
                                if (earlyContact) throw new InvalidOperationException("A declined movement reported contact.");
                                return progressed;
                            }
                            accepted = true;
                            if (earlyContact) OnBlockedMovementContact(blockedMovement);
                        }
                        else if (startMovement == null || !startMovement(movement.Cast, movement.Movement)) return progressed;
                        step.Latch.CompleteMovement(movement, out long revision);
                        movement = null; progressed = true;
                        step.Latch.WaitForWorkerBoundary(revision);
                        continue;
                    }
                    if (selection == null) step.Latch.TryTakeSelection(out selection);
                    if (selection != null)
                    {
                        var active = Find(selection.Cast);
                        var selectedCast = selection.Cast;
                        selectedHits.Add(selection.Selection);
                        long revision = bridge.QueueSelection(selection, active.Id, clock - active.Started);
                        if (reachedMountainWaves.TryGetValue(selectedCast, out int reached))
                        {
                            if ((reached & 1) != 0) RouteMountainWave(selectedCast, active.Id, 1);
                            if ((reached & 2) != 0) RouteMountainWave(selectedCast, active.Id, 2);
                        }
                        if (reachedSlashes.Contains(selectedCast)) RouteSlashImpact(selectedCast, active.Id);
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
                            if (!bridge.TryQueueUnresolved(settlement, active.Id, afterDamageStarted, canStartDamage)) return progressed;
                            resolvingStatuses.Push(settlement);
                            step.Latch.DeferStatusResolution(settlement, out long deferredRevision);
                            settlement = null; progressed = true;
                            step.Latch.WaitForWorkerBoundary(deferredRevision);
                            continue;
                        }
                        if (!bridge.TryQueueSettlement(settlement, active.Id, out long revision,
                            afterDamageStarted, canStartDamage)) return progressed;
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
            Pump(0f, lastStartCast, lastAfterDamageStarted, lastStartMovement, lastStartBlockedMovement, lastCanStartDamage);
        }

        private void OnBlockedMovementContact(FightBattleMovementEvent movement)
        {
            if (disposed || transferred) return;
            if (!step.Latch.RecordBlockedContact(movement, out long revision) || revision <= 0 || pumping) return;
            OnWorkerReleasedByArrival(revision);
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
                try { RouteJumpSlashImpact(cast, prefab, playable); AttachMountainWaves(cast, id, prefab, playable);
                    AttachSlashImpact(cast, id, prefab, playable); }
                finally { currentGraphCast = previousCast; currentGraphSkillId = previousId; }
            }
        }

        private void AttachSlashImpact(FightBattleSkillCastEvent cast, EcEntityIdentifier id,
            GameObject prefab, MyPlayable playable)
        {
            if (cast?.SkillEntityId != 10600 || prefab == null || prefab.name != "playable10600"
                || playable == null || playable.name != "effect" || !slashCasters.Contains(cast.ActorId)
                || observedSlashes.Contains(cast) || !casts.TryGetValue(cast, out var active) || !active.Id.Equals(id)) return;
            var observer = OfficialBattleSlashImpact.Attach(playable, () => RouteSlashImpact(cast, id));
            if (observer == null) return;
            observedSlashes.Add(cast); slashEffects.Add(observer);
        }

        private void RouteSlashImpact(FightBattleSkillCastEvent cast, EcEntityIdentifier id)
        {
            if (disposed || transferred || !observedSlashes.Contains(cast)
                || !casts.TryGetValue(cast, out var active) || !active.Id.Equals(id)) return;
            reachedSlashes.Add(cast);
            if (cast.HitSelections == null) return;
            foreach (var hit in new List<FightBattleHitSelectionSnapshot>(cast.HitSelections))
            {
                if (hit?.HitEntityId != 10601 || !selectedHits.Contains(hit) || hit.Targets == null) continue;
                var sent = new HashSet<int>();
                foreach (var target in new List<FightBattleHitSelectionSnapshot.TargetOccurrence>(hit.Targets))
                {
                    if (!casts.ContainsKey(cast)) return;
                    if (target == null || !target.HasScopeIndex || target.ScopeIndex != 0
                        || !sent.Add(target.EffectCenterIndex) || !sentSlashes.Add((hit, target.EffectCenterIndex))) continue;
                    bridge.RecordArrival(cast, hit, target.EffectCenterIndex, target.ScopeIndex);
                }
            }
        }

        private void AttachMountainWaves(FightBattleSkillCastEvent cast, EcEntityIdentifier id,
            GameObject prefab, MyPlayable playable)
        {
            if (cast?.SkillEntityId != 11120 || prefab == null || prefab.name != "playable11120"
                || playable == null || playable.name != "effect" || !mountainScopes.ContainsKey(cast.ActorId)
                || observedMountains.Contains(cast) || !casts.TryGetValue(cast, out var active) || !active.Id.Equals(id)) return;
            var observer = OfficialBattleMountainWaveImpact.Attach(playable, wave => RouteMountainWave(cast, id, wave));
            if (observer == null) return;
            observedMountains.Add(cast); mountainWaves.Add(observer);
        }

        private void RouteMountainWave(FightBattleSkillCastEvent cast, EcEntityIdentifier id, int wave)
        {
            if (disposed || transferred || wave < 1 || wave > 2 || !observedMountains.Contains(cast)
                || !mountainScopes.TryGetValue(cast.ActorId, out var rings)
                || !casts.TryGetValue(cast, out var active) || !active.Id.Equals(id)) return;
            reachedMountainWaves.TryGetValue(cast, out int reached);
            reachedMountainWaves[cast] = reached | (1 << (wave - 1));
            if (cast.HitSelections == null) return;
            var scopes = rings[wave - 1];
            foreach (var hit in new List<FightBattleHitSelectionSnapshot>(cast.HitSelections))
            {
                if (hit?.HitEntityId != 11121 || !selectedHits.Contains(hit) || hit.Targets == null) continue;
                var sent = new HashSet<(int Center, int Scope)>();
                foreach (var target in new List<FightBattleHitSelectionSnapshot.TargetOccurrence>(hit.Targets))
                {
                    if (!casts.ContainsKey(cast)) return;
                    if (target == null || !target.HasScopeIndex || !scopes.Contains(target.ScopeIndex)
                        || !sent.Add((target.EffectCenterIndex, target.ScopeIndex))) continue;
                    bridge.RecordArrival(cast, hit, target.EffectCenterIndex, target.ScopeIndex);
                }
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
            if (delivered && lastStartCast != null) Pump(0f, lastStartCast, lastAfterDamageStarted,
                lastStartMovement, lastStartBlockedMovement, lastCanStartDamage);
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
            foreach (var observer in mountainWaves) if (observer != null) observer.Detach();
            foreach (var observer in slashEffects) if (observer != null) observer.Detach();
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
            foreach (var observer in mountainWaves) if (observer != null) observer.Detach();
            foreach (var observer in slashEffects) if (observer != null) observer.Detach();
            step.Dispose();
            casts.Clear(); selectedHits.Clear(); jumpSlashScopes.Clear(); mountainScopes.Clear(); observedMountains.Clear(); reachedMountainWaves.Clear(); mountainWaves.Clear();
            slashCasters.Clear(); observedSlashes.Clear(); reachedSlashes.Clear(); sentSlashes.Clear(); slashEffects.Clear();
            resolvingStatuses.Clear();
            SecondsStatusChanged = null;
        }
    }
}
