using System;
using Zjcs.Official.HomeEnvironmentTime;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.Partners;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Offline.HomeUI;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.HomeEnvironmentTime
{
    // Bridges committed local profile changes to the original condition registry.
    // The selected function-90 tree remains the only unlock predicate.
    public sealed class OfflineHomeNaturalTimeLimit : IHomeNaturalTimeLimit, IDisposable
    {
        readonly OfflineMainlineProgressSession profile;
        readonly OfflineHomeHudConditions conditions;
        readonly IHomeHudCondition condition;
        int level, subRank, firstBattleCount;
        public OfflineHomeNaturalTimeLimit(OfflineMainlineProgressSession profile,
            OfficialCurrentHomeRuleAssetCatalog configuration, PartnerConditionRules partnerRules,
            Func<int, bool> ownsFurniture)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            conditions = new OfflineHomeHudConditions(profile, configuration, partnerRules, 0,
                ownsFurniture, () => false, () => false);
            condition = conditions.GetFunctionCondition(FunctionType.HomeNaturalTime)
                ?? throw new InvalidOperationException("Selected HomeNaturalTime condition is missing.");
            Snapshot();
        }
        public bool IsLimited => !conditions.IsMet(condition);
        public void Register(Action callback) => conditions.Register(condition, callback);
        public void Deregister(Action callback) => conditions.Deregister(condition, callback);
        public void RefreshCommittedProfile()
        {
            int nextLevel = profile.Save.Player.Level, nextRank = profile.Save.Player.SubRank;
            int nextBattle = profile.StoryCount("FirstBattle");
            bool levelChanged = nextLevel != level, rankChanged = nextRank != subRank,
                storyChanged = nextBattle != firstBattleCount;
            Snapshot();
            if (levelChanged) conditions.NotifyLevelUpdate();
            if (rankChanged) { conditions.NotifySubRankUpdate(); conditions.NotifyLevelUpdate(); }
            if (storyChanged) conditions.NotifyStoryCountUpdate();
        }
        void Snapshot()
        {
            level = profile.Save.Player.Level;
            subRank = profile.Save.Player.SubRank;
            firstBattleCount = profile.StoryCount("FirstBattle");
        }
        public void Dispose() => conditions.Dispose();
    }
}
