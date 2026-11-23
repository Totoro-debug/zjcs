using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.BagItemQueries;
using Zjcs.Offline.BagRecycleQueries;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Official.BagItemQueries;
using Zjcs.Official.BagRecycleQueries;
using Zjcs.Official.BagTipDots;

[InitializeOnLoad]
public static class BagRecycleQueriesValidation
{
    const string Active = "Zjcs.BagRecycleQueriesValidation.Active";
    static BagRecycleQueriesValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Active, false)) return;
            SessionState.SetBool(Active, false); AudioListener.volume = 0;
            try {
                CheckBranches(); CheckLocal();
                Debug.Log("BAG_RECYCLE_QUERIES_PASSED: complete original gift/key/gem/tag queries, lookup order and failure branches, actual selected configuration, isolated profile transactions and disk reopen, independent world rank and pure current-save readers. Recycle transactions and native Bag/storage graph host remain separate.");
                EditorApplication.Exit(0);
            } catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        };
    }
    public static void Run()
    {
        string project = Environment.GetEnvironmentVariable("ZJCS_BAG_RECYCLE_PROJECT");
        if (string.IsNullOrEmpty(project) || Path.GetFullPath(Application.dataPath) != Path.Combine(Path.GetFullPath(project), "Assets"))
            throw new InvalidOperationException("Select the independent project explicitly");
        Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Active, true); EditorApplication.EnterPlaymode();
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); Debug.Log("CHECK: " + message); }
    static PlayerItemDataWrap Item(int id, long count) => new PlayerItemDataWrap { Id = id, Count = count };
    static void CheckBranches()
    {
        var probe = new Probe(); var rules = new BagRecycleQueryRules(probe);
        var zeroGift = Item(1, 0); var negativeGift = Item(2, -3);
        probe.Items.AddRange(new[] { zeroGift, negativeGift, Item(3, 1), Item(4, 1), Item(5, 1) });
        probe.Gifts[1] = probe.Gifts[2] = 10; probe.Gifts[3] = 11; probe.Gifts[4] = 12;
        probe.Npcs[10] = new PlayerNpcDataWrap { NpcClassId = 10, FriendShipLevel = 7 };
        probe.Npcs[11] = new PlayerNpcDataWrap { NpcClassId = 11, FriendShipLevel = 8 };
        probe.Max[10] = probe.Max[11] = 7;
        var result = rules.GetCanRecycleGift(probe);
        Require(result.SequenceEqual(new[] { zeroGift, negativeGift }), "Gift query preserves input objects/order, no added count gate and exact maximum equality");
        Require(probe.Trace.SequenceEqual(new[] { "gift-parser", "find:" + ItemType.NpcUnlockItem, "gift:1", "npc:10", "max:10", "gift:2", "npc:10", "max:10", "gift:3", "npc:11", "max:11", "gift:4", "npc:12", "gift:5" }), "Gift missing mapping/NPC and over-maximum branches preserve original lookup order");
        probe.Items.Clear(); probe.Trace.Clear();
        probe.Items.AddRange(new[] { Item(1, 0), Item(2, -1), Item(3, 1), Item(4, 1), Item(5, 1), Item(6, 2), Item(7, 1), Item(8, 1) });
        probe.Tags[4] = new ItemTagInfo { Id = 4, Tag = "" };
        probe.Tags[5] = new ItemTagInfo { Id = 5, Tag = "missing" };
        probe.Tags[6] = new ItemTagInfo { Id = 6, Tag = "all" };
        probe.Tags[7] = new ItemTagInfo { Id = 7, Tag = "blocked" };
        probe.Tags[8] = new ItemTagInfo { Id = 8, Tag = "empty" };
        probe.Entities["all"] = new List<WorldStageTagEntityInfo> { new WorldStageTagEntityInfo { MapClassId = 11, Region = 1 }, new WorldStageTagEntityInfo { MapClassId = 12, Region = 2 }, new WorldStageTagEntityInfo { MapClassId = 11, Region = 1 } };
        probe.Entities["blocked"] = new List<WorldStageTagEntityInfo> { new WorldStageTagEntityInfo { MapClassId = 13, Region = 3 }, new WorldStageTagEntityInfo { MapClassId = 99, Region = 9 } };
        probe.Entities["empty"] = new List<WorldStageTagEntityInfo>();
        probe.Public.Add("11:1"); probe.Public.Add("12:2");
        result = rules.GetCanRecycleDoorkey(probe);
        Require(result.Select(value => value.Id).SequenceEqual(new[] { 5, 6, 8 }), "Keys require positive count and configuration; absent/empty tag lists succeed and inactive regions fail");
        Require(probe.Trace.SequenceEqual(new[] { "find:" + ItemType.DoorKey, "tag:3", "tag:4", "tag:5", "entities:missing", "tag:6", "entities:all", "region:11:1", "region:12:2", "region:11:1", "tag:7", "entities:blocked", "region:13:3", "tag:8", "entities:empty" }), "Key query preserves count/config/tag short circuits and each ordered region lookup, including duplicates");
        probe.Trace.Clear();
        Require(!rules.IsTagInAllRegionActived(probe, null) && !rules.IsTagInAllRegionActived(probe, "") && probe.Trace.Count == 0, "Null/empty tag returns before table or player access");
        probe.Entities["null"] = null;
        Require(rules.IsTagInAllRegionActived(probe, "null"), "Explicit null entity list retains the original success branch");
        probe.Items.Clear(); probe.Trace.Clear(); probe.WorldRank = 3;
        var low = Item(11, 0); low.Info = new BagQueryItemInfo { PartInfo = new GemPartInfo { Rank = 2 } };
        var equal = Item(12, 4); equal.Info = new BagQueryItemInfo { PartInfo = new GemPartInfo { Rank = 3 } };
        var high = Item(13, 4); high.Info = new BagQueryItemInfo { PartInfo = new GemPartInfo { Rank = 4 } };
        var negative = Item(14, -2); negative.Info = new BagQueryItemInfo { PartInfo = new GemPartInfo { Rank = 1 } };
        probe.Items.AddRange(new[] { low, equal, high, negative });
        Require(rules.GetCanRecycleGem(probe).SequenceEqual(new[] { low, negative })
            && probe.Trace.SequenceEqual(new[] { "world-rank", "find:" + ItemType.Gem }),
            "Gem query reads world rank first and keeps strict lower-rank, record order, zero/negative counts and object identity");
        low.Info.PartInfo = null; bool threw = false;
        try { rules.GetCanRecycleGem(probe); } catch (NullReferenceException) { threw = true; }
        Require(threw, "Unbound gem part retains the original failure rather than silently suppressing recycling");
    }
    static void CheckLocal()
    {
        var catalog = OfflineBagRecycleCatalog.LoadDefault(); var itemCatalog = OfflineBagItemQueryCatalog.LoadDefault();
        var gift = catalog.ExclusiveGifts.First(pair => catalog.GetMaxLevel(pair.Value) > 1 && itemCatalog.Item(pair.Key).ItemType == (int)ItemType.NpcUnlockItem);
        var key = catalog.ItemTags.First(row => itemCatalog.Item(row.Id).ItemType == (int)ItemType.DoorKey
            && catalog.GetWorldStageTagEntityInfos(row.Tag)?.Count > 1);
        var entities = catalog.GetWorldStageTagEntityInfos(key.Tag);
        Require(catalog.ExclusiveGifts.Count > 100 && catalog.ItemTags.Count > 1 && entities.All(row => row.LoEntityId > 0 && row.EntityClassId > 0), "Actual selected NPC, friendship, item-tag and four-field world-stage data are packaged locally");
        Require(catalog.GetMaxLevel(int.MaxValue) == 0 && !catalog.TryGetNpcByExclusiveGift(int.MaxValue, out _), "Original missing parser lookups keep zero/false defaults");
        string root = Path.Combine(Environment.GetEnvironmentVariable("ZJCS_BAG_RECYCLE_OUTPUT"), "isolated-saves");
        var creation = CharacterCreationOfflineService.OpenDefault(root); string slot = creation.CreateSlot();
        var draft = creation.OpenDraft(slot); draft.name = "回收查询验证";
        Require(creation.TryCreateCharacter(slot, draft, out var error), error);
        Require(creation.TrySetInitialWeapon(slot, CharacterCreationOfflineService.SwordInitialWeaponType, out error), error);
        var profile = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(root), slot);
        Require(profile.ApplyCharacterMutation(player => {
            player.Inventory.Add(new OfflineItemStack { ItemId = gift.Key, Count = 3 });
            player.Inventory.Add(new OfflineItemStack { ItemId = key.Id, Count = 2 });
            player.CharacterProperties.NpcFriendships.RemoveAll(row => row.NpcClassId == gift.Value);
        }, "bag-recycle:seed").IsApplied, "Seed isolated inventory through the actual profile transaction");
        var owner = new OfflineBagRecycleQueries(() => profile.Save, catalog, itemCatalog);
        Require(!owner.GetCanRecycleGift().Any(row => row.Id == gift.Key) && !owner.GetCanRecycleDoorkey().Any(row => row.Id == key.Id), "Actual owner requires the NPC and every public map region");
        Require(profile.ApplyCharacterMutation(player => player.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship {
            NpcClassId = gift.Value, Level = catalog.GetMaxLevel(gift.Value) - 1 }), "bag-recycle:below-max").IsApplied,
            "Persist a real NPC friendship below its original configured maximum");
        Require(!owner.GetCanRecycleGift().Any(row => row.Id == gift.Key), "Current-save reader rejects the below-maximum friendship");
        Require(profile.ApplyCharacterMutation(player => player.CharacterProperties.NpcFriendships.First(row => row.NpcClassId == gift.Value).Level++, "bag-recycle:at-max").IsApplied,
            "Persist the exact original maximum friendship");
        Require(owner.GetCanRecycleGift().Single(row => row.Id == gift.Key).Count == 3, "The same owner observes replaced Save and retains the actual gift quantity");
        foreach (int map in entities.Select(row => row.MapClassId).Distinct())
            Require(profile.InitializeMap(map, catalog.RegionIds(map)).IsApplied, "Initialize an actual tag map with its complete original region coverage: " + map);
        var regions = entities.Select(row => new { row.MapClassId, row.Region }).Distinct().ToArray();
        Require(profile.ApplyInventoryTransaction("bag-recycle:partial-regions", save => {
            foreach (var row in regions.Take(regions.Length - 1)) save.World.ActivatedGoddessRegions.Add(new OfflineMapRegionKey { MapClassId = row.MapClassId, Region = row.Region });
        }).IsApplied, "Persist all but the final required public region");
        Require(!owner.GetCanRecycleDoorkey().Any(row => row.Id == key.Id), "One remaining private region still prevents key recycling");
        Require(profile.ApplyInventoryTransaction("bag-recycle:all-regions", save => {
            var row = regions.Last(); save.World.ActivatedGoddessRegions.Add(new OfflineMapRegionKey { MapClassId = row.MapClassId, Region = row.Region });
        }).IsApplied, "Persist the final required public region");
        var files = Files(root);
        Require(owner.GetCanRecycleDoorkey().Single(row => row.Id == key.Id).Count == 2
            && owner.IsTagInAllRegionActived(key.Tag) && !owner.RegionIsActived(999999, 0)
            && !owner.RegionIsActived(regions[0].MapClassId, -1)
            && !owner.RegionIsActived(regions[0].MapClassId, int.MaxValue), "Actual owner combines every region and preserves uninitialized/out-of-range failures");
        Require(Same(files, Files(root)), "Recycling and tag queries do not normalize, reload, consume, reward or write any save file");
        var reopened = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(root), slot);
        var cold = new OfflineBagRecycleQueries(() => reopened.Save, catalog, itemCatalog);
        Require(cold.GetCanRecycleGift().Single(row => row.Id == gift.Key).Count == 3
            && cold.GetCanRecycleDoorkey().Single(row => row.Id == key.Id).Count == 2, "Disk reopen restores all dependent friendship, map and item state without a second ledger");
        Require(profile.ApplyCharacterMutation(player => player.CharacterProperties.NpcFriendships.First(row => row.NpcClassId == gift.Value).Level++, "bag-recycle:over-max").IsApplied,
            "Persist an isolated above-maximum state to verify the original equality guard");
        Require(!owner.GetCanRecycleGift().Any(row => row.Id == gift.Key), "Above-maximum state does not turn equality into a greater-or-equal approximation");
        var gem = catalog.Gems.OrderBy(row => row.Rank).First();
        Require(itemCatalog.Item(gem.ItemId).ItemType == (int)ItemType.Gem && gem.Rank > 0,
            "Selected original gem part is bound to its item and configured LimitRank");
        Require(profile.ApplyCharacterMutation(player => {
            player.Inventory.Add(new OfflineItemStack { ItemId = gem.ItemId, Count = 5 });
            player.CharacterProperties.TreasurePlaneRank = gem.Rank;
        }, "bag-recycle:gem-equal").IsApplied, "Persist an actual gem with equal independent world-content rank");
        Require(!owner.GetCanRecycleGem().Any(row => row.Id == gem.ItemId), "Equal world/gem rank does not qualify");
        int personalRank = profile.Save.Player.SubRank, season = profile.Save.Player.SeasonRank;
        Require(profile.ApplyCharacterMutation(player => player.CharacterProperties.TreasurePlaneRank++, "bag-recycle:gem-lower").IsApplied,
            "Persist a higher world-content rank through the actual profile transaction");
        files = Files(root);
        Require(owner.GetCanRecycleGem().Single(row => row.Id == gem.ItemId).Count == 5
            && profile.Save.Player.SubRank == personalRank && profile.Save.Player.SeasonRank == season
            && Same(files, Files(root)), "World-content advancement changes gem eligibility without changing personal rank/season or writing during queries");
        reopened = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(root), slot);
        cold = new OfflineBagRecycleQueries(() => reopened.Save, catalog, itemCatalog);
        Require(cold.GetCanRecycleGem().Single(row => row.Id == gem.ItemId).Count == 5,
            "Disk reopen restores the same gem quantity and independent world-rank decision");
        var publicCatalog = new OfflineBagRecycleCatalog(new OfflineBagRecycleCatalog.Payload {
            Npcs = new OfflineBagRecycleCatalog.NpcRow[0], FriendshipLevels = new OfflineBagRecycleCatalog.LevelRow[0], GiftIds = new int[0],
            ItemTags = new OfflineBagRecycleCatalog.ItemTagRow[0], WorldTags = new OfflineBagRecycleCatalog.TagRow[0],
            Gems = new OfflineBagRecycleCatalog.GemRow[0],
            Maps = new[] { new OfflineBagRecycleCatalog.MapRow { ClassId = regions[0].MapClassId, IsDefaultPublicMap = true, Regions = catalog.RegionIds(regions[0].MapClassId).ToArray() } } });
        var defaultPublic = new OfflineBagRecycleQueries(() => profile.Save, publicCatalog, itemCatalog);
        Require(defaultPublic.RegionIsActived(regions[0].MapClassId, 0), "Read-only region projection retains the original visited-map Public default branch");
    }
    static Dictionary<string, byte[]> Files(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllBytes);
    static bool Same(Dictionary<string, byte[]> first, Dictionary<string, byte[]> second) => first.Count == second.Count && first.All(row => second.TryGetValue(row.Key, out var data) && row.Value.SequenceEqual(data));
    sealed class Probe : IBagRecycleQueryServices, IBagRecycleQueryTables, IBagRecycleQueryPlayer, IBagRecycleNpcManager,
        IBagRecycleNpcTable, IBagRecycleNpcLevelTable, IBagRecycleItemTagTable, IBagRecycleWorldTagTable
    {
        public readonly List<string> Trace = new List<string>();
        public readonly List<PlayerItemDataWrap> Items = new List<PlayerItemDataWrap>();
        public readonly Dictionary<int, int> Gifts = new Dictionary<int, int>(), Max = new Dictionary<int, int>();
        public readonly Dictionary<int, PlayerNpcDataWrap> Npcs = new Dictionary<int, PlayerNpcDataWrap>();
        public readonly Dictionary<int, ItemTagInfo> Tags = new Dictionary<int, ItemTagInfo>();
        public readonly Dictionary<string, List<WorldStageTagEntityInfo>> Entities = new Dictionary<string, List<WorldStageTagEntityInfo>>();
        public readonly HashSet<string> Public = new HashSet<string>();
        public IBagRecycleQueryTables Tables => this;
        public int WorldRank;
        public int PlanesRank { get { Trace.Add("world-rank"); return WorldRank; } }
        public object NpcGiftInfoParser { get { Trace.Add("gift-parser"); return null; } }
        public IBagRecycleNpcTable NpcInfoParser => this;
        public IBagRecycleNpcLevelTable NpcFriendshipLevelInfoParser => this;
        public IBagRecycleItemTagTable ItemTagInfoParser => this;
        public IBagRecycleWorldTagTable WorldStageTagMapInfoParser => this;
        public IBagRecycleNpcManager NpcManager => this;
        public IReadOnlyCollection<PlayerItemDataWrap> FindItemsByItemType(ItemType type) { Trace.Add("find:" + type); return Items; }
        public IReadOnlyCollection<PlayerItemDataWrap> FindItemsByType(ItemType type) => FindItemsByItemType(type);
        public bool TryGetNpcByExclusiveGift(int id, out int npc) { Trace.Add("gift:" + id); return Gifts.TryGetValue(id, out npc); }
        public PlayerNpcDataWrap GetByNpcClassId(int id) { Trace.Add("npc:" + id); return Npcs.TryGetValue(id, out var row) ? row : null; }
        public int GetMaxLevel(int id) { Trace.Add("max:" + id); return Max.TryGetValue(id, out int value) ? value : 0; }
        public bool TryGet(int id, out ItemTagInfo row) { Trace.Add("tag:" + id); return Tags.TryGetValue(id, out row); }
        public List<WorldStageTagEntityInfo> GetWorldStageTagEntityInfos(string tag) { Trace.Add("entities:" + tag); return Entities.TryGetValue(tag, out var rows) ? rows : null; }
        public bool RegionIsActived(int map, int region) { string key = map + ":" + region; Trace.Add("region:" + key); return Public.Contains(key); }
    }
}
