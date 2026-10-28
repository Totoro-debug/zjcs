namespace Zjcs.Official.TeamUI
{
using Zjcs.Official.ProfessionUI;
using System;
using System.Collections.Generic;

public class TeamInvitePlayerPanel : TeamInviteSubPanel
{
	protected override bool PlayerFilter(TeamInvitePlayerInfoData infoData)
	{
		if (infoData.TeamIsLimit)
		{
			return true;
		}
		return base.PlayerFilter(infoData);
	}

	protected override void SortPlayer(List<long> playerList, TeamInviteSortType sortType)
	{
		bool isJoinStrikeTarget = mGetTeamTarget().TargetCatalog == TeamTargetCatalog.JointStrike;
		long companionId = GetCompanionId();
		Func<long, long, int> func = null;
		switch (sortType)
		{
		case TeamInviteSortType.FriendShip:
			func = new TeamInvitePlayerComparer(GetPlayerInfo, isJoinStrikeTarget, services).SetCompanionId(companionId).PlayerFriendComparer;
			break;
		case TeamInviteSortType.CombatRating:
			func = new TeamInvitePlayerComparer(GetPlayerInfo, isJoinStrikeTarget, services).SetCompanionId(companionId).PlayerCombatRatingComparer;
			break;
		case TeamInviteSortType.ActivityBuff:
		{
			int? activityData = services.FestivalActivityId;
			func = ((activityData == null) ? new Func<long, long, int>(new TeamInvitePlayerComparer(GetPlayerInfo, isJoinStrikeTarget, services).SetCompanionId(companionId).PlayerNoDataActivityBuffComparer) : new Func<long, long, int>(new TeamInvitePlayerComparer(GetPlayerInfo, isJoinStrikeTarget, services).SetCompanionId(companionId).SetActivityId(activityData.Value).PlayerActivityBuffComparer));
			break;
		}
		default:
		{
			PropType propType = sortType.ToPropType();
			func = new TeamInvitePlayerComparer(GetPlayerInfo, isJoinStrikeTarget, services, propType).SetCompanionId(companionId).PlayerElementPropTypeComparer;
			break;
		}
		}
		playerList.Sort(func.Invoke);
	}
}

}
