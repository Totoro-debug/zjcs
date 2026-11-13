"""Migrate original task trace ownership, events and map-activation ordering."""
import argparse,hashlib,json,re,shutil
from pathlib import Path
from official_apk import ROOT
from prepare_home_task_map_data import CODE,method,sha
from prepare_home_task_player import normalize,without_logging

DEST=ROOT/'unity/Assets/Game/Runtime/Official/HomeTaskUI'

def build_runtime():
 inputs={};outputs={}
 def read(relative):
  path=CODE/relative;inputs[path.relative_to(ROOT).as_posix()]=sha(path);return path.read_text('utf-8-sig')
 def write(name,text):
  path=DEST/name;path.write_text(normalize(text),'utf8',newline='\n');meta=Path(str(path)+'.meta')
  if not meta.exists():meta.write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(('zjcs-official:'+path.relative_to(ROOT/'unity').as_posix()).encode()).hexdigest()+'\nMonoImporter:\n  serializedVersion: 2\n  externalObjects: {}\n','utf8',newline='\n')
  for item in (path,meta):outputs[item.relative_to(ROOT).as_posix()]=sha(item)
 raw=read('framework-firstpass-source/Framework/SafeAction.cs');raw=raw[:raw.index('public class SafeAction<T1>')]
 event='''using System;
using System.Collections.Generic;
namespace Zjcs.Official.HomeTaskUI
{
// Original SafeAction without arguments; only the log sink is injected.
public sealed class HomeTaskTraceEvent
{
    readonly HashSet<Action> mActionSet=new HashSet<Action>();
    readonly Stack<List<Action>> mStack=new Stack<List<Action>>();
    readonly Action<Exception> reportException;
    public HomeTaskTraceEvent(Action<Exception> reportException){this.reportException=reportException??throw new ArgumentNullException(nameof(reportException));}
'''
 for signature in ('private List<Action> PopList()','private void PushList(List<Action> list)','public void AddListener(Action action)','public void RemoveListener(Action action)','public void ClearListener()','public void Invoke()'):
  text=method(raw,signature)
  if signature=='public void Invoke()':
   text=without_logging(text,1)
   text,count=re.subn(r'catch \(Exception exception\)\s*\{\s*\}','catch (Exception exception) { reportException(exception); }',text);assert count==1
  event+=text+'\n'
 write('HomeTaskTraceEvent.cs',event+'}\n}\n')
 raw=read('client-source/TaskHandler.cs')
 text=re.sub(r'^using .*;\n','',raw,flags=re.M)
 text=text.replace('public class TaskHandler : BackgroundServiceBase, ITaskHandler','public sealed class HomeTaskTraceHandler : ITaskHandler')
 text=text.replace('SafeAction ITaskHandler.OnTraceStart { get; } = new SafeAction();','public HomeTaskTraceEvent OnTraceStart { get; }')
 text=text.replace('SafeAction ITaskHandler.OnTraceEnd { get; } = new SafeAction();','public HomeTaskTraceEvent OnTraceEnd { get; }')
 text=text.replace('SafeAction<long> ITaskHandler.OnTraceCancel { get; } = new SafeAction<long>();','public HomeTaskPlayerEvent<long> OnTraceCancel { get; }')
 for signature in ('StartTrace','StopTrace','CancelTrace','TraceWhenEnterMap','CleanWaitTraceTask'):
  text=text.replace('void ITaskHandler.'+signature+'(','public void '+signature+'(')
 text=text.replace('MeManager.MainPlayer.GetTaskData','Host.Player.GetTaskData')
 text=text.replace('BroadcastManager.Instance.SystemMsg(LanguageManager.Instance.GetText("task_start_trace_target_tip"));','Host.Broadcast("task_start_trace_target_tip");').replace('BroadcastManager.Instance.SystemMsg(LanguageManager.Instance.GetText("task_stop_trace_target_tip"));','Host.Broadcast("task_stop_trace_target_tip");')
 body=method(text,'public void StartTrace(');start=body.index('\t\tGLoggerErrorInterpolatedStringHandler');end=body.index('\n\t}',start)
 text=text.replace(body,body[:start]+'\t\tHost.InvalidTask(taskId, taskData?.TaskClassId);'+body[end:])
 body=method(text,'public void TraceWhenEnterMap(');warning=method(body,'if (mWaitTraceTaskId.HasValue)')
 text=text.replace(warning,'if (mWaitTraceTaskId.HasValue) { Host.PendingTraceReplaced(mWaitTraceTaskId.Value, taskId); }')
 constructor='''
    readonly IHomeTaskTraceServices Host;
    public HomeTaskTraceHandler(IHomeTaskTraceServices host)
    {
        Host=host??throw new ArgumentNullException(nameof(host));
        OnTraceStart=new HomeTaskTraceEvent(host.ReportException);
        OnTraceEnd=new HomeTaskTraceEvent(host.ReportException);
        OnTraceCancel=new HomeTaskPlayerEvent<long>(host.ReportException);
    }
'''
 text=text.replace('\n{','\n{'+constructor,1)
 assert not any(name in text for name in ('GLogger','Log.','MeManager','BroadcastManager','LanguageManager'))
 write('HomeTaskTraceHandler.cs','// Original TaskHandler with explicit local player/text/log bindings.\nusing System;\nnamespace Zjcs.Official.HomeTaskUI\n{\n'+text+'}\n')
 raw=read('client-source/TaskMapTraceHandler.cs')
 bodies='\n'.join(method(raw,sig) for sig in ('protected override void OnActivate()','private void TryStartTrace()','private void TryStartTraceWhenEnterMap()'))
 bodies=bodies.replace('protected override void OnActivate()','public void OnActivate()').replace('MapRootManagerProxy.Initialized','Host.MapInitialized').replace('MeManager.MainPlayer.GetCurrentPinTask()','Host.GetCurrentPinTask()').replace('MeManager.MainPlayer','Host.Player')
 body=method(bodies,'private void TryStartTraceWhenEnterMap()');warning=method(body,'if (currentPinTask == null || currentPinTask.TaskId != valueOrDefault)')
 bodies=bodies.replace(warning,'if (currentPinTask == null || currentPinTask.TaskId != valueOrDefault) { Host.PendingTracePinMismatch(valueOrDefault, currentPinTask?.TaskId); }')
 assert not any(name in bodies for name in ('GLogger','Log.','MeManager'))
 write('HomeTaskMapTraceHandler.cs','''// Original map activation trace sequence, with the real map owner supplying initialization.
using System;
namespace Zjcs.Official.HomeTaskUI
{
public sealed class HomeTaskMapTraceHandler
{
    readonly IHomeTaskTraceServices Host;
    readonly ITaskHandler TaskHandler;
    public HomeTaskMapTraceHandler(IHomeTaskTraceServices host,ITaskHandler taskHandler){Host=host??throw new ArgumentNullException(nameof(host));TaskHandler=taskHandler??throw new ArgumentNullException(nameof(taskHandler));}
'''+bodies+'\n}\n}\n')
 entity=read('client-source/ITaskTraceMapEntity.cs').replace('using System;','')
 write('HomeTaskTraceServices.cs','''using System;
namespace Zjcs.Official.HomeTaskUI
{
'''+entity+'''
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
''')
 for name in ('ITaskHandler','ITaskTraceTarget','TraceTaskComponent','ClientWorldMapProgressCondition','ClientMapLogicEntityEndByTagCondition'):
  read('client-source/'+name+'.cs')
 # The restored interface also exposes the already migrated generic SafeAction.
 for suffix in ('','.meta'):
  path=DEST/('HomeTaskPlayerEvents.cs'+suffix);outputs[path.relative_to(ROOT).as_posix()]=sha(path)
 for relative in ('scripts/fixtures/home_task_trace_validation.cs','scripts/fixtures/home_task_ui_services.cs','scripts/fixtures/home_task_ui_native_validation.cs','scripts/fixtures/home_task_row_profile_validation.cs','scripts/fixtures/home_task_navigation_validation.cs','unity/Assets/Game/Runtime/Offline/HomeTaskUI/OfflineHomeTaskNavigation.cs'):
  inputs[relative]=sha(ROOT/relative)
 proof={'manifest':'884_156692','generator_sha256':sha(Path(__file__)),'inputs':inputs,'outputs':outputs,'scope':'Original trace owner, no-argument SafeAction, and map activation pending-trace sequence. Actual entity/region trace target lifecycle and direction marker factory remain explicit required ports.'}
 (ROOT/'assets/reference/home-task-trace-runtime-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
 return proof

def build():
 from prepare_home_task_ui_runtime import build as build_ui
 build_ui()
 return json.loads((ROOT/'assets/reference/home-task-trace-runtime-source-manifest.json').read_bytes())

def stage(project):
 from prepare_home_task_map_entry import build as build_entry,stage as stage_entry
 from prepare_home_task_ui_validation import stage_fixture
 proof=build_entry();stage_entry(project,proof)
 trace=build()
 ui=json.loads((ROOT/'assets/reference/home-task-ui-runtime-source-manifest.json').read_bytes())
 for relative,digest in ui['outputs'].items():
  assert sha(ROOT/relative)==digest
  target=project/(ROOT/relative).relative_to(ROOT/'unity');target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(ROOT/relative,target)
 stage_fixture(ROOT/'assets/reference',project)
 folder=project/'Assets/HomeCurrencyWindowsNativeValidation'
 shutil.copy2(ROOT/'scripts/fixtures/home_task_trace_validation.cs',folder/'HomeTaskTraceValidation.cs')
 path=folder/'HomeCurrencyWindowsNativeValidation.cs';text=path.read_text('utf8');marker='        ConfigureTaskMapEntryNative();';assert text.count(marker)==1
 path.write_text(text.replace(marker,marker+'\n        ConfigureTaskTraceNative();'),'utf8',newline='\n')

if __name__=='__main__':
 parser=argparse.ArgumentParser(__doc__);parser.add_argument('--project',type=Path);args=parser.parse_args();build()
 if args.project:stage(args.project)
