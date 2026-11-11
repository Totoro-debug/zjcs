// Original menu recommendation implementation; explicit data/resource bindings.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomePlayerPanelUI;
using Zjcs.Official.SkillsUI.FancyScrollView;
namespace Zjcs.Official.HomeFunctionMenuUI
{
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Zjcs.Official.SkillsUI.EasingCore;
using UnityEngine;

public class ActivityRecommendItemView : FancyScrollView<ActivtyEntryKeyData, ActivityRecommendContext>
{
	public IHomeMenuActivityServices Services { private get; set; }

	[SerializeField]
	private Scroller mScroller;

	[SerializeField]
	private GameObject mCellPrefab;

	[SerializeField]
	private OfficialWwiseEvent mPreSelectedEvent;

	[SerializeField]
	private GameObject mBannerDotPrefab;

	[SerializeField]
	private Transform mBannerDotSlot;

	[SerializeField]
	private Transform mPoolItemSlot;

	[SerializeField]
	private int mAutoNextDurationTimeMs;

	private Action<int> mOnSelectionChanged;

	private List<FunctionEntryBannerDotItem> mFunctionEntryBannerDotItems = new List<FunctionEntryBannerDotItem>();

	private FunctionEntryBannerDotItem mCurSelectedDotItem;

	private CancellationTokenSource mCancelSource;

	protected override GameObject CellPrefab => mCellPrefab;

	protected override void Initialize()
	{
		base.Initialize();
		Context.Services = Services;
		Context.OnCellClicked = OnCellClicked;
		Context.OnCellPreSelected = OnCellPreSelected;
		Context.PoolItemSlot = mPoolItemSlot;
		mScroller.OnValueChanged(UpdatePosition);
		mScroller.OnSelectionChanged(UpdateSelection);
	}

	private void OnDestroy()
	{
		StopAutoNextTimer();
		foreach (Queue<ActivityEntryBaseItem> value in Context.EntryItemPoolDict.Values)
		{
			value.Clear();
		}
		Context.OnCellClicked = null;
		Context.OnCellPreSelected = null;
		Context.PoolItemSlot = null;
		Context.EntryItemPoolDict.Clear();
		Context.EntryItemPoolDict = null;
	}

	private void UpdateSelection(int index)
	{
		if (Context.SelectedIndex != index)
		{
			Context.SelectedIndex = index;
			Refresh();
			SetDotSelected(index);
			mOnSelectionChanged?.Invoke(index);
			RestartAutoNextTimer();
		}
	}

	private void RestartAutoNextTimer()
	{
		if (mCancelSource != null)
		{
			StopAutoNextTimer();
		}
		mCancelSource = new CancellationTokenSource();
		StartAutoNextTimer(mCancelSource.Token).Forget();
	}

	private void StopAutoNextTimer()
	{
		if (mCancelSource != null)
		{
			mCancelSource.Cancel();
			mCancelSource.Dispose();
			mCancelSource = null;
		}
	}

	private async UniTask StartAutoNextTimer(CancellationToken token)
	{
		await UniTask.Delay(mAutoNextDurationTimeMs, ignoreTimeScale: false, PlayerLoopTiming.Update, token);
		if (this != null)
		{
			SelectNextCell();
		}
	}

	public void UpdateData(IList<ActivtyEntryKeyData> items, int startIndex)
	{
		mScroller.Draggable = items.Count > 1;
		Context.DataUpdateCompleted = false;
		Context.PreSelectedIndex = startIndex;
		UpdateContents(items);
		mScroller.SetTotalCount(items.Count);
		SelectCell(startIndex, instant: true);
		UpdateDots(items.Count, startIndex);
		RestartAutoNextTimer();
		Context.DataUpdateCompleted = true;
	}

	private void UpdateDots(int count, int startIndex)
	{
		int num = Math.Max(count, mFunctionEntryBannerDotItems.Count);
		for (int i = 0; i < num; i++)
		{
			FunctionEntryBannerDotItem functionEntryBannerDotItem;
			if (i >= mFunctionEntryBannerDotItems.Count)
			{
				functionEntryBannerDotItem = HomeMenuUIUtils.Instantiate(Services, mBannerDotPrefab, mBannerDotSlot).GetComponent<FunctionEntryBannerDotItem>();
				mFunctionEntryBannerDotItems.Add(functionEntryBannerDotItem);
			}
			else
			{
				functionEntryBannerDotItem = mFunctionEntryBannerDotItems[i];
			}
			if (functionEntryBannerDotItem != null)
			{
				functionEntryBannerDotItem.gameObject.SetActive(i < count);
				bool flag = i == startIndex;
				functionEntryBannerDotItem.SetSelected(flag);
				if (flag)
				{
					mCurSelectedDotItem = functionEntryBannerDotItem;
				}
			}
		}
	}

	private void SetDotSelected(int index)
	{
		if (index < mFunctionEntryBannerDotItems.Count)
		{
			if (mCurSelectedDotItem != null)
			{
				mCurSelectedDotItem.SetSelected(isSelect: false);
			}
			mFunctionEntryBannerDotItems[index].SetSelected(isSelect: true);
			mCurSelectedDotItem = mFunctionEntryBannerDotItems[index];
		}
	}

	public void SelectNextCell()
	{
		int index = (Context.SelectedIndex + 1) % ItemsSource.Count;
		SelectCell(index);
	}

	public void SelectPrevCell()
	{
		int index = (Context.SelectedIndex - 1) % ItemsSource.Count;
		SelectCell(index);
	}

	private void OnCellClicked(int index)
	{
		SelectCell(index);
	}

	private void OnCellPreSelected()
	{
		mPreSelectedEvent?.Post(gameObject);
	}

	public void SelectCell(int index, bool instant = false)
	{
		if (index >= 0 && index < ItemsSource.Count && index != Context.SelectedIndex)
		{
			if (instant)
			{
				mScroller.JumpTo(index);
			}
			else
			{
				mScroller.ScrollTo(index, 0.3f, Ease.OutCubic);
			}
		}
	}

	public void SetSelectCallback(Action<int> callback)
	{
		mOnSelectionChanged = callback;
	}

	public int GetSelectionIndex()
	{
		return Context.SelectedIndex;
	}
}
}
