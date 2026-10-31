"""Prepare a non-destructive APK/CDN union for native Unity conversion.

Bundle identity is (package, logical name), never a hash filename or PathID.
Current CDN bundles override APK bundles with the same identity. APK-only
bundles and both versions of the asset-path catalog remain available.
This prepares converter inputs; Android bundles are not Windows runtime assets.
"""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile

from official_apk import Apk, ROOT
from official_source import OfficialSource
from official_cloud_cache import CloudCache
from official_manifest import read_manifest, read_asset_manifest


def union(old, new, key):
    result = {}
    for layer in (old, new):
        seen = {}
        for row in layer:
            identity = key(row)
            if identity in seen and seen[identity] != row:
                raise ValueError('Ambiguous resource identity: ' + str(identity))
            seen[identity] = row
        result.update(seen)
    return list(result.values())


def sha(data):
    return hashlib.sha256(data).hexdigest()


def prepare(destination):
    source = OfficialSource()
    destination = destination.resolve()
    allowed = (ROOT / '.tools/source-packages/current' / source.version).resolve()
    if not destination.is_relative_to(allowed) or destination == allowed:
        raise ValueError('Use a dedicated conversion cache below ' + str(allowed))
    destination.mkdir(parents=True, exist_ok=True)
    inputs = destination / 'inputs'
    inputs.mkdir(exist_ok=True)
    with source.apk.open('rb') as stream:
        if hashlib.file_digest(stream, 'sha256').hexdigest() != source.apk_sha256:
            raise ValueError('APK SHA256 mismatch')
    cloud = CloudCache(source.cloud_path, source.version)
    bundles, catalogs, unavailable = [], {}, []
    with zipfile.ZipFile(source.apk) as archive:
        available = set(archive.namelist())
        for package in source.manifests:
            apk_entry = f'assets/yoo/{package}/PackageManifest_{package}_{source.apk_version}.v3.bytes'
            original_bytes = archive.read(apk_entry)
            current_bytes = source.manifest_bytes(package, archive)
            old, new = read_manifest(original_bytes), read_manifest(current_bytes)
            original = [dict(b, package=package, source_manifest=source.apk_version) for b in old['bundles']]
            current = [dict(b, package=package, source_manifest=source.version) for b in new['bundles']]
            selected = union(original, current, lambda b: (b['package'], b['name']))
            catalogs[package] = dict(
                apk_manifest_sha256=sha(original_bytes), current_manifest_sha256=sha(current_bytes),
                apk=read_asset_manifest(original_bytes), current=read_asset_manifest(current_bytes),
                merged=union(read_asset_manifest(original_bytes), read_asset_manifest(current_bytes), lambda a: (a['path'], a['address'])),
                apk_bundles=len(original), current_bundles=len(current), retained_apk_only=len(selected)-len(current))
            for record in selected:
                entry = f'assets/yoo/{package}/{record["file"]}'
                if entry in available:
                    raw = archive.read(entry)
                    provenance = dict(origin='apk_zip', apk_entry=entry, apk_sha256=source.apk_sha256)
                else:
                    if record['source_manifest'] != source.version:
                        unavailable.append(dict(record, reason='Listed in APK manifest, bytes absent from APK and selected downloaded cloud version'))
                        continue
                    cached = cloud.read(package, record)
                    if cached is None:
                        raise FileNotFoundError('Current bundle is not cached: ' + record['name'])
                    raw, provenance = cached
                if len(raw) != record['size'] or hashlib.md5(raw).hexdigest() != record['hash']:
                    raise ValueError('Bundle size/MD5 mismatch: ' + record['name'])
                decoded = Apk.decode(raw)
                # Equal hashes can occur in different packages. Names are kept
                # separate to avoid silently replacing a different identity.
                relative = Path('inputs') / package / record['name']
                target = destination / relative
                target.parent.mkdir(exist_ok=True)
                if not target.exists() or sha(target.read_bytes()) != sha(decoded):
                    temporary = target.with_suffix('.tmp')
                    temporary.write_bytes(decoded)
                    temporary.replace(target)
                bundles.append(dict(record, source=provenance, raw_sha256=sha(raw),
                                    decoded_sha256=sha(decoded), output=relative.as_posix(),
                                    format='UnityFS' if decoded.startswith(b'UnityFS') else 'original'))
            print(package, len(selected), 'retained APK-only', len(selected)-len(current), flush=True)
        # The built-in serialized files supply engine and shader dependencies.
        for entry in ('assets/bin/Data/data.unity3d', 'assets/bin/Data/unity default resources'):
            raw = archive.read(entry)
            target = inputs / Path(entry).name
            target.write_bytes(raw)
            bundles.append(dict(output=target.relative_to(destination).as_posix(),
                                source=dict(origin='apk_zip', apk_entry=entry),
                                raw_sha256=sha(raw), decoded_sha256=sha(raw), format='builtin'))
    result = dict(format=1, manifest=source.version, apk_manifest=source.apk_version,
                  apk_sha256=source.apk_sha256, generator_sha256=sha(Path(__file__).read_bytes()),
                  operation=__doc__, inputs=bundles, catalogs=catalogs, unavailable_original_bundles=unavailable)
    (destination / 'source-manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', 'utf8')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--destination', type=Path, required=True)
    print('Verified converter inputs:', len(prepare(parser.parse_args().destination)['inputs']))
