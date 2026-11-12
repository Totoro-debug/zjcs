using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private sealed class PendingUnitEndEvent
        {
            public FightBattleUnitEndEvent Event;
            public FightBattleSkillCastEvent Cast;
        }

        private List<PendingUnitEndEvent> unitEndEvents;

        private void BeginUnitEndEvents() => unitEndEvents = new List<PendingUnitEndEvent>();

        private void RecordUnitEnd(FightBattleUnitState owner, FightBattleStatusState status)
        {
            if (unitEndEvents == null || owner == null || status == null || !owner.Ended
                || owner.Hp <= 0 || !status.RemoveApplyEntityOnEnd || owner.CharacterType == 6) return;
            foreach (var pending in unitEndEvents) if (pending.Event.UnitId == owner.Id) return;
            unitEndEvents.Add(new PendingUnitEndEvent {
                Event = new FightBattleUnitEndEvent { UnitId = owner.Id, StatusId = status.ClassId,
                    Delay = Math.Max(0f, gridViewTimeOverride
                        ?? (movementStartContext == null ? 0f : movementStartContext.StatusStartTime)) },
                Cast = gridViewCast
            });
        }

        private void CompleteUnitEndEvents(FightBattleStepEvent result)
        {
            if (result == null || unitEndEvents == null) return;
            foreach (var pending in unitEndEvents)
            {
                // Outer approach/pet/replay/trigger offsets settle on the actual cast object.
                pending.Event.Delay += Math.Max(0f, pending.Cast?.Delay ?? 0f);
                result.UnitEndEvents.Add(pending.Event);
            }
        }
    }
}
