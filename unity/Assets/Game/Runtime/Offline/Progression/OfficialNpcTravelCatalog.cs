using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Offline.Progression
{
    [Serializable] public sealed class NpcTravelRequirement { public int id; public int count; }
    [Serializable] public sealed class NpcTravelQuality
    {
        public int quality, base_reward_add, extra_base_add, extra_condition_add;
        public int refresh_weight, npc_count, condition_count, duration, pity_threshold;
    }
    [Serializable] public sealed class NpcTravelRow
    {
        public int travel_id, map_group_id, region_id, position_id, node_id, quality, weight, extra_reward_rule_id;
        public NpcTravelRequirement[] professions, map_groups, preferences;
    }
    [Serializable] public sealed class NpcTravelNode
    {
        public int map_group_id, region_id, node_id, stage_id;
        public int base_add_count, base_add_percent, extra_probability_add;
    }
    [Serializable] public sealed class NpcTravelNpc
    {
        public int id, travel_profession, map_group_id, preference_type, npc_type;
        public string name_key, icon, figure_image;
        public string figure_model_ui, figure_model_with_weapon_ui, idle_anim_name, run_anim_name;
        public bool can_develop;
    }
    [Serializable] public sealed class NpcTravelRate { public int type, rate; }
    [Serializable] public sealed class NpcTravelRewardRule
    {
        public int id, order;
        public float percent;
        public int[] item_ids, weights;
        public float[] counts;
    }
    [Serializable] public sealed class NpcTravelPayload
    {
        public string manifest;
        public int daily_start_limit, npc_daily_limit, refresh_cost_item_id;
        public int base_reward_item_id, base_reward_item_count;
        public long[] refresh_costs;
        public int extra_normal_threshold, extra_super_threshold;
        public int[] travel_npc_prop_types, travel_treasure_prop_types, travel_node_prop_types;
        public int[] positions, first_auto_refresh, first_manual_refresh;
        public NpcTravelRate[] extra_rates;
        public NpcTravelQuality[] qualities;
        public NpcTravelRow[] travels;
        public NpcTravelNode[] nodes;
        public NpcTravelNpc[] npcs;
        public NpcTravelRewardRule[] reward_rules;
    }

    public sealed class OfficialNpcTravelCatalog
    {
        public NpcTravelPayload Data { get; }
        private OfficialNpcTravelCatalog(NpcTravelPayload data) { Data = data; }

        public static OfficialNpcTravelCatalog LoadDefault()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "Home", "travel-catalog.json");
            if (!File.Exists(path)) throw new FileNotFoundException("Official NPC travel catalog is missing.", path);
            NpcTravelPayload data = JsonUtility.FromJson<NpcTravelPayload>(File.ReadAllText(path));
            if (data == null || data.manifest != "854_156383" || data.positions == null
                || data.positions.Length != 4 || data.qualities == null || data.travels == null
                || data.travels.Length != 1728 || data.nodes == null || data.npcs == null
                || data.refresh_costs == null || data.refresh_costs.Length == 0
                || data.reward_rules == null || data.reward_rules.Length != 4
                || data.travel_npc_prop_types == null || data.travel_npc_prop_types.Length != 3
                || data.travel_treasure_prop_types == null || data.travel_treasure_prop_types.Length != 3
                || data.travel_node_prop_types == null || data.travel_node_prop_types.Length != 3
                || data.daily_start_limit != 4 || data.npc_daily_limit != 1)
                throw new InvalidDataException("Official NPC travel catalog is incomplete or from an unsupported manifest.");
            if (data.reward_rules.Any(rule => rule == null || rule.id <= 0 || rule.percent < 0f
                    || rule.item_ids == null || rule.weights == null || rule.counts == null
                    || rule.item_ids.Length == 0 || rule.item_ids.Length != rule.weights.Length
                    || rule.item_ids.Length != rule.counts.Length
                    || rule.item_ids.Any(id => id <= 0) || rule.weights.Any(weight => weight < 0)
                    || rule.counts.Any(count => count < 0f))
                || data.reward_rules.Select(rule => rule.id).Distinct().Count() != data.reward_rules.Length)
                throw new InvalidDataException("Official NPC travel reward rules are invalid.");
            var ruleIds = new HashSet<int>(data.reward_rules.Select(rule => rule.id));
            if (data.travels.Any(row => row == null || row.travel_id <= 0 || row.position_id < 1
                    || row.position_id > data.positions.Length || row.quality < 1
                    || !ruleIds.Contains(row.extra_reward_rule_id))
                || data.qualities.Any(quality => quality == null || quality.quality < 1
                    || quality.duration <= 0 || quality.npc_count <= 0 || quality.refresh_weight < 0
                    || quality.pity_threshold < 0)
                || data.qualities.Select(quality => quality.quality).Distinct().Count() != data.qualities.Length)
                throw new InvalidDataException("Official NPC travel rows or quality settings are invalid.");
            return new OfficialNpcTravelCatalog(data);
        }

        public NpcTravelRow FindTravel(int id) => Data.travels.FirstOrDefault(row => row.travel_id == id);
        public NpcTravelQuality Quality(int id) => Data.qualities.FirstOrDefault(row => row.quality == id);
        public NpcTravelNpc Npc(int id) => Data.npcs.FirstOrDefault(row => row.id == id);
        public NpcTravelRewardRule RewardRule(int id) => Data.reward_rules.FirstOrDefault(row => row.id == id);
        public NpcTravelNode FirstNode => Data.nodes.Where(row => row.stage_id > 0)
            .OrderBy(row => row.stage_id).ThenBy(row => row.map_group_id).ThenBy(row => row.region_id)
            .ThenBy(row => row.node_id).FirstOrDefault();
        public NpcTravelNode Node(int mapGroupId, int regionId, int nodeId) => Data.nodes.FirstOrDefault(row =>
            row.map_group_id == mapGroupId && row.region_id == regionId && row.node_id == nodeId);

        public int MeetConditionCount(int travelId, System.Collections.Generic.IReadOnlyList<int> npcIds)
        {
            NpcTravelRow row = FindTravel(travelId);
            if (row == null || npcIds == null) return 0;
            NpcTravelNpc[] npcs = npcIds.Select(Npc).Where(value => value != null).ToArray();
            int count = 0;
            count += (row.professions ?? Array.Empty<NpcTravelRequirement>()).Count(requirement =>
                npcs.Count(npc => npc.travel_profession == requirement.id) >= requirement.count);
            count += (row.map_groups ?? Array.Empty<NpcTravelRequirement>()).Count(requirement =>
                npcs.Count(npc => npc.map_group_id == requirement.id) >= requirement.count);
            count += (row.preferences ?? Array.Empty<NpcTravelRequirement>()).Count(requirement =>
                npcs.Count(npc => npc.preference_type == requirement.id) >= requirement.count);
            return count;
        }

        public NpcTravelRow Sample(int mapGroupId, int regionId, int positionId,
            System.Collections.Generic.IReadOnlyList<OfflineNpcTravelPity> pity, System.Random random,
            bool firstAutomatic, bool firstManual)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            int fixedId = firstAutomatic
                ? Data.first_auto_refresh.ElementAtOrDefault(positionId - 1)
                : firstManual ? Data.first_manual_refresh.ElementAtOrDefault(positionId - 1) : 0;
            NpcTravelRow fixedRow = fixedId > 0 ? FindTravel(fixedId) : null;
            if (fixedRow != null) return fixedRow;
            NpcTravelRow[] location = Data.travels.Where(row => row.map_group_id == mapGroupId
                && row.region_id == regionId && row.position_id == positionId && row.weight > 0).ToArray();
            if (location.Length == 0) return null;
            NpcTravelQuality[] qualities = Data.qualities.Where(value => value.refresh_weight > 0).ToArray();
            NpcTravelQuality forced = qualities.Where(value => value.pity_threshold > 0
                    && (pity?.FirstOrDefault(count => count.PositionId == positionId
                        && count.Quality == value.quality)?.Count ?? 0)
                        >= value.pity_threshold - 1)
                .OrderBy(value => value.quality).FirstOrDefault();
            int quality = forced != null ? forced.quality : Weighted(qualities,
                value => value.refresh_weight, random)?.quality ?? 0;
            NpcTravelRow[] options = location.Where(value => value.quality == quality).ToArray();
            if (options.Length == 0) return null;
            return Weighted(options, value => value.weight, random);
        }

        private static T Weighted<T>(T[] values, Func<T, int> weight, System.Random random) where T : class
        {
            long total = values.Sum(value => (long)Math.Max(0, weight(value)));
            if (total <= 0) return null;
            long roll = (long)(random.NextDouble() * total);
            foreach (T value in values)
            {
                roll -= Math.Max(0, weight(value));
                if (roll < 0) return value;
            }
            return values[values.Length - 1];
        }
    }
}
