"""Re-read every selected cloud resource and its official download receipt."""
import argparse
import concurrent.futures
import hashlib
import json
from official_source import OfficialSource
from official_bundle_directory import cab_names


def verify_cache(source):
    root = source.cloud_path
    receipts = {r['file']: r for r in map(json.loads, (root / 'receipts.jsonl').read_text().splitlines())}
    inventory = json.loads((root / 'inventory.json').read_text())
    bundles = [dict(b, package=p, manifest=source.version, packaged=False)
               for p in source.manifests for b in source.manifest(p)['bundles']]
    roots = inventory['roots']
    version = source.profile['resource_version']
    expected_roots = [f'https://{host}/patch/{version}/Android' for host in
                      ('zhangjcsdl.leiting.com', 'zhangjcsdl.roguelike.com')]
    if (not version or roots != expected_roots or
            inventory['resource_version'] != version or
            inventory['apk_sha256'] != source.apk_sha256 or
            hashlib.sha256((root / 'server-config.json').read_bytes()).hexdigest() !=
            inventory['configuration_sha256']):
        raise ValueError('Cloud inventory identity differs from the selected source')

    def verify(b):
        key = b['package'] + '/' + b['file']
        path = root / key
        receipt = receipts[key]
        md5, sha = hashlib.md5(), hashlib.sha256()
        with path.open('rb') as stream:
            while d := stream.read(1024 * 1024):
                md5.update(d)
                sha.update(d)
        if (path.stat().st_size != b['size'] or md5.hexdigest() != b['hash'] or
                receipt['size'] != b['size'] or receipt['md5'] != b['hash'] or
                receipt['sha256'] != sha.hexdigest() or
                receipt['manifest'] != source.version or
                receipt['url'] not in [r + '/' + key for r in roots]):
            raise ValueError('Resource/receipt mismatch: ' + key)
        cabs = []
        if b['file'].endswith('.bundle'):
            with path.open('rb') as stream:
                header = stream.read(16)
                stream.seek(0)
                if header[4:8] == b'ftyp' and b['package'] == 'WwiseBnk' and not b['encrypted']:
                    # Official video payloads retain the .bundle extension.
                    position = 0
                    while position < b['size']:
                        stream.seek(position)
                        box = stream.read(8)
                        if len(box) != 8:
                            raise ValueError('Truncated MP4 box: ' + key)
                        size = int.from_bytes(box[:4], 'big')
                        minimum = 8
                        if size == 1:
                            size = int.from_bytes(stream.read(8), 'big')
                            minimum = 16
                        elif size == 0:
                            size = b['size'] - position
                        if size < minimum or position + size > b['size']:
                            raise ValueError('Invalid MP4 box bounds: ' + key)
                        position += size
                else:
                    cabs = cab_names(stream)
                    if not cabs:
                        raise ValueError('No serialized CAB in UnityFS bundle: ' + key)
        return key, cabs

    cab_index = {}
    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
        for i, (key, names) in enumerate(pool.map(verify, bundles), 1):
            for name in names:
                cab_index.setdefault(name.lower(), []).append(key)
            if i % 2000 == 0 or i == len(bundles):
                print('Re-read and verified', i, '/', len(bundles), flush=True)
    inventory.update(bundles=bundles, cache_scope='all-manifest-bundles',
                     apk_available=source.apk.exists(), apk_sha256=source.apk_sha256)
    (root / 'inventory.json').write_text(json.dumps(inventory, indent=2) + '\n', encoding='utf-8')
    result = dict(manifest=source.version, resource_version=source.profile['resource_version'],
                  verified_files=len(bundles), verified_bytes=sum(b['size'] for b in bundles),
                  manifest_count=len(source.manifests), unique_cabs=len(cab_index),
                  checked=['manifest-pinned-SHA256-MD5-size', 'all-file-size-MD5',
                           'receipt-SHA256-version-exact-URL', 'UnityFS-CAB-directory-decode',
                           'WwiseBnk-MP4-box-bounds'],
                  receipts_sha256=hashlib.sha256((root / 'receipts.jsonl').read_bytes()).hexdigest(),
                  apk_available=source.apk.exists(), failures=[])
    (root / 'integrity.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    (root / 'verified-cab-files.json').write_text(json.dumps(cab_index, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result), flush=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", help="Explicit source profile; defaults to official-source.json active")
    args = parser.parse_args()
    verify_cache(OfficialSource(args.manifest))
