using System;
using System.Collections.Generic;

namespace Zjcs.Offline.State
{
    // Callers use one integer time unit throughout a range (seconds or ticks).
    // This shared offline policy splits time only; original numeric rules stay
    // with each income owner.
    public static class OfflineIncomeTimeSegments
    {
        public static IEnumerable<(long Start, long End)> Split(long from, long until,
            IEnumerable<long> changes)
        {
            if (changes == null) throw new ArgumentNullException(nameof(changes));
            if (from >= until) yield break;
            var boundaries = new SortedSet<long> { from, until };
            foreach (long change in changes)
                if (change > from && change < until) boundaries.Add(change);
            long previous = from;
            foreach (long point in boundaries)
            {
                if (point > previous) yield return (previous, point);
                previous = point;
            }
        }
    }
}
