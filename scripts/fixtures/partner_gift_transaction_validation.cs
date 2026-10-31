using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;

public static class PartnerGiftTransactionValidation
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static int I(Dictionary<string, object> row, string key) => Convert.ToInt32(row[key]);
    static long L(Dictionary<string, object> row, string key) => Convert.ToInt64(row[key]);
    public static void Run()
    {
        try { Validate(); Debug.Log("PARTNER_GIFT_TRANSACTIONS_PASSED"); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static void Validate()
    {
        string repo = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        Func<string,string> read = path => File.ReadAllText(Path.Combine(repo, "unity/Assets/Official", path));
        var catalog = OfficialCharacterCatalog.Parse(read("Character/character-progression.json"),
            read("Character/item-time-limits.json"), read("CharacterCreation/wardrobe.json"),
            read("CharacterCreation/wardrobe-rules.json"), read("Character/character-overview.json"));
        var tasks = OfficialMainlineTaskCatalog.Load(File.ReadAllText(Path.Combine(repo, "unity/Assets/Game/Resources/Official/Progression/mainline-tasks.json")),
            File.ReadAllText(Path.Combine(repo, "unity/Assets/Game/Resources/Official/Progression/mainline-tasks-source-manifest.json")));
        var slots = new ProfileSlotCatalog(Path.Combine(work, "profiles"));
        string slot = slots.CreateSlot();
        var session = OfflineMainlineProgressSession.Open(slots, slot, tasks);
        var progression = new OfflineCharacterProgressionService(session, catalog);
        var npc = catalog.Rows("npc").First(row => I(row, "Id") == 8401);
        int npcId = I(npc, "Id");
        var levels = catalog.Rows("npc_friendship_level").Where(row => I(row, "Id") == npcId).OrderBy(row => I(row,"Level")).ToArray();
        var max = levels.Last();
        int rank = I(max, "SubRankLimit");
        var gift = catalog.Rows("npc_gift").First(row => L(row,"Friendship") > 0
            && catalog.Find("npc_unlock_item", I(row,"ClassId")) == null && catalog.FindItem(I(row,"ClassId")) != null);
        int giftId = I(gift,"ClassId");
        Func<long, int, ProgressionResult> seed = (exp, subRank) => session.ApplyCharacterMutation(player => {
            player.CharacterCreated = true; player.CharacterName = "伙伴隔离验证"; player.SubRank = subRank;
            player.CharacterProperties.NpcFriendships.Clear();
            player.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship {
                NpcClassId = npcId, FriendshipExp = exp, Level = session.FriendshipLevel(npcId, exp, subRank) });
            player.Inventory.Clear(); player.Inventory.Add(new OfflineItemStack { ItemId = giftId, Count = 10000 });
        });
        Require(seed(0, rank).IsApplied, "Seed profile");
        var selected = new Dictionary<int,long> { [giftId] = 1 };
        int gain = progression.NpcGiftFriendshipExperience(npcId, selected);
        Require(gain > 0, "Valid gift experience");
        long threshold = L(max,"FriendshipExp");
        Require(seed(threshold - gain, rank).IsApplied, "Seed boundary");
        var result = progression.GiveNpcGifts(npcId, selected, "reach-cap");
        Require(result.IsApplied, "A gift may reach the cap: " + result.Message);
        Require(session.Save.Player.CharacterProperties.NpcFriendships.Single().FriendshipExp == threshold, "Exact cap exp");
        Require(progression.GiveNpcGifts(npcId, selected, "reach-cap").Status == ProgressionResultStatus.Unchanged,
            "Replay remains idempotent even after reaching the cap");
        Require(seed(threshold - 1, rank).IsApplied, "Seed overflow");
        result = progression.GiveNpcGifts(npcId, selected, "overflow");
        Require(result.Status == ProgressionResultStatus.NotReady && session.InventoryCount(giftId) == 10000,
            "Overflow requires confirmation without consuming stock");
        result = progression.GiveNpcGifts(npcId, selected, "overflow", true);
        Require(result.IsApplied, "Confirmed overflow is saved");
        long expected = threshold - 1 + gain;
        session.Reload();
        Require(session.Save.Player.CharacterProperties.NpcFriendships.Single().FriendshipExp == expected
            && session.InventoryCount(giftId) == 9999, "Reload preserves overflow and stock");
        Require(seed(0, rank).IsApplied, "Seed rollback");
        var invalid = new Dictionary<int,long> { [giftId] = 10001 };
        Require(!progression.GiveNpcGifts(npcId, invalid, "insufficient").IsApplied
            && session.InventoryCount(giftId) == 10000, "Insufficient stock never partially commits");
        var auto = progression.AutoSelectNpcGifts(npcId, null);
        Require(auto.Count > 0 && auto.Values.All(value => value > 0), "Automatic selection from official thresholds");
        int exclusiveCases = 0;
        foreach (var owner in catalog.Rows("npc"))
        {
            if (!(owner["ExclusiveGiftIds"] is List<object> ids)) continue;
            foreach (var id in ids)
            {
                int itemId = Convert.ToInt32(id);
                var unlock = catalog.Find("npc_unlock_item", itemId);
                if (unlock == null || !(unlock["CanSendGift"] is bool enabled) || !enabled
                    || I(owner,"Id") == npcId || catalog.Find("npc_gift", itemId) == null) continue;
                Require(session.ApplyCharacterMutation(player => player.Inventory.Add(new OfflineItemStack { ItemId = itemId, Count = 1 })).IsApplied, "Seed exclusive gift");
                Require(!progression.NpcGiftCandidates(npcId).Contains(itemId)
                    && progression.NpcGiftFriendshipExperience(npcId, new Dictionary<int,long> { [itemId] = 1 }) == 0,
                    "Exclusive gift cannot go to another NPC");
                exclusiveCases++;
            }
        }
        Require(exclusiveCases > 0, "Actual official exclusive gift cases must be exercised");
        Debug.Log("Official exclusive gift cases checked: " + exclusiveCases);
    }
}
