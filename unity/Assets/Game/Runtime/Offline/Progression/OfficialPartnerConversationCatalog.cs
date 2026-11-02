using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Zjcs.Offline.Equipment;
using Zjcs.Official.Partners;

namespace Zjcs.Offline.Progression
{
    // Selected generated configuration, with the original nullable lists and
    // reward dictionaries. Condition execution belongs to the actual player.
    public sealed class OfficialPartnerConversationCatalog : IPartnerConversationConfiguration
    {
        private readonly Dictionary<(int, int), PartnerConversationNode> nodes;
        private OfficialPartnerConversationCatalog(Dictionary<(int, int), PartnerConversationNode> nodes) { this.nodes = nodes; }
        public IEnumerable<PartnerConversationNode> Nodes => nodes.Values;
        public PartnerConversationNode Get(int dialogueClassId, int node) =>
            nodes.TryGetValue((dialogueClassId, node), out var row) ? row : null;

        public static OfficialPartnerConversationCatalog Parse(string json,
            Func<IReadOnlyDictionary<string, object>, IPartnerConversationCondition> bindCondition)
        {
            if (bindCondition == null) throw new ArgumentNullException(nameof(bindCondition));
            var payload = Map(OfficialEquipmentJson.Parse(json));
            if ((string)payload["manifest"] != "884_156692") throw new FormatException("Selected conversation version differs.");
            var nodes = new Dictionary<(int, int), PartnerConversationNode>();
            IPartnerConversationCondition Condition(object value)
            {
                if (value == null) return null;
                return bindCondition(Map(value)) ?? throw new InvalidOperationException("Original conversation condition is unbound.");
            }
            foreach (var value in (List<object>)payload["nodes"])
            {
                var row = Map(value);
                var node = new PartnerConversationNode {
                    ClassId = Int(row["ClassId"]), Node = Int(row["Node"]),
                    Type = (PartnerConversationType)Int(row["Type"]), SpokesMan = Int(row["SpokesMan"]),
                    NextIds = Integers(row["NextIds"]), NextNodes = Integers(row["NextNodes"]),
                    IsDayLimit = (bool)row["IsDayLimit"], UnlockCondition = Condition(row["UnlockCondition"]),
                    ChainSequenceCondition = Condition(row["ChainSequenceCondition"]),
                    RewardFriendship = Int(row["RewardFriendship"]),
                    RewardItem = row["RewardItem"] == null ? null : Map(row["RewardItem"]).ToDictionary(
                        item => int.Parse(item.Key, CultureInfo.InvariantCulture), item => Convert.ToInt64(item.Value, CultureInfo.InvariantCulture))
                };
                if (node.ClassId <= 0 || node.Node <= 0 || !Enum.IsDefined(typeof(PartnerConversationType), node.Type))
                    throw new FormatException("Invalid original conversation identity/type.");
                nodes.Add((node.ClassId, node.Node), node);
            }
            foreach (var node in nodes.Values)
            {
                foreach (int next in node.NextNodes ?? Enumerable.Empty<int>())
                    if (!nodes.ContainsKey((node.ClassId, next))) throw new FormatException("Missing original next conversation node.");
                foreach (int next in node.NextIds ?? Enumerable.Empty<int>())
                    if (!nodes.ContainsKey((next, 1))) throw new FormatException("Missing original chained conversation.");
            }
            return new OfficialPartnerConversationCatalog(nodes);
        }
        private static Dictionary<string, object> Map(object value) => value as Dictionary<string, object>
            ?? throw new FormatException("Expected an original conversation object.");
        private static int Int(object value) => Convert.ToInt32(value, CultureInfo.InvariantCulture);
        private static List<int> Integers(object value) => value == null ? null : ((List<object>)value).Select(Int).ToList();
    }
}
