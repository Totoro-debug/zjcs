// Selected official dialog; see partner-friendship-dialog-ui-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using NpcInfo = Zjcs.Official.PartnersUI.PartnerFriendshipDialogNpc;
using NpcFriendshipLevelInfo = Zjcs.Official.PartnersUI.PartnerFriendshipDialogLevel;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NpcFriendshipDialog : UIBase, IPartnerFriendshipDialog
{
	[SerializeField]
	private Transform mGiftSlot;

	[SerializeField]
	private Transform mPropSlot;

	[SerializeField]
	private MultiLanguageText mAssistConditionText;

	[SerializeField]
	private MultiLanguageText mPreferenceText;

	[SerializeField]
	private GameObject mAssistTipGo;

	[SerializeField]
	private GameObject mPropItemPrefab;

	[SerializeField]
	private List<MultiLanguageText> mRuleTitleTextList;

	[SerializeField]
	private GameObject mGiftRuleGo;

	[SerializeField]
	private WorldStateSwitcher<bool> mTravelStateSwitcher;

	[SerializeField]
	private MultiLanguageText mDescText1;

	[SerializeField]
	private MultiLanguageText mDescText2;

	private NpcInfo mNpcInfo;

	private List<ItemIcon> mItemIcons = new List<ItemIcon>();

	private GameObjectPool<NpcFriendshipPreviewPropItem> mPool;

	private List<NpcFriendshipPreviewPropItem> mPropItems = new List<NpcFriendshipPreviewPropItem>();

    private IPartnerFriendshipDialogUiServices services;
    public void Configure(IPartnerFriendshipDialogUiServices value)
    {
        services = value ?? throw new System.ArgumentNullException(nameof(value));
    }

	private void Awake()
	{
		mPool = new GameObjectPool<NpcFriendshipPreviewPropItem>(((Component)this).transform, mPropItemPrefab);
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Refresh();
		CultureTextArg cultureTextArg = new CultureTextArg(mNpcInfo.IsStarGodNpc ? "ui_rule_npc_star_god_title" : "ui_rule_npc_normal_title");
		mDescText1.Format(cultureTextArg);
		mDescText2.Format(cultureTextArg, cultureTextArg);
	}

	protected override void OnDisableAndInited()
	{
		foreach (NpcFriendshipPreviewPropItem mPropItem in mPropItems)
		{
			mPool.Return(mPropItem);
		}
		mPropItems.Clear();
		RecycleItemIcons();
		base.OnDisableAndInited();
	}

	public void Init(NpcInfo npcInfo)
	{
		InitWrapper(() =>
		{
			mNpcInfo = npcInfo;
		});
	}

	private void Refresh()
	{
		RecycleItemIcons();
		List<int> list = mNpcInfo.GiftPreferenceDict.Keys.ToList();
		bool flag = list.Count > 0;
		mGiftRuleGo.SetActive(flag);
		if (flag)
		{
			list.Sort((int a, int b) => services.CompareRewardItems(a, b));
			foreach (int item in list)
			{
				ItemIcon icon = services.BorrowGiftIcon(item, mGiftSlot);
				icon.SetClickPopupInfoDialog();
				mItemIcons.Add(icon);
			}
		}
		int num = 0;
		foreach (MultiLanguageText mRuleTitleText in mRuleTitleTextList)
		{
			if (((Component)mRuleTitleText).gameObject.activeInHierarchy)
			{
				mRuleTitleText.Format(++num);
			}
		}
		bool flag2 = mNpcInfo.AssistFriendshipLevel.HasValue;
		mAssistTipGo.SetActive(flag2);
		if (flag2)
		{
			int friendshipMeetLevel = mNpcInfo.AssistFriendshipLevel.Value;
			mAssistConditionText.Format(mNpcInfo.Name, friendshipMeetLevel);
		}
		mPreferenceText.Format(mNpcInfo.Name);
		bool state = !services.IsTravelFunctionLimited;
		mTravelStateSwitcher.SetState(state);
		int num2 = 0;
		foreach (KeyValuePair<int, NpcFriendshipLevelInfo> item2 in services.GetFriendshipLevels(mNpcInfo.Properties.Id))
		{
			if (item2.Key != 1 && item2.Value.SubRankLimit <= services.SubRank + 1)
			{
				GetPropItem(num2).Init(mNpcInfo.Properties, item2.Key);
				num2++;
			}
		}
	}

	private NpcFriendshipPreviewPropItem GetPropItem(int index)
	{
		while (mPropItems.Count <= index)
		{
			NpcFriendshipPreviewPropItem npcFriendshipPreviewPropItem = mPool.Borrow(mPropSlot);
			npcFriendshipPreviewPropItem.Configure(services);
			bool bgVisible = mPropItems.Count % 2 > 0;
			npcFriendshipPreviewPropItem.SetBgVisible(bgVisible);
			mPropItems.Add(npcFriendshipPreviewPropItem);
		}
		return mPropItems[index];
	}

	private void RecycleItemIcons()
	{
		foreach (ItemIcon mItemIcon in mItemIcons)
		{
			services.RecycleGiftIcon(mItemIcon);
		}
		mItemIcons.Clear();
	}
}

}
