namespace Zjcs.Official.TeamUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
public class ChallengeInvitePlayerPanel : TeamInviteSubPanel
{
    protected override bool PlayerFilter(TeamInvitePlayerInfoData infoData)
    {
        if (!services.MainPlayerAvailable) return true;
        if (!infoData.ChallengeUnlocked) return true;
        if (infoData.ActorType != TeamInviteActorType.RealPlayer) return true;
        if (infoData.ServerId != ((ITeamInvitePanelServices)services).MainPlayerServerId) return true;
        return base.PlayerFilter(infoData);
    }
}

}
