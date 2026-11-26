using System;
using System.Collections.Generic;
using System.Linq;
using Zjcs.Official.HomeConditions;
using Zjcs.Official.Partners;

namespace Zjcs.Offline.HomeUI
{
    // The original function-limit lookup and predicates share the current local
    // player projection, including furniture identities held outside inventory.
    public sealed class OfflineHomeFunctionLimits
    {
        private readonly Dictionary<(int type, int key0, int key1), Dictionary<string, object>> conditions;
        private readonly HomeConditionRules rules;
        private readonly Func<IHomeConditionPlayer> player;

        public OfflineHomeFunctionLimits(OfficialCurrentHomeRuleAssetCatalog configuration,
            PartnerConditionRules partnerRules, Func<IHomeConditionPlayer> player,
            Func<IReadOnlyDictionary<string, object>, bool?> policy = null)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            rules = configuration.CreateConditions(partnerRules, policy);
            conditions = configuration.Rows("function_limit").ToDictionary(row =>
                (Convert.ToInt32(row["Type"]), Convert.ToInt32(row["Key0"]), Convert.ToInt32(row["Key1"])),
                row => (Dictionary<string, object>)row["Condition"]);
        }

        public bool IsLimited(int type, int key0 = 0, int key1 = 0)
            => conditions.TryGetValue((type, key0, key1), out var condition) && !rules.IsMet(condition, player());
    }
}
