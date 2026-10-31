"""Download the current official APK and all matching live CDN resources."""
import concurrent.futures
import hashlib
import json
from pathlib import Path
import re
import shutil
import urllib.parse
import urllib.request
import zipfile

import fetch_official_cloud_assets as cloud
from fetch_official_runtime_assets import PACKAGES, package_manifest, read_url
from official_manifest import read_manifest

ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / '.tools/source-packages'
APK_URL = 'https://zhangjcsdlf.leiting.com/full/zjcs_M509085.apk'
HEADERS = {'User-Agent': 'Mozilla/5.0', 'Referer': 'https://zjcs.leiting.com/',
           'Accept-Encoding': 'identity'}


def write_json(path, value):
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    temporary.replace(path)


def acquire_bundle(bundle, roots, receipts):
    key = bundle['package'] + '/' + bundle['file']
    target = cloud.DESTINATION / key
    if bundle['size'] < 8 * 1024 * 1024 or target.exists():
        return cloud.fetch_bundle(bundle, roots, receipts)
    target.parent.mkdir(parents=True, exist_ok=True)
    errors = []
    for root in roots:
        url = root + '/' + key

        def read_range(start):
            end = min(bundle['size'], start + 2 * 1024 * 1024) - 1
            request = urllib.request.Request(url, headers={
                'Range': f'bytes={start}-{end}', 'Accept-Encoding': 'identity'})
            for attempt in range(3):
                try:
                    with urllib.request.urlopen(request, timeout=60) as response:
                        if response.status != 206 or response.headers.get('Content-Range') != f'bytes {start}-{end}/{bundle["size"]}':
                            raise ValueError('Resource range differs from the manifest size')
                        data = response.read(end - start + 2)
                    if len(data) != end - start + 1:
                        raise ValueError('Truncated resource range')
                    return data
                except Exception:
                    if attempt == 2:
                        raise

        try:
            md5, sha256 = hashlib.md5(), hashlib.sha256()
            temporary = target.with_suffix(target.suffix + '.part')
            with temporary.open('wb') as output:
                with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
                    for data in pool.map(read_range, range(0, bundle['size'], 2 * 1024 * 1024)):
                        md5.update(data)
                        sha256.update(data)
                        output.write(data)
            if temporary.stat().st_size != bundle['size'] or md5.hexdigest() != bundle['hash']:
                raise ValueError('Resource range assembly failed size/MD5 verification')
            temporary.replace(target)
            return dict(status='downloaded', file=key, name=bundle['name'], size=bundle['size'],
                        md5=md5.hexdigest(), sha256=sha256.hexdigest(), url=url,
                        downloaded_at_utc=cloud.utc_now(), transfer='parallel-ranges')
        except Exception as error:
            errors.append(str(error))
    return dict(status='failed', file=key, errors=errors)


def latest_apk():
    request = urllib.request.Request(APK_URL, headers=dict(HEADERS, Range='bytes=0-0'))
    with urllib.request.urlopen(request, timeout=60) as response:
        match = re.fullmatch(r'bytes 0-0/([0-9]+)', response.headers.get('Content-Range', ''))
        etag = response.headers.get('Etag', '').strip('"').lower()
        if response.status != 206 or not match or not re.fullmatch('[a-f0-9]{32}', etag):
            raise ValueError('Official APK identity response is invalid')
        size = int(match[1])
        headers = dict(response.headers)
        if response.read(2) != b'P':
            raise ValueError('Official APK does not have a ZIP header')
    target = CACHE / 'zjcs_M509085.apk'
    candidates = [target, CACHE / 'live' / etag / target.name]
    for candidate in candidates:
        if candidate.exists() and candidate.stat().st_size == size:
            with candidate.open('rb') as stream:
                if hashlib.file_digest(stream, 'md5').hexdigest() == etag:
                    if candidate != target:
                        shutil.copyfile(candidate, target)
                    break
    else:
        parts_dir = CACHE / 'apk-download' / etag
        parts_dir.mkdir(parents=True, exist_ok=True)
        step = 16 * 1024 * 1024

        def acquire(start):
            end = min(size, start + step) - 1
            request = urllib.request.Request(APK_URL, headers=dict(HEADERS, Range=f'bytes={start}-{end}'))
            for attempt in range(4):
                try:
                    with urllib.request.urlopen(request, timeout=90) as response:
                        if (response.status != 206 or response.headers.get('Content-Range') != f'bytes {start}-{end}/{size}' or
                                response.headers.get('Etag', '').strip('"').lower() != etag):
                            raise ValueError('Official APK changed during download')
                        data = response.read(end - start + 2)
                    if len(data) != end - start + 1:
                        raise ValueError('Truncated APK range')
                    part = parts_dir / str(start)
                    part.write_bytes(data)
                    return part
                except Exception:
                    if attempt == 3:
                        raise

        with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
            parts = list(pool.map(acquire, range(0, size, step)))
        temporary = target.with_suffix('.apk.part')
        with temporary.open('wb') as output:
            for part in parts:
                with part.open('rb') as stream:
                    shutil.copyfileobj(stream, output)
        with temporary.open('rb') as stream:
            if hashlib.file_digest(stream, 'md5').hexdigest() != etag:
                raise ValueError('APK MD5 differs from the official server')
        temporary.replace(target)
        for part in parts:
            part.unlink()
        parts_dir.rmdir()
    with target.open('rb') as stream:
        sha256 = hashlib.file_digest(stream, 'sha256').hexdigest()
    versions = set()
    with zipfile.ZipFile(target) as archive:
        if archive.testzip() is not None:
            raise ValueError('APK ZIP CRC verification failed')
        for package in PACKAGES:
            prefix = f'assets/yoo/{package}/PackageManifest_{package}_'
            entries = [n for n in archive.namelist() if n.startswith(prefix) and n.endswith('.v3.bytes')]
            if len(entries) != 1:
                raise ValueError('APK must contain one manifest per package: ' + package)
            manifest = read_manifest(archive.read(entries[0]))
            if manifest['package'] != package:
                raise ValueError('APK package identity mismatch')
            versions.add(manifest['version'])
    if len(versions) != 1:
        raise ValueError('APK package versions disagree')
    version = versions.pop()
    metadata = dict(path=target.relative_to(ROOT).as_posix(), url=APK_URL, size=size,
                    md5=etag, sha256=sha256, manifest=version,
                    build_version=int(version.split('_')[1]))
    write_json(CACHE / 'source.json', dict(metadata, http_headers=headers, zip_crc_verified=True))
    print('Current official APK verified:', version, sha256, flush=True)
    return metadata


def main():
    CACHE.mkdir(parents=True, exist_ok=True)
    apk = latest_apk()
    raw = read_url(cloud.CONFIG_URL, 2 * 1024 * 1024)
    config = json.loads(raw)
    selected = config.get(str(apk['build_version']), config.get('default'))
    if not isinstance(selected, dict):
        raise ValueError('Official configuration missing for APK build')
    version = str(selected['ResVersion'])
    if not re.fullmatch('[0-9]+', version):
        raise ValueError('Invalid live resource version')
    roots = []
    for key in ('CdnUrl', 'CdnFallbackUrl'):
        base = selected.get(key)
        if not base:
            continue
        parts = urllib.parse.urlsplit(base)
        if (parts.scheme != 'https' or parts.hostname not in
                ('zhangjcsdl.leiting.com', 'zhangjcsdl.roguelike.com') or parts.path != '/patch'):
            raise ValueError('Unexpected official CDN configuration')
        roots.append(base.rstrip('/') + '/' + version + '/Android')
    if not roots:
        raise ValueError('No official CDN root')
    destination = CACHE / 'cloud/runtime' / version
    destination.mkdir(parents=True, exist_ok=True)
    (destination / 'server-config.json').write_bytes(raw)
    manifests, bundles, versions = [], [], set()
    for package in PACKAGES:
        manifest, provenance = package_manifest(package, roots, destination)
        versions.add(manifest['version'])
        manifests.append(provenance)
        bundles.extend(dict(b, package=package, manifest=manifest['version'], packaged=False)
                       for b in manifest['bundles'])
        print('Live manifest verified:', package, manifest['version'], len(manifest['bundles']), flush=True)
    if len(versions) != 1:
        raise ValueError('Live package versions disagree; retry after publication finishes')
    active = versions.pop()
    inventory = dict(apk_sha256=apk['sha256'], resource_version=version,
                     configuration_url=cloud.CONFIG_URL, configuration_sha256=hashlib.sha256(raw).hexdigest(),
                     roots=roots, manifests=manifests, bundles=bundles, cache_scope='all-manifest-bundles')
    write_json(destination / 'inventory.json', inventory)
    receipt_path = destination / 'receipts.jsonl'
    receipts = {}
    if receipt_path.exists():
        for line in receipt_path.read_text(encoding='utf-8').splitlines():
            receipt = json.loads(line)
            receipts[receipt['file']] = receipt
    cloud.DESTINATION = destination
    failures = []
    print('Download live CDN:', version, 'files', len(bundles), 'bytes', sum(b['size'] for b in bundles), flush=True)
    with receipt_path.open('a', encoding='utf-8') as output:
        with concurrent.futures.ThreadPoolExecutor(max_workers=16) as pool:
            jobs = {pool.submit(acquire_bundle, b, roots, receipts): b for b in bundles}
            for i, future in enumerate(concurrent.futures.as_completed(jobs), 1):
                bundle = jobs[future]
                result = future.result()
                if result['status'] == 'failed':
                    failures.append(result)
                elif result['status'] != 'cached':
                    result['manifest'] = bundle['manifest']
                    output.write(json.dumps(result) + '\n')
                    output.flush()
                if i % 500 == 0 or i == len(bundles):
                    print('Downloaded/checked', i, '/', len(bundles), 'failures', len(failures), flush=True)
    if failures:
        raise RuntimeError(json.dumps(failures))
    profile = dict(apk=apk, resource_version=version,
                   cloud_cache=destination.relative_to(ROOT).as_posix(), cloud_cache_retained=True,
                   manifests=manifests)
    write_json(ROOT / 'official-source.json', dict(format=1, active=active, profiles={active: profile}))
    print('Selected latest official source:', active, version, flush=True)


if __name__ == '__main__':
    main()
