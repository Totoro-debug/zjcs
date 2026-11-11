// Original menu recommendation implementation; explicit data/resource bindings.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.HomePlayerPanelUI;
using Zjcs.Official.SkillsUI.FancyScrollView;
namespace Zjcs.Official.HomeFunctionMenuUI
{
using System;
using System.Collections.Generic;
using UnityEngine;

public class ActivityRecommendContext
{
	public IHomeMenuActivityServices Services;

	public int SelectedIndex = -1;

	public int PreSelectedIndex = -1;

	public bool DataUpdateCompleted;

	public Action<int> OnCellClicked;

	public Action OnCellPreSelected;

	public Dictionary<(ActivityEntryType, int), Queue<ActivityEntryBaseItem>> EntryItemPoolDict = new Dictionary<(ActivityEntryType, int), Queue<ActivityEntryBaseItem>>();

	public Transform PoolItemSlot;
}
}
