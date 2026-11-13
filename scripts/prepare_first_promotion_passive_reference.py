"""Compile original passive-growth arithmetic against explicit local table adapters."""
import argparse
import hashlib
import json
from pathlib import Path

from prepare_battle_damage_reference import method, identity, SOURCE
from validation_workspace import validation_directory
from official_apk import Apk
from official_tables import Constructors

ROOT = Path(__file__).resolve().parents[1]


def dictionary(values):
    return 'new Dictionary<PropType,long>{' + ','.join(
        '{(PropType)%s,%sL}' % (key, value) for key, value in values.items()) + '}'


def generate():
    combat = ROOT / 'unity/Assets/Official/Combat'
    rules = json.loads((combat / 'rules.json').read_bytes())
    config = json.loads((combat / 'skill-rules.json').read_bytes())
    # The 854 growth payload has a separate default-value table. Decode that
    # original table independently; never use the migrated defaults as oracle.
    apk = Apk()
    try:
        obj = apk.asset('config_luban', '/level_prop_default_value.g.bytes')
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        receipt = json.loads((ROOT / 'assets/reference/battle-rules/prop-defaults-source-manifest.json').read_bytes())
        assert hashlib.sha256(raw).hexdigest() == receipt['input_sha256']
        defaults = Constructors().table(raw, 'TbLevelPropDefaultValue')
    finally:
        apk.archive.close()
    scope = {item for row in rules['profession_base'] if row['Profession'] in (1, 2, 6, 10, 11, 15)
             for item in row['Skills'] + row['FightUsedSkills']}
    skills = [row for row in rules['skill'] if row['ClassId'] in scope and row['PassiveGroupLevelPropId']]
    assert len(skills) == 47
    groups = config['tables']['entity_prop_group_level']
    wanted_groups = {row['PassiveGroupLevelPropId'] for row in skills}
    groups = [row for row in groups if row['GroupId'] in wanted_groups]
    wanted = {row['LevelPropId'] for row in groups}
    wanted.update(row[key] for row in skills for key in ('PassiveRankPropId', 'PassiveRankFactorId') if row[key])
    parts = [r'''using System; using System.Linq; using System.Collections.Generic;
using Zjcs.Combat.Official;
namespace SourcePassiveGrowth {
public enum PropType { None }
public struct GLoggerErrorInterpolatedStringHandler {
    public GLoggerErrorInterpolatedStringHandler(int a,int b,out bool enabled) { enabled=false; }
    public void AppendLiteral(string value) {}
    public void AppendFormatted<T>(T value,int alignment,string format,string expression) {}
}
public static class Log {
    public static void Error(ref GLoggerErrorInterpolatedStringHandler h,object a,string b,string c,int d) {}
}
// Only data lookup and logging are adapted. Arithmetic below is original source.
public sealed class LevelPropParser {
    public readonly Dictionary<int,SortedDictionary<int,Dictionary<PropType,long>>> Tables =
        new Dictionary<int,SortedDictionary<int,Dictionary<PropType,long>>>();
    public IReadOnlyDictionary<PropType,long> GetProps(int id,int level) {
        if (!Tables.TryGetValue(id,out var rows)) return null;
        if (rows.TryGetValue(level,out var exact)) return exact;
        return level >= rows.Keys.Last() ? rows.Values.Last() : null;
    }
}
public sealed class Parsers {
    public static readonly Parsers Instance = new Parsers();
    public readonly LevelPropParser LevelPropParser = new LevelPropParser();
}
public sealed class Reference {
''']
    for signature in (
        'public Dictionary<PropType, long> CalcSkillProps(int rankPropId,',
        'public Dictionary<PropType, long> ScaleProps(IReadOnlyDictionary<PropType, long> props,',
        'public void ScaleSrcProps(Dictionary<PropType, long> props,',
        'public double Long2Double(long val)'):
        parts.append(method('BattleFormulaHandler', signature))
    parts.append('}\npublic static class Formula {')
    parts.append(method('Formula', 'public static double Long2Double(long val)'))
    parts.append('}\npublic static class Validation {\nstatic void Configure() {')
    for table_id in sorted(wanted):
        table = config['props'][str(table_id)]
        # Keep full rows: the oracle must not ask the migrated calculator for inputs.
        parts.append('Parsers.Instance.LevelPropParser.Tables.Add(%s,new SortedDictionary<int,Dictionary<PropType,long>>{' % table_id)
        for level, values in table['rows'].items():
            values = dict(values)
            for default in defaults:
                if default['ClassId'] == table_id:
                    for prop in default['PropTypes']:
                        values.setdefault(str(prop), default['DefaultValue'])
            parts.append('{%s,%s},' % (level, dictionary(values)))
        parts.append('});')
    parts.append(r'''}
static int Check(FightOfficialCombatCatalog catalog, int item,int rankId,int groupId,int factorId,
    Dictionary<PropType,long> factors,int[] subRanks,int[] levelIds) {
    int count=0;
    var rawFactors=factors.ToDictionary(p=>(int)p.Key,p=>p.Value);
    for(int i=0;i<subRanks.Length;i++) {
        var table=Parsers.Instance.LevelPropParser.Tables[levelIds[i]];
        int max=table.Keys.Last();
        foreach(int rank in new[]{1,2,3,5,10})
        foreach(int level in new[]{1,2,19,20,max-1,max,max+1}.Distinct()) {
            if(level<max && !table.ContainsKey(level)) continue;
            var expected=new Reference().CalcSkillProps(rankId,levelIds[i],factorId,factors,rank,level);
            var actual=catalog.CalculatePassiveSkillProps(rankId,groupId,factorId,rawFactors,rank,level,subRanks[i]);
            if(actual.Count!=expected.Count || expected.Any(p=>!actual.TryGetValue((int)p.Key,out var v)||v!=p.Value))
                throw new Exception("Passive growth differs: "+item+"/"+rank+"/"+level+"/"+subRanks[i]
                    +" expected="+string.Join(",",expected.Select(p=>(int)p.Key+":"+p.Value))
                    +" actual="+string.Join(",",actual.Select(p=>p.Key+":"+p.Value)));
            // Returned maps must not alias immutable table rows or another calculation.
            var snapshot=actual.ToDictionary(p=>p.Key,p=>p.Value);
            if(actual is IDictionary<int,long> mutable && mutable.Count>0) mutable[mutable.Keys.First()]=-999;
            var again=catalog.CalculatePassiveSkillProps(rankId,groupId,factorId,rawFactors,rank,level,subRanks[i]);
            if(again.Count!=snapshot.Count || snapshot.Any(p=>!again.TryGetValue(p.Key,out var v)||v!=p.Value))
                throw new Exception("Passive growth mutated its inputs: "+item);
            count++;
        }
    }
    return count;
}
public static int Run(FightOfficialCombatCatalog catalog) { Configure(); int count=0;
''')
    for row in skills:
        mappings = [g for g in groups if g['GroupId'] == row['PassiveGroupLevelPropId']]
        assert mappings
        parts.append('count+=Check(catalog,%s,%s,%s,%s,%s,new int[]{%s},new int[]{%s});' % (
            row['ClassId'], row['PassiveRankPropId'], row['PassiveGroupLevelPropId'], row['PassiveRankFactorId'],
            dictionary(row['PassivePropFactors']), ','.join(str(g['SubRank']) for g in mappings),
            ','.join(str(g['LevelPropId']) for g in mappings)))
    parts.append('return count; } } }')
    return '\n'.join(parts)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    output = args.output.resolve()
    assert output == validation_directory() and (output / '.zjcs-validation.json').is_file()
    code_manifest = ROOT / '.tools/source-packages/current/884_156692/code-manifest.json'
    receipt = json.loads(code_manifest.read_bytes())
    assembly = SOURCE.parents[1] / 'managed/decoded/Console.dll'
    assert identity(assembly)['sha256'] == receipt['assemblies']['Console.dll']['decoded_sha256']
    source = generate()
    (output / 'passive-growth-reference.cs').write_text(source, encoding='utf8')
    print('Prepared selected 884 original growth methods; reference SHA-256 ' + hashlib.sha256(source.encode()).hexdigest())


if __name__ == '__main__':
    main()
