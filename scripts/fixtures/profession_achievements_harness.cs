using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Zjcs.Offline.Progression;

static class ProfessionAchievementsHarness
{
    static object Read(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => Read(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(Read).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.GetInt64(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null
    };
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Main(string[] args)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(args[0]));
        var raw = ((List<object>)Read(document.RootElement.GetProperty("achievement"))).Cast<Dictionary<string, object>>().ToArray();
        var rules = new OfficialProfessionAchievementRules(raw);
        var rows = rules.Milestones;
        Check(rows.Count == 10005, "Original cycle count/seed coverage differs.");
        Check(rows[0].Id == 10 && rows[1].Id == 4106 && rows[10004].Id == ((10004 << 12) | 10), "Group/index packing differs.");
        int[] levels = { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
        int[] items = { 42200, 42200, 42200, 42200, 303, 301, 301, 302, 301, 303, 301 };
        for (int i = 0; i < levels.Length; i++)
        {
            Check(rows[i].RequiredLevel == levels[i], "Fixed/cycle boundary level differs: " + i);
            Check(rows[i].Reward.Single().Key == items[i], "Original reward template order differs: " + i);
            Check(rows[i].Reward.Single().Value == (i < 4 ? 300 : 2), "Original reward count differs: " + i);
        }
        Check(rows[10004].RequiredLevel == 10006 && rows[10004].RawId == 10010 && rows[10004].Reward[303] == 2,
            "Final supported cycle differs from original parser.");
        var recorded = new HashSet<string>();
        Check(rules.CanClaim(1, recorded).Count == 0, "Unmet achievement became receivable.");
        Check(rules.Visible(5, recorded, 5).Select(x => x.RequiredLevel).SequenceEqual(new[] { 2, 3, 4, 5, 6 }), "Screenshot fixture's five visible rows differ.");
        var claim = rules.CanClaim(5, recorded);
        Check(claim.Count == 4 && claim.Sum(x => x.Reward[42200]) == 1200, "Original group/count=-1 must claim all four completed rewards.");
        foreach (var row in claim) recorded.Add(row.EventId);
        Check(rules.CanClaim(5, recorded).Count == 0, "Repeat/reopened claim can duplicate rewards.");
        Check(rules.Visible(5, recorded, 5).Select(x => x.RequiredLevel).SequenceEqual(new[] { 6, 7, 8, 9, 10 }), "Claimed rows were not removed/refilled.");
        Check(rules.CanClaim(6, recorded).Single().Reward[303] == 2, "Cycle seed reward was skipped after receiving the fixed group.");
        Check(rules.CanClaim(10006, new HashSet<string>()).Count == 10005, "High-level group claim truncated the original cycles.");
        Console.WriteLine("Profession achievement official fixtures passed: 10005 milestones, fixed/cycle boundaries, five-row display, group claim and repeat protection.");
    }
}
