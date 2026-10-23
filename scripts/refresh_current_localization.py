"""Refresh generated Chinese text from verified active Wwise language rawfiles."""
from __future__ import annotations

import argparse
from collections import defaultdict
import hashlib
import json
from pathlib import Path
import struct

import xxhash

from official_apk import ROOT
from official_source import OfficialSource
from refresh_current_character_meshes import preserve


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def language(directory):
    records = []
    result = {}
    index = json.loads((directory / 'bundle-index.json').read_bytes())
    sources = json.loads((directory / 'bundle-sources.json').read_bytes())
    for name in ('wwisebnk_assets_launch_config_binary_language_zh_cn_text_g_bytes.rawfile',
                 'wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile'):
        raw = (directory / name).read_bytes()
        if hashlib.md5(raw).hexdigest() != index[name]:
            raise ValueError('Official language rawfile differs from manifest: ' + name)
        count = struct.unpack_from('<i', raw)[0]
        start = 4 + count * 12
        for i in range(count):
            identity, offset = struct.unpack_from('<Qi', raw, 4 + 12*i)
            position = start + offset
            size = struct.unpack_from('<H', raw, position)[0]
            if offset < 0 or position + 2 + size > len(raw):
                raise ValueError('Official language entry is out of bounds')
            result[identity] = raw[position+2:position+2+size].decode('utf8')
        records.append({'path': (directory / name).relative_to(ROOT).as_posix(),
                        'sha256': sha(raw), 'entries': count, 'source': sources[name]})
    return result, records


def build(activate=False):
    version = OfficialSource().version
    from current_conversion_state import activated_receipt
    prior = activated_receipt(ROOT / 'assets/reference/official-map/current-language-source-manifest.json', version)
    if prior is not None:
        return prior
    old, old_inputs = language(ROOT / '.tools/source-packages/history/854_156383/WwiseBnk')
    current, new_inputs = language(ROOT / '.tools/source-packages/WwiseBnk')
    alternatives = defaultdict(set)
    for identity, text in old.items():
        alternatives[text].add(current.get(identity))
    unambiguous = {text: next(iter(values)) for text, values in alternatives.items()
                   if len(values) == 1 and text not in values and None not in values}
    signatures = [text.encode('utf8') for text in unambiguous]
    paths = set((ROOT / 'assets/official').glob('**/*.json'))
    paths.update((ROOT / 'unity/Assets/Official').glob('**/*.json'))
    paths.update((ROOT / 'unity/Assets/StreamingAssets').glob('**/*.json'))
    paths.update((ROOT / 'unity/Assets/Game/Resources').glob('**/*.json'))
    output = ROOT / '.tools/source-packages/current' / version / 'language-payloads'
    output.mkdir(parents=True, exist_ok=True)
    converted = []
    for path in sorted(paths):
        if path.name.endswith('manifest.json'):
            continue
        raw = path.read_bytes()
        if not any(signature in raw for signature in signatures):
            continue
        document = json.loads(raw)
        changes = []

        def walk(value, location=''):
            if isinstance(value, str):
                if value in unambiguous:
                    changes.append({'pointer': location, 'old': value, 'new': unambiguous[value]})
                    return unambiguous[value]
                return value
            if isinstance(value, list):
                return [walk(child, location + '/' + str(i)) for i, child in enumerate(value)]
            if isinstance(value, dict):
                result = {}
                for key, child in value.items():
                    hashed = xxhash.xxh64_intdigest(key.encode('utf8'))
                    if isinstance(child, str) and hashed in old and child == old[hashed] and current.get(hashed) != child:
                        if hashed not in current:
                            canonical = key.strip()
                            canonical_hash = xxhash.xxh64_intdigest(canonical.encode('utf8'))
                            if canonical == key or current.get(canonical_hash) != child:
                                raise ValueError('An integrated language key was removed: ' + str(path) + ':' + key)
                            # This official key lost a trailing space. Keep the
                            # existing runtime alias and bind the actual new key.
                            result[key] = child
                            result[canonical] = current[canonical_hash]
                            changes.append({'pointer': location + '/' + key, 'key': key,
                                            'canonical_key': canonical, 'operation': 'retain_legacy_alias',
                                            'old': child, 'new': current[canonical_hash]})
                            continue
                        changes.append({'pointer': location + '/' + key, 'key': key,
                                        'old': child, 'new': current[hashed]})
                        result[key] = current[hashed]
                    else:
                        result[key] = walk(child, location + '/' + key)
                return result
            return value

        after = walk(document)
        if not changes:
            continue
        payload = json.dumps(after, ensure_ascii=False, separators=(',', ':')).encode('utf8')
        staged = output / (sha(payload) + '.json')
        staged.write_bytes(payload)
        converted.append({'path': path.relative_to(ROOT).as_posix(), 'previous_sha256': sha(raw),
                          'sha256': sha(payload), 'bytes': len(payload),
                          'staged': staged.relative_to(ROOT).as_posix(), 'changes': changes})
        print(converted[-1]['path'], len(changes), flush=True)
    receipt = {'manifest': version, 'base_manifest': '854_156383', 'operation': __doc__,
               'inputs': {'previous': old_inputs, 'current': new_inputs}, 'outputs': converted,
               'generator_sha256': sha(Path(__file__).read_bytes())}
    (output / 'source-manifest.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8')
    if activate:
        for record in converted:
            if sha((ROOT / record['path']).read_bytes()) != record['previous_sha256']:
                raise ValueError('Localized payload changed during staging: ' + record['path'])
        for record in converted:
            target = ROOT / record['path']
            record['previous'] = preserve(target)
            temporary = target.with_name(target.name + '.current.tmp')
            temporary.write_bytes((ROOT / record['staged']).read_bytes())
            temporary.replace(target)
        destination = ROOT / 'assets/reference/official-map/current-language-source-manifest.json'
        if destination.exists():
            preserve(destination)
        destination.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8')
    return receipt


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--activate', action='store_true')
    print('Converted language payloads:', len(build(parser.parse_args().activate)['outputs']))
