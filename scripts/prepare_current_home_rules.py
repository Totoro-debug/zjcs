"""Decode selected home/HUD configuration with the original Luban constructors."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from official_tables import Constructors
from validation_workspace import validation_directory


TABLES = (
    'function_limit', 'conditions', 'player_subrank', 'system_level_limit',
    'mop_up_dungeon', 'daily_dungeon_battle', 'stage', 'home_level', 'hang_up',
    'furniture', 'hang_up_reward', 'hang_up_upgrade_material', 'fast_hang_up',
    'furniture_level_rule', 'reward_rule', 'item', 'furniture_skin',
    'home_theme_skin', 'home_environment_skin', 'home_random_reward_type',
    'home_random_reward_group', 'home_random_reward_slot', 'home_decoration',
    'home_skin_weather_override_prefab', 'profession_base', 'map_region',
)


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def encode(value):
    return (json.dumps(value, ensure_ascii=False, separators=(',', ':'), allow_nan=False) + '\n').encode('utf8')


def build(work):
    if not (work / '.zjcs-validation.json').is_file():
        raise ValueError('An owned validation workspace is required')
    apk = Apk.current()
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    constructors = Constructors(code)
    mapping = {name.rsplit('/', 1)[-1]: kind for kind, name in
               re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
    objects = {}
    for bundle in ('defaultpackage_assets_config_luban.bundle', 'defaultpackage_assets_config_luban_map.bundle'):
        for obj in apk.objects(bundle):
            if obj.type.name != 'TextAsset':
                continue
            name = obj.read().m_Name.rsplit('/', 1)[-1]
            if name in objects:
                raise ValueError('Ambiguous selected table: ' + name)
            objects[name] = obj
    tables, proofs = {}, {}
    for name in TABLES:
        obj = objects[name + '.g']
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        value = constructors.table(raw, mapping[name])
        if not value:
            raise ValueError('Empty selected home table: ' + name)
        tables[name] = value
        proofs[name] = dict(source=apk.provenance(obj), object_sha256=digest(obj.get_raw_data()),
                           payload_sha256=digest(raw), bytes=len(raw), constructor=mapping[name],
                           kind='singleton' if isinstance(value,dict) else 'list',
                           rows=1 if isinstance(value,dict) else len(value))
        print(name, proofs[name]['rows'], flush=True)
    sources = {}
    for name, text in constructors.sources.items():
        if not text:
            continue
        path = code / 'common-source/Common' / (name + '.cs')
        if not path.is_file() or name in ('Pos2d', 'Vector2', 'Vector3'):
            path = code / 'common-source/Common.LubanGen' / (name + '.cs')
        sources[path.relative_to(ROOT).as_posix()] = digest(path.read_bytes())
    for path in (code / 'global-metadata.dat', ROOT / 'scripts/official_tables.py',
                 ROOT / 'scripts/official_metadata.py', ROOT / 'scripts/official_apk.py'):
        sources[path.relative_to(ROOT).as_posix()] = digest(path.read_bytes())
    condition_rows = {row['ClassId']: row['Condition'] for row in tables['conditions']}
    if len(condition_rows) != len(tables['conditions']):
        raise ValueError('Duplicate selected condition ID')
    stage_ids = {row['Id'] for row in tables['stage']}
    missing_stages = sorted({stage for row in tables['mop_up_dungeon'] for stage in row['UnlockStages']} - stage_ids)
    output = encode(dict(manifest=apk.version, tables=tables))
    (work / 'home-rules-current.json').write_bytes(output)
    receipt = dict(manifest=apk.version, tables=proofs, source_sha256=sources,
                   generator_sha256=digest(Path(__file__).read_bytes()),
                   output=dict(path='unity/Assets/Official/Home/home-rules-current.json',
                               sha256=digest(output), bytes=len(output)),
                   validation=dict(conditions=len(condition_rows),
                       mop_up_unlock_stages_without_stage_info=missing_stages),
                   scope='Selected raw configuration only; runtime migration and validation remain required.')
    (work / 'home-rules-current-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    return receipt


if __name__ == '__main__':
    argparse.ArgumentParser(__doc__).parse_args()
    build(validation_directory())
