"""Recover attachment geometry and atlas rectangles without a Spine runtime dependency."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))

from official_apk import Apk, ROOT
from convert_official_skeletons import convert

OUTPUT = ROOT / 'unity/Assets/Game/Resources/CharacterCreation/native-character-geometry.json'
RECEIPT = ROOT / 'assets/reference/character-creation/native-geometry-source-manifest.json'


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def atlas_regions(text):
    pages, regions = [], []
    current = None
    page = None
    for line in text.splitlines():
        line = line.strip()
        if not line:
            page = current = None
            continue
        if ':' not in line:
            if page is None:
                page = {'name': line, 'size': None}
                pages.append(page)
                current = None
            else:
                current = {'page': page, 'name': line}
                regions.append(current)
            continue
        key, value = line.split(':', 1)
        key, value = key.strip(), value.strip()
        if current is None:
            if key == 'size':
                page['size'] = [int(v) for v in value.split(',')]
        elif key in ('bounds', 'offsets'):
            current[key] = [int(v) for v in value.split(',')]
        elif key == 'rotate':
            current['rotate'] = 90 if value == 'true' else 0 if value == 'false' else int(value)
    result = []
    for region in regions:
        bounds = region['bounds']
        rotate = region.get('rotate', 0)
        if rotate not in (0, 90, 180, 270):
            raise ValueError('Unsupported atlas rotation: ' + str(rotate))
        result.append({'page': Path(region['page']['name']).stem,
                       'name': region['name'], 'size': region['page']['size'],
                       'bounds': bounds, 'offsets': region.get('offsets', [0, 0, bounds[2], bounds[3]]),
                       'rotate': rotate})
    return result


def build():
    apk = Apk.current()
    sexes, inputs = [], []
    for sex in ('male', 'female'):
        decoded = ROOT / '.tools/source-packages/current' / apk.version / 'native-geometry-skeletons' / sex
        convert(apk.version, sex, decoded)
        skeleton = json.loads((decoded / 'skeleton.json').read_text('utf8'))
        source = {'path': (decoded / 'source-manifest.json').relative_to(ROOT).as_posix(),
                  'sha256': sha(decoded / 'source-manifest.json')}
        prefab = ROOT / ('unity/Assets/Official/CharacterCreation/Recovered/NativeCharacterData/'
                         'Baked/Preview/CharacterCreation_' + sex.title() + '.prefab')
        skins = set(re.findall(r'^  m_Name: __skin__(.+)$', prefab.read_text('utf8'), re.M))
        slots = {slot['name']: slot for slot in skeleton['slots']}
        bones = {bone['name']: i for i, bone in enumerate(skeleton['bones'])}
        objects = apk.objects('appearanceassets_character_model_' + sex + '_1')
        atlases = [o for o in objects if o.type.name == 'TextAsset'
                   and o.read().m_Name == 'character_' + sex.title() + '_1.atlas']
        if len(atlases) != 1:
            raise ValueError('Expected one official character atlas: ' + sex)
        atlas = atlases[0]
        text = atlas.read().m_Script
        raw = text.encode('utf8', 'surrogateescape')
        provenance = apk.provenance(atlas)
        provenance['payload_sha256'] = hashlib.sha256(raw).hexdigest()
        inputs.append({'skeleton': source, 'atlas': provenance,
                       'native_prefab': {'path': prefab.relative_to(ROOT).as_posix(), 'sha256': sha(prefab)}})
        attachments = []
        for skin in skeleton['skins']:
            if skin['name'] not in skins:
                continue
            for slot, entries in skin.get('attachments', {}).items():
                for name, attachment in entries.items():
                    kind = attachment.get('type', 'region')
                    if kind not in ('region', 'mesh'):
                        continue  # Non-rendering points/paths do not create native mesh renderers.
                    row = {'skin': skin['name'], 'slot': slot, 'attachment': name,
                           'path': attachment.get('path', attachment.get('name', name)),
                           'kind': kind, 'bone': bones[slots[slot]['bone']]}
                    if kind == 'mesh':
                        row.update(vertices=attachment['vertices'], uvs=attachment['uvs'],
                                   triangles=attachment['triangles'])
                    else:
                        row.update(x=attachment.get('x', 0), y=attachment.get('y', 0),
                                   rotation=attachment.get('rotation', 0),
                                   scaleX=attachment.get('scaleX', 1), scaleY=attachment.get('scaleY', 1),
                                   width=attachment['width'], height=attachment['height'])
                    attachments.append(row)
        sexes.append({'sex': sex, 'bones': list(bones), 'attachments': attachments,
                      'regions': atlas_regions(text)})
    payload = {'manifest': apk.version, 'sexes': sexes}
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes((json.dumps(payload, ensure_ascii=False, separators=(',', ':'), allow_nan=False) + '\n').encode('utf8'))
    sidecar = OUTPUT.with_name('native-character-geometry-source-manifest.json')
    sidecar.write_bytes((json.dumps({'manifest': apk.version, 'output_sha256': sha(OUTPUT)}, separators=(',', ':')) + '\n').encode('utf8'))
    receipt = {'manifest': apk.version, 'operation': __doc__, 'inputs': inputs,
               'format_references': ['https://esotericsoftware.com/spine-json-format',
                                     'https://esotericsoftware.com/spine-atlas-format'],
               'scope': 'Official attachment bone-local coordinates, weights, region transforms and atlas packing. Existing native Animator clips are retained, not regenerated or attributed to this converter.',
               'generator_sha256': sha(__file__), 'output': OUTPUT.relative_to(ROOT).as_posix(),
               'output_sha256': sha(OUTPUT),
               'runtime_bindings': [{'path': name, 'sha256': sha(ROOT / name)} for name in (
                   'unity/Assets/Game/Runtime/Presentation/Character/CharacterNativeAttachmentGeometry.cs',
                   'unity/Assets/Game/Runtime/Presentation/Character/CharacterCreationNativeModelFactory.cs')],
               'counts': {s['sex']: {'attachments': len(s['attachments']), 'atlas_regions': len(s['regions'])} for s in sexes}}
    RECEIPT.write_bytes((json.dumps(receipt, ensure_ascii=False, indent=2) + '\n').encode('utf8'))
    print(receipt['counts'])


if __name__ == '__main__':
    build()
