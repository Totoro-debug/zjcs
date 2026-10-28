namespace Zjcs.Official.TeamUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using UnityEngine;

public abstract class TeamInviteItemBase : MonoBehaviour
{
	protected ITeamInviteUiServices services;
	public virtual void Configure(ITeamInviteUiServices value)
	{ services = value; mPlayerInfoView.Configure(value); }

	[SerializeField]
	protected TeamInvitePlayerInfoView mPlayerInfoView;

	[SerializeField]
	private Component mPlayerCompanionTag;

	protected IProfessionInfoTarget mTargetData;

	protected ITeamInviteFriendshipData mFriendshipData;

	protected bool mPlayerIsOffline;

	protected TeamInvitePlayerInfoData mPlayerInfo;

	protected TeamInviteSubPanelBase.TeamInviteSortType mSort;

	public TeamInvitePlayerInfoData PlayerInfo => mPlayerInfo;

	public virtual void Init(TeamInvitePlayerInfoData playerInfo, TeamInviteSubPanelBase.TeamInviteSortType sort)
	{
		mPlayerInfo = playerInfo;
		mTargetData = playerInfo.TargetData;
		mFriendshipData = services.GetFriendshipData(mTargetData);
		mSort = sort;
		if ((Object)(object)mPlayerCompanionTag != (Object)null)
		{
			services.SetCompanionTag(mPlayerCompanionTag, mPlayerInfo.PlayerId);
		}
		RefreshInfo();
	}

	protected virtual void OnEnable()
	{
	}

	protected virtual void OnDisable()
	{
		mTargetData = null;
		mFriendshipData = null;
		mPlayerIsOffline = false;
	}

	protected virtual void RefreshInfo()
	{
		TeamInvitePlayerInfoData teamInvitePlayerInfoData = mPlayerInfo;
		mPlayerInfoView.SetPlayerIcon(mTargetData);
		switch (mSort)
		{
		case TeamInviteSubPanelBase.TeamInviteSortType.CombatRating:
		case TeamInviteSubPanelBase.TeamInviteSortType.ActivityBuff:
			mPlayerInfoView.HideAllExcludeItem();
			return;
		case TeamInviteSubPanelBase.TeamInviteSortType.FriendShip:
			mPlayerInfoView.SetFriendShip(mFriendshipData);
			return;
		case TeamInviteSubPanelBase.TeamInviteSortType.LightAdd:
		case TeamInviteSubPanelBase.TeamInviteSortType.DarkAdd:
		case TeamInviteSubPanelBase.TeamInviteSortType.FireAdd:
		case TeamInviteSubPanelBase.TeamInviteSortType.WaterAdd:
		case TeamInviteSubPanelBase.TeamInviteSortType.WindAdd:
		case TeamInviteSubPanelBase.TeamInviteSortType.LightReduce:
		case TeamInviteSubPanelBase.TeamInviteSortType.DarkReduce:
		case TeamInviteSubPanelBase.TeamInviteSortType.FireReduce:
		case TeamInviteSubPanelBase.TeamInviteSortType.WaterReduce:
		case TeamInviteSubPanelBase.TeamInviteSortType.WindReduce:
		{
			PropType propType = mSort.ToPropType();
			teamInvitePlayerInfoData.PropTypes.TryGetValue(propType, out var value);
			mPlayerInfoView.SetProp(propType, value);
			return;
		}
		}
		UnityEngine.Debug.LogWarning("Unhandled original invitation sort value.");
	}
}

}
