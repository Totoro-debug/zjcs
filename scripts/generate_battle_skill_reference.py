"""Compile pinned skill and round phase callbacks without publishing runtime assets."""
import copy
import hashlib
import json
import random
import subprocess
from pathlib import Path
from generate_battle_reference import method

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / '.tools/source-packages/official-code/common-source/Common'
CLIENT = ROOT / '.tools/source-packages/official-code/client-source'
OUT = ROOT / '.tools/gameplay-fixtures/combat'


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    data = (SOURCE/'FightSkillData.cs').read_text('utf8')
    agent = (SOURCE/'FightSkillAgentComponentImplement.cs').read_text('utf8')
    formula = (SOURCE/'BattleFormulaHandler.cs').read_text('utf8')
    stop = (SOURCE/'FightStatusSkillStopCdComponent.cs').read_text('utf8')
    round_code = (SOURCE/'FightRoundComponent.cs').read_text('utf8')
    view = (CLIENT/'FightSkillListDataEntityProvider.cs').read_text('utf8')
    monster_view = (CLIENT/'FightSkillListDataMonsterProvider.cs').read_text('utf8')
    assert method(view, 'public override long GetSkillLeftCD(') == method(monster_view, 'public override long GetSkillLeftCD(')
    skill_catalog = json.loads((ROOT/'assets/official/skill-rules.json').read_text('utf8'))
    rules = json.loads((ROOT/'assets/official/rules.json').read_text('utf8'))
    cd_id = next(int(k) for k, v in skill_catalog['prop_names'].items() if v == 'CD')
    assert not next(x for x in rules['prop_cfg'] if x['PropType'] == cd_id)['Float']
    code = [r'''
using System;using System.Linq;using System.Collections.Generic;using System.Text.Json;
using ILogger=System.Object;using EcEntityIdentifier=System.Int32;
namespace PinnedSkill {
public class ZLoggerErrorInterpolatedStringHandler{
 public ZLoggerErrorInterpolatedStringHandler(int a,int b,object c,ref bool d){d=false;}
 public void AppendLiteral(string a){}public void AppendFormatted<T>(T a,int b,string c,string d){}
}
public static class FightLog{public static object Skill=new object();}
public static class GZLoggerExtensions{public static void Error(object a,ref ZLoggerErrorInterpolatedStringHandler b,object c,string d,string e,int f){}}
public class GAction<T>{public Action<T> callback;public void Invoke(T a)=>callback?.Invoke(a);public void SafeInvoke(T a)=>callback?.Invoke(a);}
public class GAction<T,U>{public Action<T,U> callback;public void Invoke(T a,U b)=>callback?.Invoke(a,b);public void SafeInvoke(T a,U b)=>callback?.Invoke(a,b);}
public class ReplayAction{public Action<FightSkillAgentComponent,int,FightSkillComponent> callback;public void SafeInvoke(FightSkillAgentComponent a,int b,FightSkillComponent c)=>callback?.Invoke(a,b,c);}
public enum EReplaySkillType{None}
public class EcEntity{public int Id,ClassId;}
public class FightSkillComponent{public int SkillSlot;public EcEntity Entity=new EcEntity();}
public class FightSkillComponentInfo{public bool ResetCDAtStart;public int LimitedTimes;}
''', f'public enum PropType{{CD={cd_id}}}',
        'public class SkillEntityData{public FightSkillComponentInfo SkillInfo=new FightSkillComponentInfo();public Dictionary<PropType,long> Props=new Dictionary<PropType,long>();}',
        'public class BattleFormula{public static BattleFormula Instance=new BattleFormula();public double GetProp(Dictionary<PropType,long> props,PropType type)=>props.GetValueOrDefault(type);',
        method(formula, 'public long SkillCD('), '}',
        'public class FightSkillData{private bool mHasInitLastRound;private int mLastRound;private long mCD;private int mReleasedTimes;private SkillEntityData mDefaultSkill=new SkillEntityData();public long CD=>mCD;public SkillEntityData DefaultSkill=>mDefaultSkill;',
        method(data, 'public int LastRound'),
        method(data, 'private void InitLastRound('),
        method(data, 'public void RunSkill('),
        method(data, 'public void ResetSkillReleasedTime('),
        method(data, 'public bool CanReleaseByCount('),
        method(data, 'public void PermanentReduceCd('),
        method(data, 'public void ResetSkillCd('), r'''
public FightSkillData(JsonElement x){mDefaultSkill.Props[PropType.CD]=x.GetProperty("props").GetProperty("CD_KEY").GetInt64();mCD=BattleFormula.Instance.SkillCD(mDefaultSkill.Props);mDefaultSkill.SkillInfo.ResetCDAtStart=x.GetProperty("reset").GetBoolean();mDefaultSkill.SkillInfo.LimitedTimes=x.GetProperty("limit").GetInt32();mReleasedTimes=x.GetProperty("released").GetInt32();if(x.GetProperty("last").ValueKind!=JsonValueKind.Null)LastRound=x.GetProperty("last").GetInt32();}
public void Initialize(int change){if(!mHasInitLastRound)InitLastRound(change);}
public object Snapshot()=>new{cd=mCD,last_round=mLastRound,has_init=mHasInitLastRound,released=mReleasedTimes,allowed=CanReleaseByCount()};
}
'''.replace('CD_KEY', str(cd_id)),
        'public class FightRoundComponent{private int mRound;private bool mIsDriving;public bool IsDriving=>mIsDriving;public GAction<FightRoundComponent,int> OnRoundUpdated=new GAction<FightRoundComponent,int>();public GAction<FightRoundComponent> OnRoundFinish=new GAction<FightRoundComponent>();public void Dirty(){}public void FixtureDriving(bool value){mIsDriving=value;}',
        method(round_code, 'public int Round'),
        method(round_code, 'public void DoRoundFinish('), '}', r'''
public class FightSkillAgentComponent{public SortedDictionary<int,FightSkillData> SlotSkillItemDatas=new SortedDictionary<int,FightSkillData>();public FightRoundComponent RoundComponent=new FightRoundComponent();public GAction<int> OnSkillCDChanged;public GAction<FightSkillAgentComponent,int> OnSkillUpdated;public ReplayAction OnSkillReplay=new ReplayAction();}
public class Agent{public FightSkillAgentComponent Component=new FightSkillAgentComponent();public List<object> events=new List<object>();
 public Agent(){Component.OnSkillCDChanged=new GAction<int>{callback=x=>events.Add(new object[]{"cd",x})};Component.OnSkillUpdated=new GAction<FightSkillAgentComponent,int>{callback=(c,x)=>events.Add(new object[]{"updated",x})};Component.OnSkillReplay.callback=(c,x,s)=>events.Add(new object[]{"replay",x});}
''',
        '\n'.join(method(agent, marker) for marker in [
            'public FightSkillData GetSkillData(', 'public void EquipEmptySkill(',
            'public void UpdateSkillDataOnRun(', 'public void ChangeSkillCD(',
            'public void ResetAllSkillCD(', 'public void ReplaySkill(']),
        'public object Snapshot()=>new{round=Component.RoundComponent.Round,driving=Component.RoundComponent.IsDriving,slots=Component.SlotSkillItemDatas.Select(x=>new{slot=x.Key,value=x.Value?.Snapshot()}).ToArray()};}',
        'public class Stop{public FightSkillAgentComponent mSkillAgent;',
        method(stop, 'private void OnRoundUpdated('),
        'public void Run()=>OnRoundUpdated(null,0);}',
        'public class Provider{public Agent agent;public class RoundWrap{public FightRoundComponent Component;}public RoundWrap mRound;public bool TryGetSkillData(int slot,out FightSkillData data){data=agent.GetSkillData(slot);return data!=null;}',
        method(view, 'public override long GetSkillLeftCD(').replace('public override', 'public'), '}', r'''
public static class Reference{
public static string[] Evaluate(string input){var results=new List<string>();foreach(var c in JsonDocument.Parse(input).RootElement.EnumerateArray()){
 var a=new Agent();a.Component.RoundComponent.Round=c.GetProperty("round").GetInt32();a.Component.RoundComponent.FixtureDriving(c.GetProperty("driving").GetBoolean());
 foreach(var s in c.GetProperty("slots").EnumerateObject())a.Component.SlotSkillItemDatas[int.Parse(s.Name)]=s.Value.ValueKind==JsonValueKind.Null?null:new FightSkillData(s.Value);
 var view=new Provider{agent=a,mRound=new Provider.RoundWrap{Component=a.Component.RoundComponent}};var snaps=new List<object>();foreach(var e in c.GetProperty("events").EnumerateArray()){
  a.events.Clear();int selected=-1;int slot=e.TryGetProperty("slot",out var p)?p.GetInt32():0;var skill=a.GetSkillData(slot);switch(e.GetProperty("op").GetString()){
   case "initialize":foreach(var v in a.Component.SlotSkillItemDatas.Values)v?.Initialize(e.GetProperty("change").GetInt32());break;
   case "round":a.Component.RoundComponent.Round=e.GetProperty("value").GetInt32();break;
   case "driving":a.Component.RoundComponent.FixtureDriving(e.GetProperty("value").GetBoolean());break;
   case "finish":a.Component.RoundComponent.DoRoundFinish();break;
   case "attempt":if(skill!=null&&skill.CanReleaseByCount()&&view.GetSkillLeftCD(slot)<=0){selected=slot;a.UpdateSkillDataOnRun(new FightSkillComponent{SkillSlot=slot});}break;
   case "run":a.UpdateSkillDataOnRun(new FightSkillComponent{SkillSlot=slot});break;
   case "change":a.ChangeSkillCD(slot,e.GetProperty("value").GetInt32());break;
   case "reset_all":a.ResetAllSkillCD(e.GetProperty("restart").GetBoolean());break;
   case "replay":a.ReplaySkill(slot,e.GetProperty("present").GetBoolean()?new FightSkillComponent():null,EReplaySkillType.None);break;
   case "stop_cd_round":new Stop{mSkillAgent=a.Component}.Run();break;
   case "reduce":skill?.PermanentReduceCd(e.GetProperty("value").GetInt32());break;
   case "reset_cd":skill?.ResetSkillCd();break;
   case "reset_released":skill?.ResetSkillReleasedTime();break;
   case "empty":a.EquipEmptySkill(slot);break;
  }snaps.Add(new{state=a.Snapshot(),notifications=a.events.ToArray(),left=a.Component.SlotSkillItemDatas.Select(x=>new{slot=x.Key,value=view.GetSkillLeftCD(x.Key)}).ToArray(),selected});
 }results.Add(JsonSerializer.Serialize(snaps));}return results.ToArray();
}}}
''']
    def skill(cd, reset=False, limit=-1, last=None, released=0):
        return dict(props={str(cd_id):cd}, reset=reset, limit=limit, last=last, released=released)
    rng = random.Random(854156383)
    cases = []
    explicit = [dict(op='initialize', change=0), dict(op='initialize', change=3),
                dict(op='run', slot=0), dict(op='round', value=1), dict(op='finish'),
                dict(op='round', value=1), dict(op='round', value=2), dict(op='finish'),
                dict(op='change', slot=0, value=-1), dict(op='change', slot=0, value=0),
                dict(op='change', slot=9, value=2), dict(op='stop_cd_round'),
                dict(op='reset_all', restart=True), dict(op='reset_all', restart=False),
                dict(op='replay', slot=0, present=True), dict(op='replay', slot=2, present=False),
                dict(op='replay', slot=-1, present=True), dict(op='replay', slot=99, present=True),
                dict(op='reduce', slot=0, value=99), dict(op='reset_cd', slot=0),
                dict(op='reduce', slot=0, value=-5), dict(op='reset_released', slot=0),
                dict(op='round', value=20), dict(op='driving', value=False), dict(op='empty', slot=1)]
    for i in range(121):
        ss={'0':skill(3, True, 2), '1':None, '2':skill(0, False, 0), '4':skill(5, False, -1, -3, 7)}
        if i:
            for s in ss.values():
                if s is not None:
                    s.update(reset=rng.choice([True,False]), limit=rng.choice([-1,0,1,3]), last=rng.choice([None,-5,0,5,2147483647,-2147483648]), released=rng.choice([0,1,3,2147483647]))
                    s['props'][str(cd_id)]=rng.choice([0,1,3,100,2147483648,4294967295])
            events=[]
            for j in range(35):
                op=rng.choice(['initialize','round','driving','finish','run','change','reset_all','replay','stop_cd_round','reduce','reset_cd','reset_released'])
                slot=rng.choice([0,2,4])
                e=dict(op=op, slot=slot)
                if op=='initialize':e['change']=rng.choice([-3,0,2])
                elif op=='round':e['value']=rng.choice([0,1,2,5,50,2147483647,-2147483648])
                elif op=='driving':e['value']=rng.choice([True,False])
                elif op in ['reduce','change']:e['value']=rng.choice([-2147483648,-100,-1,0,1,100,2147483647])
                elif op=='reset_all':e['restart']=rng.choice([True,False])
                elif op=='replay':e['present']=rng.choice([True,False])
                events.append(e)
        else:events=copy.deepcopy(explicit)
        cases.append(dict(name=f'original-skill-state-{i}',round=0,driving=True,slots=ss,events=events))
    # A local single-skill gate over original cooldown/count callbacks. This
    # does not reconstruct the unavailable server AI or its round scheduler.
    battle_catalog = json.loads((ROOT/'assets/official/battle-rules.json').read_text('utf8'))
    info = battle_catalog['entities']['16560']['FightSkillComponent']
    events = [dict(op='initialize', change=0)]
    for number in range(1, 11):
        events += [dict(op='round', value=number), dict(op='attempt', slot=1), dict(op='finish')]
    execution_cases = [dict(name='tutorial-16560-phase-gate', stage=1088, actor=10, entity=16560, slot=1,
                            round=0, driving=False, slots={'1':skill(1, info['ResetCDAtStart'], info['LimitedTimes'])}, events=events)]
    all_cases = cases + execution_cases
    harness=OUT/'PinnedSkill.cs';harness.write_text('\n'.join(code),'utf8')
    inputs=OUT/'skill-state-input.json';inputs.write_text(json.dumps(all_cases),'utf8')
    runner=OUT/'run-skill-state-reference.ps1'
    def ps(path):return str(path).replace("'","''")
    runner.write_text("$ErrorActionPreference='Stop'\nAdd-Type -Path '"+ps(harness)+"'\n[PinnedSkill.Reference]::Evaluate([IO.File]::ReadAllText('"+ps(inputs)+"'))",'utf8')
    result=subprocess.run(['pwsh','-NoProfile','-File',str(runner)],capture_output=True,text=True,encoding='utf8')
    if result.returncode:raise RuntimeError(result.stdout+result.stderr)
    lines=result.stdout.splitlines()
    if len(lines)!=len(all_cases):raise ValueError('Original skill output count differs')
    for case,line in zip(all_cases,lines):case['expected']=json.loads(line)
    source_files=[SOURCE/name for name in ['FightSkillData.cs','FightSkillAgentComponentImplement.cs','FightStatusSkillStopCdComponent.cs','BattleFormulaHandler.cs','FightRoundComponent.cs']]+[CLIENT/name for name in ['FightSkillListDataEntityProvider.cs','FightSkillListDataMonsterProvider.cs']]
    output=OUT/'skill-state-reference.json'
    output.write_text(json.dumps(dict(manifest='854_156383',cases=cases,execution_cases=execution_cases,original_code_sha256={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in source_files},harness_sha256=hashlib.sha256(harness.read_bytes()).hexdigest(),catalog_sha256=hashlib.sha256((ROOT/'assets/official/skill-rules.json').read_bytes()).hexdigest(),battle_catalog_sha256=hashlib.sha256((ROOT/'assets/official/battle-rules.json').read_bytes()).hexdigest()),ensure_ascii=False,indent=2)+'\n','utf8')
    print('Original skill state reference:',len(cases),'sequences;',len(execution_cases),'local gate trace;',output)


if __name__=='__main__':main()
