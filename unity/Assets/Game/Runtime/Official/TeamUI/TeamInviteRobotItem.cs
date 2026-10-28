namespace Zjcs.Official.TeamUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class TeamInviteRobotItem : TeamInviteItemBase
{
	public enum TeamAssistState
	{
		Assisted,
		CanInvite,
		FriendshipLimit,
		ProfessionLimit
	}

	public Toggle InvitedToggle;

	public GameObject LimitedGo;

	public WorldStateSwitcher<TeamAssistState> AssistStateSwitcher;

	[SerializeField]
	private MultiLanguageText mNpcFriendshipText;

	[SerializeField]
	private NpcFriendshipLevelShow mNpcFriendshipLevelShow;

	private int mAssistCount;

	private TeamInviteRobotRule mRobotInviteCfg;

	private Func<bool> mCanInviteMorePlayer;

	private Action<long, bool> mOnSelectPlayer;

	private TeamTargetData mTeamTarget;

	private void Awake()
	{
		((UnityEvent<bool>)(object)InvitedToggle.onValueChanged).AddListener((UnityAction<bool>)OnInvitedToggleValueChanged);
	}

	public override void Configure(ITeamInviteUiServices value) { base.Configure(value); mNpcFriendshipLevelShow.Configure(value); }

	public virtual void Init(TeamInvitePlayerInfoData playerInfo, TeamInviteSubPanelBase.TeamInviteSortType sort, bool playerIsSelected, TeamInviteRobotRule robotInviteCfg, Func<bool> canInviteMorePlayer, Action<long, bool> onSelectPlayer, TeamTargetData teamTarget)
	{
		mTeamTarget = teamTarget;
		mOnSelectPlayer = onSelectPlayer;
		mCanInviteMorePlayer = canInviteMorePlayer;
		mRobotInviteCfg = robotInviteCfg;
		base.Init(playerInfo, sort);
		mAssistCount = GetActorAssistCount();
		InvitedToggle.SetIsOnWithoutNotify((mTargetData.TargetId > 0) & playerIsSelected);
		((Component)mNpcFriendshipLevelShow).gameObject.SetActive(mTargetData.IsNpc);
		if (mTargetData.IsNpc)
		{
			ShowNpcFriendshipContent();
		}
		AssistStateSwitcher.SetState(GetTeamAssistState());
	}

	public void SetShowCollectionImg(bool isShow)
	{
		mPlayerInfoView.SetShowCollectionImg(isShow);
	}

	public Toggle GetInvitedToggle()
	{
		if (GetTeamAssistState() == TeamAssistState.CanInvite)
		{
			return InvitedToggle;
		}
		return null;
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		mAssistCount = 0;
	}

	private void OnInvitedToggleValueChanged(bool isOn)
	{
		if (isOn)
		{
			int num = CheckToggleCanSetOn();
			if (num != 0)
			{
				((ITeamInvitePanelServices)services).ShowError(num, false);
				InvitedToggle.SetIsOnWithoutNotify(false);
				return;
			}
		}
		RefreshAssistCount();
		mOnSelectPlayer(mTargetData.TargetId, isOn);
	}

	private int CheckToggleCanSetOn()
	{
		if (!mCanInviteMorePlayer())
		{
			return 6102;
		}
		if (!CheckActorCanAssist() || !CheckPlayTypeCanAssist())
		{
			return 1246;
		}
		return 0;
	}

	private void RefreshAssistCount()
	{
		if (mRobotInviteCfg.OnePlayerMaxInviteCount > 0)
		{
			AssistStateSwitcher.SetState(GetTeamAssistState());
		}
	}

	private int GetActorAssistCount()
	{
		return ((ITeamInvitePanelServices)services).GetActorAssistCount(mTargetData, mRobotInviteCfg.PlayType);
	}

	private bool CheckActorCanAssist()
	{
		return mTargetData.CheckActorCanAssist(mRobotInviteCfg.PlayType);
	}

	private bool CheckPlayTypeCanAssist()
	{
		return ((ITeamInvitePanelServices)services).CheckPlayTypeCanAssist(mTargetData, mRobotInviteCfg.PlayType);
	}

	private void ShowNpcFriendshipContent()
	{
		if (!(mTargetData is IProfessionInfoNpcTarget npcTargetData))
		{
			services.Error("TeamInviteRobotItem requires original NPC target data.");
			return;
		}
		TeamInviteNpcRecord byNpcClassId = services.GetNpc(npcTargetData.NpcClassId);
		if (byNpcClassId != null)
		{
			mNpcFriendshipLevelShow.Init(byNpcClassId.NpcClassId, byNpcClassId.FriendShip, byNpcClassId.FriendShipLevel, 0L);
		}
		else
		{
			mNpcFriendshipLevelShow.Init(npcTargetData.NpcClassId, 0L, 1, 0L);
		}
		if (services.IsNpcAssistLimit(npcTargetData.NpcClassId))
		{
			int requiredLevel = services.RequiredNpcAssistFriendshipLevel(npcTargetData.NpcClassId);
			mNpcFriendshipText.Format(byNpcClassId.FriendShipLevel, requiredLevel);
		}
	}

	private TeamAssistState GetTeamAssistState()
	{
		if (mTeamTarget.TargetCatalog == TeamTargetCatalog.JointStrike && !services.IsJointStrikeInvitedAvailable(mPlayerInfo))
		{
			return TeamAssistState.ProfessionLimit;
		}
		if (mRobotInviteCfg.OnePlayerMaxInviteCount > 0 && mAssistCount >= mRobotInviteCfg.OnePlayerMaxInviteCount)
		{
			return TeamAssistState.Assisted;
		}
		if (mTargetData is IProfessionInfoNpcTarget npcTargetData && services.IsNpcAssistLimit(npcTargetData.NpcClassId))
		{
			return TeamAssistState.FriendshipLimit;
		}
		return TeamAssistState.CanInvite;
	}
}

}
