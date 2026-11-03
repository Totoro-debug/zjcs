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
using ConditionType = Zjcs.Official.Partners.PartnerConditionRules.ConditionType;
using Handler = Zjcs.Official.Partners.PartnerConditionHandler;

public static class PartnerConditionHandlerValidation
{
    static void Require(bool value, string message)
    { if (!value) throw new Exception("Original partner condition subscriptions: " + message); }
    static Handler.Condition C(ConditionType type) => new Handler.Condition(type);
    static readonly ConditionType[] ItemOrder = {
        ConditionType.HasItem, ConditionType.HasItemType, ConditionType.UsedItem, ConditionType.HasOrUsedItem,
        ConditionType.CostItem, ConditionType.GotItems, ConditionType.GotItemTypes, ConditionType.TouchOpCount,
        ConditionType.ProfessionSkillRankCount, ConditionType.HasItemTypeWithQuality, ConditionType.HasManualMeetNpc,
        ConditionType.TreasureCollect, ConditionType.HasAnyNpcExclusiveGift
    };
    static readonly ConditionType[] AddOrder = {
        ConditionType.HasNpcCount, ConditionType.NpcFriendshipLevelCount, ConditionType.NpcFriendshipLevel,
        ConditionType.HasManualMeetNpc, ConditionType.NpcMeet
    };
    static void Item(Handler handler) => handler.MPlayer_OnItemCountUpdate(null, 0, null);

    public static void Check()
    {
        Require(Application.isPlaying, "actual Play Mode");
        var failures = new List<(Exception, ConditionType)>();
        int changes = 0;
        var handler = new Handler(new object(), (error, type) => failures.Add((error, type)), () => changes++);
        var item = C(ConditionType.HasItem);
        int calls = 0;
        Action callback = () => calls++;
        handler.Register(item, callback); handler.Register(item, callback);
        Item(handler); Require(calls == 1 && changes == 2, "one callback per type with two references");
        handler.Deregister(item, callback); Item(handler); Require(calls == 2, "first deregister retains remaining reference");
        handler.Deregister(item, callback); Item(handler); Require(calls == 2, "last reference removed");

        var nested = new Handler.CompositeCondition(ConditionType.CompositeAnd, new Handler.ICondition[] {
            item, new Handler.CompositeCondition(ConditionType.CompositeOr, new Handler.ICondition[] {
                item, C(ConditionType.NpcFriendshipLevel), C(ConditionType.None) }) });
        handler.Register(nested, callback); Item(handler);
        Require(calls == 3, "composite leaves of the same type deduplicated");
        handler.Deregister(item, callback); Item(handler); Require(calls == 3, "duplicate leaves do not acquire multiple references");
        handler.MPlayer_OnNpcFriendshipLevelUpdate(8401, 19, 20); Require(calls == 4, "second composite type retained");
        handler.Deregister(nested, callback); handler.MPlayer_OnNpcFriendshipLevelUpdate(8401, 20, 21);
        Require(calls == 4, "recursive deregistration");
        foreach (var excluded in new[] { ConditionType.None, ConditionType.Exception, ConditionType.Composite })
            handler.Register(C(excluded), callback);
        handler.Register(new Handler.CompositeCondition(ConditionType.Composite, new[] { item }), callback);
        Item(handler); Require(calls == 4, "excluded composite rejected before expansion");

        var sequence = new List<string>();
        Action second = () => sequence.Add("second"), third = () => sequence.Add("third");
        Action first = () => { sequence.Add("first"); handler.Deregister(item, second); handler.Register(item, third); };
        handler.Register(item, first); handler.Register(item, second);
        Item(handler); Require(sequence.SequenceEqual(new[] { "first", "second" }), "in-flight callback removal keeps the current snapshot");
        sequence.Clear(); Item(handler);
        Require(sequence.SequenceEqual(new[] { "first", "third" }), "pending changes affect the next dispatch");
        handler.DeregisterConditionType(ConditionType.HasItem);
        Action self = null;
        self = () => { calls++; handler.Deregister(item, self); handler.Register(item, self); };
        handler.Register(item, self); Item(handler); Item(handler);
        Require(calls == 6, "pending add precedes pending remove, retaining a self-renewed reference");
        handler.DeregisterConditionType(ConditionType.HasItem);
        var failure = new InvalidOperationException("expected original per-callback boundary");
        handler.Register(item, () => { throw failure; }); handler.Register(item, callback);
        Item(handler); Require(calls == 7 && failures.Count == 1 && failures[0] == (failure, ConditionType.HasItem),
            "one failing callback is reported and does not suppress later callbacks");
        handler.Destroy(); Item(handler); Require(calls == 7, "destroy clears registered actions");

        var catalog = OfficialPartnerAssetCatalog.LoadDefault();
        var subscriptions = new PartnerConditionSubscriptionCatalog(catalog.Rows("conditions"));
        foreach (var row in catalog.Rows("npc_profile"))
            Require(subscriptions.Create((Dictionary<string, object>)row["UnlockCondition"]).Type == ConditionType.NpcFriendshipLevel,
                "all original profile predicates retain their actual subscription type");
        foreach (var row in catalog.Rows("npc"))
            foreach (string name in new[] { "CanMeetCondition", "CanShowCondition", "WaitMeetCondition" })
                _ = subscriptions.Create((Dictionary<string, object>)row[name]);
        foreach (var row in catalog.Rows("npc_talent_item"))
            foreach (string field in new[] { "TalentTreeUnlockConditionId", "TransformGiftUnlockConditionId" })
            {
                int id = Convert.ToInt32(row[field]);
                var raw = (Dictionary<string, object>)catalog.Rows("conditions").Single(value => Convert.ToInt32(value["ClassId"]) == id)["Condition"];
                _ = subscriptions.Create(raw);
            }
        foreach (var row in catalog.Rows("npc_talent_tree_npc_map"))
        {
            int id = Convert.ToInt32(row["UnlockCondition"]);
            var raw = (Dictionary<string, object>)catalog.Rows("conditions").Single(value => Convert.ToInt32(value["ClassId"]) == id)["Condition"];
            _ = subscriptions.Create(raw);
        }
        foreach (var row in catalog.Rows("function_limit").Where(row => new[] { 65, 136, 1011 }.Contains(Convert.ToInt32(row["Type"])) &&
            Convert.ToInt32(row["Key0"]) == 0 && Convert.ToInt32(row["Key1"]) == 0))
            _ = subscriptions.Create((Dictionary<string, object>)row["Condition"]);
        // Actual existing slot/session boundary, never a user save.
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        var slots = new ProfileSlotCatalog(Path.Combine(work, "partner-condition-dispatch"));
        string slot = slots.CreateSlot();
        var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        var itemTypes = catalog.Rows("item").ToDictionary(row => Convert.ToInt32(row["ClassId"]), row => (ItemType)Convert.ToInt32(row["ItemType"]));
        var player = new OfflinePartnerTipDotPlayer(session, catalog, OfficialPartnerGiftAssetCatalog.LoadDefault(), _ => false, id => itemTypes[id]);
        var trace = new List<ConditionType>();
        var owner = new OfflinePartnerTipDotConditions(player, (error, type) => { throw new Exception("Unexpected condition callback", error); });
        foreach (ConditionType type in Enum.GetValues(typeof(ConditionType)))
        {
            var key = type;
            owner.Handler.Register(C(key), () => {
                Require(OfflineMainlineProgressSession.OpenCharacter(slots, slot).InventoryCount(1100) == session.InventoryCount(1100),
                    "callback reads the complete persisted transaction");
                trace.Add(key);
            });
        }
        Require(session.ApplyCharacterMutation(value => {
            value.CharacterCreated = true; value.CharacterName = "伙伴条件调度验证"; value.Inventory.Clear();
            value.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 8401, Level = 19 });
            value.Inventory.Add(new OfflineItemStack { ItemId = 1100, Count = 3 });
        }).IsApplied, "actual saved NPC/item addition");
        player.Synchronize();
        Require(trace.SequenceEqual(AddOrder.Concat(ItemOrder)), "saved add and item events preserve the complete original dispatch order");
        trace.Clear();
        Require(session.ApplyCharacterMutation(value => value.CharacterProperties.NpcFriendships.Single(npc => npc.NpcClassId == 8401).Level = 20).IsApplied,
            "actual saved friendship threshold crossing");
        player.Synchronize();
        Require(trace.SequenceEqual(new[] { ConditionType.NpcFriendshipLevel, ConditionType.NpcFriendshipLevelCount }),
            "friendship event does not invent a max-level or talent-predecessor event");
        trace.Clear();
        var profiles = new OfflinePartnerProfileService(session, new PartnerProfileRules(catalog.Rows));
        using (var locked = new FileStream(slots.GetProgressPath(slot), FileMode.Open, FileAccess.Read, FileShare.Read))
            Require(!profiles.Claim(8401, 1).IsApplied, "actual claim save failure");
        player.Synchronize(); Require(trace.Count == 0, "failed save publishes no condition event");
        Require(profiles.Claim(8401, 1).IsApplied, "actual profile claim retry"); player.Synchronize();
        Require(trace.SequenceEqual(ItemOrder), "configured profile reward follows original inventory dispatch");
        trace.Clear(); player.Reload(); player.Synchronize(); Require(trace.Count == 0, "unchanged reload does not emit conditions");
        owner.Dispose(); owner.Dispose();
        // Register again after dispose to prove that unsubscribing, not just clearing, ended event delivery.
        owner.Handler.Register(item, () => trace.Add(ConditionType.HasItem));
        Require(session.ApplyCharacterMutation(value => value.Inventory.Single(stack => stack.ItemId == 1100).Count--).IsApplied,
            "save after graph owner disposal");
        player.Synchronize(); Require(trace.Count == 0, "disposed owner is unsubscribed");
        using (var reopened = new OfflinePartnerTipDotConditions(player, (error, type) => { throw error; }))
        {
            reopened.Handler.Register(item, () => trace.Add(ConditionType.HasItem));
            Require(session.ApplyCharacterMutation(value => value.Inventory.Single(stack => stack.ItemId == 1100).Count--).IsApplied,
                "save after reopening owner");
            player.Synchronize(); Require(trace.SequenceEqual(new[] { ConditionType.HasItem }), "reopened owner subscribes exactly once");
        }
        Debug.Log("Original partner condition dispatch passed: reference counts, composites, callback mutation/failure, original event order, real atomic save/retry/reload and owner disposal");
    }
}
