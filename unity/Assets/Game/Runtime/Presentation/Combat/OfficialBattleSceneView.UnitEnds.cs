using System;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleSceneView
    {
        private OfficialBattleUnitEndPlayback unitEndPlayback;
        private bool unitEndGraphClock;

        private OfficialBattleUnitEndPlayback UnitEndPlayback => unitEndPlayback
            ?? (unitEndPlayback = new OfficialBattleUnitEndPlayback(RemoveEndedUnit,
                id => Body(id) != null || UnitSpawns.IsPending(id)));

        internal void BindUnitEndClock(OfficialBattleSkillGraphPlayback playback)
        {
            if (playback == null) return;
            playback.BindUnitEnds(UnitEndPlayback);
            playback.BindUnitSpawns(UnitSpawns);
            unitEndGraphClock = true;
        }

        private void AdvanceUnitLifecycles(float elapsed)
        {
            while (true)
            {
                float next = Math.Min(unitSpawns?.NextDelay ?? float.PositiveInfinity,
                    unitEndPlayback?.NextDelay ?? float.PositiveInfinity);
                if (next > elapsed) break;
                unitSpawns?.AdvanceTimers(next);
                unitEndPlayback?.AdvanceTimers(next);
                elapsed -= next;
                unitSpawns?.DispatchDue();
                unitEndPlayback?.DispatchDue();
            }
            unitSpawns?.AdvanceTimers(elapsed);
            unitEndPlayback?.AdvanceTimers(elapsed);
        }

        private void RemoveEndedUnit(int id)
        {
            RemoveUnit(id);
            units.Remove(id);
            motions.Remove(id);
            if (attachedUnitHuds.TryGetValue(id, out var hud)) hud?.Dispose();
            attachedUnitHuds.Remove(id);
            if (attachedAnimators.TryGetValue(id, out var animator)) ReleaseAttachedAnimator(animator);
            attachedAnimators.Remove(id);
        }
    }
}
