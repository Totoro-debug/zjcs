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
using UnityEngine.UI;

public class TaskListItem : UIBase, ITraceTaskHandler
{
	public IHomeTaskServices Services { get; set; }
	public enum TaskStatus
	{
		Processing,
		Completed,
		Jump,
		NotTracing,
		Tracing
	}

	public enum WorldMapProgressTaskStatus
	{
		None,
		Locked,
		Recommend,
		TimeLocked
	}

	[SerializeField]
	private Button mGMBtn;

	[SerializeField]
	private MultiLanguageText myTaskTitle;

	public TaskGoal TaskGoal;

	public GameObject RewardSection;

	public Transform RewardSlot;

	[SerializeField]
	private Vector3 mCountTextScale = Vector3.one;

	[SerializeField]
	private Button mClaimBtn;

	[SerializeField]
	private Button mJumpBtn;

	[SerializeField]
	private Button mTraceBtn;

	[SerializeField]
	private Button mCanclTraceBtn;

	[SerializeField]
	private CountdownText mCountdown;

	[SerializeField]
	private WorldStateSwitcher<TaskStatus> mStateSwitcher;

	[SerializeField]
	private WorldStateSwitcher<WorldMapProgressTaskStatus> mMapTaskStateSwitcher;

	[SerializeField]
	private TaskListRecommendCombatRatingItem mRecommendCombatRatingItem;

	[SerializeField]
	private ShowRemainTimeInfo mTaskLimitRemainTime;

	[SerializeField]
	private MapRegionLimitStatusRefreshHelper mLimitStatusRefreshHelper;

	private ICondition.DataHub mDataHub = new ICondition.DataHub();

	private PreRequirement mPreRequirement;

	private bool mPreSatisfied;

	private List<ItemIcon> mRewardItems = new List<ItemIcon>();

	private TraceTaskComponent mTraceTaskComponent;

	public PlayerTask MyData => Services.Player?.GetTaskData(TaskId);

	public long TaskId { get; private set; }

		private IMapInputSupportHandlerProxy mMapInput => Services.mMapInput;

		private INavigateManager NavigateManager => Services.NavigateManager;

		private ITaskHandler TaskHandler => Services.TaskHandler;

		private IItemIconManager ItemIconManager => Services.ItemIconManager;

		private IMyMapPositionHandlerProxy MyMapPositionHandler => Services.MyMapPositionHandler;

	private TraceTaskComponent TraceTaskComponent
	{
		get
		{
			if (mTraceTaskComponent == null)
			{
				mTraceTaskComponent = new TraceTaskComponent(TaskHandler, this);
			}
			return mTraceTaskComponent;
		}
	}

	private void SetPreRequirement(PreRequirement preRequirement)
	{
		UnregisterPreRequirement();
		mPreRequirement = preRequirement;
		RegisterPreRequirement();
	}

	protected override void Start()
	{
		base.Start();
		mJumpBtn.onClick.AddListener(OnJumpBtnClick);
		mClaimBtn.onClick.AddListener(OnClaim);
		if (mTraceBtn != null)
		{
			mTraceBtn.onClick.AddListener(OnTraceClicked);
		}
		if (mCanclTraceBtn != null)
		{
			mCanclTraceBtn.onClick.AddListener(OnCanclTraceClicked);
		}
		GetComponent<Button>().onClick.AddListener(OnPopUpTaskDetailClick);
		if ((bool)mGMBtn)
		{
			mGMBtn.gameObject.SetActive(Services.Player.Gm);
			mGMBtn.onClick.AddListener(GmFinishTask);
		}
		TraceTaskComponent.Init();
	}

	protected override void OnDestroy()
	{
		TraceTaskComponent.StopTrace(TaskId);
		TraceTaskComponent.Fini();
		mTraceTaskComponent = null;
		TaskId = 0L;
		base.OnDestroy();
	}

	private void OnPopUpTaskDetailClick()
	{
		Services.OpenTaskDetail(MyData);
	}

	private void GmFinishTask()
	{
		Services.DebugCompleteTask(new List<long> { MyData.TaskId });
	}

	protected override void OnEnableAndInited()
	{
		base.OnEnableAndInited();
		mLimitStatusRefreshHelper.Services = Services;
		IHomeTaskPlayer mainPlayer = Services.Player;
		mainPlayer.OnItemCountUpdate += Player_OnItemCountUpdate;
		mainPlayer.OnCombatRatingChanged += Player_OnCombatRatingChanged;
		mainPlayer.OnSubRankUpdate += Player_OnSubRankChanged;
		mainPlayer.OnMapStatusUpdate += MainPlayer_OnMapStatusUpdate;
		MyMapPositionHandler.OnMeRegionUpdate += new Action<int, int>(MapRoot_OnMeRegionUpdate);
		Services.OnTeamUpdate.AddListener(OnTeamUpdate);
		WorldMapProgressCondition taskMapProgressCondition = GetTaskMapProgressCondition();
		if (taskMapProgressCondition != null)
		{
			MapRegionInfo mapRegionInfo = Services.GetMapRegion(taskMapProgressCondition.MapClassId, taskMapProgressCondition.Region);
			if (mapRegionInfo.IsLimited(Services.Player.Level, Services.PlanesRank))
			{
				mLimitStatusRefreshHelper.OnStatusUpdate += new Action<MapRegionLimitType>(OnStatusUpdate);
			}
			mLimitStatusRefreshHelper.Start(mapRegionInfo);
		}
		PopulateReward();
		RefreshTask();
	}

	protected override void OnDisableAndInited()
	{
		if (Services != null)
		{
			Services.OnTeamUpdate.RemoveListener(OnTeamUpdate);
		}
		IHomeTaskPlayer mainPlayer = Services.Player;
		if (mainPlayer != null)
		{
			mainPlayer.OnItemCountUpdate -= Player_OnItemCountUpdate;
			mainPlayer.OnCombatRatingChanged -= Player_OnCombatRatingChanged;
			mainPlayer.OnSubRankUpdate -= Player_OnSubRankChanged;
			mainPlayer.OnMapStatusUpdate -= MainPlayer_OnMapStatusUpdate;
		}
		MyMapPositionHandler.OnMeRegionUpdate -= new Action<int, int>(MapRoot_OnMeRegionUpdate);
		mLimitStatusRefreshHelper.OnStatusUpdate -= new Action<MapRegionLimitType>(OnStatusUpdate);
		mLimitStatusRefreshHelper.Stop();
		UnregisterPreRequirement();
		RecycleItems();
		base.OnDisableAndInited();
	}

	private void MainPlayer_OnMapStatusUpdate(int mapClassId, int region, MapRegionStatus preStatus, MapRegionStatus postStatus)
	{
		if (GetTaskMapProgressCondition() != null)
		{
			RefreshInteraction();
		}
	}

	private void MapRoot_OnMeRegionUpdate(int arg1, int arg2)
	{
		if (mTraceTaskComponent != null && mTraceTaskComponent.IsInTracing)
		{
			TraceTaskComponent.StartTrace(showTip: false);
		}
	}

	private void Player_OnItemCountUpdate(PlayerItemDataWrap itemData, long preCount, AwardSourceData sourceData)
	{
		if (mPreRequirement != null && mPreRequirement.IsAchieved(Services.Player, mDataHub) != mPreSatisfied)
		{
			RefreshTask();
		}
	}

	public void Init(PlayerTask taskData)
	{
		InitWrapper(() =>
		{
			TaskId = taskData.TaskId;
			TaskInfo taskInfo = taskData?.Info();
			if (taskInfo != null && taskInfo.IsAllowTrace && taskInfo.GetTaskTraceTarget() != null)
			{
				TraceTaskComponent.SetTaskId(TaskId);
			}
		});
	}

	public void Refresh(PlayerTask playerTask)
	{
		if (TaskId == playerTask.TaskId)
		{
			RefreshTask();
		}
	}

	private void RefreshTask()
	{
		PlayerTask myData = MyData;
		mDataHub.Supply(ConditionDataType.Player, Services.Player);
		mDataHub.Supply(ConditionDataType.TaskData, myData.DataWrap);
		PopulateGoal();
		myTaskTitle.SetKey(Services.GetTaskTitleKey(myData.TaskClassId));
		RefreshInteraction();
		RewardSection.SetActive(myData.CanGetReward(Services.Player));
		TryRefreshCountdown();
	}

	private void PopulateGoal()
	{
		SetPreRequirement(null);
		ICondition finishCondition = MyData.Info().FinishCondition;
		if (MyData.IsHandInItemsTask())
		{
			TaskGoal.Init(MyData, null, MyData.GetNeedHandInItemsDict());
			return;
		}
		TaskGoal.Init(MyData, new List<ICondition> { finishCondition });
	}

	private void OnPreRequirementAchieved()
	{
		RefreshTask();
	}

	private void RecycleItems()
	{
		IItemIconManager itemIconManager = ItemIconManager;
		if (itemIconManager != null)
		{
			for (int i = 0; i < mRewardItems.Count; i++)
			{
				ItemIcon itemIcon = mRewardItems[i];
				itemIcon.ResetCountTextScale();
				itemIconManager.RecycleIcon(itemIcon);
			}
			mRewardItems.Clear();
		}
	}

	private void PopulateReward()
	{
		RecycleItems();
		MyData.InitRewardIconWithProbability(ItemIconManager, RewardSlot, mRewardItems);
		bool active = mRewardItems.Count > 0;
		RewardSection.SetActive(active);
	}

	private void RefreshInteraction()
	{
		TaskStatus currentSstatus = GetCurrentSstatus();
		mStateSwitcher.SetState(currentSstatus);
		RefreshMapTaskStatus();
	}

	private void Player_OnCombatRatingChanged(long arg1, long arg2)
	{
		RefreshMapTaskStatus();
	}

	private void Player_OnSubRankChanged(SubRank rank)
	{
		RefreshMapTaskStatus();
	}

	private void OnStatusUpdate(MapRegionLimitType limitType)
	{
		RefreshMapTaskStatus();
	}

	private WorldMapProgressCondition GetTaskMapProgressCondition()
	{
		TaskInfo taskInfo = MyData.Info();
		if (taskInfo == null)
		{
			return null;
		}
		if (taskInfo.FinishCondition.Type == ConditionType.WorldMapProgress && taskInfo.FinishCondition is WorldMapProgressCondition result)
		{
			return result;
		}
		return null;
	}

	private void RefreshMapTaskStatus()
	{
		if (RefreshTaskLimitStatus())
		{
			return;
		}
		StopTaskLimitRemainTime();
		WorldMapProgressCondition taskMapProgressCondition = GetTaskMapProgressCondition();
		if (taskMapProgressCondition != null)
		{
			MapRegionInfo mapRegionInfo = Services.GetMapRegion(taskMapProgressCondition.MapClassId, taskMapProgressCondition.Region);
			if (mapRegionInfo != null)
			{
				if (Services.CalcRankSuppression(Services.Player.SubRank, mapRegionInfo.SubRank) <= 0 && mapRegionInfo.IsLimited(Services.Player.Level, Services.PlanesRank))
				{
					mMapTaskStateSwitcher.SetState(WorldMapProgressTaskStatus.Locked);
					return;
				}
				if (mapRegionInfo.CombatRating > 0 && !MyData.CanFinish(Services.Player))
				{
					mRecommendCombatRatingItem.SetMapRegion(mapRegionInfo);
					mMapTaskStateSwitcher.SetState(WorldMapProgressTaskStatus.Recommend);
					return;
				}
			}
		}
		mMapTaskStateSwitcher.SetState(WorldMapProgressTaskStatus.None);
	}

	private bool RefreshTaskLimitStatus()
	{
		if (mTaskLimitRemainTime == null)
		{
			return false;
		}
		if (!Services.TryCheckTaskLimit(MyData.TaskClassId, Services.Player, out var result))
		{
			return false;
		}
		mMapTaskStateSwitcher.SetState(WorldMapProgressTaskStatus.TimeLocked);
		if (result.EndTime.HasValue)
		{
			mTaskLimitRemainTime.Init(result.EndTime.Value, 1f, result.LimitDesc, ETimeSpanFormatType.Normal, OnTaskLimitExpired);
		}
		else
		{
			mTaskLimitRemainTime.SetCustomText(result.LimitDesc);
		}
		return true;
	}

	private void StopTaskLimitRemainTime()
	{
		if (mTaskLimitRemainTime != null)
		{
			mTaskLimitRemainTime.Stop();
		}
	}

	private void OnTaskLimitExpired()
	{
		RefreshMapTaskStatus();
	}

	private void OnJumpBtnClick()
	{
		MyData.Navigate(mMapInput, NavigateManager, null);
	}

	private void OnClaim()
	{
		MyData.Submit(Services.Player, 1L);
	}

	private void OnTraceClicked()
	{
		TraceTaskComponent?.StartTrace();
	}

	private void OnCanclTraceClicked()
	{
		if (TryCanclTraceTarget())
		{
			RefreshInteraction();
		}
	}

	private bool TryCanclTraceTarget()
	{
		TaskInfo taskInfo = MyData?.Info();
		if (taskInfo != null && taskInfo.GetTaskTraceTarget() != null)
		{
			TraceTaskComponent?.CancelTrace();
			return true;
		}
		return false;
	}

	private void RegisterPreRequirement()
	{
		if (mPreRequirement == null)
		{
			return;
		}
		foreach (ICondition condition in mPreRequirement.Conditions)
		{
			Services.Conditions.Register(condition, RefreshOnPreSatisfiedChange);
		}
	}

	private void UnregisterPreRequirement()
	{
		if (mPreRequirement == null)
		{
			return;
		}
		foreach (ICondition condition in mPreRequirement.Conditions)
		{
			Services.Conditions.Deregister(condition, RefreshOnPreSatisfiedChange);
		}
		mPreRequirement = null;
	}

	private void RefreshOnPreSatisfiedChange()
	{
		if (mPreRequirement != null && mPreRequirement.IsAchieved(Services.Player, mDataHub) != mPreSatisfied)
		{
			RefreshTask();
		}
	}

	private TaskStatus GetCurrentSstatus()
	{
		if (MyData == null)
		{
			return TaskStatus.Processing;
		}
		if (MyData.CanFinish(Services.Player))
		{
			return TaskStatus.Completed;
		}
		if (MyData.CanNavigate() && !IsTaskLimitBlockingJump() && (mPreRequirement == null || mPreRequirement.IsAchieved(Services.Player, mDataHub)))
		{
			return TaskStatus.Jump;
		}
		TaskInfo taskInfo = MyData.Info();
		if (taskInfo.IsAllowTrace)
		{
			ITaskTraceTarget taskTraceTarget = taskInfo.GetTaskTraceTarget();
			if (taskTraceTarget != null)
			{
				if (TaskHandler.CurrentTraceData?.TaskId == TaskId)
				{
					return TaskStatus.Tracing;
				}
				if (taskTraceTarget.CanTrace())
				{
					return TaskStatus.NotTracing;
				}
			}
		}
		return TaskStatus.Processing;
	}

	private bool IsTaskLimitBlockingJump()
	{
		if (Services.TryCheckTaskLimit(MyData.TaskClassId, Services.Player, out var result))
		{
			return result.BlockJump;
		}
		return false;
	}

	private void OnTeamUpdate()
	{
		TryCanclTraceTarget();
	}

	private void TryRefreshCountdown()
	{
		if (!(mCountdown == null))
		{
			if (MyData.ShouldShowCountdown())
			{
				TimeSpan? remainTime = MyData.GetRemainTime();
				mCountdown.Show(remainTime.Value, OnTimeout);
			}
			else
			{
				mCountdown.Hide();
			}
		}
	}

	private void OnTimeout()
	{
		Services.RefreshLimitTimeTask(TaskId, invokeByPlayer: false);
		mCountdown.Hide();
	}

	public void OnStartTrace()
	{
		RefreshInteraction();
	}

	public void OnCancelTrace(long taskId)
	{
		RefreshInteraction();
	}

	public void OnTraceEnd()
	{
		TraceTaskComponent?.StopTrace(TaskId);
		RefreshInteraction();
	}
}
}
