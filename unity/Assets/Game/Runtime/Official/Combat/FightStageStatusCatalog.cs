using System;
using System.Collections.Generic;
using System.Globalization;

namespace Zjcs.Combat.Official
{
    /// <summary>
    /// Official <c>rules.json</c> stage status data: <c>stage.Status</c> selects a
    /// <c>fight_stage_status</c> row, and the row lists the statuses applied to the stage, each
    /// character type, professions, target ids and every N global rounds. The view list mirrors
    /// <c>FightStageStatusInfoParser.GetViewStatusList</c> (union minus the blacklist).
    /// </summary>
    public sealed class FightStageStatusCatalog
    {
        private readonly Dictionary<int, FightBattleStageStatusConfig> configs =
            new Dictionary<int, FightBattleStageStatusConfig>();
        private readonly Dictionary<int, int> stageStatus = new Dictionary<int, int>();

        public static FightStageStatusCatalog Parse(string rulesJson)
        {
            if (rulesJson == null) throw new ArgumentNullException(nameof(rulesJson));
            var catalog = new FightStageStatusCatalog();
            Dictionary<string, object> rules = Object(OfficialJson.Parse(rulesJson), "rules");
            if (rules.TryGetValue("stage", out object stageRows))
            {
                foreach (object item in Array(stageRows, "stage"))
                {
                    Dictionary<string, object> row = Object(item, "stage row");
                    int id = Int(row, "Id");
                    if (id <= 0 || catalog.stageStatus.ContainsKey(id)) continue;
                    catalog.stageStatus.Add(id, Int(row, "Status"));
                }
            }
            if (rules.TryGetValue("fight_stage_status", out object statusRows))
            {
                foreach (object item in Array(statusRows, "fight_stage_status"))
                {
                    Dictionary<string, object> row = Object(item, "fight_stage_status row");
                    int classId = Int(row, "ClassId");
                    if (classId <= 0 || catalog.configs.ContainsKey(classId)) continue;
                    catalog.configs.Add(classId, ReadConfig(classId, row));
                }
            }
            return catalog;
        }

        public int ClassIdOfStage(int stageId)
        {
            return stageStatus.TryGetValue(stageId, out int classId) ? classId : 0;
        }

        public FightBattleStageStatusConfig Find(int classId)
        {
            return configs.TryGetValue(classId, out FightBattleStageStatusConfig config) ? config : null;
        }

        /// <summary>Copy suitable for the serialized checkpoint.</summary>
        public FightBattleStageStatusConfig CreateCheckpointConfig(int classId)
        {
            FightBattleStageStatusConfig source = Find(classId);
            return source == null ? null : Clone(source);
        }

        public List<int> ViewStatusList(int classId)
        {
            FightBattleStageStatusConfig config = Find(classId);
            if (config == null) return null;
            var result = new List<int>();
            var seen = new HashSet<int>();
            void Add(IEnumerable<int> values)
            {
                foreach (int value in values) if (seen.Add(value)) result.Add(value);
            }
            Add(config.Status);
            foreach (FightBattleStatusGroup group in config.CharacterStatus) Add(group.Status);
            Add(config.GlobalRoundStatus);
            foreach (FightBattleStatusGroup group in config.FactionGlobalRoundStatus) Add(group.Status);
            Add(config.TargetIdStatus);
            foreach (int ignored in config.ViewStatusBlackList) seen.Remove(ignored);
            result.RemoveAll(value => !seen.Contains(value));
            return result;
        }

        private static FightBattleStageStatusConfig ReadConfig(int classId,
            Dictionary<string, object> row)
        {
            var config = new FightBattleStageStatusConfig
            {
                ClassId = classId,
                GlobalRoundStatusDelay = Float(row, "GlobalRoundStatusDelay"),
                ReleaseEveryGlobalRound = Bool(row, "ReleaseEveryGlobalRound")
            };
            AddInts(row, "Status", config.Status);
            AddInts(row, "GlobalRoundStatus", config.GlobalRoundStatus);
            AddInts(row, "TargetIdStatus", config.TargetIdStatus);
            AddInts(row, "ViewStatusBlackList", config.ViewStatusBlackList);
            AddGroups(row, "CharacterStatus", config.CharacterStatus);
            AddGroups(row, "FactionGlobalRoundStatus", config.FactionGlobalRoundStatus);
            AddGroups(row, "ProfessionStatus", config.ProfessionStatus);
            return config;
        }

        private static FightBattleStageStatusConfig Clone(FightBattleStageStatusConfig source)
        {
            var result = new FightBattleStageStatusConfig
            {
                ClassId = source.ClassId,
                GlobalRoundStatusDelay = source.GlobalRoundStatusDelay,
                ReleaseEveryGlobalRound = source.ReleaseEveryGlobalRound
            };
            result.Status.AddRange(source.Status);
            result.GlobalRoundStatus.AddRange(source.GlobalRoundStatus);
            result.TargetIdStatus.AddRange(source.TargetIdStatus);
            result.ViewStatusBlackList.AddRange(source.ViewStatusBlackList);
            foreach (FightBattleStatusGroup group in source.CharacterStatus)
                result.CharacterStatus.Add(new FightBattleStatusGroup
                {
                    Key = group.Key, Status = new List<int>(group.Status)
                });
            foreach (FightBattleStatusGroup group in source.FactionGlobalRoundStatus)
                result.FactionGlobalRoundStatus.Add(new FightBattleStatusGroup
                {
                    Key = group.Key, Status = new List<int>(group.Status)
                });
            foreach (FightBattleStatusGroup group in source.ProfessionStatus)
                result.ProfessionStatus.Add(new FightBattleStatusGroup
                {
                    Key = group.Key, Status = new List<int>(group.Status)
                });
            return result;
        }

        private static void AddInts(Dictionary<string, object> row, string key, List<int> target)
        {
            if (!row.TryGetValue(key, out object raw) || raw == null) return;
            foreach (object item in Array(raw, key))
                target.Add(Convert.ToInt32(item, CultureInfo.InvariantCulture));
        }

        private static void AddGroups(Dictionary<string, object> row, string key,
            List<FightBattleStatusGroup> target)
        {
            if (!row.TryGetValue(key, out object raw) || raw == null) return;
            Dictionary<string, object> groups = Object(raw, key);
            var keys = new List<int>();
            foreach (KeyValuePair<string, object> entry in groups)
                if (int.TryParse(entry.Key, NumberStyles.Integer, CultureInfo.InvariantCulture,
                        out int groupKey)) keys.Add(groupKey);
            keys.Sort();
            foreach (int groupKey in keys)
            {
                var group = new FightBattleStatusGroup { Key = groupKey };
                foreach (object item in Array(groups[groupKey.ToString(CultureInfo.InvariantCulture)], key))
                    group.Status.Add(Convert.ToInt32(item, CultureInfo.InvariantCulture));
                target.Add(group);
            }
        }

        private static object Required(Dictionary<string, object> value, string key)
        {
            if (value.TryGetValue(key, out object raw) && raw != null) return raw;
            throw new FormatException("Missing official stage status field: " + key);
        }

        private static Dictionary<string, object> Object(object value, string label)
        {
            if (value is Dictionary<string, object> result) return result;
            throw new FormatException(label + " is not an object.");
        }

        private static List<object> Array(object value, string label)
        {
            if (value is List<object> result) return result;
            if (value is object[] array) return new List<object>(array);
            throw new FormatException(label + " is not an array.");
        }

        private static int Int(Dictionary<string, object> value, string key)
        {
            object raw;
            return value.TryGetValue(key, out raw) && raw != null
                ? Convert.ToInt32(raw, CultureInfo.InvariantCulture) : 0;
        }

        private static float Float(Dictionary<string, object> value, string key)
        {
            object raw;
            return value.TryGetValue(key, out raw) && raw != null
                ? Convert.ToSingle(raw, CultureInfo.InvariantCulture) : 0f;
        }

        private static bool Bool(Dictionary<string, object> value, string key)
        {
            object raw;
            return value.TryGetValue(key, out raw) && raw != null
                && Convert.ToBoolean(raw, CultureInfo.InvariantCulture);
        }
    }
}
