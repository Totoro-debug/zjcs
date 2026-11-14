using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;

namespace SourceSkillProfessionReference
{
/* ORIGINAL_PROFESSION */
    public enum ItemType { None = 0, Sword = 200, Wand = 201 }
    public sealed class SkillInfo { public Profession UnlockProfession; }
    public sealed class SkillInfoParser
    {
        public bool ExistSkill(int id) => Validation.Definitions.ContainsKey(id);
        public SkillInfo Get(int id) => new SkillInfo { UnlockProfession = Validation.Unlock.TryGetValue(id, out var p) ? p : Profession.None };
    }
    public sealed class ConfigManager { public T GetParser<T>() where T : new() => new T(); }
    public class ParserBase { public readonly ConfigManager ConfigManager = new ConfigManager(); }
    public sealed class ProfessionBaseInfo
    {
        public Profession Profession, PrePro;
        public int Rank;
        public List<int> Skills, FightUsedSkills;
    }
    public sealed class ProfessionBaseInfoParser : ParserBase
    {
        public ProfessionBaseInfo Get(Profession id) => Validation.Professions[id];
/* ORIGINAL_ProfessionBaseInfoParser */
    }
    public static class ProfessionUtils
    {
/* ORIGINAL_ProfessionUtils */
    }
    public struct GLoggerErrorInterpolatedStringHandler
    {
        public GLoggerErrorInterpolatedStringHandler(int length, int count, out bool enabled) { enabled = false; }
        public void AppendLiteral(string value) { }
        public void AppendFormatted<T>(T value, int alignment, string format, string name) { }
    }
    public static class Log
    {
        public static void Error(ref GLoggerErrorInterpolatedStringHandler message, object logger, string method, string file, int line)
        {
            // The original invalid-weapon branch logs and returns None; preserve that return.
            if (method != "GetProfessionByWeaponType") throw new InvalidOperationException("Missing reference input: " + method);
        }
    }
    public static class Validation
    {
        public static readonly Dictionary<int, OfflineSkillDefinition> Definitions = new Dictionary<int, OfflineSkillDefinition>();
        public static readonly Dictionary<int, Profession> Unlock = new Dictionary<int, Profession>();
        public static readonly Dictionary<Profession, ProfessionBaseInfo> Professions = new Dictionary<Profession, ProfessionBaseInfo>();
        private static int Int(Dictionary<string, object> row, string key) => Convert.ToInt32(row[key]);
        public static void RunAndBuild() { Run(); Zjcs.Editor.Skills.OfficialSkillAssetTools.BuildWindowsValidation(); }
        public static void Run()
        {
            Zjcs.Editor.ProjectTools.Validate();
            var assets = OfficialCharacterAssetCatalog.LoadDefault();
            var catalog = assets.Load();
            var rules = assets.LoadSkillRules();
            foreach (var definition in rules.Skills) Definitions[definition.ClassId] = definition;
            foreach (var row in catalog.Rows("profession_base"))
            {
                var id = (Profession)Int(row, "Profession");
                var info = new ProfessionBaseInfo { Profession = id, PrePro = (Profession)Int(row, "PrePro"), Rank = Int(row, "Rank"),
                    Skills = ((List<object>)row["Skills"]).Select(Convert.ToInt32).ToList(),
                    FightUsedSkills = ((List<object>)row["FightUsedSkills"]).Select(Convert.ToInt32).ToList() };
                Professions[id] = info;
                foreach (int skill in info.Skills) Unlock[skill] = id;
            }
            int[] scope = { 1, 10, 2, 6, 11, 15 };
            int[] selected = scope.SelectMany(id => Professions[(Profession)id].Skills.Concat(Professions[(Profession)id].FightUsedSkills)).Distinct().ToArray();
            Require(selected.Length == 104, "Original requested scope changed.");
            string workspace = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            Require(File.Exists(Path.Combine(workspace, ".zjcs-validation.json")), "Use a marked validation workspace.");
            var slots = new ProfileSlotCatalog(Path.Combine(workspace, "skill-profession-" + Guid.NewGuid().ToString("N")));
            var session = OfflineMainlineProgressSession.OpenCharacter(slots, slots.CreateSlot());
            var service = new OfflineCharacterProgressionService(session, catalog, refreshDerivedProgress: false);
            service.AttachSkillRules(rules);
            var equipment = OfficialEquipmentAssetCatalog.LoadDefault().Load();
            service.AttachEquipmentCatalog(equipment);
            var player = service.Player;
            player.Level = 50; player.Rank = 2; player.SubRank = 5;
            player.Experience = catalog.CumulativeLevelExperience[50];
            int comparisons = 0, transactions = 0, exclusions = 0;
            var parser = new ProfessionBaseInfoParser();
            foreach (int profession in scope.Concat(new[] { 0 }))
            foreach (int variant in Enumerable.Range(0, profession == 0 ? 5 : 1))
            {
                player.Wardrobe.Profession = profession;
                int weapon = variant == 3 ? 200 : variant == 4 ? 201 : 0;
                player.InitialWeaponType = variant == 1 || variant == 4 ? 200 : variant == 2 || variant == 3 ? 201 : 0;
                player.Equipment.Instances.Clear(); player.Equipment.Equipped.Clear();
                if (weapon != 0)
                {
                    int itemId = equipment.Rows("item").Where(row => Int(row, "ItemType") == weapon).Select(row => Int(row, "ClassId")).First();
                    player.Equipment.Instances.Add(new OfflineEquipmentInstance { InstanceId = 1, ItemId = itemId });
                    player.Equipment.Equipped.Add(new OfflineEquippedSlot { Position = 10, InstanceId = 1 });
                }
                int expectedProfession = (int)ProfessionUtils.GetSafeProfession((Profession)profession, (ItemType)weapon, (ItemType)player.InitialWeaponType);
                var context = service.SkillContext();
                Require(context.Profession == expectedProfession, "Skill context ignored the original weapon fallback.");
                foreach (int id in selected)
                {
                    var definition = Definitions[id];
                    bool expected = (definition.ItemType == 100 || definition.ItemType == 101)
                        && parser.IsSkillAvailableForProfession(id, (Profession)expectedProfession);
                    Require(OfflineSkillProgression.IsAvailableFor(definition, context) == expected,
                        "Profession predicate differs: " + profession + "/" + variant + "/" + id);
                    comparisons++;
                    if (!scope.Any(p => Professions[(Profession)p].Skills.Contains(id))) continue;
                    foreach (int plan in Enumerable.Range(0, 7))
                    {
                        var profile = new OfflineSkillProfile { SelectedPlan = plan };
                        profile.Skills.Add(new OfflineOwnedSkill { ClassId = id, Rank = 1 });
                        int otherId = selected.First(other => other != id && Definitions[other].ItemType == definition.ItemType
                            && scope.Any(p => Professions[(Profession)p].Skills.Contains(other))
                            && !parser.IsSkillAvailableForProfession(other, (Profession)expectedProfession));
                        profile.Skills.Add(new OfflineOwnedSkill { ClassId = otherId, Rank = 1 });
                        var oldPlan = profile.Plans[(plan + 1) % 7];
                        (definition.ItemType == 100 ? oldPlan.ActiveSkills : oldPlan.PassiveSkills)[0] = otherId;
                        // An incompatible old plan is preserved by both successful and rejected operations.
                        profile.Plans[(plan + 1) % 7].Name = "保留方案";
                        int slot = (plan + variant) % 4;
                        var wanted = profile.Clone();
                        if (expected) (definition.ItemType == 100 ? wanted.Plans[plan].ActiveSkills : wanted.Plans[plan].PassiveSkills)[slot] = id;
                        var result = service.SkillProgression.Equip(profile, context, id, slot);
                        Require(result.Code == (expected ? OfflineSkillResultCode.Applied : OfflineSkillResultCode.SkillUnavailable),
                            "Equip transaction returned an unexpected result: " + id + "/" + profession);
                        Require(JsonUtility.ToJson(profile) == JsonUtility.ToJson(wanted), "Equip changed another plan or rejected profile.");
                        transactions++;
                    }
                    var unowned = new OfflineSkillProfile();
                    string before = JsonUtility.ToJson(unowned);
                    Require(service.SkillProgression.Equip(unowned, context, id, 0).Code == OfflineSkillResultCode.SkillNotOwned
                        && JsonUtility.ToJson(unowned) == before, "Unowned skill changed the profile.");
                    exclusions++;
                }
            }
            Debug.Log("[Skill profession] Unchanged original methods passed " + comparisons + " availability comparisons, "
                + transactions + " seven-plan equip transactions and " + exclusions + " unowned exclusions.");
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
