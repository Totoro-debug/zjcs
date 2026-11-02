using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.Partners;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Equipment;

static class PartnerConversationConditionsHarness
{
    sealed class Player : IPartnerConversationPlayer, IPartnerConversationConditionPlayer
    {
        public OfficialPartnerConversationCatalog Catalog;
        public Dictionary<int, int> Levels = new Dictionary<int, int>();
        public Dictionary<string, int> Stories = new Dictionary<string, int>();
        public List<PartnerConversationRecord> Ended = new List<PartnerConversationRecord>();
        public int Progress, FriendshipQueries;
        public bool Goddess;
        public IPartnerConversationConfiguration DialogueConfiguration => Catalog;
        public DateTime Now => new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc);
        public void FireNpcDialogueUpdate(PartnerConversationRecord record) { }
        public int BaseLevel => 100;
        public long GetItemAmount(int id) => throw new Exception("Not in the selected conversation condition closure");
        public bool IsMapAccessible(int id) => throw new Exception("Not in the selected conversation condition closure");
        public bool RegionIsActived(int id, int region) => true;
        public bool HasNpc(int id) => Levels.ContainsKey(id);
        public int GetNpcFriendshipLevel(int id) { FriendshipQueries++; return Levels.TryGetValue(id, out var value) ? value : 0; }
        public IEnumerable<PartnerConversationRecord> GetEndDialogues() => Ended;
        public int GetStoryCount(string key) => Stories.TryGetValue(key, out var value) ? value : 0;
        public int CalculateWorldMapProgress(int id, int region) => Progress;
        public bool IsRegionGoddessActive(int id, int region) => Goddess;
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static int Main(string[] args)
    {
        var payload = (Dictionary<string, object>)OfficialEquipmentJson.Parse(System.IO.File.ReadAllText(args[0]));
        var tables = (Dictionary<string, object>)payload["tables"];
        IEnumerable<Dictionary<string, object>> Rows(string name) => ((List<object>)tables[name]).Cast<Dictionary<string, object>>();
        var baseline = new PartnerConditionRules(Rows);
        var conditions = new PartnerConversationConditionRules(baseline, Rows("conditions"));
        var catalog = OfficialPartnerConversationCatalog.Parse(System.IO.File.ReadAllText(args[1]), conditions.Bind);
        var player = new Player { Catalog = catalog };
        player.Levels[8402] = 19;
        Check(!catalog.Get(2021, 1).UnlockCondition.IsMet(player), "Original friendship gate excludes level 19");
        player.Levels[8402] = 20;
        Check(catalog.Get(2021, 1).UnlockCondition.IsMet(player), "Original friendship gate includes level 20");
        player.Levels[8402] = 40;
        Check(catalog.Get(2022, 1).UnlockCondition.IsMet(player) && !catalog.Get(2022, 1).ChainSequenceCondition.IsMet(player),
            "Higher friendship does not bypass the previous conversation");
        player.Ended.Add(new PartnerConversationRecord { DialogueClassId = 2021, DialogueStatus = PartnerConversationStatus.End });
        Check(catalog.Get(2022, 1).ChainSequenceCondition.IsMet(player), "Actual previous-conversation condition observes ended records");
        var raw = Rows("conditions").ToDictionary(row => Convert.ToInt32(row["ClassId"]), row => (Dictionary<string, object>)row["Condition"]);
        var world = conditions.Bind(raw[1219]);
        player.Progress = 9999;
        Check(!world.IsMet(player), "Original world-progress float conversion truncates below full completion");
        player.Progress = 10000;
        Check(world.IsMet(player), "Original world-progress full completion boundary");
        player.Progress = 0; player.FriendshipQueries = 0;
        Check(!catalog.Get(1001, 1).UnlockCondition.IsMet(player) && player.FriendshipQueries == 0,
            "Original AND short circuit does not read later friendship when map progress fails");
        var not = conditions.Bind(new Dictionary<string, object> { ["_type"] = "LogicalNotCondition", ["Id"] = 4314 });
        Check(not.IsMet(player), "Original logical NOT follows the real story condition");
        player.Stories["FreyaDie2"] = 1;
        Check(!not.IsMet(player), "Original logical NOT updates after actual story count changes");
        var nodePayload = (Dictionary<string, object>)OfficialEquipmentJson.Parse(System.IO.File.ReadAllText(args[1]));
        var roots = ((List<object>)nodePayload["nodes"]).Cast<Dictionary<string, object>>();
        var goddess = conditions.Bind(roots.Select(row => (Dictionary<string, object>)row["UnlockCondition"])
            .First(row => (string)row["_type"] == "MapRegionGoddessCondition"));
        Check(!goddess.IsMet(player), "Original goddess condition reads inactive state");
        player.Goddess = true;
        Check(goddess.IsMet(player), "Original goddess condition reads activated state");
        foreach (var npc in Rows("npc")) player.Levels[Convert.ToInt32(npc["Id"])] = 100;
        player.Progress = 10000;
        int evaluated = 0;
        foreach (var node in catalog.Nodes)
        {
            node.UnlockCondition.IsMet(player); evaluated++;
            if (node.ChainSequenceCondition != null) { node.ChainSequenceCondition.IsMet(player); evaluated++; }
        }
        Check(evaluated == 6912, "All original node and chain conditions execute through the actual migrated closure");
        Console.WriteLine("PARTNER_CONVERSATION_CONDITIONS_PASSED: 6912 roots; friendship, previous endings, world progress, short circuit, story NOT and goddess boundaries.");
        return 0;
    }
}
