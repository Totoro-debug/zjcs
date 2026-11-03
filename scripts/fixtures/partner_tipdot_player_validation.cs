using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.Partners;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;

public static class PartnerTipDotPlayerValidation
{
    static void Require(bool value, string message)
    { if (!value) throw new Exception("Saved partner tip player: " + message); }

    public static void Check()
    {
        Require(Application.isPlaying, "actual Play Mode");
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        var slots = new ProfileSlotCatalog(Path.Combine(work, "partner-tip-player"));
        string slot = slots.CreateSlot();
        var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        var catalog = OfficialPartnerAssetCatalog.LoadDefault();
        var gifts = OfficialPartnerGiftAssetCatalog.LoadDefault();
        string repo = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
        Func<string, string> read = path => File.ReadAllText(Path.Combine(repo, "unity/Assets/Official", path));
        var character = OfficialCharacterCatalog.Parse(read("Character/character-progression.json"),
            read("Character/item-time-limits.json"), read("CharacterCreation/wardrobe.json"),
            read("CharacterCreation/wardrobe-rules.json"), read("Character/character-overview.json"));
        var selectedTypes = catalog.Rows("item").ToDictionary(row => Convert.ToInt32(row["ClassId"]), row => (ItemType)Convert.ToInt32(row["ItemType"]));
        Func<int, ItemType> itemType = id => selectedTypes.TryGetValue(id, out var type) ? type : (ItemType)character.ItemType(id);
        bool limited = false;
        var player = new OfflinePartnerTipDotPlayer(session, catalog, gifts, function => function == 65 && limited, itemType);
        var manager = player.NpcManager;
        Require(manager.GetByNpcClassId(8401) == null && !manager.Any() && player.GetItemAmount(1100) == 0,
            "original absent defaults without creating a record");
        limited = true; Require(player.IsFunctionLimit(65) && !player.IsFunctionLimit(64), "actual function provider and ordinal"); limited = false;
        var added = new List<(int, NpcMeetType)>();
        var removed = new List<int>();
        var levels = new List<(int, int, int)>();
        var receipts = new List<(int, int)>();
        var items = new List<(ItemType, long)>();
        player.OnAddNewNpc += (id, type) => {
            Require(OfflineMainlineProgressSession.OpenCharacter(slots, slot).Save.Player.CharacterProperties.NpcFriendships.Any(npc => npc.NpcClassId == id),
                "disk and live manager updated before add");
            Require(manager.GetByNpcClassId(id) != null, "stable manager follows replaced player");
            added.Add((id, type)); player.Synchronize();
        };
        player.OnRemoveNpc += id => { Require(manager.GetByNpcClassId(id) == null, "remove publishes current manager"); removed.Add(id); };
        player.OnNpcFriendshipLevelUpdate += (id, from, to) => {
            Require(manager.GetByNpcClassId(id).FriendShipLevel == to, "level state visible before callback"); levels.Add((id, from, to));
        };
        player.OnReceiveNpcProfileReward += (id, chapter) => {
            Require(manager.GetByNpcClassId(id).ReceivedProfileRewards.Contains(chapter) && session.InventoryCount(107) >= 3,
                "reward and receipt visible together");
            var saved = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            Require(saved.InventoryCount(107) == session.InventoryCount(107) && saved.Save.Player.CharacterProperties.NpcFriendships.Single(npc => npc.NpcClassId == id).ReceivedProfileRewards.Contains(chapter),
                "receipt callback follows complete persisted transaction"); receipts.Add((id, chapter));
        };
        player.OnItemCountUpdate += (item, before, source) => {
            Require(source == null, "no invented online award source"); items.Add((item.Info.ItemType, before));
        };
        Require(session.ApplyCharacterMutation(value => {
            value.CharacterCreated = true; value.CharacterName = "伙伴提示存档验证"; value.Inventory.Clear();
            value.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 8402, Level = 1, ReceivedProfileRewards = new List<int> { 1 } });
            value.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 8401, Level = 19 });
            value.Inventory.Add(new OfflineItemStack { ItemId = 1100, Count = 3 });
            value.Inventory.Add(new OfflineItemStack { ItemId = 1357, Count = 5 });
            value.Inventory.Add(new OfflineItemStack { ItemId = 42300, Count = 7 });
        }).IsApplied, "actual saved meet and inventory transaction");
        player.Synchronize();
        Require(added.SequenceEqual(new[] { (8402, NpcMeetType.Auto), (8401, NpcMeetType.Manual) }) &&
            levels.Count == 0 && receipts.Count == 0 && items.Count == 3 && items.All(item => item.Item2 == 0),
            "one original auto/manual add, distinct item types, reentrant sync does not duplicate");
        Require(items.Select(item => item.Item1).Distinct().Count() == 3 && items.Any(item => item.Item1 == ItemType.NpcGift) &&
            items.Any(item => item.Item1 == ItemType.NpcUnlockItem) && items.Any(item => item.Item1 == ItemType.NpcTalentTreePoint),
            "selected gift, unlock and talent-point enum payloads");
        Require(manager.GetByNpcClassId(8402).ReceivedProfileRewards.Contains(1), "initial history arrives with add, without a synthetic claim ACK");
        var detached = manager.GetByNpcClassId(8401);
        detached.FriendShipLevel = 99; detached.ReceivedProfileRewards.Add(999);
        Require(manager.GetByNpcClassId(8401).FriendShipLevel == 19 && manager.GetByNpcClassId(8401).ReceivedProfileRewards.Count == 0 &&
            manager.GetByNpcClassId(8401).FriendShip == gifts.Get(8401, 19).FriendshipExp, "detached NPC/receipt reads and existing legacy XP recovery");
        Require(session.ApplyCharacterMutation(value => {
            value.CharacterProperties.NpcFriendships.Single(npc => npc.NpcClassId == 8401).Level = 20;
            value.CharacterProperties.NpcTalentTrees.Add(new OfflineNpcTalentTree { NpcClassId = 8401, ActiveNodeIds = new List<int> { 1 } });
            value.Inventory.Single(item => item.ItemId == 1100).Count = 2;
        }).IsApplied, "actual level, tree and inventory replacement");
        player.Synchronize();
        Require(levels.SequenceEqual(new[] { (8401, 19, 20) }) && items.Last() == (ItemType.NpcGift, 3L) &&
            player.GetItemAmount(1100) == 2, "exact previous item count and old/new level payloads");
        ((int[])player.GetNpcTalentTreeUnlockedNodeIds(8401))[0] = 999;
        Require(player.GetNpcTalentTreeUnlockedNodeIds(8401).SequenceEqual(new[] { 1 }), "talent reads cannot mutate save");
        var profiles = new OfflinePartnerProfileService(session, new PartnerProfileRules(catalog.Rows));
        Require(profiles.Claim(8401, 1).IsApplied, "actual configured profile claim");
        player.Synchronize();
        Require(receipts.SequenceEqual(new[] { (8401, 1) }) && items.Last().Item2 == 0 && session.InventoryCount(107) == 3,
            "one receipt notification and configured reward inventory notification");
        int count = added.Count + removed.Count + levels.Count + receipts.Count + items.Count;
        Require(profiles.Claim(8401, 1).Status == ProgressionResultStatus.Unchanged, "duplicate transaction refused");
        player.Reload(); player.Synchronize();
        Require(count == added.Count + removed.Count + levels.Count + receipts.Count + items.Count, "duplicate and unchanged reload do not repeat notifications");

        var other = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        Require(other.ApplyCharacterMutation(value => {
            value.SubRank = 3;
            value.CharacterProperties.NpcFriendships.Single(npc => npc.NpcClassId == 8401).Level = 40;
            value.Inventory.RemoveAll(item => item.ItemId == 1357);
        }).IsApplied, "another owner commits complete state");
        Require(player.SubRank != 3, "another session requires explicit disk reload");
        player.Reload();
        Require(player.SubRank == 3 && levels.Last() == (8401, 20, 40) && items.Last() == (ItemType.NpcUnlockItem, 5L) &&
            player.GetItemAmount(1357) == 0, "reloaded rank, level and deleted item projection");
        count = added.Count + removed.Count + levels.Count + receipts.Count + items.Count;
        using (var locked = new FileStream(slots.GetProgressPath(slot), FileMode.Open, FileAccess.Read, FileShare.Read))
            Require(!profiles.Claim(8401, 2).IsApplied, "real write failure rolls back");
        player.Synchronize();
        Require(count == added.Count + removed.Count + levels.Count + receipts.Count + items.Count &&
            !manager.GetByNpcClassId(8401).ReceivedProfileRewards.Contains(2), "failed transaction emits no tip success events");
        Require(profiles.Claim(8401, 2).IsApplied, "real retry saves once"); player.Synchronize();
        Require(receipts.SequenceEqual(new[] { (8401, 1), (8401, 2) }), "retry adds only new receipt");
        Require(session.ApplyCharacterMutation(value => value.CharacterProperties.NpcFriendships.RemoveAll(npc => npc.NpcClassId == 8401)).IsApplied,
            "isolated removal transaction"); player.Synchronize();
        Require(removed.SequenceEqual(new[] { 8401 }) && manager.Count() == 1, "remove notification follows saved class identity");
        var reopened = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        var restored = new OfflinePartnerTipDotPlayer(reopened, catalog, gifts, _ => false, itemType);
        Require(restored.NpcManager.Count() == 1 && restored.GetItemAmount(107) == 6 && restored.SubRank == 3,
            "reopened owner reads current save without synthetic initial events");
        int pieceId = catalog.Rows("item").Where(row => Convert.ToInt32(row["ItemType"]) == 100)
            .Select(row => Convert.ToInt32(row["ClassId"]) + 2000)
            .First(id => !selectedTypes.ContainsKey(id) && character.FindItem(id) != null);
        int derivedEvents = 0;
        restored.OnItemCountUpdate += (item, previous, source) => {
            Require(item.Info.ItemType == (ItemType)character.ItemType(pieceId) && previous == 0,
                "existing generated item metadata, without approximating an unknown type"); derivedEvents++;
        };
        Require(reopened.ApplyCharacterMutation(value => value.Inventory.Add(new OfflineItemStack { ItemId = pieceId, Count = 2 })).IsApplied,
            "actual generated skill-piece inventory"); restored.Synchronize();
        Require(derivedEvents == 1 && restored.GetItemAmount(pieceId) == 2, "generated item changes reach original tip consumers");
        string beforeRead = JsonUtility.ToJson(reopened.Save);
        _ = restored.NpcManager.ToArray(); restored.Reload(); restored.Synchronize();
        Require(beforeRead == JsonUtility.ToJson(reopened.Save), "all read/sync operations preserve saved payload");
        Debug.Log("Saved original partner tip player passed: actual atomic writes, original item/meet/level/profile payloads, no duplicate or failed-save notification, detached reads and reload");
    }
}
