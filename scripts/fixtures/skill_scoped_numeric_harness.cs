using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Zjcs.Combat.Official;

public static class SkillScopedNumericHarness
{
    static string Hash(string text)
    { using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(b => b.ToString("x2"))); }
    static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
    static void Reject(Action action)
    {
        try { action(); } catch (InvalidOperationException) { return; } catch (FormatException) { return; }
        throw new InvalidOperationException("Invalid scoped skill pack was accepted.");
    }
    public static string Run(FightOfficialCombatCatalog catalog, string pack, int sentinel, string[] invalid)
    {
        var before = catalog.CalculateSkillProps(12070, 1, 2, 8);
        var shared = catalog.CalculateSkillProps(sentinel, 1, 2, 8);
        string hash = Hash(pack);
        Reject(() => catalog.ApplySkillRulePack(pack + " ", hash, "scoped-numeric-fixture"));
        Reject(() => catalog.ApplySkillRulePack(pack, hash, "wrong-source"));
        foreach (string rejected in invalid)
        {
            Reject(() => catalog.ApplySkillRulePack(rejected, Hash(rejected), "scoped-numeric-fixture"));
            Check(before.All(row => catalog.CalculateSkillProps(12070, 1, 2, 8)[row.Key] == row.Value),
                "Rejected numeric pack changed the installed catalog.");
            Check(!catalog.IsSkillRulePackEntity(12070), "Rejected numeric pack installed its graph.");
        }
        catalog.ApplySkillRulePack(pack, hash, "scoped-numeric-fixture");
        var after = catalog.CalculateSkillProps(12070, 1, 2, 8);
        Check(after.Any(row => !before.ContainsKey(row.Key) || row.Value != before[row.Key]),
            "Selected entity did not receive its own numeric source.");
        var unchanged = catalog.CalculateSkillProps(sentinel, 1, 2, 8);
        Check(shared.Count == unchanged.Count && shared.All(row => unchanged[row.Key] == row.Value),
            "Scoped numeric source changed another skill sharing the same group.");
        Reject(() => catalog.ApplySkillRulePack(pack, hash, "scoped-numeric-fixture"));
        return "Scoped skill numeric source passed: current entity changes, shared-group isolation, hash/source rejection and duplicate rejection.";
    }
}
