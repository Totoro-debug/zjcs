// Selected 903_156948 client controllers; explicit player/configuration/presentation ports.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Zjcs.Official.HomeUI;
using Zjcs.Official.HomeFurniture;
using Zjcs.Official.HomeTipDots;
using Zjcs.Official.HomeHangUpWindowsUI;
using Zjcs.Official.HomeAwardUI;
using Zjcs.Official.HomeSceneUiControls;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.BattleResultUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.World.Map13MiniMap;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using HangUpInfo = Zjcs.Official.HomeCartWindowsUI.HomeCartHangUpInfo;
using Player = Zjcs.Official.HomeCartWindowsUI.IHomeCartWindowPlayer;
using ItemAwardIcon = Zjcs.Official.HomeAwardUI.HomeAwardIcon;
using ItemInfo = Zjcs.Official.HomeAwardUI.ItemInfo;
using ItemType = Zjcs.Official.HomeAwardUI.ItemType;
using AwardData = Zjcs.Official.HomeAwardUI.AwardData;
namespace Zjcs.Official.HomeCartWindowsUI
{
public class FurnitureCartRuleDialog : UIBase, IRewardPreviewDataProvider
{
    public IHomeCartWindowServices Services { protected get; set; }
	[SerializeField]
	private RuleDialog mRuleDialog;

	[SerializeField]
	private int mEquipHangUpId;

	[SerializeField]
	private RewardPreviewDialog mPreviewDialog;

	private List<HomeCartEquipPreviewItem> mItemProbabilityAmount;

	public int Count => mItemProbabilityAmount.Count;

	public void Init()
	{
		mRuleDialog.Rules = Services.RuleTips;
		mRuleDialog.SetRule("CartRule");
		InitItem();
		mPreviewDialog.Init(this);
	}

	private void InitItem()
	{
		mItemProbabilityAmount = new List<HomeCartEquipPreviewItem>(Services.GetEquipPreviewItems(mEquipHangUpId));
		for (int num = mItemProbabilityAmount.Count - 1; num >= 0; num--)
		{
			if (mItemProbabilityAmount[num].Probability == 0.0)
			{
				mItemProbabilityAmount.RemoveAt(num);
			}
		}
	}

	public void RefreshItem(int index, RewardPreviewDialogItem item)
	{
		ItemIcon itemIcon = item.ItemIcon;
		MultiLanguageText probabilityText = item.ProbabilityText;
		var preview = mItemProbabilityAmount[index];
		int classId = preview.ItemId; double val = preview.Probability; int num = preview.Amount;
		probabilityText.SetKey("{0}", new HomeCartStandardSignificantPercentArg(val, Services.CurrentLanguage));
		itemIcon.Init(classId);
		itemIcon.SetClickPopupInfoDialog().SetCount(num).SetBottomName();
	}
}
}
