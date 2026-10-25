"""Original entity callbacks, target helpers and grid arithmetic reference."""
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
    names=['FightDamageComponentImplement.cs','FightRoleFightComponent.cs','FightStatusAgentComponentImplement.cs','FightSkillComponentImplement.cs','FightStatusUtilsHandler.cs','FightUtilsHandler.cs','FightSkillComponentInfo.cs','GridTransformComponent.cs','MyMath.cs','Pos2d.cs']
    texts={name:(SOURCE/name).read_text('utf8') for name in names}
    catalog=json.loads((ROOT/'assets/official/skill-rules.json').read_text('utf8'))
    max_id=next(int(k) for k,v in catalog['prop_names'].items() if v=='MaxHp')
    code=[r'''
using System;using System.Linq;using System.Collections.Generic;using System.Text.Json;
using ILogger=System.Object;using EcEntityIdentifier=System.Int32;
namespace PinnedEntities{
public class ZLoggerErrorInterpolatedStringHandler{
 public ZLoggerErrorInterpolatedStringHandler(int a,int b,object c,ref bool d){d=false;}
 public void AppendLiteral(string a){}public void AppendFormatted<T>(T a,int b,string c,string d){}
}
public class ZLoggerWarningInterpolatedStringHandler{
 public ZLoggerWarningInterpolatedStringHandler(int a,int b,object c,ref bool d){d=false;}
 public void AppendLiteral(string a){}public void AppendFormatted<T>(T a,int b,string c,string d){}
}
public static class FightLog{public static object Skill=new object(),Role=new object(),Status=new object();}
public static class GZLoggerExtensions{
 public static void Error(object a,ref ZLoggerErrorInterpolatedStringHandler b,object c,string d,string e,int f){}
 public static void Error(object a,Exception x,ref ZLoggerErrorInterpolatedStringHandler b,object c,string d,string e,int f){}
 public static void Warn(object a,ref ZLoggerWarningInterpolatedStringHandler b,object c,string d,string e,int f){}
}
public static class Trace{public static List<object> events=new List<object>();public static void Damage(string name,DamageEventArgs a)=>events.Add(new object[]{name,new object[]{a.DamageId,a.SourceId,a.TargetId,a.RealDamageValue,a.DamageResult.DamageValue}});}
public class EcEntity{public int Id,ClassId;public bool Removing,Ended;public EcContainer Container;public FightRoleFightComponent fight;public FightRoleCharacterComponent character;public FightComponent root;public T GetComponent<T>() where T:class {if(typeof(T)==typeof(FightRoleFightComponent))return fight as T;if(typeof(T)==typeof(FightRoleCharacterComponent))return character as T;return root as T;}}
public class EcContainer{public Dictionary<int,EcEntity> entities=new Dictionary<int,EcEntity>();public EcEntity RootEntity;public EcEntity GetEntity(int id)=>entities.GetValueOrDefault(id);}
public class FightRoleCharacterComponent{public int MasterId;}
public class FightComponent{public void FireDamage(DamageEventArgs a)=>Trace.Damage("root_damage",a);}
public class PropComponent{public Dictionary<PropType,long> FinalProps=new Dictionary<PropType,long>();}
public class BattleFormula{public static BattleFormula Instance=new BattleFormula();public double GetProp(Dictionary<PropType,long> p,PropType t)=>p.GetValueOrDefault(t);}
''',f'public enum PropType{{MaxHp={max_id}}}',
        'public class FightRoleFightComponent{public EcEntity Entity;private long _Hp;public long LastMaxHp;public PropComponent PropComponent=new PropComponent();public void Dirty(){}public Action<FightRoleFightComponent,long> OnHpUpdate,OnHpUpdateByDamag;',
        method(texts['FightRoleFightComponent.cs'],'public long Hp'),
        method(texts['FightRoleFightComponent.cs'],'public void SetHp('),
        r'''
public void InitialHp(long value){_Hp=value;}public void FireBeDamage(DamageEventArgs a)=>Trace.Damage("be_damage",a);public void FireDoHit(HitEventArgs a)=>Trace.events.Add(new object[]{"do_hit",new object[]{Entity.Id,a.TargetId,a.RealDamageValue,a.DamageResult.DamageValue}});public void FireSkillCostByHp(int skill,long old,long next)=>Trace.events.Add(new object[]{"skill_cost",Entity.Id,skill,old,next});
}
public class DamageResultParameter{public long DamageValue;}
public class FightDamageComponent{public EcEntity Entity=new EcEntity();public EcContainer Container=>Entity.Container;public int TargetId,SkillSrcEntityId;public DamageResultParameter DamageResult=new DamageResultParameter();}
public class HitEventArgs{public FightRoleFightComponent BattleComponent;public FightDamageComponent DamageComponent;public int TargetId;public long RealDamageValue;public DamageResultParameter DamageResult;}
public class DamageEventArgs{public int DamageId,SourceId,TargetId;public long RealDamageValue;public DamageResultParameter DamageResult;}
public class DamageImplementation{public FightDamageComponent Component=new FightDamageComponent();
''', '\n'.join(method(texts['FightDamageComponentImplement.cs'],m).replace('protected override void Start(', 'public void Start(').replace('EcEntityIdentifier.sDefault','0') for m in ['protected override void Start(','private (long realDamage, long hp) CalcDamageValue(','private long Damage(','private HitEventArgs GetHitEventArgs(','private DamageEventArgs GetDamageEventArgs(','private void FireEvents(','public void OnAddInClient(']),
        'public object Calc(FightRoleFightComponent target){var x=CalcDamageValue(target);return new{real=x.realDamage,hp=x.hp,kill=target.Hp>0&&x.hp<=0};}}',
        'public class FightStatusAgentComponent{public FightRoleFightComponent Fight;public EcEntity Entity;public Action<int,long,bool> OnStatusDamage;}',
        'public class StatusDamage{public FightStatusAgentComponent Component;',method(texts['FightStatusAgentComponentImplement.cs'],'public void SetStatusDamage('),'}',
        enum('EFightSkillCostType'),enum('StatusTargetType'),enum('SkillTargetType'),enum('HitAnchor'),enum('ESkillMoveDirection'),enum('Direction'),
        'public class FightSkillComponent{public EcEntity SourceEnt;public long CostValue;public EcEntity Entity=new EcEntity();public FightSkillComponentInfo Info=new FightSkillComponentInfo();}',
        'public class Cost{public FightSkillComponent Component;',method(texts['FightSkillComponentImplement.cs'],'private void DoSkillCost('),method(texts['FightSkillComponentImplement.cs'],'private void ReduceCurHpPer('),'public void Run()=>DoSkillCost(Component.SourceEnt);}',
        'public class Targets{',method(texts['FightStatusUtilsHandler.cs'],'public EcEntity GetStatusTargetByType(EcContainer'),method(texts['FightStatusUtilsHandler.cs'],'private EcEntity GetRoleMasterEntity('),method(texts['FightUtilsHandler.cs'],'public bool IsAlive('),'}',
        'public class HitCfg{public HitAnchor ActOnSouce;}public class FightSKillMoveCfg{public bool AIIgnore;public ESkillMoveDirection SkillMoveDirection;public int Index;}',
        'public class FightSkillComponentInfo{public EFightSkillCostType CostType;public bool MustHaveValidCost;public SkillTargetType SkillTargetType;public List<HitCfg> HitList;public List<FightSKillMoveCfg> SourceMoveList;',
        '\n'.join(method(texts['FightSkillComponentInfo.cs'],m) for m in ['public bool RequiresValidCostToCast(','public bool IsEntityTargetType(','public bool IsTrackTargetEntityType(','public bool HasAIPickFacingHit(','public FightSKillMoveCfg GetRelocatingTargetMove(']),'}',
        'public struct Vector2{public float x,y;public Vector2(float x_,float y_){x=x_;y=y_;}}',
        'public struct Pos2d{public int X,Y;public Pos2d(int x,int y){X=x;Y=y;}',method(texts['Pos2d.cs'],'public static Pos2d operator +('),'}',
        'public static class MyMath{',strip_logging(method(texts['MyMath.cs'],'public static Vector2 Rotate(')),'}',
        'public class Grid{',method(texts['GridTransformComponent.cs'],'public static Pos2d CalcLocalToWorldPos('),method(texts['GridTransformComponent.cs'],'public static Pos2d CalcLocalToWorldDir('),'}',r'''
public static class Reference{
public static string[] Evaluate(string input){var results=new List<string>();foreach(var c in JsonDocument.Parse(input).RootElement.EnumerateArray()){
 var container=new EcContainer();container.RootEntity=new EcEntity{root=new FightComponent()};foreach(var pair in c.GetProperty("entities").EnumerateObject()){
  var x=pair.Value;var e=new EcEntity{Id=x.GetProperty("id").GetInt32(),ClassId=x.GetProperty("class_id").GetInt32(),Container=container,Removing=x.GetProperty("removing").GetBoolean(),Ended=x.GetProperty("ended").GetBoolean()};
  if(x.GetProperty("character_component").GetBoolean())e.character=new FightRoleCharacterComponent{MasterId=x.GetProperty("master").GetInt32()};
  if(x.GetProperty("fight_component").GetBoolean()){
   e.fight=new FightRoleFightComponent{Entity=e,LastMaxHp=x.GetProperty("last_max").GetInt64()};e.fight.InitialHp(x.GetProperty("hp").GetInt64());foreach(var p in x.GetProperty("props").EnumerateObject())e.fight.PropComponent.FinalProps[(PropType)int.Parse(p.Name)]=p.Value.GetInt64();
   e.fight.OnHpUpdate=(f,old)=>Trace.events.Add(new object[]{"hp",f.Entity.Id,old});e.fight.OnHpUpdateByDamag=(f,n)=>Trace.events.Add(new object[]{"enemy_hp",f.Entity.Id,n});
  }container.entities[e.Id]=e;
 }
 var snaps=new List<object>();foreach(var e in c.GetProperty("events").EnumerateArray()){
  Trace.events.Clear();object result=null;var target=e.TryGetProperty("target",out var t)?container.GetEntity(t.GetInt32()):null;var source=e.TryGetProperty("source",out var s)?container.GetEntity(s.GetInt32()):null;var damage=new DamageImplementation();damage.Component.Entity.Id=e.TryGetProperty("damage_id",out var d)?d.GetInt32():0;damage.Component.Entity.Container=container;damage.Component.TargetId=target?.Id??0;damage.Component.SkillSrcEntityId=source?.Id??0;if(e.TryGetProperty("value",out var v)&&v.ValueKind==JsonValueKind.Number)damage.Component.DamageResult.DamageValue=v.GetInt64();
  switch(e.GetProperty("op").GetString()){
   case "damage_start":damage.Start();break;case "damage_preview":damage.OnAddInClient(container);break;
   case "calc_damage":if(target?.fight!=null)result=damage.Calc(target.fight);break;
   case "set_hp":target?.fight?.SetHp(e.GetProperty("value").GetInt64(),e.GetProperty("enemy_damage").GetBoolean());break;
   case "status_hp":var comp=new FightStatusAgentComponent{Fight=target?.fight,Entity=target??new EcEntity()};comp.OnStatusDamage=(id,n,cure)=>Trace.events.Add(new object[]{"status_damage",id,n,cure});new StatusDamage{Component=comp}.SetStatusDamage(e.GetProperty("value").GetInt64(),e.GetProperty("cure").GetBoolean());break;
   case "skill_cost":new Cost{Component=new FightSkillComponent{SourceEnt=source,CostValue=e.GetProperty("value").GetInt64(),Entity=new EcEntity{Id=e.GetProperty("skill_id").GetInt32()},Info=new FightSkillComponentInfo{CostType=(EFightSkillCostType)e.GetProperty("cost_type").GetInt32()}}}.Run();break;
   case "alive":result=new Targets().IsAlive(target);break;
   case "status_target":result=new Targets().GetStatusTargetByType(e.GetProperty("container").GetBoolean()?container:null,e.GetProperty("creator").GetInt32(),e.GetProperty("applicator").GetInt32(),(StatusTargetType)e.GetProperty("type").GetInt32())?.Id;break;
  }snaps.Add(new{hp=container.entities.Values.Where(x=>x.fight!=null).ToDictionary(x=>x.Id.ToString(),x=>x.fight.Hp),events=Trace.events.ToArray(),result=result});
 }
 var helpers=new List<object>();foreach(var h in c.GetProperty("helpers").EnumerateArray()){
  var cfg=new FightSkillComponentInfo{CostType=(EFightSkillCostType)h.GetProperty("cost_type").GetInt32(),MustHaveValidCost=h.GetProperty("must_cost").GetBoolean(),SkillTargetType=(SkillTargetType)h.GetProperty("type").GetInt32()};
  var hits=h.GetProperty("hits");if(hits.ValueKind!=JsonValueKind.Null)cfg.HitList=hits.EnumerateArray().Select(x=>x.ValueKind==JsonValueKind.Null?null:new HitCfg{ActOnSouce=(HitAnchor)x.GetProperty("ActOnSouce").GetInt32()}).ToList();
  var moves=h.GetProperty("moves");if(moves.ValueKind!=JsonValueKind.Null)cfg.SourceMoveList=moves.EnumerateArray().Select(x=>x.ValueKind==JsonValueKind.Null?null:new FightSKillMoveCfg{Index=x.GetProperty("Index").GetInt32(),AIIgnore=x.GetProperty("AIIgnore").GetBoolean(),SkillMoveDirection=(ESkillMoveDirection)x.GetProperty("SkillMoveDirection").GetInt32()}).ToList();
  var local=h.GetProperty("local").EnumerateArray().Select(x=>x.GetInt32()).ToArray();var pos=h.GetProperty("pos").EnumerateArray().Select(x=>x.GetInt32()).ToArray();var center=h.GetProperty("center").EnumerateArray().Select(x=>x.GetSingle()).ToArray();var dir=(Direction)h.GetProperty("direction").GetInt32();bool flip=h.GetProperty("flip").GetBoolean();
  var offset=Grid.CalcLocalToWorldDir(new Pos2d(local[0],local[1]),new Vector2(center[0],center[1]),dir,flip);var world=Grid.CalcLocalToWorldPos(new Pos2d(local[0],local[1]),new Pos2d(pos[0],pos[1]),new Vector2(center[0],center[1]),dir,flip);
  helpers.Add(new{entity_target=cfg.IsEntityTargetType(),track_entity=cfg.IsTrackTargetEntityType(),requires_cost=cfg.RequiresValidCostToCast(),facing=cfg.HasAIPickFacingHit(),move=cfg.GetRelocatingTargetMove()?.Index,offset=new int[]{offset.X,offset.Y},world=new int[]{world.X,world.Y}});
 }results.Add(JsonSerializer.Serialize(new{snapshots=snaps,helpers=helpers}));}return results.ToArray();
}}}
''']
    def ent(i,hp,max_hp,last_max=None,fight=True,character=True,master=0,removing=False,ended=False):
        return dict(id=i,class_id=10000+i,hp=hp,props={str(max_id):max_hp},last_max=max_hp if last_max is None else last_max,fight_component=fight,character_component=character,master=master,removing=removing,ended=ended)
    entities={'1':ent(1,80,100,120),'2':ent(2,150,200,180,master=1),'3':ent(3,0,50,master=999),'4':ent(4,10,10,fight=False,character=False),'5':ent(5,10,20,character=False,removing=True),'6':ent(6,10,20,ended=True)}
    explicit=[]
    for value in [-30,-100,0,50,1000]:
        explicit += [dict(op='damage_preview',target=1,source=2,value=value,damage_id=123),dict(op='damage_start',target=1,value=value),dict(op='calc_damage',target=1,value=value)]
    explicit += [dict(op='damage_preview',target=2,source=99,value=-10,damage_id=456),dict(op='damage_start',target=99,value=-1),dict(op='damage_start',target=4,value=-1),dict(op='status_hp',target=1,value=999,cure=True),dict(op='status_hp',target=1,value=-999,cure=False),dict(op='skill_cost',source=2,value=13,skill_id=20,cost_type=1)]
    for target in [1,3,4,5,6,99]:explicit.append(dict(op='alive',target=target))
    for typ in range(7):explicit.append(dict(op='status_target',creator=2,applicator=3,type=typ,container=True))
    explicit.append(dict(op='status_target',creator=2,applicator=1,type=1,container=False))
    rng=random.Random(854156383);cases=[]
    for i in range(101):
        es=copy.deepcopy(entities)
        if i:
            for e in es.values():e['hp']=rng.choice([-1,0,1,100,1000]);e['props'][str(max_id)]=rng.choice([0,1,100,1000]);e['last_max']=rng.choice([0,1,80,1000])
            events=[]
            for j in range(35):
                op=rng.choice(['damage_start','damage_preview','calc_damage','set_hp','status_hp','skill_cost','alive','status_target']);target=rng.choice([1,2,3,5,6]);source=rng.choice([1,2,3,5,6]);e=dict(op=op,target=target,source=source,value=rng.choice([-1000,-1,0,1,1000]),damage_id=100+j,enemy_damage=rng.choice([True,False]),cure=rng.choice([True,False]),cost_type=rng.randrange(0,3),skill_id=20)
                if op=='status_target':e.update(creator=rng.randrange(1,8),applicator=rng.randrange(1,8),type=rng.randrange(0,7),container=rng.choice([True,False]))
                events.append(e)
        else:events=explicit
        helpers=[]
        for j in range(10):
            helpers.append(dict(type=rng.randrange(0,8),cost_type=rng.randrange(0,4),must_cost=rng.choice([True,False]),hits=None if j==0 else [None,dict(ActOnSouce=rng.randrange(0,5))],moves=None if j==0 else [None]+[dict(Index=k,AIIgnore=rng.choice([True,False]),SkillMoveDirection=rng.randrange(0,3)) for k in range(3)],local=[rng.randrange(-5,6),rng.randrange(-5,6)],pos=[rng.randrange(-10,11),rng.randrange(-10,11)],center=[rng.choice([0,0.5,1,1.5,2,0.33333333]),rng.choice([0,0.5,1,1.5,2,0.33333333])],direction=rng.randrange(0,6),flip=rng.choice([True,False])))
        cases.append(dict(name=f'original-entity-state-{i}',entities=es,events=events,helpers=helpers))
    harness=OUT/'PinnedEntities.cs';harness.write_text('\n'.join(code),'utf8')
    inputs=OUT/'entities-input.json';inputs.write_text(json.dumps(cases),'utf8')
    runner=OUT/'run-entities-reference.ps1'
    def ps(path):return str(path).replace("'","''")
    runner.write_text("$ErrorActionPreference='Stop'\nAdd-Type -Path '"+ps(harness)+"'\n[PinnedEntities.Reference]::Evaluate([IO.File]::ReadAllText('"+ps(inputs)+"'))",'utf8')
    result=subprocess.run(['pwsh','-NoProfile','-File',str(runner)],capture_output=True,text=True,encoding='utf8')
    if result.returncode:raise RuntimeError(result.stdout+result.stderr)
    lines=result.stdout.splitlines()
    if len(lines)!=len(cases):raise ValueError('Original entities output count differs')
    for case,line in zip(cases,lines):case['expected']=json.loads(line)
    output=OUT/'entities-reference.json'
    output.write_text(json.dumps(dict(manifest='854_156383',cases=cases,original_code_sha256={n:hashlib.sha256((SOURCE/n).read_bytes()).hexdigest() for n in names},harness_sha256=hashlib.sha256(harness.read_bytes()).hexdigest(),catalog_sha256=hashlib.sha256((ROOT/'assets/official/skill-rules.json').read_bytes()).hexdigest(),identity_stub='EcEntityIdentifier represented by int; sDefault maps to 0; method bodies unchanged otherwise, Start override removed for direct harness call.'),ensure_ascii=False,indent=2)+'\n','utf8')
    print('Original entities reference:',len(cases),'sequences;',output)


if __name__=='__main__':main()
