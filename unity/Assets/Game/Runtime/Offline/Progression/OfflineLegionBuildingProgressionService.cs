using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    public sealed class OfflineLegionBuildingProgressionService
    {
        public const int HallBuildingId = 1;
        public const int LibraryBuildingId = 4;
        public const int BuildingUpgradeHangUpType = 1;
        public const int BuildingBuildHangUpType = 2;

        private readonly OfflineMainlineProgressSession session;
        private readonly OfficialCharacterCatalog catalog;

        private sealed class OperationPlan
        {
            public int BuildingId;
            public int HangUpType;
            public int TargetLevel;
            public long RequiredProgress;
            public Dictionary<int, long> Cost;
        }

        public OfflineLegionBuildingProgressionService(OfflineMainlineProgressSession session,
            OfficialCharacterCatalog catalog)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public int Level(int buildingId)
        {
            OfflineCharacterPropertyProgress properties = Properties(session.Save.Player);
            return properties.LegionBuildingLevels.FirstOrDefault(value => value.BuildingId == buildingId)?.Level ?? 0;
        }

        public OfflineLegionBuildingUpgrade ActiveOperation
        {
            get
            {
                OfflineLegionBuildingUpgrade value = Properties(session.Save.Player).LegionBuildingUpgrade;
                return value == null ? null : new OfflineLegionBuildingUpgrade
                {
                    BuildingId = value.BuildingId,
                    HangUpType = value.HangUpType,
                    TargetLevel = value.TargetLevel,
                    Progress = value.Progress,
                    RequiredProgress = value.RequiredProgress,
                    LastProgressUnixSeconds = value.LastProgressUnixSeconds,
                    ProgressRemainder = value.ProgressRemainder
                };
            }
        }

        public int SettlePeriodSeconds => HangUpSettlePeriodSeconds();

        public IReadOnlyDictionary<int, long> OperationCost(int buildingId)
        {
            OfflinePlayerProgress player = session.Save.Player;
            if (!TryGetOperationPlan(player, buildingId, out OperationPlan plan, out _,
                requireMaterials: false, allowActiveOperation: true))
                return new Dictionary<int, long>();
            return new Dictionary<int, long>(plan.Cost);
        }

        public bool CanStartOperation(int buildingId, out string reason)
        {
            return TryGetOperationPlan(session.Save.Player, buildingId, out _, out reason);
        }

        public ProgressionResult StartOperation(int buildingId, long nowUnixSeconds, string eventId)
        {
            if (buildingId <= 0 || nowUnixSeconds < 0 || string.IsNullOrWhiteSpace(eventId))
                return Invalid("A valid building, start time and event ID are required.");
            if (session.Save.Player.AppliedCharacterEventIds.Contains(eventId))
                return new ProgressionResult { Status = ProgressionResultStatus.Unchanged };
            if (!TryGetOperationPlan(session.Save.Player, buildingId, out _, out string reason))
                return Invalid(reason);

            return session.ApplyCharacterMutation(player =>
            {
                if (!TryGetOperationPlan(player, buildingId, out OperationPlan plan, out string currentReason))
                    throw new InvalidOperationException(currentReason);
                OfflineCharacterPropertyProgress properties = Properties(player);
                properties.LegionBuildingUpgrade = new OfflineLegionBuildingUpgrade
                {
                    BuildingId = buildingId,
                    HangUpType = plan.HangUpType,
                    TargetLevel = plan.TargetLevel,
                    RequiredProgress = plan.RequiredProgress,
                    LastProgressUnixSeconds = nowUnixSeconds
                };
                foreach (KeyValuePair<int, long> cost in plan.Cost)
                    player.Inventory.First(value => value.ItemId == cost.Key).Count -= cost.Value;
            }, eventId);
        }

        public ProgressionResult Settle(long nowUnixSeconds, string eventId = null)
        {
            if (nowUnixSeconds < 0)
                return Invalid("A valid settlement time is required.");
            if (!string.IsNullOrWhiteSpace(eventId)
                && session.Save.Player.AppliedCharacterEventIds.Contains(eventId))
                return new ProgressionResult { Status = ProgressionResultStatus.Unchanged };

            return session.ApplyCharacterMutationIfChanged(player =>
            {
                OfflineCharacterPropertyProgress properties = Properties(player);
                OfflineLegionBuildingUpgrade operation = properties.LegionBuildingUpgrade;
                if (operation == null || nowUnixSeconds <= operation.LastProgressUnixSeconds) return false;
                int period = HangUpSettlePeriodSeconds();
                if (period <= 0) return false;
                long elapsed = nowUnixSeconds - operation.LastProgressUnixSeconds;
                long settleCount = elapsed / period;
                if (settleCount <= 0) return false;
                long settledSeconds = settleCount * period;
                int speedPerHour = HangUpSpeedPerHour(player, operation.HangUpType);
                long remaining = operation.RequiredProgress - operation.Progress;
                if (remaining <= 0)
                {
                    CompleteOperation(properties, operation);
                    return true;
                }

                if (speedPerHour > 0)
                {
                    long requiredNumerator = checked(remaining * 3600L - operation.ProgressRemainder);
                    long secondsToComplete = (requiredNumerator + speedPerHour - 1) / speedPerHour;
                    if (settledSeconds >= secondsToComplete)
                    {
                        CompleteOperation(properties, operation);
                        return true;
                    }

                    long numerator = checked((long)speedPerHour * settledSeconds
                        + operation.ProgressRemainder);
                    operation.Progress += numerator / 3600L;
                    operation.ProgressRemainder = (int)(numerator % 3600L);
                }
                operation.LastProgressUnixSeconds = checked(operation.LastProgressUnixSeconds + settledSeconds);
                return true;
            }, eventId);
        }

        public int HangUpSpeedPerHour(int buildingId)
        {
            OfflineLegionBuildingUpgrade operation = ActiveOperation;
            return operation != null && operation.BuildingId == buildingId
                ? HangUpSpeedPerHour(session.Save.Player, operation.HangUpType)
                : 0;
        }

        private bool TryGetOperationPlan(OfflinePlayerProgress player, int buildingId,
            out OperationPlan plan, out string reason, bool requireMaterials = true,
            bool allowActiveOperation = false)
        {
            plan = null;
            reason = string.Empty;
            if (player == null || buildingId <= 0)
            {
                reason = "A valid character and building are required.";
                return false;
            }
            OfflineCharacterPropertyProgress properties = Properties(player);
            if (!allowActiveOperation && properties.LegionBuildingUpgrade != null)
            {
                reason = "Another guild building HangUp operation is already active.";
                return false;
            }

            Dictionary<string, object> building = catalog.Find("legion_map_building", buildingId, "Id");
            if (building == null)
            {
                reason = "The official guild building is unavailable.";
                return false;
            }
            int currentLevel = properties.LegionBuildingLevels
                .FirstOrDefault(value => value.BuildingId == buildingId)?.Level ?? 0;
            int hangUpType = currentLevel == 0 ? BuildingBuildHangUpType : BuildingUpgradeHangUpType;
            int targetLevel = currentLevel + 1;
            if (hangUpType == BuildingUpgradeHangUpType && !Boolean(building, "CanUpgrade"))
            {
                reason = "The official building cannot be upgraded.";
                return false;
            }
            int maxLevel = Integer(building, "MaxLevel");
            if (maxLevel <= 0)
                maxLevel = catalog.Rows("legion_map_building_level")
                    .Where(value => Integer(value, "Id") == buildingId)
                    .Select(value => Integer(value, "Level")).DefaultIfEmpty(0).Max();
            if (targetLevel > maxLevel)
            {
                reason = "The building has reached its official maximum level.";
                return false;
            }

            Dictionary<string, object> levelInfo = catalog.Rows("legion_map_building_level")
                .FirstOrDefault(value => Integer(value, "Id") == buildingId
                    && Integer(value, "Level") == currentLevel);
            if (levelInfo == null)
            {
                reason = "The official building level configuration is unavailable.";
                return false;
            }
            if (hangUpType == BuildingBuildHangUpType)
            {
                if (!EvaluateBuildingCondition(Map(building, "BuildCondition"), player,
                    new HashSet<int>()))
                {
                    reason = "The official building construction condition is not satisfied.";
                    return false;
                }
            }
            else if (buildingId == HallBuildingId && !HallRankConditionMet(currentLevel, player.Rank))
            {
                reason = "The character rank does not unlock the next Hall level.";
                return false;
            }
            if (!UpgradeConditionsMet(levelInfo, player))
            {
                reason = "An official guild building level condition is not satisfied.";
                return false;
            }

            long requiredProgress = Long(levelInfo, "UpgradeProgressValue");
            if (requiredProgress <= 0)
            {
                reason = "The official building progress requirement is invalid.";
                return false;
            }
            Dictionary<int, long> costs = AdjustedCost(levelInfo, hangUpType, player);
            if (costs.Count == 0 || costs.Any(value => value.Value <= 0))
            {
                reason = "The official building operation has no valid material cost.";
                return false;
            }
            foreach (KeyValuePair<int, long> cost in costs)
            {
                OfflineItemStack item = player.Inventory.FirstOrDefault(value => value.ItemId == cost.Key);
                if (requireMaterials && (item == null || item.Count < cost.Value))
                {
                    reason = "The official guild building materials are insufficient.";
                    return false;
                }
            }

            plan = new OperationPlan
            {
                BuildingId = buildingId,
                HangUpType = hangUpType,
                TargetLevel = targetLevel,
                RequiredProgress = requiredProgress,
                Cost = costs
            };
            return true;
        }

        private bool UpgradeConditionsMet(Dictionary<string, object> levelInfo, OfflinePlayerProgress player)
        {
            if (!levelInfo.TryGetValue("UpgradeConditions", out object rawConditions)
                || !(rawConditions is IEnumerable<object> conditions)) return true;
            foreach (object rawCondition in conditions)
                if (!(rawCondition is Dictionary<string, object> condition)
                    || !EvaluateBuildingCondition(condition, player, new HashSet<int>())) return false;
            return true;
        }

        private bool EvaluateBuildingCondition(Dictionary<string, object> condition, OfflinePlayerProgress player,
            HashSet<int> visiting)
        {
            if (condition == null || !condition.TryGetValue("_type", out object rawType)) return false;
            string type = Convert.ToString(rawType, CultureInfo.InvariantCulture);
            if (type == "NoneCondition") return true;
            if (type == "AlwaysCondition") return false;
            if (type == "LegionMapBuildingLevelCondition")
            {
                int targetType = Integer(condition, "BuildingType");
                int targetLevel = Integer(condition, "Level");
                int current = BuildingTypeLevel(player, targetType, Boolean(condition, "Exclud"));
                return current >= targetLevel;
            }
            if (type == "CompositeAndCondition" || type == "CompositeOrCondition")
            {
                if (!condition.TryGetValue("Ids", out object rawIds)
                    || !(rawIds is IEnumerable<object> ids)) return false;
                bool isAnd = type == "CompositeAndCondition";
                foreach (object rawId in ids)
                {
                    int id = Convert.ToInt32(rawId, CultureInfo.InvariantCulture);
                    bool met = EvaluateBuildingConditionId(id, player, visiting);
                    if (isAnd && !met) return false;
                    if (!isAnd && met) return true;
                }
                return isAnd;
            }

            var context = new OfficialConditionContext
            {
                Level = player.Level,
                ItemCounts = (player.Inventory ?? new List<OfflineItemStack>())
                    .Where(value => value != null && value.ItemId > 0)
                    .GroupBy(value => value.ItemId)
                    .ToDictionary(group => group.Key, group => group.Sum(value => value.Count))
            };
            return catalog.EvaluateCondition(condition, context);
        }

        private bool EvaluateBuildingConditionId(int conditionId, OfflinePlayerProgress player,
            HashSet<int> visiting)
        {
            if (conditionId <= 0 || !visiting.Add(conditionId)) return false;
            try
            {
                Dictionary<string, object> row = catalog.Find("conditions", conditionId, "ClassId");
                return row != null && row.TryGetValue("Condition", out object raw)
                    && raw is Dictionary<string, object> condition
                    && EvaluateBuildingCondition(condition, player, visiting);
            }
            finally
            {
                visiting.Remove(conditionId);
            }
        }

        private int BuildingTypeLevel(OfflinePlayerProgress player, int targetType, bool excludeTargetType)
        {
            var levelsByType = new Dictionary<int, int>();
            OfflineCharacterPropertyProgress properties = Properties(player);
            foreach (OfflineLegionBuildingLevel buildingLevel in properties.LegionBuildingLevels)
            {
                if (buildingLevel == null || buildingLevel.Level <= 0) continue;
                Dictionary<string, object> building = catalog.Find("legion_map_building",
                    buildingLevel.BuildingId, "Id");
                if (building == null) continue;
                int type = Integer(building, "Type");
                if (!levelsByType.TryGetValue(type, out int current) || buildingLevel.Level > current)
                    levelsByType[type] = buildingLevel.Level;
            }
            if (!excludeTargetType)
                return levelsByType.TryGetValue(targetType, out int level) ? level : 0;
            int total = 0;
            foreach (KeyValuePair<int, int> entry in levelsByType)
                if (entry.Key != targetType) total = checked(total + entry.Value);
            return total;
        }

        private bool HallRankConditionMet(int currentLevel, int rank)
        {
            Dictionary<string, object> part = catalog.Rows("legion_map_building_level_hall")
                .FirstOrDefault(value => Integer(value, "Id") == HallBuildingId
                    && Integer(value, "Level") == currentLevel);
            return part != null && rank >= Integer(part, "UpgradeRankCondition");
        }

        private Dictionary<int, long> AdjustedCost(Dictionary<string, object> levelInfo,
            int hangUpType, OfflinePlayerProgress player)
        {
            Dictionary<int, long> cost = LongMap(Map(levelInfo, "UpgradeCost"));
            Dictionary<string, object> hangUp = catalog.Rows("legion_map_hangup")
                .FirstOrDefault(value => Integer(value, "HangUpType") == hangUpType);
            if (hangUp == null) return new Dictionary<int, long>();
            int reduceType = Integer(hangUp, "OpenCostReducePropType");
            if (reduceType == 0) return cost;
            IReadOnlyDictionary<int, long> props = HangUpProperties(player);
            props.TryGetValue(reduceType, out long reductionValue);
            double reduction = reductionValue / 10000.0;
            var adjusted = new Dictionary<int, long>();
            foreach (KeyValuePair<int, long> item in cost)
            {
                double value = Math.Round(item.Value * (1.0 - reduction));
                if (double.IsNaN(value) || double.IsInfinity(value)
                    || value < long.MinValue || value > long.MaxValue) return new Dictionary<int, long>();
                adjusted[item.Key] = Math.Max(1L, (long)value);
            }
            return adjusted;
        }

        private int HangUpSpeedPerHour(OfflinePlayerProgress player, int hangUpType)
        {
            Dictionary<string, object> hangUp = catalog.Rows("legion_map_hangup")
                .FirstOrDefault(value => Integer(value, "HangUpType") == hangUpType);
            if (hangUp == null || !Boolean(hangUp, "SupportDefaultDriver")) return 0;
            int period = HangUpSettlePeriodSeconds();
            if (period <= 0) return 0;
            int timesPerHour = (int)(3600.0 / period);
            if (timesPerHour <= 0) return 0;
            IReadOnlyDictionary<int, long> props = HangUpProperties(player);
            int baseProp = Integer(hangUp, "HangUpPropTypeDefault");
            int additiveProp = Integer(hangUp, "HangUpAddPropTypeDefault");
            int boostProp = Integer(hangUp, "HangUpAddPropTypeBoost");
            props.TryGetValue(baseProp, out long baseRate);
            props.TryGetValue(additiveProp, out long additiveRate);
            props.TryGetValue(boostProp, out long boostRate);
            double rate = baseRate * (1.0 + (additiveRate + (double)boostRate) / 10000.0)
                * timesPerHour;
            if (double.IsNaN(rate) || double.IsInfinity(rate) || rate <= 0 || rate > int.MaxValue) return 0;
            return (int)Math.Round(rate);
        }

        private IReadOnlyDictionary<int, long> HangUpProperties(OfflinePlayerProgress player)
        {
            var result = new Dictionary<int, long>();
            OfflineCharacterPropertyProgress properties = Properties(player);
            foreach (OfflineLegionBuildingLevel building in properties.LegionBuildingLevels)
            {
                if (building == null || building.Level <= 0) continue;
                Dictionary<string, object> row = catalog.Rows("legion_map_building_level")
                    .FirstOrDefault(value => Integer(value, "Id") == building.BuildingId
                        && Integer(value, "Level") == building.Level);
                Merge(result, LongMap(Map(row, "Props")));
            }
            foreach (OfflineLegionMapSkill skill in properties.LegionMapSkills)
            {
                if (skill == null || skill.Level <= 0) continue;
                Dictionary<string, object> row = catalog.Rows("legion_map_skill_study_level")
                    .FirstOrDefault(value => Integer(value, "SkillClassId") == skill.SkillClassId
                        && Integer(value, "Level") == skill.Level);
                Merge(result, LongMap(Map(row, "Props")));
            }
            return result;
        }

        private int HangUpSettlePeriodSeconds()
        {
            Dictionary<string, object> settings = catalog.Rows("game_settings").FirstOrDefault();
            Dictionary<string, object> legion = Map(settings, "LegionMapSetting");
            return Integer(legion, "HangUpSettlePeriodic");
        }

        private static void CompleteOperation(OfflineCharacterPropertyProgress properties,
            OfflineLegionBuildingUpgrade operation)
        {
            OfflineLegionBuildingLevel building = properties.LegionBuildingLevels.FirstOrDefault(value =>
                value.BuildingId == operation.BuildingId);
            if (building == null)
                properties.LegionBuildingLevels.Add(building = new OfflineLegionBuildingLevel
                    { BuildingId = operation.BuildingId });
            building.Level = operation.TargetLevel;
            if (operation.BuildingId == LibraryBuildingId)
                properties.LegionLibraryLevel = operation.TargetLevel;
            properties.LegionBuildingUpgrade = null;
        }

        private static OfflineCharacterPropertyProgress Properties(OfflinePlayerProgress player)
        {
            if (player.CharacterProperties == null)
                player.CharacterProperties = new OfflineCharacterPropertyProgress();
            OfflineCharacterPropertyProgress.Normalize(player.CharacterProperties);
            return player.CharacterProperties;
        }

        private static Dictionary<int, long> LongMap(Dictionary<string, object> values)
        {
            var result = new Dictionary<int, long>();
            if (values == null) return result;
            foreach (KeyValuePair<string, object> item in values)
            {
                int id = Convert.ToInt32(item.Key, CultureInfo.InvariantCulture);
                long value = Convert.ToInt64(item.Value, CultureInfo.InvariantCulture);
                if (id > 0) result[id] = value;
            }
            return result;
        }

        private static void Merge(Dictionary<int, long> target, Dictionary<int, long> values)
        {
            foreach (KeyValuePair<int, long> item in values)
            {
                target.TryGetValue(item.Key, out long current);
                target[item.Key] = checked(current + item.Value);
            }
        }

        private static Dictionary<string, object> Map(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value)
                && value is Dictionary<string, object> result ? result : null;
        }

        private static int Integer(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value)
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        }

        private static long Long(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value)
                ? Convert.ToInt64(value, CultureInfo.InvariantCulture) : 0L;
        }

        private static bool Boolean(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value) && value is bool result && result;
        }

        private static ProgressionResult Invalid(string message)
        {
            return new ProgressionResult { Status = ProgressionResultStatus.InvalidState, Message = message };
        }
    }
}
