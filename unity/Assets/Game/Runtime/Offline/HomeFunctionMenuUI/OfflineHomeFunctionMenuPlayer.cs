using System;
using Zjcs.Official.HomeFunctionMenuUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Offline.Progression;

namespace Zjcs.Offline.HomeFunctionMenuUI
{
    // Ordinary-player FunctionLimitInfoParser.IsLimit, using the selected
    // condition scope shared with the HUD rather than separate menu predicates.
    public sealed class OfflineHomeFunctionMenuPlayer : IHomeMenuPlayer
    {
        private readonly OfflineMainlineProgressSession profile;
        private readonly IHomeMainUiConditions conditions;

        public OfflineHomeFunctionMenuPlayer(OfflineMainlineProgressSession profile, IHomeMainUiConditions conditions)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.conditions = conditions ?? throw new ArgumentNullException(nameof(conditions));
        }

        public IHomeHudConditionHandler ConditionHandler => conditions.ConditionHandler;
        public bool HasPlayer => profile.Save.Player != null && profile.Save.Player.CharacterCreated;
        public event Action<int> OnLevelUpdate;
        public event Action<int> OnSubRankUpdate;

        public HomeMenuFunctionLimitInfo GetFunctionLimit(FunctionType type)
        {
            var condition = conditions.GetFunctionCondition(type);
            return condition == null ? null : new HomeMenuFunctionLimitInfo { Condition = condition };
        }
        public bool IsMet(IHomeHudCondition condition) => conditions.IsMet(condition);
        public bool IsFunctionLimit(FunctionType type)
        {
            var info = GetFunctionLimit(type);
            if (info == null) return false;
            if (!HasPlayer) return true;
            return !IsMet(info.Condition);
        }

        // Forward only the matching committed player event. These queries do
        // not mutate a profile, synthesize unlocks or poll for state changes.
        public void NotifyLevelUpdate()
        {
            if (HasPlayer) OnLevelUpdate?.Invoke(profile.Save.Player.Level);
        }
        public void NotifySubRankUpdate()
        {
            if (HasPlayer) OnSubRankUpdate?.Invoke(profile.Save.Player.SubRank);
        }
    }
}
