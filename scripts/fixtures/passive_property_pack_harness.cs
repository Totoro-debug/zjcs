using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Zjcs.Combat.Official;

public static class PassivePropertyPackHarness
{
    static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException("Passive property source: " + message); }

    static string Hash(string value)
    {
        using (var algorithm = SHA256.Create())
            return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value)))
                .Replace("-", "").ToLowerInvariant();
    }

    static string Status(FightOfficialCombatCatalog catalog)
    {
        var checkpoint = new FightBattleCheckpoint();
        var unit = new FightBattleUnitState { Id = 1 };
        unit.Statuses.Add(catalog.CreateStatus(25113, 1, 1, applyingSkillItemId: 10604));
        checkpoint.Units.Add(unit);
        return FightBattleCheckpointSerializer.Save(checkpoint);
    }

    static void Reject(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items,
        string json, string manifest, Action<Dictionary<string, object>> change)
    {
        var pack = (Dictionary<string, object>)OfficialJson.Parse(json);
        change(pack);
        string modified = OfficialJson.Stringify(pack);
        bool rejected = false;
        try { catalog.ApplyPassiveSkillRulePack(modified, Hash(modified), manifest, items); }
        catch (FormatException) { rejected = true; }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "an invalid source binding was accepted");
    }

    public static string Run(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items, FightDamageFormula formula,
        string json, string sha256, string manifest)
    {
        var item = items.Required(10604);
        Check(formula.PropertyId("ElementMaster") == 350, "the passive property is not original elemental mastery");
        Check(item.EntityId == 25113 && item.PassiveStatusIds.SequenceEqual(new[] { 25113 }),
            "quick casting graph binding differs");
        string originalStatus = Status(catalog);
        var shared = items.Required(10525);
        var previousShared = catalog.CalculatePassiveSkillProps(shared.PassiveRankPropId,
            shared.PassiveGroupLevelPropId, shared.PassiveRankFactorId, shared.PassivePropFactors,
            4, 20, 26, 10525).OrderBy(pair => pair.Key).ToArray();
        Reject(catalog, items, json, manifest, pack =>
            ((Dictionary<string, object>)pack["skill"])["EcEntityId"] = 0L);
        Reject(catalog, items, json, manifest, pack =>
            ((Dictionary<string, object>)pack["skill"])["PassiveStatusIdList"] = new List<object>());
        Reject(catalog, items, json, manifest, pack =>
            ((Dictionary<string, object>)pack["skill"])["PassiveStatusIdList"] = new List<object> { 25114L });
        Reject(catalog, items, json, manifest, pack =>
            ((Dictionary<string, object>)((Dictionary<string, object>)pack["skill"])["PassivePropFactors"])["350"] = 1L);
        Reject(catalog, items, json, manifest, pack =>
            ((Dictionary<string, object>)pack["props"]).Remove("2051"));
        Reject(catalog, items, json, manifest, pack => pack["manifest"] = "different-source");
        catalog.ApplyPassiveSkillRulePack(json, sha256, manifest, items);
        Check(Status(catalog) == originalStatus && item.EntityId == 25113
            && item.PassiveStatusIds.SequenceEqual(new[] { 25113 }), "loading numeric data changed the status graph");
        Check(previousShared.SequenceEqual(catalog.CalculatePassiveSkillProps(shared.PassiveRankPropId,
            shared.PassiveGroupLevelPropId, shared.PassiveRankFactorId, shared.PassivePropFactors,
            4, 20, 26, 10525).OrderBy(pair => pair.Key)), "a shared cultivation table changed another item");
        // Independently extracted official level rows, then factor 7500 and rank factor.
        int[] subRanks = { 8, 11, 14, 17, 20, 23, 26, 40 };
        long[,] levelValues = { { 495, 499, 581 }, { 1486, 1490, 6173 },
            { 2145, 2146, 7076 }, { 18365, 18394, 18970 }, { 13099, 13113, 13393 },
            { 23149, 23170, 23610 }, { 52186, 52221, 52964 }, { 52186, 52221, 52964 } };
        int[] ranks = { 1, 2, 4, 7, 26 };
        long[] rankFactors = { 2800, 3000, 5100, 8800, 42000 };
        int[] levels = { 1, 2, 20 };
        int cases = 0;
        for (int s = 0; s < subRanks.Length; s++)
        for (int r = 0; r < ranks.Length; r++)
        for (int l = 0; l < levels.Length; l++)
        {
            var props = catalog.CalculatePassiveSkillProps(item.PassiveRankPropId,
                item.PassiveGroupLevelPropId, item.PassiveRankFactorId, item.PassivePropFactors,
                ranks[r], levels[l], subRanks[s], 10604);
            long expected = (long)((long)(levelValues[s, l] * .75) * (rankFactors[r] / 10000.0));
            Check(props.Count == 1 && props[350] == expected,
                "current elemental mastery cultivation differs: " + ranks[r] + "/" + levels[l] + "/" + subRanks[s]);
            CheckMasteryDamage(formula, props[350]);
            cases++;
        }
        Reject(catalog, items, json, manifest, pack => { });
        return "Passive property sources passed: " + cases + " graph-backed cultivation cases; graph, shared-source and invalid binding checks.";
    }

    static void CheckMasteryDamage(FightDamageFormula formula, long mastery)
    {
        var source = new Dictionary<int, long> { { formula.PropertyId("Attack"), 1000 },
            { formula.PropertyId("ElementMaster"), mastery }, { 200, 10000 } };
        var target = new Dictionary<int, long> { { formula.PropertyId("Defence"), 10 } };
        foreach (string name in new[] { "BaseElementMaster", "BaseElementResistance", "BaseElementAdd", "BaseElementReduce",
            "BaseKongFuMaster", "BaseKongFuResistance", "BaseCritRatePercentValue", "BaseCritAvoidPercentValue",
            "BaseBlockPercentValue", "BaseBlockAvoidPercentValue" })
        { source[formula.PropertyId(name)] = 10000; target[formula.PropertyId(name)] = 10000; }
        foreach (int element in new[] { 0, 101, 102, 103, 104, 105 })
        {
            var request = new FightDamageFormulaRequest { SourceProps = source, TargetProps = target,
                SkillDamageBasicPropType = 1, SkillDamagePropType = 200, SkillDamageFixedPropType = 190,
                SourceType = 1, TargetType = 1, SourceLevel = 70, TargetLevel = 70,
                SourceSubRank = 8, TargetSubRank = 8, SourceElementType = element };
            long damage = formula.Calculate(request, new FightDamageTypeData()).Damage;
            long expected = -(long)(1000m * 1000m / 1010m * (element == 0 ? 1m : 1m + mastery / 10000m));
            Check(damage == expected, "elemental mastery damage or physical isolation differs: " + element);
        }
    }
}
