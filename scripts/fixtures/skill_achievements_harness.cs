using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Zjcs.Offline.Skills;

static class SkillAchievementHarness
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Main(string[] args)
    {
        var rules = JsonSerializer.Deserialize<OfflineSkillRules>(File.ReadAllText(args[0]), new JsonSerializerOptions { IncludeFields = true });
        var gifts = new OfficialSkillAchievementRules(rules);
        var cost = gifts.Group(33); var score = gifts.Group(9);
        Require(cost.Count == 895 && cost[0].RequiredCount == 500 && cost[894].RequiredCount == 446766600, "Material milestones differ from original group 33.");
        Require(score.Count == 67515 && score[14].RequiredCount == 3000 && score[15].RequiredCount == 3200,
            "Skill score cycle seed/increment boundary differs from the original.");
        Require(score[29].RequiredCount == 6000 && score[29].RawId == 9030 && score.Last().RequiredCount == 13503000,
            "The original 4500 x 15 achievement cycle is truncated or expanded incorrectly.");
        Require(score[14].Rewards.Single().ItemId == 304 && score[15].Rewards.Single().ItemId == 301,
            "Skill score cycle rewards are shifted.");
        var profile = new OfflineSkillProfile();
        var received = new HashSet<string>();
        Require(gifts.Current(profile, 33) == 0 && gifts.CanClaim(profile,33,received).Count == 0,
            "A historical spend counter was invented for a new/old profile.");
        profile.SeniorMaterialSpentOnLevelUp = 499;
        Require(gifts.CanClaim(profile,33,received).Count == 0, "Material gift opens before the exact boundary.");
        profile.SeniorMaterialSpentOnLevelUp = 1000;
        Require(gifts.CanClaim(profile,33,received).Count == 2 && gifts.Visible(profile,33,received).Count == 5,
            "Completed group claim or five-row display differs.");
        foreach (var row in gifts.CanClaim(profile,33,received)) received.Add(row.EventId);
        Require(gifts.CanClaim(profile,33,received).Count == 0 && gifts.Visible(profile,33,received)[0].Index == 2,
            "Claim receipts do not suppress already received rewards.");
        for (int i=0;i<20;i++) profile.Skills.Add(new OfflineOwnedSkill { ClassId=10000+i, Rank=1 });
        Require(gifts.Current(profile,9) == 200 && gifts.CanClaim(profile,9,received).Count == 1,
            "Skill score is not the original sum of rank scores.");
        var clone=profile.Clone();
        Require(clone.SeniorMaterialSpentOnLevelUp == 1000 && clone.Skills.Count == 20,
            "A skill transaction clone drops cumulative spend or owned skills.");
        string json=JsonSerializer.Serialize(profile,new JsonSerializerOptions { IncludeFields=true });
        var reload=JsonSerializer.Deserialize<OfflineSkillProfile>(json,new JsonSerializerOptions { IncludeFields=true });
        Require(reload.SeniorMaterialSpentOnLevelUp == 1000 && gifts.Current(reload,9)==200, "Skill gift state does not survive save round-trip.");
        Console.WriteLine("Skill gift rules passed: 895 material milestones; 67515 generated score milestones; exact boundaries, reward cycle, rank score, five-row display, claim receipts, clone/save round-trip.");
    }
}
