using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class OfflineFruitTreeProgress
    {
        public int Version = 1;
        public bool ProductionRepaired;
        public bool PetFacilityRepaired;
        public bool InitialRewardGranted;
        public long StoredCount;
        public double FractionalCount;
        public long LastUpdateUnixSeconds;
        public long TotalCollected;
        public long CollectionCount;
        public bool ProductionRepairStorySeen;
        public bool PetRepairStorySeen;
        public bool CollectionTutorialSeen;
        public List<OfflineFruitTreeOutputAddition> OutputAdditions = new List<OfflineFruitTreeOutputAddition>();

        public static void Normalize(OfflineFruitTreeProgress state)
        {
            if (state == null) return;
            if (state.OutputAdditions == null) state.OutputAdditions = new List<OfflineFruitTreeOutputAddition>();
        }

        public static bool IsValid(OfflineFruitTreeProgress state)
        {
            if (state == null || state.Version != 1 || state.StoredCount < 0
                || double.IsNaN(state.FractionalCount) || double.IsInfinity(state.FractionalCount)
                || state.FractionalCount < 0 || state.FractionalCount >= 1
                || state.LastUpdateUnixSeconds < 0 || state.TotalCollected < 0 || state.CollectionCount < 0
                || state.OutputAdditions == null || state.OutputAdditions.Count > 108)
                return false;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (OfflineFruitTreeOutputAddition addition in state.OutputAdditions)
                if (addition == null || string.IsNullOrWhiteSpace(addition.Id) || addition.Id.Length > 128
                    || addition.BeginUnixSeconds < 0 || addition.EndUnixSeconds < 0
                    || addition.EndUnixSeconds > 0 && addition.EndUnixSeconds < addition.BeginUnixSeconds
                    || addition.SpeedPercent < -10000 || addition.CapacityPercent < -10000
                    || !ids.Add(addition.Id)) return false;
            return true;
        }
    }

    [Serializable]
    public sealed class OfflineFruitTreeOutputAddition
    {
        public string Id;
        public long BeginUnixSeconds;
        public long EndUnixSeconds;
        public int SpeedPercent;
        public int CapacityPercent;
        public bool Disabled;
    }
}
