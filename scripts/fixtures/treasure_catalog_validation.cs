using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.Progression;

public static class TreasureCatalogValidation
{
    static readonly List<string> checks = new List<string>();
    static int missingLogs;
    public static void Run()
    {
        bool success = false;
        string output = Environment.GetEnvironmentVariable("ZJCS_TREASURE_CATALOG_OUTPUT");
        Application.logMessageReceived += Observe;
        try { Validate(output); success = true; }
        catch (Exception error) { Debug.LogException(error); checks.Add(error.ToString()); }
        finally { Application.logMessageReceived -= Observe; }
        File.WriteAllText(Path.Combine(output, "report.json"), JsonConvert.SerializeObject(new { success, checks }, Formatting.Indented));
        EditorApplication.Exit(success ? 0 : 1);
    }
    static void Validate(string output)
    {
        var catalog = OfflineTreasureCatalog.Load();
        var raw = JObject.Parse(File.ReadAllText(Path.Combine(output, "OriginalTreasureRules.json")));
        ValidateCosts(catalog, raw);
        Check(catalog.Treasures.Count == ((JArray)raw["treasure"]).Count && catalog.Suits.Count == ((JArray)raw["treasure_suit"]).Count,
            "complete selected treasure and suit tables load through Unity Resources");
        var settings = raw["settings"];
        Check(catalog.Settings.TreasureMaxSlotCount == (int)settings["TreasureMaxSlotCount"]
            && catalog.Settings.TreasureMaxLevel == (int)settings["TreasureMaxLevel"]
            && catalog.Settings.TreasurePlanMaxCount == (int)settings["TreasurePlanMaxCount"]
            && catalog.Settings.TreasureSlotPropAddRate == (float)settings["TreasureSlotPropAddRate"]
            && catalog.Settings.TreasureSlotElementTypes.SequenceEqual(settings["TreasureSlotElementTypes"].ToObject<int[]>()),
            "original slot, level, plan, element and property-rate settings");
        foreach (var value in ((JObject)settings["TreasureSettings"]["CommonMaterial2Piece"]).Properties())
        {
            var common = catalog.GetCommonMaterial(int.Parse(value.Name));
            Check(common.ItemClassId == (int)value.Value["ItemClassId"] && common.Item2PieceCount == (int)value.Value["Item2PieceCount"], "original common piece conversion: " + value.Name);
        }
        foreach (var value in ((JObject)settings["TreasureSettings"]["PieceSplitSetting"]).Properties())
        {
            var split = catalog.Settings.PieceSplitSetting.Single(row => row.Quality == int.Parse(value.Name));
            Check(split.GroupCount == (int)value.Value["GroupCount"] && Materials(split.Award, value.Value["Award"]), "original piece split group and rewards: " + value.Name);
        }
        foreach (var source in raw["treasure"])
        {
            var item = catalog.GetTreasure((int)source["ClassId"]);
            Check(item != null && item.TreasurePieceId == (int)source["TreasurePieceId"]
                && catalog.GetTreasureIdByPieceId(item.TreasurePieceId) == item.ClassId, "treasure and reverse piece lookup: " + item?.ClassId);
            var original = new OriginalTreasureRule { ClassId = item.ClassId, StarMax = (int)source["StarMax"],
                LevelPropId = source["LevelPropId"].ToObject<Dictionary<int, int>>(),
                Effect = source["RawEffect"] is JObject effect && (string)effect["_type"] == "TreasureEffectTask" ? new TreasureEffectTask() : new object() };
            Compare(item, original);
            var effectInfo = source["RawEffect"] as JObject;
            Check(item.ObtainedShow == (bool)source["ObtainedShow"] && item.IsLimited == (bool)source["IsLimited"]
                && item.LimitUseCommonMaterial == (bool)source["LimitUseCommonMaterial"]
                && item.EffectType == ((string)effectInfo?["_type"] ?? "")
                && item.EffectPerCount == ((int?)effectInfo?["PerCount"] ?? 0)
                && item.EffectMaxHitCount == ((int?)effectInfo?["MaxHitCount"] ?? 0), "original treasure flags and effect configuration: " + item.ClassId);
            Check(item.StarPropTypes.SequenceEqual(source["StarPropTypes"].ToObject<int[]>())
                && item.LevelPropTypes.SequenceEqual(source["LevelPropTypes"].ToObject<int[]>())
                && item.EffectPropTypes.SequenceEqual(source["EffectPropTypes"].ToObject<int[]>()), "original property type lists: " + item.ClassId);
        }
        foreach (var source in raw["treasure_suit"])
        {
            int id = (int)source["ClassId"];
            var suit = catalog.GetSuit(id);
            var original = new TreasureSuitInfo {
                Props = ((JObject)source["Props"]).Properties().ToDictionary(p => (TreasureSuitPropCondition)int.Parse(p.Name), p => (int)p.Value),
                PropTypes = source["PropTypes"].Select(row => row.Select(value => (PropType)(int)value).ToList()).ToList() };
            new OriginalTreasureSuitParser().Process(original);
            Check(suit.Treasures.SequenceEqual(source["Treasures"].ToObject<int[]>())
                && suit.Treasures.All(t => catalog.GetSuitByTreasureId(t) == suit), "original suit members and reverse index: " + id);
            Check(suit.Props.Select(p => p.Condition).SequenceEqual(original.PropData.Keys.Select(key => (int)key))
                && suit.Props.All(p => p.PropId == original.PropData[(TreasureSuitPropCondition)p.Condition].Item1
                    && p.PropTypes.SequenceEqual(original.PropData[(TreasureSuitPropCondition)p.Condition].Item2.Select(value => (int)value))),
                "actual original parser pairs properties by enumeration position: " + id);
        }
        foreach (var row in raw["treasure_levelup_material"])
            Check(Materials(catalog.GetLevelUpRule((int)row["Level"]).Materials, row["Materials"]), "original level costs: " + row["Level"]);
        foreach (var row in raw["treasure_upgrade_material"])
        {
            var rule = catalog.GetUpgradeRule((int)row["Quality"], (int)row["Star"]);
            Check(rule.PieceCount == (int)row["PieceCount"] && rule.SpecialPieceCount == (int)row["SpecialPieceCount"]
                && Materials(rule.Materials, row["Materials"]), "original promotion costs: " + row["Quality"] + "/" + row["Star"]);
        }
        foreach (var row in raw["treasure_bless"])
            Check(Materials(catalog.GetBlessRule((int)row["Rank"], (int)row["BlessLevel"]).LevelCostItems, row["LevelCostItems"]),
                "original blessing costs: " + row["Rank"] + "/" + row["BlessLevel"]);
        foreach (var row in raw["random_treasure_exchange"])
        {
            var rule = catalog.RandomExchanges.Single(value => value.ItemId == (int)row["ItemId"]);
            Check(rule.RequireCount == (int)row["RequireCount"] && rule.RewardRuleIds.SequenceEqual(row["RewardRuleIds"].ToObject<int[]>()),
                "original random exchange requirement and rules: " + rule.ItemId);
        }
        // Rearrange real season entries: fallback must follow the original first
        // entry, even when a later zero-season entry is available.
        foreach (var entries in new[] {
            new[] { new TreasureSeasonLevelPropEntry { SeasonId = 9, PropId = 17 }, new TreasureSeasonLevelPropEntry { SeasonId = 0, PropId = 23 } },
            new[] { new TreasureSeasonLevelPropEntry { SeasonId = 0, PropId = 0 }, new TreasureSeasonLevelPropEntry { SeasonId = 9, PropId = 17 } },
            Array.Empty<TreasureSeasonLevelPropEntry>() })
        {
            var item = new TreasureRuleConfig { ClassId = 1, StarMax = 0, LevelPropId = entries.ToList() };
            var original = new OriginalTreasureRule { ClassId = 1, StarMax = 0, Effect = new object(), LevelPropId = entries.ToDictionary(p => p.SeasonId, p => p.PropId) };
            Compare(item, original);
        }
        var relabeled = JObject.Parse(Resources.Load<TextAsset>("Treasure/treasure-rules").text);
        relabeled["Manifest"] = "different-provenance-label";
        Check(OfflineTreasureCatalog.Parse(relabeled.ToString()).Treasures.Count == catalog.Treasures.Count,
            "source provenance never gates runtime rule loading");
        Check(catalog.GetTreasure(-1) == null && catalog.GetSuit(-1) == null && catalog.GetTreasureIdByPieceId(-1) == 0,
            "absent records remain absent");
    }
    static void Compare(TreasureRuleConfig item, OriginalTreasureRule original)
    {
        Check(item.UpgradeMaxStar == original.UpgradeMaxStar && item.InitialHitCount == original.GetInitialHitCount(), "actual original initial hit and normal star boundary: " + item.ClassId);
        foreach (int season in original.LevelPropId.Keys.Concat(new[] { -1, 1000000 }).Distinct())
        {
            int before = missingLogs, beforeOriginal = Log.Errors;
            int expected = original.GetLevelPropId(season), actual = item.GetLevelPropId(season);
            Check(actual == expected && missingLogs - before == Log.Errors - beforeOriginal,
                "actual original season lookup, negative clamp, fallback and diagnostic: " + item.ClassId + "/" + season);
        }
    }
    static void ValidateCosts(OfflineTreasureCatalog catalog, JObject raw)
    {
        var parsers = Parsers.Instance;
        parsers.ItemInfoParser.Rows = raw["items"].ToDictionary(row => (int)row["ClassId"],
            row => new ItemInfo { Quality = (Quality)(int)row["Quality"] });
        parsers.TreasureInfoParser.Rows = raw["treasure"].ToDictionary(row => (int)row["ClassId"],
            row => new TreasureInfo { StarMax = (int)row["StarMax"], TreasurePieceId = (int)row["TreasurePieceId"],
                ItemInfo = parsers.ItemInfoParser.Get((int)row["ClassId"]) });
        parsers.Costs.Rows = raw["treasure_upgrade_material"].ToDictionary(row => ((Quality)(int)row["Quality"], (int)row["Star"]),
            row => new TreasureUpgradeInfo { PieceCount = (int)row["PieceCount"], SpecialPieceCount = (int)row["SpecialPieceCount"],
                Materials = row["Materials"].ToObject<Dictionary<int, long>>() });
        var rules = new OfflineTreasureCostRules(catalog, id => (int)parsers.ItemInfoParser.Get(id).Quality);
        foreach (var treasure in catalog.Treasures)
        {
            int id = treasure.ClassId;
            Check(CostsEqual(rules.GetMergeMaterials(id), parsers.Costs.GetMergeMaterials(id)), "original merge costs and order: " + id);
            for (int current = -1; current <= treasure.StarMax; current++)
            {
                for (int target = 0; target <= treasure.StarMax + 1; target++)
                    if (!CostsEqual(rules.GetUpgradeMaterials(id, current, target), parsers.Costs.GetUpgradeMaterials(id, current, target)))
                        throw new Exception("Original cumulative promotion mismatch: " + id + "/" + current + "/" + target);
                int quality = (int)parsers.ItemInfoParser.Get(id).Quality;
                Check(CostsEqual(rules.GetSpecialUpgradeMaterials(id, current, quality),
                    parsers.Costs.GetSpecialUpgradeMaterials(id, current, (Quality)quality)), "original special promotion delta and missing row: " + id + "/" + current);
            }
            Check(true, "original all promotion ranges, initial -1, same/lower target and max clamp: " + id);
            var detached = rules.GetMergeMaterials(id);
            detached.Clear();
            Check(CostsEqual(rules.GetMergeMaterials(id), parsers.Costs.GetMergeMaterials(id)), "cost query returns independent dictionary: " + id);
        }
        // Original methods preserve zero and negative special differences and use
        // unchecked accumulation. Exercise those branches using the same raw-row
        // containers on both sides, without changing the shipped configuration.
        int probe = catalog.Treasures.First().ClassId;
        int q = (int)parsers.ItemInfoParser.Get(probe).Quality;
        var source = catalog.GetUpgradeRule(q, 0);
        var next = catalog.GetUpgradeRule(q, 1);
        var originalSource = parsers.Costs.Get((Quality)q, 0);
        var originalNext = parsers.Costs.Get((Quality)q, 1);
        int oldSpecial = next.SpecialPieceCount;
        var oldSource = source.Materials; var oldNext = next.Materials;
        var oldOriginalSource = originalSource.Materials; var oldOriginalNext = originalNext.Materials;
        try
        {
            foreach (int delta in new[] { 0, -1 })
            {
                next.SpecialPieceCount = originalNext.SpecialPieceCount = source.SpecialPieceCount + delta;
                Check(CostsEqual(rules.GetSpecialUpgradeMaterials(probe, 0, q), parsers.Costs.GetSpecialUpgradeMaterials(probe, 0, (Quality)q)), "special cost preserves delta " + delta);
            }
            source.Materials = new List<TreasureRuleItemAmount> { new TreasureRuleItemAmount { ItemId = 7, Count = long.MaxValue }, new TreasureRuleItemAmount { ItemId = 8, Count = 0 } };
            next.Materials = new List<TreasureRuleItemAmount> { new TreasureRuleItemAmount { ItemId = 7, Count = 1 }, new TreasureRuleItemAmount { ItemId = 9, Count = -3 } };
            originalSource.Materials = new Dictionary<int, long> { { 7, long.MaxValue }, { 8, 0 } };
            originalNext.Materials = new Dictionary<int, long> { { 7, 1 }, { 9, -3 } };
            Check(CostsEqual(rules.GetMergeMaterials(probe), parsers.Costs.GetMergeMaterials(probe)), "merge preserves zero material and dictionary order");
            Check(CostsEqual(rules.GetUpgradeMaterials(probe, -1, 1), parsers.Costs.GetUpgradeMaterials(probe, -1, 1)), "promotion skips zero, preserves negative cost and original overflow");
        }
        finally
        {
            next.SpecialPieceCount = originalNext.SpecialPieceCount = oldSpecial;
            source.Materials = oldSource; next.Materials = oldNext;
            originalSource.Materials = oldOriginalSource; originalNext.Materials = oldOriginalNext;
        }
    }
    static bool CostsEqual(Dictionary<int, long> actual, Dictionary<int, long> expected) => actual.SequenceEqual(expected);
    static bool Materials(List<TreasureRuleItemAmount> actual, JToken raw)
    {
        var values = raw.Type == JTokenType.Null ? Array.Empty<JProperty>() : ((JObject)raw).Properties().ToArray();
        return actual.Count == values.Length && actual.Select((row, i) => row.ItemId == int.Parse(values[i].Name) && row.Count == (long)values[i].Value).All(value => value);
    }
    static void Observe(string message, string stack, LogType type) { if (type == LogType.Error && message.StartsWith("treasure.csv classId: ")) missingLogs++; }
    static void Check(bool pass, string name) { if (!pass) throw new Exception(name); checks.Add("PASS " + name); }
}

// Only global logging and parser-owned containers are supplied to the untouched
// original methods. No rule branches or property pairing are recreated here.
public sealed class TreasureEffectTask { }
public struct GLoggerErrorInterpolatedStringHandler
{
    public GLoggerErrorInterpolatedStringHandler(int literals, int args, out bool enabled) { enabled = false; }
    public void AppendLiteral(string text) { }
    public void AppendFormatted<T>(T value, int alignment, string format, string name) { }
}
public static class Log { public static int Errors; public static void Error(ref GLoggerErrorInterpolatedStringHandler handler, object owner, string method, string path, int line) { Errors++; } }
public enum TreasureSuitPropCondition { FullCollected, FullThreeStar, FullMaxStar }
public enum PropType { }
public sealed class TreasureSuitInfo
{
    public Dictionary<TreasureSuitPropCondition, int> Props;
    public List<List<PropType>> PropTypes;
    public Dictionary<TreasureSuitPropCondition, (int, List<PropType>)> PropData;
}
public enum Quality { }
public sealed class ItemInfo { public Quality Quality; }
public sealed class TreasureInfo { public int StarMax, TreasurePieceId; public ItemInfo ItemInfo; }
public sealed class TreasureUpgradeInfo { public int PieceCount, SpecialPieceCount; public Dictionary<int, long> Materials; }
public sealed class OriginalItemParser { public Dictionary<int, ItemInfo> Rows; public ItemInfo Get(int id) => Rows.TryGetValue(id, out var value) ? value : null; }
public sealed class OriginalTreasureParser { public Dictionary<int, TreasureInfo> Rows; public TreasureInfo Get(int id) => Rows.TryGetValue(id, out var value) ? value : null; }
public sealed partial class OriginalTreasureCostParser
{
    public Dictionary<(Quality, int), TreasureUpgradeInfo> Rows;
    public TreasureUpgradeInfo Get(Quality quality, int star) => Rows.TryGetValue((quality, star), out var value) ? value : null;
}
public sealed class Parsers
{
    public static readonly Parsers Instance = new Parsers();
    public readonly OriginalItemParser ItemInfoParser = new OriginalItemParser();
    public readonly OriginalTreasureParser TreasureInfoParser = new OriginalTreasureParser();
    public readonly OriginalTreasureCostParser Costs = new OriginalTreasureCostParser();
}
