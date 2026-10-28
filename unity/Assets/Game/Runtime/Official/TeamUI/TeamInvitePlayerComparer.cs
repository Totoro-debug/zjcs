namespace Zjcs.Official.TeamUI
{
using Zjcs.Official.ProfessionUI;
using System;
using System.Collections.Generic;

public class TeamInvitePlayerComparer : TeamInviteComparer<TeamInvitePlayerComparer>
{
	private static List<(TeamInviteComparerType comparerType, ComparerOrderType orderType)> PlayerFriendComparerPriorityList { get; set; } = new List<(TeamInviteComparerType, ComparerOrderType)>
	{
		(TeamInviteComparerType.NpcCollected, ComparerOrderType.Desc),
		(TeamInviteComparerType.Online, ComparerOrderType.Desc),
		(TeamInviteComparerType.Companion, ComparerOrderType.Desc),
		(TeamInviteComparerType.ProfessionLimit, ComparerOrderType.Desc),
		(TeamInviteComparerType.NpcAssistLimit, ComparerOrderType.Desc),
		(TeamInviteComparerType.NpcCollectedTime, ComparerOrderType.Asc),
		(TeamInviteComparerType.FriendShip, ComparerOrderType.Desc)
	};

	private static List<(TeamInviteComparerType comparerType, ComparerOrderType orderType)> PlayerCombatRatingComparerPriorityList { get; set; } = new List<(TeamInviteComparerType, ComparerOrderType)>
	{
		(TeamInviteComparerType.NpcCollected, ComparerOrderType.Desc),
		(TeamInviteComparerType.Online, ComparerOrderType.Desc),
		(TeamInviteComparerType.Companion, ComparerOrderType.Desc),
		(TeamInviteComparerType.ProfessionLimit, ComparerOrderType.Desc),
		(TeamInviteComparerType.NpcAssistLimit, ComparerOrderType.Desc),
		(TeamInviteComparerType.NpcCollectedTime, ComparerOrderType.Asc),
		(TeamInviteComparerType.CombatRating, ComparerOrderType.Desc)
	};

	private static List<(TeamInviteComparerType comparerType, ComparerOrderType orderType)> PlayerActivityBuffComparerPriorityList { get; set; } = new List<(TeamInviteComparerType, ComparerOrderType)>
	{
		(TeamInviteComparerType.NpcCollected, ComparerOrderType.Desc),
		(TeamInviteComparerType.Online, ComparerOrderType.Desc),
		(TeamInviteComparerType.Companion, ComparerOrderType.Desc),
		(TeamInviteComparerType.ProfessionLimit, ComparerOrderType.Desc),
		(TeamInviteComparerType.NpcAssistLimit, ComparerOrderType.Desc),
		(TeamInviteComparerType.ActivityBuff, ComparerOrderType.Desc),
		(TeamInviteComparerType.NpcCollectedTime, ComparerOrderType.Asc),
		(TeamInviteComparerType.CombatRating, ComparerOrderType.Desc)
	};

	private static List<(TeamInviteComparerType comparerType, ComparerOrderType orderType)> PlayerNoDataActivityBuffComparerPriorityList { get; set; } = new List<(TeamInviteComparerType, ComparerOrderType)>
	{
		(TeamInviteComparerType.NpcCollected, ComparerOrderType.Desc),
		(TeamInviteComparerType.Online, ComparerOrderType.Desc),
		(TeamInviteComparerType.Companion, ComparerOrderType.Desc),
		(TeamInviteComparerType.ProfessionLimit, ComparerOrderType.Desc),
		(TeamInviteComparerType.NpcAssistLimit, ComparerOrderType.Desc),
		(TeamInviteComparerType.NpcCollectedTime, ComparerOrderType.Asc)
	};

	private static List<(TeamInviteComparerType comparerType, ComparerOrderType orderType)> PlayerElementPropTypeComparerPriorityList { get; set; } = new List<(TeamInviteComparerType, ComparerOrderType)>
	{
		(TeamInviteComparerType.NpcCollected, ComparerOrderType.Desc),
		(TeamInviteComparerType.Online, ComparerOrderType.Desc),
		(TeamInviteComparerType.Companion, ComparerOrderType.Desc),
		(TeamInviteComparerType.ProfessionLimit, ComparerOrderType.Desc),
		(TeamInviteComparerType.NpcAssistLimit, ComparerOrderType.Desc),
		(TeamInviteComparerType.NpcCollectedTime, ComparerOrderType.Asc),
		(TeamInviteComparerType.ElementPropType, ComparerOrderType.Desc)
	};

	public TeamInvitePlayerComparer(Func<long, TeamInvitePlayerInfoData> queryFunc, bool isJoinStrikeTarget, ITeamInviteComparerServices services, PropType propType = PropType.None)
		: base(queryFunc, isJoinStrikeTarget, services, propType)
	{
	}

	public int PlayerFriendComparer(long id1, long id2)
	{
		return Comparer(id1, id2, PlayerFriendComparerPriorityList);
	}

	public int PlayerCombatRatingComparer(long id1, long id2)
	{
		return Comparer(id1, id2, PlayerCombatRatingComparerPriorityList);
	}

	public int PlayerActivityBuffComparer(long id1, long id2)
	{
		return Comparer(id1, id2, PlayerActivityBuffComparerPriorityList);
	}

	public int PlayerNoDataActivityBuffComparer(long id1, long id2)
	{
		return Comparer(id1, id2, PlayerNoDataActivityBuffComparerPriorityList);
	}

	public int PlayerElementPropTypeComparer(long id1, long id2)
	{
		return Comparer(id1, id2, PlayerElementPropTypeComparerPriorityList);
	}
}

}
