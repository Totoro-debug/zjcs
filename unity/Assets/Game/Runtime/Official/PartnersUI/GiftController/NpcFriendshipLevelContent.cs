// Selected official controller; see partner-gift-controller-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using PropType = Zjcs.Official.ProfessionUI.ProfessionPropertyType;
using NpcInfo = Zjcs.Official.PartnersUI.PartnerGiftNpcInfo;
using PlayerNpcDataWrap = Zjcs.Official.PartnersUI.IPartnerGiftNpcData;
using PlayerItemDataWrap = Zjcs.Official.ProfessionUI.IProfessionItemData;
using NpcFriendshipLevelInfo = Zjcs.Official.PartnersUI.PartnerGiftLevelInfo;
using NpcFriendshipLevelInfoParser = Zjcs.Official.PartnersUI.IPartnerGiftLevels;
using NpcGiftInfoParser = Zjcs.Official.PartnersUI.IPartnerGiftExperience;
using NpcFriendshipTagInfo = Zjcs.Official.PartnersUI.PartnerGiftTagInfo;
using NpcGiftInfo = Zjcs.Official.PartnersUI.PartnerGiftInfo;
using Player = Zjcs.Official.PartnersUI.IPartnerGiftPlayer;
using NpcMeetType = System.Int32;
using AwardSourceData = System.Object;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class NpcFriendshipLevelContent : UIBase
{
    private IPartnerGiftUiServices services;
    public void Configure(IPartnerGiftUiServices value)
    { services = value ?? throw new ArgumentNullException(nameof(value)); }
	[SerializeField]
	private MultiLanguageText mLevelText;

	[SerializeField]
	private MultiLanguageText mPreviewLevelText;

	[SerializeField]
	private MultiLanguageText mCurrentExpDiffText;

	[SerializeField]
	private MultiLanguageText mNextExpDiffText;

	[SerializeField]
	private MultiLanguageText mPreviewExpText;

	[SerializeField]
	private Slider mCurrentSlider;

	[SerializeField]
	private Slider mPreviewSlider;

	[SerializeField]
	private Button mTipBtn;

	[SerializeField]
	private WorldStateSwitcher<bool> mExpStateSwitcher;

	[SerializeField]
	private WorldStateSwitcher<bool> mPreviewStateSwitcher;

	[SerializeField]
	private WorldStateSwitcher<bool> mLevelStateSwitcher;

	private NpcInfo mNpcInfo;

	private PlayerNpcDataWrap mNpcData;

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		services.Player.OnNpcFriendshipLevelUpdate += MainPlayer_OnNpcFriendshipLevelUpdate;
		services.Player.OnNpcFriendshipUpdate += MainPlayer_OnNpcFriendshipUpdate;
		if ((Object)(object)mTipBtn != (Object)null)
		{
			((UnityEvent)mTipBtn.onClick).AddListener((UnityAction)OnClickTipBtn);
		}
		Refresh();
	}

	protected override void OnDisableAndInited()
	{
		if (services.Player != null)
		{
			services.Player.OnNpcFriendshipLevelUpdate -= MainPlayer_OnNpcFriendshipLevelUpdate;
			services.Player.OnNpcFriendshipUpdate -= MainPlayer_OnNpcFriendshipUpdate;
		}
		if ((Object)(object)mTipBtn != (Object)null)
		{
			((UnityEvent)mTipBtn.onClick).RemoveListener((UnityAction)OnClickTipBtn);
		}
		base.OnDisableAndInited();
	}

	private void MainPlayer_OnNpcFriendshipLevelUpdate(int npcClassId, int fromLevel, int toLevel)
	{
		if (mNpcData.NpcClassId == npcClassId)
		{
			Refresh();
			ResetExpPreview();
		}
	}

	private void MainPlayer_OnNpcFriendshipUpdate(int npcClassId)
	{
		if (mNpcData.NpcClassId == npcClassId)
		{
			Refresh();
			ResetExpPreview();
		}
	}

	private void OnClickTipBtn()
	{
        services.PopupFriendship(this, mNpcInfo.IsStarGodNpc, dialog =>
        {
            if (dialog != null) dialog.Init(mNpcInfo);
        });
	}

	public void Init(PlayerNpcDataWrap npcDataWrap, NpcInfo npcInfo)
	{
		InitWrapper(() =>
		{
			mNpcData = npcDataWrap;
			mNpcInfo = npcInfo;
		});
		Refresh();
		ResetExpPreview();
	}

	private void Refresh()
	{
		mNpcData = services.Player.GetNpcByClassId(mNpcInfo.Id);
		NpcFriendshipLevelInfoParser npcFriendshipLevelInfoParser = services.Levels;
		int friendShipLevel = mNpcData.FriendShipLevel;
		mLevelText.Format(friendShipLevel);
		if (npcFriendshipLevelInfoParser.GetMaxLevel(mNpcData.NpcClassId) == friendShipLevel)
		{
			mCurrentSlider.maxValue = mNpcData.FriendShip;
			mCurrentSlider.value = mNpcData.FriendShip;
			mExpStateSwitcher.SetState(state: false);
			return;
		}
		NpcFriendshipLevelInfo npcFriendshipLevelInfo = npcFriendshipLevelInfoParser.Get(mNpcData.NpcClassId, friendShipLevel);
		NpcFriendshipLevelInfo npcFriendshipLevelInfo2 = npcFriendshipLevelInfoParser.Get(mNpcData.NpcClassId, friendShipLevel + 1);
		long num = mNpcData.FriendShip - npcFriendshipLevelInfo.FriendshipExp;
		long num2 = npcFriendshipLevelInfo2.FriendshipExp - npcFriendshipLevelInfo.FriendshipExp;
		mCurrentExpDiffText.Format(services.Compact(num));
		mNextExpDiffText.Format(services.Compact(num2));
		mCurrentSlider.maxValue = num2;
		mCurrentSlider.value = num;
		mPreviewSlider.maxValue = num2;
		mExpStateSwitcher.SetState(state: true);
	}

	public void ShowExpPreview(Dictionary<int, long> giftDict)
	{
		int allExpByDict = services.Gifts.GetAllExpByDict(giftDict, mNpcInfo.GiftPreferenceDict);
		ShowExpPreview(allExpByDict);
	}

	public void ShowExpPreview(int addFriendship)
	{
		if (addFriendship == 0)
		{
			ResetExpPreview();
		}
		else if (mNpcData != null)
		{
			NpcFriendshipLevelInfoParser npcFriendshipLevelInfoParser = services.Levels;
			if (npcFriendshipLevelInfoParser.GetMaxLevel(mNpcData.NpcClassId) != mNpcData.FriendShipLevel)
			{
				int friendshipLevelByExp = npcFriendshipLevelInfoParser.GetFriendshipLevelByExp(mNpcData.NpcClassId, mNpcData.FriendShip + addFriendship, services.Player.SubRank);
				mPreviewLevelText.Format(friendshipLevelByExp);
				mLevelStateSwitcher.SetState(mNpcData.FriendShipLevel != friendshipLevelByExp);
				mPreviewSlider.value = Math.Min(mCurrentSlider.value + (float)addFriendship, mCurrentSlider.maxValue);
				mPreviewExpText.Format(services.Compact(addFriendship));
				mPreviewStateSwitcher.SetState(state: true);
			}
		}
	}

	public void ResetExpPreview()
	{
		mPreviewSlider.value = 0f;
		mPreviewStateSwitcher.SetState(state: false);
		mLevelStateSwitcher.SetState(state: false);
	}
}

}
