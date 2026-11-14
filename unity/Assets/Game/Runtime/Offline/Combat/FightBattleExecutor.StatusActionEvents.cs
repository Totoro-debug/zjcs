using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private sealed class PendingStatusActionEvent
        {
            public FightBattleStatusCustomActionEvent Event;
            public FightBattleSkillCastEvent Cast;
            public bool Added;
        }

        private List<PendingStatusActionEvent> statusActionEvents;

        private void BeginStatusActionEvents()
            => statusActionEvents = new List<PendingStatusActionEvent>();

        private void OnCooldownActionTriggered(FightBattleUnitState owner)
            => RecordStatusAction(new FightBattleStatusCustomActionEvent {
                UnitId = owner.Id, Action = 3,
                SourceDamageId = statusApplicationDamage?.DamageId ?? 0,
                SourceDamageTargetId = statusApplicationDamage?.TargetId ?? 0
            });

        private void RecordStatusAction(FightBattleStatusCustomActionEvent value)
        {
            if (statusActionEvents != null)
            {
                value.HasDelay = true;
                value.Delay = Math.Max(0f, gridViewTimeOverride ?? movementStartContext?.StatusStartTime ?? 0f);
                statusActionEvents.Add(new PendingStatusActionEvent {
                    Event = value, Cast = gridViewCast, Added = currentStepStatusCustomActions != null
                });
            }
            currentStepStatusCustomActions?.Add(value);
        }

        private void CompleteStatusActionEvents(FightBattleStepEvent step)
        {
            int early = 0;
            foreach (var pending in statusActionEvents)
            {
                // Casts acquire their approach/replay/trigger offsets after execution.
                pending.Event.Delay += Math.Max(0f, pending.Cast?.Delay ?? 0f);
                if (!pending.Added) step.StatusCustomActions.Insert(early++, pending.Event);
            }
        }
    }
}
