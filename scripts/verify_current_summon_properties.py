"""Verify current summon inheritance and every referenced cultivation curve."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_unity_character_catalog import decode_level_props


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes()))


def verify(entities):
    wanted = set(entities)
    if not wanted or any(not isinstance(entity, int) or entity <= 0 for entity in wanted):
        raise ValueError('Select positive original summon entity IDs')
    baseline_path = ROOT / 'unity/Assets/Official/Combat/skill-rules.json'
    baseline = json.loads(baseline_path.read_bytes())
    proofs, selected = [], {}
    with Apk.current() as apk:
        code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
        parser = Constructors(code)
        classes = {name: constructor for constructor, name in re.findall(
            r'new (Tb\w+)\(loader\("([^"]+)"\)\)', parser.source('Tables'))}

        def rows(name, field, values):
            obj = apk.asset('config_luban', '/' + name + '.g.bytes')
            raw = obj.read().m_Script.encode('utf-8', 'surrogateescape')
            found = json.loads(json.dumps([
                row for row in parser.table(raw, classes[name]) if row[field] in values]))
            expected = [row for row in baseline['tables'][name] if row[field] in values]
            if not found or found != expected:
                raise ValueError('Current summon cultivation differs: ' + name)
            selected[name] = found
            proofs.append(dict(name=name, source=apk.provenance(obj), payload_sha256=sha(raw)))
            return found

        fixed = rows('summon_monster_fix_prop', 'EntityId', wanted)
        added = rows('summon_monster_add_prop', 'ClassId', wanted)
        if {row['EntityId'] for row in fixed} != wanted or {row['ClassId'] for row in added} != wanted:
            raise ValueError('Every selected summon requires fixed and inherited property rows')
        rows('summon_rank_additive_factor', 'RankGroup', {row['RankGroup'] for row in added})
        groups = rows('entity_prop_group_level', 'GroupId', {row['GroupLevelPropId'] for row in fixed})
        prop_ids = {row['RankPropId'] for row in fixed} | {row['LevelPropId'] for row in groups}
        prop_ids.discard(0)
        raw = apk.reader.read('WwiseBnk', 'wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile')
        props = decode_level_props(raw.data, prop_ids)
        if any(baseline['props'].get(key) != value for key, value in props.items()):
            raise ValueError('Current summon level/rank numeric curve differs')
        return dict(manifest=apk.version, entities=sorted(wanted), tables=proofs, rows=selected,
            props=props, level_prop_source=dict(raw.provenance), level_prop_sha256=sha(raw.data),
            baseline=identity(baseline_path), generator=identity(Path(__file__)),
            decoder=identity(ROOT / 'scripts/prepare_unity_character_catalog.py'),
            table_reader=identity(ROOT / 'scripts/official_tables.py'),
            code_manifest=identity(code.parent / 'code-manifest.json'))


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--entity', action='append', type=int, required=True)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    proof = verify(args.entity)
    if args.output:
        args.output.write_bytes((json.dumps(proof, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
    print('Current summon cultivation verified:', ', '.join(map(str, proof['entities'])))


if __name__ == '__main__':
    main()
