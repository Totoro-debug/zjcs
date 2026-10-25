using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Zjcs.Offline.Progression
{
    public interface IOfflineCharacterContextualPropSource : IOfflineCharacterPropSource
    {
        IReadOnlyDictionary<int, long> CalculateProps(OfflinePlayerProgress player,
            OfflineWorldProgress world, long nowUnixSeconds);
    }

    /// <summary>Official character property chunks backed by locally persisted feature state.</summary>
    public sealed class OfflineCharacterConfiguredPropSource : IOfflineCharacterContextualPropSource
    {
        private readonly OfficialCharacterCatalog catalog;

        public OfflineCharacterConfiguredPropSource(OfficialCharacterCatalog characterCatalog, int handleType)
        {
            catalog = characterCatalog ?? throw new ArgumentNullException(nameof(characterCatalog));
            HandleType = handleType;
        }

        public int HandleType { get; }

        public IReadOnlyDictionary<int, long> CalculateProps(OfflinePlayerProgress player)
        {
            return CalculateProps(player, null, 0);
        }

        public IReadOnlyDictionary<int, long> CalculateProps(OfflinePlayerProgress player,
            OfflineWorldProgress world, long nowUnixSeconds)
        {
            var result = new Dictionary<int, long>();
            if (player == null) return result;
            OfflineCharacterPropertyProgress state = player.CharacterProperties
                ?? new OfflineCharacterPropertyProgress();
            switch (HandleType)
            {
                case CharacterPropHandleType.Cooking: CalculateCooking(player, state, result); break;
                case CharacterPropHandleType.Extra: CalculateExtra(state, result); break;
                case CharacterPropHandleType.NpcFriendshipLevel: CalculateFriendship(player, state, result); break;
                case CharacterPropHandleType.NpcTalentTree: CalculateNpcTalent(state, result); break;
                case CharacterPropHandleType.Treasure: CalculateTreasures(player, state, result); break;
                case CharacterPropHandleType.TreasureSuit: CalculateTreasureSuits(player, state, result); break;
                case CharacterPropHandleType.LostGoddess: CalculateLostGoddess(world, result); break;
                case CharacterPropHandleType.PeriodIcCar: CalculatePeriodicCards(state, nowUnixSeconds, result); break;
                case CharacterPropHandleType.Legion: CalculateLegion(state, result); break;
                case CharacterPropHandleType.EquipCabinet: CalculateEquipCabinet(state, result); break;
                case CharacterPropHandleType.Astrological: CalculateAstrological(state, result); break;
            }
            return result;
        }

        public long CalculateCombatRating(OfflinePlayerProgress player,
            IReadOnlyDictionary<int, long> props, CharacterPropScoreContext context)
        {
            if (HandleType == CharacterPropHandleType.PeriodIcCar) return 0L;
            return context?.Common(props) ?? 0L;
        }

        private void CalculateCooking(OfflinePlayerProgress player, OfflineCharacterPropertyProgress state,
            Dictionary<int, long> result)
        {
            foreach (OfflineCookingUse use in state.CookingUses)
            {
                if (use == null || use.UseTimes <= 0) continue;
                Dictionary<string, object> cooking = catalog.Find("cooking", use.CookingId, "Id");
                Dictionary<string, object> item = catalog.Find("item", use.CookingId);
                if (cooking == null || item == null || !cooking.TryGetValue("EffectInfos", out object rawEffects)
                    || !(rawEffects is Dictionary<string, object> effects)) continue;
                foreach (object rawEffect in effects.Values)
                {
                    if (!(rawEffect is Dictionary<string, object> effect)
                        || !string.Equals(Text(effect, "_type"), "CookingExtraInfoProp", StringComparison.Ordinal))
                        continue;
                    int propId = Integer(effect, "PropId");
                    int rank = Math.Max(1, Integer(item, "Rank"));
                    int[] propTypes = Integers(effect, "PropTypes");
                    IReadOnlyDictionary<int, long> baseProps = catalog.GetLevelProps(propId, rank);
                    foreach (int propType in propTypes)
                    {
                        if (!baseProps.TryGetValue(propType, out long baseValue)) continue;
                        long total = 0;
                        foreach (Dictionary<string, object> tier in catalog.Rows("cooking_prop_effect_full")
                                     .Where(row => Integer(row, "CookingId") == use.CookingId)
                                     .OrderBy(row => Integer(row, "StartCount")))
                        {
                            long start = Long(tier, "StartCount");
                            long end = Long(tier, "EndCount");
                            if (use.UseTimes < start) break;
                            long count = end > 0 && use.UseTimes > end
                                ? end - start + 1 : use.UseTimes - start + 1;
                            if (count <= 0) break;
                            int efficiency = Integer(tier, "Efficiency");
                            long perUse = (long)((float)baseValue * (efficiency / 10000f));
                            total = checked(total + checked(perUse * count));
                        }
                        Add(result, propType, total);
                    }
                }
            }
        }

        private void CalculateExtra(OfflineCharacterPropertyProgress state, Dictionary<int, long> result)
        {
            foreach (OfflineExtraProperty extra in state.ExtraProperties)
            {
                if (extra == null || extra.Count <= 0) continue;
                long prop = Get(catalog.GetLevelProps(extra.PropId, extra.Level), extra.PropType);
                Add(result, extra.PropType, checked(prop * extra.Count));
            }
        }

        private void CalculateFriendship(OfflinePlayerProgress player, OfflineCharacterPropertyProgress state,
            Dictionary<int, long> result)
        {
            Dictionary<int, long> treasureScales = TreasureNpcFriendshipScales(player, state);
            foreach (OfflineNpcFriendship friendship in state.NpcFriendships)
            {
                if (friendship == null || friendship.Level <= 0) continue;
                Dictionary<string, object> npc = catalog.Find("npc", friendship.NpcClassId, "Id");
                if (npc == null) continue;
                IReadOnlyDictionary<int, long> props = catalog.GetLevelProps(Integer(npc, "LevelPropId"),
                    friendship.Level);
                CharacterPropFormula.Merge(result, props);
                if (!treasureScales.TryGetValue(friendship.NpcClassId, out long scale) || scale == 0) continue;
                foreach (KeyValuePair<int, long> property in props)
                    Add(result, property.Key, (long)(property.Value * (scale / 10000.0)));
            }
        }

        private Dictionary<int, long> TreasureNpcFriendshipScales(OfflinePlayerProgress player,
            OfflineCharacterPropertyProgress state)
        {
            var result = new Dictionary<int, long>();
            Dictionary<string, object> settings = catalog.Rows("game_settings").FirstOrDefault();
            Dictionary<string, object> qualityRows = Map(settings, "TreasureNpcFriendshipQuality");
            foreach (OfflineCharacterTreasure owned in OwnedTreasures(player, state))
            {
                if (owned == null || !owned.Active) continue;
                Dictionary<string, object> treasure = catalog.Find("treasure", owned.ClassId);
                Dictionary<string, object> item = catalog.Find("item", owned.ClassId);
                int quality = Integer(item, "Quality");
                if (treasure == null || !qualityRows.TryGetValue(quality.ToString(CultureInfo.InvariantCulture),
                        out object rawScale)) continue;
                long scale = Convert.ToInt64(rawScale, CultureInfo.InvariantCulture);
                foreach (int npcClassId in Integers(treasure, "AddNpcFriendshipPropIds"))
                    result.TryAdd(npcClassId, scale);
            }
            return result;
        }

        private void CalculateNpcTalent(OfflineCharacterPropertyProgress state, Dictionary<int, long> result)
        {
            foreach (OfflineNpcTalentTree tree in state.NpcTalentTrees)
            {
                if (tree == null) continue;
                foreach (int nodeId in tree.ActiveNodeIds)
                {
                    Dictionary<string, object> node = catalog.Rows("npc_talent_tree_prop").FirstOrDefault(row =>
                        Integer(row, "NpcClassId") == tree.NpcClassId && Integer(row, "NodeId") == nodeId);
                    if (node == null) continue;
                    HashSet<int> allowedTypes = new HashSet<int>(Integers(node, "PropTypes"));
                    foreach (KeyValuePair<int, long> prop in catalog.GetLevelProps(Integer(node, "PropId"),
                                 Integer(node, "PropLevel")))
                        if (allowedTypes.Contains(prop.Key)) Add(result, prop.Key, prop.Value);
                }
            }
        }

        private void CalculateTreasures(OfflinePlayerProgress player, OfflineCharacterPropertyProgress state,
            Dictionary<int, long> result)
        {
            Dictionary<string, object> settings = catalog.Rows("game_settings").FirstOrDefault();
            int planeRank = catalog.RankOfSubRank(player.SubRank);
            bool useFinalSlotLevels = planeRank > 0 && planeRank == state.TreasurePlaneRank;
            double slotRate = Number(settings, "TreasureSlotPropAddRate");
            Dictionary<string, object> subRank = catalog.Rows("player_subrank").FirstOrDefault(row =>
                Integer(row, "SubRank") == player.SubRank);
            int treasureBlessClass = Integer(subRank, "TreasurePropBless");

            foreach (OfflineCharacterTreasure owned in OwnedTreasures(player, state))
            {
                if (owned == null || !owned.Active) continue;
                Dictionary<string, object> treasure = catalog.Find("treasure", owned.ClassId);
                Dictionary<string, object> item = catalog.Find("item", owned.ClassId);
                if (treasure == null || item == null) continue;
                int element = Integer(item, "ElementType");
                bool inSlot = owned.SlotIndex >= 0;
                int finalLevel = owned.BaseLevel;
                int baseLevel = owned.BaseLevel;
                if (useFinalSlotLevels && inSlot)
                {
                    finalLevel = owned.Level;
                    baseLevel = owned.BaseLevel;
                }
                else if (useFinalSlotLevels)
                {
                    OfflineTreasureElementLevel shared = state.TreasureElementLevels.FirstOrDefault(value =>
                        value != null && value.ElementType == element);
                    if (shared != null)
                    {
                        finalLevel = shared.Level;
                        baseLevel = shared.BaseLevel;
                    }
                }

                int star = Math.Max(0, Math.Min(Integer(treasure, "StarMax"), owned.Star));
                Dictionary<string, object> seasonIds = Map(treasure, "LevelPropId");
                int levelPropId = Integer(seasonIds, state.SeasonId.ToString(CultureInfo.InvariantCulture));
                if (levelPropId <= 0) levelPropId = Integer(seasonIds, "0");
                if (levelPropId <= 0) levelPropId = seasonIds.Values.Select(value =>
                    Convert.ToInt32(value, CultureInfo.InvariantCulture)).FirstOrDefault();
                if (levelPropId <= 0) continue;

                HashSet<int> levelTypes = new HashSet<int>(Integers(treasure, "LevelPropTypes"));
                HashSet<int> starTypes = new HashSet<int>(Integers(treasure, "StarPropTypes"));
                HashSet<int> effectTypes = new HashSet<int>(Integers(treasure, "EffectPropTypes"));
                IReadOnlyDictionary<int, long> starFactors = catalog.GetLevelProps(
                    Integer(treasure, "StarScalePropId"), star);
                int hitMax = HitCountMaximum(owned.ClassId, finalLevel);
                int hitCount = Math.Min(owned.HitCount, hitMax);
                CalculateTreasurePart(levelTypes, levelPropId, baseLevel, finalLevel, treasureBlessClass,
                    starFactors, effectTypes, hitMax, hitCount, inSlot, slotRate, result);
                CalculateTreasurePart(starTypes, levelPropId, baseLevel, finalLevel, treasureBlessClass,
                    starFactors, effectTypes, hitMax, hitCount, inSlot, slotRate, result);
            }
        }

        private void CalculateTreasurePart(IEnumerable<int> propTypes, int levelPropId, int baseLevel,
            int finalLevel, int treasureBlessClass, IReadOnlyDictionary<int, long> starFactors,
            HashSet<int> effectTypes, int hitMax, int hitCount, bool inSlot, double slotRate,
            Dictionary<int, long> result)
        {
            foreach (int propType in propTypes)
            {
                long prop = Get(catalog.GetLevelProps(levelPropId, baseLevel), propType);
                if (finalLevel > baseLevel && treasureBlessClass > 0)
                {
                    long bless = Get(catalog.GetLevelProps(treasureBlessClass, finalLevel - baseLevel), propType);
                    prop = (long)(prop * (1.0 + bless / 10000.0));
                }
                if (starFactors.TryGetValue(propType, out long factor))
                    prop = (long)(prop * (factor / 10000.0));
                if (effectTypes.Contains(propType))
                {
                    long perHit = prop / hitMax;
                    if (perHit == 0) perHit = 1;
                    prop = checked(perHit * hitCount);
                }
                if (inSlot) prop = checked(prop + (long)((float)prop * (float)slotRate));
                Add(result, propType, prop);
            }
        }

        private void CalculateTreasureSuits(OfflinePlayerProgress player,
            OfflineCharacterPropertyProgress state, Dictionary<int, long> result)
        {
            foreach (Dictionary<string, object> suit in catalog.Rows("treasure_suit"))
            {
                int[] treasureIds = Integers(suit, "Treasures");
                if (treasureIds.Length == 0) continue;
                var active = new List<OfflineCharacterTreasure>();
                foreach (int treasureId in treasureIds)
                {
                    OfflineCharacterTreasure treasure = OwnedTreasures(player, state).FirstOrDefault(value =>
                        value != null && value.ClassId == treasureId && value.Active);
                    if (treasure != null) active.Add(treasure);
                }
                var conditions = new HashSet<int>();
                if (active.Count == treasureIds.Length) conditions.Add(0);
                if (active.Count == treasureIds.Length && active.All(value => value.Star >= 3)) conditions.Add(1);
                if (active.Count == treasureIds.Length && active.All(value =>
                        value.Star >= Integer(catalog.Find("treasure", value.ClassId), "StarMax"))) conditions.Add(2);

                Dictionary<string, object> propClasses = Map(suit, "Props");
                int[][] propTypesByCondition = IntegerLists(suit, "PropTypes");
                foreach (int condition in conditions)
                {
                    if (!propClasses.TryGetValue(condition.ToString(CultureInfo.InvariantCulture), out object rawClass))
                        continue;
                    int propClass = Convert.ToInt32(rawClass, CultureInfo.InvariantCulture);
                    if (condition < 0 || condition >= propTypesByCondition.Length) continue;
                    HashSet<int> types = new HashSet<int>(propTypesByCondition[condition]);
                    foreach (KeyValuePair<int, long> prop in catalog.GetLevelProps(propClass, 0))
                        if (types.Contains(prop.Key)) Add(result, prop.Key, prop.Value);
                }
            }
        }

        private List<OfflineCharacterTreasure> OwnedTreasures(OfflinePlayerProgress player,
            OfflineCharacterPropertyProgress state)
        {
            var result = (state.Treasures ?? new List<OfflineCharacterTreasure>())
                .Where(value => value != null).ToList();
            var known = new HashSet<int>(result.Select(value => value.ClassId));
            foreach (OfflineItemStack stack in player?.Inventory ?? new List<OfflineItemStack>())
            {
                if (stack == null || stack.Count <= 0 || known.Contains(stack.ItemId)) continue;
                Dictionary<string, object> info = catalog.Find("treasure", stack.ItemId);
                if (info == null) continue;
                result.Add(new OfflineCharacterTreasure
                {
                    ClassId = stack.ItemId,
                    Active = true,
                    Star = 0,
                    HitCount = string.Equals(Text(info, "EffectTypeName"), "Task", StringComparison.Ordinal)
                        ? 0 : 1,
                    SlotIndex = -1,
                    Level = 1,
                    BaseLevel = 1
                });
                known.Add(stack.ItemId);
            }
            return result;
        }

        private int HitCountMaximum(int treasureId, int level)
        {
            Dictionary<string, object> effect = catalog.Rows("treasure_task_effects").FirstOrDefault(row =>
                Integer(row, "ClassId") == treasureId);
            if (effect == null) return 1;
            int[] perLevel = Integers(effect, "LevelHitCount");
            if (perLevel.Length == 0) return Math.Max(1, Integer(effect, "MaxHitCount"));
            return perLevel[Math.Max(0, Math.Min(perLevel.Length - 1, level - 1))];
        }

        private void CalculateLostGoddess(OfflineWorldProgress world, Dictionary<int, long> result)
        {
            foreach (OfflineMapRegionKey activated in world?.ActivatedGoddessRegions
                         ?? new List<OfflineMapRegionKey>())
            {
                if (activated == null) continue;
                Dictionary<string, object> region = catalog.Rows("map_region").FirstOrDefault(row =>
                    Integer(row, "ClassId") == activated.MapClassId && Integer(row, "Region") == activated.Region);
                CharacterPropFormula.Merge(result, IntegerLongMap(region == null ? null
                    : Value(region, "GoddessActivePropAwardDict")));
            }
        }

        private void CalculatePeriodicCards(OfflineCharacterPropertyProgress state, long nowUnixSeconds,
            Dictionary<int, long> result)
        {
            foreach (OfflinePeriodicCard card in state.PeriodicCards)
            {
                if (card == null || card.ExpiresAtUnixSeconds <= nowUnixSeconds) continue;
                Dictionary<string, object> info = catalog.Find("periodic_card_pay", card.CardId, "Id");
                CharacterPropFormula.Merge(result, IntegerLongMap(info == null ? null : Value(info, "ExtraProps")));
            }
        }

        private void CalculateLegion(OfflineCharacterPropertyProgress state, Dictionary<int, long> result)
        {
            foreach (OfflineLegionMapSkill skill in state.LegionMapSkills)
            {
                if (skill == null || skill.Level <= 0) continue;
                Dictionary<string, object> row = catalog.Rows("legion_map_skill_study_level").FirstOrDefault(value =>
                    Integer(value, "SkillClassId") == skill.SkillClassId && Integer(value, "Level") == skill.Level);
                CharacterPropFormula.Merge(result, IntegerLongMap(row == null ? null : Value(row, "PlayerProps")));
            }
        }

        private void CalculateEquipCabinet(OfflineCharacterPropertyProgress state, Dictionary<int, long> result)
        {
            foreach (OfflineEquipCabinetSeasonScore seasonScore in state.EquipCabinetScores)
            {
                if (seasonScore == null) continue;
                Dictionary<string, object> season = catalog.Rows("astrological_season_config").FirstOrDefault(row =>
                    Integer(row, "Season") == seasonScore.SeasonId);
                if (season == null) continue;
                int rank = Integer(season, "Rank");
                foreach (Dictionary<string, object> threshold in catalog.Rows("equip_cabinet_evaluate")
                             .Where(row => Integer(row, "Rank") == rank
                                 && Integer(row, "Score") <= seasonScore.Score))
                    CharacterPropFormula.Merge(result,
                        IntegerLongMap(threshold == null ? null : Value(threshold, "Attribuate")));
            }
        }

        private void CalculateAstrological(OfflineCharacterPropertyProgress state, Dictionary<int, long> result)
        {
            long materialCount = 0;
            foreach (Dictionary<string, object> level in catalog.Rows("astrological_level")
                         .OrderBy(row => Integer(row, "Level")))
            {
                materialCount = checked(materialCount + Long(level, "MaterialAmount"));
                if (materialCount > state.AstrologicalCost) break;
                CharacterPropFormula.Merge(result,
                    IntegerLongMap(level == null ? null : Value(level, "Attribuate")));
            }
        }

        private static Dictionary<int, long> IntegerLongMap(object raw)
        {
            var result = new Dictionary<int, long>();
            if (!(raw is Dictionary<string, object> values)) return result;
            foreach (KeyValuePair<string, object> entry in values)
            {
                int key = int.Parse(entry.Key, CultureInfo.InvariantCulture);
                long value = Convert.ToInt64(entry.Value, CultureInfo.InvariantCulture);
                if (key > 0 && value != 0) result[key] = value;
            }
            return result;
        }

        private static Dictionary<string, object> Map(Dictionary<string, object> row, string key)
        {
            return row != null && row.TryGetValue(key, out object value)
                ? value as Dictionary<string, object> : null;
        }

        private static object Value(Dictionary<string, object> row, string key)
        {
            return row != null && row.TryGetValue(key, out object value) ? value : null;
        }

        private static long Get(IReadOnlyDictionary<int, long> props, int property)
        {
            return property > 0 && props != null && props.TryGetValue(property, out long value) ? value : 0L;
        }

        private static void Add(Dictionary<int, long> props, int property, long value)
        {
            if (property <= 0 || value == 0) return;
            props[property] = props.TryGetValue(property, out long current) ? checked(current + value) : value;
        }

        private static int Integer(Dictionary<string, object> row, string key)
        {
            return row != null && row.TryGetValue(key, out object value)
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        }

        private static long Long(Dictionary<string, object> row, string key)
        {
            return row != null && row.TryGetValue(key, out object value)
                ? Convert.ToInt64(value, CultureInfo.InvariantCulture) : 0L;
        }

        private static double Number(Dictionary<string, object> row, string key)
        {
            return row != null && row.TryGetValue(key, out object value)
                ? Convert.ToDouble(value, CultureInfo.InvariantCulture) : 0.0;
        }

        private static string Text(Dictionary<string, object> row, string key)
        {
            return row != null && row.TryGetValue(key, out object value)
                ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty : string.Empty;
        }

        private static int[] Integers(Dictionary<string, object> row, string key)
        {
            return row != null && row.TryGetValue(key, out object value) && value is List<object> values
                ? values.Select(item => Convert.ToInt32(item, CultureInfo.InvariantCulture)).ToArray()
                : Array.Empty<int>();
        }

        private static int[][] IntegerLists(Dictionary<string, object> row, string key)
        {
            return row != null && row.TryGetValue(key, out object value) && value is List<object> lists
                ? lists.Select(list => list is List<object> values
                    ? values.Select(item => Convert.ToInt32(item, CultureInfo.InvariantCulture)).ToArray()
                    : Array.Empty<int>()).ToArray()
                : Array.Empty<int[]>();
        }
    }
}
