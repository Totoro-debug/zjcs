using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private sealed class PendingStatusViewEvent
        {
            public FightBattleStatusViewEvent Event;
            public FightBattleSkillCastEvent Cast;
        }

        private List<PendingStatusViewEvent> statusViewEvents;
        private readonly Dictionary<long, FightBattleStatusViewState> statusViewSnapshots =
            new Dictionary<long, FightBattleStatusViewState>();

        private static long StatusViewKey(int owner, int instance) => ((long)owner << 32) | (uint)instance;

        private void BeginStatusViewEvents()
        {
            statusViewEvents = new List<PendingStatusViewEvent>();
            statusViewSnapshots.Clear();
            foreach (var owner in units)
                foreach (var status in owner.Statuses)
                    if (status != null && status.Components?.Seconds?.DefinitionResolved != true)
                        statusViewSnapshots[StatusViewKey(owner.Id, status.InstanceId)] = FightBattleStatusViewState.Capture(status);
        }

        private void ClearStatusViewEvents()
        { statusViewEvents = null; statusViewSnapshots.Clear(); }

        private void OnStatusViewRefreshed(FightBattleUnitState owner, FightBattleStatusState status)
            => RecordStatusView(owner, status, owner.Statuses.Contains(status)
                ? FightBattleStatusViewEventKind.Update : FightBattleStatusViewEventKind.End);

        private void RecordStatusView(FightBattleUnitState owner, FightBattleStatusState status,
            FightBattleStatusViewEventKind kind)
        {
            // Grid statuses and active-seconds states retain their existing native clocks.
            if (statusViewEvents == null || owner == null || owner.CharacterType == 6
                || status == null || status.Components?.Seconds?.DefinitionResolved == true) return;
            long key = StatusViewKey(owner.Id, status.InstanceId);
            var snapshot = FightBattleStatusViewState.Capture(status);
            if (kind == FightBattleStatusViewEventKind.Update
                && (!statusViewSnapshots.TryGetValue(key, out var previous) || snapshot.SameView(previous))) return;
            if (kind == FightBattleStatusViewEventKind.End) statusViewSnapshots.Remove(key);
            else statusViewSnapshots[key] = snapshot;
            statusViewEvents.Add(new PendingStatusViewEvent {
                Event = new FightBattleStatusViewEvent { Kind = kind, OwnerId = owner.Id, Status = snapshot,
                    Delay = Math.Max(0f, gridViewTimeOverride ?? movementStartContext?.StatusStartTime ?? 0f) },
                Cast = gridViewCast
            });
        }

        private void CompleteStatusViewEvents(FightBattleStepEvent result)
        {
            foreach (var value in statusViewEvents)
            {
                // Resolve nested cast offsets only after their final placement is known.
                value.Event.Delay += Math.Max(0f, value.Cast?.Delay ?? 0f);
                result.StatusViewEvents.Add(value.Event);
            }
        }
    }
}
