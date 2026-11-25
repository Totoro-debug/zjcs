"""Verify every input and output in a native home asset conversion cache."""
import hashlib
import json


def _sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def verify_native_cache(cache, prefabs, current_manifest):
    receipt = cache / 'native-export-source-manifest.json'
    export = cache / 'native-export'
    if not receipt.is_file() or not export.is_dir():
        raise ValueError('Incomplete native export; retain it for inspection: ' + str(cache))
    proof = json.loads(receipt.read_text(encoding='utf8'))
    if (not proof.get('completed') or proof.get('manifest') != current_manifest
            or proof.get('input_manifest_sha256') != _sha(cache / 'source-manifest.json')
            or proof.get('code_manifest_sha256') != _sha(cache.parent / 'code-manifest.json')):
        raise ValueError('Native export does not match its official source inputs: ' + str(cache))
    outputs = {row['path']: row['sha256'] for row in proof['outputs']}
    if len(outputs) != len(proof['outputs']):
        raise ValueError('Duplicate paths in native export receipt')
    actual = {path.relative_to(export).as_posix() for path in export.rglob('*') if path.is_file()}
    if actual != set(outputs):
        raise ValueError('Native export files differ from its receipt')
    for name, expected in outputs.items():
        path = (export / name).resolve()
        if not path.is_relative_to(export.resolve()) or _sha(path) != expected:
            raise ValueError('Native export file differs from its receipt: ' + name)
    source = json.loads((cache / 'source-manifest.json').read_text(encoding='utf8'))
    for row in source['inputs']:
        path = (cache / row['output']).resolve()
        if not path.is_relative_to(cache.resolve()) or _sha(path) != row['decoded_sha256']:
            raise ValueError('Native conversion input differs from its receipt: ' + row['output'])
    for row in proof['type_dependencies']:
        path = (cache / row['output']).resolve()
        if not path.is_relative_to(cache.resolve()) or _sha(path) != row['sha256']:
            raise ValueError('Native type descriptor differs from its receipt: ' + row['output'])
    code = json.loads((cache.parent / 'code-manifest.json').read_text(encoding='utf8'))
    for name, row in code['assemblies'].items():
        path = (cache / 'inputs/Managed' / name).resolve()
        if not path.is_relative_to((cache / 'inputs/Managed').resolve()) or _sha(path) != row['decoded_sha256']:
            raise ValueError('Native managed input differs from its receipt: ' + name)
    for name in prefabs:
        if 'ExportedProject/Assets/' + name not in outputs:
            raise ValueError('Native prefab is missing from its receipt: ' + name)
    print('Verified native export:', len(outputs), 'files;', len(prefabs), 'prefabs')
    return cache
