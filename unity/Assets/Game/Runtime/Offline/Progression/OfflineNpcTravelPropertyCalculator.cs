using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    public sealed class OfflineNpcTravelPropertyCalculator
    {
        private readonly OfficialCharacterCatalog characterCatalog;
        private readonly OfficialNpcTravelCatalog travelCatalog;
        private readonly HashSet<int> travelPropTypes;

        public OfflineNpcTravelPropertyCalculator(OfficialCharacterCatalog characterCatalog,
            OfficialNpcTravelCatalog travelCatalog)
        {
            this.characterCatalog = characterCatalog ?? throw new ArgumentNullException(nameof(characterCatalog));
            this.travelCatalog = travelCatalog ?? throw new ArgumentNullException(nameof(travelCatalog));
            travelPropTypes = new HashSet<int>(travelCatalog.Data.travel_npc_prop_types);
        }

        public IReadOnlyList<int> QuickSelect(OfflinePlayerProgress player, int travelId,
            IEnumerable<int> eligibleNpcIds)
        {
            NpcTravelRow travel = travelCatalog.FindTravel(travelId);
            NpcTravelQuality quality = travel == null ? null : travelCatalog.Quality(travel.quality);
            if (player == null || travel == null || quality == null || eligibleNpcIds == null)
                return Array.Empty<int>();
            var candidates = eligibleNpcIds.Where(id => id > 0).Distinct().OrderBy(id => id).ToList();
            var professions = Requirements(travel.professions);
            var mapGroups = Requirements(travel.map_groups);
            var preferences = Requirements(travel.preferences);
            var selected = new List<int>(quality.npc_count);
            for (int index = 0; index < quality.npc_count && candidates.Count > 0; index++)
            {
                int bestId = candidates[0];
                double bestProbability = 0.0;
                foreach (int npcId in candidates)
                {
                    NpcTravelNpc npc = travelCatalog.Npc(npcId);
                    if (npc == null) continue;
                    Calculate(player, npcId).TryGetValue(6012, out long npcProbabilityAdd);
                    int meets = Meets(npc, professions, mapGroups, preferences);
                    double probability = npcProbabilityAdd / 10000.0
                        + (double)meets * quality.extra_condition_add / 10000.0;
                    if (probability > bestProbability)
                    {
                        bestProbability = probability;
                        bestId = npcId;
                    }
                }

                NpcTravelNpc chosen = travelCatalog.Npc(bestId);
                if (chosen == null) break;
                selected.Add(bestId);
                candidates.Remove(bestId);
                Decrement(professions, chosen.travel_profession);
                Decrement(mapGroups, chosen.map_group_id);
                Decrement(preferences, chosen.preference_type);
            }
            return selected;
        }

        public IReadOnlyDictionary<int, long> Calculate(OfflinePlayerProgress player, int npcClassId)
        {
            if (player == null || npcClassId <= 0) return new Dictionary<int, long>();
            OfflineCharacterPropertyProgress state = player.CharacterProperties
                ?? new OfflineCharacterPropertyProgress();
            OfflineNpcFriendship friendship = (state.NpcFriendships ?? new List<OfflineNpcFriendship>())
                .FirstOrDefault(value => value != null && value.NpcClassId == npcClassId);
            Dictionary<string, object> npc = characterCatalog.Find("npc", npcClassId, "Id");
            if (friendship == null || npc == null) return new Dictionary<int, long>();

            var result = new Dictionary<int, long>();
            Merge(result, characterCatalog.GetLevelProps(Integer(npc, "LevelPropId"), friendship.Level));
            foreach (int nodeId in (state.NpcTalentTrees ?? new List<OfflineNpcTalentTree>())
                         .Where(value => value != null && value.NpcClassId == npcClassId)
                         .SelectMany(value => value.ActiveNodeIds ?? new List<int>()))
            {
                Dictionary<string, object> node = characterCatalog.Rows("npc_talent_tree_prop")
                    .FirstOrDefault(row => Integer(row, "NpcClassId") == npcClassId
                        && Integer(row, "NodeId") == nodeId);
                if (node == null) continue;
                HashSet<int> allowed = new HashSet<int>(Integers(node, "PropTypes"));
                foreach (KeyValuePair<int, long> property in characterCatalog.GetLevelProps(
                             Integer(node, "PropId"), Integer(node, "PropLevel")))
                    if (allowed.Contains(property.Key)) Add(result, property.Key, property.Value);
            }

            return result.Where(value => travelPropTypes.Contains(value.Key))
                .ToDictionary(value => value.Key, value => value.Value);
        }

        private static int Integer(Dictionary<string, object> row, string key)
        {
            return row != null && row.TryGetValue(key, out object value)
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        }

        private static IEnumerable<int> Integers(Dictionary<string, object> row, string key)
        {
            return row != null && row.TryGetValue(key, out object value) && value is IEnumerable<object> values
                ? values.Select(item => Convert.ToInt32(item, CultureInfo.InvariantCulture)) : Array.Empty<int>();
        }

        private static Dictionary<int, int> Requirements(NpcTravelRequirement[] requirements)
        {
            return (requirements ?? Array.Empty<NpcTravelRequirement>())
                .Where(value => value != null && value.id > 0 && value.count > 0)
                .ToDictionary(value => value.id, value => value.count);
        }

        private static int Meets(NpcTravelNpc npc, IReadOnlyDictionary<int, int> professions,
            IReadOnlyDictionary<int, int> mapGroups, IReadOnlyDictionary<int, int> preferences)
        {
            int count = 0;
            if (professions.TryGetValue(npc.travel_profession, out int professionCount) && professionCount == 1) count++;
            if (mapGroups.TryGetValue(npc.map_group_id, out int mapCount) && mapCount == 1) count++;
            if (preferences.TryGetValue(npc.preference_type, out int preferenceCount) && preferenceCount == 1) count++;
            return count;
        }

        private static void Decrement(Dictionary<int, int> requirements, int id)
        {
            if (!requirements.TryGetValue(id, out int count)) return;
            if (count <= 1) requirements.Remove(id);
            else requirements[id] = count - 1;
        }

        private static void Merge(Dictionary<int, long> destination, IReadOnlyDictionary<int, long> source)
        {
            foreach (KeyValuePair<int, long> property in source) Add(destination, property.Key, property.Value);
        }

        private static void Add(Dictionary<int, long> destination, int propertyType, long value)
        {
            destination[propertyType] = destination.TryGetValue(propertyType, out long current)
                ? checked(current + value) : value;
        }
    }
}
