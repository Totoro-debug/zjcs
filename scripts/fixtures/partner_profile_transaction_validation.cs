using System;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.Partners;

public static class PartnerProfileTransactionValidation
{
    private sealed class Player : IPartnerProfilePlayer
    {
        public int Id, Level;
        public bool TryGetNpcFriendshipLevel(int id, out int level) { level = id == Id ? Level : 0; return id == Id; }
    }
    static void Require(bool value, string message)
    { if (!value) throw new Exception("Original partner profile transaction: " + message); }
    public static void Check()
    {
        Require(Application.isPlaying, "actual Play Mode");
        var catalog = OfficialPartnerAssetCatalog.LoadDefault();
        var rules = new PartnerProfileRules(catalog.Rows);
        var player = new Player();
        int count = 0;
        foreach (var group in catalog.Rows("npc_profile").GroupBy(row => Convert.ToInt32(row["Id"])))
        {
            var profiles = rules.GetNpcProfileInfos(group.Key);
            Require(profiles.Select(row => row.ProfileId).SequenceEqual(group.Select(row => Convert.ToInt32(row["ProfileId"]))), "original configured chapter order");
            foreach (var info in profiles)
            {
                player.Id = info.UnlockCondition.NpcClassId;
                player.Level = info.UnlockCondition.FriendshipLevel - 1;
                Require(!info.UnlockCondition.IsMet(player), "below original threshold");
                player.Level++;
                Require(info.UnlockCondition.IsMet(player), "exact original threshold");
                Require(info.UnlockCondition.GetUnlockDescription(_ => false, (_, __) => throw new Exception()) == "", "absent description stays empty");
                Require(info.UnlockCondition.GetUnlockDescription(key => key == "conditon_unlock_desc_NpcFriendshipLevel", (key, level) => "好感度" + level + "级") == "好感度" + player.Level + "级", "original description key and argument");
                Require(info.Reward.Count > 0 && info.Reward.All(reward => reward.Key > 0 && reward.Value > 0), "original rewards present");
                count++;
            }
        }
        Require(count == 385 && rules.GetNpcProfileInfos(-1) == null && rules.Get(8401, 99) == null, "complete selected profile table, unknown entries absent");
        var reverse = new PartnerProfileRules.FriendshipCondition { NpcClassId = player.Id, FriendshipLevel = player.Level, IsReverse = true };
        Require(!reverse.IsMet(player), "original reverse exact boundary"); player.Level--; Require(reverse.IsMet(player), "original reverse below boundary");

        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        var slots = new ProfileSlotCatalog(Path.Combine(work, "partner-profile-transactions"));
        string slot = slots.CreateSlot();
        var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        var service = new OfflinePartnerProfileService(session, rules);
        int notifications = 0;
        service.Received += (id, chapter) => {
            var saved = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            Require(saved.Save.Player.CharacterProperties.NpcFriendships.Single(npc => npc.NpcClassId == id).ReceivedProfileRewards.Contains(chapter), "receipt saved before notification");
            Require(saved.InventoryCount(107) == session.InventoryCount(107), "rewards saved before notification"); notifications++;
        };
        Require(!service.Claim(8401, 1).IsApplied, "uncreated cannot claim");
        Require(session.ApplyCharacterMutation(value => { value.CharacterCreated = true; value.CharacterName = "伙伴档案验证"; value.Inventory.Clear(); }).IsApplied, "isolated created character");
        Require(!service.Claim(8401, 1).IsApplied, "unmet partner cannot claim");
        Require(session.ApplyCharacterMutation(value => value.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 8401, Level = 19 })).IsApplied, "isolated friendship");
        Require(!service.Claim(8401, 1).IsApplied && notifications == 0, "below condition cannot claim");
        Require(session.ApplyCharacterMutation(value => value.CharacterProperties.NpcFriendships.Single().Level = 20).IsApplied, "exact friendship threshold");
        Require(service.Claim(8401, 1).IsApplied && session.InventoryCount(107) == 3 && notifications == 1, "configured reward and receipt commit together");
        byte[] savedBytes = File.ReadAllBytes(slots.GetProgressPath(slot));
        Require(service.Claim(8401, 1).Status == ProgressionResultStatus.Unchanged && notifications == 1 && savedBytes.SequenceEqual(File.ReadAllBytes(slots.GetProgressPath(slot))), "duplicate does not save or reward again");
        ((int[])service.ReceivedProfileRewards(8401))[0] = 999;
        Require(service.ReceivedProfileRewards(8401).SequenceEqual(new[] { 1 }), "receipt read cannot change saved history");
        Require(!service.Claim(8401, 99).IsApplied && !service.Claim(11015, 1).IsApplied, "unknown and closed collaborations refused");
        Require(session.ApplyCharacterMutation(value => value.CharacterProperties.NpcFriendships.Single().Level = 40).IsApplied, "second threshold");
        savedBytes = File.ReadAllBytes(slots.GetProgressPath(slot));
        using (var locked = new FileStream(slots.GetProgressPath(slot), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Require(!service.Claim(8401, 2).IsApplied && session.InventoryCount(107) == 3 && service.ReceivedProfileRewards(8401).SequenceEqual(new[] { 1 }) && notifications == 1, "locked file rolls back reward, receipt and notifications");
            Require(savedBytes.SequenceEqual(File.ReadAllBytes(slots.GetProgressPath(slot))), "locked file preserves bytes");
        }
        Require(service.Claim(8401, 2).IsApplied && session.InventoryCount(107) == 6 && notifications == 2, "retry grants exactly once");
        var reopened = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        var restored = new OfflinePartnerProfileService(reopened, rules);
        Require(restored.ReceivedProfileRewards(8401).SequenceEqual(new[] { 1, 2 }) && restored.Claim(8401, 2).Status == ProgressionResultStatus.Unchanged, "reopened history prevents another award");
        Require(session.ApplyCharacterMutation(value => { value.CharacterProperties.NpcFriendships.Single().Level = 60; value.Inventory.Single(item => item.ItemId == 107).Count = long.MaxValue; }).IsApplied, "isolated maximum inventory");
        Require(!service.Claim(8401, 3).IsApplied && !service.ReceivedProfileRewards(8401).Contains(3) && session.InventoryCount(107) == long.MaxValue && notifications == 2, "overflow refuses complete transaction");
        Require(session.ApplyCharacterMutation(value => value.CharacterProperties.NpcFriendships.Single().ReceivedProfileRewards.Add(2)).Status == ProgressionResultStatus.InvalidState, "duplicate saved chapters rejected");
        Require(session.ApplyCharacterMutation(value => value.CharacterProperties.NpcFriendships.Single().ReceivedProfileRewards.Add(0)).Status == ProgressionResultStatus.InvalidState, "invalid saved chapter rejected");
        var legacy = JsonUtility.FromJson<OfflineCharacterPropertyProgress>("{\"NpcFriendships\":[{\"NpcClassId\":8401,\"Level\":60}]}");
        OfflineCharacterPropertyProgress.Normalize(legacy);
        Require(legacy.NpcFriendships.Single().ReceivedProfileRewards.Count == 0, "missing legacy history keeps original empty default");
        Debug.Log("Original partner profile transactions passed: all 385 chapter thresholds/order, actual reward/receipt atomicity, disk reload, duplicate, write failure/retry and overflow");
    }
}
