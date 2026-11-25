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
public class AwardPanel : UIBase
{
    public IHomeCartWindowServices Services { protected get; set; }
	[SerializeField]
	private WorldStateSwitcher<AwardContent.ShowItemStateType> mItemStateSwitcher;

	[SerializeField]
	private AwardContent mAwardContent;

	[SerializeField]
	private MultiLanguageText mTitleText;

	[SerializeField]
	private List<ItemType> mShowEffectItemTypes;

	[SerializeField]
	private float mCanCloseTime;

	[SerializeField]
	private Button mCloseBtn;

	private string mTitleKey;

	private bool mCanClose;

	private List<AwardData> mAwardList;

	private bool mNeedMergeItem;

	private Action<ItemAwardIcon, AwardData> mInitItemIcon;

	private Action mOnCloseClick;

	private Action<ItemAwardIcon, AwardData> mDefaultInitItemIcon;

	private void Awake()
	{
		mDefaultInitItemIcon = DefaultInit;
	}

	protected override void Start()
	{
		base.Start();
		mAwardContent.OnShowItemStateUpdate += OnAwardContentShowItemStateUpdate;
		mCloseBtn.onClick.AddListener(OnClick);
	}

	protected override void OnDestroy()
	{
		mCloseBtn.onClick.RemoveListener(OnClick);
		mAwardContent.OnShowItemStateUpdate -= OnAwardContentShowItemStateUpdate;
		base.OnDestroy();
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		mCanClose = false;
		Refresh();
		mAwardContent.Init(mAwardList, mNeedMergeItem, mInitItemIcon);
		this.StartCoroutine(ShowIn());
	}

	protected override void OnDisableAndInited()
	{
		mAwardContent.Clear();
		this.StopAllCoroutines();
		base.OnDisableAndInited();
	}

	private void OnClick()
	{
		if (mCanClose)
		{
			mCanClose = false;
			Services.Close(this.gameObject);
			mOnCloseClick?.Invoke();
		}
	}

	public void Init(IHomeHangUpAwardMessage msg, string titleKey = null, bool needMergeItem = true)
	{
		var list = new List<AwardData>();
		if (msg?.DataList != null)
			foreach (object entry in msg.DataList)
				if (entry is AwardData ad) list.Add(ad);
		Init(list, titleKey, needMergeItem);
	}

	public void Init(List<AwardData> awardList, string title, bool needMergeItem = true)
	{
		Init(awardList, title, needMergeItem, mDefaultInitItemIcon);
	}

	public void Init(List<AwardData> awardList, string titleKey = null, bool needMergeItem = true, Action<ItemAwardIcon, AwardData> initItemIcon = null, Action onClose = null)
	{
		InitWrapper(() =>
		{
			mTitleKey = titleKey;
			mAwardList = awardList;
			mNeedMergeItem = needMergeItem;
			mInitItemIcon = initItemIcon;
			mOnCloseClick = onClose;
		});
	}

	private void DefaultInit(ItemAwardIcon itemIcon, AwardData data)
	{
		ItemInfo itemInfo = Services.GetAwardItemInfo(data.AwardId);
		if (itemInfo != null && (data.Agiotage > 0 || mShowEffectItemTypes.Contains(itemInfo.ItemType)))
		{
			itemIcon.SetFrameEffect();
		}
		if (Services.CanUseItem(data.AwardId))
		{
			itemIcon.ItemIcon.SetClickPopupInfoDialog(PopupItemDetailDialogCallBack);
		}
	}

	private void Refresh()
	{
		mTitleText.SetKey(mTitleKey ?? "AwardPanel.CommonTitle");
	}

	public void SetCanClose()
	{
		mCanClose = true;
	}

	private IEnumerator ShowIn()
	{
		yield return (object)new WaitForSeconds(mCanCloseTime);
		SetCanClose();
	}

	private void OnAwardContentShowItemStateUpdate(AwardContent.ShowItemStateType state)
	{
		mItemStateSwitcher.SetState(state);
	}

	private void PopupItemDetailDialogCallBack(GameObject go)
	{
		Services.EnableAwardItemDetailUseButton(go);
	}
}
}
