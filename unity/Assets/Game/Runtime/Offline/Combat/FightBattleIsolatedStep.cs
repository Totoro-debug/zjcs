using System;
using System.Threading.Tasks;

namespace Zjcs.Combat.Official
{
    /// <summary>
    /// Runs one local step on a private checkpoint copy. The caller may adopt the completed
    /// executor only after the worker finishes; interruption discards the partial copy.
    /// Presentation-time snapshots are not resumable checkpoints.
    /// </summary>
    public sealed class FightBattleIsolatedStep : IDisposable
    {
        private readonly object sync = new object();
        private readonly FightBattleExecutor executor;
        private readonly Task<FightBattleExecutionResult> worker;
        private bool disposed, taken;

        public FightBattleImpactLatch Latch { get; }
        public bool IsCompleted => worker.IsCompleted;

        public FightBattleIsolatedStep(FightBattleCheckpoint checkpoint,
            Func<FightBattleCheckpoint, FightBattleExecutor> restore, int sequence, int counter)
        {
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            if (restore == null) throw new ArgumentNullException(nameof(restore));
            var copy = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(checkpoint));
            executor = restore(copy) ?? throw new InvalidOperationException("Restoring the isolated battle returned no executor.");
            if (!ReferenceEquals(executor.State, copy))
                throw new InvalidOperationException("The restored executor must own the isolated checkpoint.");
            Latch = new FightBattleImpactLatch();
            executor.ImpactGate = Latch;
            worker = Task.Run(() => {
                try { Latch.BindWorker(); return executor.Step(sequence, counter); }
                finally { Latch.FinishWorker(); }
            });
        }

        /// <summary>Transfers a finished step once; exceptions are rethrown on the caller.</summary>
        public bool TryTakeCompleted(out FightBattleExecutor completedExecutor, out FightBattleExecutionResult result)
        {
            lock (sync)
            {
                if (disposed) throw new ObjectDisposedException(nameof(FightBattleIsolatedStep));
                if (taken) throw new InvalidOperationException("The isolated step was already transferred.");
                if (!worker.IsCompleted) { completedExecutor = null; result = null; return false; }
                result = worker.GetAwaiter().GetResult();
                executor.ImpactGate = null;
                completedExecutor = executor;
                taken = true;
                return true;
            }
        }

        /// <summary>Cancels pending rendezvous without publishing any partially applied checkpoint.</summary>
        public void Dispose()
        {
            lock (sync)
            {
                if (disposed) return;
                disposed = true;
                Latch.Dispose();
            }
        }
    }
}
