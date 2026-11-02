// Ported original controller; see partner-dialogue-playback-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.Partners;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.StoryBubbles;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System.Collections.Generic;
using UnityEngine;

public class NpcDialoguePanelRewardItemContent : UIBase
{
    private IPartnerDialoguePlaybackServices services;
    public void Configure(IPartnerDialoguePlaybackServices value) => services = value ?? throw new System.ArgumentNullException(nameof(value));

	[SerializeField]
	private Transform mRewardSlot;

	private List<ItemIcon> mItemIcons = new List<ItemIcon>();

	private Dictionary<int, long> mRewardItems = new Dictionary<int, long>();


	protected override void OnDestroy()
	{
		mItemIcons = null;
		mRewardItems = null;
		base.OnDestroy();
	}

	public void Init(Dictionary<int, long> rewardItems)
	{
		InitWrapper(() =>
		{
			mRewardItems = rewardItems;
		});
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		RefreshReward();
	}

	protected override void OnDisableAndInited()
	{
		RecycleItems();
		base.OnDisableAndInited();
	}

	private void RefreshReward()
	{
		if (mRewardItems == null || mRewardItems.Count <= 0)
		{
			return;
		}
		foreach (KeyValuePair<int, long> mRewardItem in mRewardItems)
		{
			ItemIcon itemIcon = services.GetRewardIcon(mRewardItem.Key, mRewardSlot);
			if (itemIcon != null)
			{
				itemIcon.SetClickPopupInfoDialog().SetCountTextScale(ItemIcon.sTextScale)
					.SetCount(mRewardItem.Value);
				itemIcon.SetAttachment(ItemIconAttachmentType.GotGrayTip, show: true);
				mItemIcons.Add(itemIcon);
			}
		}
	}

	private void RecycleItems()
	{
		for (int i = 0; i < mItemIcons.Count; i++)
		{
			services.RecycleRewardIcon(mItemIcons[i]);
		}
		mItemIcons.Clear();
	}
}

}
