namespace Zjcs.Official.TeamUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System;
using UnityEngine;

public class PlayerCompanionTag : MonoBehaviour
{
	[SerializeField]
	private GameObject mTagGo;

	private TeamInviteResourceOperationBag mDisposableBag = new TeamInviteResourceOperationBag();

	private long mCurrentPlayerId;

	private long mCompanionId;

	private ITeamInvitePanelServices services;
	public void Configure(ITeamInvitePanelServices value) { services = value; }

	protected void OnEnable()
	{

		mDisposableBag = new TeamInviteResourceOperationBag();
		services.CompanionBound += OnBound;
		mDisposableBag.Add(new TeamInviteListenerRemoval(() => services.CompanionBound -= OnBound));
		services.CompanionDataSynced += OnDataChanged;
		mDisposableBag.Add(new TeamInviteListenerRemoval(() => services.CompanionDataSynced -= OnDataChanged));
	}

	protected void OnDisable()
	{
		mCurrentPlayerId = 0L;
		mCompanionId = 0L;
		mDisposableBag.Dispose();
	}

	private void OnBound(long companionId)
	{
		mCompanionId = companionId;
		RefreshTag();
	}

	private void OnDataChanged()
	{
		mCompanionId = services.CompanionId;
		RefreshTag();
	}

	public void SetPlayerId(long playerId)
	{
		mCurrentPlayerId = playerId;
		mCompanionId = services.CompanionId;
		RefreshTag();
	}

	private void RefreshTag()
	{
		bool flag = mCompanionId > 0 && mCurrentPlayerId == mCompanionId;
		if (mTagGo.activeSelf != flag)
		{
			mTagGo.SetActive(flag);
		}
	}
}

}
