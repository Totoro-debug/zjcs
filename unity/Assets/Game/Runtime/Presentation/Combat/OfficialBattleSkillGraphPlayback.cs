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
            public GameObject Prefab;
            public Func<int, Transform> UnitTransform;
            public float Remaining;
        }

        private readonly OfficialBattleSkillGraphCatalog catalog;
        private readonly OfflineBattleNativePlayableScope scope;
        private readonly OfficialBattleSkillEntityPlayback entityViews;
        private readonly List<ScheduledCast> pending = new List<ScheduledCast>();
        private bool disposed;

        public OfficialBattleSkillGraphPlayback(OfficialBattleSkillGraphCatalog catalog,
            OfflineBattleNativePlayableScope scope, OfficialBattleSkillEntityPlayback entityViews)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
            this.entityViews = entityViews ?? throw new ArgumentNullException(nameof(entityViews));
            this.scope.BindNativePrefabAdapter(Zjcs.Offline.ProfessionUI.PartnerNativeSkillSkeleton.BindOriginalPrefab);
        }

        public int CatalogCount => catalog.Count;
        public OfflineBattleNativePlayableScope Scope => scope;

        public void BindSourceDeathView(Zjcs.Official.StoryPlayables.IBattleSourceDeathView view)
            => scope.BindSourceDeathView(view);

        public void BindSceneHosts(IBattleCameraOffsetHost cameraHost,
            IBattleCharacterPartVisibilityHost visibilityHost = null, IBattleMasterEntityResolver masterResolver = null)
        {
            scope.BindHosts(cameraHost, visibilityHost, cameraHost as IBattleCameraPerformanceHost, masterResolver);
        }

        public void Play(FightBattleStepEvent stepEvent, FightBattleCheckpoint checkpoint,
            Func<int, Transform> unitTransform)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleSkillGraphPlayback));
            if (stepEvent?.SkillCasts == null || checkpoint?.Units == null || unitTransform == null) return;
            foreach (FightBattleSkillCastEvent cast in stepEvent.SkillCasts)
            {
                if (cast == null || cast.ActorId <= 0 || cast.SkillEntityId <= 0) continue;
                FightBattleUnitState actor = FindUnit(checkpoint, cast.ActorId);
                Transform source = unitTransform(cast.ActorId);
                if (actor == null || source == null) continue;
                int targetId = TargetId(cast);
                string direction = ResolveDirection(cast.Presentation?.FacingIndex ?? actor.Direction);
                GameObject prefab = catalog.Find(cast.SkillEntityId, direction);
                if (prefab == null && !catalog.ContainsSkill(cast.SkillEntityId)) continue;
                var scheduled = new ScheduledCast
                {
                    Cast = cast,
                    Prefab = prefab,
                    UnitTransform = unitTransform,
                    Remaining = Mathf.Max(0f, cast.Delay)
                };
                if (scheduled.Remaining <= 0f) Start(scheduled, prefab, source, unitTransform(targetId));
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
            float remaining = deltaTime;
            while (pending.Count > 0 && pending[0].Remaining <= remaining)
            {
                float elapsed = pending[0].Remaining;
                if (elapsed > 0f) entityViews.Tick(elapsed);
                remaining -= elapsed;
                foreach (ScheduledCast value in pending) value.Remaining -= elapsed;
                ScheduledCast scheduled = pending[0];
                pending.RemoveAt(0);
                Transform source = scheduled.UnitTransform(scheduled.Cast.ActorId);
                if (source == null) continue;
                Transform target = scheduled.UnitTransform(TargetId(scheduled.Cast));
                Start(scheduled, scheduled.Prefab, source, target);
            }
            foreach (ScheduledCast value in pending) value.Remaining -= remaining;
            entityViews.Tick(remaining);
        }

        public void LateTick()
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleSkillGraphPlayback));
            scope.LateTick();
        }

        private void Start(ScheduledCast scheduled, GameObject prefab, Transform source, Transform target)
        {
            entityViews.Start(scheduled.Cast, prefab, source, target);
        }

        private static int TargetId(FightBattleSkillCastEvent cast) => cast.Presentation != null
            ? cast.Presentation.TargetId : cast.TargetIds != null && cast.TargetIds.Count > 0
                ? cast.TargetIds[0] : 0;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            pending.Clear();
            entityViews.Dispose();
        }

        private static FightBattleUnitState FindUnit(FightBattleCheckpoint checkpoint, int id)
        {
            foreach (FightBattleUnitState unit in checkpoint.Units)
                if (unit != null && unit.Id == id) return unit;
            return null;
        }

        private static string ResolveDirection(int direction)
        {
            switch (direction)
            {
                // Combat directions index the original Pos2d.FourDirs list.
                case 0: return "DownPlayablePrefab";
                case 1: return "LeftPlayablePrefab";
                case 2: return "UpPlayablePrefab";
                case 3: return "RightPlayablePrefab";
                default: return "myPlayablePrefab";
            }
        }
    }
}
