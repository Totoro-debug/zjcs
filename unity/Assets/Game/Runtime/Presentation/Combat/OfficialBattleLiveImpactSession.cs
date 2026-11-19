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

        private sealed class AreaRingDefinition
        {
            public FightBattleHitRoot Root;
            public FightBattleHitEntity Hit;
            public HashSet<int>[] Scopes;
        }

        private sealed class StartingGraphLeaf
        {
            public EcEntityIdentifier Id;
            public GameObject Prefab;
            public MyPlayable Playable;
        }

        private sealed class FireballFlight
        {
            public EcEntityIdentifier SkillId;
            public FightBattleHitSelectionSnapshot Selection;
            public int CenterIndex;
            public bool Arrived;
            public Action Resume;
        }

        private readonly FightBattleIsolatedStep step;
        private readonly OfficialBattleSkillEntityPlayback views;
        private readonly OfficialBattleImpactViewBridge bridge;
        private readonly Dictionary<FightBattleSkillCastEvent, ActiveCast> casts =
            new Dictionary<FightBattleSkillCastEvent, ActiveCast>();
        private readonly HashSet<FightBattleHitSelectionSnapshot> selectedHits = new HashSet<FightBattleHitSelectionSnapshot>();
        private readonly Dictionary<int, HashSet<int>> jumpSlashScopes = new Dictionary<int, HashSet<int>>();
        private readonly Dictionary<int, HashSet<int>[]> mountainScopes = new Dictionary<int, HashSet<int>[]>();
        private readonly Dictionary<int, HashSet<int>> crossScopes = new Dictionary<int, HashSet<int>>();
        private readonly Dictionary<int, FightBattleHitEntity> windBladeScopes = new Dictionary<int, FightBattleHitEntity>();
        private readonly Dictionary<int, HashSet<int>> pullScopes = new Dictionary<int, HashSet<int>>();
        private readonly HashSet<(FightBattleHitSelectionSnapshot Hit, int Center, int Scope)> sentPullHits =
            new HashSet<(FightBattleHitSelectionSnapshot, int, int)>();
        private readonly HashSet<FightBattleSkillCastEvent> observedWindBlades = new HashSet<FightBattleSkillCastEvent>();
        private readonly Dictionary<FightBattleSkillCastEvent, int> reachedWindBlades = new Dictionary<FightBattleSkillCastEvent, int>();
        private readonly HashSet<(FightBattleHitSelectionSnapshot Hit, int Center, int Scope)> sentWindHits =
            new HashSet<(FightBattleHitSelectionSnapshot, int, int)>();
        private readonly List<OfficialBattleWindBladeImpact> windBlades = new List<OfficialBattleWindBladeImpact>();
        private readonly Dictionary<int, AreaRingDefinition> fallingWindDefinitions = new Dictionary<int, AreaRingDefinition>();
        private readonly HashSet<FightBattleSkillCastEvent> observedFallingWind = new HashSet<FightBattleSkillCastEvent>();
        private readonly Dictionary<FightBattleSkillCastEvent, int> reachedFallingWind = new Dictionary<FightBattleSkillCastEvent, int>();
        private readonly HashSet<(FightBattleHitSelectionSnapshot Hit, int Center, int Scope)> sentFallingWindHits =
            new HashSet<(FightBattleHitSelectionSnapshot, int, int)>();
        private readonly List<OfficialBattleFallingWindImpact> fallingWindEffects = new List<OfficialBattleFallingWindImpact>();
        private readonly Dictionary<int, AreaRingDefinition> fireRingDefinitions = new Dictionary<int, AreaRingDefinition>();
        private readonly HashSet<FightBattleSkillCastEvent> observedFireRings = new HashSet<FightBattleSkillCastEvent>();
        private readonly Dictionary<FightBattleSkillCastEvent, int> reachedFireRings = new Dictionary<FightBattleSkillCastEvent, int>();
        private readonly HashSet<(FightBattleHitSelectionSnapshot Hit, int Center, int Scope)> sentFireRingHits =
            new HashSet<(FightBattleHitSelectionSnapshot, int, int)>();
        private readonly List<OfficialBattleFireRingImpact> fireRingEffects = new List<OfficialBattleFireRingImpact>();
        private readonly HashSet<FightBattleSkillCastEvent> observedSwordWind = new HashSet<FightBattleSkillCastEvent>();
        private readonly Dictionary<FightBattleSkillCastEvent, float> reachedSwordWind = new Dictionary<FightBattleSkillCastEvent, float>();
        private readonly HashSet<(FightBattleHitSelectionSnapshot Hit, int Center)> sentSwordWindHits =
            new HashSet<(FightBattleHitSelectionSnapshot, int)>();
        private readonly List<OfficialBattleSwordWindImpact> swordWindEffects = new List<OfficialBattleSwordWindImpact>();
        private readonly HashSet<FightBattleSkillCastEvent> observedIceHits = new HashSet<FightBattleSkillCastEvent>();
        private readonly Dictionary<FightBattleSkillCastEvent, float> reachedIceHits = new Dictionary<FightBattleSkillCastEvent, float>();
        private readonly HashSet<(FightBattleHitSelectionSnapshot Hit, int Center)> sentIceHits =
            new HashSet<(FightBattleHitSelectionSnapshot, int)>();
        private readonly List<OfficialBattleTargetParticleImpact> iceHitEffects = new List<OfficialBattleTargetParticleImpact>();
        private readonly HashSet<FightBattleSkillCastEvent> observedAuraHits = new HashSet<FightBattleSkillCastEvent>();
        private readonly Dictionary<FightBattleSkillCastEvent, float> reachedAuraHits = new Dictionary<FightBattleSkillCastEvent, float>();
        private readonly HashSet<(FightBattleHitSelectionSnapshot Hit, int Center)> sentAuraHits =
            new HashSet<(FightBattleHitSelectionSnapshot, int)>();
        private readonly List<OfficialBattleTargetParticleImpact> auraHitEffects = new List<OfficialBattleTargetParticleImpact>();
        private readonly HashSet<(FightBattleHitSelectionSnapshot Hit, int Center)> observedWaterProjectiles =
            new HashSet<(FightBattleHitSelectionSnapshot, int)>();
        private readonly List<OfficialBattleProjectileImpact> waterProjectiles = new List<OfficialBattleProjectileImpact>();
        private readonly Dictionary<EcEntityIdentifier, FireballFlight> fireballFlights =
            new Dictionary<EcEntityIdentifier, FireballFlight>();
        private readonly List<OfficialBattleProjectileImpact> fireballProjectiles = new List<OfficialBattleProjectileImpact>();
        private readonly Dictionary<FightBattleSkillCastEvent, HashSet<(FightBattleHitSelectionSnapshot Hit, int Center)>> expectedFireballHits =
            new Dictionary<FightBattleSkillCastEvent, HashSet<(FightBattleHitSelectionSnapshot, int)>>();
        private readonly HashSet<(FightBattleHitSelectionSnapshot Hit, int Center)> completedFireballHits =
            new HashSet<(FightBattleHitSelectionSnapshot, int)>();
        private readonly HashSet<FightBattleSkillCastEvent> observedCrossEffects = new HashSet<FightBattleSkillCastEvent>();
        private readonly HashSet<FightBattleSkillCastEvent> reachedCrossEffects = new HashSet<FightBattleSkillCastEvent>();
        private readonly HashSet<(FightBattleHitSelectionSnapshot Hit, int Center, int Scope)> sentCrossHits =
            new HashSet<(FightBattleHitSelectionSnapshot, int, int)>();
        private readonly HashSet<FightBattleSkillCastEvent> observedMountains = new HashSet<FightBattleSkillCastEvent>();
        private readonly Dictionary<FightBattleSkillCastEvent, int> reachedMountainWaves = new Dictionary<FightBattleSkillCastEvent, int>();
        private readonly List<OfficialBattleMountainWaveImpact> mountainWaves = new List<OfficialBattleMountainWaveImpact>();
        private readonly HashSet<int> slashCasters = new HashSet<int>();
        private readonly HashSet<int> heavyCasters = new HashSet<int>();
        private readonly HashSet<int> comboCasters = new HashSet<int>();
        private readonly HashSet<(FightBattleSkillCastEvent Cast, int Index)> observedComboEffects =
            new HashSet<(FightBattleSkillCastEvent, int)>();
        private readonly Dictionary<FightBattleSkillCastEvent, int> reachedComboEffects =
            new Dictionary<FightBattleSkillCastEvent, int>();
        private readonly HashSet<(FightBattleHitSelectionSnapshot Hit, int Center)> sentComboHits =
            new HashSet<(FightBattleHitSelectionSnapshot, int)>();
        private readonly HashSet<FightBattleSkillCastEvent> observedSlashes = new HashSet<FightBattleSkillCastEvent>();
        private readonly HashSet<FightBattleSkillCastEvent> reachedSlashes = new HashSet<FightBattleSkillCastEvent>();
        private readonly HashSet<(FightBattleHitSelectionSnapshot Hit, int Center)> sentSlashes =
            new HashSet<(FightBattleHitSelectionSnapshot, int)>();
        private readonly List<OfficialBattleSlashImpact> slashEffects = new List<OfficialBattleSlashImpact>();
        private FightBattlePreparedCast preparation;
        private readonly Stack<FightBattlePreparedCast> suspendedPreparations = new Stack<FightBattlePreparedCast>();
        private FightBattlePreparedMovement movement;
        private readonly Stack<FightBattlePreparedMovement> suspendedMovements = new Stack<FightBattlePreparedMovement>();
        private FightBattleSelectedHit selection;
        private readonly Stack<FightBattleSelectedHit> suspendedSelections = new Stack<FightBattleSelectedHit>();
        private FightBattleSettledImpact settlement;
        private readonly Stack<FightBattleSettledImpact> suspendedSettlements = new Stack<FightBattleSettledImpact>();
        private FightBattleSecondsStatusNotice secondsStatus;
        private readonly Stack<FightBattleSecondsStatusNotice> suspendedSecondsStatuses = new Stack<FightBattleSecondsStatusNotice>();
        private readonly Stack<FightBattleSettledImpact> resolvingStatuses = new Stack<FightBattleSettledImpact>();
        private FightBattleCompletedCast completion;
        private readonly Stack<FightBattleCompletedCast> suspendedCompletions = new Stack<FightBattleCompletedCast>();
        private float clock;
        private bool disposed, transferred, pumping;
        private StartCast lastStartCast;
        private StartMovement lastStartMovement;
        private StartBlockedMovement lastStartBlockedMovement;
        private Action<FightBattleDamageEvent> lastAfterDamageStarted;
        private Func<FightBattleDamageEvent, bool> lastCanStartDamage;
        private FightBattleSkillCastEvent currentGraphCast;
        private EcEntityIdentifier currentGraphSkillId;
        private FightBattleSkillCastEvent startingCast;
        private List<StartingGraphLeaf> startingGraphLeaves;

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
                    int comboMask = 0; bool comboValid = true;
                    foreach (var hit in skill.HitGraphEntities)
                    {
                        if (hit?.ScopeEntries == null) continue;
                        if (skill.EntityId == 10630 && hit.EntityId >= 10631 && hit.EntityId <= 10633)
                        {
                            int bit = 1 << (hit.EntityId - 10631);
                            if ((comboMask & bit) != 0 || hit.ScopeEntries.Count != 1 || hit.ScopeEntries[0] == null
                                || hit.ScopeEntries[0].Delay != 0f || hit.ScopeEntries[0].Position.X != 0
                                || hit.ScopeEntries[0].Position.Y != 1) comboValid = false;
                            comboMask |= bit;
                        }
                        if (skill.EntityId == 10600 && hit.EntityId == 10601 && hit.ScopeEntries.Count == 1
                            && hit.ScopeEntries[0] != null && hit.ScopeEntries[0].Delay == 0f
                            && hit.ScopeEntries[0].Position.X == 0 && hit.ScopeEntries[0].Position.Y == 1)
                            slashCasters.Add(unit.Id);
                        if (skill.EntityId == 10610 && hit.EntityId == 10611 && hit.ScopeEntries.Count == 1
                            && hit.ScopeEntries[0] != null && hit.ScopeEntries[0].Delay == 0f
                            && hit.ScopeEntries[0].Position.X == 0 && hit.ScopeEntries[0].Position.Y == 1)
                            heavyCasters.Add(unit.Id);
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
                        if (skill.EntityId == 10650 && hit.EntityId == 10651)
                        {
                            var positions = new HashSet<FightGridPoint>(); var scopes = new HashSet<int>();
                            for (int index = 0; index < hit.ScopeEntries.Count; index++)
                            {
                                var entry = hit.ScopeEntries[index];
                                if (entry == null || entry.Delay != 0f || !positions.Add(entry.Position)) continue;
                                if (Math.Abs((long)entry.Position.X) + Math.Abs((long)entry.Position.Y) == 1) scopes.Add(index);
                            }
                            if (hit.ScopeEntries.Count == 4 && scopes.Count == 4) crossScopes[unit.Id] = scopes;
                        }
                        if (skill.EntityId == 10640 && hit.EntityId == 10641 && hit.ScopeEntries.Count == 4)
                            windBladeScopes[unit.Id] = hit;
                        if (skill.EntityId == 10700 && hit.EntityId == 10701 && hit.ScopeEntries.Count == 9)
                        {
                            var positions = new HashSet<FightGridPoint>(); var scopes = new HashSet<int>();
                            for (int index = 0; index < hit.ScopeEntries.Count; index++)
                            {
                                var entry = hit.ScopeEntries[index];
                                if (entry == null || Math.Abs(entry.Delay - .15f) > .00001f
                                    || Math.Abs((long)entry.Position.X) > 1 || Math.Abs((long)entry.Position.Y) > 1
                                    || !positions.Add(entry.Position)) continue;
                                scopes.Add(index);
                            }
                            if (scopes.Count == 9) pullScopes[unit.Id] = scopes;
                        }
                        if (skill.EntityId == 10590 && hit.EntityId == 10591 && hit.ScopeEntries.Count == 5
                            && skill.HitGraphRoots != null && skill.HitGraphRoots.Count == 1
                            && skill.HitGraphRoots[0]?.EntityId == 10591)
                        {
                            var rings = new[] { new HashSet<int>(), new HashSet<int>() };
                            var positions = new HashSet<FightGridPoint>();
                            for (int index = 0; index < hit.ScopeEntries.Count; index++)
                            {
                                var entry = hit.ScopeEntries[index];
                                if (entry == null || !positions.Add(entry.Position)) continue;
                                long distance = Math.Abs((long)entry.Position.X) + Math.Abs((long)entry.Position.Y);
                                if (distance == 0 && entry.Delay == 0f || distance == 1 && entry.Delay > 0f)
                                    rings[(int)distance].Add(index);
                            }
                            if (rings[0].Count == 1 && rings[1].Count == 4)
                                fallingWindDefinitions[unit.Id] = new AreaRingDefinition {
                                    Root = skill.HitGraphRoots[0], Hit = hit, Scopes = rings };
                        }
                        if (skill.EntityId == 10830 && hit.EntityId == 10831 && hit.ScopeEntries.Count == 13
                            && skill.HitGraphRoots != null && skill.HitGraphRoots.Count == 1
                            && skill.HitGraphRoots[0]?.EntityId == 10831)
                        {
                            var rings = new[] { new HashSet<int>(), new HashSet<int>(), new HashSet<int>() };
                            var positions = new HashSet<FightGridPoint>();
                            for (int index = 0; index < hit.ScopeEntries.Count; index++)
                            {
                                var entry = hit.ScopeEntries[index];
                                if (entry == null || !positions.Add(entry.Position)) continue;
                                long distance = Math.Abs((long)entry.Position.X) + Math.Abs((long)entry.Position.Y);
                                if (distance <= 2) rings[(int)distance].Add(index);
                            }
                            if (rings[0].Count == 1 && rings[1].Count == 4 && rings[2].Count == 8)
                                fireRingDefinitions[unit.Id] = new AreaRingDefinition {
                                    Root = skill.HitGraphRoots[0], Hit = hit, Scopes = rings };
                        }
                    }
                    if (skill.EntityId == 10630 && comboValid && comboMask == 7) comboCasters.Add(unit.Id);
                }
            }
            step = new FightBattleIsolatedStep(checkpoint, restore, sequence, counter, observeMovements);
            bridge = new OfficialBattleImpactViewBridge(views, step.Latch);
            bridge.WorkerReleasedByArrival += OnWorkerReleasedByArrival;
            views.SkillGraphPlayableStarted += OnSkillGraphPlayableStarted;
            views.HitGraphPlayableStarted += OnHitGraphPlayableStarted;
            views.HitViewDeferralRequested += OnHitViewDeferralRequested;
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
                    object current = step.Latch.CurrentWorkerHandoff;
                    CaptureHandoff(current, ref secondsStatus, suspendedSecondsStatuses,
                        () => step.Latch.TryTakeSecondsStatus(out var value) ? value : null);
                    if (secondsStatus != null && ReferenceEquals(current, secondsStatus))
                    {
                        if (SecondsStatusChanged == null) return progressed;
                        SecondsStatusChanged(secondsStatus.Event);
                        step.Latch.CompleteSecondsStatus(secondsStatus, out long secondsRevision);
                        ReleaseHandoff(ref secondsStatus, suspendedSecondsStatuses); progressed = true;
                        step.Latch.WaitForWorkerBoundary(secondsRevision);
                        continue;
                    }
                    CaptureHandoff(current, ref settlement, suspendedSettlements,
                        () => step.Latch.TryTakeSettlement(out var value) ? value : null);
                    if (settlement?.IsStatusResolution == true && ReferenceEquals(current, settlement))
                    {
                        if (resolvingStatuses.Count == 0
                            || !ReferenceEquals(settlement.Damage, resolvingStatuses.Peek().Damage)
                            || !ReferenceEquals(settlement.Impact, resolvingStatuses.Peek().Impact))
                            throw new InvalidOperationException("The completed status result belongs to another impact.");
                        if (!bridge.TryResolveStatus(settlement, out long statusRevision)) return progressed;
                        resolvingStatuses.Pop(); ReleaseHandoff(ref settlement, suspendedSettlements); progressed = true;
                        step.Latch.WaitForWorkerBoundary(statusRevision);
                        continue;
                    }
                    CaptureHandoff(current, ref preparation, suspendedPreparations,
                        () => step.Latch.TryTakePreparation(out var value) ? value : null);
                    if (preparation != null && ReferenceEquals(current, preparation))
                    {
                        var castBeingStarted = preparation.Cast;
                        var earlyLeaves = new List<StartingGraphLeaf>();
                        bool started;
                        EcEntityIdentifier id;
                        startingCast = castBeingStarted; startingGraphLeaves = earlyLeaves;
                        try { started = startCast(castBeingStarted, out id); }
                        finally { startingCast = null; startingGraphLeaves = null; }
                        if (!started)
                        {
                            if (earlyLeaves.Count != 0)
                                throw new InvalidOperationException("A declined cast started original graph leaves.");
                            return progressed;
                        }
                        casts.Add(preparation.Cast, new ActiveCast { Id = id, Started = clock });
                        foreach (var leaf in earlyLeaves)
                        {
                            if (!leaf.Id.Equals(id)) throw new InvalidOperationException("A starting graph leaf belongs to another skill entity.");
                            DispatchSkillGraphPlayableStarted(castBeingStarted, id, leaf.Prefab, leaf.Playable);
                        }
                        step.Latch.CompletePreparation(preparation, out long revision);
                        ReleaseHandoff(ref preparation, suspendedPreparations);
                        progressed = true;
                        step.Latch.WaitForWorkerBoundary(revision);
                        continue;
                    }
                    CaptureHandoff(current, ref movement, suspendedMovements,
                        () => step.Latch.TryTakeMovement(out var value) ? value : null);
                    if (movement != null && ReferenceEquals(current, movement))
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
                        ReleaseHandoff(ref movement, suspendedMovements); progressed = true;
                        step.Latch.WaitForWorkerBoundary(revision);
                        continue;
                    }
                    CaptureHandoff(current, ref selection, suspendedSelections,
                        () => step.Latch.TryTakeSelection(out var value) ? value : null);
                    if (selection != null && ReferenceEquals(current, selection))
                    {
                        var active = Find(selection.Cast);
                        var selectedCast = selection.Cast;
                        selectedHits.Add(selection.Selection);
                        if (selectedCast.SkillEntityId == 10800 && selection.Selection.HitEntityId == 10801
                            && selection.Selection.EffectCenters != null)
                        {
                            if (!expectedFireballHits.TryGetValue(selectedCast, out var expected))
                                expectedFireballHits.Add(selectedCast, expected = new HashSet<(FightBattleHitSelectionSnapshot, int)>());
                            for (int index = 0; index < selection.Selection.EffectCenters.Count; index++)
                                expected.Add((selection.Selection, index));
                        }
                        long revision = bridge.QueueSelection(selection, active.Id, clock - active.Started);
                        if (reachedMountainWaves.TryGetValue(selectedCast, out int reached))
                        {
                            if ((reached & 1) != 0) RouteMountainWave(selectedCast, active.Id, 1);
                            if ((reached & 2) != 0) RouteMountainWave(selectedCast, active.Id, 2);
                        }
                        if (reachedSlashes.Contains(selectedCast)) RouteSlashImpact(selectedCast, active.Id);
                        if (reachedCrossEffects.Contains(selectedCast)) RouteCrossImpact(selectedCast, active.Id);
                        if (reachedWindBlades.TryGetValue(selectedCast, out int windReached))
                            for (int index = 0; index < 4; index++)
                                if ((windReached & (1 << index)) != 0) RouteWindBladeImpact(selectedCast, active.Id, index);
                        if (reachedFallingWind.TryGetValue(selectedCast, out int fallingReached))
                            for (int index = 0; index < 2; index++)
                                if ((fallingReached & (1 << index)) != 0) RouteFallingWindImpact(selectedCast, active.Id, index);
                        if (reachedFireRings.TryGetValue(selectedCast, out int fireRingReached))
                            for (int index = 0; index < 3; index++)
                                if ((fireRingReached & (1 << index)) != 0) RouteFireRingImpact(selectedCast, active.Id, index);
                        if (reachedSwordWind.TryGetValue(selectedCast, out float swordWindDelay))
                            RouteSwordWindImpact(selectedCast, active.Id, swordWindDelay);
                        if (reachedIceHits.TryGetValue(selectedCast, out float iceDelay))
                            RouteIceHitImpact(selectedCast, active.Id, iceDelay);
                        if (reachedAuraHits.TryGetValue(selectedCast, out float auraDelay))
                            RouteAuraHitImpact(selectedCast, active.Id, auraDelay);
                        if (reachedComboEffects.TryGetValue(selectedCast, out int comboReached))
                            for (int index = 0; index < 3; index++)
                                if ((comboReached & (1 << index)) != 0) RouteComboImpact(selectedCast, active.Id, index);
                        ReleaseHandoff(ref selection, suspendedSelections); progressed = true;
                        step.Latch.WaitForWorkerBoundary(revision);
                        continue;
                    }
                    if (settlement != null && ReferenceEquals(current, settlement))
                    {
                        var active = Find(settlement.Impact.Cast);
                        if (!settlement.Damage.HasDamageTriggerStatus)
                        {
                            if (!bridge.TryQueueUnresolved(settlement, active.Id, afterDamageStarted, canStartDamage)) return progressed;
                            resolvingStatuses.Push(settlement);
                            step.Latch.DeferStatusResolution(settlement, out long deferredRevision);
                            ReleaseHandoff(ref settlement, suspendedSettlements); progressed = true;
                            step.Latch.WaitForWorkerBoundary(deferredRevision);
                            continue;
                        }
                        if (!bridge.TryQueueSettlement(settlement, active.Id, out long revision,
                            afterDamageStarted, canStartDamage)) return progressed;
                        ReleaseHandoff(ref settlement, suspendedSettlements); progressed = true;
                        step.Latch.WaitForWorkerBoundary(revision);
                        continue;
                    }
                    CaptureHandoff(current, ref completion, suspendedCompletions,
                        () => step.Latch.TryTakeCastCompletion(out var value) ? value : null);
                    if (completion != null && ReferenceEquals(current, completion))
                    {
                        if (expectedFireballHits.TryGetValue(completion.Cast, out var expected)
                            && !expected.IsSubsetOf(completedFireballHits)) return progressed;
                        var active = Find(completion.Cast);
                        views.CompleteLiveCast(completion.Cast, active.Id, clock - active.Started);
                        step.Latch.CompleteCast(completion, out long revision);
                        casts.Remove(completion.Cast);
                        ReleaseHandoff(ref completion, suspendedCompletions); progressed = true;
                        step.Latch.WaitForWorkerBoundary(revision);
                        continue;
                    }
                    return progressed;
                }
            }
            finally { pumping = false; }
        }

        private static void CaptureHandoff<T>(object current, ref T held, Stack<T> suspended, Func<T> take) where T : class
        {
            if (!(current is T) || ReferenceEquals(current, held)) return;
            T next = take();
            if (!ReferenceEquals(current, next))
                throw new InvalidOperationException("The current rule handoff was not available to the presentation host.");
            if (held != null) suspended.Push(held);
            held = next;
        }

        private static void ReleaseHandoff<T>(ref T held, Stack<T> suspended) where T : class
            => held = suspended.Count > 0 ? suspended.Pop() : null;

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

        private void OnHitGraphPlayableStarted(FightBattleSkillCastEvent cast, FightBattleHitSelectionSnapshot hit,
            int centerIndex, GameObject prefab, MyPlayable playable)
        {
            if (!disposed && !transferred && cast?.SkillEntityId == 10800 && hit?.HitEntityId == 10801
                && prefab != null && prefab.name == "playable_10801_SourceToHit" && playable != null && playable.name == "track"
                && selectedHits.Contains(hit) && casts.TryGetValue(cast, out var fireballCast)
                && views.TryGetHitIdentity(playable.myTarget, out var hitId) && !fireballFlights.ContainsKey(hitId))
            {
                var observer = OfficialBattleProjectileImpact.Attach(playable, hit.DelayDistFactor,
                    () => OnFireballArrived(hitId));
                if (observer != null)
                {
                    fireballFlights.Add(hitId, new FireballFlight { SkillId = fireballCast.Id,
                        Selection = hit, CenterIndex = centerIndex });
                    fireballProjectiles.Add(observer);
                }
            }
            if (!disposed && !transferred && cast?.SkillEntityId == 10500 && hit?.HitEntityId == 10501
                && prefab != null && prefab.name == "playable10501" && playable != null && playable.name == "effect"
                && selectedHits.Contains(hit) && casts.ContainsKey(cast) && hit.Targets != null
                && !observedWaterProjectiles.Contains((hit, centerIndex)))
            {
                var observer = OfficialBattleProjectileImpact.Attach(playable, hit.DelayDistFactor,
                    () => RouteWaterProjectileImpact(cast, hit, centerIndex));
                if (observer != null) { observedWaterProjectiles.Add((hit, centerIndex)); waterProjectiles.Add(observer); }
            }
            if (disposed || transferred || cast?.SkillEntityId != 10700 || hit?.HitEntityId != 10701
                || prefab == null || prefab.name != "playable10701" || playable == null || playable.name != "AtkEffect"
                || !selectedHits.Contains(hit) || !casts.ContainsKey(cast)
                || !pullScopes.TryGetValue(cast.ActorId, out var scopes) || hit.Targets == null) return;
            foreach (var target in new List<FightBattleHitSelectionSnapshot.TargetOccurrence>(hit.Targets))
            {
                if (!casts.ContainsKey(cast)) return;
                if (target == null || target.EffectCenterIndex != centerIndex || !target.HasScopeIndex
                    || !scopes.Contains(target.ScopeIndex)
                    || !sentPullHits.Add((hit, centerIndex, target.ScopeIndex))) continue;
                bridge.RecordArrival(cast, hit, centerIndex, target.ScopeIndex);
            }
        }

        private bool OnHitViewDeferralRequested(EcEntityIdentifier skillId, EcEntityIdentifier hitId, Action resume)
        {
            if (disposed || transferred || resume == null || !fireballFlights.TryGetValue(hitId, out var flight)
                || !flight.SkillId.Equals(skillId)) return false;
            if (flight.Resume != null) throw new InvalidOperationException("The fireball hit view was deferred twice.");
            flight.Resume = resume;
            if (flight.Arrived) ResumeFireballHit(hitId, flight);
            return true;
        }

        private void OnFireballArrived(EcEntityIdentifier hitId)
        {
            if (disposed || transferred || !fireballFlights.TryGetValue(hitId, out var flight)) return;
            flight.Arrived = true;
            if (flight.Resume != null) ResumeFireballHit(hitId, flight);
        }

        private void ResumeFireballHit(EcEntityIdentifier hitId, FireballFlight flight)
        {
            var resume = flight.Resume; flight.Resume = null;
            if (resume == null || !views.HasLiveHit(hitId)) return;
            if (!completedFireballHits.Add((flight.Selection, flight.CenterIndex)))
                throw new InvalidOperationException("The fireball hit view was resumed twice.");
            fireballFlights.Remove(hitId);
            resume();
            if (!pumping && lastStartCast != null) Pump(0f, lastStartCast, lastAfterDamageStarted,
                lastStartMovement, lastStartBlockedMovement, lastCanStartDamage);
        }

        private void RouteWaterProjectileImpact(FightBattleSkillCastEvent cast,
            FightBattleHitSelectionSnapshot hit, int centerIndex)
        {
            if (disposed || transferred || !casts.ContainsKey(cast) || !selectedHits.Contains(hit)
                || !observedWaterProjectiles.Contains((hit, centerIndex)) || hit.Targets == null) return;
            foreach (var target in new List<FightBattleHitSelectionSnapshot.TargetOccurrence>(hit.Targets))
            {
                if (!casts.ContainsKey(cast)) return;
                if (target == null || target.EffectCenterIndex != centerIndex || !target.HasScopeIndex
                    || target.ScopeIndex != 0 || target.ScopeDelay != 0f) continue;
                bridge.RecordArrival(cast, hit, centerIndex, 0);
                break;
            }
        }

        private void OnSkillGraphPlayableStarted(FightBattleSkillCastEvent cast, EcEntityIdentifier id,
            GameObject prefab, MyPlayable playable)
        {
            if (ReferenceEquals(startingCast, cast) && startingGraphLeaves != null)
            {
                startingGraphLeaves.Add(new StartingGraphLeaf { Id = id, Prefab = prefab, Playable = playable });
                return;
            }
            DispatchSkillGraphPlayableStarted(cast, id, prefab, playable);
        }

        private void DispatchSkillGraphPlayableStarted(FightBattleSkillCastEvent cast, EcEntityIdentifier id,
            GameObject prefab, MyPlayable playable)
        {
            var previousCast = currentGraphCast;
            var previousId = currentGraphSkillId;
            currentGraphCast = cast; currentGraphSkillId = id;
            try { SkillGraphPlayableStarted?.Invoke(cast, id, prefab, playable); }
            finally
            {
                try { RouteJumpSlashImpact(cast, prefab, playable); AttachMountainWaves(cast, id, prefab, playable);
                    AttachSlashImpact(cast, id, prefab, playable); AttachComboImpact(cast, id, prefab, playable);
                    AttachCrossImpact(cast, id, prefab, playable); AttachWindBladeImpact(cast, id, prefab, playable);
                    AttachFallingWindImpact(cast, id, prefab, playable); AttachFireRingImpact(cast, id, prefab, playable);
                    AttachSwordWindImpact(cast, id, prefab, playable);
                    AttachIceHitImpact(cast, id, prefab, playable); AttachAuraHitImpact(cast, id, prefab, playable); }
                finally { currentGraphCast = previousCast; currentGraphSkillId = previousId; }
            }
        }

        private void AttachAuraHitImpact(FightBattleSkillCastEvent cast, EcEntityIdentifier id,
            GameObject prefab, MyPlayable playable)
        {
            if (cast?.SkillEntityId != 10850 || prefab == null || prefab.name != "playable10850"
                || playable == null || playable.name != "effect" || observedAuraHits.Contains(cast)
                || !casts.TryGetValue(cast, out var active) || !active.Id.Equals(id)) return;
            var observer = OfficialBattleTargetParticleImpact.Attach(prefab, playable, "hit (4)",
                delay => RouteAuraHitImpact(cast, id, delay));
            if (observer == null) return;
            observedAuraHits.Add(cast); auraHitEffects.Add(observer);
        }

        private void RouteAuraHitImpact(FightBattleSkillCastEvent cast, EcEntityIdentifier id, float hitDelay)
        {
            if (disposed || transferred || !observedAuraHits.Contains(cast)
                || !casts.TryGetValue(cast, out var active) || !active.Id.Equals(id)) return;
            reachedAuraHits[cast] = hitDelay;
            if (cast.HitSelections == null) return;
            foreach (var hit in new List<FightBattleHitSelectionSnapshot>(cast.HitSelections))
            {
                if (hit?.HitEntityId != 10851 || !selectedHits.Contains(hit) || hit.Targets == null
                    || Math.Abs(hit.Delay - hitDelay) > .00001f) continue;
                foreach (var target in new List<FightBattleHitSelectionSnapshot.TargetOccurrence>(hit.Targets))
                {
                    if (!casts.ContainsKey(cast)) return;
                    if (target == null || target.EffectCenterIndex != 0 || !target.HasScopeIndex
                        || target.ScopeIndex != 0 || target.ScopeDelay != 0f
                        || !sentAuraHits.Add((hit, target.EffectCenterIndex))) continue;
                    bridge.RecordArrival(cast, hit, target.EffectCenterIndex, 0);
                }
            }
        }

        private void AttachIceHitImpact(FightBattleSkillCastEvent cast, EcEntityIdentifier id,
            GameObject prefab, MyPlayable playable)
        {
            if (cast?.SkillEntityId != 11300 || prefab == null || prefab.name != "playable11300"
                || playable == null || playable.name != "effect" || observedIceHits.Contains(cast)
                || !casts.TryGetValue(cast, out var active) || !active.Id.Equals(id)) return;
            var observer = OfficialBattleTargetParticleImpact.Attach(prefab, playable, "hit_da",
                delay => RouteIceHitImpact(cast, id, delay));
            if (observer == null) return;
            observedIceHits.Add(cast); iceHitEffects.Add(observer);
        }

        private void RouteIceHitImpact(FightBattleSkillCastEvent cast, EcEntityIdentifier id, float hitDelay)
        {
            if (disposed || transferred || !observedIceHits.Contains(cast)
                || !casts.TryGetValue(cast, out var active) || !active.Id.Equals(id)) return;
            reachedIceHits[cast] = hitDelay;
            if (cast.HitSelections == null) return;
            foreach (var hit in new List<FightBattleHitSelectionSnapshot>(cast.HitSelections))
            {
                if (hit?.HitEntityId != 11301 || !selectedHits.Contains(hit) || hit.Targets == null
                    || Math.Abs(hit.Delay - hitDelay) > .00001f) continue;
                foreach (var target in new List<FightBattleHitSelectionSnapshot.TargetOccurrence>(hit.Targets))
                {
                    if (!casts.ContainsKey(cast)) return;
                    if (target == null || target.EffectCenterIndex != 0 || !target.HasScopeIndex
                        || target.ScopeIndex != 0 || target.ScopeDelay != 0f
                        || !sentIceHits.Add((hit, target.EffectCenterIndex))) continue;
                    bridge.RecordArrival(cast, hit, target.EffectCenterIndex, 0);
                }
            }
        }

        private void AttachSwordWindImpact(FightBattleSkillCastEvent cast, EcEntityIdentifier id,
            GameObject prefab, MyPlayable playable)
        {
            if (cast?.SkillEntityId != 11290 || prefab == null || prefab.name != "playable11290"
                || playable == null || playable.name != "effect" || observedSwordWind.Contains(cast)
                || !casts.TryGetValue(cast, out var active) || !active.Id.Equals(id)) return;
            var observer = OfficialBattleSwordWindImpact.Attach(prefab, playable,
                delay => RouteSwordWindImpact(cast, id, delay));
            if (observer == null) return;
            observedSwordWind.Add(cast); swordWindEffects.Add(observer);
        }

        private void RouteSwordWindImpact(FightBattleSkillCastEvent cast, EcEntityIdentifier id, float hitDelay)
        {
            if (disposed || transferred || !observedSwordWind.Contains(cast)
                || !casts.TryGetValue(cast, out var active) || !active.Id.Equals(id)) return;
            reachedSwordWind[cast] = hitDelay;
            if (cast.HitSelections == null) return;
            foreach (var hit in new List<FightBattleHitSelectionSnapshot>(cast.HitSelections))
            {
                if (hit?.HitEntityId != 11291 || !selectedHits.Contains(hit) || hit.Targets == null
                    || Math.Abs(hit.Delay - hitDelay) > .00001f) continue;
                foreach (var target in new List<FightBattleHitSelectionSnapshot.TargetOccurrence>(hit.Targets))
                {
                    if (!casts.ContainsKey(cast)) return;
                    if (target == null || target.EffectCenterIndex != 0 || !target.HasScopeIndex
                        || target.ScopeIndex != 0 || target.ScopeDelay != 0f
                        || !sentSwordWindHits.Add((hit, target.EffectCenterIndex))) continue;
                    bridge.RecordArrival(cast, hit, target.EffectCenterIndex, 0);
                }
            }
        }

        private void AttachFireRingImpact(FightBattleSkillCastEvent cast, EcEntityIdentifier id,
            GameObject prefab, MyPlayable playable)
        {
            if (cast?.SkillEntityId != 10830 || prefab == null || prefab.name != "playable10830"
                || playable == null || playable.name != "effect" || observedFireRings.Contains(cast)
                || !fireRingDefinitions.TryGetValue(cast.ActorId, out var definition)
                || !casts.TryGetValue(cast, out var active) || !active.Id.Equals(id)) return;
            var observer = OfficialBattleFireRingImpact.Attach(prefab, playable, definition.Root, definition.Hit,
                index => RouteFireRingImpact(cast, id, index));
            if (observer == null) return;
            observedFireRings.Add(cast); fireRingEffects.Add(observer);
        }

        private void RouteFireRingImpact(FightBattleSkillCastEvent cast, EcEntityIdentifier id, int ring)
        {
            if (disposed || transferred || ring < 0 || ring > 2 || !observedFireRings.Contains(cast)
                || !fireRingDefinitions.TryGetValue(cast.ActorId, out var definition)
                || !casts.TryGetValue(cast, out var active) || !active.Id.Equals(id)) return;
            reachedFireRings.TryGetValue(cast, out int reached);
            reachedFireRings[cast] = reached | (1 << ring);
            if (cast.HitSelections == null) return;
            foreach (var hit in new List<FightBattleHitSelectionSnapshot>(cast.HitSelections))
            {
                if (hit?.HitEntityId != 10831 || !selectedHits.Contains(hit) || hit.Targets == null) continue;
                foreach (var target in new List<FightBattleHitSelectionSnapshot.TargetOccurrence>(hit.Targets))
                {
                    if (!casts.ContainsKey(cast)) return;
                    if (target == null || !target.HasScopeIndex || !definition.Scopes[ring].Contains(target.ScopeIndex)
                        || !sentFireRingHits.Add((hit, target.EffectCenterIndex, target.ScopeIndex))) continue;
                    bridge.RecordArrival(cast, hit, target.EffectCenterIndex, target.ScopeIndex);
                }
            }
        }

        private void AttachFallingWindImpact(FightBattleSkillCastEvent cast, EcEntityIdentifier id,
            GameObject prefab, MyPlayable playable)
        {
            if (cast?.SkillEntityId != 10590 || prefab == null || prefab.name != "playable10590"
                || playable == null || playable.name != "effect" || observedFallingWind.Contains(cast)
                || !fallingWindDefinitions.TryGetValue(cast.ActorId, out var definition)
                || !casts.TryGetValue(cast, out var active) || !active.Id.Equals(id)) return;
            var observer = OfficialBattleFallingWindImpact.Attach(prefab, playable, definition.Root, definition.Hit,
                index => RouteFallingWindImpact(cast, id, index));
            if (observer == null) return;
            observedFallingWind.Add(cast); fallingWindEffects.Add(observer);
        }

        private void RouteFallingWindImpact(FightBattleSkillCastEvent cast, EcEntityIdentifier id, int ring)
        {
            if (disposed || transferred || ring < 0 || ring > 1 || !observedFallingWind.Contains(cast)
                || !fallingWindDefinitions.TryGetValue(cast.ActorId, out var definition)
                || !casts.TryGetValue(cast, out var active) || !active.Id.Equals(id)) return;
            reachedFallingWind.TryGetValue(cast, out int reached);
            reachedFallingWind[cast] = reached | (1 << ring);
            if (cast.HitSelections == null) return;
            foreach (var hit in new List<FightBattleHitSelectionSnapshot>(cast.HitSelections))
            {
                if (hit?.HitEntityId != 10591 || !selectedHits.Contains(hit) || hit.Targets == null) continue;
                foreach (var target in new List<FightBattleHitSelectionSnapshot.TargetOccurrence>(hit.Targets))
                {
                    if (!casts.ContainsKey(cast)) return;
                    if (target == null || !target.HasScopeIndex || !definition.Scopes[ring].Contains(target.ScopeIndex)
                        || !sentFallingWindHits.Add((hit, target.EffectCenterIndex, target.ScopeIndex))) continue;
                    bridge.RecordArrival(cast, hit, target.EffectCenterIndex, target.ScopeIndex);
                }
            }
        }

        private void AttachWindBladeImpact(FightBattleSkillCastEvent cast, EcEntityIdentifier id,
            GameObject prefab, MyPlayable playable)
        {
            if (cast?.SkillEntityId != 10640 || prefab == null || prefab.name != "playable10640"
                || playable == null || playable.name != "effect (2)" || observedWindBlades.Contains(cast)
                || !windBladeScopes.TryGetValue(cast.ActorId, out var hit)
                || !casts.TryGetValue(cast, out var active) || !active.Id.Equals(id)) return;
            var observer = OfficialBattleWindBladeImpact.Attach(prefab, playable, hit,
                index => RouteWindBladeImpact(cast, id, index));
            if (observer == null) return;
            observedWindBlades.Add(cast); windBlades.Add(observer);
        }

        private void RouteWindBladeImpact(FightBattleSkillCastEvent cast, EcEntityIdentifier id, int index)
        {
            if (disposed || transferred || index < 0 || index >= 4 || !observedWindBlades.Contains(cast)
                || !casts.TryGetValue(cast, out var active) || !active.Id.Equals(id)) return;
            reachedWindBlades.TryGetValue(cast, out int reached);
            reachedWindBlades[cast] = reached | (1 << index);
            if (cast.HitSelections == null) return;
            foreach (var hit in new List<FightBattleHitSelectionSnapshot>(cast.HitSelections))
            {
                if (hit?.HitEntityId != 10641 || !selectedHits.Contains(hit) || hit.Targets == null) continue;
                foreach (var target in new List<FightBattleHitSelectionSnapshot.TargetOccurrence>(hit.Targets))
                {
                    if (!casts.ContainsKey(cast)) return;
                    if (target == null || !target.HasScopeIndex || target.ScopeIndex != index
                        || !sentWindHits.Add((hit, target.EffectCenterIndex, index))) continue;
                    bridge.RecordArrival(cast, hit, target.EffectCenterIndex, index);
                }
            }
        }

        private void AttachCrossImpact(FightBattleSkillCastEvent cast, EcEntityIdentifier id,
            GameObject prefab, MyPlayable playable)
        {
            if (cast?.SkillEntityId != 10650 || prefab == null || prefab.name != "playable10650"
                || playable == null || playable.name != "effect" || !crossScopes.ContainsKey(cast.ActorId)
                || observedCrossEffects.Contains(cast) || !casts.TryGetValue(cast, out var active) || !active.Id.Equals(id)) return;
            var observer = OfficialBattleSlashImpact.Attach(playable, "shizi", () => RouteCrossImpact(cast, id));
            if (observer == null) return;
            observedCrossEffects.Add(cast); slashEffects.Add(observer);
        }

        private void RouteCrossImpact(FightBattleSkillCastEvent cast, EcEntityIdentifier id)
        {
            if (disposed || transferred || !observedCrossEffects.Contains(cast)
                || !crossScopes.TryGetValue(cast.ActorId, out var scopes)
                || !casts.TryGetValue(cast, out var active) || !active.Id.Equals(id)) return;
            reachedCrossEffects.Add(cast);
            if (cast.HitSelections == null) return;
            foreach (var hit in new List<FightBattleHitSelectionSnapshot>(cast.HitSelections))
            {
                if (hit?.HitEntityId != 10651 || !selectedHits.Contains(hit) || hit.Targets == null) continue;
                foreach (var target in new List<FightBattleHitSelectionSnapshot.TargetOccurrence>(hit.Targets))
                {
                    if (!casts.ContainsKey(cast)) return;
                    if (target == null || !target.HasScopeIndex || !scopes.Contains(target.ScopeIndex)
                        || !sentCrossHits.Add((hit, target.EffectCenterIndex, target.ScopeIndex))) continue;
                    bridge.RecordArrival(cast, hit, target.EffectCenterIndex, target.ScopeIndex);
                }
            }
        }

        private void AttachComboImpact(FightBattleSkillCastEvent cast, EcEntityIdentifier id,
            GameObject prefab, MyPlayable playable)
        {
            if (cast?.SkillEntityId != 10630 || prefab == null || prefab.name != "playable10630"
                || playable == null || !comboCasters.Contains(cast.ActorId)
                || !casts.TryGetValue(cast, out var active) || !active.Id.Equals(id)) return;
            int index = playable.name == "effect (1)" ? 0 : playable.name == "effect (2)" ? 1
                : playable.name == "effect (3)" ? 2 : -1;
            if (index < 0 || observedComboEffects.Contains((cast, index))) return;
            var observer = OfficialBattleSlashImpact.Attach(playable,
                index == 0 ? "daoguang da (24)" : "daoguang da (28)", () => RouteComboImpact(cast, id, index));
            if (observer == null) return;
            observedComboEffects.Add((cast, index)); slashEffects.Add(observer);
        }

        private void RouteComboImpact(FightBattleSkillCastEvent cast, EcEntityIdentifier id, int index)
        {
            if (disposed || transferred || !observedComboEffects.Contains((cast, index))
                || !casts.TryGetValue(cast, out var active) || !active.Id.Equals(id)) return;
            reachedComboEffects.TryGetValue(cast, out int reached);
            reachedComboEffects[cast] = reached | (1 << index);
            if (cast.HitSelections == null) return;
            foreach (var hit in new List<FightBattleHitSelectionSnapshot>(cast.HitSelections))
            {
                if (hit?.HitEntityId != 10631 + index || !selectedHits.Contains(hit) || hit.Targets == null) continue;
                foreach (var target in new List<FightBattleHitSelectionSnapshot.TargetOccurrence>(hit.Targets))
                {
                    if (!casts.ContainsKey(cast)) return;
                    if (target == null || !target.HasScopeIndex || target.ScopeIndex != 0
                        || !sentComboHits.Add((hit, target.EffectCenterIndex))) continue;
                    bridge.RecordArrival(cast, hit, target.EffectCenterIndex, target.ScopeIndex);
                }
            }
        }

        private void AttachSlashImpact(FightBattleSkillCastEvent cast, EcEntityIdentifier id,
            GameObject prefab, MyPlayable playable)
        {
            if (cast == null || prefab == null || playable == null || observedSlashes.Contains(cast)
                || !casts.TryGetValue(cast, out var active) || !active.Id.Equals(id)) return;
            bool slash = cast.SkillEntityId == 10600 && prefab.name == "playable10600"
                && playable.name == "effect" && slashCasters.Contains(cast.ActorId);
            bool heavy = cast.SkillEntityId == 10610 && prefab.name == "playable10610"
                && playable.name == "effect (2)" && heavyCasters.Contains(cast.ActorId);
            if (!slash && !heavy) return;
            // The second heavy-chop effect's ground ring starts at the authored
            // hit boundary; observe that original particle rather than a timer.
            var observer = OfficialBattleSlashImpact.Attach(playable,
                heavy ? "dimian_guangyun_an" : "daoguang da (23)", () => RouteSlashImpact(cast, id));
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
                int hitEntityId = cast.SkillEntityId == 10610 ? 10611 : 10601;
                if (hit?.HitEntityId != hitEntityId || !selectedHits.Contains(hit) || hit.Targets == null) continue;
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
            if (settlement == null || !ReferenceEquals(settlement, step.Latch.CurrentWorkerHandoff)) return false;
            bool delivered = false;
            pumping = true;
            try
            {
                if (!step.Latch.TryCompleteSettlement(settlement, deliver, out long revision)) return false;
                ReleaseHandoff(ref settlement, suspendedSettlements);
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
            views.HitGraphPlayableStarted -= OnHitGraphPlayableStarted;
            views.HitViewDeferralRequested -= OnHitViewDeferralRequested;
            bridge.Dispose();
            foreach (var observer in mountainWaves) if (observer != null) observer.Detach();
            foreach (var observer in slashEffects) if (observer != null) observer.Detach();
            foreach (var observer in windBlades) if (observer != null) observer.Detach();
            foreach (var observer in fallingWindEffects) if (observer != null) observer.Detach();
            foreach (var observer in fireRingEffects) if (observer != null) observer.Detach();
            foreach (var observer in swordWindEffects) if (observer != null) observer.Detach();
            foreach (var observer in iceHitEffects) if (observer != null) observer.Detach();
            foreach (var observer in auraHitEffects) if (observer != null) observer.Detach();
            foreach (var observer in waterProjectiles) if (observer != null) observer.Detach();
            foreach (var observer in fireballProjectiles) if (observer != null) observer.Detach();
            transferred = true;
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            bridge.WorkerReleasedByArrival -= OnWorkerReleasedByArrival;
            views.SkillGraphPlayableStarted -= OnSkillGraphPlayableStarted;
            views.HitGraphPlayableStarted -= OnHitGraphPlayableStarted;
            views.HitViewDeferralRequested -= OnHitViewDeferralRequested;
            bridge.Dispose();
            foreach (var observer in mountainWaves) if (observer != null) observer.Detach();
            foreach (var observer in slashEffects) if (observer != null) observer.Detach();
            foreach (var observer in windBlades) if (observer != null) observer.Detach();
            foreach (var observer in fallingWindEffects) if (observer != null) observer.Detach();
            foreach (var observer in fireRingEffects) if (observer != null) observer.Detach();
            foreach (var observer in swordWindEffects) if (observer != null) observer.Detach();
            foreach (var observer in iceHitEffects) if (observer != null) observer.Detach();
            foreach (var observer in auraHitEffects) if (observer != null) observer.Detach();
            foreach (var observer in waterProjectiles) if (observer != null) observer.Detach();
            foreach (var observer in fireballProjectiles) if (observer != null) observer.Detach();
            step.Dispose();
            casts.Clear(); selectedHits.Clear(); jumpSlashScopes.Clear(); mountainScopes.Clear(); crossScopes.Clear();
            observedCrossEffects.Clear(); reachedCrossEffects.Clear(); sentCrossHits.Clear(); observedMountains.Clear(); reachedMountainWaves.Clear(); mountainWaves.Clear();
            windBladeScopes.Clear(); observedWindBlades.Clear(); reachedWindBlades.Clear(); sentWindHits.Clear(); windBlades.Clear();
            pullScopes.Clear(); sentPullHits.Clear();
            fallingWindDefinitions.Clear(); observedFallingWind.Clear(); reachedFallingWind.Clear();
            sentFallingWindHits.Clear(); fallingWindEffects.Clear();
            fireRingDefinitions.Clear(); observedFireRings.Clear(); reachedFireRings.Clear();
            sentFireRingHits.Clear(); fireRingEffects.Clear();
            observedSwordWind.Clear(); reachedSwordWind.Clear(); sentSwordWindHits.Clear(); swordWindEffects.Clear();
            observedIceHits.Clear(); reachedIceHits.Clear(); sentIceHits.Clear(); iceHitEffects.Clear();
            observedAuraHits.Clear(); reachedAuraHits.Clear(); sentAuraHits.Clear(); auraHitEffects.Clear();
            observedWaterProjectiles.Clear(); waterProjectiles.Clear();
            fireballFlights.Clear(); fireballProjectiles.Clear();
            expectedFireballHits.Clear(); completedFireballHits.Clear();
            slashCasters.Clear(); heavyCasters.Clear(); comboCasters.Clear(); observedComboEffects.Clear();
            reachedComboEffects.Clear(); sentComboHits.Clear(); observedSlashes.Clear(); reachedSlashes.Clear(); sentSlashes.Clear(); slashEffects.Clear();
            resolvingStatuses.Clear();
            suspendedPreparations.Clear();
            SecondsStatusChanged = null;
        }
    }
}
