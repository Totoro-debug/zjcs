"""Extend world character geometry from the preserved, verified native skeleton packages."""
from __future__ import annotations

import argparse
import gzip
import hashlib
import io
import json
from pathlib import Path
import struct

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / 'unity/Assets/Game/Resources/CharacterCreation/native-character-geometry.json'
PROOF = ROOT / 'assets/reference/profession-ui/skeleton-data-source-manifest.json'
RECEIPT = ROOT / 'assets/reference/character-creation/world-append-geometry-source-manifest.json'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def unpack(path):
    stream = io.BytesIO(path.read_bytes())
    def number(): return struct.unpack('<I', stream.read(4))[0]
    def string(): return stream.read(number()).decode('utf-8')
    def block():
        length, digest = number(), stream.read(32)
        data = gzip.decompress(stream.read(number()))
        if len(data) != length or hashlib.sha256(data).digest() != digest:
            raise ValueError('Native source block identity differs: ' + str(path))
        return json.loads(data)
    if stream.read(8) != b'ZJCSPROF' or number() != 1:
        raise ValueError('Unknown original native skeleton package')
    manifest, sex = string(), string()
    scale = struct.unpack('<f', stream.read(4))[0]
    skeleton, atlas = block(), block()
    if stream.read() or manifest != '871_156598' or abs(scale - .01) > 1e-8:
        raise ValueError('Original source version/scale differs')
    return manifest, sex, skeleton, atlas


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    proof = json.loads(PROOF.read_bytes())
    previous = json.loads(OUTPUT.read_bytes())
    bodies, inputs = [], []
    for record in proof['outputs']:
        path = ROOT / record['file']['path']
        if sha(path.read_bytes()) != record['file']['sha256']:
            raise ValueError('Preserved original package hash differs: ' + str(path))
        manifest, sex, skeleton, atlas = unpack(path)
        if sex not in ('male', 'female'):
            continue
        old = next(value for value in previous['sexes'] if value['sex'] == sex)
        bones = {value['name']: index for index, value in enumerate(skeleton['bones'])}
        slots = {value['name']: value for value in skeleton['slots']}
        if list(bones) != old['bones'] or atlas != old['regions'] or manifest != previous['manifest']:
            raise ValueError('World model bones, atlas or version differ: ' + sex)
        attachments = []
        for skin in skeleton['skins']:
            for slot, entries in skin.get('attachments', {}).items():
                for name, source in entries.items():
                    kind = source.get('type', 'region')
                    if kind in ('point', 'boundingbox', 'clipping', 'path'):
                        continue
                    if kind not in ('mesh', 'region'):
                        raise ValueError('Unconverted rendering attachment: ' + kind)
                    row = dict(skin=skin['name'], slot=slot, attachment=name,
                        path=source.get('path', source.get('name', name)), kind=kind,
                        bone=bones[slots[slot]['bone']])
                    if kind == 'mesh':
                        row.update(vertices=source['vertices'], uvs=source['uvs'], triangles=source['triangles'])
                    else:
                        row.update(x=source.get('x', 0), y=source.get('y', 0), rotation=source.get('rotation', 0),
                            scaleX=source.get('scaleX', 1), scaleY=source.get('scaleY', 1),
                            width=source['width'], height=source['height'])
                    attachments.append(row)
        by_key = {(row['skin'], row['slot'], row['attachment']): row for row in attachments}
        for row in old['attachments']:
            if by_key.get((row['skin'], row['slot'], row['attachment'])) != row:
                raise ValueError('Existing original geometry differs: ' + str(row['skin']))
        bodies.append(dict(sex=sex, bones=list(bones), attachments=attachments, regions=atlas))
        inputs.append(record['file'])
    if {body['sex'] for body in bodies} != {'male', 'female'}:
        raise ValueError('Both original world bodies are required')
    payload = (json.dumps(dict(manifest=previous['manifest'], sexes=bodies), ensure_ascii=False,
                         separators=(',', ':'), allow_nan=False) + '\n').encode('utf-8')
    counts = {body['sex']: len(body['attachments']) for body in bodies}
    if args.check:
        if OUTPUT.read_bytes() != payload:
            raise ValueError('World character geometry differs')
    else:
        OUTPUT.write_bytes(payload)
        OUTPUT.with_name('native-character-geometry-source-manifest.json').write_text(json.dumps(
            dict(manifest=previous['manifest'], output_sha256=sha(payload)), separators=(',', ':')) + '\n', encoding='utf-8')
        receipt = dict(manifest=previous['manifest'], operation=__doc__,
            base_geometry_source=dict(path='assets/reference/character-creation/native-geometry-source-manifest.json',
                sha256=sha((ROOT / 'assets/reference/character-creation/native-geometry-source-manifest.json').read_bytes())),
            prerequisite=dict(path=PROOF.relative_to(ROOT).as_posix(), sha256=sha(PROOF.read_bytes())),
            inputs=inputs, output=OUTPUT.relative_to(ROOT).as_posix(), output_sha256=sha(payload),
            generator_sha256=sha(Path(__file__).read_bytes()), counts=counts,
            scope='All original region/mesh skin attachments. Existing coordinates are checked unchanged. '
                  'Point, bounding box, clipping and path records do not create renderers; animation/constraint evaluation is unchanged.')
        RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Original world character attachments verified:', counts)


if __name__ == '__main__':
    main()
