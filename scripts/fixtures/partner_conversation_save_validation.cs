using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.Partners;

[InitializeOnLoad]
public static class PartnerConversationSaveValidation
{
    const string Key = "PartnerConversationSaveValidation.Active";
    static PartnerConversationSaveValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                new GameObject("Original conversation profile round trip").AddComponent<PartnerConversationSaveRunner>();
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
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "conversation-save-result.json"),
            JsonUtility.ToJson(new Result { success = error == null, error = error?.ToString() }, true));
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    [Serializable] sealed class Result { public bool success; public string error; }
}

public sealed class PartnerConversationSaveRunner : MonoBehaviour
{
    sealed class ExplicitWorldBoundary : IOfflinePartnerConversationWorld
    {
        public int CalculateWorldMapProgress(int map, int region) => throw new NotSupportedException("Map flow is outside this save fixture.");
        public bool IsRegionGoddessActive(int map, int region) => throw new NotSupportedException("Goddess flow is outside this save fixture.");
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    void Start()
    {
        try { Validate(); PartnerConversationSaveValidation.Finish(null); }
        catch (Exception error) { PartnerConversationSaveValidation.Finish(error); }
    }
    static void Validate()
    {
        string repo = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        Func<string, string> read = path => File.ReadAllText(Path.Combine(repo, "unity/Assets/Official", path));
        var character = OfficialCharacterCatalog.Parse(read("Character/character-progression.json"),
            read("Character/item-time-limits.json"), read("CharacterCreation/wardrobe.json"),
            read("CharacterCreation/wardrobe-rules.json"), read("Character/character-overview.json"));
        var partner = OfficialPartnerCatalog.Parse(read("Partners/partner-rules.json"), read("Partners/partner-properties.json"));
        var conditions = new PartnerConversationConditionRules(partner.Conditions, partner.Rows("conditions"));
        var conversations = OfficialPartnerConversationAssetCatalog.LoadDefault().Load(conditions.Bind);
        var slots = new ProfileSlotCatalog(Path.Combine(work, "profiles"));
        string slot = slots.CreateSlot();
        var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        DateTime now = new DateTime(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc);
        int updates = 0;
        var player = new OfflinePartnerConversationPlayer(session, character, conversations,
            new ExplicitWorldBoundary(), () => now, _ => updates++);
        Require(session.ApplyCharacterMutation(value => {
            value.CharacterCreated = true; value.CharacterName = "通话隔离存档"; value.Level = 1;
            value.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 8402, Level = 19 });
        }).IsApplied, "Create isolated player");
        Require(!conversations.Get(2021, 1).UnlockCondition.IsMet(player), "Saved friendship below threshold");
        Require(session.ApplyCharacterMutation(value => value.CharacterProperties.NpcFriendships[0].Level = 40).IsApplied,
            "Update saved friendship");
        Require(conversations.Get(2021, 1).UnlockCondition.IsMet(player), "Live player sees committed friendship");
        Require(!conversations.Get(2022, 1).ChainSequenceCondition.IsMet(player), "Previous conversation not ended");
        var record = PartnerConversationRecord.Create(7001, 8001, 2021, 1, 9001, now);
        var saved = OfflineNpcConversation.FromRecord(8402, record);
        Require(session.ApplyCharacterMutation(value => value.CharacterProperties.NpcConversations.Records.Add(saved),
            "conversation-fixture-create").IsApplied, "Save exact initial original record");
        record.DialogueNodes.Add(12345);
        session.Reload();
        var loaded = session.Save.Player.CharacterProperties.NpcConversations.Records.Single();
        record = loaded.ToRecord();
        Require(record.PlayerId == 7001 && record.NpcId == 8001 && record.DialogueId == 9001
            && loaded.NpcClassId == 8402 && record.DialogueNodes.SequenceEqual(new[] { 1 })
            && record.DateTime.Ticks == now.Ticks && record.DateTime.Kind == DateTimeKind.Utc,
            "Disk round trip preserves identity, clock kind and detached history");
        int steps = 0;
        while ((conversations.Get(2021, record.CurrentNode).NextNodes?.Count ?? 0) > 0)
        {
            now = now.AddSeconds(1);
            int next = conversations.Get(2021, record.CurrentNode).NextNodes[0];
            Require(conversations.Get(2021, next).UnlockCondition.IsMet(player), "Actual next-node condition");
            record.SetCurrentNodeAndUpdateStatus(next, player);
            Require(++steps < 6707, "Original node graph terminates");
        }
        Require(record.DialogueStatus == PartnerConversationStatus.End && updates == steps && steps > 0,
            "Original node method and update order");
        var ended = OfflineNpcConversation.FromRecord(8402, record);
        Require(session.ApplyCharacterMutation(value => value.CharacterProperties.NpcConversations.Records[0] = ended,
            "conversation-fixture-finish").IsApplied, "Persist ended original conversation");
        session.Reload();
        Require(player.GetEndDialogues().Single().DialogueNodes.SequenceEqual(record.DialogueNodes)
            && conversations.Get(2022, 1).ChainSequenceCondition.IsMet(player), "Reloaded end history satisfies original chain condition");
        string path = slots.GetProgressPath(slot);
        byte[] before = File.ReadAllBytes(path);
        long gold = session.InventoryCount(1);
        var invalid = session.ApplyCharacterMutation(value => {
            value.Inventory.Add(new OfflineItemStack { ItemId = 1, Count = 100 });
            value.CharacterProperties.NpcConversations.Records.Add(OfflineNpcConversation.FromRecord(8402, record));
        }, "conversation-fixture-duplicate");
        Require(!invalid.IsApplied && File.ReadAllBytes(path).SequenceEqual(before) && session.InventoryCount(1) == gold,
            "Duplicate dialogue identity rolls back the entire character mutation");
        Require(session.ApplyCharacterMutation(value => value.CharacterProperties.NpcConversations.Records.Clear(),
            "conversation-fixture-finish").Status == ProgressionResultStatus.Unchanged, "Replayed receipt cannot overwrite history");
        Require(session.SetStoryProgress("FreyaDie2", 1, 0, "conversation-fixture-story").IsApplied
            && player.GetStoryCount("FreyaDie2") == 1, "Read actual committed story count");
        before = File.ReadAllBytes(path);
        long previousTicks = session.Save.Player.CharacterProperties.NpcConversations.Records[0].DateTimeTicks;
        string blockedTemporary = path + ".tmp";
        Require(!File.Exists(blockedTemporary) && !Directory.Exists(blockedTemporary), "Own temporary path is unused");
        Directory.CreateDirectory(blockedTemporary);
        bool writeFailed = false;
        try
        {
            session.ApplyCharacterMutation(value => value.CharacterProperties.NpcConversations.Records[0].DateTimeTicks++,
                "conversation-fixture-write-failure");
        }
        catch (IOException) { writeFailed = true; }
        catch (UnauthorizedAccessException) { writeFailed = true; }
        finally { Directory.Delete(blockedTemporary); }
        Require(writeFailed && File.ReadAllBytes(path).SequenceEqual(before)
            && session.Save.Player.CharacterProperties.NpcConversations.Records[0].DateTimeTicks == previousTicks
            && !session.Save.Player.AppliedCharacterEventIds.Contains("conversation-fixture-write-failure"),
            "Actual disk write failure preserves memory, primary file and receipt");
        Require(session.ApplyCharacterMutation(value => value.CharacterProperties.NpcConversations.Records[0].DateTimeTicks++,
            "conversation-fixture-write-failure").IsApplied, "Failed receipt can be retried after disk recovery");
        var copy = ended.ToRecord();
        copy.DialogueNodes.Add(copy.CurrentNode);
        copy.DateTime = new DateTime(now.Ticks, DateTimeKind.Local);
        var repeated = JsonUtility.FromJson<OfflineNpcConversation>(JsonUtility.ToJson(OfflineNpcConversation.FromRecord(8402, copy)));
        Require(repeated.ToRecord().DateTime.Kind == DateTimeKind.Local
            && repeated.DialogueNodes.Count == ended.DialogueNodes.Count + 1, "Repeated original history and local clock kind survive JSON");
        var legacy = JsonUtility.FromJson<OfflineCharacterPropertyProgress>("{}");
        OfflineCharacterPropertyProgress.Normalize(legacy);
        Require(OfflineCharacterPropertyProgress.IsValid(legacy) && legacy.NpcConversations.Records.Count == 0,
            "Legacy save gains an empty collection without invented conversations");
        ended.CurrentNode = 123456;
        Require(!OfflineNpcConversation.IsValid(ended), "Mismatched history tail rejected");
        Debug.Log("PARTNER_CONVERSATION_SAVE_PASSED: actual disk profile, identities, history, original condition reads, rollback and legacy normalization.");
    }
}
