"""Prove that integrated Halloween audio survives the current bank update."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import struct

from official_apk import ROOT
from official_source import OfficialSource
from prepare_character_creation_opening_audio import parse_pck
from refresh_current_character_meshes import preserve


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def inspect(raw):
    banks, media = parse_pck(raw)
    files = {str(identity): {'bytes': size, 'sha256': sha(raw[offset:offset+size])}
             for identity, _, size, offset, _ in media}
    objects = {}
    for bank_id, _, size, offset, _ in banks:
        bank = raw[offset:offset+size]
        cursor = 0
        while cursor < len(bank):
            tag = bank[cursor:cursor+4]
            size = struct.unpack_from('<I', bank, cursor+4)[0]
            chunk = bank[cursor+8:cursor+8+size]
            if len(chunk) != size:
                raise ValueError('Truncated official Wwise chunk')
            cursor += 8 + size
            if tag != b'HIRC':
                continue
            count = struct.unpack_from('<I', chunk)[0]
            position = 4
            for _ in range(count):
                kind = chunk[position]
                size = struct.unpack_from('<I', chunk, position+1)[0]
                payload = chunk[position+5:position+5+size]
                if len(payload) != size:
                    raise ValueError('Truncated official Wwise object')
                identity = struct.unpack_from('<I', payload)[0]
                objects[str(identity)] = {'bank_id': bank_id, 'type': kind, 'sha256': sha(payload)}
                position += 5 + size
            if position != len(chunk):
                raise ValueError('Unconsumed official Wwise object bytes')
    return {'media': files, 'objects': objects}


def mapping(raw):
    if raw[0] != 2:
        raise ValueError('Unsupported Wwise event mapping header')
    count = struct.unpack_from('<I', raw, 1)[0]
    position = 5
    banks = []
    for _ in range(count):
        if raw[position] != 2:
            raise ValueError('Unsupported Wwise bank definition')
        identity, size = struct.unpack_from('<iI', raw, position+1)
        name = raw[position+9:position+9+size].decode('utf8')
        flags = raw[position+9+size]
        position += 10 + size
        banks.append({'id': identity, 'name': name, 'flags': flags})
    count = struct.unpack_from('<I', raw, position)[0]
    position += 4
    events = {}
    for _ in range(count):
        identity, size = struct.unpack_from('<II', raw, position)
        position += 8
        indices = list(struct.unpack_from('<' + 'H'*size, raw, position))
        position += size*2
        if identity in events or any(index >= len(banks) for index in indices):
            raise ValueError('Invalid Wwise event mapping')
        events[identity] = indices
    if position != len(raw):
        raise ValueError('Unconsumed Wwise event mapping bytes')
    return {'banks': banks, 'events': events}


def build():
    version = OfficialSource().version
    name = 'wwisebnk_assets_wwisedata_android_activity_032_halloween2026_pck.rawfile'
    directories = [ROOT / '.tools/source-packages/history/854_156383/WwiseBnk',
                   ROOT / '.tools/source-packages/WwiseBnk']
    versions = []
    for directory in directories:
        raw = (directory / name).read_bytes()
        index = json.loads((directory / 'bundle-index.json').read_bytes())
        sources = json.loads((directory / 'bundle-sources.json').read_bytes())
        if hashlib.md5(raw).hexdigest() != index[name]:
            raise ValueError('Official Wwise package MD5 differs from its manifest')
        versions.append({'path': (directory / name).relative_to(ROOT).as_posix(),
                         'source': sources[name], 'sha256': sha(raw), 'bytes': len(raw), **inspect(raw)})
    before, after = versions
    if before['media'] != after['media']:
        raise ValueError('Changed media requires audio conversion')
    if any(after['objects'].get(key) != value for key, value in before['objects'].items()):
        raise ValueError('Changed existing Wwise control object requires routing and mix conversion')
    mapping_versions = []
    mapping_name = 'wwisebnk_assets_wwisedata_android_eventbankmapping_bytes.rawfile'
    for directory in directories:
        raw = (directory / mapping_name).read_bytes()
        index = json.loads((directory / 'bundle-index.json').read_bytes())
        if hashlib.md5(raw).hexdigest() != index[mapping_name]:
            raise ValueError('Wwise mapping differs from its official manifest')
        mapping_versions.append({'path': (directory / mapping_name).relative_to(ROOT).as_posix(),
                                 'sha256': sha(raw), **mapping(raw)})
    a, b = mapping_versions
    if a['banks'] != b['banks'] or any(b['events'].get(key) != value for key, value in a['events'].items()):
        raise ValueError('Existing Wwise event routing changed')
    outputs = []
    for manifest in ('official-map/audio-source-manifest.json', 'official-map/music-source-manifest.json',
                     'match3-game/audio/source-manifest.json'):
        path = ROOT / 'assets/reference' / manifest
        source = json.loads(path.read_bytes())
        for uri, record in source.get('resources', {}).items():
            if not isinstance(record, dict):
                continue
            if name not in json.dumps(record) and record.get('source', {}).get('bundle_hash') != before['source'].get('bundle_hash'):
                continue
            target = ROOT / uri.removeprefix('res://')
            if sha(target.read_bytes()) != record['sha256']:
                raise ValueError('Integrated audio differs from its output receipt: ' + uri)
            outputs.append({'path': target.relative_to(ROOT).as_posix(), 'sha256': record['sha256'],
                            'original_receipt': path.relative_to(ROOT).as_posix()})
    result = {'manifest': version, 'base_manifest': '854_156383', 'operation': __doc__,
              'inputs': versions, 'verified_outputs': outputs,
              'identical_media': len(after['media']), 'identical_existing_objects': len(before['objects']),
              'added_objects': sorted(after['objects'].keys() - before['objects'].keys()),
              'event_bank_mapping': {'inputs': mapping_versions, 'identical_existing_events': len(a['events']),
                                     'added_events': sorted(b['events'].keys() - a['events'].keys())},
              'generator_sha256': sha(Path(__file__).read_bytes())}
    destination = ROOT / 'assets/reference/official-map/current-audio-source-manifest.json'
    if destination.exists():
        preserve(destination)
    destination.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8')
    return result


if __name__ == '__main__':
    result = build()
    print('Verified current audio:', result['identical_media'], 'media,',
          result['identical_existing_objects'], 'existing controls,', len(result['verified_outputs']), 'outputs')
