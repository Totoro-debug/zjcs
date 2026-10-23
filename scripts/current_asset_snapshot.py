"""Create, verify and restore the current source and Unity payload snapshot."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import zipfile

import asset_pack


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
        result = asset_pack._create(root, root / '.tools/asset-packs')
    else:
        if args.archive is None:
            parser.error('--archive is required for verify and restore')
        result = (verify(root, args.archive) if args.command == 'verify'
                  else asset_pack._restore(root, args.archive, args.replace))
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return int(bool(result.get('errors')))


if __name__ == '__main__':
    raise SystemExit(main())
