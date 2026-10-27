using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Zjcs.Official.World;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.World
{
    [Serializable] public sealed class OfficialMap12BuildingData
    {
        public string manifest;
        public int mapClassId;
        public OfficialMap12Building[] Buildings;
        public Map10RuntimeTalkConditionInfo[] Conditions;
        public OfficialExploreRewardRule[] MountLiftAwards;
        public Map12BuildingRewardNumber[] RewardNumbers;
        public Map12BuildingRewardItem[] RewardItems;
        public OfficialMap11UiText[] Texts;

        // The selected stage has two unconditional fixed entries. Keep the
        // original float scale multiplication and integer preview conversion.
        public IReadOnlyDictionary<int, long> MountLiftRewardPreview(int level, int subRank)
        {
            var output = new Dictionary<int, long>();
            foreach (var rule in MountLiftAwards)
            {
                if (rule.Percent != 1 || rule.IdList.Length != 1 || rule.WeightList.Length != 1
                    || rule.WeightList[0] <= 0)
                    throw new InvalidDataException("MountLift requires a different original reward evaluator.");
                float amount = rule.CountList[0];
                float Scale(int id, int index)
                {
                    double[] numbers = RewardNumbers.Single(v => v.Id == id).Nums;
                    return (float)numbers[Math.Max(0, Math.Min(index, numbers.Length - 1))];
                }
                if (rule.LevelCountId?.Length > 0) amount *= Scale(rule.LevelCountId[0], Math.Max(0, level));
                if (rule.SubRankCountId?.Length > 0) amount *= Scale(rule.SubRankCountId[0], subRank);
                output[rule.IdList[0]] = output.GetValueOrDefault(rule.IdList[0]) + (int)amount;
            }
            return output;
        }
    }
    [Serializable] public sealed class OfficialMap12Building
    {
        public int EntityClassId, PlacementId, CompleteState, PublicDefaultState;
        public int StageWorldType, Level, SubRank, RegionLevel, RegionSubRank, PrivateLevelType, PublicLevelType;
        public string EndTipKey;
        public float StageLevelAdd;
        public bool Absolute;
        public bool CanInteractAfterMove;
        public OfficialMap12BuildingState[] States;
        public WorldStageInteractionOperation[] Operations;
    }
    [Serializable] public sealed class OfficialMap12BuildingState { public long OperationId; }
    [Serializable] public sealed class Map12BuildingRewardNumber { public int Id; public double[] Nums; }
    [Serializable] public sealed class Map12BuildingRewardItem
    {
        public int ClassId, ItemType, Quality;
        public bool CanDropWhenExist;
    }
}
