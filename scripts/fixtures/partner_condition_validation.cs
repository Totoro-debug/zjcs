using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.Partners;
using Hub = Zjcs.Official.Partners.PartnerConditionRules.DataHub;
using DataType = Zjcs.Official.Partners.PartnerConditionRules.ConditionDataType;

public static class PartnerConditionValidation
{
    private sealed class Player : IPartnerConditionPlayer
    {
        public int BaseLevel { get; set; }
        public readonly Dictionary<int, long> Items = new Dictionary<int, long>();
        public readonly HashSet<int> Maps = new HashSet<int>(), Npcs = new HashSet<int>();
        public readonly HashSet<(int, int)> Regions = new HashSet<(int, int)>();
        public bool ThrowMap;
        public long GetItemAmount(int id) => Items.TryGetValue(id, out long count) ? count : 0;
        public bool IsMapAccessible(int id) => ThrowMap ? throw new Exception("Unexpected map evaluation") : Maps.Contains(id);
        public bool RegionIsActived(int id, int region) => Regions.Contains((id, region));
        public bool HasNpc(int id) => Npcs.Contains(id);
    }
    private static Dictionary<string, object> Node(string type, params object[] fields)
    {
        var node = new Dictionary<string, object> { ["_type"] = type };
        for (int i = 0; i < fields.Length; i += 2) node.Add((string)fields[i], fields[i + 1]);
        return node;
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run()
    {
        try { Check(); Debug.Log("PARTNER_CURRENT_CONDITIONS_PASSED"); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    private static void Check()
    {
        string repo = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT"), work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        Require(Path.GetFullPath(Application.dataPath).StartsWith(Path.GetFullPath(work) + Path.DirectorySeparatorChar), "Owned validation project");
        Func<string, string> read = name => File.ReadAllText(Path.Combine(repo, "unity/Assets/Official", name));
        var selected = OfficialPartnerCatalog.Parse(read("Partners/partner-rules.json"), read("Partners/partner-properties.json"));
        var rules = selected.Conditions;
        var player = new Player { BaseLevel = 38 };
        Require(rules.IsMet(Node("NoneCondition"), player) && !rules.IsMet(Node("AlwaysCondition"), player), "Original None/Always identities");
        foreach (var pair in new[] { (">", false), ("<", false), ("=", true), (">=", true), ("<=", true), ("!=", false) })
            Require(rules.IsMet(Node("LevelCondition", "Level", 38, "Op", pair.Item1), player) == pair.Item2, "Original operator " + pair.Item1);
        Require(rules.IsMet(Node("LevelCondition", "Level", 37, "Op", "=="), player)
            && rules.IsMet(Node("LevelCondition", "Level", 37, "Op", "unknown"), player), "Unknown operators retain original >= default");
        var hub = new Hub(new PartnerConditionRules.Player(player)); hub.Supply(DataType.PlayerBaseLevel, 3L);
        Require(!rules.IsMet(Node("LevelCondition", "Level", 38), hub), "Explicit base-level data takes priority over player");
        Require(rules.IsMet(Node("LevelCondition", "Level", 0), new Hub()), "Missing base-level data retains original zero");
        var itemNode = Node("HasItemCondition", "ClassId", 1300, "Count", 3);
        var itemHub = new Hub(); itemHub.Supply(DataType.ItemData, new PartnerConditionRules.PlayerItemDataWrap { Id = 1300, Count = 3 });
        Require(rules.IsMet(itemNode, itemHub), "Original item-data fallback");
        itemHub.Supply(DataType.ItemData, new PartnerConditionRules.PlayerItemDataWrap { Id = 1301, Count = 99 });
        Require(!rules.IsMet(itemNode, itemHub) && !rules.IsMet(itemNode, new Hub()), "Mismatched or missing item data cannot satisfy quantity");
        Require(!rules.IsMet(Node("MapUnlockCondition", "MapClassId", 11), player), "Locked map");
        player.Maps.Add(11); Require(rules.IsMet(Node("MapUnlockCondition", "MapClassId", 11), player), "Accessible map");
        Require(!rules.IsMet(Node("MapRegionActivedCondition", "MapClassId", 11, "Region", 10), player), "Inactive region");
        player.Regions.Add((11, 10)); Require(rules.IsMet(Node("MapRegionActivedCondition", "MapClassId", 11, "Region", 10), player), "Activated region");
        Require(rules.IsMet(Node("CompositeAndCondition"), player) && !rules.IsMet(Node("CompositeOrCondition"), player), "Null child lists retain empty AND/OR semantics");
        Require(rules.MeetStatus(-1, player) == NpcMeetStatus.NotMeet, "Missing NPC");
        Require(rules.MeetStatus(8406, player) == NpcMeetStatus.WaitMeet, "Current region wait condition");
        player.Maps.Clear(); player.Items[1300] = 3;
        Require(rules.MeetStatus(8401, player) == NpcMeetStatus.CanMeet, "CanMeet precedes hidden map");
        player.Npcs.Add(8401); Require(rules.MeetStatus(8401, player) == NpcMeetStatus.HadMeet, "Saved meeting takes priority");
        var requirement = rules.GetMeetRequirement(8401).Value;
        Require(requirement.ClassId == 1300 && requirement.Count == 3 && requirement.Op == ">=", "Original meet-item extraction");
        player.Items.Clear(); player.Npcs.Clear();
        Require(rules.HasManualMeetNpc(player), "Level 38 makes manual partners 8408/8409 meetable without signals");
        player.BaseLevel = 0; Require(!rules.HasManualMeetNpc(player), "No met or currently meetable manual partner");
        foreach (int id in new[] { 8404, 8411 }) { player.Npcs.Add(id); Require(rules.HasManualMeetNpc(player), "Met trader/tutor qualifies"); player.Npcs.Clear(); }
        player.Npcs.Add(3601); Require(!rules.HasManualMeetNpc(player), "Exploration boss excluded"); player.Npcs.Clear();
        player.Npcs.Add(8402); Require(!rules.HasManualMeetNpc(player), "Auto partner excluded");
        CheckCompositeOrdering();

        var catalog = OfficialCharacterCatalog.Parse(read("Character/character-progression.json"), read("Character/item-time-limits.json"),
            read("CharacterCreation/wardrobe.json"), read("CharacterCreation/wardrobe-rules.json"), read("Character/character-overview.json"), selected);
        Require(catalog.HasCurrentPartnerRules && catalog.PartnerRows("npc").Count == 148
            && catalog.PartnerRows("conditions").Count == 4496, "Selected scoped tables");
        var slots = new ProfileSlotCatalog(Path.Combine(work, "condition-profiles")); string slot = slots.CreateSlot();
        var tasks = OfficialMainlineTaskCatalog.Load(File.ReadAllText(Path.Combine(repo, "unity/Assets/Game/Resources/Official/Progression/mainline-tasks.json")),
            File.ReadAllText(Path.Combine(repo, "unity/Assets/Game/Resources/Official/Progression/mainline-tasks-source-manifest.json")));
        var session = OfflineMainlineProgressSession.Open(slots, slot, tasks);
        PartnerRosterTransactionValidation.Validate(catalog, session);
        var service = new OfflineCharacterProgressionService(session, catalog);
        Require(session.ApplyCharacterMutation(value => {
            value.Inventory.Clear(); value.CharacterProperties.NpcFriendships.Clear();
            OfflineInventoryOperations.Grant(value, 1300, 3, 10, 0, true);
        }).IsApplied, "Seed tracked meet signals");
        byte[] before = File.ReadAllBytes(slots.GetProgressPath(slot)); bool failed = false;
        using (var locked = new FileStream(slots.GetProgressPath(slot), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            try { service.MeetNpc(8401, "meet-write-retry"); } catch (IOException) { failed = true; }
        }
        Require(failed && before.SequenceEqual(File.ReadAllBytes(slots.GetProgressPath(slot)))
            && session.InventoryCount(1300) == 3 && session.Save.Player.Inventory.Single().UnitRecords.Count == 3
            && !session.Save.Player.CharacterProperties.NpcFriendships.Any(), "Failed write rolls back units, meeting and event");
        Require(service.MeetNpc(8401, "meet-write-retry").IsApplied && session.InventoryCount(1300) == 0
            && !session.Save.Player.Inventory.Any(), "Retry consumes tracked units and empty stack atomically");
        Require(service.MeetNpc(8401, "meet-write-retry").Status == ProgressionResultStatus.Unchanged, "Saved event replay");
        session.Reload(); Require(service.NpcMeetStatusOf(8401) == NpcMeetStatus.HadMeet, "Meeting reload");
        foreach (int id in new[] { 8404, 8411 })
        {
            var needed = catalog.CurrentPartnerMeetRequirement(id).Value;
            Require(session.ApplyCharacterMutation(value => OfflineInventoryOperations.Grant(value, needed.ClassId, needed.Count, 10, 0, true)).IsApplied, "Seed current trader/tutor signals");
            Require(service.MeetNpc(id, "current-meet-" + id).IsApplied && session.InventoryCount(needed.ClassId) == 0, "Current trader/tutor meeting consumes signals");
        }
        foreach (int id in Enumerable.Range(11015, 8)) Require(!service.CanMeetNpc(id) && !service.MeetNpc(id, "closed-" + id).IsApplied, "Closed collaboration partner " + id);
        Require(session.ApplyCharacterMutation(value => {
            value.CharacterCreated = false; value.Level = 1; value.HasManualMeetNpc = false;
            value.Inventory.Clear(); value.CharacterProperties.NpcFriendships.Clear();
        }).IsApplied, "Seed uncreated character");
        DateTime metAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var auto = new OfflineCharacterProgressionService(session, catalog, () => metAt, refreshDerivedProgress: false);
        Require(auto.MeetAutomaticNpcs().Status == ProgressionResultStatus.Unchanged
            && !session.Save.Player.CharacterProperties.NpcFriendships.Any(), "Browsing an uncreated profile cannot meet partners");
        Require(session.ApplyCharacterMutation(value => { value.CharacterCreated = true; value.CharacterName = "Auto"; value.Level = 0; }).IsApplied,
            "Seed existing character without auto partner");
        Require(auto.MeetAutomaticNpcs().Status == ProgressionResultStatus.Unchanged
            && !session.Save.Player.CharacterProperties.NpcFriendships.Any(), "Created character below original base-level condition cannot auto-meet");
        Require(session.ApplyCharacterMutation(value => value.Level = 1).IsApplied, "Reach original auto-meet base level");
        before = File.ReadAllBytes(slots.GetProgressPath(slot)); failed = false;
        using (var locked = new FileStream(slots.GetProgressPath(slot), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            try { var result = auto.MeetAutomaticNpcs(); Debug.Log("Auto-meet locked result: " + result.Status + ": " + result.Message); }
            catch (IOException) { failed = true; }
        }
        Require(failed && before.SequenceEqual(File.ReadAllBytes(slots.GetProgressPath(slot)))
            && !session.Save.Player.CharacterProperties.NpcFriendships.Any(), "Auto-meet write failure rolls back");
        Require(auto.MeetAutomaticNpcs().IsApplied, "Retry original auto-meet condition");
        var met = session.Save.Player.CharacterProperties.NpcFriendships.Single();
        Require(met.NpcClassId == 8402 && met.Level == 1 && met.FriendshipExp == 0
            && met.ActivateTimeTicks == metAt.Ticks && !session.Save.Player.HasManualMeetNpc,
            "Only original automatic partner and configured initial friendship, current time");
        before = File.ReadAllBytes(slots.GetProgressPath(slot));
        Require(auto.MeetAutomaticNpcs().Status == ProgressionResultStatus.Unchanged
            && before.SequenceEqual(File.ReadAllBytes(slots.GetProgressPath(slot))), "Repeated load does not rewrite meeting");
        Require(session.ApplyCharacterMutation(value => {
            var npc = value.CharacterProperties.NpcFriendships.Single();
            npc.Level = 10; npc.FriendshipExp = 12345; npc.CustomFriendshipTag = "kept";
        }).IsApplied, "Seed existing friendship progress");
        metAt = metAt.AddDays(1);
        Require(auto.MeetAutomaticNpcs().Status == ProgressionResultStatus.Unchanged, "Existing auto partner is never recreated");
        var restored = OfflineMainlineProgressSession.InspectCharacter(slots, slot).Save.Player.CharacterProperties.NpcFriendships.Single();
        Require(restored.Level == 10 && restored.FriendshipExp == 12345 && restored.CustomFriendshipTag == "kept"
            && restored.ActivateTimeTicks == metAt.AddDays(-1).Ticks, "Auto-meet preserves progress, title and original activation time");
    }
    private static void CheckCompositeOrdering()
    {
        var rows = new Dictionary<string, List<Dictionary<string, object>>> {
            ["npc"] = new List<Dictionary<string, object>>(), ["function_limit"] = new List<Dictionary<string, object>>(),
            ["conditions"] = new List<Dictionary<string, object>>() };
        Action<int, Dictionary<string, object>> add = (id, node) => rows["conditions"].Add(new Dictionary<string, object> { ["ClassId"] = id, ["Condition"] = node });
        add(1, Node("AlwaysCondition")); add(2, Node("MapUnlockCondition", "MapClassId", 11)); add(3, Node("NoneCondition"));
        add(4, Node("HasItemCondition", "ClassId", 1300, "Count", 3));
        add(5, Node("CompositeAndCondition", "Ids", new List<object> { 4 }));
        rows["npc"].Add(new Dictionary<string, object> { ["Id"] = 1, ["NpcType"] = 4,
            ["CanMeetCondition"] = Node("CompositeAndCondition", "Ids", new List<object> { 5, 4 }),
            ["CanShowCondition"] = Node("CompositeOrCondition", "Ids", new List<object> { 1, 2, 3 }), ["WaitMeetCondition"] = Node("NoneCondition") });
        var rules = new PartnerConditionRules(name => rows[name]); var player = new Player { ThrowMap = true };
        Require(!rules.IsMet(Node("CompositeAndCondition", "Ids", new List<object> { 1, 2 }), player), "AND short circuit preserves order");
        Require(rules.IsMet(Node("CompositeOrCondition", "Ids", new List<object> { 3, 2 }), player), "OR short circuit preserves order");
        Require(rules.GetMeetRequirement(1).Value.ClassId == 1300, "Meet requirement selects direct child after nested child");
        rows["npc"][0]["CanMeetCondition"] = Node("CompositeAndCondition", "Ids", new List<object> { 5 });
        rules = new PartnerConditionRules(name => rows[name]);
        Require(!rules.GetMeetRequirement(1).HasValue, "Meet-item helper does not flatten nested composites");
        bool missing = false;
        try { rules.IsMet(Node("CompositeAndCondition", "Ids", new List<object> { 999 }), player); } catch (NullReferenceException) { missing = true; }
        Require(missing, "Original runtime missing child is retained rather than silently skipped");
    }
}
