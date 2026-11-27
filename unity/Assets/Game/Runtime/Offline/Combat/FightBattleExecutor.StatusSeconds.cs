using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
        public event Action<FightBattleUnitState, FightBattleStatusState, bool> StatusSecondsChanged;
        public event Action<FightBattleUnitState, FightBattleStatusState> StatusSecondsInvoked;
        public double NextStatusSecondsDelay => statusCatalog == null ? double.PositiveInfinity
            : FightBattleStatusSeconds.NextDelay(checkpoint);
        private sealed class PendingStatusSecondsHit
        {
            public FightBattleDamageEvent Damage;
            public bool Displacement;
            public FightBattleStatusState LaunchStatus;
        }

        private List<PendingStatusSecondsHit> statusSecondsHits;

        private void BeginStatusSecondsHits() => statusSecondsHits = new List<PendingStatusSecondsHit>();
        private void ClearStatusSecondsHits() => statusSecondsHits = null;

        private void RecordStatusSecondsHit(FightBattleUnitState actor, FightBattleUnitState target,
            FightBattleSkillDefinition skill, FightBattleHitDefinition hit, FightBattleHitResult resolved,
            FightBattleDamageEvent damage, bool displacement)
        {
            if (statusSecondsHits == null || statusCatalog == null || resolved == null || resolved.IsCure
                || resolved.Type.Dodge || resolved.Type.Immunity || resolved.Type.Blinding
                || resolved.Formula == null && resolved.Settlement == null) return;
            FightBattleStatusState launch = null;
            var application = hit.AirCombo;
            if (application != null && application.StatusId > 0 && damage.AfterHp > 0)
            {
                if (FightStatusApplicationRules.Roll(statusCatalog, formula, random, actor,
                    target, application.StatusId, application.Chance, application.AffectedByProp))
                {
                    launch = statusCatalog.CreateStatus(application.StatusId, target.Id, actor.Id, 0,
                        skill.RootEntityId > 0 ? skill.RootEntityId : skill.EntityId, skill.ItemId,
                        skill.Rank, skill.Level, skill.SubRank, actor.Properties,
                        blessPropertyScales: skill.BlessPropertyScales);
                    launch.Origin = FightBattleStatusOrigin.Resolve(skill, actor.Id).Copy();
                }
            }
            var pending = new PendingStatusSecondsHit {
                Damage = damage, Displacement = displacement, LaunchStatus = launch
            };
            if (ImpactGate is FightBattleImpactLatch live && live.ObserveLiveStatusHits)
            {
                // This worker has already waited for the actual native hit. Its
                // status clock must consume that hit now, not replay the cast's
                // original offset after the isolated step completes.
                bool needed = checkpoint.StatusSeconds != null || launch != null;
                foreach (var owner in units)
                    foreach (var status in owner.Statuses) needed |= status.Components?.Seconds != null;
                if (needed)
                {
                    var clock = new FightBattleStatusSeconds(checkpoint, statusCatalog, random);
                    clock.Enqueue(0d, damage.TargetId, true, damage.Block, displacement, launch);
                    AdvanceStatusSeconds(0d);
                }
                return;
            }
            statusSecondsHits.Add(pending);
        }

        private void CompleteStatusSecondsHits()
        {
            bool needed = checkpoint.StatusSeconds != null && checkpoint.StatusSeconds.PendingHits.Count > 0;
            foreach (var owner in units)
                foreach (var status in owner.Statuses) needed |= status.Components?.Seconds != null;
            foreach (var hit in statusSecondsHits) needed |= hit.LaunchStatus != null;
            if (!needed) return;
            var clock = new FightBattleStatusSeconds(checkpoint, statusCatalog, random);
            foreach (var hit in statusSecondsHits)
                // The same DamageEvent is offset in-place by every nested caller.
                // Read its final time only after the whole step has settled.
                clock.Enqueue(Math.Max(0d, hit.Damage.Delay), hit.Damage.TargetId, true,
                    hit.Damage.Block, hit.Displacement, hit.LaunchStatus);
        }

        /// <summary>Keep native graph time and status notifications in chronological order,
        /// including when one rendering frame crosses several hits or both phase deadlines.</summary>
        public bool AdvanceStatusSecondsWithPresentation(double seconds, Action<double> advancePresentation,
            bool paused = false)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (advancePresentation == null) throw new ArgumentNullException(nameof(advancePresentation));
            if (paused) return false;
            bool changed = false;
            double remaining = seconds;
            do
            {
                double slice = Math.Min(remaining, NextStatusSecondsDelay);
                advancePresentation(slice);
                changed |= AdvanceStatusSeconds(slice);
                remaining = Math.Max(0d, remaining - slice);
            } while (remaining > 0d);
            return changed;
        }

        /// <summary>Advance only active gameplay seconds; pause and speed are supplied by the host.</summary>
        public bool AdvanceStatusSeconds(double seconds, bool paused = false)
        {
            if (statusCatalog == null) return false;
            if (checkpoint.StatusSeconds == null) return false;
            bool changed = false;
            var clock = new FightBattleStatusSeconds(checkpoint, statusCatalog, random);
            clock.StatusChanged += (owner, status, started) => {
                if (started)
                {
                    // During a step, FightStatusLifecycle.Apply already called the
                    // installed lifecycle handler and the step owns its callback.
                    if (owner.StatusStarted == null)
                    {
                        OnStatusStarted(owner, status);
                        status.StackChanged = null;
                    }
                }
                // FightStatusLifecycle.End likewise notifies an active step itself.
                else if (owner.StatusEnded == null) OnStatusEnded(owner, status);
                StatusSecondsChanged?.Invoke(owner, status, started);
                changed = true;
            };
            clock.StatusInvoked += (owner, status) => StatusSecondsInvoked?.Invoke(owner, status);
            clock.Advance(seconds, paused);
            if (changed) RebuildBattleProperties();
            checkpoint.RandomSeed = random.Seed;
            return changed;
        }
    }
}
