using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using R = EquipmentSelectionReference;

// Narrow validation adapters: selection uses real saved equipment models and
// both implementations receive the same controlled scoring function. Production
// score-context construction and battle-host binding are checked in Unity.
namespace Zjcs.Offline.Progression
{
    public interface IOfflineCharacterPropSource { }
    public interface IOfflineCharacterScoredPropSource : IOfflineCharacterPropSource { }
    public static class CharacterPropHandleType { public const int Equip = 3, EquipResonance = 4; }
    public sealed class CharacterPropScoreContext
    {
        public long Common(IReadOnlyDictionary<int, long> values)
        { return values == null ? 0 : values.Sum(value => value.Value * (value.Key % 7 + 1)); }
    }
    public static class CharacterPropFormula
    {
        public static void Merge(Dictionary<int, long> result, IReadOnlyDictionary<int, long> values)
        { foreach (var value in values) { result.TryGetValue(value.Key, out long old); result[value.Key] = old + value.Value; } }
    }
    public sealed class OfflineWardrobeProgress { public int Profession; }
    public sealed class OfflinePlayerProgress
    {
        public int Level = 80, SubRank = 4;
        public OfflineEquipmentProfile Equipment = new OfflineEquipmentProfile();
        public OfflineWardrobeProgress Wardrobe = new OfflineWardrobeProgress();
    }
}

public static class EquipmentSelectionChecks
{
    private static int Int(Dictionary<string, object> row, string key)
    { return row != null && row.TryGetValue(key, out object value) ? Convert.ToInt32(value) : 0; }
    private static bool Bool(Dictionary<string, object> row, string key)
    { return row != null && row.TryGetValue(key, out object value) && Convert.ToBoolean(value); }
    private static List<object> List(Dictionary<string, object> row, string key)
    { return row != null && row.TryGetValue(key, out object value) ? value as List<object> : null; }
    private static Dictionary<string, object> Map(Dictionary<string, object> row, string key)
    { return row != null && row.TryGetValue(key, out object value) ? value as Dictionary<string, object> : null; }
    private static Dictionary<R.PropType, long> Props(Dictionary<string, object> row)
    { return row?.ToDictionary(value => (R.PropType)int.Parse(value.Key), value => Convert.ToInt64(value.Value)); }
    private static List<R.EquipAttributeConfig> Configs(List<object> rows)
    { return rows?.Select(value => { var row = (Dictionary<string, object>)value; return new R.EquipAttributeConfig {
        Type = (R.ValueType)Int(row, "Type"), Values = List(row, "Values")?.Select(Convert.ToInt64).ToList() }; }).ToList(); }

    private static void ConfigureReference(OfficialEquipmentCatalog catalog)
    {
        var parsers = new R.Parsers(); R.Parsers.Instance = parsers;
        foreach (var row in catalog.Rows("equip_attributes"))
            parsers.EquipAttributeInfoParser.Rows.Add(Int(row, "Id"), new R.EquipAttributeInfo {
                Id = Int(row, "Id"), Type = (R.EquipAttributeType)Int(row, "Type"),
                ExclusiveGroup = Int(row, "ExclusiveGroup"), SelfExclusive = Bool(row, "SelfExclusive"),
                IsApplyInBattle = Bool(row, "IsApplyInBattle"), EquivalentProps = Props(Map(row, "EquivalentProps")) });
        foreach (var row in catalog.Rows("equip_attributes_props"))
            parsers.EquipAttributesPropsInfoParser.Rows.Add(Int(row, "Id"), new R.EquipAttributesPropsInfo {
                Props = List(row, "Props")?.Select(value => Props((Dictionary<string, object>)value)).ToList(),
                PropBlessScaleFactorId = Int(row, "PropBlessScaleFactorId") });
        foreach (var row in catalog.Rows("equip_attributes_passive_skill"))
            parsers.EquipAttributesPassiveSkillInfoParser.Rows.Add(Int(row, "Id"), new R.EquipAttributesPassiveSkillInfo {
                Id = Int(row, "Id"), Props = Configs(List(row, "Props")), PropsFactors = Configs(List(row, "PropsFactors")),
                Qualitys = List(row, "Qualitys")?.Select(value => (R.Quality)Convert.ToInt32(value)).ToList(),
                QualityFactors = List(row, "QualityFactors")?.Select(Convert.ToInt64).ToList(),
                PropBlessScaleFactorIds = Map(row, "PropBlessScaleFactorIds")?.ToDictionary(
                    value => (R.PropType)int.Parse(value.Key), value => Convert.ToInt32(value.Value)),
                SkillPropMapFactorIds = Map(row, "SkillPropMapFactorIds")?.ToDictionary(value => int.Parse(value.Key),
                    value => ((Dictionary<string, object>)value.Value).ToDictionary(
                        prop => (R.PropType)int.Parse(prop.Key), prop => Convert.ToInt32(prop.Value))) });
        foreach (var row in catalog.Rows("equip_suit"))
            if (!parsers.EquipSuitInfoParser.Rows.ContainsKey(Int(row, "SuitId")))
                parsers.EquipSuitInfoParser.Rows.Add(Int(row, "SuitId"), new R.EquipSuitInfo {
                    SuitAttributes = Map(row, "SuitAttributes").ToDictionary(value => int.Parse(value.Key), value => Convert.ToInt32(value.Value)) });
        parsers.EquipAttributeBlessScaleInfoParser.Resolve = (id, rank, level) => {
            var row = id == 0 ? null : catalog.Rows("equip_attribute_bless_scale").FirstOrDefault(value =>
                Int(value, "Id") == id && Int(value, "Rank") == (int)rank && Int(value, "Level") == level);
            return row == null ? 1d : Int(row, "PropFactor") / 10000d;
        };
    }

    private static R.EquipPropBase ReferenceTerm(OfflineEquipmentTerm term)
    {
        if (term.Type == 4 || term.Type == 5)
            return new R.EquipPassiveSkillPropValue((R.EquipAttributeType)term.Type, term.PropertyId,
                term.Indices, (R.Quality)term.SkillQuality);
        return new R.EquipSpecialPropValue((R.EquipAttributeType)term.Type, term.PropertyId, term.Group);
    }

    private static List<OfflineEquipmentBattleTerm> Compare(OfficialEquipmentCatalog catalog, OfflinePlayerProgress player)
    {
        ConfigureReference(catalog);
        var equips = new List<R.PlayerItemDataWrap>();
        foreach (var slot in player.Equipment.Equipped)
        {
            var instance = player.Equipment.Instances.FirstOrDefault(value => value.InstanceId == slot.InstanceId);
            if (instance == null) continue;
            var info = new R.ItemInfo { LimitRank = (R.Rank)Int(catalog.Find("item", instance.ItemId), "LimitRank") };
            var param = new R.ItemParamEquip { EquipSuitId = instance.SuitId, BlessLevel = instance.BlessLevel };
            for (int i = 0; i < instance.Terms.Count; i++)
                param.OtherProps.Add(i, new R.PlayerItemEquipPropBaseWrap { Data = ReferenceTerm(instance.Terms[i]) });
            equips.Add(new R.PlayerItemDataWrap { InstanceId = instance.InstanceId, Info = info,
                Data = new R.ItemData { Info = info }, Param = param });
        }
        var context = new CharacterPropScoreContext();
        var reference = new R.CombatRatingRefDataBase { Common = values => context.Common(
            values.ToDictionary(value => (int)value.Key, value => value.Value)) };
        R.BattleFormula.Instance.CheckEquipsAllEffectTerm(player.Level, (R.SubRank)player.SubRank,
            (R.Rank)player.SubRank, equips, reference, true, out var expected);
        var actual = new OfflineEquipmentPropSource(catalog, value => value).CollectBattlePassiveTerms(player, context);
        var expectedKeys = expected.Where(value => value.Key is R.EquipPassiveSkillPropValue)
            .Select(value => value.Key.PropId + ":" + (value.Value?.InstanceId ?? 0)).OrderBy(value => value).ToArray();
        var actualKeys = actual.Select(value => value.AttributeId + ":" + value.EquipmentInstanceId).OrderBy(value => value).ToArray();
        Require(expectedKeys.SequenceEqual(actualKeys), "Selection differs: expected=" + string.Join(",", expectedKeys)
            + "; actual=" + string.Join(",", actualKeys));
        return actual;
    }

    private static OfflineEquipmentTerm Term(OfficialEquipmentCatalog catalog, int id, Random random = null)
    {
        var attr = catalog.Find("equip_attributes", id, "Id"); int type = Int(attr, "Type");
        var result = new OfflineEquipmentTerm { Type = type, PropertyId = id };
        if (type == 4 || type == 5)
        {
            var row = catalog.Find("equip_attributes_passive_skill", id, "Id");
            result.SkillQuality = Convert.ToInt32(List(row, "Qualitys")[0]);
            result.Indices = (List(row, "Props") ?? new List<object>()).Select(value => random == null ? 0
                : random.Next(List((Dictionary<string, object>)value, "Values").Count)).ToList();
        }
        return result;
    }

    private static void Equip(OfflinePlayerProgress player, int itemId, int bless, int level,
        int suit, params OfflineEquipmentTerm[] terms)
    {
        long id = player.Equipment.Instances.Count + 1;
        player.Equipment.Instances.Add(new OfflineEquipmentInstance { InstanceId = id, ItemId = itemId,
            BlessLevel = bless, ItemLevel = level, SuitId = suit, Terms = terms.ToList() });
        player.Equipment.Equipped.Add(new OfflineEquippedSlot { InstanceId = id, Position = 9 + (int)id });
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    public static string Run(string json)
    {
        var catalog = OfficialEquipmentCatalog.Load(json);
        int item = catalog.Rows("equip").Select(row => Int(row, "ClassId"))
            .First(id => Int(catalog.Find("item", id), "LimitRank") == 4);
        int other = catalog.Rows("equip").Select(row => Int(row, "ClassId"))
            .First(id => Int(catalog.Find("item", id), "LimitRank") != 4);
        int comparisons = 0;
        int[] ids = catalog.Rows("equip_attributes").Where(row => Int(row, "Type") >= 2)
            .Select(row => Int(row, "Id")).ToArray();
        foreach (int id in ids)
        {
            var player = new OfflinePlayerProgress(); Equip(player, item, 1, 40, 0, Term(catalog, id));
            Compare(catalog, player); comparisons++;
        }
        var random = new Random(7291);
        for (int test = 0; test < 400; test++)
        {
            var player = new OfflinePlayerProgress();
            for (int n = 0; n < 5; n++)
                Equip(player, n == 3 ? other : item, random.Next(3), 20 + n, 0,
                    Enumerable.Range(0, 4).Select(_ => Term(catalog, ids[random.Next(ids.Length)], random)).ToArray());
            Compare(catalog, player); comparisons++;
        }
        foreach (var suit in catalog.Rows("equip_suit"))
            foreach (var threshold in Map(suit, "SuitAttributes"))
            {
                int count = int.Parse(threshold.Key); var player = new OfflinePlayerProgress();
                for (int n = 0; n < count; n++) Equip(player, item, 0, 31 + n * 2, Int(suit, "SuitId"));
                var selected = Compare(catalog, player); comparisons++;
                foreach (var term in selected)
                    Require(term.SuitId == Int(suit, "SuitId") && term.SkillLevel == 31 + count - 1,
                        "Suit mean effective level or source was lost.");
                Equip(player, other, 2, 90, Int(suit, "SuitId"));
                Compare(catalog, player); comparisons++;
            }

        // Controlled branches keep the original code as the oracle. In particular,
        // using the incumbent equipment's blessing would incorrectly keep term A.
        var a = catalog.Find("equip_attributes", 40000, "Id");
        var b = catalog.Find("equip_attributes", 40001, "Id");
        a["ExclusiveGroup"] = b["ExclusiveGroup"] = 99999;
        a["EquivalentProps"] = new Dictionary<string, object> { { "68", 100L } };
        b["EquivalentProps"] = new Dictionary<string, object> { { "68", 110L } };
        a["IsApplyInBattle"] = b["IsApplyInBattle"] = true;
        var pair = new OfflinePlayerProgress();
        Equip(pair, item, 1, 40, 0, Term(catalog, 40000));
        Equip(pair, item, 0, 40, 0, Term(catalog, 40001));
        var chosen = Compare(catalog, pair); comparisons++;
        Require(chosen.Count == 1 && chosen[0].AttributeId == 40001, "Incoming context did not replace the weaker term.");
        a["IsApplyInBattle"] = false;
        a["EquivalentProps"] = new Dictionary<string, object> { { "68", 100000000L } };
        Require(Compare(catalog, pair).Single().AttributeId == 40001, "An out-of-battle term suppressed the battle passive."); comparisons++;
        a["IsApplyInBattle"] = true; a["EquivalentProps"] = b["EquivalentProps"];
        Require(Compare(catalog, pair).Single().EquipmentInstanceId == 1, "A tie did not retain the earlier term."); comparisons++;

        var single = new OfflinePlayerProgress(); Equip(single, item, 0, 37, 0, Term(catalog, 40015));
        var fixedTerm = Compare(catalog, single).Single(); comparisons++;
        Require(fixedTerm.Parameters.Single(value => value.PropertyId == 5001).RawValue == 800
            && fixedTerm.Parameters.Single(value => value.PropertyId == 5001).IsFloat
            && fixedTerm.SkillLevel == 37, "Fixed probability was scaled by equipment level.");
        fixedTerm.Parameters[0].RawValue = 999999;
        Require(Compare(catalog, single).Single().Parameters.All(value => value.RawValue != 999999), "Battle parameters alias the save/configuration."); comparisons++;
        return "Original equipment selection comparisons passed: " + comparisons
            + "; battle filtering, mixed implementation types, incoming rank/blessing, ties, suits, mean levels and fixed parameters.";
    }
}
