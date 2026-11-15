using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        private static void DelayPortableFlameUntilMovementEnd(
            IList<FightBattleStatusSkillInvocation> invocations, FightBattleMovementEvent movement,
            IList<FightBattleMovementEvent> movements)
        {
            if (movement?.Path == null || movement.Path.Count < 2) return;
            bool waiting = false;
            foreach (var invocation in invocations) waiting |= invocation.WaitForMovementEnd;
            if (!waiting) return;
            // Use the same per-unit path clock as rule duration and native movement
            // playback. The original skill's action/entity tail is not travel time.
            var ends = new Dictionary<int, float>();
            foreach (var current in movements)
            {
                if (current?.Path == null || current.Path.Count < 2) continue;
                ends.TryGetValue(current.UnitId, out float previousEnd);
                float start = (float)FightOrdinaryMovementTiming.ActionStartDelay(current, previousEnd);
                float end = start + MovementDuration(current);
                ends[current.UnitId] = end;
                if (!ReferenceEquals(current, movement)) continue;
                foreach (var invocation in invocations)
                    if (invocation.WaitForMovementEnd) invocation.Delay += Math.Max(0f, end);
                return;
            }
            throw new InvalidOperationException("A fire-ring movement trigger must belong to the current step's settled paths.");
        }
    }
}
