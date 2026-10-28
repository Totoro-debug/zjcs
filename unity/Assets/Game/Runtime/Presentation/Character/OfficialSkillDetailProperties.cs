using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Zjcs.Combat.Official;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Skills;

namespace Zjcs.Presentation.Character
{
    // PropConfigUtils.CalcSkillViewProps/FillView and SkillPanelPropPanel.SortPropShow.
    internal sealed class OfficialSkillDetailProperties
    {
        internal sealed class Row
        {
            public int Property;
            public string Key, Name, Value;
        }

        private readonly OfficialCharacterCatalog character;
        private readonly FightOfficialCombatCatalog formula;
        private readonly Dictionary<string, object> entities, tables, percentValues, texts;
        private readonly Dictionary<int, Dictionary<string, object>> skills;
        private readonly Dictionary<string, object> propertyNames;

        public OfficialSkillDetailProperties(OfficialCharacterCatalog character, OfficialSkillAssetCatalog assets)
        {
            this.character = character;
            var combat = OfficialCombatAssetCatalog.LoadDefault();
            formula = combat.Load();
            combat.LoadSkillItems();
            var rules = Obj(OfficialEquipmentJson.Parse(combat.Rules.text));
            skills = ((List<object>)rules["skill"]).Select(Obj).ToDictionary(row => Int(row, "ClassId"));
            var data = Obj(OfficialEquipmentJson.Parse(assets.SkillRules.text));
            entities = Obj(data["entities"]); tables = Obj(data["tables"]);
            percentValues = Obj(data["percent_value_props"]);
            propertyNames = Obj(data["prop_names"]);
            texts = Obj(OfficialEquipmentJson.Parse(assets.FidelityTexts.text));
        }

        public List<Row> Calculate(int skillId, int rank, int level, int subRank)
        {
            if (!skills.TryGetValue(skillId, out var skill))
                throw new InvalidOperationException("Original skill view configuration is absent: " + skillId);
            int[] views = List(skill, "ViewPropEntities").Select(Convert.ToInt32).ToArray();
            Dictionary<string, object> addition = null;
            foreach (int id in views)
            {
                var component = Component(id, "FightSkillComponent");
                if (component == null) continue;
                var summonIds = new HashSet<int>();
                var closed = new HashSet<int>();
                foreach (object hit in List(component, "HitList")) CollectSummons(Int(Obj(hit), "ClassId"), summonIds, closed);
                foreach (int hit in summonIds)
                {
                    int summon = SummonId(Component(hit, "FightHitSummonComponent"));
                    addition = Table("summon_monster_add_prop").FirstOrDefault(row => Int(row, "ClassId") == summon);
                    if (addition != null) break;
                }
                if (addition != null) break;
            }
            var rows = new List<Row>();
            Fill(0, formula.CalculatePassiveSkillProps(Int(skill, "PassiveRankPropId"),
                Int(skill, "PassiveGroupLevelPropId"), Int(skill, "PassiveRankFactorId"),
                LongMap(skill, "PassivePropFactors"), rank, level, subRank), addition, rank, rows);
            foreach (int id in views)
            {
                if (Component(id, "FightSkillComponent") != null || Component(id, "FightStatusComponent") != null)
                    Fill(id, formula.CalculateSkillProps(id, rank, level, subRank), addition, rank, rows);
                else
                {
                    int summon = SummonId(Component(id, "FightHitSummonComponent"));
                    var config = Table("summon_monster_fix_prop").FirstOrDefault(row => Int(row, "EntityId") == summon);
                    if (config != null)
                        Fill(id, formula.CalculatePassiveSkillProps(Int(config, "RankPropId"),
                            Int(config, "GroupLevelPropId"), 0, LongMap(config, "PropFactors"), rank, level, subRank), addition, rank, rows);
                }
            }
            foreach (string key in List(skill, "ViewPropBlackList").Select(Convert.ToString))
            {
                Row first = rows.Find(row => row.Key.Contains(key));
                if (first != null) rows.Remove(first);
            }
            rows.Sort((a, b) => {
                int order = character.Props.Get(b.Property).SkillPanelShowOrder - character.Props.Get(a.Property).SkillPanelShowOrder;
                return order == 0 ? b.Property - a.Property : order;
            });
            return rows;
        }

        private void Fill(int entityId, IReadOnlyDictionary<int, long> props,
            Dictionary<string, object> addition, int rank, List<Row> rows)
        {
            foreach (var pair in props)
            {
                OfficialPropDefinition definition = character.Props.Get(pair.Key);
                if (definition == null) throw new InvalidOperationException("Original skill property config is absent: " + pair.Key);
                if (definition.SkillHide) continue;
                string value = pair.Value == 0 ? string.Empty : Format(definition, pair.Value, false);
                if (addition != null)
                {
                    var additions = LongMap(addition, "PropAdditions");
                    if (additions.TryGetValue(pair.Key, out long multiplier))
                    {
                        var factor = Table("summon_rank_additive_factor").FirstOrDefault(row =>
                            Int(row, "RankGroup") == Int(addition, "RankGroup") && Int(row, "Rank") == rank);
                        long rankFactor = factor != null && LongMap(factor, "PropFactors").TryGetValue(pair.Key, out long f) ? f : 0;
                        long percent = (long)((multiplier / 10000.0) * (rankFactor / 10000.0) * 10000.0);
                        value = Format(definition, percent, true) + (pair.Value == 0 ? string.Empty : "+" + Format(definition, pair.Value, false));
                    }
                }
                else if (definition.Percent && percentValues.TryGetValue(pair.Key.ToString(CultureInfo.InvariantCulture), out object rawValueId)
                    && props.TryGetValue(Convert.ToInt32(rawValueId), out long extra) && extra != 0)
                    value += "+" + Format(character.Props.Get(Convert.ToInt32(rawValueId)), extra, false);
                string propName = Convert.ToString(propertyNames[pair.Key.ToString(CultureInfo.InvariantCulture)]);
                string key = entityId > 0 ? entityId.ToString(CultureInfo.InvariantCulture) + "." + propName : null;
                if (key == null || !texts.ContainsKey(key)) key = "PropType." + propName;
                if (addition != null || value.Length > 0)
                    rows.Add(new Row { Property = pair.Key, Key = key,
                        Name = texts.TryGetValue(key, out object name) ? Convert.ToString(name) : key, Value = value });
            }
        }

        private static string Format(OfficialPropDefinition definition, long value, bool percent)
        {
            value = Math.Abs(value);
            return percent ? OfficialPropFormat.Percent(value) : OfficialPropFormat.Value(definition, value);
        }

        private void CollectSummons(int id, HashSet<int> found, HashSet<int> closed)
        {
            if (!closed.Add(id)) return;
            if (Component(id, "FightHitSummonComponent") != null) { found.Add(id); return; }
            foreach (string kind in new[] { "FightHitChainedComponent", "FightHitCustomTargetComponent", "FightHitRandomTargetComponent" })
            {
                var component = Component(id, kind);
                if (component == null) continue;
                foreach (object hit in List(component, kind == "FightHitChainedComponent" ? "NextHitList" : "HitList"))
                    if (!found.Contains(Int(Obj(hit), "ClassId"))) CollectSummons(Int(Obj(hit), "ClassId"), found, closed);
            }
        }

        private static int SummonId(Dictionary<string, object> component)
        {
            if (component == null) return -1;
            var config = Obj(component["SummonCfg"]);
            string kind = Convert.ToString(config["_type"]);
            if (kind != "FightHitSummonPetCfg" && kind != "FightHitSummonMonsterCfg" && kind != "FightHitSummonVoodooDollCfg") return -1;
            int id = Int(config, "SummonId");
            if (id <= 0 && kind == "FightHitSummonMonsterCfg" && config.TryGetValue("SummonPoolSetting", out object raw) && raw != null)
                foreach (object pool in List(Obj(raw), "SummonPools"))
                    if (Obj(pool).TryGetValue("SummonPoolCfg", out object rawPool) && rawPool != null && (id = Int(Obj(rawPool), "SummonId")) > 0) break;
            return id;
        }

        private Dictionary<string, object> Component(int id, string kind) =>
            entities.TryGetValue(id.ToString(CultureInfo.InvariantCulture), out object entity)
            && Obj(entity).TryGetValue(kind, out object component) ? Obj(component) : null;
        private IEnumerable<Dictionary<string, object>> Table(string key) => ((List<object>)tables[key]).Select(Obj);
        private static Dictionary<string, object> Obj(object value) => CharacterModelPackage.AsObject(value, "original skill properties");
        private static int Int(Dictionary<string, object> value, string key) => value.TryGetValue(key, out object raw) && raw != null ? Convert.ToInt32(raw) : 0;
        private static List<object> List(Dictionary<string, object> value, string key) => value.TryGetValue(key, out object raw) && raw is List<object> list ? list : new List<object>();
        private static Dictionary<int, long> LongMap(Dictionary<string, object> value, string key) =>
            value.TryGetValue(key, out object raw) && raw != null ? Obj(raw).ToDictionary(pair => int.Parse(pair.Key, CultureInfo.InvariantCulture), pair => Convert.ToInt64(pair.Value)) : new Dictionary<int, long>();
    }
}
