namespace Zjcs.Official.TeamUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class FriendshipInfo : UIBase
{
	protected ITeamInviteUiServices services;
	public virtual void Configure(ITeamInviteUiServices value) { services = value; }

	public List<Image> FriendshipToTargetImage;

	public List<Image> FriendshipToPlayerImage;

	public MultiLanguageText RelationshipText;

	public Button Btn;

	public bool mDefaultShowNumTxt;

	private ITeamInviteFriendshipData mFriendshipData;

	public void Init(ITeamInviteFriendshipData data)
	{
		InitWrapper(() =>
		{
			mFriendshipData = data;
		});
	}

	public void Init(long targetPlayerId)
	{
		InitWrapper(() =>
		{
			mFriendshipData = services.GetPlayerFriendshipData(targetPlayerId);
		});
	}

	private void Awake()
	{

		Btn.onClick.AddListener(ToggleText);
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		((Component)RelationshipText).gameObject.SetActive(mDefaultShowNumTxt);
		services.FriendshipUpdated += RelationshipData_OnFriendshipUpdate;
		RefreshFriendshipUI();
	}

	protected override void OnDisableAndInited()
	{
		base.OnDisableAndInited();
		if (services.MainPlayerAvailable)
		{
			services.FriendshipUpdated -= RelationshipData_OnFriendshipUpdate;
		}
	}

	private void RelationshipData_OnFriendshipUpdate(long targetId, int friendshipToTarget, int friendshipToPlayer)
	{
		if (services.MainPlayerAvailable)
		{
			RefreshFriendshipUI();
		}
	}

	private void RefreshFriendshipUI()
	{
		if (mFriendshipData != null)
		{
			long friendshipToTarget = mFriendshipData.FriendshipToTarget;
			long friendshipToPlayer = mFriendshipData.FriendshipToPlayer;
			SetFriendshipImage(FriendshipToTargetImage, friendshipToTarget);
			SetFriendshipImage(FriendshipToPlayerImage, friendshipToPlayer);
			RelationshipText.SetKey("{0}", services.Icons.PlayerInfo.CompactNumber(unchecked(friendshipToTarget + friendshipToPlayer)));
		}
	}

	private void SetFriendshipImage(List<Image> images, long friendshipExp)
	{
		int num = services.RelationshipLevelByExp(4, friendshipExp);
		for (int i = 0; i < images.Count; i++)
		{
			if (i <= num - 1)
			{
				images[i].fillAmount = 1f;
			}
			else if (i == num)
			{
				long exp = services.RelationshipExp(4, num);
				long levelUpExp = services.RelationshipLevelUpExp(4, num);
				images[i].fillAmount = (float)(friendshipExp - exp) * 1f / (float)levelUpExp;
			}
			else
			{
				images[i].fillAmount = 0f;
			}
		}
	}

	public void ToggleText()
	{
		((Component)RelationshipText).gameObject.SetActive(!((Component)RelationshipText).gameObject.activeSelf);
	}
}

}
