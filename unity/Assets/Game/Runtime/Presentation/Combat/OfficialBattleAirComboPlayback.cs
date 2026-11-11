using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.BattleGridItemEntities;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityActions;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Binds confirmed airborne transitions to the complete original status prefabs.</summary>
    public sealed class OfficialBattleAirComboPlayback : IDisposable
    {
        [Serializable] private sealed class Catalog { public Entry[] graphs; }
        [Serializable] private sealed class Entry { public int id; public string resource; }
        private sealed class State : IFightStatusPlayableState
        {
            private int count;
            private bool forbidden;
            public FightBattleStatusState Value;
            public int StackedCount => Value.StackCount;
            public bool CanStackCount => Value.IsOpenStack;
            public bool Forbidden => Value.ForbiddenType != 0;
            public event Action<IFightStatusPlayableState, int, int> OnStatusStackUpdate;
            public event Action<IFightStatusPlayableState> OnInvoked;
            public event Action<IFightStatusPlayableState, bool> OnForbiddenChanged;
            public State(FightBattleStatusState value)
            { Value = value; count = StackedCount; forbidden = Forbidden; }
            public void Refresh(FightBattleStatusState value)
            {
                int previousCount = count; bool previousForbidden = forbidden;
                Value = value; count = StackedCount; forbidden = Forbidden;
                if (count != previousCount) OnStatusStackUpdate?.Invoke(this, previousCount, count);
                if (forbidden != previousForbidden) OnForbiddenChanged?.Invoke(this, previousForbidden);
            }
            public void Invoke() => OnInvoked?.Invoke(this);
        }
        private sealed class Context : IFightStatusPlayableContext
        {
            public State Status;
            public GameObject Body;
            public MyFightStatusAgent StatusAgent;
            public IFightStatusPlayableState State => Status;
            public GameObject Owner => Body;
            public GameObject Creator => null; // Neither original prefab consumes this view input.
            public GameObject Master => null;
            public MyFightStatusAgent Agent => StatusAgent;
            public int? ShareId => null; // Neither original state has a share-stack component.
            public bool HasIcon => false; // Original ShowSpriteAbovePlayer is false for both states.
        }
        private sealed class ActionData : ActionComponentData
        {
            public int Owner;
            public override EcEntityIdentifier ApplyId => new EcEntityIdentifier(1, Owner);
            public override object ComponentInfo => null;
        }
        private sealed class EntityData : ECViewEntityData, IBattleGridComponentViewData
        {
            public FightBattleStatusState Status;
            public ActionData Action;
            public override int ClassId => Status.ClassId;
            public override EcEntityIdentifier Id => new EcEntityIdentifier(4, Status.InstanceId);
            public override T GetComponent<T>()
            {
                object result = typeof(T) == typeof(IBattleGridComponentViewData) ? this
                    : typeof(T) == typeof(ActionComponentData) ? Action : null;
                return result == null ? default : (T)result;
            }
        }
        private sealed class Lease
        {
            public MyEntity Entity;
            public MyFightStatus View;
            public State State;
        }
        private readonly FightBattleExecutor executor;
        private readonly OfflineBattleNativePlayableScope scope;
        private readonly Transform root;
        private readonly Func<int, Transform> body;
        private readonly GameObject staging;
        private readonly OfficialBattleEntityPositionContainer container;
        private readonly Dictionary<int, GameObject> prefabs = new Dictionary<int, GameObject>();
        private readonly Dictionary<long, Lease> leases = new Dictionary<long, Lease>();
        private readonly List<MyFightStatusAgent> ownedAgents = new List<MyFightStatusAgent>();
        private bool disposed;

        public OfficialBattleAirComboPlayback(FightBattleExecutor executor, Transform root,
            OfflineBattleNativePlayableScope scope, Func<int, Transform> body)
        {
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
            this.body = body ?? throw new ArgumentNullException(nameof(body));
            var catalog = Resources.Load<TextAsset>("Combat/air-combo");
            if (!catalog) throw new InvalidOperationException("Original airborne prefab catalog is missing.");
            foreach (var entry in JsonUtility.FromJson<Catalog>(catalog.text).graphs)
            {
                if (entry.id != 24269 && entry.id != 24270) continue;
                var prefab = Resources.Load<GameObject>(entry.resource);
                if (!prefab || !prefab.GetComponent<MyEntity>() || !prefab.GetComponent<MyFightStatus>())
                    throw new InvalidOperationException("Original airborne status prefab is unbound: " + entry.id);
                prefabs.Add(entry.id, prefab);
            }
            if (prefabs.Count != 2) throw new InvalidOperationException("Both original airborne status prefabs are required.");
            staging = new GameObject("Inactive original airborne status staging");
            staging.transform.SetParent(root, false); staging.SetActive(false);
            container = staging.AddComponent<OfficialBattleEntityPositionContainer>();
            container.EntitySlot = root;
            executor.StatusSecondsChanged += Changed;
            executor.StatusSecondsInvoked += Invoked;
        }

        private static long Key(int owner, int instance) => ((long)owner << 32) | (uint)instance;

        public void Synchronize()
        {
            if (disposed) return;
            var current = new HashSet<long>();
            foreach (var unit in executor.State.Units)
                foreach (var status in unit.Statuses)
                {
                    if (!prefabs.ContainsKey(status.ClassId) || status.Components?.Seconds?.Started != true) continue;
                    long key = Key(unit.Id, status.InstanceId); current.Add(key);
                    if (leases.TryGetValue(key, out var lease)) scope.Dispatch(() => lease.State.Refresh(status));
                    else Start(unit, status, false);
                }
            foreach (long key in new List<long>(leases.Keys)) if (!current.Contains(key)) End(key);
        }

        private void Changed(FightBattleUnitState owner, FightBattleStatusState status, bool started)
        {
            if (!prefabs.ContainsKey(status.ClassId)) return;
            if (started) Start(owner, status, true); else End(Key(owner.Id, status.InstanceId));
        }

        private void Invoked(FightBattleUnitState owner, FightBattleStatusState status)
        {
            if (leases.TryGetValue(Key(owner.Id, status.InstanceId), out var lease))
                scope.Dispatch(lease.State.Invoke);
        }

        private void Start(FightBattleUnitState owner, FightBattleStatusState status, bool fresh)
        {
            long key = Key(owner.Id, status.InstanceId);
            if (leases.ContainsKey(key)) return;
            Transform target = body(owner.Id);
            if (!target) return; // An attached original model can become available on a later frame.
            var agent = target.GetComponent<MyFightStatusAgent>();
            if (!agent) { agent = target.gameObject.AddComponent<MyFightStatusAgent>(); ownedAgents.Add(agent); }
            var go = UnityEngine.Object.Instantiate(prefabs[status.ClassId], staging.transform, false);
            var entity = go.GetComponent<MyEntity>();
            var view = go.GetComponent<MyFightStatus>();
            var state = new State(status);
            view.Bind(new Context { Status = state, Body = target.gameObject, StatusAgent = agent }, scope);
            entity.Bind(container, new EntityData { Status = status, Action = new ActionData { Owner = owner.Id } }, fresh);
            leases.Add(key, new Lease { Entity = entity, View = view, State = state });
            go.transform.SetParent(root, false);
            scope.Dispatch(() => { entity.MyStart(); view.OnStart(fresh); });
        }

        private void End(long key)
        {
            if (!leases.TryGetValue(key, out var lease)) return;
            leases.Remove(key);
            scope.Dispatch(() => { lease.View.OnEnd(); lease.Entity.End(null); lease.Entity.Remove(null); });
            UnityEngine.Object.Destroy(lease.Entity.gameObject);
        }

        public void Dispose()
        {
            if (disposed) return;
            executor.StatusSecondsChanged -= Changed;
            executor.StatusSecondsInvoked -= Invoked;
            foreach (long key in new List<long>(leases.Keys)) End(key);
            foreach (var agent in ownedAgents) if (agent) UnityEngine.Object.Destroy(agent);
            ownedAgents.Clear();
            UnityEngine.Object.Destroy(staging);
            disposed = true;
        }
    }
}
