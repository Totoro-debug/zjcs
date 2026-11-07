using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleGridItemEntities;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityActions;
using Zjcs.Official.EntityClientComponents;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleGridEntityPlayback
    {
        private sealed class RoleData : BattleRoleCharacterViewData
        {
            public EcEntityIdentifier Master;
            public bool NewEntity, Dead;
            public override BattleRoleCharacterIdentity Character => null;
            public override EcEntityIdentifier MasterId => Master;
            public override bool Die => Dead;
            public override bool IsNew => NewEntity;
            public override event Action<bool, long> OnDie { add { } remove { } }
            // Grid removal is an entity end, not an HP/death transition. The
            // selected original grid has neither a health pool nor a die event.
        }

        private sealed class GridInfo : ECGridTransformInfo
        {
            private readonly List<Pos2d> body = new List<Pos2d> { new Pos2d(0, 0) };
            public override Vector2 RealCenter => Vector2.zero;
            public override List<Pos2d> GetBodyRange() => body;
        }

        private sealed class GridData : ECGridTransformData
        {
            public FightBattleGridViewState State;
            public OfficialBattleGridMapper Mapper;
            public Transform Root;
            private readonly GridInfo info = new GridInfo();
            private readonly HashSet<IGridTransformPosUpdatedEventReceiver> receivers =
                new HashSet<IGridTransformPosUpdatedEventReceiver>();
            public override Pos2d Pos => new Pos2d(State.X, State.Y);
            public override Direction Direction
            {
                get
                {
                    switch (State.Direction)
                    {
                        case 0: return Direction.Down;
                        case 1: return Direction.Left;
                        case 2: return Direction.Up;
                        case 3: return Direction.Right;
                        default: throw new ArgumentOutOfRangeException(nameof(State.Direction));
                    }
                }
            }
            public override bool FlipX => State.FlipX;
            public override ECGridTransformInfo Info => info;
            public override Vector3 GridToPosition(Pos2d point) => Root.InverseTransformPoint(
                Mapper.GridToWorld(new FightGridPoint(point.X, point.Y)));
            public override Pos2d PositionToGrid(Vector3 point)
            {
                var grid = Mapper.WorldToGrid(Root.TransformPoint(point));
                return new Pos2d(grid.X, grid.Y);
            }
            public override void AddPosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver receiver) => receivers.Add(receiver);
            public override void RemovePosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver receiver) => receivers.Remove(receiver);
        }

        private sealed class GridItemData : BattleGridItemViewData
        {
            public EcEntityIdentifier Owner;
            public override EcEntityIdentifier OwnerEntId => Owner;
        }

        private sealed class GridEntityData : ECViewEntityData, IBattleGridComponentViewData, IBattleEntityEndEvents
        {
            public readonly FightBattleGridViewState Snapshot;
            public readonly ActionsData Actions = new ActionsData();
            private readonly GridData grid;
            private readonly RoleData role;
            private readonly GridItemData item;
            public override int ClassId => Snapshot.EntityClassId;
            public override EcEntityIdentifier Id => UnitId(Snapshot.Id);
            public event Action<ECViewEntityData, object> Ended;
            public GridEntityData(FightBattleGridViewState state, OfficialBattleGridMapper mapper, Transform root, bool fresh,
                EcEntityIdentifier owner, EcEntityIdentifier master)
            {
                Snapshot = state;
                grid = new GridData { State = state, Mapper = mapper, Root = root };
                role = new RoleData { Master = master, NewEntity = fresh, Dead = false };
                item = new GridItemData { Owner = owner };
            }
            public override T GetComponent<T>()
            {
                object value = typeof(T) == typeof(ECGridTransformData) ? grid
                    : typeof(T) == typeof(BattleRoleCharacterViewData) ? role
                    : typeof(T) == typeof(BattleGridItemViewData) ? item
                    : typeof(T) == typeof(ActionAgentComponentData) ? Actions
                    : typeof(T) == typeof(IBattleGridComponentViewData) ? this
                    : typeof(T) == typeof(IBattleEntityEndEvents) ? this : null;
                return value == null ? default : (T)value;
            }
            public void End() => Ended?.Invoke(this, null);
        }

        private sealed class StatusEntityData : ECViewEntityData, IBattleGridComponentViewData
        {
            private readonly FightBattleGridStatusViewState status;
            private readonly StatusActionData action;
            public StatusEntityData(int owner, FightBattleGridStatusViewState status)
            { this.status = status; action = new StatusActionData { Owner = owner }; }
            public override int ClassId => status.ClassId;
            public override EcEntityIdentifier Id => new EcEntityIdentifier(4, status.InstanceId);
            public override T GetComponent<T>()
            {
                object value = typeof(T) == typeof(IBattleGridComponentViewData) ? this
                    : typeof(T) == typeof(ActionComponentData) ? action : null;
                return value == null ? default : (T)value;
            }
        }

        private sealed class StatusActionData : ActionComponentData
        {
            public int Owner;
            public override EcEntityIdentifier ApplyId => UnitId(Owner);
            public override object ComponentInfo => null;
        }

        private sealed class ActionRecord : ActionAgentActionData
        {
            public FightBattleGridStatusViewState State;
            public override object Original => State;
            public override ActionType ActionType => (ActionType)State.ActionType;
            public override EcEntityIdentifier EntityId => new EcEntityIdentifier(4, State.InstanceId);
            public override event Action<ActionAgentActionData, object> EntityEndEvent;
            public void End() => EntityEndEvent?.Invoke(this, null);
        }

        private sealed class ActionsData : ActionAgentComponentData
        {
            private readonly Dictionary<int, ActionRecord> actions = new Dictionary<int, ActionRecord>();
            public override object Original => this;
            public override object ComponentInfo => null;
            public override bool HasActionType(ActionType type) => FindAction(type).IsValid();
            public override EcEntityIdentifier FindAction(ActionType type)
            {
                foreach (var action in actions.Values) if (action.ActionType == type) return action.EntityId;
                return default;
            }
            public override event Action<ActionAgentComponentData, ActionAgentActionData> StartEvent;
            public override event Action<ActionAgentComponentData, ActionAgentActionData> EndEvent;
            public void Add(FightBattleGridStatusViewState state)
            {
                var action = new ActionRecord { State = state };
                actions.Add(state.InstanceId, action);
            }
            // ActionAgentComponentImplement.StartAction adds runtime data, then
            // starts the entity through the container, then publishes StartEvent.
            public void NotifyStart(int id) => StartEvent?.Invoke(this, actions[id]);
            public void End(int id)
            {
                if (!actions.TryGetValue(id, out var action)) return;
                actions.Remove(id); action.End(); EndEvent?.Invoke(this, action);
            }
        }

        private sealed class StatusState : IFightStatusPlayableState
        {
            private FightBattleGridStatusViewState state;
            public StatusState(FightBattleGridStatusViewState state) { this.state = state; }
            public int StackedCount => state.StackCount;
            public bool CanStackCount => state.CanStack;
            public bool Forbidden => state.Forbidden;
            public event Action<IFightStatusPlayableState, int, int> OnStatusStackUpdate;
            public event Action<IFightStatusPlayableState> OnInvoked { add { } remove { } }
            public event Action<IFightStatusPlayableState, bool> OnForbiddenChanged;
            public void Update(FightBattleGridStatusViewState next)
            {
                var before = state; state = next;
                if (before.StackCount != next.StackCount) OnStatusStackUpdate?.Invoke(this, before.StackCount, next.StackCount);
                if (before.Forbidden != next.Forbidden) OnForbiddenChanged?.Invoke(this, next.Forbidden);
            }
        }

        private sealed class StatusContext : IFightStatusPlayableContext
        {
            public StatusState Status;
            public GameObject OwnerObject, CreatorObject, MasterObject;
            public MyFightStatusAgent StatusAgent;
            public bool Icon;
            public IFightStatusPlayableState State => Status;
            public GameObject Owner => OwnerObject;
            public GameObject Creator => CreatorObject;
            public GameObject Master => MasterObject;
            public MyFightStatusAgent Agent => StatusAgent;
            public int? ShareId => null; // Neither selected original status has a share-stack component.
            public bool HasIcon => Icon;
        }
    }
}
