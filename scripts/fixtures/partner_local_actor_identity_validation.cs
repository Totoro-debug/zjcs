using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.Partners;

public static class PartnerLocalActorIdentityValidation
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }

    static void FailWrite(string path, Action action)
    {
        byte[] before = File.ReadAllBytes(path);
        string temporary = path + ".tmp";
        Require(!File.Exists(temporary) && !Directory.Exists(temporary), "Own temporary path unused");
        Directory.CreateDirectory(temporary);
        bool failed = false;
        try { action(); }
        catch (IOException) { failed = true; }
        catch (UnauthorizedAccessException) { failed = true; }
        finally { Directory.Delete(temporary); }
        Require(failed && before.SequenceEqual(File.ReadAllBytes(path)), "Failed identity transaction preserves disk");
    }

    public static void Validate(ProfileSlotCatalog slots, OfflineMainlineProgressSession legacy,
        OfficialCharacterCatalog character, IPartnerConversationConfiguration conversations, DateTime now)
    {
        // The preceding real conversation test supplies an old-format profile
        // with two NPC instances, rewards, receipts and a daily count.
        string legacyBefore = JsonUtility.ToJson(legacy.Save.Player.CharacterProperties.NpcConversations);
        long[] idsBefore = legacy.Save.Player.CharacterProperties.NpcConversations.Records
            .Select(row => row.DialogueId).ToArray();
        Require(legacy.EnsureLocalActorIdentities().IsApplied, "Migrate actual old conversation identities");
        var adopted = legacy.Save.Player.CharacterProperties.LocalActorIdentities;
        Require(adopted.PlayerId == 7001 && adopted.Npcs.Count == 2
            && legacyBefore == JsonUtility.ToJson(legacy.Save.Player.CharacterProperties.NpcConversations)
            && idsBefore.SequenceEqual(legacy.Save.Player.CharacterProperties.NpcConversations.Records.Select(row => row.DialogueId)),
            "Migration preserves original owner, NPC, history, dialogue and quota identities");
        Require(legacy.EnsureLocalActorIdentities().Status == ProgressionResultStatus.Unchanged, "Identity migration is idempotent");
        Require(!legacy.ApplyCharacterMutation(value => value.CharacterProperties.LocalActorIdentities.Npcs[0].NpcId
            = value.CharacterProperties.LocalActorIdentities.PlayerId).IsApplied, "Reject cross-category identity collision");
        Require(!legacy.ApplyCharacterMutation(value => value.CharacterProperties.NpcConversations.Records[0].PlayerId++)
            .IsApplied, "Reject records owned by a different actor");

        string slot = slots.CreateSlot();
        string path = slots.GetProgressPath(slot);
        var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        Require(session.EnsureLocalActorIdentities().Status == ProgressionResultStatus.Unchanged,
            "Identity access does not create a character");
        Require(session.ApplyCharacterMutation(value => {
            value.CharacterCreated = true; value.CharacterName = "本地伙伴身份隔离验证";
            value.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 8402, Level = 40 });
        }).IsApplied, "Prepare real local profile");
        FailWrite(path, () => session.EnsureLocalActorIdentities());
        Require(session.Save.Player.CharacterProperties.LocalActorIdentities.PlayerId == 0,
            "Failed migration does not publish allocated actor IDs");
        Require(session.EnsureLocalActorIdentities().IsApplied, "Retry migration after real disk failure");
        var identities = session.Save.Player.CharacterProperties.LocalActorIdentities;
        Require(identities.PlayerId == 1 && identities.NpcId(8402) == 2 && identities.LastAllocatedId == 2
            && session.Save.Player.CharacterProperties.NpcConversations.Records.Count == 0,
            "Actor mappings are stable and never trigger a new conversation");
        session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        Require(session.Save.Player.CharacterProperties.LocalActorIdentities.NpcId(8402) == 2, "Reload stable actor identity");
        var world = new OfflinePartnerConversationWorld(session, OfficialPartnerConversationWorldAssetCatalog.LoadDefault());
        var player = new OfflinePartnerConversationPlayer(session, character, conversations, world, () => now,
            _ => throw new Exception("Uncommitted node notification"));
        int notifications = 0;
        var service = new OfflinePartnerConversationService(session, character, player, () => false, 8, () => notifications++);
        FailWrite(path, () => service.RefreshOnPanelOpen());
        Require(notifications == 0 && session.Save.Player.CharacterProperties.LocalActorIdentities.LastAllocatedId == 2
            && session.Save.Player.CharacterProperties.NpcConversations.Records.Count == 0,
            "Failed creation rolls back the sequence, record and notification together");
        Require(service.RefreshOnPanelOpen().IsApplied && notifications == 1, "Production allocator creates original conversation");
        var original = session.Save.Player.CharacterProperties.NpcConversations.Records.Single();
        Require(original.PlayerId == 1 && original.NpcId == 2 && original.DialogueId == 3 && original.DialogueClassId == 2021,
            "Production host uses persisted actor IDs and clone-local allocation");
        byte[] before = File.ReadAllBytes(path);
        Require(service.RefreshOnPanelOpen().Status == ProgressionResultStatus.Unchanged
            && before.SequenceEqual(File.ReadAllBytes(path)), "Repeated opening cannot allocate unused IDs");
        Require(session.ApplyCharacterMutation(value => value.CharacterProperties.NpcFriendships.Add(
            new OfflineNpcFriendship { NpcClassId = 8406, Level = 0 })).IsApplied, "Newly met NPC before original dialogue eligibility");
        Require(service.RefreshOnPanelOpen().IsApplied
            && session.Save.Player.CharacterProperties.LocalActorIdentities.NpcId(8406) == 4
            && session.Save.Player.CharacterProperties.NpcConversations.Records.Count == 1,
            "Ineligible new NPC gets a stable actor mapping without a fabricated dialogue");
        Require(service.Acknowledge(3, conversations.Get(2021, 1).NextNodes[0]).IsApplied, "Real node receipt before history removal fixture");
        // Explicit synthetic deletion exercises retained high-water state. The
        // production UI has no delete-history action.
        Require(session.ApplyCharacterMutation(value => value.CharacterProperties.NpcConversations.Records.Clear()).IsApplied,
            "Synthetic history removal preserves durable actor sequence");
        Require(service.RefreshOnPanelOpen().IsApplied
            && session.Save.Player.CharacterProperties.NpcConversations.Records.Single().DialogueId == 5,
            "Removed history never causes a new dialogue to reuse a rewarded identity");
        Require(session.ApplyCharacterMutation(value => value.CharacterProperties.LocalActorIdentities.LastAllocatedId = long.MaxValue)
            .IsApplied, "Prepare explicit overflow fixture");
        Require(session.ApplyCharacterMutation(value => value.CharacterProperties.NpcConversations.Records.Clear()).IsApplied,
            "Remove synthetic test history for next creation");
        before = File.ReadAllBytes(path);
        Require(service.RefreshOnPanelOpen().Status == ProgressionResultStatus.InvalidState
            && before.SequenceEqual(File.ReadAllBytes(path)), "Identity exhaustion rejects creation atomically");

        string receiptSlot = slots.CreateSlot();
        var receiptSession = OfflineMainlineProgressSession.OpenCharacter(slots, receiptSlot);
        Require(receiptSession.ApplyCharacterMutation(value => {
            value.CharacterCreated = true; value.CharacterName = "旧领取凭据验证";
            value.AppliedCharacterEventIds.Add("npc-dialogue-node:9000:2");
            value.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 8402 });
        }).IsApplied && receiptSession.EnsureLocalActorIdentities().IsApplied
            && receiptSession.Save.Player.CharacterProperties.LocalActorIdentities.PlayerId == 9001
            && receiptSession.Save.Player.CharacterProperties.LocalActorIdentities.NpcId(8402) == 9002,
            "Legacy reward receipts protect removed conversation IDs during migration");
        Debug.Log("PARTNER_LOCAL_ACTOR_IDENTITIES_PASSED: legacy adoption, actual disk rollback/retry, production conversation creation, reload, no ID reuse, collision rejection and checked exhaustion.");
    }
}
