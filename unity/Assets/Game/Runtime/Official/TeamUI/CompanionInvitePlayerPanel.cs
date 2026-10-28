namespace Zjcs.Official.TeamUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
public class CompanionInvitePlayerPanel : TeamInviteSubPanel
{
	protected override bool PlayerFilter(TeamInvitePlayerInfoData infoData)
	{
		return base.PlayerFilter(infoData);
	}
}

}
