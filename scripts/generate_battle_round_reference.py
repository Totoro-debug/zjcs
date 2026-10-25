"""Compile pinned queue callbacks and speed/change formulas; no runtime assets."""
import copy
import hashlib
import json
import random
import subprocess
from pathlib import Path
from generate_battle_reference import method, strip_logging, enum

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / '.tools/source-packages/official-code/common-source/Common'
OUT = ROOT / '.tools/gameplay-fixtures/combat'


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    driver = (SOURCE/'FightRoundDriverComponent.cs').read_text('utf8')
    runtime = (SOURCE/'RoundRuntimeData.cs').read_text('utf8')
    formula = (SOURCE/'BattleFormulaHandler.cs').read_text('utf8')
    change = (SOURCE/'FightStatusRoundChangeCfg.cs').read_text('utf8')
    update = (SOURCE/'FightRoundUpdateDataModification.cs').read_text('utf8')
    rules = json.loads((ROOT/'assets/official/rules.json').read_text('utf8'))
    battle = json.loads((ROOT/'assets/official/battle-rules.json').read_text('utf8'))
    skill = json.loads((ROOT/'assets/official/skill-rules.json').read_text('utf8'))
    speed_id = next(int(k) for k,v in skill['prop_names'].items() if v=='Speed')
    assert not next(r for r in rules['prop_cfg'] if r['PropType']==speed_id)['Float']
    queue_methods = '\n'.join(method(driver, marker) for marker in [
        'public void UpdateData(', 'private void AddRuntimeDataToList(', 'public void InsertData(',
        'public void RemoveData(', 'public void UpdateCurActing(', 'public RoundRuntimeData FindData(',
        'private void OnGameStatusUpdate(', 'private void ResetDataList('])
    code = [r'''
using System;using System.Linq;using System.Collections.Generic;using System.Text.Json;
using EcEntityIdentifier=System.Int32;using ILogger=System.Object;
namespace PinnedRound {
public enum GameStatus{None,Start,End}
public class ZLoggerErrorInterpolatedStringHandler {
 public ZLoggerErrorInterpolatedStringHandler(int a,int b,object c,ref bool d){d=false;}
 public void AppendLiteral(string a){} public void AppendFormatted<T>(T a,int b,string c,string d){}
}
public static class FightLog{public static object Round=new object();}
public static class GZLoggerExtensions{public static void Error(object a,ref ZLoggerErrorInterpolatedStringHandler b,object c,string d,string e,int f){}}
public static class JsonUtils{public static string ToJson(object a)=>"";}
public class GAction<T>{public Action<T> callback;public void Invoke(T a)=>callback?.Invoke(a);}
public class GAction<T,U>{public Action<T,U> callback;public void Invoke(T a,U b)=>callback?.Invoke(a,b);}
public class EcEntity{public int ClassId;public FightRoundComponent component;public T GetComponent<T>() where T:class=>component as T;}
public class Container{public Dictionary<int,EcEntity> entities=new Dictionary<int,EcEntity>();public EcEntity GetEntity(int id)=>entities.GetValueOrDefault(id);}
public class FightRankOffsetDamageInfo{public int SpeedScale;}
public class OffsetParser{public Dictionary<Rank,FightRankOffsetDamageInfo> values=new Dictionary<Rank,FightRankOffsetDamageInfo>();public FightRankOffsetDamageInfo Get(Rank rank)=>values.GetValueOrDefault(rank);}
public class Parsers{public static Parsers Instance=new Parsers();public OffsetParser FightRankOffsetDamageInfoParser=new OffsetParser();}
''', enum('Rank'), enum('CharacterRoundDriverUpdateState'), f'public enum PropType {{Speed={speed_id}}}',
        'public class Formula { public double GetProp(Dictionary<PropType,long> p,PropType t)=>p.GetValueOrDefault(t);',
        strip_logging(method(formula, 'public double SpeedToTime(')),
        method(formula, 'public double CalcRoundInterval('), '}',
        'public class FightRoundComponent { public Dictionary<PropType,long> props=new Dictionary<PropType,long>();public Rank rank;public double CalcRoundInterval()=>new Formula().CalcRoundInterval(props,rank);}',
        'public class RoundRuntimeData {public int EntityId;public double PreTime,NextTime;public FightRoundComponent RoundComponent;',
        method(runtime,'public void ResetTime('),
        'public void SetRoundComp(EcEntity entity){RoundComponent=entity?.GetComponent<FightRoundComponent>();}}',
        'public class FightRoundDriverComponent { public LinkedList<RoundRuntimeData> DataList=new LinkedList<RoundRuntimeData>();public int acting,round;private double mCurrentTime;public double CurrentTime=>mCurrentTime;public EcEntity Entity=new EcEntity();public Container Container=new Container();public List<object> events=new List<object>();',
        'public GAction<RoundRuntimeData> OnDataInsert;public GAction<int> OnDataRemove;public GAction<RoundRuntimeData,CharacterRoundDriverUpdateState> OnDataUpdate;',
        'public int CurActingEntityId{get=>acting;set{if(acting!=value){events.Add(new object[]{"acting",acting,value});acting=value;}}}',
        'public int Round{get=>round;set{if(round!=value){round=value;events.Add(new object[]{"round",round});}}}',
        'public FightRoundDriverComponent(){OnDataInsert=new GAction<RoundRuntimeData>{callback=x=>events.Add(new object[]{"insert",x.EntityId})};OnDataRemove=new GAction<int>{callback=x=>events.Add(new object[]{"remove",x})};OnDataUpdate=new GAction<RoundRuntimeData,CharacterRoundDriverUpdateState>{callback=(x,s)=>events.Add(new object[]{"update",x.EntityId,(int)s})};}',
        queue_methods,
        'public void Reset(){OnGameStatusUpdate(GameStatus.Start);}public object Snapshot()=>new{entries=DataList.Select(x=>new{id=x.EntityId,pre=x.PreTime,next=x.NextTime}).ToArray(),acting=acting,round=round,current=mCurrentTime};}',
        'public class Change { public float RoundIntervalPercent;',method(change,'public double GetNextTimeChangeValue('),'}',
        'public static class Reference {',method(update,'public static CharacterRoundDriverUpdateState GetUpdateState('),r'''
static RoundRuntimeData Entry(JsonElement x)=>x.ValueKind==JsonValueKind.Null?null:new RoundRuntimeData{EntityId=x.GetProperty("id").GetInt32(),PreTime=x.GetProperty("pre").GetDouble(),NextTime=x.GetProperty("next").GetDouble()};
public static string[] Evaluate(string inputs,string tables){
 foreach(var x in JsonDocument.Parse(tables).RootElement.GetProperty("tables").GetProperty("fight_rank_offset_damage").EnumerateArray())Parsers.Instance.FightRankOffsetDamageInfoParser.values[(Rank)x.GetProperty("Rank").GetInt32()]=new FightRankOffsetDamageInfo{SpeedScale=x.GetProperty("SpeedScale").GetInt32()};
 var results=new List<string>();foreach(var c in JsonDocument.Parse(inputs).RootElement.EnumerateArray()){
  var d=new FightRoundDriverComponent();foreach(var e in c.GetProperty("entities").EnumerateObject()){
   var cfg=e.Value;var component=cfg.GetProperty("round_component").GetBoolean()?new FightRoundComponent{rank=(Rank)cfg.GetProperty("rank").GetInt32()}:null;
   if(component!=null)component.props[PropType.Speed]=cfg.GetProperty("props").EnumerateObject().First().Value.GetInt64();
   d.Container.entities[int.Parse(e.Name)]=new EcEntity{component=component};
  }var snapshots=new List<object>();foreach(var e in c.GetProperty("events").EnumerateArray()){
   d.events.Clear();switch(e.GetProperty("op").GetString()){
    case "insert":d.InsertData(Entry(e.GetProperty("entry")));break;
    case "update":d.UpdateData(Entry(e.GetProperty("entry")),(CharacterRoundDriverUpdateState)e.GetProperty("state").GetInt32());break;
    case "remove":d.RemoveData(e.GetProperty("id").GetInt32());break;
    case "acting":d.UpdateCurActing(e.GetProperty("id").GetInt32());break;
    case "round":d.Round=e.GetProperty("value").GetInt32();break;
    case "status":d.Reset();break;
   }snapshots.Add(new{state=d.Snapshot(),notifications=d.events.ToArray()});
  }
  var changes=new List<object>();foreach(var v in c.GetProperty("changes").EnumerateArray()){
   var entry=new RoundRuntimeData{NextTime=v.GetProperty("next").GetDouble(),RoundComponent=new FightRoundComponent()};
   entry.RoundComponent.props[PropType.Speed]=v.GetProperty("speed").GetInt64();entry.RoundComponent.rank=(Rank)v.GetProperty("rank").GetInt32();
   var driver=new FightRoundDriverComponent();driver.InsertData(new RoundRuntimeData{EntityId=999,NextTime=v.GetProperty("current").GetDouble()});
   driver.Container.entities[999]=new EcEntity{component=new FightRoundComponent()};driver.InsertData(new RoundRuntimeData{EntityId=999,NextTime=v.GetProperty("current").GetDouble()});driver.UpdateCurActing(999);
   changes.Add(new{interval=entry.RoundComponent.CalcRoundInterval(),delta=new Change{RoundIntervalPercent=v.GetProperty("percent").GetSingle()}.GetNextTimeChangeValue(driver,entry),state=(int)GetUpdateState(v.GetProperty("next").GetDouble(),v.GetProperty("new_time").GetDouble())});
  }
  results.Add(JsonSerializer.Serialize(new{snapshots=snapshots,changes=changes}));
 }return results.ToArray();
}}}
''']
    entities={str(i):dict(props={str(speed_id):speed},rank=rank,round_component=flag) for i,speed,rank,flag in [(1,100,1,True),(2,100,1,True),(3,400,5,True),(4,0,0,True),(5,100,2,False)]}
    def entry(id,next,pre=0):return dict(id=id,next=next,pre=pre)
    events=[dict(op='insert',entry=entry(1,20)),dict(op='insert',entry=entry(2,20)),dict(op='insert',entry=entry(3,5)),dict(op='acting',id=3),dict(op='round',value=1),dict(op='acting',id=3),dict(op='update',entry=entry(1,5,1),state=1),dict(op='remove',id=2),dict(op='status'),dict(op='insert',entry=entry(5,0)),dict(op='insert',entry=None),dict(op='update',entry=None,state=0),dict(op='acting',id=999)]
    rng=random.Random(854156383);cases=[]
    for i in range(101):
        es=copy.deepcopy(entities)
        if i:
            for entity in es.values():entity['props'][str(speed_id)]=rng.choice([-100,0,1,7,50,100,400,10000]);entity['rank']=rng.choice([0,1,2,3,5,10,30,100])
            seq=[]
            for j in range(30):
                op=rng.choice(['insert','update','remove','acting','round','status']);value=rng.randrange(1,8)
                if op in ['insert','update']:seq.append(dict(op=op,entry=entry(value,rng.choice([0,1,2,5,5,20,100]),rng.randrange(0,3)),state=rng.randrange(0,4)))
                elif op=='round':seq.append(dict(op=op,value=rng.randrange(0,9)))
                elif op=='status':seq.append(dict(op=op))
                else:seq.append(dict(op=op,id=value))
        else:seq=events
        changes=[dict(current=15,next=n,speed=s,rank=r,percent=p,new_time=t) for n,s,r,p,t in [(20,100,1,0,10),(10,0,0,0,10),(20,-5,100,0.1,21),(30,400,5,-0.3333333333,30),(15,1,30,1,14),(25,10000,2,-2,26)]]
        cases.append(dict(name=f'original-round-queue-{i}',entities=es,events=seq,changes=changes))
    harness=OUT/'PinnedRound.cs';harness.write_text('\n'.join(code),'utf8')
    inputs=OUT/'round-input.json';inputs.write_text(json.dumps(cases),'utf8')
    runner=OUT/'run-round-reference.ps1'
    def ps(p):return str(p).replace("'","''")
    runner.write_text("$ErrorActionPreference='Stop'\nAdd-Type -Path '"+ps(harness)+"'\n[PinnedRound.Reference]::Evaluate([IO.File]::ReadAllText('"+ps(inputs)+"'),[IO.File]::ReadAllText('"+ps(ROOT/'assets/official/battle-rules.json')+"'))",'utf8')
    result=subprocess.run(['pwsh','-NoProfile','-File',str(runner)],capture_output=True,text=True,encoding='utf8')
    if result.returncode:raise RuntimeError(result.stdout+result.stderr)
    lines=result.stdout.splitlines()
    if len(lines)!=len(cases):raise ValueError('Original round reference output count differs')
    for case,line in zip(cases,lines):case['expected']=json.loads(line)
    original_names=['FightRoundDriverComponent.cs','RoundRuntimeData.cs','BattleFormulaHandler.cs','FightStatusRoundChangeCfg.cs','FightRoundUpdateDataModification.cs']
    output=OUT/'round-reference.json'
    output.write_text(json.dumps(dict(manifest='854_156383',cases=cases,original_code_sha256={name:hashlib.sha256((SOURCE/name).read_bytes()).hexdigest() for name in original_names},harness_sha256=hashlib.sha256(harness.read_bytes()).hexdigest(),table_sha256=hashlib.sha256((ROOT/'assets/official/battle-rules.json').read_bytes()).hexdigest()),ensure_ascii=False,indent=2)+'\n','utf8')
    print('Original queue/formula reference:',len(cases),'sequences;',output)


if __name__=='__main__':main()
