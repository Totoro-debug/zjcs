using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.Partners;

[InitializeOnLoad]
public static class PartnerConversationDayLimitValidation
{
    const string Key = "PartnerConversationDayLimitValidation.Active";
    static PartnerConversationDayLimitValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                new GameObject("Original daily conversation limits").AddComponent<PartnerConversationDayLimitRunner>();
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!File.Exists(Path.Combine(work, ".zjcs-validation.json")) ||
            !Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned isolated project required");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false);
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "conversation-day-limit-result.json"),
            JsonUtility.ToJson(new Result { success = error == null, error = error?.ToString() }, true));
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    [Serializable] sealed class Result { public bool success; public string error; }
}

public sealed class PartnerConversationDayLimitRunner : MonoBehaviour
{
    sealed class ConditionBoundary : IPartnerConversationCondition
    { public bool IsMet(IPartnerConversationPlayer player) => throw new NotSupportedException("Unlock is outside the day-limit fixture."); }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    void Start()
    {
        try { Validate(); PartnerConversationDayLimitValidation.Finish(null); }
        catch (Exception error) { PartnerConversationDayLimitValidation.Finish(error); }
    }
    static void Validate()
    {
        string repo = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        var conversations = OfficialPartnerConversationAssetCatalog.LoadDefault().Load(_ => new ConditionBoundary());
        var settings = (Dictionary<string, object>)((Dictionary<string, object>)Zjcs.Offline.Equipment.OfficialEquipmentJson.Parse(
            File.ReadAllText(Path.Combine(repo, "unity/Assets/Official/Partners/partner-rules.json"))))["tables"];
        settings = (Dictionary<string, object>)settings["game_settings"];
        int limit = Convert.ToInt32(settings["NpcDialogueDayLimit"]);
        DateTime reset = DateTime.ParseExact((string)settings["DailyResetTime"], "(HH:mm:ss)", CultureInfo.InvariantCulture);
        Require(limit == 1 && reset.TimeOfDay == TimeSpan.FromHours(8), "Actual selected original daily configuration");
        var start = conversations.Nodes.Where(node => node.Node == 1).ToArray();
        int limited = start.First(node => node.IsDayLimit).ClassId;
        int unlimited = 2021;
        Require(!conversations.Get(unlimited, 1).IsDayLimit && !conversations.Get(2022, 1).IsDayLimit,
            "Original same-day continuation classes are not day-limited");
        var records = new Dictionary<long, PartnerConversationDayLimitRecord>();
        DateTime now = new DateTime(2026, 11, 2, 23, 59, 59, DateTimeKind.Utc);
        int clockReads = 0;
        var rules = new PartnerConversationDayLimitRules(conversations, records, () => { clockReads++; return now; }, limit, reset, 8);
        Require(rules.GetDailyResetTimeOfDay().hour == 0, "Original UTC reset conversion for UTC+8");
        Require(!rules.CheckNpcDialogueHasDayLimit(8001, limited) && clockReads == 0, "No record permits first limited dialogue without clock read");
        rules.RecordNpcDialogueDayLimit(8001, limited);
        Require(records[8001].DayShowCount == 1 && records[8001].LastShowTime == now && clockReads == 2,
            "Original recording clock call order and increment");
        Require(rules.CheckNpcDialogueHasDayLimit(8001, limited) && !rules.CheckNpcDialogueHasDayLimit(8002, limited),
            "Limit is per NPC identity");
        int size = records.Count;
        foreach (var node in start.Where(node => !node.IsDayLimit))
        {
            Require(!rules.CheckNpcDialogueHasDayLimit(8001, node.ClassId), "Actual unlimited start bypasses quota");
            rules.RecordNpcDialogueDayLimit(8001, node.ClassId);
        }
        Require(records.Count == size && records[8001].DayShowCount == 1, "Unlimited original classes never consume daily quota");
        Require(rules.CheckNpcDialogueHasDayLimit(8001, -999), "Missing original config is blocked");
        rules.RecordNpcDialogueDayLimit(8001, -999);
        Require(records[8001].DayShowCount == 1, "Missing config does not modify quota");
        now = now.AddSeconds(1);
        Require(!rules.CheckNpcDialogueHasDayLimit(8001, limited), "Exact reset second permits next dialogue");
        rules.RecordNpcDialogueDayLimit(8001, limited);
        Require(records[8001].DayShowCount == 1 && records[8001].LastShowTime == now, "New day resets count before increment");
        var west = new PartnerConversationDayLimitRules(conversations, records, () => now, limit, reset, -5);
        var east = new PartnerConversationDayLimitRules(conversations, records, () => now, limit, reset, 20);
        Require(west.GetDailyResetTimeOfDay().hour == 13 && east.GetDailyResetTimeOfDay().hour == 12,
            "Original positive and negative modulo branches");
        Require(west.GetDailyResetTimeFromDate(new DateTime(2026, 11, 2, 12, 59, 59, DateTimeKind.Utc))
            == new DateTime(2026, 11, 1, 13, 0, 0, DateTimeKind.Utc), "Before-reset date rolls back one day");
        Require(west.GetDailyResetTimeFromDate(DateTime.MinValue) == new DateTime(1969, 12, 31, 13, 0, 0, DateTimeKind.Utc),
            "Original Unix-epoch clamp before reset");
        var precise = new PartnerConversationDayLimitRules(conversations, records, () => now, limit,
            new DateTime(2000, 1, 1, 8, 17, 31), 8);
        Require(precise.GetDailyResetTimeFromDate(new DateTime(2026, 11, 2, 0, 17, 31, DateTimeKind.Utc))
            == new DateTime(2026, 11, 2, 0, 17, 31, DateTimeKind.Utc), "Original reset minute and second preserved");
        var slots = new ProfileSlotCatalog(Path.Combine(work, "profiles"));
        string slot = slots.CreateSlot();
        var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        Require(session.ApplyCharacterMutation(value => {
            value.CharacterCreated = true; value.CharacterName = "每日通话隔离存档"; value.Level = 1;
            new OfflinePartnerConversationDayLimits(value.CharacterProperties.NpcConversations,
                conversations, () => now, limit, reset, 8).RecordNpcDialogueDayLimit(8001, limited);
        }, "daily-limit-fixture-first").IsApplied, "Save original quota with real character transaction");
        session.Reload();
        var saved = session.Save.Player.CharacterProperties.NpcConversations.DayLimits.Single();
        Require(saved.NpcId == 8001 && saved.DayShowCount == 1 && saved.ToRecord().LastShowTime == now
            && saved.ToRecord().LastShowTime.Kind == DateTimeKind.Utc, "Quota identity, count and UTC clock survive disk reload");
        Require(new OfflinePartnerConversationDayLimits(session.Save.Player.CharacterProperties.NpcConversations,
            conversations, () => now, limit, reset, 8).CheckNpcDialogueHasDayLimit(8001, limited), "Reloaded quota remains enforced");
        string path = slots.GetProgressPath(slot);
        byte[] before = File.ReadAllBytes(path);
        Require(!session.ApplyCharacterMutation(value => value.CharacterProperties.NpcConversations.DayLimits.Add(
            OfflineNpcConversationDayLimit.FromRecord(saved.ToRecord()))).IsApplied
            && File.ReadAllBytes(path).SequenceEqual(before), "Duplicate NPC quota rejects whole transaction");
        string temporary = path + ".tmp";
        Require(!File.Exists(temporary) && !Directory.Exists(temporary), "Own temporary path unused");
        Directory.CreateDirectory(temporary); bool failed = false;
        try { session.ApplyCharacterMutation(value => new OfflinePartnerConversationDayLimits(
            value.CharacterProperties.NpcConversations, conversations, () => now.AddDays(1), limit, reset, 8)
                .RecordNpcDialogueDayLimit(8001, limited), "daily-limit-fixture-failed"); }
        catch (IOException) { failed = true; }
        catch (UnauthorizedAccessException) { failed = true; }
        finally { Directory.Delete(temporary); }
        Require(failed && File.ReadAllBytes(path).SequenceEqual(before)
            && session.Save.Player.CharacterProperties.NpcConversations.DayLimits.Single().LastShowTimeTicks == saved.LastShowTimeTicks
            && !session.Save.Player.AppliedCharacterEventIds.Contains("daily-limit-fixture-failed"), "Disk failure rolls back quota and receipt");
        var legacy = JsonUtility.FromJson<OfflineCharacterPropertyProgress>("{\"NpcConversations\":{\"Records\":[]}}");
        OfflineCharacterPropertyProgress.Normalize(legacy);
        Require(OfflineCharacterPropertyProgress.IsValid(legacy) && legacy.NpcConversations.DayLimits.Count == 0,
            "Old conversation saves receive empty daily history without invented counts");
        // The gift fixture also calls the public roster regression with its
        // configured catalog/session; reuse that complete existing entry.
        typeof(PartnerGiftTransactionValidation).GetMethod("Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
        Debug.Log("PARTNER_CONVERSATION_DAY_LIMIT_PASSED: actual original table, per-NPC quota, unrestricted classes, UTC reset boundaries, disk reload and atomic rollback. Trigger/UI not exercised.");
    }
}
