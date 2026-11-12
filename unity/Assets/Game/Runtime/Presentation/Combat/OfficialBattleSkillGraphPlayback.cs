using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.StoryPlayableManager;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Dispatches each settled skill cast into its original FightSkill prefab.</summary>
    public sealed class OfficialBattleSkillGraphPlayback : IBattleMovementActionView, IBattleMovementSkillView, IDisposable
    {
        private sealed class ScheduledMove
        {
            public FightBattleMovementEvent Movement;
            public Func<int, Transform> UnitTransform;
            public float Remaining;
        }


        private sealed class ScheduledStatusAction
        {
            public FightBattleStatusCustomActionEvent Action;
            public float Remaining;
        }

        private sealed class ScheduledCast
        {
            public FightBattleSkillCastEvent Cast;
            public Func<int, Transform> UnitTransform;
            public Func<int, Transform> PetTransform;
            public IReadOnlyDictionary<int, FightBattleDamageEvent> DamageReceipts;
            public float Remaining;
        }

        private readonly OfficialBattleSkillGraphCatalog catalog;
        private readonly OfflineBattleNativePlayableScope scope;
        private readonly OfficialBattleSkillEntityPlayback entityViews;
        private readonly List<ScheduledCast> pending = new List<ScheduledCast>();
        private readonly List<ScheduledMove> pendingMoves = new List<ScheduledMove>();
        private readonly List<ScheduledStatusAction> pendingStatusActions = new List<ScheduledStatusAction>();
        private readonly HashSet<FightBattleStatusCustomActionEvent> nativeStatusActions =
            new HashSet<FightBattleStatusCustomActionEvent>();
        private readonly HashSet<int> nativeDisperseIndices = new HashSet<int>();
        private OfficialBattleGridEntityPlayback gridViews;
        private OfficialBattleAirComboPlayback airComboViews;
        private OfficialBattleStatusPlayback statusViews;
        private Func<int, bool> hasStatusIcon;
        private OfficialBattleUnitEndPlayback unitEnds;
        private Action<GameObject> bindPlayerClones;
        private readonly Zjcs.Offline.ProfessionUI.EmbeddedMonsterMecanimResources embeddedMecanim;
        private bool disposed;

        public OfficialBattleSkillGraphPlayback(OfficialBattleSkillGraphCatalog catalog,
            OfflineBattleNativePlayableScope scope, OfficialBattleSkillEntityPlayback entityViews)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
            this.entityViews = entityViews ?? throw new ArgumentNullException(nameof(entityViews));
            embeddedMecanim = new Zjcs.Offline.ProfessionUI.EmbeddedMonsterMecanimResources();
            this.scope.BindNativePrefabAdapter((sourcePrefab, instance) =>
            {
                Zjcs.Offline.ProfessionUI.PartnerNativeSkillSkeleton.BindOriginalPrefab(instance);
                Zjcs.Offline.ProfessionUI.EmbeddedMonsterNativeSkeleton.BindOriginalPrefab(instance);
                embeddedMecanim.BindOriginalPrefab(sourcePrefab, instance);
                foreach (var apply in instance.GetComponentsInChildren<Zjcs.Official.StoryGroundEffects.ApplyByGroundType>(true))
                    apply.Host = entityViews.GroundEffects;
                bindPlayerClones?.Invoke(instance);
            });
        }

        public bool OwnsMovement(FightBattleMovementEvent movement) => entityViews.OwnsMovement(movement);

        public bool OwnsMovementSkill(FightBattleMovementEvent movement) => OwnsMovement(movement)
            && movement.OrdinarySkillPresentation != null
            && FightOrdinaryMovementTiming.Supports(movement.MoveSkillEntityId)
            && catalog.ContainsSkill(movement.MoveSkillEntityId);

        public int CatalogCount => catalog.Count;
        public void BindDamageUI(Transform parent, int stageType) => entityViews.BindDamageUI(parent, stageType);
        public void SetDamageUIVisible(bool visible) => entityViews.SetDamageUIVisible(visible);
        public bool OwnsReplayUI(FightBattleSkillCastEvent cast) => entityViews.HasDamageUI
            && cast?.Presentation != null && catalog.ContainsSkill(cast.SkillEntityId);
        public bool OwnsStatusCustomActionUI(FightBattleStatusCustomActionEvent action)
            => action != null && nativeStatusActions.Contains(action);
        public bool OwnsDisperseUI(int operationIndex) => nativeDisperseIndices.Contains(operationIndex);
        public bool OwnsDamageUI(FightBattleStepEvent step, FightBattleDamageEvent damage)
        {
            if (entityViews.HasDamageUI && damage?.IsSkillCost == true && step?.SkillCasts != null)
                foreach (var cast in step.SkillCasts)
                    if (OwnsReplayUI(cast) && cast.HasSkillCostHp && cast.ActorId == damage.SourceId
                        && cast.ActorId == damage.TargetId && cast.SkillEntityId == damage.SkillEntityId
                        && cast.Delay == damage.Delay && cast.SkillCostBeforeHp == damage.BeforeHp
                        && cast.SkillCostAfterHp == damage.AfterHp) return true;
            if (!entityViews.HasDamageUI || !OfficialBattleDamageReceiptPlayback.CanCreate(damage)
                || step?.SkillCasts == null) return false;
            foreach (var cast in step.SkillCasts)
                if (OwnsReplayUI(cast) && cast.ActorId == damage.SourceId
                    && cast.SkillEntityId == damage.SkillEntityId
                    && cast.DamageEventIds != null && cast.DamageEventIds.Contains(damage.DamageId)) return true;
            return false;
        }
        public OfflineBattleNativePlayableScope Scope => scope;
        public Transform TryGetGridTransform(int id) => gridViews?.TryGetTransform(id);
        public void RefreshGridSnapshot(FightBattleCheckpoint checkpoint)
        { gridViews?.RefreshSnapshot(checkpoint); statusViews?.Restore(checkpoint); }

        public void BindGridEntities(Transform root, OfficialBattleGridMapper mapper,
            FightBattleCheckpoint checkpoint, Func<int, Transform> unitTransform, Func<int, bool> hasStatusIcon)
        {
            if (gridViews != null) throw new InvalidOperationException("Native grid entities are already bound.");
            this.hasStatusIcon = hasStatusIcon ?? throw new ArgumentNullException(nameof(hasStatusIcon));
            gridViews = new OfficialBattleGridEntityPlayback(root, mapper, scope, entityViews,
                checkpoint, unitTransform, hasStatusIcon);
            gridViews.Restore();
        }

        public void BindSourceDeathView(Zjcs.Official.StoryPlayables.IBattleSourceDeathView view)
            => scope.BindSourceDeathView(view);

        internal void BindUnitEnds(OfficialBattleUnitEndPlayback value)
        {
            if (unitEnds != null) throw new InvalidOperationException("Unit removal playback is already bound.");
            unitEnds = value ?? throw new ArgumentNullException(nameof(value));
        }

        public void BindAirCombo(FightBattleExecutor executor, Transform root, Func<int, Transform> body)
        {
            if (airComboViews != null) throw new InvalidOperationException("Original airborne status views are already bound.");
            airComboViews = new OfficialBattleAirComboPlayback(executor, root, scope, body);
            airComboViews.Synchronize();
            statusViews = new OfficialBattleStatusPlayback(executor, root, scope, body, hasStatusIcon, "Combat/dark-status");
            statusViews.Restore();
        }

        public void BindPlayerClonePrefabs(Action<GameObject> bind)
        {
            if (bindPlayerClones != null) throw new InvalidOperationException("Player clone prefabs are already bound.");
            bindPlayerClones = bind ?? throw new ArgumentNullException(nameof(bind));
        }

        public void BindSceneHosts(IBattleCameraOffsetHost cameraHost,
            IBattleCharacterPartVisibilityHost visibilityHost = null, IBattleMasterEntityResolver masterResolver = null)
        {
            scope.BindHosts(cameraHost, visibilityHost, cameraHost as IBattleCameraPerformanceHost, masterResolver);
        }

        public void Play(FightBattleStepEvent stepEvent, FightBattleCheckpoint checkpoint,
            Func<int, Transform> unitTransform, Func<int, Transform> petTransform = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleSkillGraphPlayback));
            nativeStatusActions.Clear();
            nativeDisperseIndices.Clear();
            if (stepEvent?.SkillCasts == null || checkpoint?.Units == null || unitTransform == null) return;
            airComboViews?.Synchronize();
            statusViews?.Enqueue(stepEvent);
            statusViews?.DispatchDue();
            gridViews?.Enqueue(stepEvent, checkpoint);
            gridViews?.DispatchDue();
            Func<int, Transform> resolveTransform = id => gridViews?.TryGetTransform(id) ?? unitTransform(id);
            entityViews.BindDamageUITargets(resolveTransform);
            ScheduleMovements(stepEvent, resolveTransform);
            var damageReceipts = new Dictionary<int, FightBattleDamageEvent>();
            if (stepEvent.DamageEvents != null) foreach (var damage in stepEvent.DamageEvents)
            {
                if (damage == null || damage.DamageId <= 0) continue;
                // Derived settlements (for example lifesteal) retain their
                // originating hit serial but create no original damage entity.
                // Preserve them in the step for statistics/compatible UI; only
                // the configured direct occurrence owns native entity playback.
                if (!damageReceipts.TryGetValue(damage.DamageId, out var previous)
                    || previous.DamageEntityId == 0) damageReceipts[damage.DamageId] = damage;
                else if (damage.DamageEntityId != 0)
                    throw new InvalidOperationException("Duplicate original damage entity receipt: " + damage.DamageId);
            }
            ScheduleDisperses(stepEvent);
            foreach (FightBattleSkillCastEvent cast in stepEvent.SkillCasts)
            {
                if (cast == null || cast.ActorId <= 0 || cast.SkillEntityId <= 0) continue;
                FightBattleUnitState actor = FindUnit(checkpoint, cast.ActorId);
                Transform source = cast.IsPetSupport ? petTransform?.Invoke(cast.ActorId) : resolveTransform(cast.ActorId);
                if (actor == null || source == null && actor.CharacterType != 6) continue;
                int targetId = TargetId(cast);
                if (!catalog.ContainsSkill(cast.SkillEntityId)) continue;
                var scheduled = new ScheduledCast
                {
                    Cast = cast,
                    UnitTransform = resolveTransform,
                    PetTransform = petTransform,
                    DamageReceipts = damageReceipts,
                    Remaining = Mathf.Max(0f, cast.Delay)
                };
                if (scheduled.Remaining <= 0f && source != null) Start(scheduled, source, resolveTransform(targetId));
                else
                {
                    int index = pending.FindIndex(value => value.Remaining > scheduled.Remaining);
                    if (index < 0) pending.Add(scheduled);
                    else pending.Insert(index, scheduled);
                }
            }
            DispatchMoves();
            ScheduleStatusActions(stepEvent);
            DispatchStatusActions();
        }

        private void ScheduleStatusActions(FightBattleStepEvent step)
        {
            if (step.StatusCustomActions == null) return;
            foreach (var action in step.StatusCustomActions)
            {
                if (action == null) continue;
                if (action.SourceDamageId == 0)
                {
                    // Legacy/unassociated actions retain their existing boundary.
                    if (entityViews.PlayStatusCustomAction(action)) nativeStatusActions.Add(action);
                    continue;
                }
                if (!TryGetStatusActionDelay(step, action, out float delay)
                    || !entityViews.CanPlayStatusCustomAction(action)) continue;
                var scheduled = new ScheduledStatusAction { Action = action, Remaining = Mathf.Max(0f, delay) };
                int index = pendingStatusActions.FindIndex(value => value.Remaining > scheduled.Remaining);
                if (index < 0) pendingStatusActions.Add(scheduled); else pendingStatusActions.Insert(index, scheduled);
                nativeStatusActions.Add(action);
            }
        }

        private static bool TryGetStatusActionDelay(FightBattleStepEvent step,
            FightBattleStatusCustomActionEvent action, out float delay)
        {
            delay = 0f;
            bool found = false;
            if (step.DamageEvents != null) foreach (var damage in step.DamageEvents)
            {
                int targetId = action.SourceDamageTargetId > 0 ? action.SourceDamageTargetId : action.UnitId;
                if (damage == null || damage.IsSkillCost || action.Action == 2 && damage.IsCure
                    || damage.DamageId != action.SourceDamageId || damage.TargetId != targetId
                    || action.SourceDamageTargetId > 0 && damage.DamageEntityId <= 0) continue;
                if (found) return false; // An incomplete/ambiguous association retains the compatible path.
                found = true;
                delay = damage.Delay;
            }
            return found;
        }

        private void DispatchStatusActions()
        {
            while (pendingStatusActions.Count > 0 && pendingStatusActions[0].Remaining <= 0f)
            {
                var value = pendingStatusActions[0]; pendingStatusActions.RemoveAt(0);
                entityViews.PlayStatusCustomAction(value.Action);
            }
        }

        private void ScheduleDisperses(FightBattleStepEvent step)
        {
            if (step.DamageEvents == null || step.DispersedUnitIds == null) return;
            foreach (var damage in step.DamageEvents)
            {
                if (damage == null || !damage.HasDisperseNotification || !OwnsDamageUI(step, damage)) continue;
                int index = damage.DisperseNotificationIndex;
                if (index < 0 || index >= step.DispersedUnitIds.Count
                    || step.DispersedUnitIds[index] != damage.TargetId || nativeDisperseIndices.Contains(index)
                    || !entityViews.PrepareDisperse(damage)) continue;
                nativeDisperseIndices.Add(index);
            }
        }

        private void ScheduleMovements(FightBattleStepEvent stepEvent, Func<int, Transform> resolveTransform)
        {
            var cursors = new Dictionary<int, float>();
            if (stepEvent.Movements != null) foreach (var movement in stepEvent.Movements)
            {
                if (movement?.Path == null || movement.Path.Count < 2) continue;
                cursors.TryGetValue(movement.UnitId, out float cursor);
                bool ownsSkill = OwnsMovementSkill(movement);
                float start = (float)FightOrdinaryMovementTiming.ActionStartDelay(movement, cursor);
                float skillStart = start - movement.OrdinaryMoveDelay;
                if (ownsSkill)
                {
                    var cast = new ScheduledCast {
                        Cast = new FightBattleSkillCastEvent { ActorId = movement.UnitId, Slot = -1,
                            SkillEntityId = movement.MoveSkillEntityId, Delay = skillStart,
                            Presentation = movement.OrdinarySkillPresentation },
                        UnitTransform = resolveTransform, Remaining = skillStart };
                    var source = resolveTransform(movement.UnitId);
                    if (cast.Remaining <= 0f && source != null) Start(cast, source, null);
                    else
                    {
                        int index = pending.FindIndex(value => value.Remaining > cast.Remaining);
                        if (index < 0) pending.Add(cast); else pending.Insert(index, cast);
                    }
                }
                float duration = 0f;
                for (int index = 1; index < movement.Path.Count; index++)
                {
                    var a = movement.Path[index - 1]; var b = movement.Path[index];
                    float dx = b.X - a.X, dy = b.Y - a.Y;
                    duration += movement.MoveDuration >= 0f ? movement.MoveDuration
                        : movement.MoveSpeed > 0f ? Mathf.Sqrt(dx * dx + dy * dy) / movement.MoveSpeed : .5f;
                }
                if (OwnsMovement(movement)) duration = entityViews.MovementDuration(movement);
                cursors[movement.UnitId] = start + duration;
                if (!OwnsMovement(movement)) continue;
                var scheduled = new ScheduledMove { Movement = movement,
                    UnitTransform = resolveTransform, Remaining = start };
                int insert = pendingMoves.FindIndex(value => value.Remaining > start);
                if (insert < 0) pendingMoves.Add(scheduled); else pendingMoves.Insert(insert, scheduled);
            }
        }

        private void DispatchMoves()
        {
            while (pendingMoves.Count > 0 && pendingMoves[0].Remaining <= 0f)
            {
                var value = pendingMoves[0]; pendingMoves.RemoveAt(0);
                var source = value.UnitTransform(value.Movement.UnitId);
                if (source != null) entityViews.StartMovement(value.Movement, source);
            }
        }

        public void Tick(float deltaTime)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleSkillGraphPlayback));
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            embeddedMecanim.Update();
            airComboViews?.Synchronize();
            statusViews?.DispatchDue();
            float remaining = deltaTime;
            while (true)
            {
                float nextCast = pending.Count == 0 ? float.PositiveInfinity : pending[0].Remaining;
                float nextGrid = gridViews?.NextDelay ?? float.PositiveInfinity;
                float nextMove = pendingMoves.Count == 0 ? float.PositiveInfinity : pendingMoves[0].Remaining;
                float nextStatusAction = pendingStatusActions.Count == 0 ? float.PositiveInfinity : pendingStatusActions[0].Remaining;
                float elapsed = Mathf.Min(nextCast, nextGrid, nextMove, nextStatusAction, unitEnds?.NextDelay ?? float.PositiveInfinity, statusViews?.NextDelay ?? float.PositiveInfinity);
                if (elapsed > remaining) break;
                if (elapsed > 0f) entityViews.Tick(elapsed);
                remaining -= elapsed;
                foreach (ScheduledCast value in pending) value.Remaining -= elapsed;
                foreach (var value in pendingMoves) value.Remaining -= elapsed;
                foreach (var value in pendingStatusActions) value.Remaining -= elapsed;
                gridViews?.AdvanceTimers(elapsed);
                statusViews?.AdvanceTimers(elapsed);
                unitEnds?.AdvanceTimers(elapsed);
                // Native action-end notifications above precede StatusEnd entity removal.
                unitEnds?.DispatchDue();
                gridViews?.DispatchDue();
                statusViews?.DispatchDue();
                while (pending.Count > 0 && pending[0].Remaining <= 0f)
                {
                    ScheduledCast scheduled = pending[0];
                    pending.RemoveAt(0);
                    Transform source = scheduled.Cast.IsPetSupport
                        ? scheduled.PetTransform?.Invoke(scheduled.Cast.ActorId) : scheduled.UnitTransform(scheduled.Cast.ActorId);
                    if (source == null) continue;
                    Transform target = scheduled.UnitTransform(TargetId(scheduled.Cast));
                    Start(scheduled, source, target);
                }
                DispatchMoves();
                DispatchStatusActions();
            }
            foreach (ScheduledCast value in pending) value.Remaining -= remaining;
            foreach (var value in pendingMoves) value.Remaining -= remaining;
            foreach (var value in pendingStatusActions) value.Remaining -= remaining;
            gridViews?.AdvanceTimers(remaining);
            statusViews?.AdvanceTimers(remaining);
            unitEnds?.AdvanceTimers(remaining);
            entityViews.Tick(remaining);
        }

        public void LateTick()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleSkillGraphPlayback));
            scope.LateTick();
            entityViews.UpdateDamageUIVisibility();
        }

        private void Start(ScheduledCast scheduled, Transform source, Transform target)
        {
            if (scheduled.DamageReceipts != null && scheduled.Cast.DamageEventIds != null)
                foreach (int id in scheduled.Cast.DamageEventIds)
                    if (scheduled.DamageReceipts.TryGetValue(id, out var damage))
                    {
                        var transform = scheduled.UnitTransform(damage.TargetId);
                        if (transform) entityViews.EnsureUnit(damage.TargetId, transform);
                    }
            entityViews.Start(scheduled.Cast, source, target, scheduled.DamageReceipts);
        }

        private static int TargetId(FightBattleSkillCastEvent cast) => cast.Presentation != null
            ? cast.Presentation.TargetId : cast.TargetIds != null && cast.TargetIds.Count > 0
                ? cast.TargetIds[0] : 0;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            pending.Clear();
            pendingMoves.Clear();
            pendingStatusActions.Clear();
            unitEnds?.Clear();
            unitEnds = null;
            nativeStatusActions.Clear();
            nativeDisperseIndices.Clear();
            statusViews?.Dispose();
            statusViews = null;
            airComboViews?.Dispose();
            airComboViews = null;
            gridViews?.Dispose();
            gridViews = null;
            try { entityViews.Dispose(); }
            finally { embeddedMecanim.Dispose(); }
        }

        private static FightBattleUnitState FindUnit(FightBattleCheckpoint checkpoint, int id)
        {
            foreach (FightBattleUnitState unit in checkpoint.Units)
                if (unit != null && unit.Id == id) return unit;
            foreach (var item in checkpoint.GridItems)
                if (item != null && item.HasRuntime && item.Id == id) return item.Runtime;
            return null;
        }

    }
}
