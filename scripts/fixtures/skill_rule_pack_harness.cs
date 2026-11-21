using System;
using System.Security.Cryptography;
using System.Text;
using Zjcs.Combat.Official;

public static class SkillRulePackHarness
{
    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("An invalid skill rule pack was accepted.");
    }

    public static string Apply(FightOfficialCombatCatalog catalog, string json, string sha, string manifest)
    {
        Reject(() => catalog.ApplySkillRulePack(json + " ", sha, manifest));
        Reject(() => catalog.ApplySkillRulePack(json, sha, manifest + "-wrong"));
        string changed = json.Replace("\"SkillId\":11700", "\"SkillId\":-11700");
        string changedSha;
        using (var hash = SHA256.Create())
            changedSha = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(changed))).Replace("-", "").ToLowerInvariant();
        Reject(() => catalog.ApplySkillRulePack(changed, changedSha, manifest));
        if (catalog.IsSkillRulePackEntity(24235) || catalog.ContainsEntity(11703))
            throw new InvalidOperationException("A rejected skill rule pack changed the catalog.");
        catalog.ApplySkillRulePack(json, sha, manifest);
        if (!catalog.IsSkillRulePackEntity(24235) || !catalog.ContainsEntity(11703)
            || catalog.CreateStatus(24235, 1, 1).RoundStartSkills.Count != 2)
            throw new InvalidOperationException("The current independent skill children were not installed.");
        Reject(() => catalog.ApplySkillRulePack(json, sha, manifest));
        return "Skill rule pack passed: payload integrity, baseline conflict, rejected mutation and current graph installation.";
    }
}
