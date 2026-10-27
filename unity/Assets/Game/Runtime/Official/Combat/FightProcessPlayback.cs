using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    /// <summary>
    /// Local-clock port of NormalPlaybackStrategy.PlayRound: consume StepList in its original
    /// order, waiting max(0, step.Time - elapsed). Payloads are dispatched by the owning session.
    /// This consumes events; it does not implement the missing server event generator.
    /// </summary>
    public sealed class FightProcessPlayback<T>
    {
        private readonly IReadOnlyList<T> steps;
        private readonly Func<T, double> time;
        private readonly Action<T> dispatch;
        private bool ticking;

        public double Elapsed { get; private set; }
        public int NextStep { get; private set; }
        public bool Cancelled { get; private set; }
        public bool Complete => Cancelled || NextStep >= steps.Count;

        public FightProcessPlayback(IReadOnlyList<T> steps, Func<T, double> time,
            Action<T> dispatch, int startStep = 0)
        {
            this.steps = steps ?? throw new ArgumentNullException(nameof(steps));
            this.time = time ?? throw new ArgumentNullException(nameof(time));
            this.dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
            if (startStep < 0 || startStep > steps.Count) throw new ArgumentOutOfRangeException(nameof(startStep));
            for (int i = startStep; i < steps.Count; i++)
            {
                double value = time(steps[i]);
                if (double.IsNaN(value) || double.IsInfinity(value))
                    throw new ArgumentException("Fight step time must be finite.", nameof(steps));
            }
            NextStep = startStep;
        }

        public void Tick(double scaledDelta)
        {
            if (double.IsNaN(scaledDelta) || double.IsInfinity(scaledDelta) || scaledDelta < 0)
                throw new ArgumentOutOfRangeException(nameof(scaledDelta));
            if (ticking) throw new InvalidOperationException("Fight playback cannot dispatch recursively.");
            if (Complete) return;
            ticking = true;
            try
            {
                Elapsed += scaledDelta;
                while (!Complete && Math.Max(0, time(steps[NextStep]) - Elapsed) <= 0)
                {
                    T step = steps[NextStep++];
                    dispatch(step);
                }
            }
            finally { ticking = false; }
        }

        public void Cancel() => Cancelled = true;
    }
}
