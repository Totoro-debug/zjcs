namespace Zjcs.Official.TeamUI
{
using Zjcs.Official.ProfessionUI;
using System;
using System.Collections.Generic;

public class TeamInviteComparerBase : TeamInviteComparer<TeamInviteComparerBase>
{
	private static List<(TeamInviteComparerType comparerType, ComparerOrderType orderType)> BaseFriendComparerPriorityList { get; set; } = new List<(TeamInviteComparerType, ComparerOrderType)>
	{
		(TeamInviteComparerType.Online, ComparerOrderType.Desc),
		(TeamInviteComparerType.Companion, ComparerOrderType.Desc),
		(TeamInviteComparerType.ProfessionLimit, ComparerOrderType.Desc),
		(TeamInviteComparerType.FriendShip, ComparerOrderType.Desc)
	};

	private static List<(TeamInviteComparerType comparerType, ComparerOrderType orderType)> BaseCombatRatingComparerPriorityList { get; set; } = new List<(TeamInviteComparerType, ComparerOrderType)>
	{
		(TeamInviteComparerType.Online, ComparerOrderType.Desc),
		(TeamInviteComparerType.Companion, ComparerOrderType.Desc),
		(TeamInviteComparerType.ProfessionLimit, ComparerOrderType.Desc),
		(TeamInviteComparerType.CombatRating, ComparerOrderType.Desc)
	};

	private static List<(TeamInviteComparerType comparerType, ComparerOrderType orderType)> BaseNoDataActivityBuffComparerPriorityList { get; set; } = new List<(TeamInviteComparerType, ComparerOrderType)>
	{
		(TeamInviteComparerType.Online, ComparerOrderType.Desc),
		(TeamInviteComparerType.Companion, ComparerOrderType.Desc),
		(TeamInviteComparerType.ProfessionLimit, ComparerOrderType.Desc)
	};

	private static List<(TeamInviteComparerType comparerType, ComparerOrderType orderType)> BaseActivityBuffComparerPriorityList { get; set; } = new List<(TeamInviteComparerType, ComparerOrderType)>
	{
		(TeamInviteComparerType.Online, ComparerOrderType.Desc),
		(TeamInviteComparerType.Companion, ComparerOrderType.Desc),
		(TeamInviteComparerType.ProfessionLimit, ComparerOrderType.Desc),
		(TeamInviteComparerType.ActivityBuff, ComparerOrderType.Desc),
		(TeamInviteComparerType.CombatRating, ComparerOrderType.Desc)
	};

	private static List<(TeamInviteComparerType comparerType, ComparerOrderType orderType)> BaseElementPropTypeComparerPriorityList { get; set; } = new List<(TeamInviteComparerType, ComparerOrderType)>
	{
		(TeamInviteComparerType.Online, ComparerOrderType.Desc),
		(TeamInviteComparerType.Companion, ComparerOrderType.Desc),
		(TeamInviteComparerType.ProfessionLimit, ComparerOrderType.Desc),
		(TeamInviteComparerType.ElementPropType, ComparerOrderType.Desc)
	};

	public TeamInviteComparerBase(Func<long, TeamInvitePlayerInfoData> queryFunc, bool isJoinStrikeTarget, ITeamInviteComparerServices services, PropType propType = PropType.None)
		: base(queryFunc, isJoinStrikeTarget, services, propType)
	{
	}

	public int BaseFriendComparer(long id1, long id2)
	{
		return Comparer(id1, id2, BaseFriendComparerPriorityList);
	}

	public int BaseCombatRatingComparer(long id1, long id2)
	{
		return Comparer(id1, id2, BaseCombatRatingComparerPriorityList);
	}

	public int BaseNoDataActivityBuffComparer(long id1, long id2)
	{
		return Comparer(id1, id2, BaseNoDataActivityBuffComparerPriorityList);
	}

	public int BaseActivityBuffComparer(long id1, long id2)
	{
		return Comparer(id1, id2, BaseActivityBuffComparerPriorityList);
	}

	public int BaseElementPropTypeComparer(long id1, long id2)
	{
		return Comparer(id1, id2, BaseElementPropTypeComparerPriorityList);
	}
}

}
