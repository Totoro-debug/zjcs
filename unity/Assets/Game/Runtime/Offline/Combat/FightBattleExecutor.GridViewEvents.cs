using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private sealed class PendingGridViewEvent
        {
            public FightBattleGridViewEvent Event;
            public FightBattleSkillCastEvent Cast;
        }

        private List<PendingGridViewEvent> gridViewEvents;
        private readonly Dictionary<int, FightBattleGridStatusViewState> gridViewStatuses =
            new Dictionary<int, FightBattleGridStatusViewState>();
        private FightBattleSkillCastEvent gridViewCast;
        private float? gridViewTimeOverride;

        private void BeginGridViewEvents()
        {
            gridViewEvents = new List<PendingGridViewEvent>();
            gridViewStatuses.Clear();
            foreach (var item in checkpoint.GridItems)
            {
                if (item == null || !item.HasRuntime || item.Runtime == null) continue;
                item.ViewRemoved = RecordGridViewRemove;
                foreach (var status in item.Runtime.Statuses)
                    gridViewStatuses[status.InstanceId] = FightBattleGridStatusViewState.Capture(status);
            }
        }

        private void ClearGridViewEvents()
        {
            foreach (var item in checkpoint.GridItems) if (item != null) item.ViewRemoved = null;
            gridViewEvents = null;
            gridViewStatuses.Clear();
            gridViewCast = null;
            gridViewTimeOverride = null;
        }

        private void RecordGridView(FightBattleGridViewEvent value)
        {
            if (gridViewEvents == null) return;
            value.Delay = Math.Max(0f, gridViewTimeOverride
                ?? (movementStartContext == null ? 0f : movementStartContext.StatusStartTime));
            gridViewEvents.Add(new PendingGridViewEvent { Event = value, Cast = gridViewCast });
        }

        private void RecordGridViewCreate(FightBattleGridItemState item)
        {
            item.ViewRemoved = RecordGridViewRemove;
            RecordGridView(new FightBattleGridViewEvent { Kind = FightBattleGridViewEventKind.Create,
                GridId = item.Id, Grid = FightBattleGridViewState.Capture(item) });
        }

        private void RecordGridViewRemove(FightBattleGridItemState item)
            => RecordGridView(new FightBattleGridViewEvent { Kind = FightBattleGridViewEventKind.Remove,
                GridId = item.Id });

        private void RecordGridStatus(FightBattleUnitState owner, FightBattleStatusState status,
            FightBattleGridViewEventKind kind)
        {
            if (gridViewEvents == null || owner == null || owner.CharacterType != 6 || status == null) return;
            var value = FightBattleGridStatusViewState.Capture(status);
            if (kind == FightBattleGridViewEventKind.StatusUpdate)
            {
                if (!gridViewStatuses.TryGetValue(status.InstanceId, out var previous)) return;
                if (previous.StackCount == value.StackCount && previous.Forbidden == value.Forbidden) return;
            }
            if (kind == FightBattleGridViewEventKind.StatusEnd) gridViewStatuses.Remove(status.InstanceId);
            else gridViewStatuses[status.InstanceId] = value;
            RecordGridView(new FightBattleGridViewEvent { Kind = kind, GridId = owner.Id, Status = value });
        }

        private void CompleteGridViewEvents(FightBattleStepEvent result)
        {
            if (gridViewEvents == null) return;
            foreach (var pending in gridViewEvents)
            {
                // Nested callers offset the actual cast object after execution.
                // Resolve only now, after approach/pet/replay/trigger offsets finish.
                pending.Event.Delay += Math.Max(0f, pending.Cast?.Delay ?? 0f);
                result.GridViewEvents.Add(pending.Event);
            }
        }
    }
}
