using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Equipment;

namespace Zjcs.Offline.Progression
{
    [Serializable] public sealed class OfficialExploreSettings
    {
        public int FoodId, ExploreFoodCost, ExploreFoodTokenCost, WorldExploreMaxTimes,
            WorldExploreGuideFixedRewardId, ExploreEntityMaxDist, ExploreHistoryRecordDays,
            WorldExploreActionId, WorldExploreMoreTimesActionId;
    }
    [Serializable] public sealed class OfficialExploreToken
    {
        public int ItemId, RequireType;
        public int[] ExploreGroups, ExploreWeights, ExploreMininums, ExplorePriorities, ExploreRecordIndexes, PityProbabilitys;
        public string ExploreRecordSharedId;
        public string RecordKey => string.IsNullOrEmpty(ExploreRecordSharedId) ? ItemId.ToString(CultureInfo.InvariantCulture) : ExploreRecordSharedId;
        public int RecordIndex(int index) => ExploreRecordIndexes != null && ExploreRecordIndexes.Length > index ? ExploreRecordIndexes[index] : index;
    }
    [Serializable] public sealed class OfficialExploreGroup { public int Id, GroupId; public int[] EntityGroups, Weights; }
    [Serializable] public sealed class OfficialExploreCondition
    {
        public string _type, Op;
        public int[] Ids;
        public int MapClassId, MapGroupId, Level, SubRank, Profession, ClassId, Count, ActivityType, Id, AheadTime;
        public int[] RegionCodes;
        public bool Not;
    }
    [Serializable] public sealed class OfficialExploreEntityGroup
    {
        public int Id, Group, MatchOrder, RefreshDistance, RefreshPrecent;
        public string Icon;
        public int[] Entitys, Weights, ExploreCounts, ExploreCountWeights;
        public float LevelMin, LevelMax;
        public bool ExpandReward, LastMapRefreshOnly;
        public OfficialExploreCondition FilterCondition;
    }
    [Serializable] public sealed class OfficialExploreParameters
    {
        public int EntityClassId, EntityQuality, WorldStageType, CompleteState, PublicDefaultState, DurationWhenExplore,
            InteractionActionId, InteractionEntityActionId, RegionActivateActionId, LevelTypeInPublicMap;
        public string MiniMapMark, Icon;
    }
    [Serializable] public sealed class OfficialExploreEntityTemplate
    {
        public int entityClassId;
        public OfficialExploreParameters parameters;
        public OfficialExploreDisplayPath[] displayPaths;
        public OfficialExploreTemplateComponent[] templateComponents;
    }
    [Serializable] public sealed class OfficialExploreDisplayPath { public int mapClassId; public string path; }
    [Serializable] public sealed class OfficialExploreTemplateComponent { public string Type, Json; }
    [Serializable] public sealed class OfficialExploreSpawner
    {
        public int mapClassId;
        public int[] EnableSpawnGroundTypes, EnableSpawnEntityClassIds;
    }
    [Serializable] public sealed class OfficialExploreFunctionLimit
    {
        public int Type, Key0, Key1;
        public OfficialExploreCondition Condition;
    }
    [Serializable] public sealed class OfficialExploreLimit { public int Limit, MapEntityGroupId; }
    [Serializable] public sealed class OfficialExploreMapLevel { public int Level, SubRank, MapingLevel; }
    [Serializable] public sealed class OfficialExploreProfession { public int Profession, PrePro; }
    [Serializable] public sealed class OfficialExploreRewardRule
    {
        public int Id, Order;
        public float Percent;
        public int[] IdList, WeightList, SubRankCountId, LevelCountId;
        public float[] CountList;
        public OfficialExploreCondition Condition;
    }
    [Serializable] public sealed class OfficialExploreRandomSelector
    {
        public string _type;
        public int MapClassId, Quality;
        public int[] DefaultGachaIds, ElementTypes;
        public bool IsNeedMet, IsPiece;
    }
    [Serializable] public sealed class OfficialExploreRandomReward
    {
        public int Id, MinCount, MaxCount;
        public int[] IdList, WeightOfIdList;
        public bool IsCanAutoExpend;
        public OfficialExploreRandomSelector RandomItemRangeSelector;
    }
    [Serializable] public sealed class OfficialExploreTreasureGacha
    {
        public int ChestGachaId, MapClassId;
        public int[] ChestGachaItems;
        public OfficialExploreCondition Condition;
    }
    [Serializable] public sealed class OfficialExploreItem { public int ClassId, Quality, ElementType; }
    [Serializable] public sealed class OfficialExploreActivity { public int OpenConditionId; }
    [Serializable] public sealed class OfficialExploreGuideReward { public int Id, CanGetTimes; public OfflineItemStack[] Items; }
    [Serializable] public sealed class OfficialExploreConditionRow { public int ClassId; public OfficialExploreCondition Condition; }
    [Serializable] public sealed class OfficialWorldExplorationData
    {
        public string manifest;
        public int dailyLimit;
        public OfficialExploreSettings settings;
        public OfficialExploreToken[] tokenGroups;
        public OfficialExploreGroup[] exploreGroups;
        public OfficialExploreEntityGroup[] entityGroups;
        public OfficialExploreConditionRow[] conditions;
        public OfficialExploreEntityTemplate[] entities;
        public OfficialExploreSpawner[] spawners;
        public OfficialExploreFunctionLimit[] functionLimits;
        public OfficialExploreLimit[] limits;
        public OfficialExploreMapLevel[] playerMapLevels;
        public OfficialExploreRewardRule[] rewardRules;
        public OfficialExploreRandomReward[] randomRewards;
        public MaterialGatheringLevelNumber[] rewardLevelNumbers;
        public OfficialExploreTreasureGacha[] treasureGachas;
        public OfficialExploreProfession[] professions;
        public OfficialExploreItem[] items;
        public OfficialExploreActivity cooperativeActivity;
        public OfficialExploreGuideReward guideReward;
    }

    public sealed class OfficialWorldExplorationCatalog
    {
        public OfficialWorldExplorationData Data { get; }
        private readonly Dictionary<int, Dictionary<string, object>> groups;
        private readonly Dictionary<int, Dictionary<string, object>> entities;
        public OfficialWorldExplorationCatalog(string json)
        {
            Data = JsonUtility.FromJson<OfficialWorldExplorationData>(json);
            if (Data?.manifest != "854_156383" || Data.tokenGroups?.Length != 7 || Data.entityGroups?.Length != 65
                || Data.entities?.Length != 28 || Data.settings == null || Data.dailyLimit <= 0
                || Data.guideReward?.Id != Data.settings.WorldExploreGuideFixedRewardId || Data.guideReward.CanGetTimes != 1)
                throw new InvalidDataException("Official exploration rules are unbound.");
            var raw = Object(OfficialEquipmentJson.Parse(json));
            // Unity JsonUtility creates an empty reference object for JSON null.
            // Restore nullable native filters from the raw source explicitly.
            foreach (var row in Values(raw, "rewardRules").Select(Object).Zip(Data.rewardRules, (source, target) => (source, target)))
                if (row.source["Condition"] == null) row.target.Condition = null;
            foreach (var row in Values(raw, "treasureGachas").Select(Object).Zip(Data.treasureGachas, (source, target) => (source, target)))
                if (row.source["Condition"] == null) row.target.Condition = null;
            foreach (var row in Values(raw, "randomRewards").Select(Object).Zip(Data.randomRewards, (source, target) => (source, target)))
                if (row.source["RandomItemRangeSelector"] == null) row.target.RandomItemRangeSelector = null;
            foreach (var row in Values(raw, "entityGroups").Select(Object).Zip(Data.entityGroups, (source, target) => (source, target)))
                if (row.source["FilterCondition"] == null) row.target.FilterCondition = null;
            foreach (var row in Values(raw, "functionLimits").Select(Object).Zip(Data.functionLimits, (source, target) => (source, target)))
                if (row.source["Condition"] == null) row.target.Condition = null;
            groups = Values(raw, "entityGroups").Select(Object).ToDictionary(v => Int(v, "Id"));
            entities = Values(raw, "entities").Select(Object).ToDictionary(v => Int(v, "entityClassId"));
            // All pinned guaranteed probability periods are empty. Refuse a new
            // source version requiring a different native dynamic-weight branch.
            foreach (var token in Values(raw, "tokenGroups").Select(Object))
                if (Values(token, "ExploreGuaranteeds").Any(v => ((List<object>)v).Count != 0))
                    throw new InvalidDataException("Unconverted native exploration guaranteed periods.");
            if (Data.tokenGroups.Any(t => t.ExploreGroups.Length != t.ExploreWeights.Length
                || t.ExploreGroups.Length != t.ExploreMininums.Length || t.ExploreGroups.Length != t.ExplorePriorities.Length))
                throw new InvalidDataException("Native exploration random arrays differ in length.");
        }
        public static OfficialWorldExplorationCatalog Load()
        {
            var asset = Resources.Load<TextAsset>("World/Mainline/exploration");
            return new OfficialWorldExplorationCatalog(asset?.text ?? throw new InvalidDataException("Exploration catalog is missing."));
        }
        public IReadOnlyList<object> ExtraData(int variant) => Values(groups[variant], "ExtraDataInfos");
        public Dictionary<string, object> Components(int entity) => Object(entities[entity]["components"]);
        public OfficialExploreParameters Parameters(int entity) => Data.entities.Single(v => v.entityClassId == entity).parameters;
        public static Dictionary<string, object> Object(object value) => value as Dictionary<string, object>
            ?? throw new InvalidDataException("Expected original exploration object.");
        public static List<object> Values(Dictionary<string, object> value, string key) => value.TryGetValue(key, out var item)
            && item != null ? item as List<object> ?? throw new InvalidDataException("Expected original exploration array: " + key) : new List<object>();
        public static int Int(Dictionary<string, object> value, string key) => Convert.ToInt32(value[key], CultureInfo.InvariantCulture);
        public static int[] Ints(Dictionary<string, object> value, string key) => Values(value, key).Select(v => Convert.ToInt32(v, CultureInfo.InvariantCulture)).ToArray();
        public static string Text(Dictionary<string, object> value, string key) => Convert.ToString(value[key], CultureInfo.InvariantCulture);
    }
}
