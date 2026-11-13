using System;
namespace Zjcs.Official.HomeTaskUI
{


public interface ITaskTraceMapEntity
{
    long TaskId { get; }

    void Fini();

    void SetTraceEndCallback(Action callback);
}

public interface IHomeTaskTraceServices
{
    IHomeTaskPlayer Player {get;}
    bool MapInitialized {get;}
    PlayerTask GetCurrentPinTask();
    void Broadcast(string key);
    void InvalidTask(long taskId,int? taskClassId);
    void PendingTraceReplaced(long previous,long next);
    void PendingTracePinMismatch(long waiting,long? pinned);
    void ReportException(Exception error);
}
public interface IHomeTaskRegionTrace : ITaskTraceMapEntity { void Init(long taskId,int mapClassId,int region); }
public interface IHomeTaskEntityTrace : ITaskTraceMapEntity { void Init(long taskId,int mapClassId,int logicId); }
public interface IHomeTaskTraceFactory
{
    IHomeTaskRegionTrace CreateRegionTrace();
    IHomeTaskEntityTrace CreateEntityTrace();
}
}
