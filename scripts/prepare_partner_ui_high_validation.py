"""Stage native High texture expectations from verified original-source receipts."""
import hashlib
import json
from pathlib import Path

import yaml

from official_apk import ROOT
from official_source import OfficialSource
from validation_workspace import validation_directory


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def prepare():
    work = validation_directory()
    marker = json.loads((work / '.zjcs-validation.json').read_text('utf-8-sig'))
    if Path(marker['repository']).resolve() != ROOT.resolve():
        raise ValueError('Use an owned repository validation workspace')
    high_path = ROOT / 'assets/reference/partner-ui-high-textures-source-manifest.json'
    high = json.loads(high_path.read_bytes())
    scene_path = ROOT / high['current_scene_receipt']['path']
    scene = json.loads(scene_path.read_bytes())
    if (high['manifest'] != OfficialSource().version or scene['manifest'] != high['manifest']
            or sha(scene_path.read_bytes()) != high['current_scene_receipt']['sha256']):
        raise ValueError('High source receipts differ from the selected current scene')
    for record in high['outputs']:
        if sha((ROOT / record['path']).read_bytes()) != record['sha256']:
            raise ValueError('High output changed: ' + record['path'])
    rows = {row['output']['path']: row for row in scene['textures'] + high['textures']}
    used = {row['output']['path'] for row in high['textures']}
    used.update(row['native']['path'] for row in high['reused'])
    used.update('unity/' + binding['path'] for material in scene['materials']
                for binding in material['texture_bindings'] if binding['path'])
    textures = []
    for name in sorted(used):
        row = rows[name]
        if sha((ROOT / name).read_bytes()) != row['output']['sha256']:
            raise ValueError('Shared original texture changed: ' + name)
        textures.append(dict(path=name.removeprefix('unity/'), resource=row.get('resource', ''),
            format=row['target_format'], mips=row['mips'], original_fields=row['original_fields']))
    placeholders = {row['binding']['guid']: row for row in rows.values()}
    loader = ROOT / 'unity/Assets/Official/Partners/Appearance/HighTextures/Bindings/texture-loader-high.asset'
    bindings = yaml.safe_load(loader.read_text('utf8').split('MonoBehaviour:', 1)[1])['placeholderMap']
    mappings = []
    originals = high['original_fields']['placeholderMap']
    if len(bindings) != len(originals):
        raise ValueError('Original High mapping count differs')
    for binding, original in zip(bindings, originals):
        row = placeholders[binding['placeholderTexture']['guid']]
        mappings.append(dict(path=row['output']['path'].removeprefix('unity/'),
            main=original['targetTextureReference']['AssetReference']['mAssetGuid'],
            dye=original['dyeTextureReference']['AssetReference']['mAssetGuid']))
    fixture = dict(textures=textures, mappings=mappings, materials=[dict(
        path=row['output']['path'].removeprefix('unity/'), original_fields=row['fields'],
        texture_bindings=row['texture_bindings']) for row in scene['materials']])
    (work / 'partner-high-textures-fixture.json').write_text(
        json.dumps(fixture, ensure_ascii=False, separators=(',', ':')) + '\n', 'utf8')
    print('Prepared selected High textures:', len(textures), 'mappings:', len(mappings))


if __name__ == '__main__':
    prepare()
