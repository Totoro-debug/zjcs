using System;
using System.Collections.Generic;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    [Serializable]
    public sealed class OfflinePartnerAchievementRecord
    {
        public int GroupId;
        public int CanRewardsMaxIndex = -1;
        public int GotRewardMaxIndex = -1;

        public static bool IsValid(List<OfflinePartnerAchievementRecord> records)
            => records != null && records.Count <= 4095 &&
                records.All(value => value != null && value.GroupId > 0 && value.GroupId <= 4095 &&
                    value.CanRewardsMaxIndex >= -1 && value.CanRewardsMaxIndex <= 1048575 &&
                    value.GotRewardMaxIndex >= -1 && value.GotRewardMaxIndex <= 1048575) &&
                records.Select(value => value.GroupId).Distinct().Count() == records.Count;
    }
}
