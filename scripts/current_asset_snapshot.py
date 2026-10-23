"""Create, verify and restore the current source and Unity payload snapshot."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import tempfile
import zipfile

import asset_pack


def create(root, output_dir):
    """Accept timestamp-only regeneration after checking the actual saved bytes."""
    layout = asset_pack.layout_at(root)
    layout_hash = asset_pack.digest(root / 'asset-pack/layout.json')
    paths = asset_pack.payload_files(root, layout)
    if not paths:
        raise ValueError('No asset payload found')
    output_dir = output_dir.resolve()
    if any(output_dir.is_relative_to((root / folder).resolve()) for folder in layout['payload_roots']):
        raise ValueError('Pack output must be outside the payload directories')
    output_dir.mkdir(parents=True, exist_ok=True)
    code = asset_pack.code_hashes(root, layout)
    snapshot = {name: (path.stat().st_size, path.stat().st_mtime_ns) for name, path in paths.items()}
    handle, name = tempfile.mkstemp(dir=output_dir, suffix='.zip.tmp')
    os.close(handle)
    temporary = Path(name)
    records = {}
    changed = []
    try:
        with temporary.open('w+b', buffering=8 * asset_pack.CHUNK) as output, zipfile.ZipFile(
                output, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=1) as archive:
            for index, (name, path) in enumerate(paths.items(), 1):
                info = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
                info.compress_type = (zipfile.ZIP_STORED if path.suffix in {'.png', '.ogg', '.wav', '.ttf'}
                                      else zipfile.ZIP_DEFLATED)
                info._compresslevel = 1
                digest = hashlib.sha256()
                size = 0
                with path.open('rb') as source, archive.open(info, 'w', force_zip64=True) as target:
                    while block := source.read(asset_pack.CHUNK):
                        digest.update(block)
                        size += len(block)
                        target.write(block)
                records[name] = {'bytes': size, 'sha256': digest.hexdigest()}
                if index % 10000 == 0:
                    print(f'Packed {index}/{len(paths)} files', flush=True)
            current = asset_pack.payload_files(root, layout)
            if (set(current) != set(paths) or asset_pack.code_hashes(root, layout) != code
                    or asset_pack.digest(root / 'asset-pack/layout.json') != layout_hash):
                raise ValueError('The payload file set, layout or code changed while packing')
            for name, path in current.items():
                stat = path.stat()
                if (stat.st_size, stat.st_mtime_ns) == snapshot[name]:
                    continue
                changed.append(name)
                if stat.st_size != records[name]['bytes'] or asset_pack.digest(path) != records[name]['sha256']:
                    raise ValueError('Payload bytes changed while packing: ' + name)
            manifest = asset_pack.encode({'format': 1, 'files': records})
            info = zipfile.ZipInfo(asset_pack.INVENTORY, date_time=(1980, 1, 1, 0, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info._compresslevel = 1
            archive.writestr(info, manifest)
        archive_hash = asset_pack.digest(temporary)
        filename = 'zjcs-assets-' + layout['source_manifests'][0] + '-' + archive_hash[:16] + '.zip'
        lock = {'format': 1, 'archive_file': filename, 'archive_sha256': archive_hash,
                'archive_bytes': temporary.stat().st_size, 'inventory_sha256': hashlib.sha256(manifest).hexdigest(),
                'files': len(records), 'uncompressed_bytes': sum(row['bytes'] for row in records.values()),
                'source_manifests': layout['source_manifests'], 'layout_sha256': layout_hash, 'code_sha256': code}
        destination = output_dir / filename
        if destination.exists() and asset_pack.digest(destination) != archive_hash:
            raise ValueError('A different archive already uses this filename')
        temporary.replace(destination)
        asset_pack.atomic_write(output_dir / (filename + '.manifest.json'), manifest)
        asset_pack.atomic_write(root / 'asset-pack/lock.json', asset_pack.encode(lock))
        return {'archive': str(destination), 'identical_regenerated_files_verified': changed, **lock}
    finally:
        temporary.unlink(missing_ok=True)


def verify(root, archive):
    result = asset_pack.verify(root, archive)
    if result['errors']:
        return result
    _, _, records, _ = asset_pack.locked_manifest(root, archive)
    with zipfile.ZipFile(archive) as source:
        for index, (name, expected) in enumerate(records.items(), 1):
            digest = hashlib.sha256()
            size = 0
            with source.open(name) as stream:
                while chunk := stream.read(asset_pack.CHUNK):
                    digest.update(chunk)
                    size += len(chunk)
            if size != expected['bytes'] or digest.hexdigest() != expected['sha256']:
                raise ValueError('Snapshot entry differs from its inventory: ' + name)
            if index % 10000 == 0:
                print(f'Verified {index}/{len(records)} archived payloads', flush=True)
    result['archive_payloads_verified'] = len(records)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=('create', 'verify', 'restore'))
    parser.add_argument('--archive', type=Path)
    parser.add_argument('--replace', action='store_true')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    if args.command == 'create':
        # The writer checks the complete file set, timestamps and binding hashes
        # before activating the new lock. Other sessions keep their own files.
        result = create(root, root / '.tools/asset-packs')
    else:
        if args.archive is None:
            parser.error('--archive is required for verify and restore')
        result = (verify(root, args.archive) if args.command == 'verify'
                  else asset_pack._restore(root, args.archive, args.replace))
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return int(bool(result.get('errors')))


if __name__ == '__main__':
    raise SystemExit(main())
