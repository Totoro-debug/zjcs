using System;
using System.Collections.Generic;

namespace Zjcs.Official.World.NativeMap13Labels
{
    // Original MapRegionInfo fields/methods consumed by world labels. The decoded
    // Luban constructor remains the source of all field values in the local data.
    [Serializable]
    public sealed class MapRegionInfo
    {
        public int ClassId, Region, Level, LimitLevel;
        public SubRank SubRank;
        public Rank LimitServerRank;
        public int LimitServerRankUnlockDayOffset, ReleaseTimeId, ReleaseTimeOffset;
        public MapRegionStrengthDisplayType StrengthDisplayType;
        public List<long> CombatRatingLevelValues;
        // JsonUtility changes null lists to empty lists. Keep the original null branch.
        public bool HasCombatRatingLevelValues;
        public int RecommendCombatRatingIndex;
        public long CombatRating
        {
            get
            {
                if (HasCombatRatingLevelValues)
                    return CombatRatingLevelValues[RecommendCombatRatingIndex];
                return 0L;
            }
        }
        public void ValidateScope()
        {
            if (ClassId != 13 || LimitServerRank != Rank.None || LimitServerRankUnlockDayOffset != 0 || ReleaseTimeId != 0 || ReleaseTimeOffset != 0)
                throw new InvalidOperationException("Restore the original rank/date services before expanding verified Map13 label scope.");
        }
        public bool ExistLimit()
        {
            ValidateScope();
            if (LimitLevel <= 0 && LimitServerRank == Rank.None) return ExistReleaseTimeLimit();
            return true;
        }
        public bool ExistReleaseTimeLimit() => ReleaseTimeId > 0;
        public bool IsLimited(int level) => IsLimitedByType(level) != MapRegionLimitType.None;
        public MapRegionLimitType IsLimitedByType(int level)
        {
            ValidateScope();
            // The original IsUnlockTimeReached always returns true for the
            // verified zero server-rank/date fields of every Map13 region.
            if (level < LimitLevel) return MapRegionLimitType.Level;
            return MapRegionLimitType.None;
        }
    }
}
