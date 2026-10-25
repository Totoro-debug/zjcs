"""Compile original status/agent/mutex methods; callbacks are explicit inputs."""
import copy
import hashlib
import json
import random
import subprocess
from pathlib import Path
from generate_battle_reference import method, strip_logging, enum

ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'.tools/source-packages/official-code/common-source/Common'
OUT=ROOT/'.tools/gameplay-fixtures/combat'


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    agent=(SOURCE/'FightStatusAgentComponentImplement.cs').read_text('utf8')
    status=(SOURCE/'FightStatusComponentImplement.cs').read_text('utf8')
    info=(SOURCE/'FightStatusComponentInfo.cs').read_text('utf8')
    share=(SOURCE/'FightStatusShareStackComponent.cs').read_text('utf8')
    formula=(SOURCE/'BattleFormulaHandler.cs').read_text('utf8')
    mutex=(SOURCE/'FightStatusMutexForbiddenGroup.cs').read_text('utf8').split('namespace Common;',1)[1]
    rules=json.loads((ROOT/'assets/official/rules.json').read_text('utf8'))
    catalog=json.loads((ROOT/'assets/official/skill-rules.json').read_text('utf8'))
    code=[r'''
using System;using System.Linq;using System.Collections.Generic;using System.Text.Json;
using ILogger=System.Object;using EcEntityIdentifier=System.Int32;
namespace PinnedStatus{
public class ZLoggerErrorInterpolatedStringHandler{
 public ZLoggerErrorInterpolatedStringHandler(int a,int b,object c,ref bool d){d=false;}
 public void AppendLiteral(string a){}public void AppendFormatted<T>(T a,int b,string c,string d){}
}
public static class FightLog{public static object Status=new object();}
public static class GZLoggerExtensions{
 public static void Error(object a,ref ZLoggerErrorInterpolatedStringHandler b,object c,string d,string e,int f){}
 public static void Error(object a,Exception x,ref ZLoggerErrorInterpolatedStringHandler b,object c,string d,string e,int f){}
}
public static class SafeExtensions{
 public static void SafeInvoke<T,U>(this Action<T,U> a,T x,U y)=>a?.Invoke(x,y);
 public static void SafeInvoke<T,U,V>(this Action<T,U,V> a,T x,U y,V z)=>a?.Invoke(x,y,z);
}
public class Settings{public List<StatusMutexGroup> StatusMutexForbiddenGroup=new List<StatusMutexGroup>();public Dictionary<int,int> ShareStatusMaxStacks=new Dictionary<int,int>();}
public class GameSettings{public Settings Battle=new Settings();}
public static class Parsers{public static GameSettings GameSettings=new GameSettings();}
public class StatusMutexGroup{public EStatusMutexGroupMode Mode;public List<int> StatusIds=new List<int>();}
public class EcEntity{public int Id,ClassId;public bool Removing;}
public class FightRoundComponent{public int Round;}
public enum PropType{None}
''',enum('EFightStatusForbiddenType'),enum('EStatusMutexGroupMode'),
        'public class FightStatusComponentInfo{public bool ForbiddenOnApplicatorDie,IsOpenStack,IsAddDurationRound,ScalePropWhenChangeStacked;public int DurationRound,MaxAddDurationRound;',
        method(info,'public bool CanStackCount('),method(info,'public bool CanStackRound('),'}',
        'public class FightStatusShareStackComponentInfo{public int ShareId;}',
        'public class FightStatusShareStackComponent{public FightStatusShareStackComponentInfo Info=new FightStatusShareStackComponentInfo();public int StackCount{get;set;}',
        method(share,'public void ChangeStackCount('),'}',
        'public class BattleFormula{public static BattleFormula Instance=new BattleFormula();',
        method(formula,'public void ScaleProps(Dictionary<PropType, long> props, int scale,'),
        strip_logging(method(formula,'public void MergeProps(IReadOnlyDictionary')),
        strip_logging(method(formula,'public void MergePropsInPlace(Dictionary<PropType, long> dest, IReadOnlyDictionary')),'}',
        r'''
public class FightSourceInfo{public Dictionary<PropType,long> Props;}
public class FightStatusAgentComponent{
 public EcEntity Entity=new EcEntity();
 public Dictionary<int,FightStatusComponent> ApplyStatusDict=new Dictionary<int,FightStatusComponent>();public List<int> ApplyStatusList=new List<int>();public List<FightStatusComponent> StatusCache=new List<FightStatusComponent>();public Dictionary<int,int> StatusToShareStack=new Dictionary<int,int>();public bool IsApplicatorDie;
 public Action<FightStatusAgentComponent,FightStatusComponent> OnStatusStart,OnStatusEnd;public Action<FightStatusComponent,bool> OnForbiddenChanged;public Action<FightStatusComponent,int,int> OnStatusStackUpdate;
 public Agent impl;public bool CanAddShareStack(int id)=>impl.CanAddShareStack(id);public int ChangeShareStack(int id,int delta)=>impl.ChangeShareStack(id,delta);public void GetApplyStatusInOrder(List<FightStatusComponent> result)=>impl.GetApplyStatusInOrder(result);public void ChangeStatusForbidden(FightStatusComponent status,bool old)=>impl.ChangeStatusForbidden(status,old);public void FireUpdateStatusStack(FightStatusComponent status,int old,int next)=>impl.FireUpdateStatusStack(status,old,next);
}
public class Agent{public FightStatusAgentComponent Component=new FightStatusAgentComponent();public Agent(){Component.impl=this;}
''', '\n'.join(method(agent,m) for m in ['public void AddStatus(','public void RemoveStatus(','public void ChangeStatusForbidden(','public void FireUpdateStatusStack(','public void UpdateStatusForbidden(','private void SetStatusApplicatorDie(','public void GetApplyStatusInOrder(','public bool CanAddShareStack(','public int ChangeShareStack(']),
        'public void Dead(bool value){Component.IsApplicatorDie=value;SetStatusApplicatorDie(value);}}',
        r'''
public class FightStatusComponent{
 public EcEntity Entity=new EcEntity();public FightStatusComponentInfo Info=new FightStatusComponentInfo();public EFightStatusForbiddenType ForbiddenType;public bool Forbidden=>ForbiddenType!=EFightStatusForbiddenType.None;public FightStatusAgentComponent ApplyStatusAgent;public FightStatusShareStackComponent ShareStackComponent;public FightRoundComponent RoundComp;public int StartRound,CurDurationRound,StackedCount;public Dictionary<PropType,long> StatusConfigProps,StatusProps,Props;public FightSourceInfo SourceInfo=new FightSourceInfo();
 public Action<FightStatusComponent,bool> OnForbiddenChanged;public Action<FightStatusComponent,int,int> OnStatusStackUpdate;public Status impl;
 public void SetForbidden(EFightStatusForbiddenType type,bool value)=>impl.SetForbidden(type,value);
}
public class Status{public FightStatusComponent Component;public Status(FightStatusComponent component){Component=component;component.impl=this;}
''', '\n'.join(method(status,m) for m in ['public void SetForbidden(','public void ResetDuration(','public void SetStackedCount(','public void UpdateStatusRound(','public bool TryChangeShareStack(','private void RemoveShareStackCountOnEnd(']),
        'public void EndShare()=>RemoveShareStackCountOnEnd();}', mutex, r'''
public static class Reference{
static Dictionary<PropType,long> Props(JsonElement x)=>x.ValueKind==JsonValueKind.Null?null:x.EnumerateObject().ToDictionary(p=>(PropType)int.Parse(p.Name),p=>p.Value.GetInt64());
static object PropsView(Dictionary<PropType,long> p)=>p?.ToDictionary(x=>((int)x.Key).ToString(),x=>x.Value);
public static string[] Evaluate(string input){var results=new List<string>();foreach(var c in JsonDocument.Parse(input).RootElement.EnumerateArray()){
 Parsers.GameSettings.Battle=new Settings();foreach(var g in c.GetProperty("groups").EnumerateArray())Parsers.GameSettings.Battle.StatusMutexForbiddenGroup.Add(new StatusMutexGroup{Mode=(EStatusMutexGroupMode)g.GetProperty("Mode").GetInt32(),StatusIds=g.GetProperty("StatusIds").EnumerateArray().Select(x=>x.GetInt32()).ToList()});
 foreach(var x in c.GetProperty("limits").EnumerateObject())Parsers.GameSettings.Battle.ShareStatusMaxStacks[int.Parse(x.Name)]=x.Value.GetInt32();
 var a=new Agent();new FightStatusMutexForbiddenGroup(a.Component).Start();var ev=new List<object>();
 a.Component.OnStatusStart+=(x,s)=>ev.Add(new object[]{"start",s.Entity.Id});a.Component.OnStatusEnd+=(x,s)=>ev.Add(new object[]{"end",s.Entity.Id});a.Component.OnForbiddenChanged+=(s,old)=>ev.Add(new object[]{"agent_forbidden",s.Entity.Id,old});a.Component.OnStatusStackUpdate+=(s,old,next)=>ev.Add(new object[]{"agent_stack",s.Entity.Id,old,next});
 var entities=new Dictionary<int,FightStatusComponent>();foreach(var pair in c.GetProperty("entities").EnumerateObject()){
  var x=pair.Value;var cfg=x.GetProperty("info");var s=new FightStatusComponent{Entity=new EcEntity{Id=x.GetProperty("id").GetInt32(),ClassId=x.GetProperty("class_id").GetInt32()},ApplyStatusAgent=a.Component,ForbiddenType=(EFightStatusForbiddenType)x.GetProperty("forbidden").GetInt32(),StartRound=x.GetProperty("start").GetInt32(),CurDurationRound=x.GetProperty("duration").GetInt32(),StackedCount=x.GetProperty("stacks").GetInt32(),StatusConfigProps=Props(x.GetProperty("config_props")),StatusProps=Props(x.GetProperty("status_props")),Props=Props(x.GetProperty("props")),SourceInfo=new FightSourceInfo{Props=Props(x.GetProperty("source_props"))},Info=new FightStatusComponentInfo{ForbiddenOnApplicatorDie=cfg.GetProperty("ForbiddenOnApplicatorDie").GetBoolean(),IsOpenStack=cfg.GetProperty("IsOpenStack").GetBoolean(),IsAddDurationRound=cfg.GetProperty("IsAddDurationRound").GetBoolean(),ScalePropWhenChangeStacked=cfg.GetProperty("ScalePropWhenChangeStacked").GetBoolean(),DurationRound=cfg.GetProperty("DurationRound").GetInt32(),MaxAddDurationRound=cfg.GetProperty("MaxAddDurationRound").GetInt32()}};
  if(x.GetProperty("round").ValueKind!=JsonValueKind.Null)s.RoundComp=new FightRoundComponent{Round=x.GetProperty("round").GetInt32()};
  if(x.GetProperty("share_id").ValueKind!=JsonValueKind.Null)s.ShareStackComponent=new FightStatusShareStackComponent{Info=new FightStatusShareStackComponentInfo{ShareId=x.GetProperty("share_id").GetInt32()},StackCount=x.GetProperty("share_count").GetInt32()};
  s.OnForbiddenChanged+=(p,old)=>ev.Add(new object[]{"component_forbidden",p.Entity.Id,old});s.OnStatusStackUpdate+=(p,old,next)=>ev.Add(new object[]{"component_stack",p.Entity.Id,old,next});new Status(s);entities[s.Entity.Id]=s;
 }
 var snapshots=new List<object>();foreach(var e in c.GetProperty("events").EnumerateArray()){
  ev.Clear();var s=e.TryGetProperty("id",out var id)?entities.GetValueOrDefault(id.GetInt32()):null;object result=null;
  switch(e.GetProperty("op").GetString()){
   case "add":a.AddStatus(s);break;case "remove":a.RemoveStatus(s);break;
   case "forbidden":s?.SetForbidden((EFightStatusForbiddenType)e.GetProperty("type").GetInt32(),e.GetProperty("value").GetBoolean());break;
   case "dead":a.Dead(e.GetProperty("value").GetBoolean());break;
   case "class_forbidden":a.UpdateStatusForbidden(e.GetProperty("class_id").GetInt32(),(EFightStatusForbiddenType)e.GetProperty("type").GetInt32(),e.GetProperty("value").GetBoolean());break;
   case "stack":s?.impl.SetStackedCount(e.GetProperty("value").GetInt32());break;
   case "reset_duration":s?.impl.ResetDuration();break;case "update_duration":s?.impl.UpdateStatusRound();break;
   case "round":if(s!=null)s.RoundComp=e.GetProperty("value").ValueKind==JsonValueKind.Null?null:new FightRoundComponent{Round=e.GetProperty("value").GetInt32()};break;
   case "try_share":if(s!=null){bool ok=s.impl.TryChangeShareStack(e.GetProperty("value").GetInt32(),out int delta);result=new{ok=ok,delta=delta};}break;
   case "change_share":result=a.ChangeShareStack(e.GetProperty("share_id").GetInt32(),e.GetProperty("value").GetInt32());break;
   case "can_share":result=a.CanAddShareStack(e.GetProperty("share_id").GetInt32());break;case "end_share":s?.impl.EndShare();break;
  }
  snapshots.Add(new{active=a.Component.ApplyStatusList.ToArray(),share=a.Component.StatusToShareStack.ToDictionary(x=>x.Key.ToString(),x=>x.Value),dead=a.Component.IsApplicatorDie,entities=entities.ToDictionary(x=>x.Key.ToString(),x=>new{forbidden=(int)x.Value.ForbiddenType,start=x.Value.StartRound,duration=x.Value.CurDurationRound,stacks=x.Value.StackedCount,share_count=x.Value.ShareStackComponent?.StackCount??0,status_props=PropsView(x.Value.StatusProps),props=PropsView(x.Value.Props),round=x.Value.RoundComp==null?(int?)null:x.Value.RoundComp.Round}),events=ev.ToArray(),result=result});
 }results.Add(JsonSerializer.Serialize(snapshots));}return results.ToArray();
}}}
''']
    groups=rules['game_settings']['Battle']['StatusMutexForbiddenGroup'][:4]
    limits=rules['game_settings']['Battle']['ShareStatusMaxStacks']
    classes=groups[0]['StatusIds']+[groups[0]['StatusIds'][1]]+groups[1]['StatusIds']
    def entity(i,cls):
        cfg=copy.deepcopy(catalog['entities'][str(cls)]['FightStatusComponent'])
        return dict(id=i,class_id=cls,info=cfg,forbidden=0,start=0,duration=cfg['DurationRound'],stacks=1,round=1,share_id=None,share_count=0,config_props={'2':7,'150':3},status_props={'2':7,'150':3},source_props={'2':20,'3':9},props={'2':27,'3':9,'150':3})
    entities={str(i):entity(i,cls) for i,cls in enumerate(classes,1)}
    # Explicit callback fixtures exercise shared limits, scaling and both timing
    # branches without claiming these overrides are original production content.
    entities['4']['share_id']=25300;entities['5']['share_id']=25300
    for key in ['4','5']:
        entities[key]['info'].update(IsOpenStack=True,ScalePropWhenChangeStacked=True,IsAddDurationRound=True,DurationRound=3,MaxAddDurationRound=8,ForbiddenOnApplicatorDie=True)
        entities[key]['duration']=3
    explicit=[dict(op='add',id=i) for i in [1,2,3,4,5,1,99]]+[
        dict(op='forbidden',id=2,type=4,value=True),dict(op='remove',id=3),dict(op='remove',id=2),
        dict(op='dead',value=True),dict(op='dead',value=False),dict(op='stack',id=4,value=4),dict(op='stack',id=5,value=4),
        dict(op='can_share',share_id=25300),dict(op='stack',id=5,value=5),dict(op='stack',id=4,value=0),
        dict(op='reset_duration',id=4),dict(op='round',id=4,value=5),dict(op='update_duration',id=4),
        dict(op='end_share',id=5),dict(op='change_share',share_id=999,value=1),dict(op='remove',id=99)]
    rng=random.Random(854156383);cases=[]
    # Original ScaleProps explicitly accepts null source/result dictionaries.
    # Check all four nullable dictionaries independently and in combination.
    for mask in range(16):
        es=copy.deepcopy(entities)
        for key in ['4','5']:
            es[key]['share_id']=None
            for bit,field in enumerate(['config_props','status_props','props','source_props']):
                if mask&(1<<bit):es[key][field]=None
        events=[dict(op='stack',id=4,value=2),dict(op='stack',id=4,value=0),dict(op='stack',id=4,value=3),dict(op='stack',id=5,value=0)]
        cases.append(dict(name=f'original-status-nullable-{mask}',entities=es,groups=groups,limits=limits,events=events))
    for i in range(101):
        es=copy.deepcopy(entities);gs=copy.deepcopy(groups)
        if i%2:gs[0]['Mode']=1
        if i:
            for s in es.values():
                s['forbidden']=rng.choice([0,1,2,4]);s['start']=rng.choice([0,1,5]);s['duration']=rng.choice([-1,0,3,8]);s['round']=rng.choice([None,0,5,10]);s['info']['ForbiddenOnApplicatorDie']=rng.choice([True,False])
                if rng.randrange(4)==0:s['config_props']={}
                if rng.randrange(4)==0:s['props']=None
            events=[]
            for j in range(40):
                op=rng.choice(['add','remove','forbidden','dead','class_forbidden','stack','reset_duration','update_duration','round','try_share','change_share','can_share','end_share']);e=dict(op=op,id=rng.randrange(1,7))
                if op in ['forbidden','class_forbidden']:e.update(type=rng.choice([1,2,4,8]),value=rng.choice([True,False]),class_id=rng.choice(classes))
                elif op=='dead':e['value']=rng.choice([True,False])
                elif op in ['stack','try_share']:e['value']=rng.choice([-5,-1,0,1,2,4,8])
                elif op=='round':e['value']=rng.choice([None,0,1,5,10])
                elif op in ['change_share','can_share']:e.update(share_id=rng.choice([25300,24029,999]),value=rng.choice([-10,-1,0,1,3,20]))
                events.append(e)
        else:events=explicit
        cases.append(dict(name=f'original-status-state-{i}',entities=es,groups=gs,limits=limits,events=events))
    harness=OUT/'PinnedStatus.cs';harness.write_text('\n'.join(code),'utf8')
    inputs=OUT/'status-state-input.json';inputs.write_text(json.dumps(cases),'utf8')
    runner=OUT/'run-status-state-reference.ps1'
    def ps(path):return str(path).replace("'","''")
    runner.write_text("$ErrorActionPreference='Stop'\nAdd-Type -Path '"+ps(harness)+"'\n[PinnedStatus.Reference]::Evaluate([IO.File]::ReadAllText('"+ps(inputs)+"'))",'utf8')
    result=subprocess.run(['pwsh','-NoProfile','-File',str(runner)],capture_output=True,text=True,encoding='utf8')
    if result.returncode:raise RuntimeError(result.stdout+result.stderr)
    lines=result.stdout.splitlines()
    if len(lines)!=len(cases):raise ValueError('Original status output count differs')
    for case,line in zip(cases,lines):case['expected']=json.loads(line)
    names=['FightStatusAgentComponentImplement.cs','FightStatusComponentImplement.cs','FightStatusComponentInfo.cs','FightStatusShareStackComponent.cs','FightStatusMutexForbiddenGroup.cs','BattleFormulaHandler.cs','EFightStatusForbiddenType.cs','EStatusMutexGroupMode.cs']
    output=OUT/'status-state-reference.json'
    output.write_text(json.dumps(dict(manifest='854_156383',cases=cases,original_code_sha256={n:hashlib.sha256((SOURCE/n).read_bytes()).hexdigest() for n in names},harness_sha256=hashlib.sha256(harness.read_bytes()).hexdigest(),catalog_sha256=hashlib.sha256((ROOT/'assets/official/skill-rules.json').read_bytes()).hexdigest(),settings_sha256=hashlib.sha256((ROOT/'assets/official/rules.json').read_bytes()).hexdigest()),ensure_ascii=False,indent=2)+'\n','utf8')
    print('Original status state reference:',len(cases),'sequences;',output)


if __name__=='__main__':main()
