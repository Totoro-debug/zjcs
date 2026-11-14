using System;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleSceneView
    {
        private OfficialBattleUnitEndPlayback unitEndPlayback;
        private OfficialBattleUnitLifePlayback unitLifePlayback;
        private OfficialBattleUnitLifePlayback UnitLifePlayback => unitLifePlayback
            ?? (unitLifePlayback = new OfficialBattleUnitLifePlayback(ApplyUnitLife));
        private bool unitEndGraphClock;

        private OfficialBattleUnitEndPlayback UnitEndPlayback => unitEndPlayback
            ?? (unitEndPlayback = new OfficialBattleUnitEndPlayback(RemoveEndedUnit,
                id => Body(id) != null || UnitSpawns.IsPending(id)));

        internal void BindUnitEndClock(OfficialBattleSkillGraphPlayback playback)
        {
            if (playback == null) return;
            playback.BindUnitEnds(UnitEndPlayback);
            playback.BindUnitSpawns(UnitSpawns);
            playback.BindUnitLife(UnitLifePlayback);
            unitEndGraphClock = true;
        }

        private void AdvanceUnitLifecycles(float elapsed)
        {
            while (true)
            {
                float next = Math.Min(unitSpawns?.NextDelay ?? float.PositiveInfinity,
                    Math.Min(unitLifePlayback?.NextDelay ?? float.PositiveInfinity,
                        unitEndPlayback?.NextDelay ?? float.PositiveInfinity));
                if (next > elapsed) break;
                unitSpawns?.AdvanceTimers(next);
                unitLifePlayback?.AdvanceTimers(next);
                unitEndPlayback?.AdvanceTimers(next);
                elapsed -= next;
                unitSpawns?.DispatchDue();
                unitLifePlayback?.DispatchDue();
                unitEndPlayback?.DispatchDue();
            }
            unitSpawns?.AdvanceTimers(elapsed);
            unitLifePlayback?.AdvanceTimers(elapsed);
            unitEndPlayback?.AdvanceTimers(elapsed);
        }

        private void RemoveEndedUnit(int id)
        {
            unitLifePlayback?.Cancel(id);
            RemoveUnit(id);
            units.Remove(id);
            motions.Remove(id);
            if (attachedUnitHuds.TryGetValue(id, out var hud)) hud?.Dispose();
            attachedUnitHuds.Remove(id);
            if (attachedAnimators.TryGetValue(id, out var animator)) ReleaseAttachedAnimator(animator);
            attachedAnimators.Remove(id);
        }

        private void ApplyUnitLife(FightBattleUnitLifeEvent receipt)
        {
            var settled = latestCheckpoint?.Units.Find(value => value != null && value.Id == receipt.UnitId);
            if (settled == null || UnitSpawns.IsPending(receipt.UnitId)) return;
            var state = settled.WithPresentationHealth(receipt.Hp, receipt.MaxHp);
            if (receipt.IsDead)
            {
                NotifySourceDeath(state.Id);
                if (attachedTransforms.TryGetValue(state.Id, out var attached) && attached != null)
                    TryBeginAttachedDeath(state, attached);
                else TryBeginDeath(state);
                return;
            }
            notifiedSourceDeaths.Remove(state.Id);
            if (attachedTransforms.TryGetValue(state.Id, out var body) && body != null)
            {
                var animator = GetOrCreateAttachedAnimator(state, body);
                if (animator != null)
                {
                    animator.Dying = false;
                    animator.DeathFinished = false;
                    PlayAttachedAnimation(animator, "idle_01", true);
                }
                RefreshAttachedHud(state, body);
                return;
            }
            var view = GetOrCreate(state);
            if (view == null) return;
            view.State = state;
            view.Dying = false;
            PlayAnimation(view, "idle_01", true);
            view.Hud?.Refresh(state, latestCheckpoint.Units);
            var transform = Body(state.Id);
            if (transform != null) { PlaceUnit(state.Id, transform, state, null); orientUnit(transform, state); }
        }
    }
}
