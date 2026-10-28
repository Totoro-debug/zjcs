namespace Zjcs.Official.TeamUI
{
using Zjcs.Official.ProfessionUI;
using System;
using System.Collections.Generic;

public abstract class TeamInviteComparer<T> where T : TeamInviteComparer<T>
{
	private PropType mPropType;
	private readonly ITeamInviteComparerServices services;

	private int mAcitivityId;

	private long mCompanionId;

	private bool mIsJoinStrikeTarget;

	private bool mOnlineCheck = true;

	private Func<long, TeamInvitePlayerInfoData> mQueryFunc;

	private Func<long, int> mQueryAssistCountFunc;

	public TeamInviteComparer(Func<long, TeamInvitePlayerInfoData> queryFunc, bool isJoinStrikeTarget, ITeamInviteComparerServices services, PropType propType = PropType.None)
	{
		this.services = services;
		mQueryFunc = queryFunc;
		mIsJoinStrikeTarget = isJoinStrikeTarget;
		mPropType = propType;
	}

	public virtual T SetActivityId(int acitivityId)
	{
		mAcitivityId = acitivityId;
		return (T)this;
	}

	public T SetCompanionId(long companionId)
	{
		mCompanionId = companionId;
		return (T)this;
	}

	public void SetOnlineCheck(bool onlineCheck)
	{
		mOnlineCheck = onlineCheck;
	}

	public virtual T SetQueryAssistCountFunc(Func<long, int> queryAssistCountFunc)
	{
		mQueryAssistCountFunc = queryAssistCountFunc;
		return (T)this;
	}

	public static int ComparerResult(long x, long y, ComparerOrderType type)
	{
		switch (type)
		{
		case ComparerOrderType.Asc:
			return ConvertLong2IntForSort(unchecked(x - y));
		case ComparerOrderType.Desc:
			return ConvertLong2IntForSort(unchecked(y - x));
		default:
		{
			UnityEngine.Debug.LogError("unsupport comparerOrderType : " + type);
			return 0;
		}
		}
	}

	public int GetProfessionLimitVal(TeamInvitePlayerInfoData player)
	{
		if (!mIsJoinStrikeTarget)
		{
			return 0;
		}
		if (mOnlineCheck && !player.Online)
		{
			return 0;
		}
		if (!services.CanJointTogether(player.Profession, services.MainPlayerProfession))
		{
			return 0;
		}
		return 1;
	}

	protected virtual long GetScore(TeamInvitePlayerInfoData player, TeamInviteComparerType comparerType)
	{
		switch (comparerType)
		{
		case TeamInviteComparerType.NpcCollected:
		{
			IProfessionInfoNpcTarget npcTargetData = player.TargetData as IProfessionInfoNpcTarget;
			if (npcTargetData == null)
			{
				return 0L;
			}
			if (services.IsNpcPinLimited)
			{
				return 0L;
			}
			TeamInviteNpcRecord byNpcClassId = services.GetNpc(npcTargetData.NpcClassId);
			return (byNpcClassId != null && byNpcClassId.IsCollected) ? 1 : 0;
		}
		case TeamInviteComparerType.NpcCollectedTime:
		{
			IProfessionInfoNpcTarget npcTargetData3 = player.TargetData as IProfessionInfoNpcTarget;
			if (npcTargetData3 == null)
			{
				return 0L;
			}
			if (services.IsNpcPinLimited)
			{
				return 0L;
			}
			TeamInviteNpcRecord playerNpcDataWrap = services.GetNpc(npcTargetData3.NpcClassId);
			if (playerNpcDataWrap == null || !playerNpcDataWrap.IsCollected)
			{
				return 0L;
			}
			return playerNpcDataWrap.CollectedTime.Ticks;
		}
		case TeamInviteComparerType.Online:
			return player.Online ? 1 : 0;
		case TeamInviteComparerType.ProfessionLimit:
			return GetProfessionLimitVal(player);
		case TeamInviteComparerType.AssistCount:
			return mQueryAssistCountFunc(player.PlayerId);
		case TeamInviteComparerType.NpcAssistLimit:
		{
			IProfessionInfoNpcTarget npcTargetData2 = player.TargetData as IProfessionInfoNpcTarget;
			return (!(npcTargetData2 != null) || !services.IsNpcAssistLimit(npcTargetData2.NpcClassId)) ? 1 : 0;
		}
		case TeamInviteComparerType.FriendShip:
		{
			var friendshipData = services.ToFriendshipData(player.TargetData);
			return unchecked((friendshipData?.ToTarget ?? 0) + (friendshipData?.ToPlayer ?? 0));
		}
		case TeamInviteComparerType.CombatRating:
			return player.CombatRating;
		case TeamInviteComparerType.ActivityBuff:
			services.RefreshFestivalActivityData();
			return (services.HasFestivalBossNpc(mAcitivityId, (player.TargetData as IProfessionInfoNpcTarget).NpcClassId)) ? 1 : 0;
		case TeamInviteComparerType.ElementPropType:
		{
			player.PropTypes.TryGetValue(mPropType, out var value);
			return value;
		}
		case TeamInviteComparerType.Companion:
			return (mCompanionId > 0 && player.PlayerId == mCompanionId) ? 1 : 0;
		default:
			return 0L;
		}
	}

	protected int Comparer(long id1, long id2, List<(TeamInviteComparerType comparerType, ComparerOrderType orderType)> priorityList)
	{
		TeamInvitePlayerInfoData player = mQueryFunc(id1);
		TeamInvitePlayerInfoData player2 = mQueryFunc(id2);
		foreach (var priority in priorityList)
		{
			long score = GetScore(player, priority.comparerType);
			long score2 = GetScore(player2, priority.comparerType);
			int num = ComparerResult(score, score2, priority.orderType);
			if (num != 0)
			{
				return num;
			}
		}
		return 0;
	}

	public static int ConvertLong2IntForSort(long longValue)
	{
		if (longValue == 0L)
		{
			return 0;
		}
		if (longValue <= 0)
		{
			return -1;
		}
		return 1;
	}
}

}
