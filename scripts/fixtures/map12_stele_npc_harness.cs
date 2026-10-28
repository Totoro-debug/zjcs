using System;
using System.IO;
using System.Linq;
using Zjcs.Combat.Official;

public static class Map12SteleNpcHarness
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private static string Read(string root, string name) => File.ReadAllText(Path.Combine(root, name));

    public static void Run(string root)
    {
        string combatRoot = Path.Combine(root, "unity/Assets/Official/Combat");
        var skillSource = FightOfficialCombatCatalog.ReadSourceManifest(Read(combatRoot, "skill-rules-source-manifest.json"));
        var battleSource = FightOfficialCombatCatalog.ReadSourceManifest(Read(combatRoot, "battle-rules-source-manifest.json"));
        var ruleSource = FightOfficialCombatCatalog.ReadSourceManifest(Read(combatRoot, "rules-source-manifest.json"));
        string rules = Read(combatRoot, "rules.json");
        var combat = FightOfficialCombatCatalog.Load(Read(combatRoot, "skill-rules.json"), skillSource.OutputSha256,
            skillSource.Manifest, Read(combatRoot, "battle-rules.json"), battleSource.OutputSha256, battleSource.Manifest);
        var skillItems = FightOfficialSkillItemCatalog.Load(rules, ruleSource.OutputSha256, ruleSource.Manifest);
        var formula = combat.CreateDamageFormula(rules, ruleSource.OutputSha256, ruleSource.Manifest);
        string npcRoot = Path.Combine(root, "unity/Assets/Game/Resources/DailyChallenges");
        var npcSource = FightOfficialCombatCatalog.ReadSourceManifest(Read(npcRoot, "npc-assistance-rules-source-manifest.json"));
        var catalog = FightOfficialNpcAssistanceCatalog.Load(Read(npcRoot, "npc-assistance-rules.json"),
            npcSource.OutputSha256, npcSource.Manifest, combat, skillItems, formula);
        var companions = catalog.FightAssistNpcClassIds();
        var team = catalog.TeamInviteNpcClassIds();
        Require(companions.Count == 85 && team.Count == 95 && companions.All(team.Contains),
            "Original team selector includes 85 companions, nine tutors and one trader");
        Require(team.Distinct().Count() == 95, "No duplicate team NPC classes");
        foreach (int id in team)
        {
            Require(catalog.CanInviteTeamNpc(id, true, true), "Configured team fighter rejected: " + id);
            Require(!catalog.CanInviteTeamNpc(id, true, false), "Assist condition must be met: " + id);
            Require(catalog.CanInviteTeamNpc(id, false, true) == !catalog.RequiresEncounter(id),
                "Encounter requirement changed: " + id);
        }
        Require(team.Count(id => !catalog.RequiresEncounter(id)) == 3, "Original three no-encounter candidates");
        Require(!catalog.CanInviteTeamNpc(-1, true, true), "Unknown NPC cannot join");
        int created = 0;
        foreach (int id in team.Except(companions))
        {
            Require(!catalog.CanInviteNpc(id, true, true), "Existing companion API scope changed");
            var unit = catalog.CreateTeamInviteUnit(1000 + created, 1, 60, 2, 6, created, 0, 4, id);
            Require(unit.NpcClassId == id && unit.Hp > 0 && unit.Skills.Count > 0 && unit.Properties.Count > 0,
                "Original extra team fighter lacks actual properties/skills: " + id);
            created++;
        }
        Require(created == 10, "Ten additional original team candidates must assemble");
        foreach (int id in companions.Take(3))
        {
            var a = catalog.CreateUnit(2000, 1, 60, 2, 6, 0, 0, 4, id);
            var b = catalog.CreateTeamInviteUnit(2000, 1, 60, 2, 6, 0, 0, 4, id);
            Require(a.Hp == b.Hp && a.Turn.Speed == b.Turn.Speed && a.Level == b.Level &&
                a.Properties.Select(x => (x.Id, x.Value)).SequenceEqual(b.Properties.Select(x => (x.Id, x.Value))) &&
                a.Skills.Count == b.Skills.Count, "Team path changed existing companion formula/skills");
        }
        Console.WriteLine("Map12 original team NPC checks passed: 95 candidates, encounter/condition gates, ten extra battle units and companion parity.");
    }
}
