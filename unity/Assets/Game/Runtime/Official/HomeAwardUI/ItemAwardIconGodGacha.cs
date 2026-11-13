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
public class ItemAwardIconGodGacha : MonoBehaviour, IGameObjectPoolItem
{
	private enum IconState
	{
		None,
		TransitionPieces,
		TransitionToken,
		TransitionPiecesAndToken
	}

	[SerializeField]
	private Transform mAnchor;

	[SerializeField]
	private WorldStateSwitcher<IconState> mIconStateSwitcher;

	[SerializeField]
	private ImageProxy mAgiotageIconImage;

	[SerializeField]
	private ImageProxy mRecycleIconImage;

	[SerializeField]
	private MultiLanguageText mAgiotageCountText;

	[SerializeField]
	private MultiLanguageText mRecycleCountText;

	private ItemIcon mItemIcon;

	private AwardData mAwardData;

	private Action<GameObject> mOnClickCallback;

	public IHomeAwardIconServices Services { get; set; }

	private void OnDisable()
	{
		mAgiotageIconImage.Clear();
		mRecycleIconImage.Clear();
		mIconStateSwitcher.Clear();
		RecycleIcon();
	}

	public void Init(AwardData data, Action<GameObject> onClickCallback = null)
	{
		mAwardData = data;
		mOnClickCallback = onClickCallback;
	}

	public void RecycleIcon()
	{
		if (Services != null && (Object)mItemIcon != (Object)null)
		{
			Services.RecycleIcon(mItemIcon);
			mItemIcon = null;
		}
	}

	private void InitItemIcon(ItemInfo itemInfo)
	{
		RecycleIcon();
		mItemIcon = Services.GetIcon(itemInfo, mAnchor);
		mItemIcon.SetBottomName().SetBottomTextColor(ThemeColorType.Dark).SetBottomTextEffect(isEnable: true);
		mItemIcon.SetClickPopupInfoDialog(mOnClickCallback);
	}

	public void RefreshOrigin()
	{
		ItemInfo itemInfo = Services.GetItemInfo(mAwardData.AwardId);
		if (itemInfo != null)
		{
			InitItemIcon(itemInfo);
			mIconStateSwitcher.SetState(IconState.None);
			if (mAwardData.IsAwardDataAgiotage(out var _, out var _) || itemInfo.IsSkill || itemInfo.ItemType == ItemType.Treasure)
			{
				mItemIcon.HideText();
			}
			else
			{
				mItemIcon.SetCount(mAwardData.Amount);
			}
		}
	}

	public void RefreshTransition()
	{
		if (mAwardData.IsAwardDataAgiotage(out var _, out var isRecycle))
		{
			mAwardData.GetDetailAward(out (int, long) agiotageAward, out (int, long) recycleAward);
			ItemInfo itemInfo = ((mAwardData.RecycleAwards != null && mAwardData.RecycleAwards.Count > 0) ? Services.GetItemInfo(mAwardData.RecycleAwards[0].AwardId) : null);
			IconState state;
			if (isRecycle)
			{
				mRecycleCountText.Format(Services.Compact(recycleAward.Item2));
				mRecycleIconImage.Load(itemInfo.Icon);
				state = ((agiotageAward.Item2 <= 0) ? IconState.TransitionToken : IconState.TransitionPiecesAndToken);
			}
			else
			{
				state = IconState.TransitionPieces;
			}
			mAgiotageCountText.Format(Services.Compact(agiotageAward.Item2));
			if ((Object)mItemIcon == (Object)null)
			{
				ItemInfo itemInfo2 = Services.GetItemInfo(mAwardData.AwardId);
				mAgiotageIconImage.Load(itemInfo2.Icon);
			}
			else
			{
				mAgiotageIconImage.Load(mItemIcon.myInfo.Icon);
			}
			mIconStateSwitcher.SetState(state);
			InitItemIcon(Services.GetItemInfo(mAwardData.AwardId));
		}
	}

	public void ShowAttachment(ItemIconAttachmentType type, bool value)
	{
		if (!((Object)mItemIcon == (Object)null))
		{
			mItemIcon.SetAttachment(type, value);
		}
	}

	public void InitQualityImage(Quality quality)
	{
		if (!((Object)mItemIcon == (Object)null))
		{
			mItemIcon.UpdateQualityImage(quality);
		}
	}

	void IGameObjectPoolItem.OnGameObjectPoolReset()
	{
	}

	void IGameObjectPoolItem.OnGameObjectPoolClear()
	{
	}
}
}
