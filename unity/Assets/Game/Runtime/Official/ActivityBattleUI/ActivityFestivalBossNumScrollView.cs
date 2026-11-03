// Migrated selected official activity selector; see activity-selector-runtime-source-manifest.json.
using Zjcs.Official.WorldUI;
using Zjcs.Official.ProfessionUI;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.ActivityBattleUI
{
using System;
using System.Collections.Generic;
using Zjcs.Official.SkillsUI.EasingCore;
using Zjcs.Official.SkillsUI.FancyScrollView;
using UnityEngine;

public class ActivityFestivalBossNumScrollView : FancyScrollView<int, ActivityFestivalBossNumContext>
{
	[SerializeField]
	private Scroller scroller;

	[SerializeField]
	private GameObject cellPrefab;

	[SerializeField]
	private AudioClip mPreSelectedEvent;

	[NonSerialized] public Action<AudioClip, GameObject> Post;

	private Action<int> mOnSelectionChanged;

	protected override GameObject CellPrefab => cellPrefab;

	protected override void Initialize()
	{
		base.Initialize();
		Context.OnCellClicked = OnCellClicked;
		Context.OnCellPreSelected = OnCellPreSelected;
		scroller.OnValueChanged(UpdatePosition);
		scroller.OnSelectionChanged(UpdateSelection);
	}

	private void UpdateSelection(int index)
	{
		if (Context.SelectedIndex != index)
		{
			Context.SelectedIndex = index;
			Refresh();
			mOnSelectionChanged?.Invoke(index);
		}
	}

	public void UpdateData(IList<int> items, int startIndex)
	{
		Context.DataUpdateCompleted = false;
		Context.PreSelectedIndex = startIndex;
		UpdateContents(items);
		scroller.SetTotalCount(items.Count);
		SelectCell(startIndex, instant: true);
		Context.DataUpdateCompleted = true;
	}

	public void SelectNextCell()
	{
		SelectCell(Context.SelectedIndex + 1);
	}

	public void SelectPrevCell()
	{
		SelectCell(Context.SelectedIndex - 1);
	}

	private void OnCellClicked(int index)
	{
		SelectCell(index);
	}

	private void OnCellPreSelected()
	{
		AudioClip val = mPreSelectedEvent;
		if (val != null)
		{
			Post?.Invoke(val, ((Component)this).gameObject);
		}
	}

	public void SelectCell(int index, bool instant = false)
	{
		if (index >= 0 && index < ItemsSource.Count && index != Context.SelectedIndex)
		{
			if (instant)
			{
				scroller.JumpTo(index);
			}
			else
			{
				scroller.ScrollTo(index, 0.35f, Ease.OutCubic);
			}
		}
	}

	public void SetSelectCallback(Action<int> callback)
	{
		mOnSelectionChanged = callback;
	}

	public void SetIsUnlockedDelegate(Func<int, bool> isUnlockedDelegate)
	{
		Context.IsUnlockedDelegate = isUnlockedDelegate;
	}

	public void SetShowRedDotRewardDelegate(Func<int, bool> canFetchRewardDelegate)
	{
		Context.ShowRedDotDelegate = canFetchRewardDelegate;
	}

	public int GetSelectionIndex()
	{
		return Context.SelectedIndex;
	}
}

}
