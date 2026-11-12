// Selected official pinned-task UI; explicit local service bindings only.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.ActivityBattleUI;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeTaskUI
{
using System;
using System.Collections.Generic;
using UnityEngine;

public class TaskGoal : UIBase
{
	public IHomeTaskServices Services { get; set; }
	public List<TaskConditionItemBase> ConditionItems;

	public GameObject ContentConditonsContent;

	public MultiLanguageText ProgressText;

	public GameObject ResourceGo;

	public Transform ItemSlot;

	public WorldStateSwitcher<bool> ResourceEnoughSwitcher;

	private List<ICondition> mConditions;

	private Dictionary<int, long> mItems;

	private PreRequirement mPreRequirement;

	public Action OnRequirementAchieved;

	private ICondition.DataHub mDataHub = new ICondition.DataHub();

	private PlayerTask mTaskData;

	private bool mConditionRegistered;

	private List<ItemIcon> mIconCache = new List<ItemIcon>();

		private IItemIconManager ItemIconManager => Services.ItemIconManager;

		private IHomeTaskGlobalColors GlobalColors => Services.GlobalColors;

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		Services.Player.OnItemCountUpdate += Player_OnItemCountUpdate;
		PopulateConditions();
		PopulateItems();
		RegisterCondition();
		CheckAndFireOnAchieved();
	}

	protected override void OnDisableAndInited()
	{
		RecycleItems();
		IHomeTaskPlayer mainPlayer = Services.Player;
		if (mainPlayer != null)
		{
			mainPlayer.OnItemCountUpdate -= Player_OnItemCountUpdate;
		}
		DeregisterCondition();
		base.OnDisableAndInited();
	}

	private void Player_OnItemCountUpdate(PlayerItemDataWrap itemData, long preCount, AwardSourceData sourceData)
	{
		if (mItems != null && mPreRequirement != null)
		{
			if (mItems.ContainsKey(itemData.Info.ClassId))
			{
				RefreshItemIconGotStatus();
			}
			CheckAndFireOnAchieved();
			UpdateResourceState();
		}
	}

	public void Init(PlayerTask taskData, PreRequirement requirement, Action onAchieved)
	{
		DoInit(taskData, null, requirement.Conditions, requirement.Items, onAchieved);
	}

	public void Init(PlayerTask task, List<ICondition> conditions = null, Dictionary<int, long> items = null)
	{
		DoInit(task, null, conditions, items);
	}

	private void DoInit(PlayerTask taskData, PreRequirement requirement = null, List<ICondition> conditions = null, Dictionary<int, long> items = null, Action onAchieved = null)
	{
		InitWrapper(() =>
		{
			mTaskData = taskData;
			mPreRequirement = requirement;
			mConditions = conditions;
			mItems = items;
			OnRequirementAchieved = onAchieved;
			IHomeTaskPlayer mainPlayer = Services.Player;
			mDataHub.Supply(ConditionDataType.Player, mainPlayer);
			mDataHub.Supply(ConditionDataType.TaskData, taskData.DataWrap);
		});
	}

	private void RegisterCondition()
	{
		if (mConditions == null || mConditions.Count == 0)
		{
			return;
		}
		if (mConditionRegistered)
		{
			Debug.LogError("Path: " + Services.GetFullName(gameObject) + " RegisterCondition Error");
			return;
		}
		mConditionRegistered = true;
		foreach (ICondition mCondition in mConditions)
		{
			Services.Conditions.Register(mCondition, OnConditionUpdate);
		}
	}

	private void DeregisterCondition()
	{
		if (!mConditionRegistered)
		{
			return;
		}
		mConditionRegistered = false;
		foreach (ICondition mCondition in mConditions)
		{
			Services.Conditions.Deregister(mCondition, OnConditionUpdate);
		}
	}

	private void OnConditionUpdate()
	{
		PopulateConditions();
		CheckAndFireOnAchieved();
	}

	private void PopulateConditions()
	{
		int num = 0;
		if (mConditions != null)
		{
			ContentConditonsContent.SetActive(value: true);
			foreach (ICondition mCondition in mConditions)
			{
				TaskConditionItemBase taskConditionItemBase = ConditionItems[num++];
				taskConditionItemBase.gameObject.SetActive(value: true);
				taskConditionItemBase.Populate(mCondition, mDataHub);
			}
			for (int i = num; i < ConditionItems.Count; i++)
			{
				ConditionItems[i].gameObject.SetActive(value: false);
			}
		}
		else
		{
			ContentConditonsContent.SetActive(value: false);
		}
		if (!(ProgressText != null))
		{
			return;
		}
		if (mPreRequirement == null && mTaskData.Info().ShowProgressBar && mConditions != null && mConditions.Count == 1)
		{
			ProgressText.gameObject.SetActive(value: true);
			ICondition condition = mConditions[0];
			(long, long) progress = condition.GetProgress(mDataHub);
			if (condition.IsMet(mDataHub))
			{
				ProgressText.Format(CultureNumberArg.Compact(progress.Item1), CultureNumberArg.Compact(progress.Item2));
			}
			else
			{
				ProgressText.Format(new NumberWithColorArg(progress.Item1, "#" + ColorUtility.ToHtmlStringRGB(GlobalColors.Value.LackColor)), CultureNumberArg.Compact(progress.Item2));
			}
		}
		else
		{
			ProgressText.gameObject.SetActive(value: false);
		}
	}

	private void CheckAndFireOnAchieved()
	{
		if (mPreRequirement != null && mPreRequirement.IsAchieved(Services.Player, mDataHub))
		{
			OnRequirementAchieved?.Invoke();
		}
	}

	private void RecycleItems()
	{
		IItemIconManager itemIconManager = ItemIconManager;
		if (itemIconManager != null)
		{
			for (int i = 0; i < mIconCache.Count; i++)
			{
				ItemIcon itemIcon = mIconCache[i];
				itemIconManager.RecycleIcon(itemIcon);
			}
			mIconCache.Clear();
		}
	}

	private void PopulateItems()
	{
		if (ResourceGo == null)
		{
			return;
		}
		RecycleItems();
		bool flag = mItems != null && mItems.Count != 0;
		ResourceGo.SetActive(flag);
		if (!flag)
		{
			return;
		}
		IItemIconManager itemIconManager = ItemIconManager;
		foreach (KeyValuePair<int, long> mItem in mItems)
		{
			ItemIcon icon = itemIconManager.GetIcon(mItem.Key, ItemSlot);
			icon.SetClickPopupInfoDialog().SetCenterHintContent(show: true);
			if (!mTaskData.IsCompletedState())
			{
				icon.SetRequireCount(mItem.Value);
			}
			bool show = Services.Player.GetItemAmount(mItem.Key) >= mItem.Value;
			icon.SetAttachment(ItemIconAttachmentType.GotTip, show);
			mIconCache.Add(icon);
		}
		UpdateResourceState();
	}

	private void UpdateResourceState()
	{
		bool state = ItemIsEnough();
		ResourceEnoughSwitcher.SetState(state);
	}

	private bool ItemIsEnough()
	{
		if ((mItems?.Count ?? 0) < 0)
		{
			return true;
		}
		return Services.Player.HasItems(mItems);
	}

	private void RefreshItemIconGotStatus()
	{
		foreach (ItemIcon item in mIconCache)
		{
			ItemInfo myInfo = item.myInfo;
			if (mItems.TryGetValue(myInfo.ClassId, out var value))
			{
				long itemAmount = Services.Player.GetItemAmount(myInfo.ClassId);
				item.SetAttachment(ItemIconAttachmentType.GotTip, itemAmount >= value);
			}
		}
	}
}
}
