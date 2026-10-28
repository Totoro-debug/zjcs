namespace Zjcs.Official.TeamUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System;
using System.Collections.Generic;

public class TeamInviteRobotComparer : TeamInviteComparer<TeamInviteRobotComparer>
{
	private static List<(TeamInviteComparerType comparerType, ComparerOrderType orderType)> RobotFriendComparerPriorityList { get; set; } = new List<(TeamInviteComparerType, ComparerOrderType)>
	{
		(TeamInviteComparerType.NpcCollected, ComparerOrderType.Desc),
		(TeamInviteComparerType.Companion, ComparerOrderType.Desc),
		(TeamInviteComparerType.ProfessionLimit, ComparerOrderType.Desc),
		(TeamInviteComparerType.AssistCount, ComparerOrderType.Desc),
		(TeamInviteComparerType.NpcAssistLimit, ComparerOrderType.Desc),
		(TeamInviteComparerType.NpcCollectedTime, ComparerOrderType.Asc),
		(TeamInviteComparerType.FriendShip, ComparerOrderType.Desc)
	};

	private static List<(TeamInviteComparerType comparerType, ComparerOrderType orderType)> RobotCombatRatingComparerPriorityList { get; set; } = new List<(TeamInviteComparerType, ComparerOrderType)>
	{
		(TeamInviteComparerType.NpcCollected, ComparerOrderType.Desc),
		(TeamInviteComparerType.Companion, ComparerOrderType.Desc),
		(TeamInviteComparerType.ProfessionLimit, ComparerOrderType.Desc),
		(TeamInviteComparerType.AssistCount, ComparerOrderType.Desc),
		(TeamInviteComparerType.NpcAssistLimit, ComparerOrderType.Desc),
		(TeamInviteComparerType.NpcCollectedTime, ComparerOrderType.Asc),
		(TeamInviteComparerType.CombatRating, ComparerOrderType.Desc)
	};

	private static List<(TeamInviteComparerType comparerType, ComparerOrderType orderType)> RobotActivityBuffComparerPriorityList { get; set; } = new List<(TeamInviteComparerType, ComparerOrderType)>
	{
		(TeamInviteComparerType.NpcCollected, ComparerOrderType.Desc),
		(TeamInviteComparerType.Companion, ComparerOrderType.Desc),
		(TeamInviteComparerType.ProfessionLimit, ComparerOrderType.Desc),
		(TeamInviteComparerType.ActivityBuff, ComparerOrderType.Desc),
		(TeamInviteComparerType.AssistCount, ComparerOrderType.Desc),
		(TeamInviteComparerType.NpcAssistLimit, ComparerOrderType.Desc),
		(TeamInviteComparerType.NpcCollectedTime, ComparerOrderType.Asc),
		(TeamInviteComparerType.CombatRating, ComparerOrderType.Desc)
	};

	private static List<(TeamInviteComparerType comparerType, ComparerOrderType orderType)> RobotNoDataActivityBuffComparerPriorityList { get; set; } = new List<(TeamInviteComparerType, ComparerOrderType)>
	{
		(TeamInviteComparerType.NpcCollected, ComparerOrderType.Desc),
		(TeamInviteComparerType.Companion, ComparerOrderType.Desc),
		(TeamInviteComparerType.ProfessionLimit, ComparerOrderType.Desc),
		(TeamInviteComparerType.AssistCount, ComparerOrderType.Desc),
		(TeamInviteComparerType.NpcAssistLimit, ComparerOrderType.Desc),
		(TeamInviteComparerType.NpcCollectedTime, ComparerOrderType.Asc)
	};

	private static List<(TeamInviteComparerType comparerType, ComparerOrderType orderType)> RobotElementPropTypeComparerPriorityList { get; set; } = new List<(TeamInviteComparerType, ComparerOrderType)>
	{
		(TeamInviteComparerType.NpcCollected, ComparerOrderType.Desc),
		(TeamInviteComparerType.Companion, ComparerOrderType.Desc),
		(TeamInviteComparerType.ProfessionLimit, ComparerOrderType.Desc),
		(TeamInviteComparerType.AssistCount, ComparerOrderType.Desc),
		(TeamInviteComparerType.NpcAssistLimit, ComparerOrderType.Desc),
		(TeamInviteComparerType.NpcCollectedTime, ComparerOrderType.Asc),
		(TeamInviteComparerType.ElementPropType, ComparerOrderType.Desc)
	};

	public TeamInviteRobotComparer(Func<long, TeamInvitePlayerInfoData> queryFunc, Func<long, int> queryAssistCountFunc, bool isJoinStrikeTarget, ITeamInviteComparerServices services, PropType propType = PropType.None)
		: base(queryFunc, isJoinStrikeTarget, services, propType)
	{
		SetOnlineCheck(onlineCheck: false);
		SetQueryAssistCountFunc(queryAssistCountFunc);
	}

	public int RobotFriendComparer(long id1, long id2)
	{
		return Comparer(id1, id2, RobotFriendComparerPriorityList);
	}

	public int RobotCombatRatingComparer(long id1, long id2)
	{
		return Comparer(id1, id2, RobotCombatRatingComparerPriorityList);
	}

	public int RobotActivityBuffComparer(long id1, long id2)
	{
		return Comparer(id1, id2, RobotActivityBuffComparerPriorityList);
	}

	public int RobotNoDataActivityBuffComparer(long id1, long id2)
	{
		return Comparer(id1, id2, RobotNoDataActivityBuffComparerPriorityList);
	}

	public int RobotElementPropTypeComparer(long id1, long id2)
	{
		return Comparer(id1, id2, RobotElementPropTypeComparerPriorityList);
	}
}

}
