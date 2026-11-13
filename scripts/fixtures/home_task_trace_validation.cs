using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.HomeTaskUI;
using Zjcs.Offline.HomeHudUI;
using Zjcs.Official.WorldUI;
using TaskUI=Zjcs.Official.HomeTaskUI;

public sealed partial class HomeCurrencyWindowsNativeRunner
{
    TraceFactory traceFactory;
    TraceServices traceServices;
    TaskUI.ITaskHandler traceOwner;
    TaskRowProfileHost traceRowHost;
    void ConfigureTaskTraceNative()
    {
        traceFactory=new TraceFactory();taskRowTraceFactory=traceFactory;
        taskRowTraceSetup=(player,host)=>{
            traceServices=new TraceServices{Player=player};traceRowHost=host;
            traceOwner=new TaskUI.HomeTaskTraceHandler(traceServices);host.TraceHandlerOverride=traceOwner;
        };
        taskRowTraceVerifier=VerifyNativeTaskTrace;
        taskRowTraceFinalCheck=()=>{
            Require(traceServices.Errors.Count==0&&traceFactory.Targets.All(target=>target.FiniCalls==1),"all original trace targets released exactly once without observer exceptions");
            Require(Field<HashSet<Action>>(traceOwner.OnTraceStart,"mActionSet").Count==0&&Field<HashSet<Action>>(traceOwner.OnTraceEnd,"mActionSet").Count==0
                &&Field<HashSet<Action<long>>>(traceOwner.OnTraceCancel,"mActionSet").Count==0,"original destroyed task components unsubscribe all three trace events");
            Debug.Log("HOME_TASK_TRACE_RELEASE_PASSED: live target released on actual claim/removal, all controlled trace targets finalized exactly once, and original task components remove all three owner-event subscriptions.");
        };
    }
    IEnumerator VerifyNativeTaskTrace(TaskUI.TaskListItem row)
    {
        CheckTaskTraceOwnership();
        rowNavigationHost.Map=10;row.Refresh(traceServices.Player.GetTaskData(1005));yield return null;
        var state=Field<WorldStateSwitcher<TaskUI.TaskListItem.TaskStatus>>(row,"mStateSwitcher");
        var start=Field<Button>(row,"mTraceBtn");var cancel=Field<Button>(row,"mCanclTraceBtn");
        Require(state.State==TaskUI.TaskListItem.TaskStatus.NotTracing&&start.gameObject.activeInHierarchy,"real same-map task exposes original trace button");
        int tips=traceServices.Messages.Count;
        Click(start);yield return null;
        var first=traceFactory.Last;
        Require(traceOwner.CurrentTraceData==first&&first.TaskId==1005&&first.Map==10&&first.Target==0&&first.Kind=="region"
            &&state.State==TaskUI.TaskListItem.TaskStatus.Tracing&&cancel.gameObject.activeInHierarchy,"native pointer creates original regional trace and changes original row state");
        Require(traceServices.Messages.Skip(tips).SequenceEqual(new[]{"task_start_trace_target_tip"}),"native trace emits original start text key once");
        RequireTaskTextOnScreen(row);Capture("home-task-tracing-native.png");
        Click(cancel);yield return null;
        Require(first.FiniCalls==1&&traceOwner.CurrentTraceData==null&&state.State==TaskUI.TaskListItem.TaskStatus.NotTracing,"original cancel pointer disposes target and restores trace button");
        Click(start);yield return null;first=traceFactory.Last;tips=traceServices.Messages.Count;
        traceRowHost.NotifyPosition(10,1);yield return null;
        Require(first.FiniCalls==1&&traceFactory.Last!=first&&traceOwner.CurrentTraceData==traceFactory.Last&&traceServices.Messages.Count==tips,"original region observer replaces trace without start/stop tips");
        first=traceFactory.Last;first.End();yield return null;
        Require(first.FiniCalls==1&&traceOwner.CurrentTraceData==null&&state.State==TaskUI.TaskListItem.TaskStatus.NotTracing,"target end reaches original component and row, which stops and disposes the target");
        Capture("home-task-trace-ended-native.png");
        // Leave a live target for the existing real claim/removal exercise to destroy.
        Click(start);yield return null;first=traceFactory.Last;
        Require(traceOwner.CurrentTraceData==first,"live trace remains owned until original row removal");
        taskRowTraceVerifier=null;
        Debug.Log("HOME_TASK_TRACE_NATIVE_PASSED: actual character task and native trace/cancel pointers, original TaskHandler/TraceTaskComponent/TaskListItem events and switchers, silent region replacement and completion disposal. Entity/direction visualization is an explicit controlled factory; actual map composition remains pending.");
    }
    void CheckTaskTraceOwnership()
    {
        var service=new TraceServices{Player=traceServices.Player};TaskUI.ITaskHandler owner=new TaskUI.HomeTaskTraceHandler(service);
        var order=new List<string>();owner.OnTraceStart.AddListener(()=>order.Add("start"));owner.OnTraceCancel.AddListener(id=>order.Add("cancel:"+id));
        owner.StartTrace(1005);var first=traceFactory.Last;
        owner.StopTrace(999);Require(owner.CurrentTraceData==first&&first.FiniCalls==0,"mismatched stop preserves original target");
        owner.StartTrace(1005);Require(first.FiniCalls==1&&order.SequenceEqual(new[]{"start","cancel:1005","start"}),"original start cancels previous before publishing the next start");
        first=traceFactory.Last;int ends=0;owner.OnTraceEnd.AddListener(()=>ends++);first.End();
        Require(ends==1&&owner.CurrentTraceData==first&&first.FiniCalls==0,"owner end event retains target until its UI subscriber stops it");
        int events=order.Count;owner.StopTrace(1005);Require(first.FiniCalls==1&&owner.CurrentTraceData==null&&order.Count==events,"stop releases a matching target without cancellation event");
        owner.StartTrace(1005,false);first=traceFactory.Last;owner.StartTrace(long.MaxValue,false);
        Require(first.FiniCalls==1&&owner.CurrentTraceData==null&&service.Invalid.Count==1&&service.Invalid[0]==long.MaxValue,"invalid new task still cancels the old trace and reports missing data");
        var map=new TaskUI.HomeTaskMapTraceHandler(service,owner);
        owner.TraceWhenEnterMap(1000);owner.TraceWhenEnterMap(1005);
        Require(service.Replaced.SequenceEqual(new[]{(1000L,1005L)})&&owner.WaitTraceTaskId==1005,"pending trace replacement warns and retains the new request");
        service.MapInitialized=false;map.OnActivate();Require(owner.WaitTraceTaskId==1005&&owner.CurrentTraceData==null,"uninitialized map retains pending request");
        service.MapInitialized=true;var player=service.Player;service.Player=null;map.OnActivate();Require(owner.WaitTraceTaskId==1005,"missing player retains pending request");service.Player=player;
        service.Pin=player.GetTaskData(1000);map.OnActivate();
        Require(service.Mismatches.Count==1&&owner.CurrentTraceData?.TaskId==1005&&owner.WaitTraceTaskId==null,"pin mismatch warns but original activation still starts requested trace and clears pending");owner.StopTrace(1005);
        owner.TraceWhenEnterMap(0);map.OnActivate();Require(owner.WaitTraceTaskId==0&&owner.CurrentTraceData==null,"nonpositive pending request remains unconsumed");owner.CleanWaitTraceTask();
        owner.TraceWhenEnterMap(1005);service.Pin=player.GetTaskData(1005);map.OnActivate();Require(owner.CurrentTraceData?.TaskId==1005&&owner.WaitTraceTaskId==null&&service.Mismatches.Count==1,"matching pin starts pending trace without extra mismatch");owner.CancelTrace();
        var data=OfflineHomeMainlineTaskData.LoadDefault();var navigation=new OfflineHomeTaskNavigation(data,rowNavigationHost,_=>{},traceFactory);
        var entity=(TraceTarget)navigation.TraceTarget(1025).StartTrace(1025);
        Require(entity.Kind=="entity"&&entity.TaskId==1025&&entity.Map==10&&entity.Target==77,"original tag lookup initializes entity trace with exact supplied map/logic ID");entity.Fini();
        bool rejected=false;try{new OfflineHomeTaskNavigation(data,rowNavigationHost,_=>{}).TraceTarget(1005).StartTrace(1005);}catch(InvalidOperationException){rejected=true;}
        Require(rejected,"missing actual trace factory is explicit");
        var errors=new List<Exception>();var action=new TaskUI.HomeTaskTraceEvent(errors.Add);int count=0;Action listener=()=>count++;action.AddListener(listener);action.AddListener(listener);action.AddListener(()=>throw new InvalidOperationException("controlled listener failure"));action.Invoke();
        Require(count==1&&errors.Count==1,"original SafeAction deduplicates and isolates throwing listeners");action.RemoveListener(listener);action.ClearListener();action.Invoke();Require(count==1&&errors.Count==1,"original SafeAction remove and clear release listeners");
        var snapshot=new TaskUI.HomeTaskTraceEvent(errors.Add);Action late=()=>count+=100;Action mutate=()=>{count++;snapshot.RemoveListener(late);};snapshot.AddListener(mutate);snapshot.AddListener(late);snapshot.Invoke();Require(count==102,"original event snapshot survives mutation during invocation");snapshot.Invoke();Require(count==103,"next event reflects listener removal");
        Require(service.Errors.Count==0,"trace lifecycle has no unhandled subscriber error");
        Debug.Log("HOME_TASK_TRACE_OWNERSHIP_PASSED: original replacement/cancel/stop/end distinctions, invalid task, pending replacement, map/player readiness, pin mismatch and matching pin, nonpositive pending ID, exact region/tag target initialization, missing factory and original safe-event semantics.");
    }
    sealed class TraceServices:TaskUI.IHomeTaskTraceServices
    {
        public TaskUI.IHomeTaskPlayer Player{get;set;}public bool MapInitialized{get;set;}=true;public TaskUI.PlayerTask Pin;
        public readonly List<string> Messages=new List<string>();public readonly List<long> Invalid=new List<long>();public readonly List<(long,long)> Replaced=new List<(long,long)>();public readonly List<(long,long?)> Mismatches=new List<(long,long?)>();public readonly List<Exception> Errors=new List<Exception>();
        // A controlled current-pin port; the live pin selection owner is not replaced here.
        public TaskUI.PlayerTask GetCurrentPinTask()=>Pin;
        public void Broadcast(string key)=>Messages.Add(key);public void InvalidTask(long id,int?classId)=>Invalid.Add(id);
        public void PendingTraceReplaced(long before,long after)=>Replaced.Add((before,after));public void PendingTracePinMismatch(long waiting,long?pin)=>Mismatches.Add((waiting,pin));public void ReportException(Exception error)=>Errors.Add(error);
    }
    sealed class TraceFactory:TaskUI.IHomeTaskTraceFactory
    {
        public TraceTarget Last;
        public readonly List<TraceTarget> Targets=new List<TraceTarget>();
        TraceTarget Create(string kind){Last=new TraceTarget(kind);Targets.Add(Last);return Last;}
        public TaskUI.IHomeTaskRegionTrace CreateRegionTrace()=>Create("region");
        public TaskUI.IHomeTaskEntityTrace CreateEntityTrace()=>Create("entity");
    }
    sealed class TraceTarget:TaskUI.IHomeTaskRegionTrace,TaskUI.IHomeTaskEntityTrace
    {
        public long TaskId{get;private set;}public int Map,Target,FiniCalls;public readonly string Kind;Action ended;
        public TraceTarget(string kind){Kind=kind;}public void Init(long id,int map,int target){TaskId=id;Map=map;Target=target;}
        public void Fini(){FiniCalls++;ended=null;}public void SetTraceEndCallback(Action callback)=>ended=callback;public void End()=>ended?.Invoke();
    }
}
