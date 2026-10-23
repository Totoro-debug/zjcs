"""Verify concurrent native regeneration against preserved current mesh payloads."""
from __future__ import annotations

import collections
import hashlib
import json
import math
from pathlib import Path
import re
import struct

from official_apk import ROOT
from refresh_current_character_meshes import preserve


def sha(data):
    return hashlib.sha256(data).hexdigest()


def meshes(data):
    result = {}
    text = data.decode('utf8')
    for match in re.finditer(r'^--- !u!43[^\n]*\nMesh:\n.*?(?=^---|\Z)', text, re.M | re.S):
        block = match[0].split('\n', 1)[1]
        label = re.search(r'^  m_Name: (.+)$', block, re.M)[1]
        if label in result:
            raise ValueError('Duplicate native mesh: ' + label)
        result[label] = block
    return result


def verify():
    source_path = ROOT / 'assets/reference/character-creation/current-native-prefabs-source-manifest.json'
    source_bytes = source_path.read_bytes()
    source = json.loads(source_bytes)
    records = []
    counts = collections.Counter()
    max_error = 0.0
    for row in source['outputs']:
        path = ROOT / row['path']
        current = path.read_bytes()
        original_path = ROOT / '.tools/source-packages/history/converted' / (row['sha256'] + '.bin')
        original = original_path.read_bytes()
        if sha(original) != row['sha256']:
            raise ValueError('Preserved converted mesh hash differs: ' + str(original_path))
        before, after = meshes(original), meshes(current)
        if before.keys() != after.keys():
            raise ValueError('Native mesh set changed: ' + row['path'])
        for label, block in before.items():
            other = after[label]
            if block != other:
                pattern = r'(?m)^(    _typelessdata: )([0-9a-f]+)$'
                a, b = re.search(pattern, block), re.search(pattern, other)
                if a is None or b is None or re.sub(pattern, r'\1<float-buffer>', block) != re.sub(pattern, r'\1<float-buffer>', other):
                    raise ValueError('Native mesh metadata or topology changed: ' + row['path'] + ':' + label)
                av, bv = bytes.fromhex(a[2]), bytes.fromhex(b[2])
                if len(av) != len(bv) or len(av) % 4:
                    raise ValueError('Native vertex layout changed: ' + label)
                channels = [tuple(map(int, values)) for values in re.findall(
                    r'^    - stream: (\d+)\n      offset: (\d+)\n      format: (\d+)\n      dimension: (\d+)$', block, re.M)]
                vertices = int(re.search(r'^    m_VertexCount: (\d+)$', block, re.M)[1])
                strides = {}
                for stream, offset, fmt, dimension in channels:
                    if not dimension:
                        continue
                    if fmt not in (0, 10):
                        raise ValueError('Unsupported native vertex format: ' + label)
                    strides[stream] = max(strides.get(stream, 0), offset + 4 * dimension)
                starts, position = {}, 0
                for stream, stride in sorted(strides.items()):
                    starts[stream] = position
                    position = (position + stride * vertices + 15) & ~15
                float_words = set()
                for stream, offset, fmt, dimension in channels:
                    if dimension and fmt == 0:
                        for vertex in range(vertices):
                            base = starts[stream] + vertex * strides[stream] + offset
                            float_words.update((base // 4 + component) for component in range(dimension))
                error = 0.0
                for word in range(len(av) // 4):
                    left_bytes, right_bytes = av[word * 4:word * 4 + 4], bv[word * 4:word * 4 + 4]
                    if left_bytes == right_bytes:
                        continue
                    if word not in float_words:
                        raise ValueError('Native integer vertex or padding data changed: ' + label)
                    left, right = struct.unpack('<f', left_bytes)[0], struct.unpack('<f', right_bytes)[0]
                    if not math.isfinite(left) or not math.isfinite(right):
                        raise ValueError('Non-finite native vertex: ' + label)
                    error = max(error, abs(left - right))
                if not error <= 0.000001:
                    raise ValueError('Native vertex data changed beyond float rounding: ' + label + ':' + str(error))
                max_error = max(max_error, error)
                counts['meshes_with_float_rounding'] += 1
            counts['verified_meshes'] += 1
        if path.read_bytes() != current:
            raise ValueError('Native regeneration is still writing: ' + row['path'])
        records.append({'path': row['path'], 'previous_sha256': row['sha256'],
                        'sha256': sha(current), 'bytes': len(current)})
        print('Verified native variant ' + str(len(records)) + '/' + str(len(source['outputs'])), flush=True)
    for row in records:
        if sha((ROOT / row['path']).read_bytes()) != row['sha256']:
            raise ValueError('Native regeneration changed during verification: ' + row['path'])
    result = {'manifest': source['manifest'], 'operation': __doc__,
              'basis': {'path': source_path.relative_to(ROOT).as_posix(), 'sha256': sha(source_bytes)},
              'scope': 'Mesh names, topology, metadata and Float32 vertex payloads; concurrent animation generation is not attributed to this converter.',
              'counts': dict(counts), 'maximum_float_error': max_error,
              'generator_sha256': sha(Path(__file__).read_bytes()), 'outputs': records}
    receipt = ROOT / 'assets/reference/character-creation/current-native-concurrent-bindings-source-manifest.json'
    if receipt.exists():
        preserve(receipt)
    receipt.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8')
    return result


if __name__ == '__main__':
    print(json.dumps(verify()['counts']))
