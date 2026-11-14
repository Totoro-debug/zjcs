"""Compare selected original growth inputs and compile unchanged shared formulas."""
import argparse
import hashlib
import json
import re
from pathlib import Path

import prepare_battle_arc_growth_reference as growth
import prepare_battle_damage_reference as original
from official_apk import Apk
from official_tables import Constructors
from prepare_unity_character_catalog import decode_level_props
from validation_workspace import validation_directory


PASSIVE = r'''
namespace SourceGrowthReference {
public static partial class Reference {
    public static Dictionary<int,long> EvaluatePassive(int rankId,int groupId,int factorId,
        Dictionary<int,long> factors,int rank,int level,int subRank) {
        int table=new GroupLevelPropCfg{GroupLevelPropId=groupId}.GetLevelPropId((SubRank)subRank);
        return new Handler().CalcSkillProps(rankId,table,factorId,
            factors.ToDictionary(p=>(PropType)p.Key,p=>p.Value),rank,level).ToDictionary(p=>(int)p.Key,p=>p.Value);
    }
}
}
'''


def normalized(value):
    return json.loads(json.dumps(value))


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--record', action='store_true')
    args = parser.parse_args()
    work = validation_directory()
    assert work.parent == original.ROOT / 'output/validation' and (work / '.zjcs-validation.json').is_file()
    proof_path = original.ROOT / 'assets/reference/battle-rules/first-promotion-source-manifest.json'
    scope = json.loads(proof_path.read_bytes())
    assert scope['manifest'] == '884_156692' and len(scope['skill_item_ids']) == 104
    assert len(scope['configured_entity_ids']) == 383
    config = json.loads((original.CONFIG / 'skill-rules.json').read_bytes())
    rules = json.loads((original.CONFIG / 'rules.json').read_bytes())
    runtime_entities = [row for row in config['tables']['entity_prop']
                        if row['EntityId'] in scope['configured_entity_ids']]
    runtime_skills = [row for row in rules['skill'] if row['ClassId'] in scope['skill_item_ids']]
    assert len(runtime_entities) == 128 and len(runtime_skills) == 104
    wanted_groups = {row['GroupLevelPropId'] for row in runtime_entities}
    wanted_groups.update(row['PassiveGroupLevelPropId'] for row in runtime_skills)
    runtime_groups = [row for row in config['tables']['entity_prop_group_level'] if row['GroupId'] in wanted_groups]
    assembly = original.CURRENT / 'official-code/managed/decoded/Console.dll'
    code_receipt = original.CURRENT / 'code-manifest.json'
    code = json.loads(code_receipt.read_bytes())
    assert code['manifest'] == '884_156692'
    assert original.identity(assembly)['sha256'] == code['assemblies']['Console.dll']['decoded_sha256']
    tables, inputs, differences = {}, {}, []
    with Apk.current() as apk:
        assert apk.version == scope['manifest']
        constructors = Constructors(original.CURRENT / 'official-code')
        mapping = {name: cls for cls, name in re.findall(
            r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
        for name in ('entity_prop', 'entity_prop_group_level', 'skill', 'level_prop_default_value'):
            obj = apk.asset('config_luban', '/' + name + '.g.bytes')
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            tables[name] = normalized(constructors.table(raw, mapping[name]))
            inputs[name] = dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()),
                                payload_sha256=sha(raw), constructor=mapping[name])
        entities = [row for row in tables['entity_prop'] if row['EntityId'] in scope['configured_entity_ids']]
        skills = [row for row in tables['skill'] if row['ClassId'] in scope['skill_item_ids']]
        groups = [row for row in tables['entity_prop_group_level'] if row['GroupId'] in wanted_groups]
        assert {row['EntityId'] for row in entities} == {row['EntityId'] for row in runtime_entities}
        assert {row['ClassId'] for row in skills} == set(scope['skill_item_ids'])
        indexed = {row['EntityId']: row for row in entities}
        for row in runtime_entities:
            selected = indexed[row['EntityId']]
            for key, value in row.items():
                if selected.get(key) != value:
                    differences.append(dict(entity=row['EntityId'], field=key, historical=value, selected=selected.get(key)))
        # The original non-PVP overload reads RankPropId, GroupLevelPropId and
        # PropFactors. Preserve other version differences explicitly; no adapter
        # changes either historical input or the original oracle's selected row.
        assert differences == [dict(entity=i, field='PvpPropScale', historical=4500, selected=5000)
                               for i in (10720, 10730, 11620)], differences
        selected_skills = {row['ClassId']: row for row in skills}
        for row in runtime_skills:
            assert all(selected_skills[row['ClassId']].get(key) == value for key, value in row.items())
        assert sorted(groups, key=lambda row: (row['GroupId'], row['SubRank'])) == sorted(
            runtime_groups, key=lambda row: (row['GroupId'], row['SubRank']))
        wanted = {row['LevelPropId'] for row in groups}
        wanted.update(row['RankPropId'] for row in entities)
        wanted.update(row[key] for row in skills for key in ('PassiveRankPropId', 'PassiveRankFactorId'))
        wanted.discard(0)
        name = 'wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile'
        bundle, = [b for b in apk.reader.manifest('WwiseBnk')['bundles'] if b['name'] == name]
        entry = 'assets/yoo/WwiseBnk/' + bundle['file']
        verified = apk.reader.read('WwiseBnk', name)
        props = decode_level_props(verified.data, wanted)
        assert all(config['props'][key] == value for key, value in props.items()), 'Selected growth rows differ'
        binary = dict(package='WwiseBnk', bundle=bundle, source=dict(verified.provenance),
                      apk_checked_first=dict(entry=entry, packaged=entry in apk.available), sha256=sha(verified.data))
    data = dict(props=props, groups=groups, entities=entities, defaults=tables['level_prop_default_value'],
                skills=skills, configured_entity_ids=scope['configured_entity_ids'],
                ranks=sorted({row['Rank'] for row in rules['skill_rank'] if row['Rank'] > 0}))
    original.USED.clear()
    source = growth.generate().replace('public static class Reference {', 'public static partial class Reference {') + PASSIVE
    (work / 'first-promotion-growth-reference.cs').write_text(source, encoding='utf-8')
    (work / 'first-promotion-growth-input.json').write_text(json.dumps(data), encoding='utf-8')
    constructor_code = []
    for name, content in sorted(constructors.sources.items()):
        if not content:
            continue
        path = constructors.source_root / (name + '.cs')
        if not path.is_file():
            path = constructors.source_root.parent / 'Common.LubanGen' / (name + '.cs')
        constructor_code.append(original.identity(path))
    receipt = dict(manifest=scope['manifest'], historical_configuration_manifest='854_156383',
        scope=original.identity(proof_path), assembly=original.identity(assembly), code_receipt=original.identity(code_receipt),
        original=[original.identity(p) for p in sorted(original.USED)], constructors=constructor_code,
        tables=inputs, binary=binary, unconsumed_pvp_differences=differences,
        input=[original.identity(original.CONFIG / (name + '.json')) for name in ('rules', 'skill-rules', 'battle-rules')],
        helpers=[original.identity(Path(__file__)), original.identity(Path(growth.__file__)),
                 original.identity(original.ROOT / 'scripts/prepare_battle_damage_reference.py'),
                 original.identity(original.ROOT / 'scripts/prepare_unity_character_catalog.py'),
                 original.identity(original.ROOT / 'scripts/official_tables.py'),
                 original.identity(original.ROOT / 'scripts/official_metadata.py'),
                 original.identity(original.ROOT / 'scripts/test_first_promotion_growth.ps1'),
                 original.identity(original.ROOT / 'scripts/fixtures/first_promotion_growth_harness.cs')],
        generated_reference_sha256=sha(source.encode()),
        coverage=dict(skill_items=len(skills), configured_entities=len(scope['configured_entity_ids']),
                      entities_with_growth=len(entities), growth_tables=len(props), group_rows=len(groups)),
        operation='Compare selected APK entity/group/skill/default tables and original binary rows to historical runtime inputs. '
            'Execute unchanged original group selection, row/default lookup and all skill/passive scaling methods with explicit '
            'data/cache/logging adapters. PVP fields are recorded but not consumed; no server scheduling or native presentation claim.')
    target = original.ROOT / 'assets/reference/battle-rules/first-promotion-growth-source-manifest.json'
    if args.record:
        target.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    else:
        assert json.loads(target.read_bytes()) == receipt, 'First-promotion growth source identities changed'
    print('Prepared selected original growth inputs:', len(entities), 'entities;', len(skills), 'skills;', len(props), 'tables.')


if __name__ == '__main__':
    main()
