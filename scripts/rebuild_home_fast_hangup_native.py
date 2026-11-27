"""Replay an existing acceleration binding against its verified retained raw inputs.

Uses the migrated resource's own source selection. Missing old CDN files can be
recovered from native-conversion inputs only when those are unchanged raw bundle
bytes; both the pinned official manifest and the existing source chain must pass.
"""
import argparse
import hashlib
import json
from pathlib import Path
from types import MappingProxyType

from official_apk import Apk, ROOT
from official_bundle_reader import OfficialBundleReader, VerifiedBundle, verify_payload
from official_source import OfficialSource
from prepare_home_fast_hangup_native import HomeFastHangUpBinding, RECEIPT, install
from validation_workspace import validation_directory


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def rebuild(work, install_result=False, record_unchanged=False):
    prior = json.loads(RECEIPT.read_bytes())
    version = prior['manifest']
    cache = ROOT / '.tools/source-packages/current' / version / 'native-conversion'
    export = cache / 'native-export-all-source-manifest.json'
    relative = export.relative_to(ROOT).as_posix()
    if sha(export) != prior['inputs'][relative]:
        raise ValueError('Original acceleration native export receipt differs')
    proof = json.loads(export.read_bytes())
    inputs = cache / 'source-manifest.json'
    if not proof['completed'] or proof['manifest'] != version or sha(inputs) != proof['input_manifest_sha256']:
        raise ValueError('Retained original conversion inputs differ')
    source = json.loads(inputs.read_bytes())
    if source['manifest'] != version:
        raise ValueError('Retained conversion source selection differs')
    retained = {(row['package'], row['name']): row for row in source['inputs'] if 'name' in row}
    original_current = Apk.__dict__['current']
    original_read = OfficialBundleReader.read
    original_source_init = OfficialSource.__init__
    used = {}
    raw_inputs = {}

    def select_source(self, manifest=None):
        original_source_init(self, manifest or version)

    def read(reader, package, name):
        try:
            return original_read(reader, package, name)
        except FileNotFoundError:
            if reader.source.version != version or (package, name) not in retained:
                raise
            row = retained[package, name]
            path = (cache / row['output']).resolve()
            if not path.is_relative_to(cache.resolve()) or row['encrypted']:
                raise ValueError('Retained bundle is not an unchanged raw input: ' + name)
            data = path.read_bytes()
            digest = hashlib.sha256(data).hexdigest()
            if digest != row['raw_sha256'] or digest != row['decoded_sha256']:
                raise ValueError('Retained original bundle bytes differ: ' + name)
            reader.manifest(package)
            verify_payload(data, reader._indexes[package][name], identity=name)
            if row['source'].get('sha256', row['source'].get('raw_sha256')) != digest:
                raise ValueError('Retained original source receipt differs: ' + name)
            used[path.relative_to(ROOT).as_posix()] = digest
            raw_inputs[path.relative_to(ROOT).as_posix()] = digest
            return VerifiedBundle(data, MappingProxyType(row['source']))

    binding = None
    try:
        Apk.current = classmethod(lambda cls: cls(version, reuse_current_cache=True))
        OfficialSource.__init__ = select_source
        OfficialBundleReader.read = read
        binding = HomeFastHangUpBinding(work)
        code_path = cache.parent / 'code-manifest.json'
        if sha(code_path) != proof['code_manifest_sha256']:
            raise ValueError('Retained original managed source receipt differs')
        managed = cache / 'inputs/Managed'
        code = json.loads(code_path.read_bytes())
        for name, row in code['assemblies'].items():
            path = managed / name
            if sha(path) != row['decoded_sha256']:
                raise ValueError('Retained original managed assembly differs: ' + name)
            used[path.relative_to(ROOT).as_posix()] = row['decoded_sha256']
        used[code_path.relative_to(ROOT).as_posix()] = sha(code_path)
        binding.recovery.schema_managed = managed
        binding.build()
    finally:
        if binding: binding.recovery.apk.reader.close()
        Apk.current = original_current
        OfficialBundleReader.read = original_read
        OfficialSource.__init__ = original_source_init
    staged = work / RECEIPT.name
    result = json.loads(staged.read_bytes())
    result['inputs'].update(used)
    result['inputs'].update({path.relative_to(ROOT).as_posix(): sha(path) for path in (Path(__file__), inputs)})
    result['retained_raw_input_replay'] = dict(source=version, raw_inputs=raw_inputs,
        validation='Existing native export chain, original raw SHA256 and decoded SHA256, pinned bundle MD5 and size.')
    staged.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    if record_unchanged:
        # A controller refactor does not reinstall or rewrite shared assets.
        # Retain every existing resource expectation, including shared fonts;
        # a resource verifier continues to detect any separate workspace edits.
        if install_result or result['outputs'] != prior['outputs'] or result['roots'] != prior['roots']:
            raise ValueError('Recording source bindings requires identical original resource outputs and roots')
        for name, row in prior['script_bindings'].items():
            if result['script_bindings'][name]['pointer'] != row['pointer']:
                raise ValueError('Native controller script identity changed: ' + name)
        for relative, expected in prior['installed_output_sha256'].items():
            if sha(ROOT / relative) != expected:
                raise ValueError('Owned acceleration resource changed: ' + relative)
        result['installed_output_sha256'] = prior['installed_output_sha256']
        result['reused'] = prior['reused']
        RECEIPT.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    elif install_result:
        install(work)
    print('Replayed original acceleration source:', version, len(raw_inputs), 'retained raw bundles')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    parser.add_argument('--record-unchanged', action='store_true',
        help='Record rebuilt controller bindings only; require identical outputs and preserve all resource hashes')
    args = parser.parse_args()
    rebuild(validation_directory(), args.install, args.record_unchanged)
