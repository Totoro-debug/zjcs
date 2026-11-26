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
using Zjcs.Official.StoryPlayableManager;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Binds confirmed airborne transitions to the complete original status prefabs.</summary>
    public sealed class OfficialBattleAirComboPlayback : IMyActionEntityLookup, IServiceProvider, IECViewInjection, IDisposable
    {
        [Serializable] private sealed class Catalog { public Entry[] graphs; }
        [Serializable] private sealed class Entry { public int id; public string resource; }
        private sealed class State : IFightStatusPlayableState
        {
            private int count;
            private bool forbidden;
            public FightBattleStatusViewState Value;
            public int StackedCount => Value.StackCount;
            public bool CanStackCount => Value.CanStack;
            public bool Forbidden => Value.Forbidden;
            public event Action<IFightStatusPlayableState, int, int> OnStatusStackUpdate;
            public event Action<IFightStatusPlayableState> OnInvoked;
            public event Action<IFightStatusPlayableState, bool> OnForbiddenChanged;
            public State(FightBattleStatusViewState value)
            { Value = value; count = StackedCount; forbidden = Forbidden; }
            public void Refresh(FightBattleStatusViewState value)
            {
                int previousCount = count; bool previousForbidden = forbidden;
                Value = value; count = StackedCount; forbidden = Forbidden;
                if (count != previousCount) OnStatusStackUpdate?.Invoke(this, previousCount, count);
                if (forbidden != previousForbidden) OnForbiddenChanged?.Invoke(this, previousForbidden);
            }
            public void Invoke() => OnInvoked?.Invoke(this);
        }
        private sealed class Context : IFightStatusPlayableContext, ICurrentFightStatusPlayableContext
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
            public ICurrentFightStatusPlayableView CurrentPlayableView { get; set; }
        }
        private sealed class ActionData : ActionComponentData
        {
            public int Owner;
            public override EcEntityIdentifier ApplyId => new EcEntityIdentifier(1, Owner);
            public override object ComponentInfo => null;
        }
        private sealed class EntityData : ECViewEntityData, IBattleGridComponentViewData
        {
            public FightBattleStatusViewState Status;
            public ActionData Action;
            public override int ClassId => Status.ClassId;
            public override EcEntityIdentifier Id => new EcEntityIdentifier(4, Status.InstanceId);
            public override T GetComponent<T>()
            {
                object result = typeof(T) == typeof(IBattleGridComponentViewData) ? this
                    : typeof(T) == typeof(IFightStatusPlayableState) ? StatusState
                    : typeof(T) == typeof(ActionComponentData) ? Action : null;
                return result == null ? default : (T)result;
            }
            public IFightStatusPlayableState StatusState;
        }
        private sealed class Lease
        {
            public MyEntity Entity;
            public MyFightStatus View;
            public State State;
        }
        private FightBattleExecutor executor;
        private readonly OfflineBattleNativePlayableScope scope;
        private readonly Transform root;
        private readonly Func<int, Transform> body;
        private readonly GameObject staging;
        private readonly OfficialBattleEntityPositionContainer container;
        private readonly Dictionary<int, GameObject> prefabs = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, Dictionary<int, GameObject>> sourcePrefabs =
            new Dictionary<int, Dictionary<int, GameObject>>();
        private readonly Dictionary<long, Lease> leases = new Dictionary<long, Lease>();
        private readonly List<MyFightStatusAgent> ownedAgents = new List<MyFightStatusAgent>();
        private bool disposed;

        public OfficialBattleAirComboPlayback(FightBattleExecutor executor, Transform root,
            OfflineBattleNativePlayableScope scope, Func<int, Transform> body,
            IReadOnlyDictionary<int, string> sourceCatalogs = null)
        {
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
            this.body = body ?? throw new ArgumentNullException(nameof(body));
            LoadCatalog("Combat/air-combo", prefabs);
            foreach (var source in sourceCatalogs ?? new Dictionary<int, string>())
            {
                if (source.Key <= 0) throw new ArgumentException("An airborne source catalog requires a skill item.");
                var selected = new Dictionary<int, GameObject>();
                LoadCatalog(source.Value, selected);
                sourcePrefabs.Add(source.Key, selected);
            }
            staging = new GameObject("Inactive original airborne status staging");
            staging.transform.SetParent(root, false); staging.SetActive(false);
            container = staging.AddComponent<OfficialBattleEntityPositionContainer>();
            container.EntitySlot = root;
            executor.StatusSecondsChanged += Changed;
            executor.StatusSecondsInvoked += Invoked;
        }

        private static void LoadCatalog(string path, Dictionary<int, GameObject> target)
        {
            var catalog = Resources.Load<TextAsset>(path);
            if (!catalog) throw new InvalidOperationException("Original airborne prefab catalog is missing: " + path);
            foreach (var entry in JsonUtility.FromJson<Catalog>(catalog.text).graphs)
            {
                if (entry.id != 24269 && entry.id != 24270) continue;
                var prefab = Resources.Load<GameObject>(entry.resource);
                if (!prefab || !prefab.GetComponent<MyEntity>() || !prefab.GetComponent<MyFightStatus>())
                    throw new InvalidOperationException("Original airborne status prefab is unbound: " + entry.id);
                target.Add(entry.id, prefab);
            }
            if (target.Count != 2) throw new InvalidOperationException("Both original airborne status prefabs are required.");
        }

        private static long Key(int owner, int instance) => ((long)owner << 32) | (uint)instance;

        /// <summary>Follow an adopted, completed executor without restarting matching native states.</summary>
        internal void Rebind(FightBattleExecutor next)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleAirComboPlayback));
            if (next == null) throw new ArgumentNullException(nameof(next));
            if (ReferenceEquals(executor, next)) return;
            executor.StatusSecondsChanged -= Changed;
            executor.StatusSecondsInvoked -= Invoked;
            executor = next;
            executor.StatusSecondsChanged += Changed;
            executor.StatusSecondsInvoked += Invoked;
            Synchronize();
        }

        public void Synchronize()
        {
            if (disposed) return;
            var current = new HashSet<long>();
            foreach (var unit in executor.State.Units)
                foreach (var status in unit.Statuses)
                {
                    if (!prefabs.ContainsKey(status.ClassId) || status.Components?.Seconds?.Started != true) continue;
                    long key = Key(unit.Id, status.InstanceId); current.Add(key);
                    var snapshot = FightBattleStatusViewState.Capture(status);
                    if (leases.TryGetValue(key, out var lease)) scope.Dispatch(() => lease.State.Refresh(snapshot));
                    else Start(unit.Id, snapshot, false);
                }
            foreach (long key in new List<long>(leases.Keys)) if (!current.Contains(key)) End(key);
        }

        private void Changed(FightBattleUnitState owner, FightBattleStatusState status, bool started)
            => ApplySecondsStatus(new FightBattleStatusViewEvent { OwnerId = owner.Id,
                Kind = started ? FightBattleStatusViewEventKind.Start : FightBattleStatusViewEventKind.End,
                Status = FightBattleStatusViewState.Capture(status) });

        private void Invoked(FightBattleUnitState owner, FightBattleStatusState status)
            => ApplySecondsStatus(new FightBattleStatusViewEvent { OwnerId = owner.Id,
                Kind = FightBattleStatusViewEventKind.Invoke, Status = FightBattleStatusViewState.Capture(status) });

        internal void ApplySecondsStatus(FightBattleStatusViewEvent value)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleAirComboPlayback));
            if (value?.Status == null) throw new ArgumentNullException(nameof(value));
            if (!prefabs.ContainsKey(value.Status.ClassId)) return;
            long key = Key(value.OwnerId, value.Status.InstanceId);
            switch (value.Kind)
            {
                case FightBattleStatusViewEventKind.Start: Start(value.OwnerId, value.Status, true); break;
                case FightBattleStatusViewEventKind.End: End(key); break;
                case FightBattleStatusViewEventKind.Invoke:
                    if (leases.TryGetValue(key, out var lease)) scope.Dispatch(lease.State.Invoke);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(value.Kind));
            }
        }

        private void Start(int ownerId, FightBattleStatusViewState status, bool fresh)
        {
            long key = Key(ownerId, status.InstanceId);
            if (leases.ContainsKey(key)) return;
            Transform target = body(ownerId);
            if (!target) return; // An attached original model can become available on a later frame.
            var agent = target.GetComponent<MyFightStatusAgent>();
            if (!agent) { agent = target.gameObject.AddComponent<MyFightStatusAgent>(); ownedAgents.Add(agent); }
            var selected = sourcePrefabs.TryGetValue(status.ApplyingSkillItemId, out var bySource) ? bySource : prefabs;
            var go = UnityEngine.Object.Instantiate(selected[status.ClassId], staging.transform, false);
            var entity = go.GetComponent<MyEntity>();
            var view = go.GetComponent<MyFightStatus>();
            var state = new State(status);
            OfficialBattleCurrentViewBinding.Prepare(go, this);
            var context = new Context { Status = state, Body = target.gameObject, StatusAgent = agent,
                CurrentPlayableView = CurrentStatusViewLookup.Get(view) };
            view.Bind(context, scope);
            entity.Bind(container, new EntityData { Status = status, StatusState = state,
                Action = new ActionData { Owner = ownerId } }, fresh);
            leases.Add(key, new Lease { Entity = entity, View = view, State = state });
            go.transform.SetParent(root, false);
            scope.Dispatch(() => {
                if (context.CurrentPlayableView != null) { view.OnStart(fresh); entity.MyStart(); }
                else { entity.MyStart(); view.OnStart(fresh); }
            });
        }

        MyEntity IMyActionEntityLookup.GetEntity(EcEntityIdentifier id)
        {
            var target = body(id.EntityId);
            return target ? target.GetComponent<MyEntity>() : null;
        }

        public object GetService(Type type) => type == typeof(IMyActionEntityLookup) ? this
            : type == typeof(IMyPlayableManager) ? scope : null;

        public void Inject(object value)
        {
            if (value is ECFightStatusPlayableView status) status.MyPlayableManager = scope;
            else if (value is ECStatusInvokedEventSourceView) { }
            else throw new InvalidOperationException("Unbound original airborne EC view: " + value?.GetType().FullName);
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
