"""Restore exact original Sprite geometry in verified Unity native serialization carriers."""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import re

import yaml

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'unity/Assets/Official/World/Map/12/Source'
PROOF = ROOT / 'assets/reference/official-map/map12-team-target-sprites-source-manifest.json'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def build(write=False):
    geometry_path = SOURCE / 'team-target-sprite-geometry.json'
    templates_path = SOURCE / 'team-target-sprite-templates.json'
    geometry, templates = json.loads(geometry_path.read_bytes()), json.loads(templates_path.read_bytes())
    if geometry['manifest'] != '871_156598' or templates['editorVersion'] != '2022.3.57f1c2':
        raise ValueError('Inspect original Sprite/Unity carrier selection')
    carriers = {row['path']: row for row in templates['sprites']}
    outputs = {}
    for row in geometry['sprites']:
        carrier = carriers[row['path']]
        if sha(carrier['yaml'].encode()) != carrier['sha256']:
            raise ValueError('Native Sprite serialization carrier drift')
        native = yaml.safe_load(carrier['yaml'].split('\n', 3)[3])
        sprite = native['Sprite']
        if (sprite['m_Rect']['width'] != row['rect']['width'] or sprite['m_Rect']['height'] != row['rect']['height']
                or sprite['m_PixelsToUnits'] != row['ppu']):
            raise ValueError('Original Sprite carrier rect/PPU differs')
        vertices, indices = row['vertices'], row['triangles']
        if not vertices or len(indices) % 3 or any(index < 0 or index >= len(vertices) for index in indices):
            raise ValueError('Original Sprite triangle indices are incomplete')
        positions = b''.join(struct.pack('<fff', vertex['x'], vertex['y'], 0) for vertex in vertices)
        positions += b'\0' * (-len(positions) % 16)
        uvs = b''.join(struct.pack('<ff', (vertex['x'] * row['ppu'] + row['pivot']['x']) / row['rect']['width'],
            (vertex['y'] * row['ppu'] + row['pivot']['y']) / row['rect']['height']) for vertex in vertices)
        data = positions + uvs
        index_data = b''.join(struct.pack('<H', index) for index in indices)
        for field in ('m_RD', 'm_AtlasRD'):
            rd = sprite[field]
            vd = rd['m_VertexData']
            active = [(channel['stream'], channel['offset'], channel['format'], channel['dimension'])
                      for channel in vd['m_Channels'] if channel['dimension']]
            if active != [(0, 0, 0, 3), (1, 0, 0, 2)] or len(rd['m_SubMeshes']) != 1:
                raise ValueError('Inspect native Sprite position/UV stream serialization')
            vd['m_VertexCount'], vd['m_DataSize'], vd['_typelessdata'] = len(vertices), len(data), data.hex()
            rd['m_IndexBuffer'] = index_data.hex()
            submesh = rd['m_SubMeshes'][0]
            submesh.update(firstByte=0, indexCount=len(indices), baseVertex=0, firstVertex=0, vertexCount=len(vertices))
        # Keep every unrelated serialized scalar and pointer byte-for-byte. In
        # particular, YAML must not turn an all-zero GUID key into integer zero.
        text, count = re.subn(r'(?m)^  m_Name: .*$', '  m_Name: ' + row['name'], carrier['yaml'])
        if count != 1:
            raise ValueError('Native Sprite name carrier differs')
        def render_data(match):
            block = match[0]
            for key, value, indent in (('indexCount', len(indices), 6), ('vertexCount', len(vertices), 6),
                    ('m_VertexCount', len(vertices), 6), ('m_DataSize', len(data), 6),
                    ('m_IndexBuffer', index_data.hex(), 4), ('_typelessdata', data.hex(), 6)):
                block, count = re.subn(r'(?m)^' + ' ' * indent + key + r': .*$',
                                      ' ' * indent + key + ': ' + str(value), block)
                if count != 1:
                    raise ValueError('Native Sprite mesh carrier field differs: ' + key)
            return block
        text, count = re.subn(r'(?ms)^  m_(?:Atlas)?RD:\n.*?(?=^  \w|\Z)', render_data, text)
        if count != 2:
            raise ValueError('Native Sprite render-data carrier coverage differs')
        raw = text.encode()
        target = ROOT / 'unity' / row['path']
        outputs[target] = raw
        outputs[target.with_name(target.name + '.meta')] = ('fileFormatVersion: 2\nguid: ' + row['guid'] +
            '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 21300000\n').encode()
    def record(path):
        return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes()))
    value = dict(format=1, manifest=geometry['manifest'], inputs=[record(geometry_path), record(templates_path)],
        outputs=[dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(raw)) for path, raw in outputs.items()],
        adapters=[record(Path(__file__).resolve())],
        validation=[record(ROOT / 'unity/Assets/Game/Editor' / name) for name in
            ('OfficialMap12PlayModeAcceptance.TeamTargets.cs', 'OfficialMap12PlayModeAcceptance.TeamSpriteImport.cs')],
        operation='Preserve exact official vertex positions and triangle order in Unity-generated native Sprite '
        'serialization carriers, including both render-data copies. Preserve original rect/pivot/border/PPU and '
        'decoded original texture. Rebase UV coordinates onto the verified unrotated logical Sprite image; '
        'retain aligned native position and UV streams. Runtime acceptance checks the imported native data.',
        limitations=['Formal full TeamPanel/local NPC/battle/save integration remains pending.'])
    for path, raw in outputs.items():
        if write:
            path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(raw)
        elif not path.is_file() or path.read_bytes() != raw:
            raise ValueError('Original native Sprite geometry drift: ' + path.name)
    raw = (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode()
    if write:
        PROOF.write_bytes(raw)
    elif not PROOF.is_file() or PROOF.read_bytes() != raw:
        raise ValueError('Original native Sprite geometry receipt drift')
    print('Original native target Sprite geometry verified: ' + str(len(geometry['sprites'])), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true')
    build(parser.parse_args().write)
