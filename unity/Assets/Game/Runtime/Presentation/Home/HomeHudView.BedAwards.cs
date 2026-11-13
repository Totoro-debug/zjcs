using Zjcs.Offline.Home;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeHudView
    {
        OfflineHomeBedAwards bedAwards;
        internal void ShowBedAward(OfflineHomeCollectedReward reward)
        {
            bedAwards ??= new OfflineHomeBedAwards(host, canvas, PostPartnerPromptSound);
            bedAwards.Show(reward);
        }
    }
}
