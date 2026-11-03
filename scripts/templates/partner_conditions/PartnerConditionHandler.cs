using System;
using System.Collections.Generic;
using System.Linq;
using ConditionType = Zjcs.Official.Partners.PartnerConditionRules.ConditionType;

namespace Zjcs.Official.Partners
{
    // Subscription descriptors carry the original Type/composite structure.
    // Predicates remain in the migrated condition/profile/talent rules.
    public sealed class PartnerConditionHandler
    {
        public interface ICondition
        {
            ConditionType Type { get; }
            bool IsComposite { get; }
        }
        public sealed class Condition : ICondition
        {
            public ConditionType Type { get; }
            public bool IsComposite => false;
            public Condition(ConditionType type) { Type = type; }
        }
        public sealed class CompositeCondition : ICondition
        {
            public ConditionType Type { get; }
            public bool IsComposite => true;
            private readonly List<ICondition> children;
            public CompositeCondition(ConditionType type, IEnumerable<ICondition> children)
            {
                Type = type;
                this.children = children?.ToList() ?? throw new ArgumentNullException(nameof(children));
            }
            public List<ICondition> GetConditions() => new List<ICondition>(children);
        }
        private readonly object mPlayer;
        private readonly Action<Exception, ConditionType> reportFailure;
        private readonly Action onConditionMapChange;
        /*FIELDS*/

        public PartnerConditionHandler(object player, Action<Exception, ConditionType> reportFailure,
            Action onConditionMapChange = null)
        {
            mPlayer = player;
            this.reportFailure = reportFailure ?? throw new ArgumentNullException(nameof(reportFailure));
            this.onConditionMapChange = onConditionMapChange;
        }
        // Owner disposal follows original Destroy: clear callbacks, then unsubscribe.
        public void Destroy() { mConditionActionMap.Clear(); }

        /*HANDLER_METHODS*/

        private sealed class ConditionInfoParser
        {
            /*PARSER_METHODS*/
        }
        /*PLAYER_METHODS*/
    }

    // Resolve the same selected serialized condition tree for subscriptions.
    // No condition value, comparison or reward logic lives in this projection.
    public sealed class PartnerConditionSubscriptionCatalog
    {
        private readonly Dictionary<int, Dictionary<string, object>> rows;
        private static readonly Dictionary<string, ConditionType> types = new Dictionary<string, ConditionType>
        {
            /*CONDITION_TYPES*/
        };
        public PartnerConditionSubscriptionCatalog(IEnumerable<Dictionary<string, object>> conditionRows)
        {
            rows = conditionRows.ToDictionary(row => Convert.ToInt32(row["ClassId"]),
                row => (Dictionary<string, object>)row["Condition"]);
        }
        public PartnerConditionHandler.ICondition Create(Dictionary<string, object> node)
            => Create(node, new HashSet<int>());
        private PartnerConditionHandler.ICondition Create(Dictionary<string, object> node, HashSet<int> visiting)
        {
            if (node == null) return null;
            string name = (string)node["_type"];
            if (!types.TryGetValue(name, out var type))
                throw new NotSupportedException("Condition outside selected partner subscriptions: " + name);
            if (type != ConditionType.CompositeAnd && type != ConditionType.CompositeOr)
                return new PartnerConditionHandler.Condition(type);
            var children = new List<PartnerConditionHandler.ICondition>();
            if (node.TryGetValue("Ids", out var value) && value is List<object> ids)
                foreach (var id in ids)
                {
                    int key = Convert.ToInt32(id);
                    if (!visiting.Add(key)) throw new FormatException("Cyclic partner subscription condition: " + key);
                    if (!rows.TryGetValue(key, out var child))
                        throw new FormatException("Missing partner subscription condition: " + key);
                    children.Add(Create(child, visiting));
                    visiting.Remove(key);
                }
            return new PartnerConditionHandler.CompositeCondition(type, children);
        }
    }
}
