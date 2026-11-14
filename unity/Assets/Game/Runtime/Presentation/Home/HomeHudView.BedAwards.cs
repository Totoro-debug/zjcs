using Zjcs.Offline.Home;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        OfflineHomeBedAwards bedAwards;
        OfflineHomeBedAwards GetBedAwards()
            => bedAwards ?? (bedAwards = new OfflineHomeBedAwards(host, canvas, PostPartnerPromptSound));
        internal void ShowBedAward(OfflineHomeCollectedReward reward)
        {
            GetBedAwards().Show(reward);
        }
    }
}
