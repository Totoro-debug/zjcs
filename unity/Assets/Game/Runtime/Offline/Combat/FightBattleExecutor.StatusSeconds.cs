using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed partial class FightBattleExecutor
    {
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
                double rate = statusCatalog.CalculateStatusApplicationRate(actor, target,
                    application.StatusId, application.Chance, application.AffectedByProp, formula);
                if (random.Next() <= rate)
                {
                    launch = statusCatalog.CreateStatus(application.StatusId, target.Id, actor.Id, 0,
                        skill.RootEntityId > 0 ? skill.RootEntityId : skill.EntityId, skill.ItemId,
                        skill.Rank, skill.Level, skill.SubRank, actor.Properties,
                        blessPropertyScales: skill.BlessPropertyScales);
                    launch.Origin = FightBattleStatusOrigin.Resolve(skill, actor.Id).Copy();
                }
            }
            statusSecondsHits.Add(new PendingStatusSecondsHit {
                Damage = damage, Displacement = displacement, LaunchStatus = launch
            });
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
                    OnStatusStarted(owner, status);
                    // These callbacks also serve Step, whose finally normally
                    // releases the checkpoint's temporary executor references.
                    // A seconds tick runs outside that scope.
                    status.StackChanged = null;
                }
                else OnStatusEnded(owner, status);
                changed = true;
            };
            clock.Advance(seconds, paused);
            if (changed) RebuildBattleProperties();
            checkpoint.RandomSeed = random.Seed;
            return changed;
        }
    }
}
