using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.World.NativeMap14Labels
{
    // Explicit ports for the original map/player/config services used by these components.
    public interface IMap14LabelServices
    {
        int MapClassId { get; }
        bool IsMapInitialized { get; }
        event Action Initialized;
        IMap14LabelPlayer Player { get; }
        bool IsTeamMember { get; }
        int QueryRegion(Vector3 worldPosition);
        MapRegionInfo GetRegion(int mapClassId, int region);
        string GetRegionNameKey(int mapClassId, int region);
        object CompactNumber(long value);
        int CalcWorldMapRegionProgress(int mapClassId, int region, int progress);
        int CalcRankSuppression(int mySubRank, int otherSubRank);
        bool TryGetLevelColor(CombatRateingColorType type, int level, out Color color);
        DateTime Now { get; }
        double CalcRemainSecond(DateTime endTime);
        object FormatTimeSpan(TimeSpan span, int format);
    }

    public interface IMap14LabelPlayer
    {
        int Level { get; }
        int SubRank { get; }
        long CombatRating { get; }
        event Action<int> OnLevelUpdate;
        event Action<int> OnPlanesRankUnlock;
        event Action<long, long> OnCombatRatingChanged;
        event Action<int, List<int>> OnWorldMapProgressUpdate;
        event Action<int, int, MapRegionStatus, MapRegionStatus> OnMapStatusUpdate;
        event Action<long> OnDebugTimeOffset;
        bool RegionIsActived(int mapClassId, int region);
        MapRegionStatus GetMapRegionStatus(int mapClassId, int region);
        int GetWorldMapRegionProgress(int mapClassId, int region);
    }

    public enum MapRegionStatus { None, Private, Public }

    // The original MapRegionInfo fields/methods reachable in Map14 world labels.
    [Serializable]
    public sealed class MapRegionInfo
    {
        public int ClassId, Region, SubRank, Level, LimitLevel;
        public int LimitServerRank, LimitServerRankUnlockDayOffset, ReleaseTimeId, ReleaseTimeOffset;
        public MapRegionStrengthDisplayType StrengthDisplayType;
        public List<long> CombatRatingLevelValues;
        // Unity JSON represents a null List as empty; retain the original Luban null branch explicitly.
        public bool HasCombatRatingLevelValues;
        public int RecommendCombatRatingIndex;
        public long CombatRating => HasCombatRatingLevelValues ? CombatRatingLevelValues[RecommendCombatRatingIndex] : 0L;
        public void ValidateMap14Scope()
        {
            if (ClassId != 14 || LimitServerRank != 0 || LimitServerRankUnlockDayOffset != 0 || ReleaseTimeId != 0 || ReleaseTimeOffset != 0)
                throw new InvalidOperationException("Bind the original timed/rank region services before expanding the proven Map14 label scope.");
        }
        public bool ExistLimit() { ValidateMap14Scope(); return LimitLevel > 0; }
        public bool IsLimited(int level) => IsLimitedByType(level) != MapRegionLimitType.None;
        public MapRegionLimitType IsLimitedByType(int level)
        {
            ValidateMap14Scope();
            return level < LimitLevel ? MapRegionLimitType.Level : MapRegionLimitType.None;
        }
    }

    public static class Map14LabelFormula
    {
        // Original FormulaHandler.GetCombatRatingLevel, including equality at a threshold.
        public static int GetCombatRatingLevel(long combatRating, IReadOnlyList<long> combatRatingLevelValues)
        {
            if (combatRatingLevelValues == null || combatRatingLevelValues.Count == 0) return 0;
            int i = 0;
            int count;
            for (count = combatRatingLevelValues.Count; i < count && combatRating >= combatRatingLevelValues[i]; i++) { }
            return count - i;
        }
    }
}
