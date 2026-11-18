using System;
using System.Collections.Generic;
using Zjcs.Combat.Official;
using Zjcs.Official.StoryPlayables;

namespace Zjcs.Presentation.Combat
{
    /// <summary>
    /// Coordinates one isolated rule step with the original cast, hit and damage views.
    /// Use from one presentation thread. The host decides when a prepared cast actually
    /// starts and advances this clock with the same scaled time as native presentation.
    /// </summary>
    public sealed class OfficialBattleLiveImpactSession : IDisposable
    {
        public delegate bool StartCast(FightBattleSkillCastEvent cast, out EcEntityIdentifier skillId);

        private sealed class ActiveCast
        {
            public EcEntityIdentifier Id;
            public float Started;
        }

        private readonly FightBattleIsolatedStep step;
        private readonly OfficialBattleSkillEntityPlayback views;
        private readonly OfficialBattleImpactViewBridge bridge;
        private readonly Dictionary<FightBattleSkillCastEvent, ActiveCast> casts =
            new Dictionary<FightBattleSkillCastEvent, ActiveCast>();
        private FightBattlePreparedCast preparation;
        private FightBattleSelectedHit selection;
        private FightBattleSettledImpact settlement;
        private FightBattleCompletedCast completion;
        private float clock;
        private bool disposed, transferred, pumping;

        public bool IsCompleted => step.IsCompleted;
        public FightBattleSettledImpact PendingSettlement => settlement;

        public OfficialBattleLiveImpactSession(FightBattleCheckpoint checkpoint,
            Func<FightBattleCheckpoint, FightBattleExecutor> restore,
            OfficialBattleSkillEntityPlayback views, int sequence, int counter)
        {
            this.views = views ?? throw new ArgumentNullException(nameof(views));
            step = new FightBattleIsolatedStep(checkpoint, restore, sequence, counter);
            bridge = new OfficialBattleImpactViewBridge(views, step.Latch);
        }

        /// <summary>
        /// Processes available handoffs without blocking the presentation thread. If the
        /// start callback declines, the prepared cast remains held until a later pump.
        /// If a damage result is incomplete, it remains held for the host to handle.
        /// </summary>
        public bool Pump(float elapsed, StartCast startCast, Action<FightBattleDamageEvent> afterDamageStarted = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleLiveImpactSession));
            if (transferred) throw new InvalidOperationException("The live step was already transferred.");
            if (float.IsNaN(elapsed) || float.IsInfinity(elapsed) || elapsed < 0f)
                throw new ArgumentOutOfRangeException(nameof(elapsed));
            if (startCast == null) throw new ArgumentNullException(nameof(startCast));
            if (pumping) throw new InvalidOperationException("The live step is already being pumped.");
            pumping = true;
            try
            {
                clock += elapsed;
                if (float.IsInfinity(clock)) throw new InvalidOperationException("The live presentation clock overflowed.");
                bool progressed = false;
                // These requests are diagnostic only; arrivals are keyed independently and
                // can precede a target request. Do not keep completed requests indefinitely.
                while (step.Latch.TryTake(out _)) { }
                if (preparation == null) step.Latch.TryTakePreparation(out preparation);
                if (preparation != null)
                {
                    if (!startCast(preparation.Cast, out var id)) return progressed;
                    casts.Add(preparation.Cast, new ActiveCast { Id = id, Started = clock });
                    step.Latch.CompletePreparation(preparation);
                    preparation = null; progressed = true;
                }
                if (selection == null) step.Latch.TryTakeSelection(out selection);
                if (selection != null)
                {
                    var active = Find(selection.Cast);
                    bridge.QueueSelection(selection, active.Id, clock - active.Started);
                    selection = null; progressed = true;
                }
                if (settlement == null) step.Latch.TryTakeSettlement(out settlement);
                if (settlement != null)
                {
                    var active = Find(settlement.Impact.Cast);
                    if (!bridge.TryQueueSettlement(settlement, active.Id, afterDamageStarted)) return progressed;
                    settlement = null; progressed = true;
                }
                if (completion == null) step.Latch.TryTakeCastCompletion(out completion);
                if (completion != null)
                {
                    var active = Find(completion.Cast);
                    views.CompleteLiveCast(completion.Cast, active.Id, clock - active.Started);
                    step.Latch.CompleteCast(completion);
                    casts.Remove(completion.Cast);
                    completion = null; progressed = true;
                }
                return progressed;
            }
            finally { pumping = false; }
        }

        /// <summary>Lets the host deliver an unsupported result while the worker remains held.</summary>
        public bool TryCompletePendingSettlement(Func<FightBattlePendingImpact, FightBattleDamageEvent, bool> deliver)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleLiveImpactSession));
            if (transferred) throw new InvalidOperationException("The live step was already transferred.");
            if (pumping) throw new InvalidOperationException("The live step is already being pumped.");
            if (deliver == null) throw new ArgumentNullException(nameof(deliver));
            if (settlement == null) return false;
            pumping = true;
            try
            {
                if (!step.Latch.TryCompleteSettlement(settlement, deliver)) return false;
                settlement = null;
                return true;
            }
            finally { pumping = false; }
        }

        private ActiveCast Find(FightBattleSkillCastEvent cast)
        {
            if (cast == null || !casts.TryGetValue(cast, out var active))
                throw new InvalidOperationException("The live event has no started original cast.");
            return active;
        }

        public bool TryTakeCompleted(out FightBattleExecutor executor, out FightBattleExecutionResult result)
        {
            if (disposed) throw new ObjectDisposedException(nameof(OfficialBattleLiveImpactSession));
            if (transferred) throw new InvalidOperationException("The live step was already transferred.");
            if (pumping) throw new InvalidOperationException("The live step is already being pumped.");
            if (!step.TryTakeCompleted(out executor, out result)) return false;
            bridge.Dispose();
            transferred = true;
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            bridge.Dispose();
            step.Dispose();
            casts.Clear();
        }
    }
}
