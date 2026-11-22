using System.Collections.Generic;

namespace Zjcs.Offline.Home
{
    // A read-only view of saved activity data for one local settlement. UTC ticks
    // keep activity boundaries and subsecond collection in the same time unit.
    public interface IHomeIncomeAdditionSnapshot
    {
        bool HasRecords { get; }
        IReadOnlyList<long> ChangeTicks { get; }
        int AddPercent(int hangUpId, long utcTicks);
        long ExtraCapacity(int hangUpId, long capacity, long utcTicks);
        long ExtraCount(int hangUpId, double count, long utcTicks);
    }
}
