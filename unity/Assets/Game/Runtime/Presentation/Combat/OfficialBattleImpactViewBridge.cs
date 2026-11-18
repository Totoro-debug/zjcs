using System;
using Zjcs.Combat.Official;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Forwards the original receipt-backed hit-view arrivals to one local step.</summary>
    public sealed class OfficialBattleImpactViewBridge : IDisposable
    {
        private readonly OfficialBattleSkillEntityPlayback views;
        private readonly FightBattleImpactLatch latch;
        private bool disposed;

        public OfficialBattleImpactViewBridge(OfficialBattleSkillEntityPlayback views, FightBattleImpactLatch latch)
        {
            this.views = views ?? throw new ArgumentNullException(nameof(views));
            this.latch = latch ?? throw new ArgumentNullException(nameof(latch));
            views.HitSelectionViewArrived += OnArrival;
        }

        private void OnArrival(FightBattleSkillCastEvent cast, FightBattleHitSelectionSnapshot selection,
            int centerIndex, Pos2d position, int scopeIndex)
        {
            latch.RecordArrival(cast, selection, centerIndex, scopeIndex);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            views.HitSelectionViewArrived -= OnArrival;
        }
    }
}
