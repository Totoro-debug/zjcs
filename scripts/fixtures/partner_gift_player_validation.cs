using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.PartnersUI;
using Zjcs.Offline.State;
using Zjcs.Official.ProfessionUI;

[InitializeOnLoad]
public static class PartnerGiftPlayerValidation
{
    private sealed class ItemInfo : IProfessionItemInfo
    {
        public int ClassId { get; set; }
        public string Icon { get; set; }
        public string IconEffect { get; set; }
        public Quality Quality { get; set; }
        public bool CanOverlap { get; set; }
        public object GetFakeIcon() => throw new NotSupportedException("This state-only fixture does not render item icons.");
    }
    static PartnerGiftPlayerValidation()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("PartnerGiftPlayer", false)) return;
            EditorApplication.delayCall += () => {
                try { Check(); EditorApplication.Exit(0); }
                catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
                finally { SessionState.SetBool("PartnerGiftPlayer", false); }
            };
        };
    }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("PartnerGiftPlayer", true);
        EditorApplication.isPlaying = true;
    }
    private static void Check()
    {
        Require(Application.isPlaying, "actual Play Mode");
        string repo = Environment.GetEnvironmentVariable("ZJCS_REPOSITORY_ROOT");
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        var catalog = OfficialPartnerAssetCatalog.LoadDefault();
        var configuration = OfficialPartnerGiftAssetCatalog.LoadDefault();
        var rules = new OfflinePartnerGiftInventoryRules(catalog, configuration);
        var slots = new ProfileSlotCatalog(Path.Combine(work, "profiles"));
        string slot = slots.CreateSlot();
        var session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
        var infos = catalog.Rows("item").ToDictionary(row => I(row, "ClassId"), row => (IProfessionItemInfo)new ItemInfo {
            ClassId = I(row, "ClassId"), Icon = (string)row["RawIcon"], IconEffect = (string)row["IconEffect"],
            Quality = (Quality)I(row, "Quality"), CanOverlap = (bool)row["CanOverlap"] });
        bool limited = false;
        // Custom-title moderation is deliberately outside this inventory fixture.
        var player = new OfflinePartnerGiftPlayer(session, catalog, configuration, id => infos[id], () => limited,
            id => "", (id, tag) => { throw new NotSupportedException("Custom-title policy is not this fixture's subject."); }, () => 1000);
        Action<long, int, int> seed = (exp, rank, level) => {
            Require(session.ApplyCharacterMutation(value => {
                value.CharacterCreated = true; value.CharacterName = "伙伴库存验证"; value.SubRank = rank;
                value.CharacterProperties.NpcFriendships.Clear();
                value.CharacterProperties.NpcFriendships.Add(new OfflineNpcFriendship { NpcClassId = 8401, Level = level, FriendshipExp = exp });
                value.Inventory.Clear();
                foreach (int id in new[] { 1134, 1131, 1300, 1301 }) OfflineInventoryOperations.Grant(value, id, 8, 10, 0, true);
            }).IsApplied, "seed isolated actual save");
            player.Reload();
        };
        seed(0, 1, 1);
        var live = player.GetNpcByClassId(8401);
        var item = player.GetAllCanSendToNpcGift(8401).Single(value => value.Id == 1134);
        Require(live != null && item.Count == 8 && item.GetQuality() == infos[1134].Quality && ReferenceEquals(item.Info, infos[1134]), "real state and explicit metadata binding");
        Require(player.GetAllCanSendToNpcGift(8401).Select(value => value.Id).SequenceEqual(new[] { 1134, 1131, 1300 }), "inventory order and exclusive-gift exclusion");
        Require(player.GetNpcByClassId(-1) == null && !player.GetAllCanSendToNpcGift(-1).Any(), "invalid NPC view");
        limited = true; Require(player.IsAddressListFunctionLimited, "live function access"); limited = false;
        var notifications = new List<string>();
        player.OnItemCountUpdate += (data, before, source) => {
            var reopened = OfflineMainlineProgressSession.OpenForCommerce(slots, slot);
            Require(reopened.InventoryCount(data.Id) == data.Count, "save completed before inventory notification");
            notifications.Add("item:" + data.Id + ":" + before + ":" + data.Count);
        };
        player.OnNpcFriendshipLevelUpdate += (id, before, after) => {
            var reopened = OfflineMainlineProgressSession.OpenForCommerce(slots, slot);
            Require(reopened.Save.Player.CharacterProperties.NpcFriendships.Single(value => value.NpcClassId == id).Level == after, "save completed before level notification");
            notifications.Add("level:" + before + ":" + after);
        };
        player.OnNpcFriendshipUpdate += id => notifications.Add("friendship:" + id);
        long second = configuration.Get(8401, 2).FriendshipExp;
        seed(second - 200, 1, 1); notifications.Clear();
        var gifts = new Dictionary<int, long> { [1134] = 1 };
        var result = player.GiveGifts(8401, gifts, "one-preferred-gift");
        Require(result.IsApplied, "preferred gift is committed: " + result.Message);
        Require(ReferenceEquals(live, player.GetNpcByClassId(8401)) && live.FriendShipLevel == 2 && live.FriendShip == second && item.Count == 7, "stable views follow replaced player state");
        Require(notifications.SequenceEqual(new[] { "item:1134:8:7", "level:1:2", "friendship:8401" }), "previous count and original level/friendship order");
        Require(session.Save.Player.Inventory.Single(value => value.ItemId == 1134).UnitRecords.Count == 7, "tracked gift units consumed with count");
        int calls = notifications.Count;
        Require(player.GiveGifts(8401, gifts, "one-preferred-gift").Status == ProgressionResultStatus.Unchanged && notifications.Count == calls && item.Count == 7, "duplicate command is silent and idempotent");
        Require(!player.GiveGifts(8401, new Dictionary<int, long> { [1134] = 100 }, "too-many").IsApplied && item.Count == 7 && notifications.Count == calls, "insufficient inventory is atomic");
        Require(!player.GiveGifts(8401, new Dictionary<int, long> { [1301] = 1 }, "wrong-partner").IsApplied, "other partner's exclusive gift cannot be consumed");
        Require(!player.GiveGifts(8401, new Dictionary<int, long> { [1134] = -1 }, "negative").IsApplied, "negative gift rejected");
        // A second save session changes the bag while this view stays open.
        var external = OfflineMainlineProgressSession.OpenForCommerce(slots, slot);
        Require(external.ApplyCharacterMutation(value => value.Inventory.Single(stack => stack.ItemId == 1134).Count += 2).IsApplied, "external inventory change");
        player.Reload();
        Require(item.Count == 9 && notifications.Last() == "item:1134:7:9", "external reload updates the original count event");
        int beforeFailureCalls = notifications.Count;
        long beforeFailureExp = live.FriendShip;
        byte[] beforeFailureSave = File.ReadAllBytes(slots.GetProgressPath(slot));
        bool failedWrite = false;
        using (var locked = new FileStream(slots.GetProgressPath(slot), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            try { player.GiveGifts(8401, gifts, "retry-after-write-failure"); }
            catch (IOException) { failedWrite = true; }
        }
        Require(failedWrite && item.Count == 9 && live.FriendShip == beforeFailureExp && notifications.Count == beforeFailureCalls
            && File.ReadAllBytes(slots.GetProgressPath(slot)).SequenceEqual(beforeFailureSave), "failed atomic write leaves disk/state/events unchanged");
        Require(player.GiveGifts(8401, gifts, "retry-after-write-failure").IsApplied && item.Count == 8, "same command succeeds after the file lock is released");
        seed(0, 1, 2); notifications.Clear();
        Require(live.FriendShip == second, "legacy level-only save has the same official threshold in the UI");
        Require(player.GiveGifts(8401, gifts, "legacy-level-only").IsApplied && live.FriendShip == second + 200,
            "legacy level-only state gains experience from its original threshold");
        var maximum = configuration.GetMaxLevelInfo(8401);
        int topRank = catalog.Rows("npc_friendship_level").Where(row => I(row, "Id") == 8401).Max(row => I(row, "SubRankLimit"));
        seed(maximum.FriendshipExp - 200, topRank, 99); notifications.Clear();
        Require(player.GiveGifts(8401, gifts, "exact-maximum").IsApplied && live.FriendShip == maximum.FriendshipExp && live.FriendShipLevel == 100, "exact global maximum");
        seed(maximum.FriendshipExp - 1, topRank, 99); notifications.Clear();
        Require(player.GiveGifts(8401, gifts, "overflow").Status == ProgressionResultStatus.NotReady && item.Count == 8 && notifications.Count == 0, "overflow cancel leaves original inventory");
        Require(player.GiveGifts(8401, gifts, "overflow", true).IsApplied && live.FriendShip == maximum.FriendshipExp + 199 && item.Count == 7, "confirmed overflow retained");
        player.Reload(); Require(live.FriendShip == maximum.FriendshipExp + 199, "overflow restore");
        int cap = configuration.GetMaxSubRankLevel(8401, 1);
        seed(configuration.Get(8401, cap).FriendshipExp, 1, cap); notifications.Clear();
        Require(!player.GiveGifts(8401, gifts, "rank-cap").IsApplied && item.Count == 8 && notifications.Count == 0, "rank cap blocks gifting");
        int eligibilityCases = 0;
        var unlocks = catalog.Rows("npc_unlock_item").ToDictionary(row => I(row, "ClassId"));
        var itemTypes = catalog.Rows("item").ToDictionary(row => I(row, "ClassId"), row => I(row, "ItemType"));
        var owners = catalog.Rows("npc").SelectMany(row => ((List<object>)row["ExclusiveGiftIds"]).Select(value => new { Gift = Convert.ToInt32(value), Npc = I(row, "Id") })).ToDictionary(value => value.Gift, value => value.Npc);
        foreach (var npc in catalog.Rows("npc"))
            foreach (var gift in catalog.Rows("npc_gift"))
            {
                int id = I(npc, "Id"), giftId = I(gift, "ClassId");
                bool expected = I(npc, "NpcRoleType") != 2 || ((Dictionary<string, object>)npc["GiftPreferenceDict"]).ContainsKey(giftId.ToString());
                if (itemTypes[giftId] == 433)
                    expected &= unlocks.TryGetValue(giftId, out var unlock) && (bool)unlock["CanSendGift"] && (!owners.TryGetValue(giftId, out int owner) || owner == id);
                Require(rules.CanSendGiftToNpc(giftId, id) == expected, "all selected NPC/gift eligibility combinations");
                eligibilityCases++;
            }
        Require(eligibilityCases == 17020 && !rules.CanSendGiftToNpc(-1, 8401), "complete eligibility coverage");
        Debug.Log("PARTNER_GIFT_PLAYER_PASSED: actual local transactions, live views, saved-before-notify ordering, replay, inventory units, external reload, caps/overflow, " + eligibilityCases + " eligibility combinations. PlayMode=True; complete UI/Player/custom-title save not covered.");
    }
    private static int I(Dictionary<string, object> row, string key) => Convert.ToInt32(row[key]);
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Partner gift player: " + message); }
}
