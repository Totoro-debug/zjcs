"""Verify and activate all five official raw package views, retaining history."""
import argparse
import hashlib
import json
from pathlib import Path

from official_source import OfficialSource, CACHE
from official_cloud_cache import CloudCache
from fetch_official_assets import extract
from official_manifest import read_asset_manifest


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    temporary.replace(path)


def activate(manifest=None):
    source = OfficialSource(manifest)
    if source.version != OfficialSource().version:
        raise ValueError('Select the active profile in official-source.json before activation')
    with source.apk.open('rb') as stream:
        if hashlib.file_digest(stream, 'sha256').hexdigest() != source.apk_sha256:
            raise ValueError('APK SHA256 mismatch')
    inventory = source.inventory(verify_packaged=True)
    cloud = CloudCache(source.cloud_path, source.version)
    remote = 0
    for bundle in inventory['bundles']:
        if bundle['packaged']:
            continue
        if cloud.read(bundle['package'], bundle) is None:
            raise FileNotFoundError(bundle['package'] + '/' + bundle['file'])
        remote += 1
    print('Verified APK and', remote, 'remote files', flush=True)

    marker = CACHE / 'active-source.json'
    prior = json.loads(marker.read_text(encoding='utf-8'))['manifest'] if marker.exists() else source.apk_version
    history = CACHE / 'history' / prior
    # Check every resolved move target before performing directory mutations.
    views = ['extracted', 'Boot', 'Launch', 'HybridCLR', 'WwiseBnk']
    if prior != source.version:
        moves = [(CACHE / name, history / name) for name in views]
        moves += [(CACHE / name, history / name) for name in
                  ['inventory.json', 'manifest-assets.json', 'cloud-research/bundle-inventory.json']]
        for original, backup in moves:
            if not original.resolve().is_relative_to(CACHE.resolve()) or not backup.resolve().is_relative_to(CACHE.resolve()):
                raise ValueError('History path outside source cache')
            if original.exists() and backup.exists():
                raise FileExistsError('Existing history must be inspected: ' + str(backup))
        for original, backup in moves:
            if original.exists():
                backup.parent.mkdir(parents=True, exist_ok=True)
                original.rename(backup)
        write_json(marker, dict(manifest=source.version, apk_manifest=source.apk_version,
                                resource_version=source.profile['resource_version'], complete=False))

    for package in source.manifests:
        destination = CACHE / ('extracted' if package == 'DefaultPackage' else package)
        destination.mkdir(parents=True, exist_ok=True)
        extract('.*', package, source.version, quiet=True)
        expected = {b['name']: b for b in inventory['bundles'] if b['package'] == package}
        for name, bundle in expected.items():
            path = destination / name
            if not path.exists() or path.stat().st_size != bundle['size']:
                raise ValueError('Incomplete extraction: ' + name)
        write_json(destination / 'source-profile.json', dict(manifest=source.version, count=len(expected)))
        print('Activated', package, len(expected), flush=True)
    default = [b for b in inventory['bundles'] if b['package'] == 'DefaultPackage']
    write_json(CACHE / 'inventory.json', [dict(bundle=b['name'], hash=b['hash'], packaged=b['packaged']) for b in default])
    write_json(CACHE / 'manifest-assets.json', read_asset_manifest(source.manifest_bytes('DefaultPackage')))
    write_json(CACHE / 'cloud-research/bundle-inventory.json', inventory)
    write_json(marker, dict(manifest=source.version, apk_manifest=source.apk_version,
                            resource_version=source.profile['resource_version'], complete=True,
                            bundles=len(inventory['bundles']), remote=remote))
    return inventory


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--manifest')
    activate(parser.parse_args().manifest)
