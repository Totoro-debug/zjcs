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
public static class PartnerConversationTransactionValidation
{
    const string Key = "PartnerConversationTransactionValidation.Active";
    static PartnerConversationTransactionValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                new GameObject("Original conversation local transaction").AddComponent<PartnerConversationTransactionRunner>();
        };
    }
    public static void Run()
    {
        string work = Path.GetFullPath(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"));
        if (!File.Exists(Path.Combine(work, ".zjcs-validation.json")) ||
            !Path.GetFullPath(Application.dataPath).StartsWith(work + Path.DirectorySeparatorChar))
            throw new InvalidOperationException("Owned isolated project required");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
    }
    public static void Finish(Exception error)
    {
        SessionState.SetBool(Key, false);
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "conversation-transaction-result.json"),
            JsonUtility.ToJson(new Result { success = error == null, error = error?.ToString() }, true));
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
    [Serializable] sealed class Result { public bool success; public string error; }
}

public sealed class PartnerConversationTransactionRunner : MonoBehaviour
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    void Start()
    {
        try { Validate(); PartnerConversationTransactionValidation.Finish(null); }
        catch (Exception error) { PartnerConversationTransactionValidation.Finish(error); }
    }
    static void Validate()
    {
        string repo = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        Func<string, string> read = path => File.ReadAllText(Path.Combine(repo, "unity/Assets/Official", path));
        var partner = OfficialPartnerCatalog.Parse(read("Partners/partner-rules.json"), read("Partners/partner-properties.json"));
        var character = OfficialCharacterCatalog.Parse(read("Character/character-progression.json"),
            read("Character/item-time-limits.json"), read("CharacterCreation/wardrobe.json"),
            read("CharacterCreation/wardrobe-rules.json"), read("Character/character-overview.json"), partner);
        var conditions = new PartnerConversationConditionRules(partner.Conditions, partner.Rows("conditions"));
        var conversations = OfficialPartnerConversationAssetCatalog.LoadDefault().Load(conditions.Bind);
        var slots = new ProfileSlotCatalog(Path.Combine(work, "profiles"));
        string slot = slots.CreateSlot();
        var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        string path = slots.GetProgressPath(slot);
        DateTime now = new DateTime(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc);
        var world = new OfflinePartnerConversationWorld(session, OfficialPartnerConversationWorldAssetCatalog.LoadDefault());
        var player = new OfflinePartnerConversationPlayer(session, character, conversations, world, () => now,
            _ => throw new InvalidOperationException("Uncommitted state must not reach the live update callback."));
        int notifications = 0; long identity = 8000; bool limited = true;
        var service = new OfflinePartnerConversationService(session, character, player, 7001, () => ++identity,
            () => limited, 8, () => { Require(File.Exists(path), "Notify after disk commit"); notifications++; });
        Require(session.ApplyCharacterMutation(value => {
            value.CharacterCreated = true; value.CharacterName = "通话事务隔离存档"; value.Level = 1;
            value.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 8402, Level = 40 });
        }).IsApplied, "Create actual isolated profile");
        Require(service.RefreshOnPanelOpen().Status == ProgressionResultStatus.Unchanged && notifications == 0
            && session.Save.Player.CharacterProperties.NpcConversations.Records.Count == 0, "Locked original function cannot create dialogue");
        limited = false;
        Require(service.RefreshOnPanelOpen().IsApplied && notifications == 1, "Explicit panel opening creates eligible original dialogue");
        var saved = session.Save.Player.CharacterProperties.NpcConversations.Records.Single();
        long dialogueId = saved.DialogueId, npcId = saved.NpcId;
        Require(saved.NpcClassId == 8402 && saved.DialogueClassId == 2021 && saved.CurrentNode == 1
            && saved.PlayerId == 7001 && saved.DialogueNodes.SequenceEqual(new[] { 1 })
            && session.Save.Player.CharacterProperties.NpcConversations.DayLimits.Count == 0,
            "Original NPC order, start identity and chain qualification; unrestricted start consumes no daily quota");
        Require(service.RefreshOnPanelOpen().Status == ProgressionResultStatus.Unchanged && notifications == 1,
            "Reopening never duplicates a started conversation");
        int initialNotifications = notifications;
        byte[] before = File.ReadAllBytes(path);
        Require(!service.Acknowledge(dialogueId, 777777).IsApplied && notifications == initialNotifications
            && File.ReadAllBytes(path).SequenceEqual(before), "Invalid successor cannot alter file, rewards or notification stream");
        var expected = new Dictionary<int, long>();
        var acknowledged = new List<int>();
        bool failureExercised = false;
        int steps = 0;
        while (true)
        {
            saved = session.Save.Player.CharacterProperties.NpcConversations.Records.Single();
            var current = conversations.Get(2021, saved.CurrentNode);
            if ((current.NextNodes?.Count ?? 0) == 0) break;
            int node = current.NextNodes[0];
            var next = conversations.Get(2021, node);
            if (!failureExercised && (next.RewardItem?.Count ?? 0) > 0)
            {
                before = File.ReadAllBytes(path);
                int count = notifications;
                string temporary = path + ".tmp";
                Require(!File.Exists(temporary) && !Directory.Exists(temporary), "Own temporary path unused");
                Directory.CreateDirectory(temporary); bool failed = false;
                try { service.Acknowledge(dialogueId, node); }
                catch (IOException) { failed = true; }
                catch (UnauthorizedAccessException) { failed = true; }
                finally { Directory.Delete(temporary); }
                Require(failed && notifications == count && File.ReadAllBytes(path).SequenceEqual(before)
                    && session.Save.Player.CharacterProperties.NpcConversations.Records.Single().CurrentNode == saved.CurrentNode
                    && !session.Save.Player.AppliedCharacterEventIds.Contains("npc-dialogue-node:" + dialogueId + ":" + node),
                    "Actual disk failure rolls back reward, history, receipt and notification together");
                failureExercised = true;
            }
            now = now.AddSeconds(1);
            var result = service.Acknowledge(dialogueId, node);
            Require(result.IsApplied, "Original successor accepted: " + result.Message);
            acknowledged.Add(node);
            foreach (var reward in next.RewardItem ?? new Dictionary<int, long>())
            { expected.TryGetValue(reward.Key, out long count); expected[reward.Key] = count + reward.Value; }
            Require(session.Save.Player.Inventory.Where(row => row.Count > 0).Count() == expected.Count
                && expected.All(row => session.InventoryCount(row.Key) == row.Value), "Every original reward count is committed exactly once");
            Require(++steps < 6707, "Actual original dialogue path terminates");
        }
        Require(failureExercised && expected.Count > 0 && saved.DialogueStatus == PartnerConversationStatus.End
            && saved.DialogueNodes.SequenceEqual(new[] { 1 }.Concat(acknowledged)) && notifications == initialNotifications + steps,
            "Original end state, complete history and one post-commit notification per step");
        session.Reload();
        before = File.ReadAllBytes(path); int after = notifications;
        foreach (int node in acknowledged)
            Require(service.Acknowledge(dialogueId, node).Status == ProgressionResultStatus.Unchanged, "Confirmed node replay is idempotent");
        Require(notifications == after && File.ReadAllBytes(path).SequenceEqual(before), "History review cannot claim rewards again");
        Require(session.Save.Player.CharacterProperties.NpcConversations.Records.Count == 1,
            "Finishing/reading a save does not automatically create the next conversation");
        Require(service.RefreshOnPanelOpen().IsApplied, "Next explicit panel opening follows the original completed-chain condition");
        var continuation = session.Save.Player.CharacterProperties.NpcConversations.Records.Single(row => row.DialogueClassId == 2022);
        Require(continuation.NpcId == npcId && continuation.PlayerId == 7001 && continuation.DialogueId != dialogueId
            && continuation.DateTimeTicks == now.Ticks && session.Save.Player.CharacterProperties.NpcConversations.DayLimits.Count == 0,
            "User-confirmed same-day continuation keeps the original NPC identity and no invented daily limit");
        // Every selected official node currently has RewardFriendship=0. An
        // explicit in-memory synthetic value exercises the general atomic
        // reward branch; it is restored and never enters production data.
        Require(conversations.Nodes.All(row => row.RewardFriendship == 0), "Selected original friendship reward scope");
        var level40 = partner.Rows("npc_friendship_level").Single(row => Convert.ToInt32(row["Id"]) == 8402 && Convert.ToInt32(row["Level"]) == 40);
        Require(session.ApplyCharacterMutation(value => value.SubRank = Convert.ToInt32(level40["SubRankLimit"])).IsApplied,
            "Use actual original rank required by friendship level 40");
        var synthetic = conversations.Get(2022, conversations.Get(2022, 1).NextNodes[0]);
        synthetic.RewardFriendship = 1;
        try
        {
            Require(service.Acknowledge(continuation.DialogueId, synthetic.Node).IsApplied, "Explicit synthetic friendship reward transaction");
            var friendship = session.Save.Player.CharacterProperties.NpcFriendships.Single(row => row.NpcClassId == 8402);
            Require(friendship.FriendshipExp == Convert.ToInt64(level40["FriendshipExp"]) + 1 && friendship.Level >= 40,
                "Original current level thresholds and rank cap consume the transaction's friendship reward");
        }
        finally { synthetic.RewardFriendship = 0; }
        Require(session.ApplyCharacterMutation(value => value.CharacterProperties.NpcFriendships.Add(
            new OfflineNpcFriendship { NpcClassId = 8406, Level = 5 })).IsApplied
            && session.InitializeMap(12, new[] { 27 }).IsApplied
            && session.SetMapRegionProgress(12, 27, 10000).IsApplied, "Prepare actual limited dialogue qualification");
        Require(service.RefreshOnPanelOpen().IsApplied, "Original map and friendship conditions allow NPC 8406");
        var daily = session.Save.Player.CharacterProperties.NpcConversations.DayLimits.Single();
        Require(daily.DayShowCount == 1 && session.Save.Player.CharacterProperties.NpcConversations.Records
            .Where(row => row.NpcClassId == 8406).All(row => row.NpcId == daily.NpcId), "Original quota is saved with its created NPC dialogue identity");
        Require(service.RefreshOnPanelOpen().Status == ProgressionResultStatus.Unchanged, "Repeated opening cannot consume a second daily count");
        var clone = JsonUtility.FromJson<OfflinePlayerProgress>(JsonUtility.ToJson(session.Save.Player));
        clone.Inventory.Add(new OfflineItemStack { ItemId = 999001, Count = 3 });
        clone.CharacterProperties.NpcFriendships.Single(row => row.NpcClassId == 8402).Level = 1;
        var clonePlayer = player.ForCharacterMutation(clone, _ => {});
        Require(clonePlayer.GetItemAmount(999001) == 3 && player.GetItemAmount(999001) == 0
            && clonePlayer.GetNpcFriendshipLevel(8402) == 1 && player.GetNpcFriendshipLevel(8402) >= 40,
            "Original conditions inside reward transaction read the cloned player, without publishing it");
        Debug.Log("PARTNER_CONVERSATION_TRANSACTIONS_PASSED: actual open-only creation, original complete dialogue 2021, exact node reward 110 x3, disk rollback, receipts, same-day 2022 chain and original daily quota. Native UI/Player not exercised.");
    }
}
