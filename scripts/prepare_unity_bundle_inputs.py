"""Inspect pinned Android bundles and optionally stage their CAB dependency closure.

This prepares verified converter inputs, not Windows-ready assets. No downloads,
counterpart substitution, Unity Editor invocation, or game asset writes occur.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import sys
import tempfile

from official_apk import Apk, ROOT
from fetch_official_assets import SHA256 as APK_SHA256


def serialized_files(file):
    if hasattr(file, 'objects'):
        yield file
    else:
        for child in getattr(file, 'files', {}).values():
            yield from serialized_files(child)


def collect(apk, names):
    """Resolve every external serialized file, including transitive dependencies."""
    for name in names:
        if name not in apk.index:
            raise ValueError('Use an exact bundle name from manifest ' + apk.version + ': ' + name)
        apk.load(name)
    processed = set()
    assets = {}
    missing = {}
    while pending := sorted(set(apk.loaded) - processed):
        for name in pending:
            processed.add(name)
            for asset in serialized_files(apk.loaded[name]):
                externals = [external.path for external in asset.externals]
                assets[asset.name.lower()] = {
                    'serialized_file': asset.name,
                    'source': name,
                    'unity_version': asset.unity_version,
                    'build_target': int(asset.target_platform),
                    'build_target_name': getattr(asset.target_platform, 'name', str(asset.target_platform)),
                    'externals': externals,
                }
                for path in externals:
                    try:
                        dependency = apk.env.find_file(path)
                        if dependency is None:
                            raise FileNotFoundError('Serialized file was not resolved: ' + path)
                    except FileNotFoundError as error:
                        missing[(asset.name, path)] = str(error)
    return assets, missing


def read_source(apk, name):
    """Verify original bytes independently of the decoded UnityPy objects."""
    if name.startswith('assets/bin/Data/'):
        raw = apk.archive.read(name)
        source = {'origin': 'apk_zip', 'apk_entry': name, 'apk_sha256': APK_SHA256}
    else:
        record = apk.bundle_records[name]
        entry = 'assets/yoo/DefaultPackage/' + record['file']
        if entry in apk.available:
            raw = apk.archive.read(entry)
            source = {'origin': 'apk_zip', 'apk_entry': entry, 'apk_sha256': APK_SHA256}
        else:
            cached = apk.cloud.read('DefaultPackage', record)
            if cached is None:
                raise FileNotFoundError('Not packaged or cached: ' + name)
            raw, source = cached
        if len(raw) != record['size'] or hashlib.md5(raw).hexdigest() != record['hash']:
            raise ValueError('Pinned manifest size/MD5 mismatch: ' + name)
    decoded = Apk.decode(raw)
    return decoded, dict(source, name=name, manifest=apk.version,
                         raw_size=len(raw), raw_md5=hashlib.md5(raw).hexdigest(),
                         raw_sha256=hashlib.sha256(raw).hexdigest(),
                         decoded_size=len(decoded), decoded_sha256=hashlib.sha256(decoded).hexdigest(),
                         decoding='xor_0x40' if decoded != raw else 'none')


def prepare(names, stage_dir=None, verbose=False, include_unity_builtins=False, manifest=None):
    destination = None
    if stage_dir:
        destination = Path(stage_dir).resolve()
        output_root = (ROOT / 'output').resolve()
        if not destination.is_relative_to(output_root) or destination == output_root:
            raise ValueError('--stage-dir must be a new subdirectory inside this repository/output/')
        if destination.exists():
            raise FileExistsError('Refusing to overwrite an existing input directory: ' + str(destination))
    apk = Apk(manifest=manifest)
    try:
        assets, missing = collect(apk, names)
        for asset in sorted(assets.values(), key=lambda a: (a['source'], a['serialized_file'])):
            if not verbose and asset['source'] not in names:
                continue
            print(f"{asset['source']}: {asset['serialized_file']} Unity={asset['unity_version']} "
                  f"target={asset['build_target_name']}({asset['build_target']}) externals={len(asset['externals'])}", flush=True)
        for (owner, path), error in sorted(missing.items()):
            print(f'MISSING: {owner} -> {path}: {error}', file=sys.stderr)
        if missing:
            print('Dependency closure incomplete; no converter inputs were staged.', file=sys.stderr)
            return 1
        # Verify all source bytes even when only inspecting. Keep decoded payloads
        # on disk only in staging mode to avoid holding a second full copy in RAM.
        records = []
        builtin_records = []
        def write_inputs(folder=None):
            used = set()
            for name in sorted(apk.loaded):
                decoded, record = read_source(apk, name)
                filename = Path(name).name
                if filename.casefold() in used:
                    raise ValueError('Converter input filename collision: ' + filename)
                used.add(filename.casefold())
                record['output'] = filename
                records.append(record)
                if folder is not None:
                    (folder / filename).write_bytes(decoded)
            if include_unity_builtins:
                builtin_path = 'assets/bin/Data/unity default resources'
                raw = apk.archive.read(builtin_path)
                filename = Path(builtin_path).name
                existing = next((record for record in records
                                 if record.get('name') == builtin_path and record.get('output') == filename), None)
                record = {
                    'name': builtin_path,
                    'origin': 'apk_zip',
                    'apk_sha256': APK_SHA256,
                    'raw_size': len(raw),
                    'raw_sha256': hashlib.sha256(raw).hexdigest(),
                    'output': filename,
                }
                builtin_records.append(record)
                if existing is not None:
                    if existing.get('raw_sha256') != record['raw_sha256']:
                        raise ValueError('Staged Unity built-in resource does not match the pinned APK')
                elif filename.casefold() in used:
                    raise ValueError('Converter input filename collision: ' + filename)
                else:
                    used.add(filename.casefold())
                if folder is not None and existing is None:
                    (folder / filename).write_bytes(raw)
        if destination:
            destination.parent.mkdir(parents=True, exist_ok=True)
            with tempfile.TemporaryDirectory(prefix='.unity-bundle-inputs-', dir=destination.parent) as temporary:
                folder = Path(temporary)
                write_inputs(folder)
                manifest = {'format': 1, 'manifest': apk.version, 'requested_bundles': names,
                            'inputs': records, 'builtin_inputs': builtin_records,
                            'serialized_files': list(assets.values())}
                (folder / 'source-manifest.json').write_text(
                    json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
                folder.rename(destination)
            print('Converter input directory: ' + str(destination))
        else:
            write_inputs()
        print(f'Verified {len(records)} bundle source files, {len(builtin_records)} built-in resources, '
              f'{len(assets)} serialized files; CAB closure resolved. '
              'This does not verify object decoding, restored scripts, shaders, or Windows rendering.')
        return 0
    finally:
        apk.archive.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--bundle', required=True, action='append', help='Exact pinned bundle name; repeat for multiple roots')
    parser.add_argument('--stage-dir', help='New directory inside output/; omit to inspect without staging')
    parser.add_argument('--verbose', action='store_true', help='Print metadata for transitive dependencies as well as root bundles')
    parser.add_argument('--include-unity-builtins', action='store_true',
                        help='Stage the exact unity default resources file from the pinned APK')
    parser.add_argument('--manifest', help='Explicit historical manifest; defaults to the active official source')
    args = parser.parse_args()
    try:
        return prepare(list(dict.fromkeys(args.bundle)), args.stage_dir, args.verbose,
                       args.include_unity_builtins, args.manifest)
    except (OSError, ValueError, KeyError) as error:
        print('ERROR: ' + str(error), file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main())
