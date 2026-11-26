using Zjcs.Offline.Home;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        OfflineHomeBedAwards bedAwards;
        OfflineHomeBedAwards GetBedAwards()
            => bedAwards ?? (bedAwards = new OfflineHomeBedAwards(host, canvas, PostPartnerPromptSound,
                new[] { OfflineFruitTreeService.RewardItemId }));
        internal void ShowHomeAward(int itemId, long count)
            => GetBedAwards().Show(itemId, count);
        internal void ShowBedAward(OfflineHomeCollectedReward reward)
        {
            GetBedAwards().Show(reward);
        }
    }
}
