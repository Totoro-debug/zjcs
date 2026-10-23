"""Seal an unactivated ZIP only after verifying every entry against current assets."""
from __future__ import annotations

import argparse
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
from pathlib import Path
import zipfile

import asset_pack


def refresh_source_inventories(root, candidate, paths):
    """Update source receipts in an unregistered candidate, then verify all bytes."""
    from current_conversion_state import activated_receipt
    from official_source import OfficialSource

    version = OfficialSource().version
    for path in (root / 'assets/reference').rglob('current-*-source-manifest.json'):
        if json.loads(path.read_bytes()).get('manifest') == version:
            activated_receipt(path, version)
    updates = {}
    with zipfile.ZipFile(candidate) as archive:
        for info in archive.infolist():
            name = info.filename
            if name in paths and name.endswith('source-manifest.json'):
                raw = paths[name].read_bytes()
                if archive.read(info) != raw:
                    updates[name] = raw
    if updates:
        with zipfile.ZipFile(candidate, 'a') as archive:
            # Replace the central-directory entries, so extraction sees one
            # entry per path. Original compressed data stays available as bytes.
            replaced = set(updates) | {asset_pack.INVENTORY}
            archive.filelist = [info for info in archive.filelist if info.filename not in replaced]
            archive.NameToInfo = {info.filename: info for info in archive.filelist}
            for name, raw in updates.items():
                info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                info._compresslevel = 1
                archive.writestr(info, raw)
    return sorted(updates)


def finalize(root, candidate, refresh_sources=False):
    candidate = candidate.resolve()
    output_dir = (root / '.tools/asset-packs').resolve()
    if candidate.parent != output_dir:
        raise ValueError('The unactivated candidate must be in the local asset pack directory')
    registered = root / 'asset-pack/lock.json'
    if registered.exists():
        active = output_dir / json.loads(registered.read_bytes())['archive_file']
        if active.exists() and candidate.samefile(active):
            raise ValueError('A registered snapshot cannot be modified as a candidate')
    layout = asset_pack.layout_at(root)
    paths = asset_pack.payload_files(root, layout)
    refreshed = refresh_source_inventories(root, candidate, paths) if refresh_sources else []
    before = {name: (path.stat().st_size, path.stat().st_mtime_ns) for name, path in paths.items()}
    records = {}
    previous_manifest = None
    with zipfile.ZipFile(candidate) as archive:
        infos = archive.infolist()
        names = [info.filename for info in infos]
        if len(names) != len(set(names)):
            raise ValueError('Candidate contains duplicate entries')
        if asset_pack.INVENTORY in names:
            previous_manifest = archive.read(asset_pack.INVENTORY)
            infos = [info for info in infos if info.filename != asset_pack.INVENTORY]
        if {info.filename for info in infos} != set(paths):
            raise ValueError('Candidate entries differ from the complete current payload file set')
        if any(info.is_dir() or info.flag_bits & 1 for info in infos):
            raise ValueError('Candidate contains a directory or encrypted entry')

        def verify_one(info):
            digest = hashlib.sha256()
            size = 0
            with archive.open(info) as source:
                while chunk := source.read(asset_pack.CHUNK):
                    digest.update(chunk)
                    size += len(chunk)
            value = {'bytes': size, 'sha256': digest.hexdigest()}
            path = paths[info.filename]
            if size != path.stat().st_size or asset_pack.digest(path) != value['sha256']:
                raise ValueError('Archived bytes differ from current payload: ' + info.filename)
            return info.filename, value

        with ThreadPoolExecutor(max_workers=4) as executor:
            for start in range(0, len(infos), 64):
                for name, value in executor.map(verify_one, infos[start:start + 64]):
                    records[name] = value
                    if len(records) % 10000 == 0:
                        print(f'Verified {len(records)}/{len(infos)} archive and local payloads', flush=True)
    current = asset_pack.payload_files(root, layout)
    if set(current) != set(paths):
        raise ValueError('Payload file set changed during verification')
    for name, path in current.items():
        stat = path.stat()
        if (stat.st_size, stat.st_mtime_ns) != before[name]:
            if stat.st_size != records[name]['bytes'] or asset_pack.digest(path) != records[name]['sha256']:
                raise ValueError('Payload changed during verification: ' + name)

    manifest = asset_pack.encode({'format': 1, 'files': records})
    if previous_manifest is not None:
        if previous_manifest != manifest:
            raise ValueError('The candidate inventory differs from the verified payload records')
    else:
        with zipfile.ZipFile(candidate, 'a') as archive:
            info = zipfile.ZipInfo(asset_pack.INVENTORY, date_time=(1980, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info._compresslevel = 1
            archive.writestr(info, manifest)
    archive_hash = asset_pack.digest(candidate)

    # Fingerprint the code that exists at finalization, and retain its exact bytes.
    # Code may have advanced while compression ran; no asset mismatch is accepted.
    code_files = {name for name in layout['code_files'] if not name.startswith('unity/Assets/Game/')}
    code_files.add('scripts/finalize_current_asset_snapshot.py')
    for suffix in ('.cs', '.shader', '.asmdef', '.asmref', '.unity'):
        code_files.update(path.relative_to(root).as_posix()
                          for path in (root / 'unity/Assets/Game').rglob('*' + suffix))
    layout['code_files'] = sorted(code_files)
    asset_pack.atomic_write(root / 'asset-pack/layout.json', asset_pack.encode(layout))
    cache = root / '.tools/source-packages/history/converted'
    cache.mkdir(parents=True, exist_ok=True)
    code = {}
    code_sources = {}
    for name in layout['code_files']:
        raw = (root / name).read_bytes()
        digest = hashlib.sha256(raw).hexdigest()
        preserved = cache / (digest + '.bin')
        if preserved.exists():
            if asset_pack.digest(preserved) != digest:
                raise ValueError('Preserved code cache differs from its SHA256')
        else:
            preserved.write_bytes(raw)
        code[name] = digest
        code_sources[name] = preserved.relative_to(root).as_posix()
    if asset_pack.code_hashes(root, layout) != code:
        raise ValueError('Code changed while capturing its exact source bytes')
    filename = 'zjcs-assets-' + layout['source_manifests'][0] + '-' + archive_hash[:16] + '.zip'
    destination = output_dir / filename
    if destination.exists():
        raise ValueError('A finalized archive already uses this name')
    lock = {'format': 1, 'archive_file': filename, 'archive_sha256': archive_hash,
            'archive_bytes': candidate.stat().st_size, 'inventory_sha256': hashlib.sha256(manifest).hexdigest(),
            'files': len(records), 'uncompressed_bytes': sum(value['bytes'] for value in records.values()),
            'source_manifests': layout['source_manifests'],
            'layout_sha256': asset_pack.digest(root / 'asset-pack/layout.json'), 'code_sha256': code,
            'preserved_code_sources': code_sources}
    candidate.replace(destination)
    asset_pack.atomic_write(output_dir / (filename + '.manifest.json'), manifest)
    asset_pack.atomic_write(root / 'asset-pack/lock.json', asset_pack.encode(lock))
    return {'archive': str(destination), 'refreshed_source_inventories': refreshed,
            'archive_payloads_verified': len(records),
            'local_payloads_verified': len(records), **lock}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path, required=True)
    parser.add_argument('--refresh-source-inventories', action='store_true')
    args = parser.parse_args()
    print(json.dumps(finalize(Path(__file__).resolve().parents[1], args.archive,
                              args.refresh_source_inventories), ensure_ascii=False, indent=2))
