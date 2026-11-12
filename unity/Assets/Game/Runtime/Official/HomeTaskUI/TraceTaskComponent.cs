// Selected official pinned-task UI; explicit local service bindings only.
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.ActivityBattleUI;
using MultiLanguageText=Zjcs.Official.WorldUI.WorldMultiLanguageText;
namespace Zjcs.Official.HomeTaskUI
{
public class TraceTaskComponent
{
	private ITraceTaskHandler mTraceTaskHandler;

	private ITaskHandler mTaskHandler;

	private bool mIsInTracing;

	public bool IsInTracing => mIsInTracing;

	public long TaskId { get; private set; }

	public TraceTaskComponent(ITaskHandler taskHandler, ITraceTaskHandler traceTaskHandler)
	{
		mTraceTaskHandler = traceTaskHandler;
		mTaskHandler = taskHandler;
	}

	public void Init()
	{
		mTaskHandler.OnTraceStart.AddListener(OnTraceStart);
		mTaskHandler.OnTraceEnd.AddListener(OnTraceEnd);
		mTaskHandler.OnTraceCancel.AddListener(OnTraceCancel);
	}

	public void Fini()
	{
		if (mTaskHandler != null)
		{
			mTaskHandler.OnTraceStart.RemoveListener(OnTraceStart);
			mTaskHandler.OnTraceEnd.RemoveListener(OnTraceEnd);
			mTaskHandler.OnTraceCancel.RemoveListener(OnTraceCancel);
		}
		mTraceTaskHandler = null;
		TaskId = 0L;
	}

	public void SetTaskId(long taskId)
	{
		TaskId = taskId;
	}

	public void StartTrace(bool showTip = true)
	{
		mTaskHandler.StartTrace(TaskId, showTip);
	}

	public void CancelTrace(bool showTip = true)
	{
		mTaskHandler.CancelTrace(showTip);
	}

	public void StopTrace(long taskId)
	{
		if (mTaskHandler != null)
		{
			mTaskHandler.StopTrace(taskId);
		}
	}

	private void OnTraceStart()
	{
		if (mTaskHandler.CurrentTraceData?.TaskId == TaskId)
		{
			mTraceTaskHandler.OnStartTrace();
			mIsInTracing = true;
		}
	}

	private void OnTraceEnd()
	{
		if (mTaskHandler.CurrentTraceData?.TaskId == TaskId)
		{
			mTraceTaskHandler.OnTraceEnd();
			mIsInTracing = false;
		}
	}

	private void OnTraceCancel(long taskId)
	{
		if (taskId == TaskId)
		{
			mTraceTaskHandler.OnCancelTrace(taskId);
			mIsInTracing = false;
		}
	}
}
}
