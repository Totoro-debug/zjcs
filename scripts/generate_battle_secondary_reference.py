"""Run pinned C# healing, status, HP cost and secondary battle formulas."""
import hashlib
import json
import random
import subprocess
from pathlib import Path
from generate_battle_reference import SOURCE, ROOT, method, strip_logging, enum


def main():
    original=(SOURCE/'BattleFormulaHandler.cs').read_text('utf8')
    signatures={
        'cure':'public long Cure(', 'suck_hp':'public long SuckHp(',
        'suck_hp_once':'public long SuckHpByOnceHit(', 'status_damage':'public long StatusDamage(',
        'skill_falloff':'public long SkillDamageFalloff(', 'resilience_change':'public int CalcResilienceChangeValue(',
        'resilience_broken_damage':'public long ResilienceBrokenDamage(', 'skill_cost_hp':'public long CalcSkillCostHp(',
        'shield_to_hp':'public long CalcConvertShieldConvertToHp(', 'effect_rate':'public double EffectRate(',
        'repulse_damage':'public long CalcRepulseDamage(', 'true_damage':'public long CalcTrueDamage(',
        'rebound_damage':'public long ReboundDamage(', 'skill_cd':'public long SkillCD(',
    }
    methods='\n'.join(strip_logging(method(original,marker)) for marker in [
        'private double CalcCureAdd(', 'private double ApplyCureAddSoftCap(', *signatures.values()])
    rules=json.loads((ROOT/'assets/official/rules.json').read_text('utf8'))
    settings=rules['game_settings']['Battle']
    skill=json.loads((ROOT/'assets/official/skill-rules.json').read_text('utf8'))
    ids={v:int(k) for k,v in skill['prop_names'].items()}
    def props(**values):return {str(ids[k]):v for k,v in values.items()}
    base=props(MaxHp=1000,Attack=100,BaseCureAdd=100,BaseBeCureAdd=100,BaseEffectRate=100,BaseEffectDodge=100)
    def with_props(**values):return dict(base,**props(**values))
    cases=[]
    def case(name,operation,*args):cases.append(dict(name=name,method=operation,args=list(args)))
    for prop in ['SkillCureByHp','SkillCureByAttack','SkillCureByTargetHp']:
        for bonus in [0,10000,300000]:
            case(prop+'-'+str(bonus),'cure',with_props(**{prop:4500,'SkillFixedCure':17,'CureAddPercent':bonus,'FinalCureScale':1500}),
                 with_props(MaxHp=2500,BeCureAddPercent=2500),ids[prop],ids['SkillFixedCure'],33)
    case('negative-cure-absolute','cure',with_props(SkillFixedCure=-200),base,ids['None'],ids['SkillFixedCure'],None)
    case('cure-add-value-denominators','cure',with_props(SkillCureByHp=5000,CureAdd=50),with_props(BeCureAdd=20),ids['SkillCureByHp'],ids['SkillFixedCure'],None)
    for type_,master in [(1,0),(4,0),(7,0),(3,1)]:
        info=dict(SourceProp=with_props(PlayerSkillDmgReduceScale=5000,ProSkillDmgReduceScale=2500),
                  SourceType=type_,SourceMasterType=master,SkillProps=props(SuckHpByDamage=2500,FixedSuckHp=20),
                  SuckPropType=ids['SuckHpByDamage'],FixedSuckPropType=ids['FixedSuckHp'],Damage=-211,
                  DamageSkillProps=props(SkillAttack1=17500),DamageHitPropType=ids['SkillAttack1'])
        case('suck-hp-type-'+str(type_)+'-'+str(master),'suck_hp',info)
    case('once-hit-suck-hp','suck_hp_once',-201,with_props(SuckHpByDamage=2500,CureAddPercent=10000),ids['SuckHpByDamage'])
    for maximum in [-1,0,50]:case('status-cap-'+str(maximum),'status_damage',props(DamageByDamage=5000,FixedDamageByDamage=7),ids['DamageByDamage'],ids['FixedDamageByDamage'],-211,maximum)
    for percent in [0.0,.1,.33333333,1.0]:case('falloff-'+str(percent),'skill_falloff',-1003,percent,3)
    for hp,maxhp in [(1,1000),(100,1000),(0,1000),(100,0)]:
        case('hp-cost-'+str(hp)+'-'+str(maxhp),'skill_cost_hp',props(SkillCostByCurHp=5000,SkillCostByMaxHp=2500),hp,maxhp)
    case('negative-cost-ignored','skill_cost_hp',props(SkillCostByCurHp=-5000),100,1000)
    case('empty-cost','skill_cost_hp',{},100,1000)
    for initial,rest,total in [(100,50,100),(16777219,301,1000),(100,50,0)]:case('shield-hp-'+str(initial)+'-'+str(total),'shield_to_hp',initial,rest,total)
    for kind in [0,1,2,3]:
        case('effect-type-'+str(kind),'effect_rate',with_props(BaseEffectRate=90,EffectRate=30,EffectRateValue=500,AbnormalDebuffRateScale=2000,DebuffRateScale=5000),
             with_props(BaseEffectDodge=100,EffectDodge=40,EffectDodgeValue=300),.7,kind)
    case('effect-reduction-floor','effect_rate',base,with_props(EffectDodge=-500),.7,3)
    for shield in [0,50,300]:case('true-damage-'+str(shield),'true_damage',-200,shield)
    case('positive-true-damage','true_damage',100,50)
    case('repulse-cap','repulse_damage',base,with_props(MaxHp=100000))
    case('repulse-hp-limit','repulse_damage',with_props(Attack=10000),base)
    case('rebound','rebound_damage',base,base,-211)
    case('resilience-minimum','resilience_change',base,base,.3,10000)
    case('resilience-break','resilience_change',with_props(BreakResilience=71,BreakResiliencePercent=5000),base,.3,7500)
    case('broken-resilience-cap','resilience_broken_damage',base,base,ids['MaxHp'],.5,ids['Attack'],.6)
    case('original-cooldown','skill_cd',props(CD=3))
    seeded=random.Random(854156383)
    for i in range(24):
        case('healing-combined-'+str(i),'cure',with_props(Attack=seeded.randint(1,10000),SkillCureByAttack=seeded.randint(0,30000),
            SkillFixedCure=seeded.randint(-100,1000),CureAdd=seeded.randint(0,500),CureAddPercent=seeded.randint(-3000,300000),
            FinalCureScale=seeded.randint(-3000,10000)),with_props(BeCureAdd=seeded.randint(0,500)),ids['SkillCureByAttack'],ids['SkillFixedCure'],None)

    def cs(value,kind='long'):
        if value is None:return 'null'
        if isinstance(value,dict):return 'new Dictionary<PropType,long>{'+','.join('{(PropType)'+k+','+str(v)+'L}' for k,v in value.items())+'}'
        return '('+kind+')'+repr(value)
    types={
        'cure':['dict','dict','PropType','PropType','long?'], 'suck_hp':['BattleFormula.SuckHpInfo'],
        'suck_hp_once':['long','dict','PropType'], 'status_damage':['dict','PropType','PropType','long','long'],
        'skill_falloff':['long','float','int'], 'resilience_change':['dict','dict','double','long'],
        'resilience_broken_damage':['dict','dict','PropType','double','PropType','double'],
        'skill_cost_hp':['dict','long','long'], 'shield_to_hp':['long','long','long'],
        'effect_rate':['dict','dict','float','StatusType'], 'repulse_damage':['dict','dict'],
        'true_damage':['long','long'], 'rebound_damage':['dict','dict','long'], 'skill_cd':['dict'],
    }
    run=[]
    for i,vector in enumerate(cases):
        op=vector['method'];args=[]
        for value,kind in zip(vector['args'],types[op]):
            if op=='suck_hp':
                fields={k:('GameCharacterType' if k in ['SourceType','SourceMasterType'] else 'PropType' if k.endswith('PropType') else 'long') for k in value}
                args.append('new BattleFormula.SuckHpInfo{'+','.join(k+'='+cs(v,fields[k]) for k,v in value.items())+'}')
            else:args.append(cs(value,kind))
        name=signatures[op].split(' ')[2].split('(')[0]
        if op=='cure':
            args.insert(4,'out absolute'+str(i));run.append('long absolute'+str(i)+';')
        run.append('var result'+str(i)+'=handler.'+name+'('+','.join(args)+');')
        result='result'+str(i)
        if op=='cure':result='new long[]{'+result+',absolute'+str(i)+'}'
        run.append('results.Add(System.Text.Json.JsonSerializer.Serialize('+result+'));')
    floats=[str(int(r['PropType'])) for r in rules['prop_cfg'] if r['Float']]
    settings_code='public long CureAddSoftCapThreshold='+str(settings['CureAddSoftCapThreshold'])+'L,CureAddOverflowScale='+str(settings['CureAddOverflowScale'])+'L;public float TrueDamageAddPerWhenHasShield='+repr(settings['TrueDamageAddPerWhenHasShield'])+'f;'
    code='using System;using System.Collections.Generic;\nnamespace PinnedSecondaryReference {\n'+'\n'.join(enum(n) for n in ['PropType','GameCharacterType','StatusType'])
    code+='\npublic class BattleFormula {'+method((SOURCE/'BattleFormula.cs').read_text('utf8'),'public struct SuckHpInfo')+'}\n'
    code+='public static class Formula {'+method((SOURCE/'Formula.cs').read_text('utf8'),'public static double Long2Double(')+'}\n'
    code+='public static class MathUtils {'+method((SOURCE/'MathUtils.cs').read_text('utf8'),'public static double Clamp(')+'}\n'
    code+='public class BattleSettings {'+settings_code+'}public class Settings {public BattleSettings Battle=new BattleSettings();}public static class Parsers {public static Settings GameSettings=new Settings();}\n'
    code+='public class Handler {private static HashSet<int> FloatProps=new HashSet<int>{'+','.join(floats)+'};\n'
    code+='public double GetProp(Dictionary<PropType,long> p,PropType t){long v;if(p==null||!p.TryGetValue(t,out v))v=0;return FloatProps.Contains((int)t)?v/10000.0:v;}\n'
    code+='public double Long2Double(long v){return v/10000.0;}private double OneAddPropWithLimitMinValue(Dictionary<PropType,long> p,PropType t,double min){return Math.Max(1+GetProp(p,t),min);}\n'+methods+'}\n'
    code+='public static class Reference {public static string[] Evaluate(){var handler=new Handler();var results=new List<string>();\n'+'\n'.join(run)+'\nreturn results.ToArray();}}}'
    folder=ROOT/'.tools/battle-reference';folder.mkdir(exist_ok=True)
    source=folder/'PinnedSecondaryReference.cs';source.write_text(code,'utf8')
    script=folder/'run-secondary.ps1';script.write_text("$ErrorActionPreference='Stop'\nAdd-Type -Path '"+str(source).replace("'","''")+"'\n[PinnedSecondaryReference.Reference]::Evaluate()\n",'utf8')
    output=subprocess.run(['pwsh','-NoProfile','-File',str(script)],capture_output=True,text=True,encoding='utf8')
    if output.returncode:raise RuntimeError(output.stdout+output.stderr)
    lines=output.stdout.splitlines()
    if len(lines)!=len(cases):raise ValueError('Original C# secondary result count differs')
    for vector,line in zip(cases,lines):vector['expected']=json.loads(line)
    sources=[SOURCE/(n+'.cs') for n in ['BattleFormulaHandler','BattleFormula','BattleSettings','Formula','MathUtils','PropType','StatusType','GameCharacterType']]
    record=dict(manifest='854_156383',cases=cases,original_code={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sources},
        input_sha256={name:hashlib.sha256((ROOT/name).read_bytes()).hexdigest() for name in ['assets/official/rules.json','assets/official/skill-rules.json']},
        harness_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),generator_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest())
    (ROOT/'assets/reference/battle-rules/secondary-reference.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n','utf8')
    print('Pinned C# secondary battle reference:',len(cases),'cases')


if __name__=='__main__':main()
