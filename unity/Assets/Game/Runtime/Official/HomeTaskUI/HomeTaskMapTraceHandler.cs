// Original map activation trace sequence, with the real map owner supplying initialization.
using System;
namespace Zjcs.Official.HomeTaskUI
{
public sealed class HomeTaskMapTraceHandler
{
    readonly IHomeTaskTraceServices Host;
    readonly ITaskHandler TaskHandler;
    public HomeTaskMapTraceHandler(IHomeTaskTraceServices host,ITaskHandler taskHandler){Host=host??throw new ArgumentNullException(nameof(host));TaskHandler=taskHandler??throw new ArgumentNullException(nameof(taskHandler));}
public void OnActivate()
    {
        TryStartTrace();
    }
private void TryStartTrace()
    {
        if (Host.MapInitialized && Host.Player != null)
        {
            TryStartTraceWhenEnterMap();
        }
    }
private void TryStartTraceWhenEnterMap()
    {
        long valueOrDefault = TaskHandler.WaitTraceTaskId.GetValueOrDefault();
        if (valueOrDefault <= 0)
        {
            return;
        }
        PlayerTask currentPinTask = Host.GetCurrentPinTask();
        if (currentPinTask == null || currentPinTask.TaskId != valueOrDefault) { Host.PendingTracePinMismatch(valueOrDefault, currentPinTask?.TaskId); }
        TaskHandler.StartTrace(valueOrDefault);
        TaskHandler.CleanWaitTraceTask();
    }
}
}
