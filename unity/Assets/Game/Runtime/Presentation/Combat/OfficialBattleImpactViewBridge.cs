using System;
using Zjcs.Combat.Official;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Forwards the original receipt-backed hit-view arrivals to one local step.</summary>
    public sealed class OfficialBattleImpactViewBridge : IDisposable
    {
        private readonly OfficialBattleSkillEntityPlayback views;
        private readonly FightBattleImpactLatch latch;
        private bool disposed;

        public event Action<long> WorkerReleasedByArrival;

        public OfficialBattleImpactViewBridge(OfficialBattleSkillEntityPlayback views, FightBattleImpactLatch latch)
        {
            this.views = views ?? throw new ArgumentNullException(nameof(views));
            this.latch = latch ?? throw new ArgumentNullException(nameof(latch));
            views.HitSelectionViewArrived += OnArrival;
        }

        /// <summary>
        /// Starts the selected hit view while the executor is held; an immediate original
        /// arrival can safely be recorded before the worker continues.
        /// </summary>
        public long QueueSelection(FightBattleSelectedHit selected, EcEntityIdentifier skillId, float elapsedSinceCast)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleImpactViewBridge));
            latch.CompleteSelection(selected, (cast, selection) =>
                views.QueueSelectedHit(cast, skillId, selection, elapsedSinceCast), out long revision);
            return revision;
        }

        /// <summary>
        /// Returns false without releasing an incomplete or unsupported damage result.
        /// The optional callback can refresh HP while the executor is still suspended.
        /// </summary>
        public bool TryQueueSettlement(FightBattleSettledImpact settlement, EcEntityIdentifier skillId,
            Action<FightBattleDamageEvent> afterDamageStarted = null)
            => TryQueueSettlement(settlement, skillId, out _, afterDamageStarted);

        public bool TryQueueSettlement(FightBattleSettledImpact settlement, EcEntityIdentifier skillId,
            out long releasedRevision, Action<FightBattleDamageEvent> afterDamageStarted = null,
            Func<FightBattleDamageEvent, bool> canStartDamage = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleImpactViewBridge));
            return latch.TryCompleteSettlement(settlement, (impact, damage) => {
                if (canStartDamage != null && !canStartDamage(damage)) return false;
                if (!views.QueueSettledDamage(impact.Cast, skillId, damage)) return false;
                afterDamageStarted?.Invoke(damage);
                return true;
            }, out releasedRevision);
        }

        internal bool TryQueueUnresolved(FightBattleSettledImpact settlement, EcEntityIdentifier skillId,
            Action<FightBattleDamageEvent> afterDamageStarted, Func<FightBattleDamageEvent, bool> canStartDamage)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleImpactViewBridge));
            var impact = settlement?.Impact;
            var damage = settlement?.Damage;
            if (impact == null || damage == null || damage.HasDamageTriggerStatus
                || canStartDamage != null && !canStartDamage(damage)) return false;
            if (!views.QueueUnresolvedDamage(impact.Cast, skillId, damage)) return false;
            afterDamageStarted?.Invoke(damage);
            return true;
        }

        internal bool TryResolveStatus(FightBattleSettledImpact settlement, out long releasedRevision)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleImpactViewBridge));
            return latch.TryCompleteSettlement(settlement,
                (impact, damage) => views.ResolveDamageStatus(damage), out releasedRevision);
        }

        private void OnArrival(FightBattleSkillCastEvent cast, FightBattleHitSelectionSnapshot selection,
            int centerIndex, Pos2d position, int scopeIndex)
            => RecordArrival(cast, selection, centerIndex, scopeIndex);

        internal void RecordArrival(FightBattleSkillCastEvent cast, FightBattleHitSelectionSnapshot selection,
            int centerIndex, int scopeIndex)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleImpactViewBridge));
            latch.RecordArrival(cast, selection, centerIndex, scopeIndex, out long revision);
            if (revision > 0) WorkerReleasedByArrival?.Invoke(revision);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            views.HitSelectionViewArrived -= OnArrival;
        }
    }
}
