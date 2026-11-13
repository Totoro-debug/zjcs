// Original TaskHandler with explicit local player/text/log bindings.
using System;
namespace Zjcs.Official.HomeTaskUI
{

public sealed class HomeTaskTraceHandler : ITaskHandler
{
    readonly IHomeTaskTraceServices Host;
    public HomeTaskTraceHandler(IHomeTaskTraceServices host)
    {
        Host=host??throw new ArgumentNullException(nameof(host));
        OnTraceStart=new HomeTaskTraceEvent(host.ReportException);
        OnTraceEnd=new HomeTaskTraceEvent(host.ReportException);
        OnTraceCancel=new HomeTaskPlayerEvent<long>(host.ReportException);
    }

    private ITaskTraceMapEntity mCurrentTraceData;

    private long? mWaitTraceTaskId;

    public HomeTaskTraceEvent OnTraceStart { get; }

    public HomeTaskTraceEvent OnTraceEnd { get; }

    public HomeTaskPlayerEvent<long> OnTraceCancel { get; }

    ITaskTraceMapEntity ITaskHandler.CurrentTraceData => mCurrentTraceData;

    long? ITaskHandler.WaitTraceTaskId => mWaitTraceTaskId;

    public void StartTrace(long taskId, bool showTip)
    {
        ((ITaskHandler)this).CancelTrace(showTip);
        PlayerTask taskData = Host.Player.GetTaskData(taskId);
        TaskInfo taskInfo = taskData?.Info();
        ITaskTraceTarget taskTraceTarget = null;
        if (taskInfo != null && taskInfo.IsAllowTrace)
        {
            taskTraceTarget = taskInfo.GetTaskTraceTarget();
        }
        if (taskTraceTarget != null)
        {
            mCurrentTraceData = taskTraceTarget.StartTrace(taskId);
            mCurrentTraceData.SetTraceEndCallback(OnTraceEndCallback);
            if (showTip)
            {
                Host.Broadcast("task_start_trace_target_tip");
            }
            ((ITaskHandler)this).OnTraceStart?.Invoke();
            return;
        }
        Host.InvalidTask(taskId, taskData?.TaskClassId);
    }

    public void StopTrace(long taskId)
    {
        if (mCurrentTraceData != null && mCurrentTraceData.TaskId == taskId)
        {
            mCurrentTraceData.Fini();
            mCurrentTraceData = null;
        }
    }

    public void CancelTrace(bool showTip)
    {
        if (mCurrentTraceData != null)
        {
            long taskId = mCurrentTraceData.TaskId;
            mCurrentTraceData.Fini();
            mCurrentTraceData = null;
            if (showTip)
            {
                Host.Broadcast("task_stop_trace_target_tip");
            }
            ((ITaskHandler)this).OnTraceCancel?.Invoke(taskId);
        }
    }

    private void OnTraceEndCallback()
    {
        ((ITaskHandler)this).OnTraceEnd?.Invoke();
    }

    public void TraceWhenEnterMap(long taskId)
    {
        if (mWaitTraceTaskId.HasValue) { Host.PendingTraceReplaced(mWaitTraceTaskId.Value, taskId); }
        mWaitTraceTaskId = taskId;
    }

    public void CleanWaitTraceTask()
    {
        mWaitTraceTaskId = null;
    }
}
}
