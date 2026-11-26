"""Extract a migrated skill's current EC graph and cultivation inputs into a verified scoped pack."""
import argparse
import copy
import hashlib
import json
import os
import re
from pathlib import Path

from official_apk import Apk, ROOT
from official_ec_codec import Formats
from official_map_codec import unpack
from official_tables import Constructors
from official_unity_schema import ScriptSchemas
from prepare_unity_character_catalog import decode_level_props


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes()))


def build(item, spec_path=None):
    spec_path = (ROOT / spec_path).resolve() if spec_path is not None else ROOT / f'scripts/profession_skill_sources/fourth/{item}.json'
    spec = json.loads(spec_path.read_bytes())
    baseline = ROOT / 'unity/Assets/Official/Combat'
    skill = json.loads((baseline / 'skill-rules.json').read_bytes())
    previous = copy.deepcopy(skill['entities'])
    for key, value in json.loads((baseline / 'battle-rules.json').read_bytes())['entities'].items():
        previous.setdefault(key, {}).update(value)
    wanted = {str(value) for value in spec.get('rule_pack_entities', spec['entities'])}
    with Apk.current() as apk:
        code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
        formats, constructors = Formats(code), Constructors(code)
        inputs, entities, locations = [], {}, {}
        for name in spec['rule_pack_fragments']:
            obj = apk.asset('config_ec', '/' + name)
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            try:
                rows, prefix = unpack(raw)[0], False
            except Exception:
                rows, prefix = unpack(raw[2:])[0], True
            inputs.append(dict(source=apk.provenance(obj), payload_sha256=sha(raw)))
            for _, entity, components in rows:
                key = str(entity)
                if key not in wanted:
                    continue
                assert key not in entities, ('Duplicate EC entity', entity)
                entities[key] = {name.removeprefix('Common.'): formats.component(name, payload[2:] if prefix and payload else payload)
                                 for name, payload in components}
                locations[key] = 'Assets/Config/EC/' + name
        assert entities.keys() == wanted, ('Missing EC entities', wanted - entities.keys())
        mappings = {name: cls for cls, name in re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
        def table(name):
            obj = apk.asset('config_luban', '/' + name + '.g.bytes')
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            inputs.append(dict(source=apk.provenance(obj), payload_sha256=sha(raw)))
            return json.loads(json.dumps(constructors.table(raw, mappings[name])))
        props = [row for row in table('entity_prop') if str(row['EntityId']) in wanted]
        group_ids = {row['GroupLevelPropId'] for row in props}
        groups = [row for row in table('entity_prop_group_level') if row['GroupId'] in group_ids]
        prop_ids = {row['LevelPropId'] for row in groups}
        prop_ids.update(row[key] for row in props for key in ('RankPropId', 'SubRankPropId'))
        prop_ids.discard(0)
        raw = apk.reader.read('WwiseBnk', 'wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile')
        inputs.append(dict(source=dict(raw.provenance), payload_sha256=sha(raw.data)))
        values = decode_level_props(raw.data, prop_ids)
        os.environ['PATH'] = str(ROOT / '.tools/dotnet') + os.pathsep + os.environ.get('PATH', '')
        schemas = ScriptSchemas(code_root=code)
        visibility, managed_assemblies = {}, set()
        for entity in spec.get('visibility_bundles', ()):
            bundle = str(entity) if str(entity).endswith('.bundle') else f'defaultpackage_assets_prefabs_ecentity_fight_fightskill_{entity}.bundle'
            for obj in apk.objects(bundle):
                if obj.type.name != 'MonoBehaviour':
                    continue
                native = obj.read(check_read=False)
                script = native.m_Script.deref()
                if script.read().m_ClassName != 'MyFightDamage':
                    continue
                assembly = script.read().m_AssemblyName
                managed_assemblies.add(assembly if assembly.endswith('.dll') else assembly + '.dll')
                owner = native.m_GameObject.deref()
                entity_id = int(owner.read().m_Name)
                if str(entity_id) not in wanted:
                    continue
                fields = schemas.read(obj)
                visibility[entity_id] = dict(damage_entity_id=entity_id, hide=bool(fields['myHideDamage']),
                                             show_zero=bool(fields['myShowZeroDamage']))
                for original in (obj, script, owner):
                    inputs.append(dict(source=apk.provenance(original), payload_sha256=sha(original.get_raw_data())))
        damage_ids = {value['FightHitFixedComponent']['DamageId'] for value in entities.values() if 'FightHitFixedComponent' in value}
        assert visibility.keys() == damage_ids, ('Missing native damage display binding', damage_ids - visibility.keys())
        # Shared cultivation tables must stay unchanged for existing migrations.
        if not spec.get('scope_numeric'):
            assert all(skill['props'][key] == value for key, value in values.items()), 'Shared numeric rows changed'
            assert groups == [row for row in skill['tables']['entity_prop_group_level'] if row['GroupId'] in group_ids], 'Shared cultivation groups changed'
        result = dict(manifest=apk.version, item_id=item, entities=entities,
                      previous_entities={key: previous.get(key) for key in sorted(wanted)},
                      tables=dict(entity_prop=props, entity_prop_group_level=groups), props=values,
                      damage=[visibility[key] for key in sorted(visibility)])
        if spec.get('scope_numeric'):
            result['scope_numeric'] = True
        payload = (json.dumps(result, ensure_ascii=False, separators=(',', ':')) + '\n').encode('utf8')
        proof = dict(manifest=apk.version, item_id=item, output_sha256=sha(payload), inputs=inputs,
                     entity_locations=locations, source_spec=identity(spec_path), generator=identity(Path(__file__)),
                     baseline=[identity(baseline / (name + '.json')) for name in ('skill-rules', 'battle-rules')],
                     managed_schemas=[dict(name=name, fields=fields) for name, fields in sorted(schemas.records.items())],
                     managed_assemblies=[identity(code / 'managed/decoded' / name) for name in sorted(managed_assemblies)],
                     managed_reader=[identity(ROOT / 'scripts' / name) for name in ('official_unity_schema.py', 'unity_schema_types.cs')],
                     constructors=[identity(code / 'common-source' / ('Common' if (code / 'common-source/Common' / (name + '.cs')).is_file() else 'Common.LubanGen') / (name + '.cs')) for name in sorted(constructors.sources)],
                     formatters=[identity(path) for path in sorted(set(formats.sources.values()))])
    return payload, proof


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--item', type=int, required=True)
    parser.add_argument('--write', action='store_true')
    parser.add_argument('--spec', type=Path)
    args = parser.parse_args()
    payload, proof = build(args.item, args.spec)
    target = ROOT / f'unity/Assets/Game/Resources/Combat/SkillRules/{args.item}.json'
    receipt = target.with_name(target.stem + '-source-manifest.json')
    if args.write:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(payload)
        receipt.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8')
    else:
        assert target.read_bytes() == payload and json.loads(receipt.read_bytes()) == proof, 'Current skill rule pack changed'
    print('Current skill rule pack verified:', args.item)


if __name__ == '__main__':
    main()
