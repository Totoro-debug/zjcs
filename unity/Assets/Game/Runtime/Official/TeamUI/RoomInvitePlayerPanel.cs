namespace Zjcs.Official.TeamUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
public class RoomInvitePlayerPanel : TeamInviteSubPanel
{
	protected override bool PlayerFilter(TeamInvitePlayerInfoData infoData)
	{
		if (infoData.RoomIsLimit)
		{
			return true;
		}
		return base.PlayerFilter(infoData);
	}
}

}
