namespace Zjcs.Official.TeamUI
{
using Zjcs.Official.ProfessionUI;
using System.Collections.Generic;

public abstract class TeamInviteSubPanelBase : UIBase
{
	public enum TeamInviteSortType
	{
		CombatRating,
		FriendShip,
		ActivityBuff,
		LightAdd,
		DarkAdd,
		FireAdd,
		WaterAdd,
		WindAdd,
		LightReduce,
		DarkReduce,
		FireReduce,
		WaterReduce,
		WindReduce
	}

	protected HashSet<long> mExcludePlayers = new HashSet<long>();

	public List<long> PlayerList { get; protected set; } = new List<long>();

	public TeamTargetData TeamTarget { get; protected set; }

	public TeamInviteSortType? PriorSort { get; protected set; }

	public TeamInviteSortType DefaultSort { get; private set; }

	public void SetDefaultSortType(TeamInviteSortType sortType)
	{
		DefaultSort = sortType;
	}

	public virtual TeamInviteSortType GetCurSortType()
	{
		if (PriorSort.HasValue)
		{
			return PriorSort.Value;
		}
		return DefaultSort;
	}

	public TeamInviteSubPanelBase SetTeamTarget(TeamTargetData teamTarget)
	{
		TeamTarget = teamTarget;
		return this;
	}

	public TeamInviteSubPanelBase SetExcludePlayers(List<long> excludePlayers)
	{
		mExcludePlayers.Clear();
		if (excludePlayers != null)
		{
			foreach (long excludePlayer in excludePlayers)
			{
				mExcludePlayers.Add(excludePlayer);
			}
		}
		return this;
	}

	public virtual TeamInvitePlayerInfoData GetPlayerInfo(long playerId)
	{
		return null;
	}
}

}
