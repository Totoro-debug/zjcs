using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.Progression;

// No UI or user save is involved: run the source data module in Play Mode and
// compare it with the selected client's actual LoadResultFromChest and sorter.
[InitializeOnLoad]
public static class ChestUsePreviewValidation
{
    const string Pending = "Zjcs.ChestUsePreview.Pending";
    static readonly List<string> checks = new List<string>();
    static ChestUsePreviewValidation() { EditorApplication.playModeStateChanged += Changed; }
    public static void Run() { SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true; }
    static void Changed(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        bool success = false;
        try
        {
            var asset = Resources.Load<TextAsset>("PetGacha/chest-use-preview");
            Check(asset, "selected original preview payload loads in independent Unity");
            var json = JObject.Parse(asset.text);
            var rules = new OfficialChestUsePreviewRules(asset.text);
            Common.Parsers.Load(json);
            foreach (var row in (JArray)json["tables"]["chest"])
            {
                int id = (int)row["ClassId"];
                Compare(rules, id);
                var first = rules.Preview(id); first.UseCount = 23;
                Check(rules.Preview(id).UseCount == 1, "fresh initialization resets count: " + id);
            }
            Check(rules.Preview(-1) == null && rules.Preview(1530) == null,
                "absent item and non-preview material take the original early return");
            // Exercise precedence and ordering with rearranged real material ids,
            // independent of the selected payload's naturally ascending keys.
            var variant = (JObject)json.DeepClone();
            var chest = (JObject)variant["tables"]["chest"][0];
            int chestId = (int)chest["ClassId"];
            var real = ((JObject)chest["OptionalRewards"]).Properties().ToArray();
            chest["OptionalRewards"] = new JObject(real.Reverse().Select(value => new JProperty(value.Name, value.Value.DeepClone())));
            chest["Rewards"] = new JObject(new JProperty(real[0].Name, 87L));
            Common.Parsers.Load(variant); Compare(new OfficialChestUsePreviewRules(variant.ToString()), chestId);
            chest["OptionCount"] = 0;
            Common.Parsers.Load(variant); Compare(new OfficialChestUsePreviewRules(variant.ToString()), chestId);
            chest["OptionCount"] = 1;
            chest["OptionalRewards"] = new JObject();
            Common.Parsers.Load(variant); Compare(new OfficialChestUsePreviewRules(variant.ToString()), chestId);
            chest["Rewards"] = new JObject();
            Common.Parsers.Load(variant); Compare(new OfficialChestUsePreviewRules(variant.ToString()), chestId);
            chest["RewardRules"] = new JArray(123);
            Reject<NotSupportedException>(() => new OfficialChestUsePreviewRules(variant.ToString()), "random loader cannot enter explicit preview");
            chest["RewardRules"] = null;
            chest["Rewards"] = new JObject(new JProperty("999999999", 1));
            Reject<FormatException>(() => new OfficialChestUsePreviewRules(variant.ToString()), "missing candidate cannot silently disappear");
            var changedProvenance = (JObject)json.DeepClone(); changedProvenance["manifest"] = "another-source-label";
            Compare(new OfficialChestUsePreviewRules(changedProvenance.ToString()), (int)json["tables"]["chest"][0]["ClassId"], json);
            success = true;
        }
        catch (Exception error) { Debug.LogException(error); checks.Add(error.ToString()); }
        finally
        {
            string output = Environment.GetEnvironmentVariable("ZJCS_CHEST_PREVIEW_OUTPUT");
            File.WriteAllText(Path.Combine(output, "report.json"), JsonConvert.SerializeObject(new { success, checks }, Formatting.Indented));
            SessionState.SetBool(Pending, false); SessionState.SetInt(Pending + ".exit", success ? 0 : 1);
            EditorApplication.playModeStateChanged += Exit; EditorApplication.isPlaying = false;
        }
    }
    static void Compare(OfficialChestUsePreviewRules rules, int id, JObject reset = null)
    {
        if (reset != null) Common.Parsers.Load(reset);
        var original = new OriginalChestUsePreview();
        original.LoadResultFromChest(id, out bool random, out int options, out bool equal);
        var migrated = rules.Preview(id);
        Check(!random && !equal && !migrated.IsRandom && migrated.OptionCount == options
            && migrated.Count == original.mResultItemInfos.Count, "original explicit/optional branch: " + id);
        foreach (long count in new[] { 1L, 2L, 999L, 1000L, long.MaxValue })
        {
            migrated.UseCount = count;
            Check(original.mResultItemInfos.Select((row, index) => row.ItemId == migrated.ItemAt(index)
                && row.FromId == migrated.ItemId && unchecked(row.Count * count) == migrated.CountAt(index)).All(value => value),
                "original dictionary order, from id and count scale: " + id + "/" + count);
        }
        Check(migrated.HasResults == (original.mResultItemInfos.Count > 0)
            && migrated.PerUseCount == original.mResultItemInfos.Select(row => row.Count).FirstOrDefault(),
            "original empty and first-result count: " + id);
        migrated.UseCount = 8; migrated.UseCount = 0; migrated.UseCount = -1;
        Check(migrated.UseCount == 8, "original nonpositive use-count guard: " + id);
    }
    static void Reject<T>(Action action, string name) where T : Exception
    { try { action(); } catch (T) { Check(true, name); return; } throw new Exception(name); }
    static void Check(bool pass, string name) { if (!pass) throw new Exception(name); checks.Add("PASS " + name); }
    static void Exit(PlayModeStateChange state)
    { if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.Exit(SessionState.GetInt(Pending + ".exit", 1)); }
}

// Configuration-only services for the untouched original preview and sorter.
// No skill sorting path is allowed for these material candidates.
namespace Common
{
    public enum Profession { None }
    public enum ItemType { PassiveSkill = 999999 }
    public sealed class ItemInfo { public int ClassId; public bool IsSkill, IsSkillPiece; public ItemType ItemType; }
    public sealed class ChestInfo
    {
        public List<int> RewardRules; public Dictionary<int, long> Rewards, OptionalRewards; public int OptionCount;
    }
    public sealed class ItemParser
    {
        public readonly Dictionary<int, ItemInfo> Rows = new Dictionary<int, ItemInfo>();
        public ItemInfo Get(int id) => Rows.TryGetValue(id, out var value) ? value : null;
    }
    public sealed class ChestParser
    {
        public readonly Dictionary<int, ChestInfo> Rows = new Dictionary<int, ChestInfo>();
        public ChestInfo Get(int id) => Rows.TryGetValue(id, out var value) ? value : null;
    }
    public sealed class ProfessionInfo { public Profession Profession; }
    public sealed class ProfessionParser
    {
        public bool TryGetProfessionInfoBySkill(int id, out ProfessionInfo info) => throw new Exception("Unexpected skill sorting.");
        public IEnumerable<Profession> GetAllSuccessorProfessions(Profession value) => throw new Exception("Unexpected skill sorting.");
    }
    public sealed class Parsers
    {
        public static Parsers Instance;
        public readonly ItemParser ItemInfoParser = new ItemParser();
        public readonly ChestParser ChestInfoParser = new ChestParser();
        public readonly ProfessionParser ProfessionBaseInfoParser = new ProfessionParser();
        public static void Load(JObject json)
        {
            Instance = new Parsers();
            var settings = json["tables"]["game_settings"];
            var skillTypes = ((JObject)settings["SkillTypes"]).Properties().Select(row => int.Parse(row.Name)).ToHashSet();
            var pieceTypes = ((JArray)settings["SkillPieceTypes"]).Select(row => (int)row).ToHashSet();
            foreach (var row in (JArray)json["tables"]["item"])
            {
                int id = (int)row["ClassId"], type = (int)row["ItemType"];
                Instance.ItemInfoParser.Rows.Add(id, new ItemInfo { ClassId = id, ItemType = (ItemType)type,
                    IsSkill = skillTypes.Contains(type), IsSkillPiece = pieceTypes.Contains(type) });
            }
            foreach (var row in (JArray)json["tables"]["chest"])
                Instance.ChestInfoParser.Rows.Add((int)row["ClassId"], new ChestInfo {
                    RewardRules = row["RewardRules"].Type == JTokenType.Null ? null : row["RewardRules"].ToObject<List<int>>(),
                    Rewards = row["Rewards"].Type == JTokenType.Null ? null : row["Rewards"].ToObject<Dictionary<int, long>>(),
                    OptionalRewards = row["OptionalRewards"].Type == JTokenType.Null ? null : row["OptionalRewards"].ToObject<Dictionary<int, long>>(),
                    OptionCount = (int)row["OptionCount"] });
        }
    }
    public sealed class Player
    {
        public Profession Profession => throw new Exception("Unexpected skill sorting.");
        public Profession GetSafeProfession() => throw new Exception("Unexpected skill sorting.");
        public bool IsSkillAvailableForCurProfession(int id) => throw new Exception("Unexpected skill sorting.");
    }
    public static class MeManager { public static readonly Player MainPlayer = new Player(); }
    public static class Formula { public static int GetSkillIdByPieceId(int id) => throw new Exception("Unexpected skill sorting."); }
}
