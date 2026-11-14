using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Zjcs.Official.HomeLevelUpRules;
using Zjcs.Official.HomeIncomeRules;
using Zjcs.Official.HomeExperienceRules;

public static class HomeLevelUpPropertiesChecks
{
    static int checks;
    static void Require(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };
    public static string Run(string configuration, string binary, string reference)
    {
        checks = 0;
        using var source = JsonDocument.Parse(File.ReadAllText(configuration));
        var input = source.RootElement.GetProperty("tables");
        var roles = JsonSerializer.Deserialize<RolePropGroupInfo[]>(input.GetProperty("role_prop_group").GetRawText(), Json);
        var settings = JsonSerializer.Deserialize<HomeBattleSettings>(input.GetProperty("game_settings").GetRawText(), Json);
        var professions = JsonSerializer.Deserialize<ProfessionBaseInfo[]>(input.GetProperty("profession_base").GetRawText(), Json);
        var defaults = JsonSerializer.Deserialize<LevelPropDefaultValue[]>(input.GetProperty("level_prop_default_value").GetRawText(), Json);
        byte[] bytes = File.ReadAllBytes(binary);
        using var tables = new HomeCharacterBattleTables(settings, bytes, professions, roles, defaults);
        using var vectors = JsonDocument.Parse(File.ReadAllText(reference));
        foreach (var row in vectors.RootElement.GetProperty("level_rows").EnumerateArray())
        {
            var value = tables.LevelPropParser.GetProps(row.GetProperty("id").GetInt32(), row.GetProperty("level").GetInt32());
            Equal(value, row.GetProperty("expected"), "independent binary decode / default fill");
        }
        foreach (var row in vectors.RootElement.GetProperty("formulas").EnumerateArray())
        {
            var levels = JsonSerializer.Deserialize<Dictionary<Profession,int>>(row.GetProperty("professions").GetRawText(), Json);
            var value = tables.Formula.CalcCharacterBattleProps((Profession)row.GetProperty("profession").GetInt32(), levels,
                row.GetProperty("level").GetInt32(), (SubRank)row.GetProperty("subrank").GetInt32());
            Equal(value, row.GetProperty("expected"), "independent selected formula inputs");
        }
        var first = tables.LevelPropParser.GetProps(1, 1);
        int visited = 0;
        foreach (var row in vectors.RootElement.GetProperty("level_rows").EnumerateArray())
        {
            int id = row.GetProperty("id").GetInt32(), level = row.GetProperty("level").GetInt32();
            if (id == 1 && level == 1) continue;
            tables.LevelPropParser.TryGetLevelRange(id, out var range);
            if (level > range.Item2) continue;
            tables.LevelPropParser.GetProps(id, level);
            if (++visited == 1200) break;
        }
        var revisited = tables.LevelPropParser.GetProps(1, 1);
        Require(!ReferenceEquals(first, revisited) && first.OrderBy(v=>v.Key).SequenceEqual(revisited.OrderBy(v=>v.Key)), "original 1000-entry LRU evicts and reconstructs equal data");
        Require(tables.Formula.GetRawProp(null, PropType.BaseAttack) == 0, "raw null returns zero");
        var one = new Dictionary<PropType,long> { [PropType.None] = 123, [PropType.BaseAttack] = long.MaxValue };
        Require(tables.Formula.GetRawProp(one, PropType.None) == 123, "raw None key is not silently discarded");
        Require(tables.Formula.AddProps(null, one).Count == 0, "original null first operand discards additions");
        Require(tables.Formula.AddProps(one, null)[PropType.None] == 123, "null second operand keeps copied first values");
        tables.Formula.AddProp(one, PropType.BaseAttack, 1);
        Require(one[PropType.BaseAttack] == long.MinValue, "original unchecked long addition retained");
        var role = roles.First();
        var before = tables.Formula.CalcCharacterBattlePropsWithoutProfession(1, role.SubRank);
        role.ExtraLevelPropID = role.LevelPropID;
        var extra = tables.Formula.CalcCharacterBattlePropsWithoutProfession(1, role.SubRank);
        var expectedExtra = tables.LevelPropParser.GetProps(role.LevelPropID, 1);
        Require(extra.Keys.Union(before.Keys).All(key => tables.Formula.GetRawProp(extra,key) == tables.Formula.GetRawProp(before,key)
            + (expectedExtra != null && expectedExtra.TryGetValue(key,out long number) ? number : 0)), "original extra role group branch");
        role.ExtraLevelPropID = 0;
        using var parser = new LevelPropParser(bytes, new[] { new LevelPropDefaultValue {
            ClassId = 1, PropTypes = new List<PropType> { PropType.None, PropType.BaseAttack }, DefaultValue = 123 } });
        var original = tables.LevelPropParser.GetProps(1, 1);
        var filled = parser.GetProps(1, 1);
        Require(filled[PropType.None] == 123 && filled[PropType.BaseAttack] == original[PropType.BaseAttack], "synthetic defaults fill missing keys and preserve existing values");
        int errors = 0; var previous = HomeLevelUpDiagnostics.Error;
        HomeLevelUpDiagnostics.Error = _ => errors++;
        try {
            Require(parser.GetProps(int.MaxValue, 1) == null && parser.GetProps(1, -1) == null && errors == 2, "missing group and below-minimum level retain errors and null");
        } finally { HomeLevelUpDiagnostics.Error = previous; }
        var invalid = (byte[])bytes.Clone(); invalid[0] ^= 1;
        bool rejected = false;
        try { using var bad = new LevelPropParser(invalid, defaults); } catch (InvalidDataException) { rejected = true; }
        Require(rejected, "original binary header rejection");
        return "HOME_LEVEL_UP_PROPERTIES_PASSED: "+checks+" independent selected binary/formula and edge checks";
    }
    static void Equal(IReadOnlyDictionary<PropType,long> actual, JsonElement expected, string message)
    {
        if (expected.ValueKind == JsonValueKind.Null) { Require(actual == null, message); return; }
        var values = expected.EnumerateObject().ToDictionary(row => (PropType)int.Parse(row.Name), row => row.Value.GetInt64());
        Require(actual != null && actual.Count == values.Count && values.All(row => actual.TryGetValue(row.Key,out long value) && value == row.Value), message);
    }
}
