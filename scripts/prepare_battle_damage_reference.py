"""Evaluate original shared damage methods with pinned local configuration adapters."""
import argparse
import copy
import hashlib
import json
import random
import re
import subprocess
from pathlib import Path

from validation_workspace import validation_directory

ROOT = Path(__file__).resolve().parents[1]
CURRENT = ROOT / '.tools/source-packages/current/884_156692'
SOURCE = CURRENT / 'official-code/common-source/Common'
CONFIG = ROOT / 'unity/Assets/Official/Combat'
USED = set()


def original(name):
    path = SOURCE / (name + '.cs')
    USED.add(path)
    return path.read_text(encoding='utf-8-sig')


def method(name, signature):
    source = original(name)
    start = source.index(signature)
    opening = source.index('{', start)
    # Skip quoted strings, character literals and comments when balancing bodies.
    tokens = re.compile(r'@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'|//[^\n]*|/\*.*?\*/|[{}]', re.S)
    depth = 0
    for token in tokens.finditer(source, opening):
        if token.group() == '{':
            depth += 1
        elif token.group() == '}':
            depth -= 1
            if not depth:
                return source[start:token.end()]
    raise ValueError('Unbalanced original method: ' + signature)


def identity(path):
    raw = path.read_bytes()
    return dict(path=path.relative_to(ROOT).as_posix(), bytes=len(raw),
                sha256=hashlib.sha256(raw).hexdigest())


ADAPTERS = r'''
public class EcEntity { }
public struct DamageResultParameter {
    public DamageTypeData DamageTypeData; public float ElementConfrontation; public bool IsElementAffect;
}
public class GLoggerWarnInterpolatedStringHandler {
    public GLoggerWarnInterpolatedStringHandler(int a,int b,out bool enabled){enabled=false;}
    public void AppendLiteral(string x){} public void AppendFormatted<T>(T x,int a,string b,string c){}
}
public class GLoggerErrorInterpolatedStringHandler {
    public GLoggerErrorInterpolatedStringHandler(int a,int b,out bool enabled){enabled=false;}
    public void AppendLiteral(string x){} public void AppendFormatted<T>(T x,int a,string b,string c){}
}
public class ZLoggerWarningInterpolatedStringHandler {
    public ZLoggerWarningInterpolatedStringHandler(int a,int b,object logger,ref bool enabled){enabled=false;}
    public void AppendLiteral(string x){} public void AppendFormatted<T>(T x,int a,string b,string c){}
}
public static class Log {
    public static void Warn(ref GLoggerWarnInterpolatedStringHandler x,object a,string b,string c,int d){}
    public static void Error(ref GLoggerErrorInterpolatedStringHandler x,object a,string b,string c,int d){}
}
public static class FightLog {public static object Skill=new object();}
public static class GZLoggerExtensions {
    public static void Warn(object l,ref ZLoggerWarningInterpolatedStringHandler x,object a,string b,string c,int d){}
}
public class PropCfgInfo {public int PropType;public bool Float;}
public class FightLevelOffsetDamageInfo {public int LevelOffset,DamageScale;}
public class FightRankOffsetDamageInfo {public int Rank,DamageScale;}
public class DemonBattlefieldFightRankOffsetDamageInfo {public int Rank,DamageScale;}
public class MonsterSpecialPropTypeInfo {public int MonsterId;public PropType DmgAdd;}
public class Table<T> {
    private readonly Dictionary<int,T> rows=new Dictionary<int,T>();
    public Table(JsonElement rows,string key,JsonSerializerOptions options){
        foreach(var row in rows.EnumerateArray())this.rows.Add(row.GetProperty(key).GetInt32(),JsonSerializer.Deserialize<T>(row.GetRawText(),options));
    }
    public T Get<K>(K key){rows.TryGetValue(Convert.ToInt32(key),out var row);return row;}
    public bool TryGet<K>(K key,out T row){return rows.TryGetValue(Convert.ToInt32(key),out row);}
}
public class BattleSettings {
    public int FixedDmgLimitPercent,MaxLevelOffsetDamageScale;
    public float MinCritPowerPercent,MinBlockValuePercent;
}
public class Settings {public BattleSettings Battle;}
public class Parsers {
    public static Parsers Instance;public static Settings GameSettings;
    public Table<PropCfgInfo> PropCfgInfoParser;
    public Table<FightLevelOffsetDamageInfo> FightLevelOffsetDamageInfoParser;
    public Table<FightRankOffsetDamageInfo> FightRankOffsetDamageInfoParser;
    public Table<DemonBattlefieldFightRankOffsetDamageInfo> DemonBattlefieldFightRankOffsetDamageInfoParser;
    public Table<MonsterSpecialPropTypeInfo> MonsterSpecialPropTypeInfoParser;
    public Parsers(JsonElement config,JsonSerializerOptions options){
        Instance=this;GameSettings=JsonSerializer.Deserialize<Settings>(config.GetProperty("game_settings").GetRawText(),options);
        PropCfgInfoParser=new Table<PropCfgInfo>(config.GetProperty("prop_cfg"),"PropType",options);
        var tables=config.GetProperty("tables");
        FightLevelOffsetDamageInfoParser=new Table<FightLevelOffsetDamageInfo>(tables.GetProperty("fight_level_offset_damage"),"LevelOffset",options);
        FightRankOffsetDamageInfoParser=new Table<FightRankOffsetDamageInfo>(tables.GetProperty("fight_rank_offset_damage"),"Rank",options);
        DemonBattlefieldFightRankOffsetDamageInfoParser=new Table<DemonBattlefieldFightRankOffsetDamageInfo>(tables.GetProperty("demon_battlefield_fight_rank_offset_damage"),"Rank",options);
        MonsterSpecialPropTypeInfoParser=new Table<MonsterSpecialPropTypeInfo>(tables.GetProperty("monster_special_prop_type"),"MonsterId",options);
    }
}
public static class Reference {
    public static string[] Evaluate(string input){
        var options=new JsonSerializerOptions{IncludeFields=true};
        var data=JsonDocument.Parse(input).RootElement;new Parsers(data.GetProperty("configuration"),options);
        var handler=new Handler();BattleFormula.Instance=handler;var output=new List<string>();
        foreach(var test in data.GetProperty("cases").EnumerateArray()){
            var info=JsonSerializer.Deserialize<BattleFormula.DamageInfo>(test.GetProperty("info").GetRawText(),options);
            var result=new DamageResultParameter {
                DamageTypeData=JsonSerializer.Deserialize<DamageTypeData>(test.GetProperty("flags").GetRawText(),options),
                ElementConfrontation=test.GetProperty("confrontation").GetSingle()
            };
            var custom=test.GetProperty("custom");
            long absolute;long damage=handler.Damage(info,ref result,out absolute,custom.ValueKind==JsonValueKind.Null?(long?)null:custom.GetInt64());
            output.Add(JsonSerializer.Serialize(new {damage,absolute,element_affect=result.IsElementAffect}));
        }
        return output.ToArray();
    }
}
'''


def reference_source():
    parts = ['using System;using System.Collections.Generic;using System.Text.Json;using UnityEngine;using ILogger=System.Object;\nnamespace SourceDamageReference {']
    for name in ('PropType', 'Rank', 'SubRank', 'Profession', 'StageType', 'MonsterType', 'ElementType', 'GameCharacterType'):
        parts.append(original(name).replace('namespace Common;', '').replace('[NotCheckFightResult]', ''))
    parts.extend((ADAPTERS, 'public static class Extensions {',
                  method('EnumExt', 'public static Rank ToRank(this SubRank'),
                  method('EnumExt', 'public static bool IsLargeMonster(this MonsterType'),
                  '} public static class DoubleUtils {', method('DoubleUtils', 'public static bool IsZero(double a)'),
                  '} public static class Formula {', method('Formula', 'public static double Long2Double(long val)'),
                  '} public class BattleFormula { public static Handler Instance;',
                  method('BattleFormula', 'public enum CustomPropsPhase'),
                  method('BattleFormula', 'public struct DamageInfo'),
                  '}'))
    flags = method('DamageTypeData', 'public struct DamageTypeData')
    parts.append(re.sub(r'\[Key\(\d+\)\]', '', flags))
    parts.append('public class Handler {')
    for signature in (
        'private double OneAddPropWithLimitMinValue(', 'private bool IsTooSmall(',
        'private static void InvokeDamagePhaseProcessor(',
        'public long Damage(', 'private bool IsCombatRatingSuppressionEfective(',
        'private void DamageRankOffsetDamage(', 'private double GetMonsterDmgAddSpecialProp(',
        'public double GetProp(Dictionary<PropType, long> props, PropType type)',
        'public double Long2Double(long val)'):
        parts.append(method('BattleFormulaHandler', signature))
    parts.append('}}')
    return '\n'.join(parts)


def cases_for(skill):
    ids = {v: int(k) for k, v in skill['prop_names'].items()}
    def props(**values):
        return {str(ids[k]): v for k, v in values.items()}
    source = props(Attack=1000, MaxHp=10000, SkillAttack1=12500, BaseElementAdd=100,
                   BaseKongFuMaster=100, BaseElementMaster=100, BaseBlockAvoidPercentValue=100,
                   BaseCritRatePercentValue=100)
    target = props(Defence=300, MaxHp=20000, BaseElementReduce=100, BaseKongFuResistance=100,
                   BaseElementResistance=100, BaseCritAvoidPercentValue=100, BaseBlockPercentValue=100)
    base = dict(SourceProps=source, TargetProps=target, SourceType=1, TargetType=2,
                SourceMasterType=0, TargetMasterType=0, SourceLevel=30, TargetLevel=30,
                SourceSubRank=2, TargetSubRank=2, SourceProfession=1, TargetProfession=10,
                SourceEleType=0, TargetMonsterClassId=0, TargetMonsterType=1, TargetHp=15000,
                SkillDmgBasicPropType=ids['Attack'], SkillDmgPropType=ids['SkillAttack1'],
                SkillDmgFixedPropType=ids['FixedDmgAdd'], Distance=1, IsPvp=False,
                StageType=1, CombatRatingSuppression=0)
    cases = []
    def case(name, changes=None, source_changes=None, target_changes=None, flags=None, custom=None, confrontation=0):
        info = copy.deepcopy(base)
        info.update(changes or {})
        info['SourceProps'].update(props(**(source_changes or {})))
        info['TargetProps'].update(props(**(target_changes or {})))
        cases.append(dict(name=name, info=info, flags=flags or {}, custom=custom, confrontation=confrontation))
    case('baseline')
    for flag in ('IsBlinding', 'IsDodge', 'IsImmunity', 'IsCrit', 'IsBlock'):
        case(flag, flags={flag: True})
    case('critical-and-block', flags=dict(IsCrit=True, IsBlock=True))
    for n in (0, 1, -17, 700):
        case('custom-' + str(n), custom=n)
    case('zero-attack', source_changes=dict(Attack=0))
    case('zero-attack-custom', source_changes=dict(Attack=0), custom=700)
    case('skill-fixed-component', source_changes=dict(FixedDmgAdd=71))
    case('skill-max-hp-basic', dict(SkillDmgBasicPropType=ids['MaxHp']))
    case('critical-flat-and-percent', source_changes=dict(CritPower=71, CritPowerPercent=17000), flags=dict(IsCrit=True))
    case('block-flat-and-percent', target_changes=dict(BlockValue=71, BlockValuePercent=17000), flags=dict(IsBlock=True))
    case('zero-element-bases', source_changes=dict(BaseElementAdd=0), target_changes=dict(BaseElementReduce=0))
    case('fixed-reduction-floor', target_changes=dict(DmgReduce=1000000, DmgReducePercent=-20000, StatusDmgReducePer=-20000))
    # Probe every property actually read by the original damage method on both sides.
    damage = method('BattleFormulaHandler', 'public long Damage(')
    property_names = sorted(set(re.findall(r'PropType\.(\w+)', damage)))
    for name in property_names:
        if name in ids:
            for side in ('source', 'target'):
                case(side + '-' + name, **{side + '_changes': {name: 2500}})
    for element in (0, 100, 101, 102, 103, 104, 200):
        for kind in range(10):
            case('element-type-' + str(element) + '-' + str(kind), dict(SourceEleType=element, SourceType=kind, TargetType=9-kind,
                 SourceMasterType=1 if kind == 3 else 0, TargetMasterType=4 if kind == 6 else 0),
                 source_changes=dict(FireDamageAdd=40, WindDamageAdd=30, FinalCharacterDamageScale=3000,
                 FinalSummonDamageScale=2500, FinalPetEntityDamageScale=2000))
    for profession in range(39):
        case('profession-' + str(profession), dict(SourceProfession=profession, TargetProfession=profession),
             source_changes=dict(ZhanshiDmgAddScale=1000, HuweiDmgAddScale=2000, DoushiDmgAddScale=3000,
                                 FashiDmgAddScale=4000, ShushiDmgAddScale=5000, XianzheDmgAddScale=6000),
             target_changes=dict(ZhanshiDmgReduceScale=1000, HuweiDmgReduceScale=2000, DoushiDmgReduceScale=3000,
                                 FashiDmgReduceScale=4000, ShushiDmgReduceScale=5000, XianzheDmgReduceScale=6000))
    for rank in (0, 1, 2, 5, 8, 11, 14, 20, 23, 41, 68, 77, 85):
        for pvp in (False, True):
            case('rank-' + str(rank) + '-' + str(pvp), dict(SourceSubRank=rank, TargetSubRank=8, StageType=3, IsPvp=pvp))
    for level in (-200, -10, -1, 0, 1, 10, 200):
        case('level-' + str(level), dict(SourceLevel=300+level, TargetLevel=300))
    for monster in range(6):
        case('monster-size-' + str(monster), dict(TargetMonsterType=monster),
             source_changes=dict(SkillDmgAddPerByLargeTarget=2500, SkillDmgAddPerByTargetHp=1000))
    for hp in (20000, 17001, 17000, 1, 0):
        case('target-hp-boundary-' + str(hp), dict(TargetHp=hp), source_changes=dict(
            SkillTargetReduceHpPer=1500, SkillDmgUnitAddPer=1000, SkillDmgMaxAddPer=5000))
    for row in skill.get('_monster_special', []):
        name = skill['prop_names'][str(row['DmgAdd'])]
        case('monster-special-' + str(row['MonsterId']), dict(TargetMonsterClassId=row['MonsterId']), source_changes={name: 2500})
    rng = random.Random(884156692)
    for i in range(96):
        case('combined-' + str(i), dict(SourceLevel=rng.randrange(1, 200), TargetLevel=rng.randrange(1, 200),
             SourceSubRank=rng.choice([1, 2, 5, 8, 11, 14]), TargetSubRank=rng.choice([1, 2, 5, 8, 11, 14]),
             SourceType=rng.randrange(1, 8), TargetType=rng.randrange(1, 8), SourceEleType=rng.choice([0, 100, 101, 102, 103, 104]),
             StageType=rng.choice([1, 3, 4]), IsPvp=bool(i%2), Distance=rng.randrange(10),
             CombatRatingSuppression=rng.choice([-.3, 0, .4])),
             source_changes=dict(Attack=rng.randrange(1, 10000), SkillAttack1=rng.randrange(1, 40000),
             DmgAddPercent=rng.randrange(-3000, 10000), StatusDmgAddPer=rng.randrange(-1000, 5000),
             StatusIgnoreDefence=rng.randrange(0, 12000), FixedStatusIgnoreDefence=rng.randrange(100),
             SkillTargetReduceHpPer=1500, SkillDmgUnitAddPer=1000, SkillDmgMaxAddPer=5000,
             DistanceAddDmgPercent=1000, DistanceAddDmg=13, CritPowerPercent=15000),
             target_changes=dict(Defence=rng.randrange(10000), StatusDmgVulnerablePer=rng.randrange(-3000, 5000),
             StatusDmgReducePer=rng.randrange(-15000, 10000)),
             flags=dict(IsCrit=i%3==0, IsBlock=i%5==0), confrontation=rng.choice([0, .2, -.3]))
    return cases


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--unity-core', type=Path, default=Path('D:/Tool/Unity/Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.dll'))
    parser.add_argument('--record', action='store_true', help='Write the persistent source-derived fixture after evaluation.')
    args = parser.parse_args()
    code_manifest = json.loads((CURRENT / 'code-manifest.json').read_bytes())
    assembly = CURRENT / 'official-code/managed/decoded/Console.dll'
    assert code_manifest['manifest'] == '884_156692'
    assert identity(assembly)['sha256'] == code_manifest['assemblies']['Console.dll']['decoded_sha256']
    work = validation_directory()
    assert work.parent == ROOT / 'output/validation' and (work / '.zjcs-validation.json').is_file()
    rules = json.loads((CONFIG / 'rules.json').read_bytes())
    skill = json.loads((CONFIG / 'skill-rules.json').read_bytes())
    battle = json.loads((CONFIG / 'battle-rules.json').read_bytes())
    skill['_monster_special'] = battle['tables']['monster_special_prop_type']
    cases = cases_for(skill)
    source = work / 'original-damage-reference.cs'
    source.write_text(reference_source(), encoding='utf-8')
    inputs = work / 'original-damage-input.json'
    inputs.write_text(json.dumps(dict(cases=cases, configuration=dict(game_settings=rules['game_settings'],
        prop_cfg=rules['prop_cfg'], tables=battle['tables']))), encoding='utf-8')
    def quote(path):
        return str(path).replace("'", "''")
    runner = work / 'run-original-damage-reference.ps1'
    runner.write_text("$ErrorActionPreference='Stop'\n[Reflection.Assembly]::LoadFrom('" + quote(args.unity_core) + "') | Out-Null\n"
        "$refs=@((Get-ChildItem (Join-Path $PSHOME 'ref') -Filter '*.dll').FullName) + @('" + quote(args.unity_core) + "')\n"
        "Add-Type -Path '" + quote(source) + "' -ReferencedAssemblies $refs\n"
        "[SourceDamageReference.Reference]::Evaluate([IO.File]::ReadAllText('" + quote(inputs) + "'))\n", encoding='utf-8')
    process = subprocess.run(['pwsh', '-NoProfile', '-File', str(runner)], capture_output=True, text=True, encoding='utf-8')
    (work / 'original-damage-reference.log').write_text(process.stdout + process.stderr, encoding='utf-8')
    if process.returncode:
        raise RuntimeError(process.stderr[-8000:])
    lines = process.stdout.splitlines()
    assert len(lines) == len(cases), (len(lines), len(cases))
    for case, line in zip(cases, lines):
        case['expected'] = json.loads(line)
    record = dict(code_manifest='884_156692', configuration_manifest='854_156383', cases=cases,
        original_code=[identity(p) for p in sorted(USED)], assembly=identity(assembly),
        code_receipt=identity(CURRENT / 'code-manifest.json'),
        input=[identity(CONFIG / p) for p in ('rules.json', 'skill-rules.json', 'battle-rules.json')],
        unity_core_sha256=hashlib.sha256(args.unity_core.read_bytes()).hexdigest(),
        harness_sha256=hashlib.sha256(source.read_bytes()).hexdigest(), generator=identity(Path(__file__).resolve()),
        operation='Run unmodified original Damage and its called shared methods; adapters provide pinned table lookup, fields and disabled logging. '
                  'Mathf.Approximately executes the local Unity engine method. The original empty custom phase processor stays empty; '
                  'server conditional modifiers, random damage type selection and settlement are outside this fixture.')
    encoded = json.dumps(record, ensure_ascii=False, indent=2) + '\n'
    (work / 'damage-reference.json').write_text(encoded, encoding='utf-8')
    if args.record:
        (ROOT / 'assets/reference/battle-rules/damage-reference.json').write_text(encoded, encoding='utf-8')
    print('Original C# damage reference:', len(cases), 'cases')


if __name__ == '__main__':
    main()
