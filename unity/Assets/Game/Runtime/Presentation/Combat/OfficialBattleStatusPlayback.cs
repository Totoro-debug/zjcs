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
    /// <summary>Plays complete original character-status entities at their captured transition times.</summary>
    public sealed class OfficialBattleStatusPlayback : IDisposable
    {
        [Serializable] private sealed class Catalog { public Entry[] graphs; }
        [Serializable] private sealed class Entry { public int id; public string resource; }
        private sealed class Scheduled { public FightBattleStatusViewEvent Event; public float Remaining; }
        private sealed class State : BattleStatusRoundStartViewData, IFightStatusPlayableState
        {
            public FightBattleStatusViewState Value;
            public int StackedCount => Value.StackCount;
            public bool CanStackCount => Value.CanStack;
            public bool Forbidden => Value.Forbidden;
            public override int GetRemainCount() => Value.RoundStartRemainingCount;
            public event Action<IFightStatusPlayableState, int, int> OnStatusStackUpdate;
            public event Action<IFightStatusPlayableState> OnInvoked;
            public event Action<IFightStatusPlayableState, bool> OnForbiddenChanged;
            public void Update(FightBattleStatusViewState value)
            {
                var previous = Value; Value = value;
                if (previous.StackCount != value.StackCount)
                    OnStatusStackUpdate?.Invoke(this, previous.StackCount, value.StackCount);
                if (previous.Forbidden != value.Forbidden) OnForbiddenChanged?.Invoke(this, previous.Forbidden);
                if (previous.RoundStartRemainingCount != value.RoundStartRemainingCount) NotifyRemainCountUpdate();
            }
            public void Invoke() => OnInvoked?.Invoke(this);
        }
        private sealed class Context : IFightStatusPlayableContext
        {
            public State Status;
            public GameObject Owner { get; set; }
            public GameObject Creator { get; set; }
            public GameObject Master { get; set; }
            public MyFightStatusAgent Agent { get; set; }
            public bool HasIcon { get; set; }
            public IFightStatusPlayableState State => Status;
            public int? ShareId => Status.Value.ShareId > 0 ? (int?)Status.Value.ShareId : null;
        }
        private sealed class ActionData : ActionComponentData
        {
            public int Owner;
            public override EcEntityIdentifier ApplyId => new EcEntityIdentifier(1, Owner);
            public override object ComponentInfo => null;
        }
        private sealed class EntityData : ECViewEntityData, IBattleGridComponentViewData
        {
            public State Status;
            public ActionData Action;
            public override int ClassId => Status.Value.ClassId;
            public override EcEntityIdentifier Id => new EcEntityIdentifier(4, Status.Value.InstanceId);
            public override T GetComponent<T>()
            {
                object result = typeof(T) == typeof(IBattleGridComponentViewData) ? this
                    : typeof(T) == typeof(ActionComponentData) ? Action
                    : typeof(T) == typeof(BattleStatusRoundStartViewData) && Status.Value.HasRoundStart ? Status : null;
                return result == null ? default : (T)result;
            }
        }
        private sealed class Lease
        {
            public MyEntity Entity;
            public MyFightStatus View;
            public State State;
            public Context Context;
        }
        private sealed class Deferred
        { public int Owner; public FightBattleStatusViewState Status; public bool Fresh; }

        private readonly FightBattleExecutor executor;
        private FightBattleCheckpoint checkpoint;
        private readonly OfflineBattleNativePlayableScope scope;
        private readonly Func<int, Transform> body;
        private readonly Func<int, bool> hasIcon;
        private readonly Transform root;
        private readonly GameObject staging;
        private readonly OfficialBattleEntityPositionContainer container;
        private readonly Dictionary<int, GameObject> prefabs = new Dictionary<int, GameObject>();
        private readonly Dictionary<long, Lease> leases = new Dictionary<long, Lease>();
        private readonly Dictionary<long, Deferred> deferred = new Dictionary<long, Deferred>();
        private readonly List<MyFightStatusAgent> ownedAgents = new List<MyFightStatusAgent>();
        private readonly List<Scheduled> pending = new List<Scheduled>();
        private bool disposed;

        public OfficialBattleStatusPlayback(FightBattleExecutor executor, Transform root,
            OfflineBattleNativePlayableScope scope, Func<int, Transform> body, Func<int, bool> hasIcon,
            params string[] catalogs)
        {
            this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
            this.body = body ?? throw new ArgumentNullException(nameof(body));
            this.hasIcon = hasIcon ?? throw new ArgumentNullException(nameof(hasIcon));
            foreach (string path in catalogs)
            {
                var text = Resources.Load<TextAsset>(path);
                if (!text) throw new InvalidOperationException("Original status prefab catalog is missing: " + path);
                foreach (var entry in JsonUtility.FromJson<Catalog>(text.text).graphs)
                {
                    var prefab = Resources.Load<GameObject>(entry.resource);
                    if (!prefab || !prefab.GetComponent<MyEntity>() || !prefab.GetComponent<MyFightStatus>())
                        throw new InvalidOperationException("Original status prefab is unbound: " + entry.id);
                    prefabs.Add(entry.id, prefab);
                }
            }
            staging = new GameObject("Inactive original character-status staging");
            staging.transform.SetParent(root, false); staging.SetActive(false);
            container = staging.AddComponent<OfficialBattleEntityPositionContainer>(); container.EntitySlot = root;
        }

        public int Count => leases.Count;
        public float NextDelay => pending.Count == 0 ? float.PositiveInfinity : pending[0].Remaining;
        private static long Key(int owner, int instance) => ((long)owner << 32) | (uint)instance;

        public void Restore(FightBattleCheckpoint state = null)
        {
            checkpoint = state ?? executor.State;
            pending.Clear(); deferred.Clear();
            foreach (long key in new List<long>(leases.Keys)) End(key);
            foreach (var owner in checkpoint.Units)
                foreach (var status in owner.Statuses)
                    if (status.Components?.Seconds?.DefinitionResolved != true && prefabs.ContainsKey(status.ClassId))
                        Start(owner.Id, FightBattleStatusViewState.Capture(status), false);
        }

        public void Enqueue(FightBattleStepEvent step)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleStatusPlayback));
            if (step?.StatusViewEvents == null) return;
            foreach (var value in step.StatusViewEvents)
            {
                if (value?.Status == null || !prefabs.ContainsKey(value.Status.ClassId)) continue;
                var scheduled = new Scheduled { Event = value, Remaining = Mathf.Max(0f, value.Delay) };
                int index = pending.FindIndex(other => other.Remaining > scheduled.Remaining);
                if (index < 0) pending.Add(scheduled); else pending.Insert(index, scheduled);
            }
        }

        public void AdvanceTimers(float elapsed) { foreach (var value in pending) value.Remaining -= elapsed; }

        public void DispatchDue()
        {
            foreach (var value in new List<Deferred>(deferred.Values)) Start(value.Owner, value.Status, value.Fresh);
            while (pending.Count > 0 && pending[0].Remaining <= 0f)
            {
                var value = pending[0].Event; pending.RemoveAt(0);
                long key = Key(value.OwnerId, value.Status.InstanceId);
                switch (value.Kind)
                {
                    case FightBattleStatusViewEventKind.Start: Start(value.OwnerId, value.Status, true); break;
                    case FightBattleStatusViewEventKind.Update:
                        if (leases.TryGetValue(key, out var lease))
                        {
                            lease.Context.Creator = Body(value.Status.CreatorId);
                            scope.Dispatch(() => lease.State.Update(value.Status));
                        }
                        else if (deferred.TryGetValue(key, out var waiting)) waiting.Status = value.Status;
                        break;
                    case FightBattleStatusViewEventKind.Invoke:
                        if (leases.TryGetValue(key, out var invoked)) scope.Dispatch(invoked.State.Invoke);
                        break;
                    case FightBattleStatusViewEventKind.End: End(key); break;
                    default: throw new ArgumentOutOfRangeException(nameof(value.Kind));
                }
            }
        }

        private GameObject Body(int id) { var target = body(id); return target ? target.gameObject : null; }
        private int MasterId(int owner)
        { foreach (var unit in checkpoint.Units) if (unit.Id == owner) return unit.MasterId; return 0; }

        private void Start(int owner, FightBattleStatusViewState snapshot, bool fresh)
        {
            long key = Key(owner, snapshot.InstanceId);
            if (leases.ContainsKey(key)) throw new InvalidOperationException("Original status view started twice.");
            var target = Body(owner);
            if (!target) { deferred[key] = new Deferred { Owner = owner, Status = snapshot, Fresh = fresh }; return; }
            deferred.Remove(key);
            var agent = target.GetComponent<MyFightStatusAgent>();
            if (!agent) { agent = target.AddComponent<MyFightStatusAgent>(); ownedAgents.Add(agent); }
            var go = UnityEngine.Object.Instantiate(prefabs[snapshot.ClassId], staging.transform, false);
            var entity = go.GetComponent<MyEntity>(); var view = go.GetComponent<MyFightStatus>();
            var state = new State { Value = snapshot };
            var context = new Context { Status = state, Owner = target, Creator = Body(snapshot.CreatorId),
                Master = Body(MasterId(owner)), Agent = agent, HasIcon = hasIcon(snapshot.ClassId) };
            view.Bind(context, scope);
            entity.Bind(container, new EntityData { Status = state, Action = new ActionData { Owner = owner } }, fresh);
            leases.Add(key, new Lease { Entity = entity, View = view, State = state, Context = context });
            go.transform.SetParent(root, false);
            // MyFightStatus was originally a MyComponent before the counter component
            // in this prefab. Its adapted MonoBehaviour still starts in that order.
            scope.Dispatch(() => { view.OnStart(fresh); entity.MyStart(); });
        }

        private void End(long key)
        {
            deferred.Remove(key);
            if (!leases.TryGetValue(key, out var lease)) return;
            leases.Remove(key);
            scope.Dispatch(() => { lease.Entity.End(null); lease.View.OnEnd(); lease.Entity.Remove(null); });
            lease.Entity.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(lease.Entity.gameObject);
        }

        public void Dispose()
        {
            if (disposed) return;
            pending.Clear(); deferred.Clear();
            foreach (long key in new List<long>(leases.Keys)) End(key);
            foreach (var agent in ownedAgents) if (agent) UnityEngine.Object.Destroy(agent);
            ownedAgents.Clear(); UnityEngine.Object.Destroy(staging); disposed = true;
        }
    }
}
