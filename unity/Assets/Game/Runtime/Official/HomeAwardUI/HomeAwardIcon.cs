// Migrated from the selected official 884_156692 client/common source.
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using Event = Zjcs.Official.ProfessionUI.OfficialWwiseEvent;
using UIParticle = Coffee.UIExtensions.UIParticle;
namespace Zjcs.Official.HomeAwardUI
{
public sealed class HomeAwardIcon : UIBase, IGameObjectPoolItem
{
	private enum IconState
	{
		None,
		Pieces,
		Token,
		PiecesAndToken
	}

	public Transform Anchor;

	[SerializeField]
	private WorldStateSwitcher<IconState> mIconStateSwitcher;

	public ImageProxy AgioIcon;

	public MultiLanguageText AgioCountText;

	public ImageProxy RecycleItemIcon;

	public MultiLanguageText RecycleText;

	private AwardData mAwardData;

	private ItemInfo mItemInfo;

	private Action<HomeAwardIcon, AwardData> mInitItemIcon;

	private bool mAutoTransition;

	private bool mIsSelf;

	private bool mShowOutputAddition;

	public IHomeAwardIconServices Services { get; set; }

	public ItemIcon ItemIcon { get; private set; }

	public event Action OnInitCompleted;

	public void Init(AwardData data, Action<HomeAwardIcon, AwardData> initItemIcon = null, bool autoTransition = true, bool isSelf = true, bool showOutputAddition = true)
	{
		InitWrapper(() =>
		{
			mAwardData = data;
			mInitItemIcon = initItemIcon;
			mAutoTransition = autoTransition;
			mIsSelf = isSelf;
			mShowOutputAddition = showOutputAddition;
		});
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		ShowAward();
	}

	protected override void OnDisableAndInited()
	{
		RecycleIcon();
		mIconStateSwitcher.Clear();
		AgioIcon.Clear();
		RecycleItemIcon.Clear();
		base.OnDisableAndInited();
	}

	private void InitItemIcon(ItemInfo itemInfo)
	{
		RecycleIcon();
		ItemIcon = Services.GetIcon(itemInfo, Anchor);
		ItemIcon.HideText();
		SetItemIconStyle();
		ItemIcon.SetClickPopupInfoDialog();
	}

	public void TryPlayTransition()
	{
		if (mAwardData.IsAwardDataAgiotage(out var _, out var isRecycle))
		{
			mAwardData.GetDetailAward(out (int, long) agiotageAward, out (int, long) recycleAward);
			ItemInfo itemInfo = ((mAwardData.RecycleAwards != null && mAwardData.RecycleAwards.Count > 0) ? Services.GetItemInfo(mAwardData.RecycleAwards[0].AwardId) : null);
			IconState state;
			if (isRecycle)
			{
				RecycleText.Format(Services.Compact(recycleAward.Item2));
				RecycleItemIcon.Load(itemInfo.Icon);
				state = ((agiotageAward.Item2 <= 0) ? IconState.Token : IconState.PiecesAndToken);
			}
			else if (mItemInfo.IsSkillPiece || mItemInfo.ItemType == ItemType.TreasurePiece)
			{
				state = IconState.Pieces;
			}
			else
			{
				RecycleText.Format(Services.Compact(agiotageAward.Item2));
				RecycleItemIcon.Load(ItemIcon.myInfo.Icon);
				state = IconState.Token;
			}
			AgioCountText.Format(Services.Compact(agiotageAward.Item2));
			AgioIcon.Load(ItemIcon.myInfo.Icon);
			mIconStateSwitcher.SetState(state);
			InitItemIcon(Services.GetItemInfo(mAwardData.AwardId));
		}
	}

	public GameObject SetFrameEffect(Transform transform = null, bool isShow = true)
	{
		if ((Object)ItemIcon == (Object)null)
		{
			return null;
		}
		return ItemIcon.SetFrameEffect(transform, isShow);
	}

	public void InitQualityImage(Quality quality)
	{
		ItemIcon.UpdateQualityImage(quality);
	}

	public void Clear()
	{
		RecycleIcon();
		ResetInitState();
	}

	private void SetItemIconStyle()
	{
		ItemIcon.SetBottomName().SetBottomTextColor(ThemeColorType.Dark).SetBottomTextEffect(isEnable: true);
		if (ItemIcon is ItemIconSkill itemIconSkill)
		{
			itemIconSkill.ShowSkillLevel(show: false);
		}
	}

	private void ShowAward()
	{
		ShowOriginalIcon();
		mIconStateSwitcher.SetState(IconState.None);
		if (mAutoTransition)
		{
			TryPlayTransition();
		}
		ItemIcon.SetAttachment(ItemIconAttachmentType.ExtraGetTip, mAwardData.AddItemType == AddItemRewardType.Extra);
		if (mAwardData.OverflowCount > 0)
		{
			ItemIcon.SetAttachment(ItemIconAttachmentType.UpperLimitTip, show: true);
		}
		CheckShowOutputAdditionText();
		CheckShowAttachmentType();
		mInitItemIcon?.Invoke(this, mAwardData);
		OnInitCompleted?.Invoke();
	}

	private void CheckShowOutputAdditionText()
	{
		if (!mAwardData.IsOutputAddition || !mShowOutputAddition)
		{
			return;
		}
		ItemInfo itemInfo = Services.GetItemInfo(mAwardData.AwardId);
		if (itemInfo == null)
		{
			return;
		}
		IHomeAwardOutputAddition attachment = Services.GetOutputAddition(ItemIcon);
		if (!(attachment == null))
		{
			ItemIcon.HideText();
			if (itemInfo.IsEquip)
			{
				attachment.Format(Services.Text("ui_output_addition_equip_text"));
			}
			else
			{
				attachment.Format(Services.Compact(mAwardData.Amount));
			}
		}
	}

	private void CheckShowAttachmentType()
	{
		if (mAwardData.ShowAttachmentType != ShowAttachmentType.None && Services.GetItemInfo(mAwardData.AwardId) != null)
		{
			HandlerItemIconAttachmentType(mAwardData.ShowAttachmentType);
		}
	}

	private void HandlerItemIconAttachmentType(ShowAttachmentType showAttachmentType)
	{
		if (showAttachmentType != ShowAttachmentType.None && showAttachmentType == ShowAttachmentType.Expire)
		{
			ItemIcon.GetAttachment(ItemIconAttachmentType.ExpireTip);
			return;
		}
		Debug.LogError("Original award UI: missing configured dependency.");
	}

	private void ShowOriginalIcon()
	{
		InitItemInfo();
		SetItemIconStyle();
		string countKey = ((mAwardData.Amount > 0) ? "ui_ItemIcon_count_key" : "ui_ItemIcon_count_overflow_key");
		if (!mItemInfo.CanOverlap)
		{
			ItemIcon.HideText();
		}
		else
		{
			ItemIcon.SetCount(mAwardData.Amount, countKey);
		}
		ItemIcon.SetClickPopupInfoDialog();
		if (ItemIcon is IHomeAwardAppearanceIcon itemIconAppendAppearance)
		{
			itemIconAppendAppearance.ShowRemainTime(mAwardData.Amount);
		}
		else if (ItemIcon is IHomeAwardTreasureIcon itemIconTreasure)
		{
			itemIconTreasure.SetAttachment(ItemIconAttachmentType.PrayEffect, show: true);
		}
	}

	private void InitItemInfo()
	{
		if (!mIsSelf)
		{
			InitIcomByItemClass();
		}
		else if (!TryInitIcomByItemIndex())
		{
			InitIcomByItemClass();
		}
	}

	private bool TryInitIcomByItemIndex()
	{
		if (mAwardData.Index == 0)
		{
			return false;
		}
		IProfessionItemData item = Services.GetItem(mAwardData.Index);
		if (item == null)
		{
			Debug.LogWarning("Original award UI: missing configured dependency.");
			return false;
		}
		mItemInfo = Services.GetItemInfo(item.Id);
		ItemIcon = Services.GetIcon(item, Anchor);
		return true;
	}

	private void InitIcomByItemClass()
	{
		ItemInfo itemInfo = Services.GetItemInfo(mAwardData.AwardId);
		if (itemInfo == null)
		{
			Debug.LogError("Original award UI: missing configured dependency.");
		}
		else
		{
			mItemInfo = itemInfo;
			ItemIcon = Services.GetIcon(itemInfo, Anchor);
		}
	}

	private void RecycleIcon()
	{
		if (Services != null && (Object)ItemIcon != (Object)null)
		{
			ItemIcon.ResetCountTextColor();
			Services.RecycleIcon(ItemIcon);
			ItemIcon = null;
		}
	}

	public void OnGameObjectPoolReset()
	{
	}

	public void OnGameObjectPoolClear()
	{
	}
}
}
