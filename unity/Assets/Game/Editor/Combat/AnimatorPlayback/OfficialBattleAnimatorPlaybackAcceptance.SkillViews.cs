using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunSkillViews()
        {
            RunCasts(); SessionState.SetBool(Key + ".skillViews", true);
        }

        private sealed class SkillViewInfo : FightSkillComponentInfo
        {
            public bool Flip = true, EntityTarget = true;
            public override bool FlipSource => Flip;
            public override bool IsEntityTargetType() => EntityTarget;
        }

        private sealed class SkillViewState : FightSkillComponent
        {
            public readonly SkillViewInfo Configuration = new SkillViewInfo();
            public bool WasInterrupted;
            public int Slot;
            public Pos2d Position = new Pos2d(2, 3);
            public EcEntityIdentifier SourceId = new EcEntityIdentifier(1, 1), VictimId = new EcEntityIdentifier(1, 2);
            public SkillViewEntity Data;
            public override EcEntityIdentifier SrcEntityId => SourceId;
            public override EcEntityIdentifier TargetId => VictimId;
            public override Pos2d TargetPos => Position;
            public override int SkillSlot => Slot;
            public override bool Interrupted => WasInterrupted;
            public override ECViewEntityData Entity => Data;
            public override FightSkillComponentInfo Info => Configuration;
        }

        private sealed class SkillViewGrid : ECGridTransformData
        {
            public Direction Facing;
            public bool Mirrored;
            public readonly HashSet<IGridTransformPosUpdatedEventReceiver> Receivers = new HashSet<IGridTransformPosUpdatedEventReceiver>();
            public override Pos2d Pos => new Pos2d(0, 0);
            public override Direction Direction => Facing;
            public override bool FlipX => Mirrored;
            public override ECGridTransformInfo Info => null;
            public override Vector3 GridToPosition(Pos2d grid) => Vector3.zero;
            public override Pos2d PositionToGrid(Vector3 pos) => new Pos2d(0, 0);
            public override void AddPosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver receiver) => Receivers.Add(receiver);
            public override void RemovePosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver receiver) => Receivers.Remove(receiver);
        }

        private sealed class SkillViewEntity : ECViewEntityData
        {
            public readonly SkillViewGrid Grid = new SkillViewGrid();
            public SkillViewState State;
            public override int ClassId => 11170;
            public override EcEntityIdentifier Id => new EcEntityIdentifier(1, 3);
            public override T GetComponent<T>()
            {
                object result = typeof(T) == typeof(ECGridTransformData) ? Grid
                    : typeof(T) == typeof(FightSkillComponent) ? State : null;
                return result == null ? default : (T)result;
            }
        }

        private sealed class SkillViewServices : IFightSkillViewServices
        {
            public readonly Dictionary<EcEntityIdentifier, MyEntity> Entities = new Dictionary<EcEntityIdentifier, MyEntity>();
            public readonly List<Pos2d> GridQueries = new List<Pos2d>();
            public MyEntity GetEntity(EcEntityIdentifier id) => Entities.TryGetValue(id, out var entity) ? entity : null;
            public Vector3 GridToPos(Pos2d position)
            {
                GridQueries.Add(position); return new Vector3(position.X * 1.7f + .3f, position.Y * .8f - .2f, .25f);
            }
        }

        // Record arguments while executing the actual full native manager/graphs.
        private sealed class SkillViewManager : IMyPlayableManager
        {
            private readonly IMyPlayableManager backend;
            public readonly List<GameObject> Prefabs = new List<GameObject>();
            public readonly List<MyPlayable> Instances = new List<MyPlayable>();
            public Transform PlayableSlot => backend.PlayableSlot;
            public SkillViewManager(IMyPlayableManager backend) { this.backend = backend; }
            public MyPlayable Play(GameObject prefab, GameObject source, GameObject target, float speed = 1,
                GameObject parentGo = null, Dictionary<string, object> param = null, IMyPlayableNeedStop needStopObj = null)
            {
                Prefabs.Add(prefab); var result = backend.Play(prefab, source, target, speed, parentGo, param, needStopObj);
                Instances.Add(result); return result;
            }
            public void RemoveAllPlayable(bool release) => backend.RemoveAllPlayable(release);
            public void SetObjectPool(GamePrefabTypeSetPools<MyPlayable, GameObjectPoolBase<MyPlayable>> pools) => backend.SetObjectPool(pools);
            public void ForceReleaseObjectPool() => backend.ForceReleaseObjectPool();
        }

        private static IEnumerator CheckSkillViews(GameObject actor, GameObject victim, GameObject prefab,
            OfflineBattleNativePlayableScope scope, Func<int, float, IEnumerator> pump, Action<string> capture)
        {
            var root = new GameObject("Original skill view lifecycle fixture"); root.SetActive(false);
            var sourceEntity = actor.GetComponent<MyEntity>(); bool ownEntity = !sourceEntity;
            if (ownEntity) sourceEntity = actor.AddComponent<MyEntity>();
            var sourceGrid = actor.GetComponent<MyGridTransform>(); bool ownGrid = !sourceGrid;
            if (ownGrid) sourceGrid = actor.AddComponent<MyGridTransform>();
            var priorGrid = sourceGrid.Component; var priorScale = actor.transform.localScale;
            var inputGrid = new SkillViewGrid { Mirrored = true }; sourceGrid.Component = inputGrid;
            var victimEntity = victim.AddComponent<MyEntity>();
            var agent = actor.AddComponent<MyFightSkillAgent>();
            var services = new SkillViewServices();
            services.Entities.Add(new EcEntityIdentifier(1, 1), sourceEntity);
            services.Entities.Add(new EcEntityIdentifier(1, 2), victimEntity);
            var manager = new SkillViewManager(scope);
            var notifications = new List<string>();
            agent.OnSkillApplied += value => notifications.Add("apply");
            agent.OnSkillEnded += value => notifications.Add("end");
            var objects = new List<MyEntity>();
            var clones = new List<GameObject>();
            MyFightSkill Make(SkillViewState state, Direction direction = Direction.Up)
            {
                var go = new GameObject("Original skill view 11170"); go.SetActive(false);
                go.transform.SetParent(root.transform, false);
                var entity = go.AddComponent<MyEntity>(); var view = go.AddComponent<MyFightSkill>();
                view.ViewServices = services; view.myPlayableManager = manager; view.myPlayablePrefab = prefab;
                view.PreferredPosToPlayable = false;
                state.Data = new SkillViewEntity { State = state }; state.Data.Grid.Facing = direction;
                entity.Bind(null, state.Data, true); objects.Add(entity);
                go.SetActive(true); return view;
            }
            void Start(MyFightSkill view) => scope.Dispatch(view.myEntity.MyStart);
            void Remove(MyFightSkill view) => scope.Dispatch(() => { view.myEntity.End(null); view.myEntity.Remove(null); });
            try
            {
                root.SetActive(true);
                var state = new SkillViewState(); var view = Make(state);
                Start(view);
                Require(manager.Instances.Count == 1 && notifications.SequenceEqual(new[] { "apply" }), "native entity bind/start routes skill applied event once");
                var graph = manager.Instances.Last();
                Require(graph.mySource == actor && graph.myTarget == victim && ReferenceEquals(graph.TryGetParam("Skill"), view.gameObject),
                    "original skill parameter and caster/victim enter full graph");
                Require(actor.transform.localScale.x < 0 && services.GridQueries.Count == 0, "original FlipSource resets mirrored source without querying a position target");
                yield return pump(65, .01f);
                Require(graph.GetComponentsInChildren<ParticleSystem>(true).Sum(p => p.particleCount) > 0,
                    "native entity lifecycle runs complete original particle graph");
                capture("active");
                scope.Dispatch(() => state.OnInterruped.Invoke(state));
                Require(graph.myStatus != MyPlayable.PlayableStatus.Stop, "original interruption uses deferred Stop");
                yield return pump(5, .01f);
                Require(graph.myStatus == MyPlayable.PlayableStatus.Stop, "original interruption drains full native sequence");
                capture("interrupted");
                Remove(view); Require(notifications.SequenceEqual(new[] { "apply", "end" }), "native remove routes skill ended after applied");
                Require(state.Data.Grid.Receivers.Count == 0, "native entity End removes original grid listener");

                // The original removal does not stop the graph, and detaches its
                // interruption listener before publishing the ended event.
                var detached = new SkillViewState(); var detachedView = Make(detached); Start(detachedView);
                var detachedGraph = manager.Instances.Last(); Remove(detachedView);
                scope.Dispatch(() => detached.OnInterruped.Invoke(detached)); yield return pump(2, .01f);
                Require(detachedGraph.myStatus != MyPlayable.PlayableStatus.Stop, "removed skill no longer listens to interruption and does not invent a graph stop");
                scope.Stop(detachedGraph, true); yield return pump(3, .01f);

                int before = manager.Instances.Count; notifications.Clear();
                var interrupted = Make(new SkillViewState { WasInterrupted = true }); Start(interrupted); Remove(interrupted);
                Require(manager.Instances.Count == before && notifications.SequenceEqual(new[] { "apply", "end" }), "pre-interrupted skill keeps events without playing");
                notifications.Clear(); var missing = Make(new SkillViewState { SourceId = new EcEntityIdentifier(1, 99) });
                Start(missing); Remove(missing);
                Require(manager.Instances.Count == before && notifications.Count == 0, "missing source has neither graph nor agent events");
                var child = Make(new SkillViewState { Slot = -1 }); Start(child); Remove(child);
                Require(manager.Instances.Count == before + 1 && notifications.Count == 0, "negative-slot child plays without main-skill events");
                scope.Stop(manager.Instances.Last(), true); yield return pump(3, .01f);

                // Root-local coordinates, cached target reuse and destruction.
                var positionState = new SkillViewState(); positionState.Configuration.Flip = false;
                var positionView = Make(positionState); positionView.PreferredPosToPlayable = true;
                actor.transform.localScale = priorScale; var localScale = actor.transform.localScale;
                Start(positionView); var positionGraph = manager.Instances.Last(); var positionTarget = positionGraph.myTarget;
                Require(positionTarget != victim && positionTarget.name == "skill_11170_target" && positionTarget.transform.parent == positionView.transform.parent,
                    "position target follows original naming and skill parent");
                Require(actor.transform.localScale == localScale && services.GridQueries.Last() == positionState.Position, "FlipSource false and exact root-grid input preserved");
                positionState.Position = new Pos2d(4, 5);
                Require(positionView.GetPlayableTargetGo() == positionTarget && positionTarget.transform.localPosition == new Vector3(7.1f, 3.8f, .25f),
                    "cached target updates root-local position without replacement");
                positionView.PreferredPosToPlayable = false;
                Require(positionView.GetPlayableTargetGo() == victim, "entity mode can use actual victim after cached position use");
                positionState.Configuration.EntityTarget = false;
                Require(positionView.GetPlayableTargetGo() == positionTarget, "nonentity target type uses cached position even with entity available");
                positionState.Configuration.EntityTarget = true; positionState.VictimId = new EcEntityIdentifier(1, 99);
                Require(positionView.GetPlayableTargetGo() == positionTarget, "missing entity target falls back to configured position");
                scope.Stop(positionGraph, true); yield return pump(3, .01f); Remove(positionView);
                Object.Destroy(positionView.gameObject); yield return null;
                Require(!positionTarget, "original OnDestroy releases its cached position target");

                var directions = new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right };
                foreach (var direction in directions)
                {
                    var directional = Object.Instantiate(prefab, root.transform); directional.SetActive(false);
                    directional.name = "Full original graph fixture " + direction; clones.Add(directional);
                }
                var directionState = new SkillViewState(); var directionView = Make(directionState);
                directionView.UpPlayablePrefab = clones[0]; directionView.DownPlayablePrefab = clones[1];
                directionView.LeftPlayablePrefab = clones[2]; directionView.RightPlayablePrefab = clones[3];
                directionView.UpdateDirectionPlyables();
                HashSet<GameObject> all = new HashSet<GameObject>(); directionView.TryGetPlayables(ref all);
                Require(all.SetEquals(clones.Concat(new[] { prefab })), "official preload collects default and all four directional graphs");
                HashSet<GameObject> absent = null; directionView.TryGetPlayables(ref absent);
                for (int i = 0; i < directions.Length; i++)
                {
                    directionState.Data.Grid.Facing = directions[i]; Start(directionView);
                    Require(manager.Prefabs.Last() == clones[i], "original four-way graph selection " + directions[i]);
                    scope.Stop(manager.Instances.Last(), true); yield return pump(3, .01f); Remove(directionView);
                }
                directionView.UpPlayablePrefab = null; directionView.UpdateDirectionPlyables();
                directionState.Data.Grid.Facing = Direction.Up; Start(directionView);
                Require(manager.Prefabs.Last() == prefab, "null directional graph falls back to original default");
                scope.Stop(manager.Instances.Last(), true); yield return pump(3, .01f); Remove(directionView);
                directionView.myPlayablePrefab = null; before = manager.Instances.Count; Start(directionView); Remove(directionView);
                Require(manager.Instances.Count == before, "absent direction/default emits lifecycle events without creating a graph");
                // Purging playback does not transfer ownership of an inactive
                // pooled handle. A late cleanup callback may still stop it.
                manager.RemoveAllPlayable(false); scope.Stop(manager.Instances.Last(), true);
                Debug.Log("[Original skill view] full native entity Bind/MyStart/End/Remove, source flip, caster/victim/Skill parameter, pre-interruption, deferred interruption, unsubscribe, negative slot, cached grid target and destruction, all four directions and default/null fallback passed.");
            }
            finally
            {
                foreach (var entity in objects) if (entity) Object.DestroyImmediate(entity.gameObject);
                foreach (var clone in clones) if (clone) Object.DestroyImmediate(clone);
                sourceGrid.Component = priorGrid; actor.transform.localScale = priorScale;
                Object.DestroyImmediate(agent); Object.DestroyImmediate(victimEntity);
                if (ownGrid) Object.DestroyImmediate(sourceGrid);
                if (ownEntity) Object.DestroyImmediate(sourceEntity);
                Object.DestroyImmediate(root);
            }
        }
    }
}
