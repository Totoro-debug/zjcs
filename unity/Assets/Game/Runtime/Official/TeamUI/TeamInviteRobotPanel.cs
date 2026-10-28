namespace Zjcs.Official.TeamUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class TeamInviteRobotPanel : TeamInviteSubPanel
{
	public delegate void SelectInvitePlayerCallback(long targetId, TeamInvitePlayerInfoData infoData, bool isSelected);

	public MultiLanguageText SelectedPlayerCountLabel;

	public MultiLanguageText SelectedPlayerPropValueType;

	public MultiLanguageText PlayerRobotAllAssistCount;

	public Button ConfirmBtn;

	private PropType? mPropType;

	private long? meBasePropValue;

	private bool mShowNpcCollectionImg;

	private HashSet<long> mSelectedPlayerIds = new HashSet<long>();

	private object mPropTypeArg;

	private int mAllAssistCount;

	private int mMaxInviteCount;

	private Func<bool> mCanInviteMorePlayer;

	private Action<long, bool> mOnSelectPlayerAction;

	private SelectInvitePlayerCallback mOnSelectPlayer;

	public TeamInviteRobotRule RobotInviteCfg { get; private set; }

	private Func<bool> CanInviteMorePlayerFunc
	{
		get
		{
			if (mCanInviteMorePlayer == null)
			{
				mCanInviteMorePlayer = CanInviteMorePlayer;
			}
			return mCanInviteMorePlayer;
		}
	}

	private Action<long, bool> OnSelectPlayerAction
	{
		get
		{
			if (mOnSelectPlayerAction == null)
			{
				mOnSelectPlayerAction = OnSelectPlayer;
			}
			return mOnSelectPlayerAction;
		}
	}

	public TeamInviteRobotPanel SetShowCollectionImg(bool isShow)
	{
		mShowNpcCollectionImg = isShow;
		return this;
	}

	protected override void Awake()
	{

		base.Awake();
		((UnityEvent)ConfirmBtn.onClick).AddListener((UnityAction)(() =>
		{
			if (mSelectedPlayerIds.Count <= 0)
			{
				string text = services.Localize("INVITE_ASSIST_MATCH_NO_FRIEND");
				services.SystemMessage(text);
			}
			else
			{
				((ITeamInvitePanelServices)services).CloseInviteWindow(this);
			}
		}));
	}

	protected override void OnDisable()
	{
		mSelectedPlayerIds.Clear();
		base.OnDisable();
	}

	public void Init(int type, List<long> selectedPlayerIds = null, int maxInviteCount = -1)
	{
		RobotInviteCfg = ((ITeamInvitePanelServices)services).GetRobotInvite(type);
		mMaxInviteCount = ((maxInviteCount < 0) ? RobotInviteCfg.MaxPlayer : maxInviteCount);
		mSelectedPlayerIds.Clear();
		if (selectedPlayerIds != null)
		{
			mSelectedPlayerIds.UnionWith(selectedPlayerIds);
		}
		mAllAssistCount = ((ITeamInvitePanelServices)services).GetAllFriendAssistCount(type);
		RefreshOnSelectPlayer();
	}

	public TeamInviteRobotPanel SetSortByPropType(PropType sortPropType, long meBasePropValue)
	{
		mPropType = sortPropType;
		mPropTypeArg = new TeamInviteCultureTextArg(services.Localize, $"{typeof(PropType).Name}.{mPropType.Value}");
		this.meBasePropValue = meBasePropValue;
		if (mPropType != PropType.None)
		{
			TeamInviteSortType teamInviteSortType = sortPropType.ToSortType();
			SelectSort = teamInviteSortType;
			SetDefaultSortType(teamInviteSortType);
			SetSortType(teamInviteSortType);
		}
		return this;
	}

	private void SetSortType(TeamInviteSortType sortType)
	{
		if (PriorSort != sortType)
		{
			PriorSort = sortType;
			mUISortSelector.SetSelectedItemIndex(GetSortIndex());
		}
		if ((Object)(object)SelectedPlayerPropValueType != (Object)null && ((Component)SelectedPlayerPropValueType).gameObject.activeSelf)
		{
			((Component)SelectedPlayerPropValueType).gameObject.SetActive(false);
		}
	}

	public TeamInviteRobotPanel SetOnSelectPlayerCallback(SelectInvitePlayerCallback onSelectPlayer)
	{
		mOnSelectPlayer = onSelectPlayer;
		return this;
	}

	protected override bool PlayerFilter(TeamInvitePlayerInfoData infoData)
	{
		if (RobotInviteCfg.NeedTeam && services.TeamHasPlayer(infoData.PlayerId))
		{
			return true;
		}
		return base.PlayerFilter(infoData);
	}

	public bool CanInviteMorePlayer()
	{
		return mSelectedPlayerIds.Count < mMaxInviteCount;
	}

	public bool PlayerIsSelected(long playerId)
	{
		return mSelectedPlayerIds.Contains(playerId);
	}

	public void OnSelectPlayer(long playerId, bool isSelect)
	{
		if (isSelect)
		{
			mSelectedPlayerIds.Add(playerId);
		}
		else
		{
			mSelectedPlayerIds.Remove(playerId);
		}
		RefreshOnSelectPlayer();
		TeamInvitePlayerInfoData infoData = ((!isSelect) ? null : GetPlayerInfo(playerId));
		mOnSelectPlayer?.Invoke(playerId, infoData, isSelect);
	}

	protected override void OnFriendRemoveRelation(long playerId)
	{
		base.OnFriendRemoveRelation(playerId);
		if (mSelectedPlayerIds.Contains(playerId))
		{
			OnSelectPlayer(playerId, isSelect: false);
		}
	}

	private void RefreshOnSelectPlayer()
	{
		bool flag = mSelectedPlayerIds.Count > 0;
		((ITeamInvitePanelServices)services).SetUiGray(((Component)ConfirmBtn).gameObject, !flag);
		SelectedPlayerCountLabel.Format(mSelectedPlayerIds.Count, mMaxInviteCount);
		if ((Object)(object)PlayerRobotAllAssistCount != (Object)null)
		{
			((Component)PlayerRobotAllAssistCount).gameObject.SetActive(RobotInviteCfg.MaxInviteCount > 0);
			PlayerRobotAllAssistCount.Format(mAllAssistCount + mSelectedPlayerIds.Count, RobotInviteCfg.MaxInviteCount);
		}
		bool hasValue = meBasePropValue.HasValue;
		if ((Object)(object)SelectedPlayerPropValueType != (Object)null)
		{
			((Component)SelectedPlayerPropValueType).gameObject.SetActive(hasValue);
			if (hasValue)
			{
				long totalPropValue = GetTotalPropValue();
				SelectedPlayerPropValueType.Format(mPropTypeArg, totalPropValue);
			}
		}
	}

	private long GetTotalPropValue()
	{
		long num = meBasePropValue.Value;
		foreach (long mSelectedPlayerId in mSelectedPlayerIds)
		{
			TeamInvitePlayerInfoData playerInfo = GetPlayerInfo(mSelectedPlayerId);
			long value = 0L;
			playerInfo?.PropTypes.TryGetValue(mPropType.Value, out value);
			num += value;
		}
		return num;
	}

	protected override void SortPlayer(List<long> playerList, TeamInviteSortType sortType)
	{
		bool isJoinStrikeTarget = mGetTeamTarget().TargetCatalog == TeamTargetCatalog.JointStrike;
		long companionId = GetCompanionId();
		Func<long, long, int> func = null;
		switch (sortType)
		{
		case TeamInviteSortType.FriendShip:
			func = new TeamInviteRobotComparer(GetPlayerInfo, GetAssistCount, isJoinStrikeTarget, services).SetCompanionId(companionId).RobotFriendComparer;
			break;
		case TeamInviteSortType.CombatRating:
			func = new TeamInviteRobotComparer(GetPlayerInfo, GetAssistCount, isJoinStrikeTarget, services).SetCompanionId(companionId).RobotCombatRatingComparer;
			break;
		case TeamInviteSortType.ActivityBuff:
		{
			int? activityData = services.FestivalActivityId;
			func = ((activityData == null) ? new Func<long, long, int>(new TeamInviteRobotComparer(GetPlayerInfo, GetAssistCount, isJoinStrikeTarget, services).SetCompanionId(companionId).RobotNoDataActivityBuffComparer) : new Func<long, long, int>(new TeamInviteRobotComparer(GetPlayerInfo, GetAssistCount, isJoinStrikeTarget, services).SetCompanionId(companionId).SetActivityId(activityData.Value).RobotActivityBuffComparer));
			break;
		}
		default:
		{
			PropType propType = sortType.ToPropType();
			func = new TeamInviteRobotComparer(GetPlayerInfo, GetAssistCount, isJoinStrikeTarget, services, propType).SetCompanionId(companionId).RobotElementPropTypeComparer;
			break;
		}
		}
		playerList.Sort(func.Invoke);
	}

	public int GetAssistCount(long id)
	{
		int assistCount = mCurrentPlayer.GetAssistCount(RobotInviteCfg.PlayType, id);
		return RobotInviteCfg.OnePlayerMaxInviteCount - assistCount;
	}

	public override void ProvideData(Transform transform, int index)
	{
		if (index >= 0 && index < PlayerList.Count)
		{
			TeamInvitePlayerInfoData playerInfo = GetPlayerInfo(PlayerList[index]);
			TeamInviteRobotItem component = ((Component)transform).GetComponent<TeamInviteRobotItem>();
			TeamInviteSortType curSortType = GetCurSortType();
			bool playerIsSelected = PlayerIsSelected(playerInfo.PlayerId);
			component.Init(playerInfo, curSortType, playerIsSelected, RobotInviteCfg, CanInviteMorePlayerFunc, OnSelectPlayerAction, TeamTarget);
			component.SetShowCollectionImg(mShowNpcCollectionImg);
		}
	}
}

}
