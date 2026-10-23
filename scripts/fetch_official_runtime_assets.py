"""Cache the live client's five package manifests and every required remote file.

The bootstrap endpoint was recovered from the official APK running through ADB.
This cache follows the selected live resource version, independently of the
pinned 854_156383 extraction cache. Equal hashes can reuse verified old files.
"""
import argparse
import concurrent.futures
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import urllib.parse
import urllib.request
import zipfile

import fetch_official_cloud_assets as cloud
from fetch_official_assets import APK, CACHE, SHA256, SIZE, download
from official_manifest import read_manifest

PACKAGES = ('Boot', 'Launch', 'HybridCLR', 'DefaultPackage', 'WwiseBnk')


def read_url(url, limit=32 * 1024 * 1024):
    with urllib.request.urlopen(url, timeout=60) as response:
        data = response.read(limit + 1)
    if len(data) > limit:
        raise ValueError('Official response exceeds expected limit: ' + url)
    return data


def fingerprints(path):
    md5, sha256 = hashlib.md5(), hashlib.sha256()
    with path.open('rb') as stream:
        while data := stream.read(1024 * 1024):
            md5.update(data)
            sha256.update(data)
    return md5.hexdigest(), sha256.hexdigest()


def receipts(path):
    result = {}
    if path.exists():
        for line in path.read_text(encoding='utf-8').splitlines():
            record = json.loads(line)
            result[record['file']] = record
    return result


def configuration(expected):
    raw = read_url(cloud.CONFIG_URL, 2 * 1024 * 1024)
    parsed = json.loads(raw)
    selected = parsed.get('156383', parsed.get('default'))
    if not isinstance(selected, dict):
        raise ValueError('No server configuration for the pinned APK build')
    version = str(selected['ResVersion'])
    if not re.fullmatch(r'[0-9]+', version) or (expected and version != expected):
        raise ValueError('Selected resource version differs from expected version: ' + version)
    roots = []
    for field in ('CdnUrl', 'CdnFallbackUrl'):
        if not selected.get(field):
            continue
        base = selected[field].rstrip('/')
        parts = urllib.parse.urlsplit(base)
        if (parts.scheme != 'https' or parts.hostname not in
                {'zhangjcsdl.leiting.com', 'zhangjcsdl.roguelike.com'} or
                parts.path != '/patch' or parts.query or parts.fragment):
            raise ValueError('Inspect changed official CDN configuration before use')
        roots.append(base + '/' + version + '/Android')
    if not roots:
        raise ValueError('No official CDN roots')
    return raw, version, roots


def package_manifest(package, roots, destination):
    errors = []
    for root in roots:
        base = root + '/' + package + '/PackageManifest_' + package
        try:
            version_bytes = read_url(base + '.version', 256)
            version = version_bytes.decode('ascii').strip()
            if not re.fullmatch(r'[0-9]+_[0-9]+', version):
                raise ValueError('Invalid official package version')
            prefix = base + '_' + version + '.v3'
            expected = read_url(prefix + '.hash', 256).decode('ascii').strip()
            data = read_url(prefix + '.bytes')
            if not re.fullmatch(r'[a-f0-9]{32}', expected) or hashlib.md5(data).hexdigest() != expected:
                raise ValueError('Official manifest hash mismatch: ' + package)
            manifest = read_manifest(data)
            if manifest['package'] != package or manifest['version'] != version:
                raise ValueError('Manifest does not match official version endpoint')
            folder = destination / 'manifests' / package
            folder.mkdir(parents=True, exist_ok=True)
            (folder / ('PackageManifest_' + package + '.version')).write_bytes(version_bytes)
            (folder / ('PackageManifest_' + package + '_' + version + '.v3.bytes')).write_bytes(data)
            (folder / ('PackageManifest_' + package + '_' + version + '.v3.hash')).write_text(expected, encoding='ascii')
            return manifest, {'package': package, 'version': version, 'url': prefix + '.bytes',
                              'md5': expected, 'sha256': hashlib.sha256(data).hexdigest(),
                              'size': len(data)}
        except Exception as error:
            errors.append(str(error))
    raise RuntimeError('Manifest download failed: ' + '; '.join(errors))


def cached_package_manifest(package, destination, source):
    folder = destination / 'manifests' / package
    version = (folder / ('PackageManifest_' + package + '.version')).read_text(encoding='ascii').strip()
    if not re.fullmatch(r'[0-9]+_[0-9]+', version) or version != source['version']:
        raise ValueError('Cached package version differs from recorded provenance: ' + package)
    prefix = folder / ('PackageManifest_' + package + '_' + version + '.v3')
    expected = prefix.with_suffix('.v3.hash').read_text(encoding='ascii').strip()
    data = prefix.with_suffix('.v3.bytes').read_bytes()
    if (len(data) != source['size'] or expected != source['md5'] or
            hashlib.md5(data).hexdigest() != expected or
            hashlib.sha256(data).hexdigest() != source['sha256']):
        raise ValueError('Cached manifest failed recorded MD5/SHA256/size verification: ' + package)
    manifest = read_manifest(data)
    if manifest['package'] != package or manifest['version'] != version:
        raise ValueError('Cached manifest package/version mismatch: ' + package)
    return manifest, source


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument('--expected-resource-version', help='Require the ResVersion observed in the client')
    mode.add_argument('--verify-cache', metavar='RESOURCE_VERSION', help='Verify a saved version without network requests or downloads')
    parser.add_argument('--workers', type=int, choices=range(1, 17), default=12, metavar='1..16')
    args = parser.parse_args()
    if args.verify_cache and (not APK.exists() or APK.stat().st_size != SIZE):
        raise ValueError('Offline verification requires the complete pinned APK')
    download()
    if args.verify_cache:
        resource_version = args.verify_cache
        if not re.fullmatch(r'[0-9]+', resource_version):
            raise ValueError('Invalid cached resource version')
        destination = CACHE / 'cloud/runtime' / resource_version
        recorded = json.loads((destination / 'inventory.json').read_text(encoding='utf-8'))
        raw = (destination / 'server-config.json').read_bytes()
        if (recorded['resource_version'] != resource_version or recorded['apk_sha256'] != SHA256 or
                hashlib.sha256(raw).hexdigest() != recorded['configuration_sha256']):
            raise ValueError('Cached configuration differs from recorded provenance')
        roots = recorded['roots']
    else:
        raw, resource_version, roots = configuration(args.expected_resource_version)
    destination = CACHE / 'cloud/runtime' / resource_version
    destination.mkdir(parents=True, exist_ok=True)
    (destination / 'server-config.json').write_bytes(raw)
    manifests, provenance = [], []
    for package in PACKAGES:
        if args.verify_cache:
            source = next(m for m in recorded['manifests'] if m['package'] == package)
            manifest, source = cached_package_manifest(package, destination, source)
        else:
            manifest, source = package_manifest(package, roots, destination)
        manifests.append(manifest)
        provenance.append(source)
        print(f'Manifest verified: {package}={source["version"]}', flush=True)
    bundles = []
    with zipfile.ZipFile(APK) as archive:
        apk_files = {item.filename: item.file_size for item in archive.infolist()}
        for manifest in manifests:
            for bundle in manifest['bundles']:
                entry = 'assets/yoo/' + manifest['package'] + '/' + bundle['file']
                packaged = entry in apk_files
                if packaged:
                    if apk_files[entry] != bundle['size']:
                        raise ValueError('APK entry size differs from current manifest: ' + entry)
                    with archive.open(entry) as stream:
                        md5 = hashlib.file_digest(stream, 'md5').hexdigest()
                    if md5 != bundle['hash']:
                        raise ValueError('APK entry MD5 differs from current manifest: ' + entry)
                bundles.append(dict(bundle, package=manifest['package'], manifest=manifest['version'], packaged=packaged))
    inventory = {'apk_sha256': SHA256, 'resource_version': resource_version,
                 'configuration_url': cloud.CONFIG_URL, 'configuration_sha256': hashlib.sha256(raw).hexdigest(),
                 'roots': roots, 'manifests': provenance, 'bundles': bundles}
    (destination / 'inventory.json').write_text(json.dumps(inventory, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    required = {(b['package'], b['file']): b for b in bundles if not b['packaged']}
    receipt_path = destination / 'receipts.jsonl'
    existing = receipts(receipt_path)
    old_root = CACHE / 'cloud/854_156383'
    old_receipts = receipts(old_root / 'receipts.jsonl')
    cloud.DESTINATION = destination

    def acquire(bundle):
        key = bundle['package'] + '/' + bundle['file']
        target = destination / key
        if target.exists() and key in existing:
            md5, sha256 = fingerprints(target)
            record = existing[key]
            if (target.stat().st_size != bundle['size'] or md5 != bundle['hash'] or
                    record.get('md5') != md5 or record.get('sha256') != sha256 or
                    record.get('size') != bundle['size'] or not record.get('url')):
                return {'status': 'failed', 'file': key, 'error': 'Existing runtime cache failed integrity verification; preserved'}
            return dict(record, status='cached')
        if args.verify_cache:
            return {'status': 'failed', 'file': key, 'error': 'Runtime cache file or provenance receipt is missing'}
        old = old_root / key
        old_receipt = old_receipts.get(key)
        if not target.exists() and old.exists() and old_receipt and old_receipt.get('url'):
            md5, sha256 = fingerprints(old)
            if (old.stat().st_size == bundle['size'] and md5 == bundle['hash'] and
                    old_receipt.get('md5') == md5 and old_receipt.get('sha256') == sha256 and
                    old_receipt.get('size') == bundle['size']):
                target.parent.mkdir(parents=True, exist_ok=True)
                try:
                    os.link(old, target)
                except OSError:
                    shutil.copyfile(old, target)
                return dict(old_receipt, status='reused', name=bundle['name'], manifest=bundle['manifest'],
                            reused_from=str(old.relative_to(CACHE)), source_manifest='854_156383')
        result = cloud.fetch_bundle(bundle, roots, existing)
        return dict(result, manifest=bundle['manifest'])

    print(f'Resource version {resource_version}; packages: ' + ', '.join(p['package'] + '=' + p['version'] for p in provenance), flush=True)
    print(f'APK entries reused: {sum(b["packaged"] for b in bundles)}; remote files: {len(required)}; bytes: {sum(b["size"] for b in required.values())}', flush=True)
    results = []
    with receipt_path.open('a', encoding='utf-8') as output:
        with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
            jobs = {pool.submit(acquire, b): b for b in required.values()}
            for future in concurrent.futures.as_completed(jobs):
                try:
                    result = future.result()
                except Exception as error:
                    b = jobs[future]
                    result = {'status': 'failed', 'file': b['package'] + '/' + b['file'], 'error': str(error)}
                results.append(result)
                if result['status'] in {'reused', 'downloaded'}:
                    output.write(json.dumps(result, ensure_ascii=False) + '\n')
                    output.flush()
                if len(results) % 500 == 0 or len(results) == len(required):
                    print(f'Checked {len(results)}/{len(required)}; failed {sum(r["status"] == "failed" for r in results)}; downloaded {sum(r["status"] == "downloaded" for r in results)}; reused {sum(r["status"] == "reused" for r in results)}; cached {sum(r["status"] == "cached" for r in results)}', flush=True)
    verified = [r for r in results if r['status'] != 'failed']
    report = {'checked_at_utc': datetime.now(timezone.utc).isoformat(), 'apk_sha256': SHA256,
              'mode': 'verify-cache' if args.verify_cache else 'live-download',
              'resource_version': resource_version, 'manifests': provenance,
              'configuration_url': cloud.CONFIG_URL, 'configuration_sha256': inventory['configuration_sha256'],
              'roots': roots, 'apk_entries': sum(b['packaged'] for b in bundles),
              'remote_files': len(required), 'remote_bytes': sum(b['size'] for b in required.values()),
              'verified_files': len(verified), 'verified_bytes': sum(r['size'] for r in verified),
              'downloaded_this_run': sum(r['status'] == 'downloaded' for r in results),
              'reused_this_run': sum(r['status'] == 'reused' for r in results),
              'cached_this_run': sum(r['status'] == 'cached' for r in results),
              'failures': [r for r in results if r['status'] == 'failed'],
              'receipts_sha256': hashlib.sha256(receipt_path.read_bytes()).hexdigest()}
    (destination / 'download-report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({k: v for k, v in report.items() if k not in {'manifests', 'failures'}}, ensure_ascii=False, indent=2), flush=True)
    print('Cache:', destination, flush=True)
    return 1 if report['failures'] else 0


if __name__ == '__main__':
    raise SystemExit(main())
