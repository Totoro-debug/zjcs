using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.BattleNativePlayables;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.EntityViewPlayables;
using Zjcs.Offline.World.NativeMap14EntityLifecycle;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Projects settled cast inputs into the original MyFightSkill lifecycle.</summary>
    public sealed partial class OfficialBattleSkillEntityPlayback : IFightHitViewServices, IServiceProvider, IECViewInjection, IDisposable
    {
        private sealed class Grid : ECGridTransformData
        {
            public Pos2d Position;
            public Direction Facing;
            public bool Mirrored;
            public Func<Pos2d, Vector3> ToPosition;
            public Func<Vector3, Pos2d> ToGrid;
            public readonly HashSet<IGridTransformPosUpdatedEventReceiver> Receivers =
                new HashSet<IGridTransformPosUpdatedEventReceiver>();
            public override Pos2d Pos => Position;
            public override Direction Direction => Facing;
            public override bool FlipX => Mirrored;
            public override ECGridTransformInfo Info => null;
            public override Vector3 GridToPosition(Pos2d pos) => ToPosition(pos);
            public override Pos2d PositionToGrid(Vector3 pos) => ToGrid(pos);
            public override void AddPosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver value) => Receivers.Add(value);
            public override void RemovePosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver value) => Receivers.Remove(value);
        }

        private sealed class EntityData : ECViewEntityData
        {
            public int TypeId;
            public EcEntityIdentifier Identifier;
            public Grid Grid;
            public SkillState Skill;
            public OfficialBattleSkillPrefabData Additional;
            public override int ClassId => TypeId;
            public override EcEntityIdentifier Id => Identifier;
            public override T GetComponent<T>()
            {
                object value = typeof(T) == typeof(ECGridTransformData) ? Grid
                    : typeof(T) == typeof(FightSkillComponent) ? Skill : null;
                return value == null ? Additional == null ? default : Additional.GetComponent<T>() : (T)value;
            }
        }

        private sealed class SkillInfo : FightSkillComponentInfo
        {
            public FightBattleSkillCastPresentation Cast;
            public override bool FlipSource => Cast.FlipSource;
            public override bool IsEntityTargetType() => Cast.SkillTargetType == 1
                || Cast.SkillTargetType == 4 || Cast.SkillTargetType == 5;
        }

        private sealed class SkillState : FightSkillComponent
        {
            public FightBattleSkillCastEvent Cast;
            public EntityData Data;
            public readonly SkillInfo Configuration = new SkillInfo();
            public override EcEntityIdentifier SrcEntityId => Cast.IsPetSupport ? PetId(Cast.ActorId) : UnitId(Cast.ActorId);
            public override EcEntityIdentifier TargetId => UnitId(Cast.Presentation.TargetId);
            public override Pos2d TargetPos => new Pos2d(Cast.Presentation.TargetX, Cast.Presentation.TargetY);
            public override int SkillSlot => Cast.IsPetSupport ? -2 - Cast.Slot : Cast.Slot;
            public override bool Interrupted => false;
            public override ECViewEntityData Entity => Data;
            public override FightSkillComponentInfo Info => Configuration;
        }

        private sealed class UnitLease
        {
            public Transform Transform;
            public MyEntity Entity;
            public MyGridTransform GridView;
            public ECGridTransformData PreviousGrid;
            public Grid Projection;
            public MyFightSkillAgent SkillAgent;
            public Action<MyFightSkill> SkillApplied, SkillEnded;
            public readonly List<Component> Added = new List<Component>();
        }

        private sealed class ActiveCast
        {
            public MyEntity Entity;
            public float Remaining;
            public bool Removed;
        }

        private readonly Transform root;
        private readonly OfficialBattleEntityPositionContainer positionContainer;
        private readonly OfficialBattleGridMapper gridMapper;
        internal Zjcs.Official.StoryGroundEffects.IPlayableGroundEffectHost GroundEffects { get; }
        private readonly FightOfficialCombatCatalog combatCatalog;
        private readonly OfficialBattleNativeEntityLoader skillViews;
        private readonly OfflineBattleNativePlayableScope scope;
        private readonly Func<int, FightBattleUnitState> unitState;
        private readonly OfficialBattleHitReceiptPlayback receiptHits;
        private readonly Dictionary<int, UnitLease> units = new Dictionary<int, UnitLease>();
        private readonly Dictionary<int, UnitLease> pets = new Dictionary<int, UnitLease>();
        private OfficialBattlePetFollowerCatalog petCatalog;
        private readonly Dictionary<EcEntityIdentifier, MyEntity> entities = new Dictionary<EcEntityIdentifier, MyEntity>();
        private readonly HashSet<int> nativeGridIds = new HashSet<int>();
        private readonly List<ActiveCast> casts = new List<ActiveCast>();
        private int serial;
        private bool disposed;

        // FightSkillListDataEntityProvider forwards these original agent events
        // by slot. Keep the unit identity so another unit cannot alter the HUD.
        public event Action<int, int> SkillStarted;
        public event Action<int, int> SkillEnded;
        // Original pet provider consumes FightSkillAgentComponent.OnSkillStarted,
        // including negative-slot casts. Preserve the source item separately
        // from MyFightSkill's main-slot-only applied/ended notifications.
        public event Action<FightBattleSkillCastEvent> CastStarted;

        public OfficialBattleSkillEntityPlayback(Transform root, OfficialBattleGridMapper gridMapper,
            OfflineBattleNativePlayableScope scope, Func<int, FightBattleUnitState> unitState)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            this.gridMapper = gridMapper ?? throw new ArgumentNullException(nameof(gridMapper));
            GroundEffects = new OfficialBattleGroundEffectLibrary(() => this.gridMapper);
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
            this.unitState = unitState ?? throw new ArgumentNullException(nameof(unitState));
            combatCatalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var containerRoot = new GameObject("Battle presentation entity coordinates");
            containerRoot.transform.SetParent(root, false);
            positionContainer = containerRoot.AddComponent<OfficialBattleEntityPositionContainer>();
            positionContainer.EntitySlot = root;
            skillViews = new OfficialBattleNativeEntityLoader(root, positionContainer, scope, true,
                PrepareSkillInstance, PrepareBoundSkill);
            moveViews = new OfficialBattleMoveActionPlayback(root, positionContainer, scope, GetEntity);
            receiptHits = new OfficialBattleHitReceiptPlayback(this, combatCatalog);
        }

        public MyEntity GetEntity(EcEntityIdentifier id) =>
            entities.TryGetValue(id, out var entity) ? entity
                : skillViews.GetEntity(id) ?? hitViews?.GetEntity(id) ?? moveViews?.GetEntity(id);

        public MyEntity EnsureUnit(int id, Transform transform) => BindUnit(id, transform).Entity;

        public void RegisterGrid(int id, MyEntity entity)
        {
            if (entity == null || entities.ContainsKey(UnitId(id)))
                throw new InvalidOperationException("Native battle grid identity is already registered: " + id);
            nativeGridIds.Add(id);
            entities.Add(UnitId(id), entity);
        }

        public void RemoveGrid(int id)
        {
            if (nativeGridIds.Remove(id)) entities.Remove(UnitId(id));
        }

        public Vector3 GridToPos(Pos2d position) => root.InverseTransformPoint(
            gridMapper.GridToWorld(new FightGridPoint(position.X, position.Y)));

        public Vector3 ToWorldPos(Vector3 position) => root.TransformPoint(position);

        private OfficialBattleHitEntityPlayback hitViews;

        public MyEntity CreateHit(ECViewEntityData data)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleSkillEntityPlayback));
            hitViews ??= new OfficialBattleHitEntityPlayback(root, positionContainer, this, scope);
            return hitViews.Create(data);
        }

        public void RemoveHit(EcEntityIdentifier id) => hitViews?.Remove(id);

        private Pos2d PositionToGrid(Vector3 position)
        {
            FightGridPoint point = gridMapper.WorldToGrid(root.TransformPoint(position));
            return new Pos2d(point.X, point.Y);
        }

        public EcEntityIdentifier Start(FightBattleSkillCastEvent cast, Transform source, Transform target)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleSkillEntityPlayback));
            if (cast.Presentation == null)
                throw new InvalidOperationException("A native skill requires its creation-time presentation snapshot.");
            var sourcePosition = new Pos2d(cast.Presentation.SourceX, cast.Presentation.SourceY);
            var sourceFacing = Facing(cast.Presentation.FacingIndex);
            if (!nativeGridIds.Contains(cast.ActorId) || cast.IsPetSupport)
            {
                var sourceLease = BindUnit(cast.ActorId, source, cast.IsPetSupport);
                sourceLease.Projection.Position = sourcePosition;
                sourceLease.Projection.Mirrored = cast.Presentation.FlipX;
            }
            // Original grid prefabs already own their bound transform and its event
            // subscriptions. A child cast must not replace that component projection.
            if (cast.Presentation.TargetId > 0 && target != null && !nativeGridIds.Contains(cast.Presentation.TargetId))
                BindUnit(cast.Presentation.TargetId, target);
            var state = new SkillState { Cast = cast };
            state.Configuration.Cast = cast.Presentation;
            state.Data = new EntityData { TypeId = cast.SkillEntityId,
                Identifier = new EcEntityIdentifier(2, ++serial), Skill = state,
                Additional = new OfficialBattleSkillPrefabData(combatCatalog, cast.SkillEntityId),
                Grid = new Grid { Position = sourcePosition,
                    Facing = sourceFacing, Mirrored = cast.Presentation.FlipX,
                    ToPosition = GridToPos, ToGrid = PositionToGrid } };
            var entity = skillViews.Create(state.Data);
            casts.Add(new ActiveCast { Entity = entity, Remaining = cast.Presentation.Duration < 0f
                ? float.PositiveInfinity : cast.Presentation.Duration });
            scope.Dispatch(entity.MyStart);
            receiptHits.Enqueue(cast, state.Data.Identifier);
            CastStarted?.Invoke(cast);
            // Return the assigned identity, not the pooled object whose Id can
            // change on reuse. Dependent views belong to this exact cast.
            return state.Data.Identifier;
        }

        // Original ECViewDriver injects its serialized views during Awake.
        // The loader holds new roots inactive until these bindings and Bind finish.
        private void PrepareSkillInstance(GameObject instance)
        {
            var skill = instance.GetComponent<MyFightSkill>();
            if (!skill) throw new InvalidOperationException("Original skill prefab has no MyFightSkill.");
            skill.ViewServices = this; skill.myPlayableManager = scope;
            foreach (var driver in instance.GetComponentsInChildren<ECViewDriver>(true))
            {
                if (!instance.GetComponent<ECViewEntityBinding>()) instance.AddComponent<NativeMyEntityBinding>();
                driver.ServiceProvider = this;
                driver.ClientVisibilitySupporterFactory = new ConstantClientVisibilitySupporterFactory();
            }
        }

        private void PrepareBoundSkill(MyEntity entity)
        {
            var data = (EntityData)entity.Entity;
            var context = new OfficialBattleSkillSourceStatusSnapshot(data.Skill.Cast.Presentation,
                data.Skill.SrcEntityId, combatCatalog);
            foreach (var view in entity.GetComponents<PlayableOverrideByStatusStack>()) view.StatusServices = context;
            foreach (var view in entity.GetComponents<MyFightHitExtDelayInfoOverriderByStatusStack>()) view.StatusServices = context;
        }

        public object GetService(Type type) => null;
        public void Inject(object value)
        {
            if (value is ECPlayableSupporterView playable)
            { playable.MyPlayableManager = scope; playable.ServiceProvider = this; }
            else throw new InvalidOperationException("Unbound original skill entity view: " + value?.GetType().FullName);
        }

        // Split native updates at entity removal as well as cast creation. Original
        // MyFightSkill.OnRemove publishes ended and detaches interruption; it does not stop graphs.
        public void Tick(float deltaTime)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleSkillEntityPlayback));
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            float remaining = deltaTime;
            while (true)
            {
                float next = float.PositiveInfinity;
                foreach (var cast in casts) if (!cast.Removed) next = Mathf.Min(next, cast.Remaining);
                next = Mathf.Min(next, moveViews.NextDelay);
                next = Mathf.Min(next, receiptHits.NextDelay);
                float elapsed = Mathf.Min(next, remaining);
                if (elapsed > 0f) scope.Tick(elapsed);
                moveViews.Advance(elapsed);
                receiptHits.Advance(elapsed);
                foreach (var cast in casts) if (!cast.Removed) cast.Remaining -= elapsed;
                remaining -= elapsed;
                // Hits due at a cast's final boundary still resolve that exact
                // skill identity before the original skill returns to its pool.
                receiptHits.DispatchDue();
                foreach (var cast in casts)
                    if (!cast.Removed && cast.Remaining <= 0f) Remove(cast);
                if (remaining <= 0f) break;
            }
        }

        private UnitLease BindUnit(int id, Transform transform, bool pet = false)
        {
            var leases = pet ? pets : units;
            var identifier = pet ? PetId(id) : UnitId(id);
            if (leases.TryGetValue(id, out var lease))
            {
                if (lease.Transform == transform) return lease;
                ReleaseUnit(lease);
            }
            lease = new UnitLease { Transform = transform,
                Projection = new Grid { ToPosition = GridToPos, ToGrid = PositionToGrid } };
            var state = unitState(id) ?? throw new InvalidOperationException("Native skill unit state is missing: " + id);
            lease.Projection.Position = new Pos2d(state.X, state.Y);
            lease.Projection.Mirrored = state.HasFlipX ? state.FlipX : state.Direction == 1;
            T Bind<T>() where T : Component
            {
                var value = transform.GetComponent<T>();
                if (value == null) { value = transform.gameObject.AddComponent<T>(); lease.Added.Add(value); }
                return value;
            }
            lease.Entity = Bind<MyEntity>();
            lease.GridView = Bind<MyGridTransform>();
            lease.PreviousGrid = lease.GridView.Component;
            // Role grid rotation is independent of the cast/action direction parameter.
            // The original movement StartImpl changes FlipX, not grid Direction. Keep
            // an existing grid's orientation, or the original new-grid default (Up),
            // so End.ResetRotation cannot rotate the character towards its skill aim.
            lease.Projection.Facing = lease.PreviousGrid?.Direction ?? Direction.Up;
            lease.SkillAgent = Bind<MyFightSkillAgent>();
            if (!pet)
            {
                lease.SkillApplied = skill => SkillStarted?.Invoke(id, skill.Component.SkillSlot);
                lease.SkillEnded = skill => SkillEnded?.Invoke(id, skill.Component.SkillSlot);
                lease.SkillAgent.OnSkillApplied += lease.SkillApplied;
                lease.SkillAgent.OnSkillEnded += lease.SkillEnded;
            }
            int typeId = OfficialBattleSceneBindings.EntityId(state);
            if (pet)
            {
                petCatalog ??= OfficialBattlePetFollowerCatalog.Load();
                typeId = petCatalog.Find(state.PetEnergy.PetClassId)?.entityId
                    ?? throw new InvalidOperationException("Original native pet source is unavailable.");
            }
            if (lease.Added.Contains(lease.Entity))
                lease.Entity.Bind(positionContainer, new EntityData { TypeId = typeId,
                    Identifier = identifier, Grid = lease.Projection }, true);
            lease.GridView.Component = lease.Projection;
            // Only configure a grid view owned by this adapter. Existing official
            // entity views retain their own serialized flip-transform binding.
            if (lease.Added.Contains(lease.GridView)) lease.GridView.BindFlipTransform(transform);
            leases[id] = lease;
            entities[identifier] = lease.Entity;
            return lease;
        }

        private void Remove(ActiveCast cast)
        {
            cast.Removed = true;
            if (cast.Entity != null) skillViews.Remove(cast.Entity.Id);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            moveViews.Dispose();
            receiptHits.Dispose();
            hitViews?.Dispose();
            hitViews = null;
            foreach (var cast in casts) if (!cast.Removed) Remove(cast);
            // Original entity FIFO pools release only after all live removals.
            skillViews.Dispose();
            scope.Dispose();
            casts.Clear();
            foreach (var lease in units.Values) ReleaseUnit(lease);
            foreach (var lease in pets.Values) ReleaseUnit(lease);
            units.Clear(); pets.Clear(); entities.Clear(); nativeGridIds.Clear();
            if (positionContainer) Destroy(positionContainer.gameObject);
        }

        private static void ReleaseUnit(UnitLease lease)
        {
            if (lease.SkillAgent != null)
            {
                lease.SkillAgent.OnSkillApplied -= lease.SkillApplied;
                lease.SkillAgent.OnSkillEnded -= lease.SkillEnded;
            }
            if (lease.GridView != null) lease.GridView.Component = lease.PreviousGrid;
            for (int i = lease.Added.Count - 1; i >= 0; i--) if (lease.Added[i] != null) Destroy(lease.Added[i]);
        }

        private static void Destroy(UnityEngine.Object value)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        private static EcEntityIdentifier UnitId(int id) => new EcEntityIdentifier(1, id);
        private static EcEntityIdentifier PetId(int ownerId) => new EcEntityIdentifier(3, ownerId);
        private static Direction Facing(int index)
        {
            switch (index)
            {
                case 0: return Direction.Down;
                case 1: return Direction.Left;
                case 2: return Direction.Up;
                case 3: return Direction.Right;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }
}
