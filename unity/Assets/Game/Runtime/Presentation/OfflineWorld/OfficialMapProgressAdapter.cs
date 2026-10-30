using System;
using System.Collections.Generic;
using Zjcs.Official.World;
using Zjcs.Offline.World;
using UnityEngine;

namespace Zjcs.Presentation.OfflineWorld
{
    public static class OfficialMapProgressAdapter
    {
        [Serializable]
        private sealed class EntityProgressConfig
        {
            public int EntityClassId = 0;
            public int Progress = 0;
            public int CompleteState = 0;
        }

        public static Dictionary<int, OfficialMapEntityProgressRule> ReadEntityRules(OfficialMapDefinition map)
        {
            if (map == null || map.Configs == null)
                throw new ArgumentException("Map definition config records are required.", nameof(map));

            var rules = new Dictionary<int, OfficialMapEntityProgressRule>();
            foreach (OfficialWorldConfigRecord record in map.Configs)
            {
                if (record == null || record.TableName != "TbEntityExtendParameterInfo")
                    continue;

                EntityProgressConfig config = JsonUtility.FromJson<EntityProgressConfig>(record.Json);
                // The exported Map14 Luban table omits RecordId on this
                // shared entity-parameter table. Its EntityClassId is the
                // stable primary key used by the official rows and is
                // therefore the lossless fallback for that one source form.
                int recordId = config?.EntityClassId ?? 0;
                if (config != null && !string.IsNullOrWhiteSpace(record.RecordId)
                    && (!int.TryParse(record.RecordId, out recordId) || recordId != config.EntityClassId))
                    recordId = 0;
                if (config == null || config.EntityClassId <= 0 || recordId != config.EntityClassId)
                    throw new InvalidOperationException("Invalid official entity progress config record " + record.RecordId);

                if (rules.ContainsKey(config.EntityClassId))
                    throw new InvalidOperationException("Duplicate official entity progress config " + record.RecordId);
                rules.Add(config.EntityClassId, new OfficialMapEntityProgressRule
                {
                    EntityClassId = config.EntityClassId,
                    Progress = config.Progress,
                    CompleteState = config.CompleteState
                });
            }

            return rules;
        }

        public static Dictionary<int, int> CalculateRegionPoints(
            OfficialMapDefinition map,
            IReadOnlyDictionary<int, int> privateEntityStates,
            ISet<int> completedEntityIds)
        {
            return OfficialMapExplorationProgress.CalculateRegionPoints(
                map,
                ReadEntityRules(map),
                privateEntityStates ?? new Dictionary<int, int>(),
                completedEntityIds ?? new HashSet<int>());
        }
    }
}
