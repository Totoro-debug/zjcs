"""Refresh explicitly bound material properties from the active official bundles."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from refresh_current_character_meshes import preserve


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def build(activate=False):
    current = Apk.current()
    from current_conversion_state import activated_receipt
    prior = activated_receipt(ROOT / 'assets/reference/official-map/current-material-source-manifest.json', current.version)
    if prior is not None:
        return prior
    apks = [Apk(), current]
    root = ROOT / '.tools/source-packages/current' / current.version
    comparison = json.loads((root / 'referenced-object-comparison.json').read_bytes())
    changed = {(row['bundle'], str(row['path_id'])): row for row in comparison
               if row.get('type') == 'Material' and not row.get('equal')}
    cache = {}
    proofs = {}

    def material(key):
        if key in proofs:
            return proofs[key]
        row = changed[key]
        if any(not field.startswith('/m_SavedProperties/m_Floats') for field in row['changed_fields']):
            raise ValueError('Material needs an additional conversion: ' + str(key))
        versions = []
        for version, apk in enumerate(apks):
            cache_key = (version, key[0])
            if cache_key not in cache:
                cache[cache_key] = {str(obj.path_id): obj for obj in apk.objects(key[0])
                                    if obj.type.name == 'Material'}
            obj = cache[cache_key][key[1]]
            raw = obj.get_raw_data()
            tree = obj.read_typetree()
            versions.append({'floats': dict(tree['m_SavedProperties']['m_Floats']),
                             'source': apk.provenance(obj), 'raw_sha256': sha(raw)})
        proofs[key] = versions
        return versions

    staged = root / 'material-payloads'
    staged.mkdir(parents=True, exist_ok=True)
    outputs = []
    paths = set((ROOT / 'assets/official').glob('**/*.json'))
    paths.update((ROOT / 'unity/Assets/Official').glob('**/*.json'))
    for path in sorted(paths):
        raw = path.read_bytes()
        if b'"floats"' not in raw or b'"source"' not in raw:
            continue
        document = json.loads(raw)
        bindings = []

        def walk(value, location=''):
            if isinstance(value, list):
                for index, child in enumerate(value):
                    walk(child, location + '/' + str(index))
            elif isinstance(value, dict):
                source = value.get('source', {})
                if not isinstance(source, dict):
                    source = {}
                key = (source.get('bundle'), str(source.get('path_id')))
                if key in changed and 'floats' in value:
                    before, after = material(key)
                    if value['floats'] not in (before['floats'], after['floats']):
                        raise ValueError('Material properties differ from their source: ' + str(path) + location)
                    if value['floats'] != after['floats'] or source.get('manifest') != current.version:
                        value['floats'] = after['floats']
                        value['source'] = after['source']
                        bindings.append({'pointer': location, 'bundle': key[0], 'path_id': key[1]})
                for name, child in list(value.items()):
                    walk(child, location + '/' + name)

        walk(document)
        if not bindings:
            continue
        payload = json.dumps(document, ensure_ascii=False, separators=(',', ':')).encode('utf8')
        destination = staged / (sha(payload) + '.json')
        destination.write_bytes(payload)
        outputs.append({'path': path.relative_to(ROOT).as_posix(), 'previous_sha256': sha(raw),
                        'sha256': sha(payload), 'bytes': len(payload),
                        'staged': destination.relative_to(ROOT).as_posix(), 'bindings': bindings})
        if path.is_relative_to(ROOT / 'unity'):
            print(outputs[-1]['path'], len(bindings), flush=True)
    receipt = {'manifest': current.version, 'base_manifest': apks[0].version,
               'operation': __doc__, 'materials': [{'bundle': key[0], 'path_id': key[1], 'versions': value}
                                                   for key, value in proofs.items()],
               'outputs': outputs, 'generator_sha256': sha(Path(__file__).read_bytes())}
    (staged / 'source-manifest.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8')
    if activate:
        for record in outputs:
            if sha((ROOT / record['path']).read_bytes()) != record['previous_sha256']:
                raise ValueError('Material payload changed during staging: ' + record['path'])
        for record in outputs:
            target = ROOT / record['path']
            record['previous'] = preserve(target)
            temporary = target.with_name(target.name + '.current.tmp')
            temporary.write_bytes((ROOT / record['staged']).read_bytes())
            temporary.replace(target)
        target = ROOT / 'assets/reference/official-map/current-material-source-manifest.json'
        if target.exists():
            preserve(target)
        target.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8')
    return receipt


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--activate', action='store_true')
    print('Converted material payloads:', len(build(parser.parse_args().activate)['outputs']))
