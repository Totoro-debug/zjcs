using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.StoryPlayableManager;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Dispatches each settled skill cast into its original FightSkill prefab.</summary>
    public sealed class OfficialBattleSkillGraphPlayback : IDisposable
    {
        private sealed class ScheduledCast
        {
            public FightBattleSkillCastEvent Cast;
            public Func<int, Transform> UnitTransform;
            public Func<int, Transform> PetTransform;
            public float Remaining;
        }

        private readonly OfficialBattleSkillGraphCatalog catalog;
        private readonly OfflineBattleNativePlayableScope scope;
        private readonly OfficialBattleSkillEntityPlayback entityViews;
        private readonly List<ScheduledCast> pending = new List<ScheduledCast>();
        private OfficialBattleGridEntityPlayback gridViews;
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
                bindPlayerClones?.Invoke(instance);
            });
        }

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
            gridViews?.Enqueue(stepEvent, checkpoint);
            gridViews?.DispatchDue();
            Func<int, Transform> resolveTransform = id => gridViews?.TryGetTransform(id) ?? unitTransform(id);
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
        }

        public void Tick(float deltaTime)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleSkillGraphPlayback));
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            embeddedMecanim.Update();
            float remaining = deltaTime;
            while (true)
            {
                float nextCast = pending.Count == 0 ? float.PositiveInfinity : pending[0].Remaining;
                float nextGrid = gridViews?.NextDelay ?? float.PositiveInfinity;
                float elapsed = Mathf.Min(nextCast, nextGrid);
                if (elapsed > remaining) break;
                if (elapsed > 0f) entityViews.Tick(elapsed);
                remaining -= elapsed;
                foreach (ScheduledCast value in pending) value.Remaining -= elapsed;
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
            }
            foreach (ScheduledCast value in pending) value.Remaining -= remaining;
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
            entityViews.Start(scheduled.Cast, source, target);
        }

        private static int TargetId(FightBattleSkillCastEvent cast) => cast.Presentation != null
            ? cast.Presentation.TargetId : cast.TargetIds != null && cast.TargetIds.Count > 0
                ? cast.TargetIds[0] : 0;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            pending.Clear();
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
