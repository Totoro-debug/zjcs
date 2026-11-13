"""Compile unchanged official growth/parser methods against pinned local table adapters."""
import argparse
import hashlib
import json

import prepare_battle_damage_reference as original
from official_apk import Apk
from official_tables import Constructors
from validation_workspace import validation_directory


ADAPTERS = r'''
public struct GLoggerErrorInterpolatedStringHandler {
    public GLoggerErrorInterpolatedStringHandler(int a,int b,out bool enabled){enabled=false;}
    public void AppendLiteral(string value){}
    public void AppendFormatted<T>(T value,int a,string b,string c){}
}
public static class Log {
    public static void Error(ref GLoggerErrorInterpolatedStringHandler h,object a,string b,string c,int d){}
}
public class IPropDictionary : Dictionary<PropType,long> {
    public IPropDictionary(Dictionary<PropType,long> data):base(data){}
}
public class DefaultRow { public int ClassId;public long DefaultValue;public PropType[] PropTypes; }
public class LevelPropDefaultValueParser {
    public Dictionary<int,DefaultRow> Rows=new Dictionary<int,DefaultRow>();
    public bool TryGet(int id,out DefaultRow row)=>Rows.TryGetValue(id,out row);
}
public class ConfigManager {
    public T GetParser<T>() where T:class => Parsers.Instance.Defaults as T;
}
public class ParserBase { public ConfigManager ConfigManager=new ConfigManager(); }
public class LevelPropBinaryReader {
    public Dictionary<int,Dictionary<int,Dictionary<PropType,long>>> Rows;
    public bool TryGetProps(int id,int level,out IPropDictionary props){
        props=null;
        if(!Rows.TryGetValue(id,out var rows)||!rows.TryGetValue(level,out var raw))return false;
        props=new IPropDictionary(raw);return true;
    }
    public bool TryGetLevelRange(int id,out int min,out int max){
        min=max=0;if(!Rows.TryGetValue(id,out var rows)||rows.Count==0)return false;
        min=rows.Keys.Min();max=rows.Keys.Max();return true;
    }
}
public partial class LevelPropParser : ParserBase {
    private LevelPropBinaryReader mBinaryReader;
    private Dictionary<(int,int),IPropDictionary> mCache=new Dictionary<(int,int),IPropDictionary>();
    private LevelPropDefaultValueParser mDefaultValueParser;
    public LevelPropParser(LevelPropBinaryReader reader){mBinaryReader=reader;}
}
public partial class GroupLevelPropCfg {
    public int RankPropId,GroupLevelPropId;
    public Dictionary<PropType,long> PropFactors;
}
public class GroupRow { public int LevelPropId; }
public class GroupParser {
    public Dictionary<(int,int),GroupRow> Rows=new Dictionary<(int,int),GroupRow>();
    public GroupRow Get(int group,SubRank subRank){Rows.TryGetValue((group,(int)subRank),out var row);return row;}
}
public class Parsers {
    public static Parsers Instance;
    public LevelPropParser LevelPropParser;
    public LevelPropDefaultValueParser Defaults=new LevelPropDefaultValueParser();
    public GroupParser EntityPropGroupLevelInfoParser=new GroupParser();
}
public static class Reference {
    private static Dictionary<int,GroupLevelPropCfg> entities;
    private static Dictionary<PropType,long> Props(JsonElement row){
        var result=new Dictionary<PropType,long>();
        foreach(var p in row.EnumerateObject())result.Add((PropType)int.Parse(p.Name),p.Value.GetInt64());
        return result;
    }
    public static void Load(string json){
        var root=JsonDocument.Parse(json).RootElement;var parsers=new Parsers();Parsers.Instance=parsers;
        var binary=new LevelPropBinaryReader{Rows=new Dictionary<int,Dictionary<int,Dictionary<PropType,long>>>()};
        foreach(var table in root.GetProperty("props").EnumerateObject()){
            var rows=new Dictionary<int,Dictionary<PropType,long>>();
            foreach(var row in table.Value.GetProperty("rows").EnumerateObject())rows.Add(int.Parse(row.Name),Props(row.Value));
            binary.Rows.Add(int.Parse(table.Name),rows);
        }
        parsers.LevelPropParser=new LevelPropParser(binary);
        foreach(var row in root.GetProperty("defaults").EnumerateArray()){
            var d=new DefaultRow{ClassId=row.GetProperty("ClassId").GetInt32(),DefaultValue=row.GetProperty("DefaultValue").GetInt64(),
                PropTypes=row.GetProperty("PropTypes").EnumerateArray().Select(p=>(PropType)p.GetInt32()).ToArray()};
            parsers.Defaults.Rows.Add(d.ClassId,d);
        }
        foreach(var row in root.GetProperty("groups").EnumerateArray())
            parsers.EntityPropGroupLevelInfoParser.Rows.Add((row.GetProperty("GroupId").GetInt32(),row.GetProperty("SubRank").GetInt32()),
                new GroupRow{LevelPropId=row.GetProperty("LevelPropId").GetInt32()});
        entities=new Dictionary<int,GroupLevelPropCfg>();
        foreach(var row in root.GetProperty("entities").EnumerateArray())
            entities.Add(row.GetProperty("EntityId").GetInt32(),new GroupLevelPropCfg{
                RankPropId=row.GetProperty("RankPropId").GetInt32(),GroupLevelPropId=row.GetProperty("GroupLevelPropId").GetInt32(),
                PropFactors=Props(row.GetProperty("PropFactors"))});
    }
    public static Dictionary<int,long> Evaluate(int entity,int rank,int level,int subRank)
        => new Handler().CalcSkillProps(entities[entity],rank,level,(SubRank)subRank).ToDictionary(p=>(int)p.Key,p=>p.Value);
}
'''


def generate():
    parts = ['using System;using System.Collections.Generic;using System.Linq;using System.Text.Json;\nnamespace SourceGrowthReference {']
    for name in ('PropType', 'SubRank'):
        parts.append(original.original(name).replace('namespace Common;', '').replace('[NotCheckFightResult]', ''))
    parts.extend([ADAPTERS, 'public partial class GroupLevelPropCfg {',
                  original.method('GroupLevelPropCfg', 'public int GetLevelPropId('),
                  '} public partial class LevelPropParser {'])
    for signature in ('public IReadOnlyDictionary<PropType, long> GetProps(',
                      'private IReadOnlyDictionary<PropType, long> GetConfigProps(',
                      'private bool TryGetPropDict(', 'public bool TryGetLevelRange('):
        parts.append(original.method('LevelPropParser', signature))
    parts.append('} public class Handler {')
    for signature in ('public Dictionary<PropType, long> CalcSkillProps(GroupLevelPropCfg',
                      'public Dictionary<PropType, long> CalcSkillProps(int rankPropId, int levelPropId, int rankFactorId',
                      'public Dictionary<PropType, long> ScaleProps(IReadOnlyDictionary',
                      'public void ScaleSrcProps(', 'public double Long2Double(long val)'):
        parts.append(original.method('BattleFormulaHandler', signature))
    parts.extend(['} public static class Formula {', original.method('Formula', 'public static double Long2Double(long val)'), '}}'])
    return '\n'.join(parts)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--record', action='store_true')
    args = parser.parse_args()
    work = validation_directory()
    assert work.parent == original.ROOT / 'output/validation' and (work / '.zjcs-validation.json').is_file()
    code = json.loads((original.CURRENT / 'code-manifest.json').read_bytes())
    assembly = original.CURRENT / 'official-code/managed/decoded/Console.dll'
    assert original.identity(assembly)['sha256'] == code['assemblies']['Console.dll']['decoded_sha256']
    inputs = []
    for name in ('skill-rules', 'battle-rules', 'rules'):
        path = original.CONFIG / (name + '.json')
        proof = json.loads(path.with_name(name + '-source-manifest.json').read_bytes())
        assert original.identity(path)['sha256'] == proof['output_sha256']
        assert proof['manifest'] == '854_156383'
        inputs.append(original.identity(path))
    defaults_proof = json.loads((original.ROOT / 'assets/reference/battle-rules/prop-defaults-source-manifest.json').read_bytes())
    for item in defaults_proof['code']:
        assert original.identity(original.ROOT / item['path'])['sha256'] == item['sha256']
    apk = Apk('884_156692')
    path, pointer = next((path, pointer) for path, pointer in apk.container('config_luban')
                         if path == defaults_proof['asset']['path'])
    obj = pointer.deref()
    raw = obj.read().m_Script.encode('utf-8', 'surrogateescape')
    assert hashlib.sha256(raw).hexdigest() == defaults_proof['input_sha256']
    # The selected APK has a different bundle identity. Verify its actual table
    # bytes against the historical 854 payload, keeping both provenances explicit.
    defaults_source = apk.provenance(obj)
    assert defaults_source['manifest'] == '884_156692'
    defaults = Constructors().table(raw, 'TbLevelPropDefaultValue')
    apk.close()
    skill = json.loads((original.CONFIG / 'skill-rules.json').read_bytes())
    groups = [row for row in skill['tables']['entity_prop_group_level'] if row['GroupId'] == 1]
    entities = [row for row in skill['tables']['entity_prop'] if row['EntityId'] in (10760, 24030)]
    ids = {row['LevelPropId'] for row in groups} | {row['RankPropId'] for row in entities}
    data = dict(props={str(key): skill['props'][str(key)] for key in sorted(ids)},
                groups=groups, entities=entities, defaults=defaults)
    original.USED.clear()
    source = generate()
    (work / 'arc-growth-reference.cs').write_text(source, encoding='utf-8')
    (work / 'arc-growth-input.json').write_text(json.dumps(data), encoding='utf-8')
    receipt = dict(code_manifest=code['manifest'], configuration_manifest='854_156383',
        assembly=original.identity(assembly), code_receipt=original.identity(original.CURRENT / 'code-manifest.json'),
        original=[original.identity(p) for p in sorted(original.USED)], input=inputs,
        defaults=dict(asset=path, source=defaults_source, historical_source=defaults_proof['source'],
                      sha256=defaults_proof['input_sha256']),
        generated_reference_sha256=hashlib.sha256(source.encode()).hexdigest(),
        operation='Execute unchanged original group selection, exact/max-clamped row lookup, absent-key defaults, '
                  'rank scaling, factor scaling and truncation against explicit local table/cache/logging adapters; '
                  'compare arc active/status growth and actual executor status lifecycle. No PVP processor in this comparison.')
    receipt_path = original.ROOT / 'assets/reference/battle-rules/arc-growth-source-manifest.json'
    if args.record:
        receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    else:
        assert json.loads(receipt_path.read_bytes()) == receipt, 'Official growth source identities changed'
    print('Prepared pinned original arc growth and parser methods; APK default table verified.')


if __name__ == '__main__':
    main()
