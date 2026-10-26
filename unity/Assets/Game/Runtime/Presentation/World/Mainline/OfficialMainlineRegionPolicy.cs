using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Official.World;

namespace Zjcs.Presentation.World
{
    /// <summary>Official region access conditions, evaluated against the active map and story state.</summary>
    public sealed class OfficialMainlineRegionPolicy
    {
        private readonly OfficialMapDefinition definition;
        private readonly OfficialMainlineMapProfile profile;
        private int mapClassId => definition.ClassId;
        internal readonly Dictionary<int, OfficialMainlineRegionConfig> regionConfigs = new Dictionary<int, OfficialMainlineRegionConfig>();
        private readonly Dictionary<int, RegionCondition> mapConditions = new Dictionary<int, RegionCondition>();
        internal readonly Dictionary<int, int> regionPointLimits = new Dictionary<int, int>();

        public OfficialMainlineRegionPolicy(OfficialMapDefinition definition, OfficialMainlineMapProfile profile)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            if (definition.ClassId != profile.MapClassId)
                throw new InvalidDataException("Map profile differs from its official definition.");
            LoadRegionConditions();
        }

        private void LoadRegionConditions()
        {
            regionConfigs.Clear();
            mapConditions.Clear();
            regionPointLimits.Clear();
            foreach (OfficialMapRegionProgress progress in definition.MaxRegionProgress)
            {
                if (progress == null || progress.Value < 0 || regionPointLimits.ContainsKey(progress.Region))
                    throw new InvalidDataException("Official Map10 region progress maximum is malformed.");
                regionPointLimits.Add(progress.Region, progress.Value);
            }

            foreach (OfficialWorldConfigRecord record in definition.Configs ?? Array.Empty<OfficialWorldConfigRecord>())
            {
                if (record == null) continue;
                if (record.TableName == "TbMapRegionInfo")
                {
                    OfficialMainlineRegionConfig region = JsonUtility.FromJson<OfficialMainlineRegionConfig>(record.Json);
                    if (region == null || region.ClassId != mapClassId || region.Region < 0 || region.Areas == null
                        || regionConfigs.ContainsKey(region.Region))
                        throw new InvalidDataException("Official Map10 region configuration is malformed.");
                    regionConfigs.Add(region.Region, region);
                }
                else if (record.TableName == "TbConditionInfo")
                {
                    RegionConditionRecord row = JsonUtility.FromJson<RegionConditionRecord>(record.Json);
                    if (row == null || row.ClassId <= 0 || row.Condition == null || mapConditions.ContainsKey(row.ClassId))
                        throw new InvalidDataException("Official Map10 access condition is malformed.");
                    mapConditions.Add(row.ClassId, row.Condition);
                }
            }

            if (regionConfigs.Count != regionPointLimits.Count)
                throw new InvalidDataException("Official Map10 regions and progress limits differ.");
            foreach (OfficialMainlineRegionConfig region in regionConfigs.Values)
                if (region.AccessibleConditionId > 0 && !mapConditions.ContainsKey(region.AccessibleConditionId))
                    throw new InvalidDataException("Map" + mapClassId + " region references a missing official access condition: " + region.AccessibleConditionId);

            if (mapClassId == 10)
            {
                foreach (int id in profile.RequiredAccessConditions)
                {
                    if (!mapConditions.TryGetValue(id, out RegionCondition condition)
                        || condition.Type != "WorldMapProgressCondition" || condition.MapClassId != mapClassId
                        || condition.Progress != 10000 || !regionPointLimits.ContainsKey(condition.Region))
                        throw new InvalidDataException("Pinned Map10 full-region access condition is invalid: " + id);
                }
            }
            else
            {
                if (profile.RequiredAccessConditions.Any(id => !mapConditions.ContainsKey(id)))
                    throw new InvalidDataException("Pinned Map11 region access conditions are incomplete.");
            }
        }

        public bool IsAccessible(int regionId, IReadOnlyDictionary<int, int> regionPoints, Func<string, int> storyCount)
        {
            if (!regionConfigs.TryGetValue(regionId, out OfficialMainlineRegionConfig region))
                throw new InvalidDataException("Map" + mapClassId + " entity references an unknown region: " + regionId);
            if (region.AccessibleConditionId <= 0) return true;
            if (!mapConditions.TryGetValue(region.AccessibleConditionId, out RegionCondition condition))
                throw new InvalidDataException("Map" + mapClassId + " references an unknown access condition: " + region.AccessibleConditionId);
            if (condition.Type == "StoryCountCondition")
                return Compare(storyCount(condition.StoryKey), condition.Op, condition.Count);
            if (condition.Type != "WorldMapProgressCondition" || condition.MapClassId != mapClassId
                || condition.Progress != 10000)
                throw new InvalidDataException("Unsupported official Map" + mapClassId + " access condition: " + region.AccessibleConditionId);
            if (!regionPointLimits.TryGetValue(condition.Region, out int maximum) || maximum <= 0)
                throw new InvalidDataException("Map" + mapClassId + " access condition references a region without a progress maximum.");
            return regionPoints.TryGetValue(condition.Region, out int progress) && progress >= maximum;
        }

        private static bool Compare(int current, string operation, int target)
        {
            switch (operation)
            {
                case "=": return current == target;
                case ">": return current > target;
                case "<": return current < target;
                case ">=": return current >= target;
                case "<=": return current <= target;
                case "!=": return current != target;
                default: throw new InvalidDataException("Unsupported official MapTalk comparison: " + operation);
            }
        }

        [Serializable]
        private sealed class RegionConditionRecord
        {
            public int ClassId;
            public RegionCondition Condition;
        }

        [Serializable]
        private sealed class RegionCondition
        {
            public string _type;
            public int MapClassId;
            public int Region;
            public int Progress;
            public string StoryKey;
            public int Count;
            public string Op;
            public string Type => _type;
        }
    }

    [Serializable]
    internal sealed class OfficialMainlineRegionConfig
    {
        public int ClassId;
        public int Region;
        public int AccessibleConditionId;
        public long CombatRating;
        public long[] CombatRatingLevelValues;
        public int SubRank;
        public int[] Areas;
        public string NotAccessibleMapTalkId;
    }

}
