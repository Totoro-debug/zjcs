using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Zjcs.Official.Partners;
using Zjcs.Offline.Progression;

static class PartnerConversationRulesHarness
{
    sealed class Condition : IPartnerConversationCondition
    {
        public bool Allowed = true;
        public bool IsMet(IPartnerConversationPlayer player) => Allowed;
    }
    sealed class Player : IPartnerConversationPlayer, IPartnerConversationConfiguration
    {
        public Dictionary<(int, int), PartnerConversationNode> Nodes = new Dictionary<(int, int), PartnerConversationNode>();
        public IPartnerConversationConfiguration DialogueConfiguration => this;
        public DateTime Now { get; set; } = new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc);
        public PartnerConversationRecord Notified;
        public int Updates;
        public PartnerConversationNode Get(int id, int node) => Nodes.TryGetValue((id, node), out var row) ? row : null;
        public void FireNpcDialogueUpdate(PartnerConversationRecord record)
        {
            Check(record.DialogueNodes.Last() == record.CurrentNode && record.DateTime == Now,
                "Notification observes the completed original mutation");
            Notified = record; Updates++;
        }
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static int Main(string[] args)
    {
        var player = new Player();
        var source = System.IO.File.ReadAllText(args.Single());
        var catalog = OfficialPartnerConversationCatalog.Parse(source, condition => new Condition());
        foreach (var node in catalog.Nodes) player.Nodes.Add((node.ClassId, node.Node), node);
        using (var json = JsonDocument.Parse(System.IO.File.ReadAllBytes(args.Single())))
        {
            Check(json.RootElement.GetProperty("manifest").GetString() == "884_156692", "Selected configuration version");
            foreach (var row in json.RootElement.GetProperty("nodes").EnumerateArray())
            {
                var next = row.GetProperty("NextNodes");
                var node = catalog.Get(row.GetProperty("ClassId").GetInt32(), row.GetProperty("Node").GetInt32());
                Check(next.ValueKind == JsonValueKind.Null ? node.NextNodes == null : node.NextNodes.SequenceEqual(next.EnumerateArray().Select(x => x.GetInt32())),
                    "Runtime catalog retains original next-node order and nullability");
                Check(node.IsDayLimit == row.GetProperty("IsDayLimit").GetBoolean() && node.RewardFriendship == row.GetProperty("RewardFriendship").GetInt32(),
                    "Runtime catalog retains daily-limit and friendship reward values");
                var rewards = row.GetProperty("RewardItem");
                Check(rewards.ValueKind == JsonValueKind.Null ? node.RewardItem == null :
                    node.RewardItem.Count == rewards.EnumerateObject().Count() && rewards.EnumerateObject().All(x => node.RewardItem[int.Parse(x.Name)] == x.Value.GetInt64()),
                    "Runtime catalog retains exact reward item quantities");
            }
        }
        Check(catalog.Get(-1, 1) == null, "An absent original node stays absent");
        Check(player.Nodes.Count == 6707 && player.Nodes.Keys.Select(x => x.Item1).Distinct().Count() == 349, "Complete real node table");
        var created = PartnerConversationRecord.Create(7, 8402, 2021, 1, 91, player.Now);
        Check(created.PlayerId == 7 && created.NpcId == 8402 && created.DialogueId == 91 && created.DialogueClassId == 2021,
            "Creation retains supplied local identities");
        Check(created.CurrentNode == 1 && created.DialogueNodes.SequenceEqual(new[] { 1 }) &&
            created.DialogueStatus == PartnerConversationStatus.InProgress && created.DateTime == player.Now && player.Updates == 0,
            "Creation begins at original start node without an update notification");
        foreach (var pair in player.Nodes)
        {
            var node = pair.Value;
            var record = PartnerConversationRecord.Create(7, 8402, node.ClassId, 1, 92, player.Now.AddDays(-1));
            record.SetCurrentNodeAndUpdateStatus(node.Node, player);
            Check(record.DialogueNodes.SequenceEqual(new[] { 1, node.Node }) && ReferenceEquals(player.Notified, record),
                "Visited history is appended and notification retains record identity");
            bool terminal = node.NextNodes == null || node.NextNodes.Count == 0;
            Check(record.DialogueStatus == (terminal ? PartnerConversationStatus.End : PartnerConversationStatus.InProgress),
                "Real terminal and continuing nodes preserve original status");
        }
        var branch = player.Nodes.Values.First(x => x.NextNodes != null && x.NextNodes.Count > 1);
        var blocked = (Condition)player.Get(branch.ClassId, branch.NextNodes.Last()).UnlockCondition;
        blocked.Allowed = false;
        var paused = PartnerConversationRecord.Create(7, 8402, branch.ClassId, 1, 93, player.Now);
        paused.SetCurrentNodeAndUpdateStatus(branch.Node, player);
        Check(paused.DialogueStatus == PartnerConversationStatus.Pause, "One locked option pauses the original multi-option node");
        blocked.Allowed = true;
        paused.SetCurrentNodeAndUpdateStatus(branch.Node, player);
        Check(paused.DialogueStatus == PartnerConversationStatus.Pause,
            "Original method retains the previous pause status; it does not silently resume it");
        var end = player.Nodes.Values.First(x => x.ClassId == branch.ClassId && (x.NextNodes == null || x.NextNodes.Count == 0));
        paused.SetCurrentNodeAndUpdateStatus(end.Node, player);
        Check(paused.DialogueStatus == PartnerConversationStatus.End, "A terminal node ends an existing paused conversation");
        Console.WriteLine("PARTNER_CONVERSATION_RULES_PASSED: 6707 selected nodes; creation, history, branching, pause/end and notification ordering.");
        return 0;
    }
}
