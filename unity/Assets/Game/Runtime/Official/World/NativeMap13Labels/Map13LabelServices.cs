using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.World.NativeMap13Labels
{
    // Explicit inputs for the original map/player/clock singletons. Bind before activation.
    public interface IMap13LabelServices
    {
        int MapClassId { get; }
        object Map { get; }
        event Action Initialized;
        IMap13LabelPlayer Player { get; }
        bool IsTeamMember { get; }
        int QueryRegion(Vector3 position);
        MapRegionInfo GetRegion(int mapClassId, int region);
        Map13LabelData Data { get; }
        CombatRatingConfig Colors { get; }
        DateTime Now { get; }
        double CalcRemainSecond(DateTime endTime);
    }

    public interface IMap13LabelPlayer
    {
        int Level { get; }
        SubRank SubRank { get; }
        long CombatRating { get; }
        event Action<int> OnLevelUpdate;
        event Action<Rank> OnPlanesRankUnlock;
        event Action<long, long> OnCombatRatingChanged;
        event Action<int, List<int>> OnWorldMapProgressUpdate;
        event Action<int, int, MapRegionStatus, MapRegionStatus> OnMapStatusUpdate;
        event Action<long> OnDebugTimeOffset;
        bool RegionIsActived(int mapClassId, int region);
        MapRegionStatus GetMapRegionStatus(int mapClassId, int region);
        int GetWorldMapRegionProgress(int mapClassId, int region);
    }
}
