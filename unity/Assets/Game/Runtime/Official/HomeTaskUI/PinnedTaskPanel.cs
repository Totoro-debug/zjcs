// Selected official pinned-task UI; explicit local service bindings only.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.ActivityBattleUI;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeTaskUI
{
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PinnedTaskPanel : MonoBehaviour
{
	public IHomeTaskServices Services { get; set; }
	[SerializeField]
	private Transform myTaskItemSlot;

	[SerializeField]
	private TaskListItem myTaskItemTemplate;

	private Dictionary<long, TaskListItem> mCurrentTaskListItem = new Dictionary<long, TaskListItem>();

	private Coroutine mRefreshDailyTaskCo;

	protected void OnEnable()
	{
		Services.Player.OnTaskAdd += MainPlayer_OnTaskAdd;
		Services.Player.OnTaskUpdate += MainPlayer_OnTaskUpdate;
		Services.Player.OnTaskRemove += MainPlayer_OnTaskRemove;
		Services.Player.OnDebugTimeOffset += MainPlayer_OnDebugTimeOffset;
		Refresh();
		TryRefreshDailyTask();
	}

	protected void OnDisable()
	{
		if (Services.Player != null)
		{
			Services.Player.OnTaskAdd -= MainPlayer_OnTaskAdd;
			Services.Player.OnTaskUpdate -= MainPlayer_OnTaskUpdate;
			Services.Player.OnTaskRemove -= MainPlayer_OnTaskRemove;
			Services.Player.OnDebugTimeOffset -= MainPlayer_OnDebugTimeOffset;
		}
		if (mRefreshDailyTaskCo != null)
		{
			StopCoroutine(mRefreshDailyTaskCo);
			mRefreshDailyTaskCo = null;
		}
	}

	private void MainPlayer_OnTaskAdd(PlayerTaskDataWrap taskData)
	{
		TryAddTaskItem(taskData);
	}

	private void MainPlayer_OnTaskRemove(long taskId)
	{
		TryDeleteTaskItem(taskId);
	}

	private void MainPlayer_OnTaskUpdate(PlayerTaskDataWrap obj)
	{
		if (!obj.Pinned)
		{
			TryDeleteTaskItem(obj.TaskId);
		}
		else
		{
			TryAddTaskItem(obj);
		}
	}

	private void Refresh()
	{
		List<PlayerTask> list = Services.Player.PinTasks().ToList();
		list.Sort((PlayerTask a, PlayerTask b) => PlayerTaskPinComparer.PinComparer(Services.Player, a, b));
		foreach (TaskListItem taskItem in mCurrentTaskListItem.Values.ToList())
		{
			if (list.FindIndex((PlayerTask it) => it.TaskId == taskItem.TaskId) < 0)
			{
				DeleteTaskItem(taskItem);
			}
		}
		foreach (PlayerTask item in list)
		{
			TryAddTaskItem(item);
		}
	}

	private void TryAddTaskItem(PlayerTask taskData)
	{
		if (taskData != null && (taskData.Info().DisplayCondition == null || taskData.Info().DisplayCondition.IsMet(Services.Player)) && Services.Player.IsPinnedTask(taskData.TaskId))
		{
			long taskId = taskData.TaskId;
			if (mCurrentTaskListItem.TryGetValue(taskId, out var value))
			{
				value.Refresh(taskData);
			}
			else
			{
				AddTaskItem(taskData);
			}
		}
	}

	private void AddTaskItem(PlayerTask taskData)
	{
		TaskListItem taskListItem = Services.InstantiateTaskItem(myTaskItemTemplate.gameObject, myTaskItemSlot);
		taskListItem.gameObject.SetActive(value: true);
		taskListItem.Init(taskData);
		mCurrentTaskListItem.Add(taskData.TaskId, taskListItem);
	}

	private void TryDeleteTaskItem(long taskId)
	{
		if (mCurrentTaskListItem.TryGetValue(taskId, out var value))
		{
			DeleteTaskItem(value);
		}
	}

	private void DeleteTaskItem(TaskListItem taskItem)
	{
		mCurrentTaskListItem.Remove(taskItem.TaskId);
		UnityEngine.Object.Destroy(taskItem.gameObject);
	}

	private IEnumerator DoRefreshDailyTask()
	{
		while (true)
		{
			Services.RefreshAllTasks(invokeByPlayer: false);
			DateTime dateTime = Services.GetDateTime();
			double totalSeconds = (Services.GetDailyResetTimeFromDate(dateTime).AddDays(1.0) - dateTime).TotalSeconds;
			yield return new WaitForSeconds((float)totalSeconds);
		}
	}

	private void TryRefreshDailyTask()
	{
		if (mRefreshDailyTaskCo != null)
		{
			StopCoroutine(mRefreshDailyTaskCo);
		}
		mRefreshDailyTaskCo = StartCoroutine(DoRefreshDailyTask());
	}

	private void MainPlayer_OnDebugTimeOffset(long obj)
	{
		Services.RefreshAllTasks(invokeByPlayer: false);
		TryRefreshDailyTask();
	}
}
}
