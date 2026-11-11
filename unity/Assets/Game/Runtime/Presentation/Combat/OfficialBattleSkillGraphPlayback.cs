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
        private OfficialBattleGridEntityPlayback gridViews;
        private OfficialBattleAirComboPlayback airComboViews;
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
        public OfflineBattleNativePlayableScope Scope => scope;
        public Transform TryGetGridTransform(int id) => gridViews?.TryGetTransform(id);
        public void RefreshGridSnapshot(FightBattleCheckpoint checkpoint) => gridViews?.RefreshSnapshot(checkpoint);

        public void BindGridEntities(Transform root, OfficialBattleGridMapper mapper,
            FightBattleCheckpoint checkpoint, Func<int, Transform> unitTransform, Func<int, bool> hasStatusIcon)
        {
            if (gridViews != null) throw new InvalidOperationException("Native grid entities are already bound.");
            gridViews = new OfficialBattleGridEntityPlayback(root, mapper, scope, entityViews,
                checkpoint, unitTransform, hasStatusIcon);
            gridViews.Restore();
        }

        public void BindSourceDeathView(Zjcs.Official.StoryPlayables.IBattleSourceDeathView view)
            => scope.BindSourceDeathView(view);

        public void BindAirCombo(FightBattleExecutor executor, Transform root, Func<int, Transform> body)
        {
            if (airComboViews != null) throw new InvalidOperationException("Original airborne status views are already bound.");
            airComboViews = new OfficialBattleAirComboPlayback(executor, root, scope, body);
            airComboViews.Synchronize();
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
            if (stepEvent?.SkillCasts == null || checkpoint?.Units == null || unitTransform == null) return;
            airComboViews?.Synchronize();
            gridViews?.Enqueue(stepEvent, checkpoint);
            gridViews?.DispatchDue();
            Func<int, Transform> resolveTransform = id => gridViews?.TryGetTransform(id) ?? unitTransform(id);
            ScheduleMovements(stepEvent, resolveTransform);
            var damageReceipts = new Dictionary<int, FightBattleDamageEvent>();
            if (stepEvent.DamageEvents != null) foreach (var damage in stepEvent.DamageEvents)
                if (damage != null && damage.DamageId > 0) damageReceipts.Add(damage.DamageId, damage);
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
            float remaining = deltaTime;
            while (true)
            {
                float nextCast = pending.Count == 0 ? float.PositiveInfinity : pending[0].Remaining;
                float nextGrid = gridViews?.NextDelay ?? float.PositiveInfinity;
                float nextMove = pendingMoves.Count == 0 ? float.PositiveInfinity : pendingMoves[0].Remaining;
                float elapsed = Mathf.Min(nextCast, nextGrid, nextMove);
                if (elapsed > remaining) break;
                if (elapsed > 0f) entityViews.Tick(elapsed);
                remaining -= elapsed;
                foreach (ScheduledCast value in pending) value.Remaining -= elapsed;
                foreach (var value in pendingMoves) value.Remaining -= elapsed;
                gridViews?.AdvanceTimers(elapsed);
                gridViews?.DispatchDue();
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
            }
            foreach (ScheduledCast value in pending) value.Remaining -= remaining;
            foreach (var value in pendingMoves) value.Remaining -= remaining;
            gridViews?.AdvanceTimers(remaining);
            entityViews.Tick(remaining);
        }

        public void LateTick()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleSkillGraphPlayback));
            scope.LateTick();
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
