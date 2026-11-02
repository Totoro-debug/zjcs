using System;

namespace Zjcs.Offline.ProfessionUI
{
    // Independent planar cubic construction. Public-call numerical observations
    // of the original player animation identify ten equal parameter intervals.
    public sealed class ProfessionNativeBezierPolyline
    {
        private readonly float[] times = new float[11], values = new float[11];

        public ProfessionNativeBezierPolyline(float startTime, float startValue, float firstTime, float firstValue,
            float secondTime, float secondValue, float endTime, float endValue)
        {
            foreach (float value in new[] { startTime, startValue, firstTime, firstValue, secondTime, secondValue, endTime, endValue })
                Finite(value);
            if (endTime <= startTime) throw new ArgumentException("Original Bezier interval must increase.");
            for (int index = 0; index <= 10; index++)
            {
                double t = index / 10d, u = 1 - t;
                times[index] = (float)(u * u * u * startTime + 3 * u * u * t * firstTime
                    + 3 * u * t * t * secondTime + t * t * t * endTime);
                values[index] = (float)(u * u * u * startValue + 3 * u * u * t * firstValue
                    + 3 * u * t * t * secondValue + t * t * t * endValue);
            }
        }

        public float Evaluate(float time)
        {
            Finite(time);
            if (time <= times[0]) return values[0];
            if (time >= times[10]) return values[10];
            for (int index = 1; index <= 10; index++)
            {
                if (time > times[index]) continue;
                float span = times[index] - times[index - 1];
                if (span <= 0) continue;
                return values[index - 1] + (values[index] - values[index - 1]) * ((time - times[index - 1]) / span);
            }
            throw new InvalidOperationException("Original Bezier time has no polyline segment.");
        }

        private static void Finite(float value)
        { if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Original Bezier values must be finite."); }
    }
}
