using System;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomeExperienceRewards
{
    // Ports of the selected client's player, ConsoleDataManager and notification
    // services. The handler keeps their original call and event order.
    public interface IHomeExperienceRewardPlayer : IHomeExperiencePlayer
    {
        event Action<int> OnLevelUpdate;
        event Action<long, long> OnBlessExpUpdate;
        event Action<int, int> OnBlessLevelUp;
        long GetCurrentExp();
    }

    public interface IHomeExperienceRewardStore
    {
        bool Inited { get; }
        long HandledExp { get; set; }
        int HandledLevel { get; set; }
    }

    public interface IHomeExperienceCombatRatingBlockers
    {
        void RegisterBlocker(HomeExperienceRewardHandler handler);
        void UnregisterBlocker(HomeExperienceRewardHandler handler);
    }

    public interface IHomeExperienceRewardBindings
    {
        IHomeExperienceRewardPlayer Player { get; }
        IHomeExperienceRewardStore Store { get; }
        bool ManagerAvailable { get; }
        SafeAction OnPlayerUpdate { get; }
        int PlayerExpItemId { get; }
        IHomeExperienceCombatRatingBlockers CombatRatingBlockers { get; }
        void ShowLevelUpNotification(int previousLevel);
    }
}
