using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zjcs.Offline.HomeUI;
using Zjcs.Official.HomeConditions;
using Zjcs.Official.Partners;

namespace Zjcs.Offline.GachaUI
{
    public sealed class OfflineScoreRewardConfiguration
    {
        private readonly HomeConditionRules rules;
        private readonly Dictionary<string, object> condition;
        private readonly Func<IHomeConditionPlayer> player;
        public int[] BatchAchievementTypes { get; }
        public OfflineScoreRewardConfiguration(OfficialCurrentHomeRuleAssetCatalog home,
            PartnerConditionRules partnerRules, Func<IHomeConditionPlayer> player)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            var asset = Resources.Load<TextAsset>("PetGacha/score-reward-ui");
            if (!asset) throw new InvalidOperationException("Original score reward configuration is missing.");
            var data = JObject.Parse(asset.text);
            BatchAchievementTypes = data["batchAchievementTypes"].ToObject<int[]>();
            var function = data["batchFunction"];
            if ((int)function["Type"] != 189 || (int)function["Key0"] != 0 || (int)function["Key1"] != 0)
                throw new InvalidOperationException("Original score batch function identity differs.");
            condition = function["Condition"].ToObject<Dictionary<string, object>>();
            rules = home.CreateConditions(partnerRules);
        }
        public bool IsBatchLimited => !rules.IsMet(condition, player());
    }
}
