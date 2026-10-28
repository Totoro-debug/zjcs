namespace Zjcs.Official.ProfessionUI
{
using Object = UnityEngine.Object;
using PopupWindow = Zjcs.Official.WorldUI.WorldPopupWindow;
using System;
using System.Collections.Generic;
using UnityEngine;

public class TopBarHandler : MonoBehaviour
{
	[Header("预展示的货币类型")]
	[SerializeField]
	private List<PlayerItemType> mShowTypeList;

	[Header("直接按物品ID展示的货币（无需枚举/代码）")]
	[SerializeField]
	private List<int> mShowItemIdList = new List<int>();

	[Header("顶部栏挂载的位置")]
	[SerializeField]
	private Transform mSlot;

	[Header("跟随的弹窗")]
	[SerializeField]
	private PopupWindow mPopupWindow;

	[Header("等待窗口进入动画播放完毕后显示")]
	[SerializeField]
	private bool mShowWaitEnterAnim;

	[Header("在窗口退出动画开始前隐藏")]
	[SerializeField]
	private bool mHideBeforeExitAnim;

	private TopBarItem mTopBar;

	private List<CurrencyItemData> mShowDatas = new List<CurrencyItemData>();

	private bool mIsStopUpdateCount;

	private ITopBarManager TopBarManager { get; set; }

	public void Bind(ITopBarManager manager) => TopBarManager = manager ?? throw new ArgumentNullException(nameof(manager));

	private void Awake()
	{
		foreach (PlayerItemType mShowType in mShowTypeList)
		{
			mShowDatas.Add(new CurrencyItemData(mShowType));
		}
		foreach (int mShowItemId in mShowItemIdList)
		{
			mShowDatas.Add(new CurrencyItemData(PlayerItemType.None, new PlayerItemUIItemIdProvider
			{
				ItemId = mShowItemId
			}));
		}
	}

	private void OnEnable()
	{
		if (!TryBorrowTopBar())
		{
			return;
		}
		((Component)mTopBar).gameObject.SetActive((Object)(object)mPopupWindow == (Object)null || !mShowWaitEnterAnim);
		if ((Object)(object)mPopupWindow != (Object)null)
		{
			if (mShowWaitEnterAnim)
			{
				mPopupWindow.OnShow += new Action<PopupWindow>(Show);
			}
			if (mHideBeforeExitAnim)
			{
				mPopupWindow.OnHiding += new Action<PopupWindow>(Hide);
			}
		}
	}

	private void OnDisable()
	{
		TryReturnTopBar();
		if ((Object)(object)mPopupWindow != (Object)null)
		{
			if (mShowWaitEnterAnim)
			{
				mPopupWindow.OnShow -= new Action<PopupWindow>(Show);
			}
			if (mHideBeforeExitAnim)
			{
				mPopupWindow.OnHiding -= new Action<PopupWindow>(Hide);
			}
		}
	}

	private bool IsValid()
	{
		return mShowDatas.Count > 0;
	}

	private bool TryBorrowTopBar()
	{






		if ((Object)(object)mTopBar != (Object)null)
		{
			TryReturnTopBar();
		}
		if (!IsValid())
		{
			return false;
		}
		mTopBar = TopBarManager.BorrowTopBar(mShowDatas);
		mTopBar.SetStopUpdateCount(mIsStopUpdateCount);
		((Component)mTopBar).transform.SetParent(mSlot);
		RectTransform component = ((Component)mTopBar).GetComponent<RectTransform>();
		component.anchoredPosition = (Vector2)Vector3.zero;
		((Transform)component).localPosition = new Vector3(((Transform)component).localPosition.x, ((Transform)component).localPosition.y, 0f);
		((Transform)component).localScale = Vector3.one;
		return true;
	}

	private void TryReturnTopBar()
	{
		if (!((Object)(object)mTopBar == (Object)null))
		{
			mTopBar.SetStopUpdateCount(isStop: false);
			TopBarManager.ReturnTopBar(mTopBar);
			mTopBar = null;
		}
	}

	private void Show(PopupWindow window)
	{
		if (!((Object)(object)mTopBar == (Object)null))
		{
			((Component)mTopBar).gameObject.SetActive(true);
		}
	}

	private void Hide(PopupWindow window)
	{
		if (!((Object)(object)mTopBar == (Object)null))
		{
			((Component)mTopBar).gameObject.SetActive(false);
		}
	}

	private void UpdateTopBar()
	{
		if (!((Object)(object)mTopBar == (Object)null) || TryBorrowTopBar())
		{
			TopBarManager.UpdateTopBar(mTopBar, mShowDatas);
			mTopBar.SetStopUpdateCount(mIsStopUpdateCount);
		}
	}

	public void UpdateCurrencyData(PlayerItemType type, int itemId)
	{
		mShowDatas.Clear();
		mShowDatas.Add(new CurrencyItemData(type, new PlayerItemUIItemIdProvider
		{
			ItemId = itemId
		}));
		UpdateTopBar();
	}

	public void UpdateCurrencyData(Dictionary<PlayerItemType, int> dict)
	{
		mShowDatas.Clear();
		if (dict != null)
		{
			foreach (KeyValuePair<PlayerItemType, int> item in dict)
			{
				mShowDatas.Add(new CurrencyItemData(item.Key, new PlayerItemUIItemIdProvider
				{
					ItemId = item.Value
				}));
			}
		}
		UpdateTopBar();
	}

	public void UpdateCurrencyData(List<PlayerItemType> types)
	{
		mShowDatas.Clear();
		if (types != null)
		{
			foreach (PlayerItemType type in types)
			{
				mShowDatas.Add(new CurrencyItemData(type));
			}
		}
		UpdateTopBar();
	}

	public void UpdateCurrencyDataByItemId(List<int> itemIds)
	{
		mShowDatas.Clear();
		if (itemIds != null)
		{
			foreach (int itemId in itemIds)
			{
				mShowDatas.Add(new CurrencyItemData(PlayerItemType.None, new PlayerItemUIItemIdProvider
				{
					ItemId = itemId
				}));
			}
		}
		UpdateTopBar();
	}

	public void UpdateEmptyCurrency()
	{
		mShowDatas.Clear();
		UpdateTopBar();
	}

	public void UpdateCurrencyData(List<(PlayerItemType, PlayerItemUIItemIdProvider)> list)
	{
		mShowDatas.Clear();
		if (list != null)
		{
			foreach (var item in list)
			{
				mShowDatas.Add(new CurrencyItemData(item.Item1, item.Item2));
			}
		}
		UpdateTopBar();
	}

	public void UpdateCurrencyData(Dictionary<PlayerItemType, PlayerItemUIItemIdProvider> dict)
	{
		mShowDatas.Clear();
		if (dict != null)
		{
			foreach (KeyValuePair<PlayerItemType, PlayerItemUIItemIdProvider> item in dict)
			{
				mShowDatas.Add(new CurrencyItemData(item.Key, item.Value));
			}
		}
		UpdateTopBar();
	}

	public void SetStopUpdateCount(bool isStop)
	{
		mIsStopUpdateCount = isStop;
		if ((Object)(object)mTopBar != (Object)null)
		{
			mTopBar.SetStopUpdateCount(mIsStopUpdateCount);
		}
	}
}

}
