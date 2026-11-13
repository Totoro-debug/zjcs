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
public class AwardListPanelItem : UIBase, IGameObjectPoolItem
{
	public readonly string ImgLightPath = "UI/Reward/ImgLight_{0}";

	[SerializeField]
	private AwardListPanelItemType mItemType;

	[SerializeField]
	private Transform mSlot;

	[SerializeField]
	private MultiLanguageText mItemNameText;

	[SerializeField]
	private Image mLightImage;

	[SerializeField]
	private UIParticle mParticle;

	[SerializeField]
	private WorldStateSwitcher<bool> mStateSwitcher;

	[SerializeField]
	private MultiLanguageText mOutputAdditionItemNameText;

	[SerializeField]
	private MultiLanguageText mOutputAdditionItemCountText;

	[SerializeField]
	private bool mIsHideQualityImg;

	private HomeAwardIcon mItemAwardIcon;

	private AwardData mData;

	private Action<AwardListPanelItem> mReturnPoolAction;

	private Action<HomeAwardIcon, AwardData> mInitItemAwardIcon;

	private static int sTextArgsLength = 3;

	private object[] mTextArgs = new object[sTextArgsLength];

	private HomeAwardResourceBag mDisposableBag;

	public AwardListPanelItemType ItemType => mItemType;

	public IHomeAwardItemServices Services { get; set; }





	public void Init(AwardData data, Action<AwardListPanelItem> returnPoolAction)
	{
		InitWrapper(() =>
		{
			mData = data;
			mReturnPoolAction = returnPoolAction;
		});
	}

	protected override void OnEnableAndInited()
	{

		base.OnEnableAndInited();
		ItemInfo itemInfo = Services.GetItemInfo(mData.AwardId);
		if (itemInfo == null)
		{
			return;
		}
		mDisposableBag = new HomeAwardResourceBag();
		if (mInitItemAwardIcon == null)
		{
			mInitItemAwardIcon = InitItemAwardIcon;
		}
		if ((Object)mItemAwardIcon == (Object)null)
		{
			mItemAwardIcon = Services.InstantiateAward(mSlot, mData, mInitItemAwardIcon, autoTransition: true, isSelf: true, showOutputAddition: false);
		}
		else
		{
			mItemAwardIcon.Init(mData, mInitItemAwardIcon, autoTransition: true, isSelf: true, showOutputAddition: false);
		}
		UpdateLightImage(itemInfo);
		if (mData.Agiotage > 0)
		{
			if (mData.Agiotage > 1)
			{
				mItemNameText.SetKey("ui_award_list_tip", Services.CultureText(Services.GetItemNameKey(mData.AgiotageId)), Services.Compact(mData.Agiotage));
			}
			else
			{
				mItemNameText.SetKey(Services.GetItemNameKey(mData.AgiotageId));
			}
		}
		else
		{
			(long raw, long extra) tuple = SplitAwardExtraAmount(mData);
			long item = tuple.raw;
			long item2 = tuple.extra;
			bool flag = (Object)mOutputAdditionItemNameText != (Object)null && mData.IsOutputAddition && item > 0;
			mStateSwitcher.SetState(flag);
			if (flag)
			{
				SetOutputAddtionText(mData, item);
			}
			else
			{
				SetText(mData, item, item2);
			}
		}
		mItemAwardIcon.ItemIcon.SetAttachment(ItemIconAttachmentType.ExtraGetTip, mData.AddItemType == AddItemRewardType.Extra);
		if ((Object)mParticle != (Object)null)
		{
			mParticle.Play();
		}
	}

	protected override void OnDisableAndInited()
	{
		mDisposableBag.Dispose();
		base.OnDisableAndInited();
	}

	private void InitItemAwardIcon(HomeAwardIcon itemAwardIcon, AwardData awardData)
	{
		itemAwardIcon.ItemIcon.HideBottomText();
		itemAwardIcon.ItemIcon.HideText();
		if (mIsHideQualityImg)
		{
			itemAwardIcon.ItemIcon.HideQualityImg();
		}
	}

	private void SetText(AwardData data, long rawAmount, long extraAmount)
	{


		Array.Clear(mTextArgs, 0, sTextArgsLength);
		string key = "{0}";
		mTextArgs[0] = Services.CultureText(Services.GetItemNameKey(data.AwardId));
		if (rawAmount > 1)
		{
			key = "ui_award_list_tip";
			mTextArgs[1] = Services.Compact(rawAmount);
			if (extraAmount > 0)
			{
				key = "ui_award_list_append_tip";
				mTextArgs[2] = Services.DecorateExtra(Services.Compact(extraAmount), Color.green);
			}
		}
		mItemNameText.SetKey(key, mTextArgs);
	}

	private void SetOutputAddtionText(AwardData data, long rawAmount)
	{
		mOutputAdditionItemNameText.SetKey("{0}", Services.CultureText(Services.GetItemNameKey(data.AwardId)));
		mOutputAdditionItemCountText.SetKey("x{0}", Services.Compact(rawAmount));
	}

	private (long raw, long extra) SplitAwardExtraAmount(AwardData data)
	{
		long num = 0L;
		long item = data.Amount;
		if (data.AwardId == Services.MoneyId)
		{
			double prop = Services.GetMoneyAdditionPercent();
			num = (long)((double)data.Amount * prop / (1.0 + prop));
			item = data.Amount - num;
		}
		else if (data.AwardId == Services.PlayerExpItemId)
		{
			double prop2 = Services.GetExperienceAdditionPercent();
			num = (long)((double)data.Amount * prop2 / (1.0 + prop2));
			item = data.Amount - num;
		}
		return (raw: item, extra: num);
	}

	public void ReturnPool()
	{
		mReturnPoolAction?.Invoke(this);
		if ((Object)mParticle != (Object)null)
		{
			mParticle.Stop();
		}
	}

	public void UpdateLightImage(ItemInfo itemInfo)
	{
		if (itemInfo.Quality >= Quality.Orange)
		{
			((Component)mLightImage).gameObject.SetActive(true);
			mLightImage.sprite = mDisposableBag.Add(Services.LoadSprite(string.Format(ImgLightPath, itemInfo.Quality))).Result;
		}
		else
		{
			((Component)mLightImage).gameObject.SetActive(false);
		}
	}

	public void OnGameObjectPoolReset()
	{



		this.transform.localPosition = Vector3.zero;
		this.transform.localRotation = Quaternion.identity;
		this.transform.localScale = Vector3.one;
	}

	public void OnGameObjectPoolClear()
	{
		mItemAwardIcon.Clear();
		mReturnPoolAction = null;
		ResetInitState();
	}
}
}
