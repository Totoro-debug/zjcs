"""Refresh native prefab index buffers from independently decoded official meshes."""
from __future__ import annotations

import collections
import hashlib
import json
from pathlib import Path
import re
import struct

from official_apk import ROOT
from official_source import OfficialSource
from refresh_current_character_meshes import load_skeleton, preserve


def sha(data):
    return hashlib.sha256(data).hexdigest()


def triangles(values):
    return collections.Counter(min(tuple(values[i:i + 3][n:] + values[i:i + 3][:n])
                                   for n in range(3)) for i in range(0, len(values), 3))


def build():
    version = OfficialSource().version
    receipt_path = ROOT / 'assets/reference/character-creation/current-native-prefabs-source-manifest.json'
    if receipt_path.exists():
        prior = json.loads(receipt_path.read_bytes())
        if prior['manifest'] == version:
            for record in prior['outputs']:
                if sha((ROOT / record['path']).read_bytes()) != record['sha256']:
                    raise ValueError('Converted native prefab changed: ' + record['path'])
            return prior
    old, old_source = load_skeleton('854_156383')
    new, new_source = load_skeleton(version)
    for key in ('bones', 'slots', 'ik', 'transform', 'path', 'physics'):
        if old.get(key) != new.get(key):
            raise ValueError('Native prefab pose evaluation input changed: ' + key)
    # The existing native controller contains these two generated clips. Other
    # state names alias them; this refresh does not invent new animation clips.
    root = ROOT / 'unity/Assets/Official/CharacterCreation/Recovered/NativeCharacterData/Baked'
    controller = next((root / 'Variants/Male').glob('*.controller'))
    clips = re.findall(r'^--- !u!74[^\n]*\nAnimationClip:\n.*?^  m_Name: ([^\n]+)$',
                       controller.read_text('utf8'), re.M | re.S)
    if set(clips) != {'home/idle_01', 'map/run_01'}:
        raise ValueError('Native controller clip set differs from its recorded scope')
    for name in clips:
        if old['animations'][name] != new['animations'][name]:
            raise ValueError('Native animation needs resampling: ' + name)
    old_skins = {s['name']: s for s in old['skins']}
    new_skins = {s['name']: s for s in new['skins']}
    pending = []
    counts = collections.Counter()
    for path in sorted((root / 'Variants/Male').glob('*.prefab')):
        match = re.search(r'\((.+)\)\.prefab$', path.name)
        if not match:
            raise ValueError('Native variant has no unambiguous source skin: ' + path.name)
        skin = match[1]
        previous = path.read_bytes()
        text = previous.decode('utf8')

        def convert(match):
            block = match[0]
            label = re.search(r'^  m_Name: (.+)$', block, re.M)[1].strip("'").replace("''", "'")
            slot, attachment = re.fullmatch(r'\[(.*)\] (.*)', label).groups()
            a = old_skins[skin]['attachments'][slot][attachment]
            b = new_skins[skin]['attachments'][slot][attachment]
            if {k: v for k, v in a.items() if k != 'triangles'} != {k: v for k, v in b.items() if k != 'triangles'}:
                raise ValueError('Native attachment geometry changed: ' + skin + ':' + label)
            if 'triangles' not in a:
                counts['regions'] += 1
                return block
            if triangles(a['triangles']) != triangles(b['triangles']):
                raise ValueError('Native attachment triangle coverage or winding changed')
            width = 4 if re.search(r'^  m_IndexFormat: 1$', block, re.M) else 2
            code = 'I' if width == 4 else 'H'
            index = re.search(r'(?m)^  m_IndexBuffer: ([0-9a-f]+)$', block)
            raw = bytes.fromhex(index[1])
            values = list(struct.unpack('<' + code * (len(raw) // width), raw))
            if values != a['triangles']:
                raise ValueError('Native index buffer differs from the source attachment: ' + label)
            if a['triangles'] == b['triangles']:
                counts['identical_meshes'] += 1
                return block
            replacement = struct.pack('<' + code * len(b['triangles']), *b['triangles']).hex()
            counts['updated_meshes'] += 1
            return block[:index.start(1)] + replacement + block[index.end(1):]

        converted = re.sub(r'^--- !u!43[^\n]*\nMesh:\n.*?(?=^---|\Z)', convert, text, flags=re.M | re.S).encode('utf8')
        pending.append((path, previous, converted))
    records = []
    for path, previous, converted in pending:
        if path.read_bytes() != previous:
            raise ValueError('Native prefab changed during conversion')
        history = preserve(path)
        if converted != previous:
            temporary = path.with_name(path.name + '.current.tmp')
            temporary.write_bytes(converted)
            temporary.replace(path)
        records.append({'path': path.relative_to(ROOT).as_posix(), 'previous': history,
                        'bytes': len(converted), 'sha256': sha(converted)})
    result = {'manifest': version, 'base_manifest': '854_156383', 'operation': __doc__,
              'sources': [old_source, new_source], 'unchanged_native_clips': clips,
              'controller_sha256': sha(controller.read_bytes()), 'counts': dict(counts),
              'generator_sha256': sha(Path(__file__).read_bytes()), 'outputs': records}
    receipt_path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8')
    return result


if __name__ == '__main__':
    print(json.dumps(build()['counts']))
