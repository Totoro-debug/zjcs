"""Recover the current native Sprite objects behind the existing inventory aliases.

The alias is retained for the existing UI; selection uses the recorded logical
bundle and object identity. Native meshes, atlas UVs, texture mips and samplers
come from the selected official inputs, never the historical cropped PNG.
"""
import argparse
from collections import defaultdict
import hashlib
import json
from pathlib import Path
import re

import yaml

from official_apk import Apk, ROOT
from official_source import OfficialSource
from prepare_current_creation_ui_sprites import CurrentUiSprites


PAYLOAD = Path('unity/Assets/Official/Inventory')
CATALOG = Path('unity/Assets/Game/Resources/Inventory/OfficialInventoryAssetCatalog.asset')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def source_records():
    records, inputs = {}, []
    for relative in (PAYLOAD/'source-manifest.json', PAYLOAD/'item-source-manifest.json'):
        raw = (ROOT/relative).read_bytes()
        inputs.append(dict(path=relative.as_posix(), sha256=sha(raw)))
        for uri, row in json.loads(raw)['resources'].items():
            alias = Path(uri).name
            if not alias.endswith('.png'):
                continue
            if alias in records and any(records[alias].get(key) != row.get(key)
                                        for key in ('bundle', 'path_id', 'object')):
                raise ValueError('Conflicting recorded inventory alias: '+alias)
            records[alias] = row
    raw = (ROOT/CATALOG).read_bytes()
    inputs.append(dict(path=CATALOG.as_posix(), sha256=sha(raw)))
    aliases = [yaml.safe_load(value) for value in re.findall(r'^  - sourcePath: (.+)$', raw.decode('utf8'), re.M)]
    if len(set(aliases)) != len(aliases):
        raise ValueError('Duplicate installed inventory Sprite aliases')
    if not aliases:
        raise ValueError('No installed inventory Sprite aliases')
    # The original quality array contains two aliases for the white background.
    # Its extra PNGs were recorded by the historical quality exporter separately.
    for alias in set(aliases)-records.keys():
        match = re.fullmatch(r'(ImgQuality_[A-Za-z]+)-[a-f0-9]{10}\.png', alias)
        if not match:
            raise ValueError('Inventory alias has no original source record: '+alias)
        records[alias] = dict(bundle='defaultpackage_assets_image_ui_itemquality.bundle',
                              object=match[1], source_rule='Original named quality background')
    return {name: records[name] for name in aliases}, inputs


def select_sprite(objects, record):
    candidates = [obj for obj in objects if obj.type.name == 'Sprite'
                  and obj.read().m_Name == record['object']]
    exact = [obj for obj in candidates if str(obj.path_id) == str(record.get('path_id'))]
    if len(exact) == 1:
        return exact[0]
    if len(candidates) != 1:
        raise ValueError('Current original Sprite is absent or ambiguous: '
                         +record['bundle']+':'+record['object']+' candidates='+str(len(candidates)))
    return candidates[0]


def stage():
    source = OfficialSource()
    cache = source.prepared_path/'inventory/native-sprites'
    cache.mkdir(parents=True, exist_ok=True)
    records, inputs = source_records()
    grouped = defaultdict(list)
    for alias, record in records.items():
        grouped[record['bundle']].append((alias, record))
    apk = Apk.current()
    converter = CurrentUiSprites(apk, cache)
    aliases, failures = [], []
    try:
        for bundle, rows in sorted(grouped.items()):
            try:
                objects = list(apk.objects(bundle))
            except (FileNotFoundError, KeyError) as error:
                failures.append(dict(bundle=bundle, aliases=[alias for alias,_ in rows], error=str(error)))
                continue
            for alias, record in rows:
                try:
                    obj = select_sprite(objects, record)
                    native = converter.sprite(obj)
                    aliases.append(dict(alias=alias, previous_source=record, native=native))
                except (ValueError, FileNotFoundError, KeyError) as error:
                    failures.append(dict(alias=alias, bundle=bundle, error=str(error)))
            print('Inventory native Sprites:', len(aliases), '/', len(records),
                  'textures:', len(converter.textures), 'failures:', len(failures), bundle, flush=True)
    finally:
        apk.close()
    tools = [Path(__file__).resolve(), ROOT/'scripts/prepare_current_creation_ui_sprites.py',
             ROOT/'scripts/prepare_current_creation_skin_icons.py',
             ROOT/'scripts/prepare_unity_map12_team_panel_sprites.py',
             ROOT/'scripts/prepare_unity_profession_reward_sprites.py',
             ROOT/'scripts/prepare_unity_map13_native_scene.py']
    result = dict(manifest=source.version, apk_sha256=source.apk_sha256,
                  inputs=inputs, generators=[dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes())) for path in tools],
                  aliases=aliases, sprites=list(converter.sprites.values()), textures=list(converter.textures.values()),
                  failures=failures, completed=not failures and len(aliases)==len(records))
    target = cache/'source-manifest.json'
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', 'utf8')
    print('Inventory native source receipt:', target, flush=True)
    if not result['completed']:
        raise ValueError('Resolve current inventory Sprite dependencies: '+repr(failures[:12]))
    return result


if __name__ == '__main__':
    argparse.ArgumentParser(description=__doc__).parse_args()
    stage()
