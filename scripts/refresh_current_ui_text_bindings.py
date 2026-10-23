"""Bind integrated UI localization keys to verified current Chinese rawfiles."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

import xxhash

from activate_current_payloads import activate
from current_conversion_state import activated_receipt
from official_apk import ROOT
from official_source import OfficialSource
from refresh_current_localization import language, sha


def build(activate_outputs=False):
    version = OfficialSource().version
    receipt = ROOT / 'assets/reference/official-map/current-ui-text-bindings-source-manifest.json'
    prior = activated_receipt(receipt, version)
    if prior is not None:
        return prior
    texts, inputs = language(ROOT / '.tools/source-packages/WwiseBnk')
    stage = ROOT / '.tools/source-packages/current' / version / 'ui-text-bindings'
    stage.mkdir(parents=True, exist_ok=True)
    outputs = []
    paths = set()
    for name in ('current-ui-graphics-source-manifest.json', 'current-language-source-manifest.json'):
        source = ROOT / 'assets/reference/official-map' / name
        document = json.loads(source.read_bytes())
        paths.update(ROOT / row['path'] for row in document['outputs'])
    for path in sorted(paths):
        if 'manifest' in path.name:
            continue
        raw = path.read_bytes()
        if b'localization_key' not in raw:
            continue
        data = json.loads(raw)
        changes = []

        def walk(value):
            if isinstance(value, dict):
                key = value.get('localization_key')
                if key:
                    text = texts.get(xxhash.xxh64_intdigest(key.encode('utf8')))
                    if text is not None:
                        label = value.get('text')
                        if isinstance(label, dict) and label.get('value') != text:
                            changes.append({'path': value.get('path'), 'key': key,
                                            'previous': label.get('value'), 'current': text})
                            label['value'] = text
                        elif 'text_value' in value and value['text_value'] != text:
                            changes.append({'path': value.get('path'), 'key': key,
                                            'previous': value['text_value'], 'current': text})
                            value['text_value'] = text
                for child in value.values():
                    walk(child)
            elif isinstance(value, list):
                for child in value:
                    walk(child)

        walk(data)
        if not changes:
            continue
        payload = json.dumps(data, ensure_ascii=False, separators=(',', ':')).encode('utf8')
        target = stage / (sha(payload) + '.json')
        target.write_bytes(payload)
        outputs.append({'path': path.relative_to(ROOT).as_posix(), 'previous_sha256': sha(raw),
                        'sha256': sha(payload), 'bytes': len(payload),
                        'staged': target.relative_to(ROOT).as_posix(), 'changes': changes})
        print(path.relative_to(ROOT), len(changes), flush=True)
    result = {'manifest': version, 'operation': __doc__, 'inputs': inputs,
              'outputs': outputs, 'generator_sha256': sha(Path(__file__).read_bytes())}
    source = stage / 'source-manifest.json'
    source.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8')
    return activate(source, Path(__file__), receipt) if activate_outputs else result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--activate', action='store_true')
    print('Updated UI text payloads:', len(build(parser.parse_args().activate)['outputs']))
