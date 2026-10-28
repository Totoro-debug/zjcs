namespace Zjcs.Official.TeamUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

public class TeamInvitePanel : UIBase
{
	public enum InviteType
	{
		Player,
		Robot,
		Challenge,
		Room,
		Companion
	}

	private const int DemonLordMapClassId = 91;

	[SerializeField]
	protected GameObject mTabGroupGo;

	[SerializeField]
	protected WorldTabGroup mTabGroup;

	private ITeamInvitePlayer mInviteFriend;

	[SerializeField]
	protected WorldTabItem mFriendTab;

	[SerializeField]
	protected string mFriendTitleKey;

	private ITeamInvitePlayer mInviteLegionMember;

	[SerializeField]
	protected WorldTabItem mLegionTab;

	[SerializeField]
	protected string mLegionTitleKey;

	private ITeamInvitePlayer mInviteNpc;

	[SerializeField]
	protected WorldTabItem mNpcTab;

	[SerializeField]
	protected string mNpcTitleKey;

	private ITeamInvitePlayer mRecentBuddies;

	[SerializeField]
	protected WorldTabItem mRecentBuddiesTab;

	[SerializeField]
	protected string mRecentBuddiesTitleKey;

	[SerializeField]
	protected PanelTitle mPanelTitle;

	public ChallengeInvitePlayerPanel ChallengeInvitePanel;

	public TeamInvitePlayerPanel PlayerPanel;

	public RoomInvitePlayerPanel RoomPanel;

	public TeamInviteRobotPanel RobotPanel;

	public CompanionInvitePlayerPanel CompanionPanel;

	public WorldStateSwitcher<InviteType> StateSwitcher;

	private TeamInviteSubPanelBase.TeamInviteSortType mDefaultSortType;

	private InviteType mInviteType;

	private TeamTargetData mTeamTarget;

	private CancellationTokenSource mCancellationTokenSource;

	private ITeamInvitePanelServices services;
	public void Configure(ITeamInvitePanelServices value)
	{
		services = value;
		ChallengeInvitePanel.Configure(value); PlayerPanel.Configure(value);
		RoomPanel.Configure(value); RobotPanel.Configure(value); CompanionPanel?.Configure(value);
	}
	protected void Awake()
	{
		mInviteFriend = services.CreateFriendsProvider();
		mInviteNpc = services.CreateNpcProvider();
		mRecentBuddies = services.CreateRecentBuddiesProvider();
	}

	public void Init(InviteType contentType, TeamTargetData teamTarget, TeamInviteSubPanelBase.TeamInviteSortType defaultSortType, List<long> excludePlayers = null)
	{
		InitWrapper(() =>
		{
			mInviteType = contentType;
			mDefaultSortType = defaultSortType;
			mTeamTarget = teamTarget;
			ChallengeInvitePanel.SetTeamTarget(teamTarget).SetExcludePlayers(excludePlayers);
			PlayerPanel.SetTeamTarget(teamTarget).SetExcludePlayers(excludePlayers);
			RobotPanel.SetTeamTarget(teamTarget).SetExcludePlayers(excludePlayers);
			RobotPanel.SetShowCollectionImg(isShow: true);
			RoomPanel.SetTeamTarget(teamTarget).SetExcludePlayers(excludePlayers);
			if ((Object)(object)CompanionPanel != (Object)null)
			{
				CompanionPanel.SetTeamTarget(teamTarget).SetExcludePlayers(excludePlayers);
			}
			StateSwitcher.SetState(contentType);
		});
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		UpdateTabGroupShow();
		if (services.MainPlayerAvailable)
		{
			services.LegionUpdated += new Action<long, long>(OnPlayerLegionUpdate);
		}
		((UnityEvent<bool>)mFriendTab.OnValueChanged).AddListener((UnityAction<bool>)OnFriendTabValueChanged);
		((UnityEvent<bool>)mLegionTab.OnValueChanged).AddListener((UnityAction<bool>)OnLegionTabValueChanged);
		((UnityEvent<bool>)mNpcTab.OnValueChanged).AddListener((UnityAction<bool>)OnNpcTabValueChanged);
		((UnityEvent<bool>)mRecentBuddiesTab.OnValueChanged).AddListener((UnityAction<bool>)OnRecentBuddyTabValueChanged);
		mTabGroup.ResetTabItem(needNotify: true, mFriendTab);
	}

	protected override void OnDisableAndInited()
	{
		if (services.MainPlayerAvailable)
		{
			services.LegionUpdated -= new Action<long, long>(OnPlayerLegionUpdate);
		}
		((UnityEvent<bool>)mFriendTab.OnValueChanged).RemoveListener((UnityAction<bool>)OnFriendTabValueChanged);
		((UnityEvent<bool>)mLegionTab.OnValueChanged).RemoveListener((UnityAction<bool>)OnLegionTabValueChanged);
		((UnityEvent<bool>)mNpcTab.OnValueChanged).RemoveListener((UnityAction<bool>)OnNpcTabValueChanged);
		((UnityEvent<bool>)mRecentBuddiesTab.OnValueChanged).RemoveListener((UnityAction<bool>)OnRecentBuddyTabValueChanged);
		TryCancelAsyncOperation();
		base.OnDisableAndInited();
	}

	public TeamInviteSubPanel GetCurrentSubPanel()
	{
		return StateSwitcher.State switch
		{
			InviteType.Player => (TeamInviteSubPanel)PlayerPanel,
			InviteType.Robot => RobotPanel,
			InviteType.Challenge => ChallengeInvitePanel,
			InviteType.Room => RoomPanel,
			InviteType.Companion => CompanionPanel,
			_ => PlayerPanel,
		};
	}

	private void UpdateTabGroupShow()
	{
		UpdateLegionTab();
		UpdateNpcTab();
		((Component)mRecentBuddiesTab).gameObject.SetActive(CanShowRecentBuddiesTab());
	}

	private bool CanShowRecentBuddiesTab()
	{
		if (mInviteType == InviteType.Robot)
		{
			return false;
		}
		return true;
	}

	private void UpdateLegionTab()
	{
		bool mainPlayer = services.MainPlayerAvailable;
		if (!mainPlayer)
		{
			((Component)mLegionTab).gameObject.SetActive(false);
			return;
		}
		bool active = services.LegionId > 0;
		((Component)mLegionTab).gameObject.SetActive(active);
	}

	private void UpdateNpcTab()
	{
		((Component)mNpcTab).gameObject.SetActive(CanShowNpcTab());
	}

	private bool CanShowNpcTab()
	{
		bool mainPlayer = services.MainPlayerAvailable;
		if (!mainPlayer)
		{
			return false;
		}
		if (IsNpcFunctionLimited())
		{
			return false;
		}
		if (!IsTeamLeaderOrSolo())
		{
			return false;
		}
		if (IsChallengeOrRoomMode())
		{
			return false;
		}
		if (IsInDemonLordMap())
		{
			return false;
		}
		return mTeamTarget.CanInviteNpc();
	}

	private bool IsNpcFunctionLimited()
	{
		return services.NpcFightAssistLimited;
	}

	private bool IsTeamLeaderOrSolo()
	{
		if (services.TeamLeaderId.HasValue)
		{
			return services.TeamLeaderId.Value == services.MainPlayerId;
		}
		return true;
	}

	private bool IsChallengeOrRoomMode()
	{
		if (StateSwitcher.State != InviteType.Challenge)
		{
			return StateSwitcher.State == InviteType.Room;
		}
		return true;
	}

	private bool IsInDemonLordMap()
	{
		if (!services.MapRootAvailable)
		{
			return false;
		}
		int? myContainer = services.MapClassId;
		if (myContainer.HasValue)
		{
			return myContainer.Value == 91;
		}
		return false;
	}

	private void OnPlayerLegionUpdate(long preLegionId, long legionId)
	{
		UpdateTabGroupShow();
	}

	private void TryResetCancellation()
	{
		if (mCancellationTokenSource != null)
		{
			mCancellationTokenSource.Cancel();
			mCancellationTokenSource.Dispose();
		}
		mCancellationTokenSource = new CancellationTokenSource();
	}

	private void OnFriendTabValueChanged(bool isOn)
	{
		if (isOn)
		{
			TryResetCancellation();
			mPanelTitle.SetKey(mFriendTitleKey);
			GetCurrentSubPanel().RefreshPanel(mInviteFriend, mDefaultSortType, mCancellationTokenSource.Token);
		}
	}

	private void OnLegionTabValueChanged(bool isOn)
	{
		if (isOn)
		{
			TryResetCancellation();
			mPanelTitle.SetKey(mLegionTitleKey);
			GetCurrentSubPanel().RefreshPanel(mInviteLegionMember ?? (mInviteLegionMember = services.CreateLegionProvider()), mDefaultSortType, mCancellationTokenSource.Token);
		}
	}

	private void OnNpcTabValueChanged(bool isOn)
	{
		if (isOn)
		{
			TryResetCancellation();
			mPanelTitle.SetKey(mNpcTitleKey);
			GetCurrentSubPanel().RefreshPanel(mInviteNpc, mDefaultSortType, mCancellationTokenSource.Token);
		}
	}

	private void OnRecentBuddyTabValueChanged(bool isOn)
	{
		if (isOn)
		{
			TryResetCancellation();
			TeamInviteSubPanel currentSubPanel = GetCurrentSubPanel();
			mPanelTitle.SetKey(mRecentBuddiesTitleKey);
			currentSubPanel.RefreshPanel(mRecentBuddies, mDefaultSortType, mCancellationTokenSource.Token);
		}
	}

	private void TryCancelAsyncOperation()
	{
		if (mCancellationTokenSource != null)
		{
			mCancellationTokenSource.Cancel();
			mCancellationTokenSource.Dispose();
			mCancellationTokenSource = null;
		}
	}
}

}
