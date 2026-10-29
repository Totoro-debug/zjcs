"""Download files absent from the pinned APK, checking its original size and MD5.

The bootstrap URL was observed in this official APK's startup log. CDN paths are
case sensitive: Android/{package}/{hash}.{extension}. No newer manifest is mixed
into the APK's inventory. Downloads and their receipts stay in ignored .tools/.
"""
import argparse
import concurrent.futures
from datetime import datetime, timezone
import hashlib
import http.client
import json
from pathlib import Path
import re
import ssl
import threading
import time
import urllib.parse
import urllib.request

from fetch_official_assets import APK, CACHE, SHA256, download
from official_manifest import inventory_apk

CONFIG_URL = 'https://zhangjcsupdate.leiting.com/zhangjcs/version/release/110001_Main.dis'
DESTINATION = CACHE / 'cloud/854_156383'
LOCAL = threading.local()
TLS = ssl.create_default_context()


def utc_now():
    return datetime.now(timezone.utc).isoformat()


def load_config():
    with urllib.request.urlopen(CONFIG_URL, timeout=30) as response:
        raw = response.read(2 * 1024 * 1024)
    parsed = json.loads(raw)
    selected = parsed.get('156383', parsed.get('default'))
    if not isinstance(selected, dict):
        raise ValueError('No server configuration for this build')
    current = str(selected['ResVersion'])
    reserve = json.loads(selected.get('ReserveJson') or '{}')
    predownload = str(reserve.get('PredownloadConfig', {}).get('ResVersion', ''))
    # An older advertised predownload directory may retain hashes replaced later.
    historical = [str(value['ResVersion']) for value in parsed.values()
                  if isinstance(value, dict) and value.get('ResVersion')]
    versions = list(dict.fromkeys(v for v in [predownload, current, *historical] if v))
    roots = []
    for field in ['CdnUrl', 'CdnFallbackUrl']:
        base = selected.get(field)
        if not base:
            continue
        parts = urllib.parse.urlsplit(base)
        if parts.scheme != 'https' or parts.hostname not in {
                'zhangjcsdl.leiting.com', 'zhangjcsdl.roguelike.com'}:
            raise ValueError('Official CDN configuration changed; inspect it before downloading')
        for version in versions:
            if not re.fullmatch(r'[0-9]+', version):
                raise ValueError('Invalid CDN resource version')
            roots.append(base.rstrip('/') + '/' + version + '/Android')
    if not roots:
        raise ValueError('No official CDN roots')
    digest = hashlib.sha256(raw).hexdigest()
    DESTINATION.mkdir(parents=True, exist_ok=True)
    (DESTINATION / ('config-' + digest + '.json')).write_bytes(raw)
    return roots, {'url': CONFIG_URL, 'sha256': digest, 'checked_at_utc': utc_now(),
                   'current_resource_version': current, 'roots': roots}


def connection(host):
    if not hasattr(LOCAL, 'connections'):
        LOCAL.connections = {}
    if host not in LOCAL.connections:
        LOCAL.connections[host] = http.client.HTTPSConnection(host, timeout=60, context=TLS)
    return LOCAL.connections[host]


def discard_connection(host):
    conn = LOCAL.connections.pop(host, None)
    if conn:
        conn.close()


def fetch_bundle(bundle, roots, receipts):
    package, filename = bundle['package'], bundle['file']
    destination = DESTINATION / package / filename
    key = package + '/' + filename
    if destination.exists():
        with destination.open('rb') as stream:
            digest = hashlib.file_digest(stream, 'md5').hexdigest()
        if destination.stat().st_size != bundle['size'] or digest != bundle['hash']:
            return {'status': 'failed', 'file': key, 'error': 'Existing cache failed size/MD5 verification; preserved'}
        receipt = receipts.get(key)
        if receipt and receipt.get('url'):
            return dict(receipt, status='cached')
        # Recover provenance by fetching again if a previous run lost its receipt.
    destination.parent.mkdir(parents=True, exist_ok=True)
    errors = []
    temporary = destination.with_suffix(destination.suffix + '.part')
    for root in roots:
        url = root + '/' + key
        parts = urllib.parse.urlsplit(url)
        for attempt in range(3):
            try:
                conn = connection(parts.netloc)
                conn.request('GET', parts.path, headers={
                    'User-Agent': 'zjcs-official-asset-cache/1.0', 'Accept-Encoding': 'identity'})
                response = conn.getresponse()
                if response.status != 200:
                    response.read()
                    errors.append(f'{response.status}: {url}')
                    if response.status in (403, 404):
                        break
                    raise OSError(f'HTTP {response.status}')
                length = response.getheader('Content-Length')
                if length is not None and int(length) != bundle['size']:
                    discard_connection(parts.netloc)
                    errors.append(f'Content length mismatch: {url}')
                    break
                md5, sha256, total = hashlib.md5(), hashlib.sha256(), 0
                with temporary.open('wb') as output:
                    while data := response.read(min(1024 * 1024, bundle['size'] - total + 1)):
                        total += len(data)
                        if total > bundle['size']:
                            raise ValueError('Response exceeds the pinned manifest size')
                        output.write(data)
                        md5.update(data)
                        sha256.update(data)
                if total != bundle['size'] or md5.hexdigest() != bundle['hash']:
                    errors.append(f'Size/MD5 mismatch: {url}')
                    break
                temporary.replace(destination)
                return {'status': 'downloaded', 'file': key, 'name': bundle['name'],
                        'size': total, 'md5': md5.hexdigest(), 'sha256': sha256.hexdigest(),
                        'url': url, 'downloaded_at_utc': utc_now()}
            except Exception as error:
                discard_connection(parts.netloc)
                errors.append(f'{type(error).__name__}: {error}: {url}')
                if attempt < 2:
                    time.sleep(attempt + 1)
    return {'status': 'failed', 'file': key, 'name': bundle['name'], 'errors': errors}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--extract', help='Optional regex restricting which missing bundle names to cache')
    parser.add_argument('--workers', type=int, default=16, choices=range(1, 17), metavar='1..16')
    args = parser.parse_args()
    download()
    inventory = inventory_apk(APK)
    roots, configuration = load_config()
    missing = [b for b in inventory['bundles'] if not b['packaged']
               and (not args.extract or re.search(args.extract, b['name']))]
    # Start with terrain packages so the requested maps can be checked early.
    missing.sort(key=lambda b: (not ('mapground' in b['name'] or 'prefabs_map_map_' in b['name']), b['name']))
    unique = {(b['package'], b['file']): b for b in missing}
    receipt_path = DESTINATION / 'receipts.jsonl'
    receipts = {}
    if receipt_path.exists():
        for line in receipt_path.read_text(encoding='utf-8').splitlines():
            receipt = json.loads(line)
            receipts[receipt['file']] = receipt
    results = []
    print(f'Missing records: {len(missing)}, unique files: {len(unique)}, bytes: {sum(b["size"] for b in unique.values())}', flush=True)
    with receipt_path.open('a', encoding='utf-8') as receipt_file:
        with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
            futures = [pool.submit(fetch_bundle, b, roots, receipts) for b in unique.values()]
            for future in concurrent.futures.as_completed(futures):
                result = future.result()
                results.append(result)
                if result['status'] == 'downloaded':
                    receipt_file.write(json.dumps(result, ensure_ascii=False) + '\n')
                    receipt_file.flush()
                if len(results) % 200 == 0 or len(results) == len(unique):
                    good = [r for r in results if r['status'] != 'failed']
                    print(f'Checked {len(results)}/{len(unique)}, verified {len(good)}, failed {len(results)-len(good)}, bytes {sum(r["size"] for r in good)}', flush=True)
    verified = [r for r in results if r['status'] != 'failed']
    failures = [r for r in results if r['status'] == 'failed']
    report = {'checked_at_utc': utc_now(), 'apk_sha256': SHA256, 'manifest': '854_156383',
              'configuration': configuration, 'requested_missing_records': len(missing),
              'verified_files': len(verified), 'verified_bytes': sum(r['size'] for r in verified),
              'downloaded_this_run': sum(r['status'] == 'downloaded' for r in results),
              'failed_files': len(failures), 'failures': failures,
              'receipts_sha256': hashlib.sha256(receipt_path.read_bytes()).hexdigest()}
    report_path = DESTINATION / 'download-report.json'
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'Report: {report_path}', flush=True)
    return 1 if failures else 0


if __name__ == '__main__':
    raise SystemExit(main())
