using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleSceneView
    {
        private OfficialBattleUnitEndPlayback unitEndPlayback;
        private bool unitEndGraphClock;

        private OfficialBattleUnitEndPlayback UnitEndPlayback => unitEndPlayback
            ?? (unitEndPlayback = new OfficialBattleUnitEndPlayback(RemoveEndedUnit, id => Body(id) != null));

        internal void BindUnitEndClock(OfficialBattleSkillGraphPlayback playback)
        {
            if (playback == null) return;
            playback.BindUnitEnds(UnitEndPlayback);
            unitEndGraphClock = true;
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
