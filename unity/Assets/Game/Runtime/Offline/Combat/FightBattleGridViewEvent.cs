using System;

namespace Zjcs.Combat.Official
{
    public enum FightBattleGridViewEventKind
    {
        Create, StatusStart, StatusUpdate, StatusEnd, Remove
    }

    // Captured at the actual entity/status transition. A settled checkpoint alone
    // loses entities created and removed in the same atomic step.
    [Serializable]
    public sealed class FightBattleGridViewEvent
    {
        public FightBattleGridViewEventKind Kind;
        public float Delay;
        public int GridId;
        public FightBattleGridViewState Grid;
        public FightBattleGridStatusViewState Status;
    }

    [Serializable]
    public sealed class FightBattleGridViewState
    {
        public int Id, EntityClassId, OwnerId, MasterId, Side, X, Y, Direction;
        public bool FlipX;

        public static FightBattleGridViewState Capture(FightBattleGridItemState item)
        {
            var unit = item.Runtime;
            return new FightBattleGridViewState
            {
                Id = item.Id, EntityClassId = item.EntityClassId, OwnerId = item.OwnerId,
                MasterId = unit.MasterId, Side = unit.Side, X = item.X, Y = item.Y,
                Direction = unit.Direction, FlipX = unit.HasFlipX ? unit.FlipX : unit.Direction == 1
            };
        }
    }

    [Serializable]
    public sealed class FightBattleGridStatusViewState
    {
        public int InstanceId, ClassId, CreatorId, StackCount, ActionType;
        public bool CanStack, Forbidden;

        public static FightBattleGridStatusViewState Capture(FightBattleStatusState status)
            => new FightBattleGridStatusViewState
            {
                InstanceId = status.InstanceId, ClassId = status.ClassId,
                CreatorId = status.CreatorId, StackCount = status.StackCount,
                ActionType = status.ActionType,
                CanStack = status.IsOpenStack, Forbidden = status.ForbiddenType != 0
            };
    }
}
