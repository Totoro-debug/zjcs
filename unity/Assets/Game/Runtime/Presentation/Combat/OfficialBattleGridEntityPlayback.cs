using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Offline.World.NativeMap14EntityLifecycle;
using Zjcs.Official.BattleGridItemEntities;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Combat
{
    // Binds original prefab components to captured local entity transitions.
    // Rules and timing remain in the executor; no lifetime is inferred from HP.
    public sealed partial class OfficialBattleGridEntityPlayback : IDisposable,
        IBattleGridRoleViewRoot, IServiceProvider, IECViewInjection, IBattleGridPlayableEnvironment
    {
        [Serializable] private sealed class Catalog { public Entry[] graphs; }
        [Serializable] private sealed class Entry { public int id; public string resource; }
        private sealed class Scheduled
        {
            public FightBattleGridViewEvent Value;
            public float Remaining;
        }
        private sealed class GridLease
        {
            public GameObject Object;
            public MyEntity Entity;
            public GridEntityData Data;
            public readonly Dictionary<int, StatusLease> Statuses = new Dictionary<int, StatusLease>();
        }
        private sealed class StatusLease
        {
            public MyEntity Entity;
            public MyFightStatus View;
            public StatusState State;
        }
        private sealed class RoleLease
        {
            public MyFightRoleCharacter Role;
            public bool Owned;
        }

        private readonly Transform root;
        private readonly GameObject staging;
        private readonly OfficialBattleGridMapper mapper;
        private readonly OfficialBattleEntityPositionContainer container;
        private readonly OfflineBattleNativePlayableScope scope;
        private readonly OfficialBattleSkillEntityPlayback skillEntities;
        private readonly Func<int, Transform> unitTransform;
        private readonly Func<int, bool> hasStatusIcon;
        private readonly Dictionary<int, GameObject> prefabs = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GridLease> grids = new Dictionary<int, GridLease>();
        private readonly Dictionary<int, RoleLease> roles = new Dictionary<int, RoleLease>();
        private readonly HashSet<MyFightRoleCharacter> characters = new HashSet<MyFightRoleCharacter>();
        private readonly List<Scheduled> pending = new List<Scheduled>();
        private FightBattleCheckpoint checkpoint;
        private bool disposed;

        public OfficialBattleGridEntityPlayback(Transform root, OfficialBattleGridMapper mapper,
            OfflineBattleNativePlayableScope scope, OfficialBattleSkillEntityPlayback skillEntities,
            FightBattleCheckpoint checkpoint, Func<int, Transform> unitTransform,
            Func<int, bool> hasStatusIcon)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
            this.skillEntities = skillEntities ?? throw new ArgumentNullException(nameof(skillEntities));
            this.checkpoint = checkpoint ?? throw new ArgumentNullException(nameof(checkpoint));
            this.unitTransform = unitTransform ?? throw new ArgumentNullException(nameof(unitTransform));
            this.hasStatusIcon = hasStatusIcon ?? throw new ArgumentNullException(nameof(hasStatusIcon));
            var text = Resources.Load<TextAsset>("Combat/grid-entities");
            if (text == null) throw new InvalidOperationException("Original native grid catalog is missing.");
            foreach (var entry in JsonUtility.FromJson<Catalog>(text.text).graphs)
            {
                var prefab = Resources.Load<GameObject>(entry.resource);
                if (prefab == null) throw new InvalidOperationException("Original grid prefab is missing: " + entry.id);
                prefabs.Add(entry.id, prefab);
            }
            staging = new GameObject("Inactive native battle grid staging");
            staging.transform.SetParent(root, false); staging.SetActive(false);
            container = staging.AddComponent<OfficialBattleEntityPositionContainer>();
            container.EntitySlot = root;
        }

        public long? MainPlayerId => null; // No server account identity is fabricated.
        public bool IsPvpStage => checkpoint.IsPvp;
        public bool HasFightRoot => !disposed;
        public bool IsFastforward => false;
        public bool NotPlayGeneratePlayable => false;
        public MyFightRoleCharacter Character { get; set; }
        public IEnumerable<MyFightRoleCharacter> Characters => characters;
        public MyEntity GetEntity(EcEntityIdentifier id)
        {
            var entity = skillEntities.GetEntity(id);
            if (entity != null) return entity;
            foreach (var lease in roles.Values)
                if (lease.Role != null && lease.Role.myEntity.Id == id) return lease.Role.myEntity;
            return null;
        }
        public void AddCharacter(MyFightRoleCharacter value) => characters.Add(value);
        public void RemoveCharacter(MyFightRoleCharacter value) => characters.Remove(value);
        public int? FactionOf(MyEntity entity)
        {
            if (entity == null) return null;
            foreach (var pair in roles)
                if (pair.Value.Role != null && pair.Value.Role.myEntity == entity) return FindState(pair.Key)?.Side;
            foreach (var lease in grids.Values)
                if (lease.Entity == entity) return lease.Data.Snapshot.Side;
            return null;
        }
        public Transform TryGetTransform(int id) => grids.TryGetValue(id, out var lease) ? lease.Object.transform : null;
        public float NextDelay => pending.Count == 0 ? float.PositiveInfinity : pending[0].Remaining;
        public int Count => grids.Count;
        public object GetService(Type type) => null;

        public void Inject(object value)
        {
            if (value is ECFightRoleCharacterView role)
            { role.MyPlayableManager = scope; role.GamePlayManager = this; role.FightRoot = this; }
            if (value is ECFightRoleStatusDisplayView statuses) statuses.mFightRoot = this;
        }

        public void Restore()
        {
            foreach (var item in checkpoint.GridItems)
            {
                if (item == null || item.Ended || !item.HasRuntime || item.Runtime == null || item.Runtime.Ended) continue;
                Create(FightBattleGridViewState.Capture(item), false);
                foreach (var status in item.Runtime.Statuses)
                    StartStatus(item.Id, FightBattleGridStatusViewState.Capture(status), false);
            }
        }

        public void RefreshSnapshot(FightBattleCheckpoint state)
        {
            pending.Clear();
            foreach (var id in new List<int>(grids.Keys)) Remove(id);
            checkpoint = state ?? throw new ArgumentNullException(nameof(state));
            Restore();
        }

        public void Enqueue(FightBattleStepEvent step, FightBattleCheckpoint state)
        {
            checkpoint = state ?? throw new ArgumentNullException(nameof(state));
            if (step?.GridViewEvents == null) return; // Old between-step saves restore through Restore().
            foreach (var value in step.GridViewEvents)
            {
                var scheduled = new Scheduled { Value = value, Remaining = Mathf.Max(0f, value.Delay) };
                int index = pending.FindIndex(other => other.Remaining > scheduled.Remaining);
                if (index < 0) pending.Add(scheduled); else pending.Insert(index, scheduled);
            }
        }

        public void AdvanceTimers(float elapsed)
        { foreach (var value in pending) value.Remaining -= elapsed; }

        public void DispatchDue()
        {
            while (pending.Count > 0 && pending[0].Remaining <= 0f)
            {
                var value = pending[0].Value; pending.RemoveAt(0);
                switch (value.Kind)
                {
                    case FightBattleGridViewEventKind.Create: Create(value.Grid, true); break;
                    case FightBattleGridViewEventKind.StatusStart: StartStatus(value.GridId, value.Status, true); break;
                    case FightBattleGridViewEventKind.StatusUpdate: UpdateStatus(value.GridId, value.Status); break;
                    case FightBattleGridViewEventKind.StatusEnd: EndStatus(value.GridId, value.Status.InstanceId); break;
                    case FightBattleGridViewEventKind.Remove: Remove(value.GridId); break;
                    default: throw new ArgumentOutOfRangeException(nameof(value.Kind));
                }
            }
        }

        private FightBattleUnitState FindState(int id)
        {
            foreach (var unit in checkpoint.Units) if (unit.Id == id) return unit;
            foreach (var grid in checkpoint.GridItems) if (grid != null && grid.HasRuntime && grid.Id == id) return grid.Runtime;
            return null;
        }

        private void EnsureRole(int id)
        {
            if (id <= 0 || grids.ContainsKey(id)) return;
            if (roles.TryGetValue(id, out var previous))
            {
                if (previous.Role != null) return;
                roles.Remove(id);
            }
            var state = FindState(id);
            var transform = unitTransform(id);
            if (state == null || transform == null) return;
            var entity = skillEntities.EnsureUnit(id, transform);
            var role = entity.GetComponent<MyFightRoleCharacter>();
            bool owned = role == null;
            if (owned)
            {
                role = entity.gameObject.AddComponent<MyFightRoleCharacter>();
                role.Component = new RoleData { Master = EntityIdentifier(state.MasterId), NewEntity = false,
                    Dead = state.Hp <= 0 && state.CharacterType != 6 };
                role.mFightRoot = this;
            }
            roles.Add(id, new RoleLease { Role = role, Owned = owned });
            if (owned) role.OnStart(false); else AddCharacter(role);
            if (id == 1) Character = role; // Existing local main-unit identity.
        }

        private GameObject Instantiate(int classId)
        {
            if (!prefabs.TryGetValue(classId, out var prefab))
                throw new InvalidOperationException("Original native grid/status prefab is not bound: " + classId);
            return UnityEngine.Object.Instantiate(prefab, staging.transform, false);
        }

        private void Create(FightBattleGridViewState state, bool fresh)
        {
            if (grids.ContainsKey(state.Id)) throw new InvalidOperationException("Native grid was created twice: " + state.Id);
            EnsureRole(1); EnsureRole(state.OwnerId); EnsureRole(state.MasterId);
            var go = Instantiate(state.EntityClassId);
            var data = new GridEntityData(state, mapper, root, fresh,
                EntityIdentifier(state.OwnerId), EntityIdentifier(state.MasterId));
            var entity = go.GetComponent<MyEntity>();
            go.AddComponent<NativeMyEntityBinding>();
            var driver = go.GetComponentInChildren<ECViewDriver>(true);
            driver.ServiceProvider = this;
            driver.ClientVisibilitySupporterFactory = new ConstantClientVisibilitySupporterFactory();
            go.GetComponent<MyFightRoleCharacter>().mFightRoot = this;
            var view = go.GetComponent<MyFightRoleGridItem>(); view.mFightRoot = this; view.mMyPlayableManager = scope;
            entity.Bind(container, data, fresh);
            var lease = new GridLease { Object = go, Entity = entity, Data = data };
            grids.Add(state.Id, lease); skillEntities.RegisterGrid(state.Id, entity);
            go.transform.SetParent(root, false);
            scope.Dispatch(entity.MyStart);
        }

        private void StartStatus(int gridId, FightBattleGridStatusViewState state, bool fresh)
        {
            var grid = grids[gridId];
            if (grid.Statuses.ContainsKey(state.InstanceId)) throw new InvalidOperationException("Native grid status was started twice.");
            var go = Instantiate(state.ClassId);
            var entity = go.GetComponent<MyEntity>();
            var view = go.GetComponent<MyFightStatus>();
            var status = new StatusState(state);
            var context = new StatusContext
            {
                Status = status, OwnerObject = grid.Object,
                CreatorObject = EntityObject(state.CreatorId), MasterObject = EntityObject(grid.Data.Snapshot.MasterId),
                StatusAgent = grid.Object.GetComponent<MyFightStatusAgent>(), Icon = hasStatusIcon(state.ClassId)
            };
            view.Bind(context, scope);
            entity.Bind(container, new StatusEntityData(gridId, state), fresh);
            grid.Statuses.Add(state.InstanceId, new StatusLease { Entity = entity, View = view, State = status });
            grid.Data.Actions.Add(state);
            go.transform.SetParent(root, false);
            scope.Dispatch(() => { entity.MyStart(); view.OnStart(fresh); grid.Data.Actions.NotifyStart(state.InstanceId); });
        }

        private GameObject EntityObject(int id)
        { var entity = GetEntity(UnitId(id)); return entity == null ? null : entity.gameObject; }

        private EcEntityIdentifier EntityIdentifier(int id)
        { var entity = skillEntities.GetEntity(UnitId(id)); return entity == null ? UnitId(id) : entity.Id; }

        private void UpdateStatus(int gridId, FightBattleGridStatusViewState state)
        {
            var status = grids[gridId].Statuses[state.InstanceId];
            scope.Dispatch(() => status.State.Update(state));
        }

        private void EndStatus(int gridId, int instanceId)
        {
            var grid = grids[gridId];
            if (!grid.Statuses.TryGetValue(instanceId, out var status)) return;
            grid.Statuses.Remove(instanceId);
            scope.Dispatch(() => { status.View.OnEnd(); status.Entity.End(null); status.Entity.Remove(null); });
            grid.Data.Actions.End(instanceId);
            Destroy(status.Entity.gameObject);
        }

        private void Remove(int gridId)
        {
            if (!grids.TryGetValue(gridId, out var lease)) return;
            foreach (var id in new List<int>(lease.Statuses.Keys)) EndStatus(gridId, id);
            scope.Dispatch(() => { lease.Entity.End(null); lease.Data.End(); lease.Entity.Remove(null); });
            // A zero-HP grid ending is not a character death notification.
            skillEntities.RemoveGrid(gridId); grids.Remove(gridId); Destroy(lease.Object);
        }

        public void Dispose()
        {
            if (disposed) return;
            pending.Clear();
            foreach (var id in new List<int>(grids.Keys)) Remove(id);
            foreach (var lease in roles.Values)
            {
                if (lease.Role == null) continue;
                if (lease.Owned) { lease.Role.OnRemove(null); Destroy(lease.Role); }
                else RemoveCharacter(lease.Role);
            }
            roles.Clear(); characters.Clear(); Character = null;
            disposed = true; Destroy(staging);
        }

        private static EcEntityIdentifier UnitId(int id) => new EcEntityIdentifier(1, id);
        private static void Destroy(UnityEngine.Object value)
        { if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
    }
}
