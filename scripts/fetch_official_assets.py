"""Cache the public official APK and extract named Unity asset bundles.

Python standard library only for download; UnityPy is an offline extraction tool.
The APK and intermediate bundles stay in ignored .tools/, outside Windows exports.
"""
import argparse
import concurrent.futures
import hashlib
import json
from pathlib import Path
import re
import time
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / '.tools/source-packages'
_SELECTION = json.loads((ROOT / 'official-source.json').read_text(encoding='utf-8'))
_APK_SOURCE = _SELECTION['profiles'][_SELECTION['active']]['apk']
URL = _APK_SOURCE['url']
SIZE = _APK_SOURCE['size']
SHA256 = _APK_SOURCE['sha256']
APK = ROOT / _APK_SOURCE['path']

def download():
    CACHE.mkdir(parents=True, exist_ok=True)
    if APK.exists() and APK.stat().st_size == SIZE:
        with APK.open('rb') as cached:
            if hashlib.file_digest(cached, 'sha256').hexdigest() != SHA256:
                raise ValueError('Cached APK checksum mismatch; preserve and inspect the file')
        print('Using cached APK', APK, flush=True)
        return
    # The public filename is reused for new releases. Verify the selected size
    # before reusing ranges or downloading.
    probe = urllib.request.Request(URL, headers={
        'User-Agent': 'Mozilla/5.0', 'Referer': 'https://zjcs.leiting.com/',
        'Range': 'bytes=0-0', 'Accept-Encoding': 'identity'})
    with urllib.request.urlopen(probe, timeout=90) as response:
        if response.status != 206 or response.headers.get('Content-Range') != f'bytes 0-0/{SIZE}':
            raise ValueError('Official APK URL no longer serves the selected size; '
                             'run scripts/fetch_latest_official_sources.py to refresh the source. '
                             'Content-Range: ' + response.headers.get('Content-Range', '<missing>'))
        if len(response.read(2)) != 1:
            raise ValueError('Unexpected APK size probe response')
    chunk_size = 16 * 1024 * 1024
    def chunk(index):
        start = index * chunk_size
        end = min(SIZE, start + chunk_size) - 1
        path = CACHE / f'zjcs.part-{index:03d}'
        if path.exists() and path.stat().st_size == end - start + 1:
            return path
        for attempt in range(5):
            try:
                req = urllib.request.Request(URL, headers={
                    'User-Agent': 'Mozilla/5.0', 'Referer': 'https://zjcs.leiting.com/',
                    'Range': f'bytes={start}-{end}', 'Accept-Encoding': 'identity'})
                with urllib.request.urlopen(req, timeout=90) as response:
                    if response.status != 206 or response.headers.get('Content-Range') != f'bytes {start}-{end}/{SIZE}':
                        raise ValueError('Unexpected range response')
                    value = response.read(end - start + 2)
                if len(value) != end - start + 1:
                    raise ValueError('Incomplete range')
                path.write_bytes(value)
                print(f'Cached range {index + 1}/{(SIZE + chunk_size - 1) // chunk_size}', flush=True)
                return path
            except Exception:
                if attempt == 4:
                    raise
                time.sleep(2)
    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
        parts = list(pool.map(chunk, range((SIZE + chunk_size - 1) // chunk_size)))
    temporary = APK.with_suffix('.apk.tmp')
    digest = hashlib.sha256()
    with temporary.open('wb') as out:
        for part in parts:
            value = part.read_bytes()
            out.write(value)
            digest.update(value)
    with zipfile.ZipFile(temporary) as archive:
        if archive.testzip() is not None:
            raise ValueError('APK ZIP CRC verification failed')
    if digest.hexdigest() != SHA256:
        raise ValueError('Official APK checksum changed; inspect the source before repinning')
    temporary.replace(APK)
    for part in parts:
        part.unlink()
    metadata = dict(_APK_SOURCE, zip_crc_verified=True)
    (CACHE / 'source.json').write_text(json.dumps(metadata, indent=2), encoding='utf-8')
    print(json.dumps(metadata), flush=True)

def extract(pattern, package='DefaultPackage', manifest=None, quiet=False):
    from official_source import OfficialSource
    from official_cloud_cache import CloudCache

    selected = OfficialSource(manifest)
    extraction_root = CACHE if selected.version == OfficialSource().version else CACHE / 'history' / selected.version
    destination = extraction_root / ('extracted' if package == 'DefaultPackage' else package)
    destination.mkdir(exist_ok=True)
    cloud = CloudCache(selected.cloud_path, selected.version)
    with zipfile.ZipFile(APK) as archive:
        manifest = selected.manifest(package, archive)
        mapping = {bundle['name']: bundle['hash'] for bundle in manifest['bundles']}
        (destination / 'bundle-index.json').write_text(json.dumps(mapping, indent=2), encoding='utf-8')
        available = set(archive.namelist())
        source_path = destination / 'bundle-sources.json'
        sources = json.loads(source_path.read_text(encoding='utf-8')) if source_path.exists() else {}
        for bundle in manifest['bundles']:
            name = bundle['name']
            if not re.search(pattern, name):
                continue
            zipped = f'assets/yoo/{package}/{bundle["file"]}'
            if zipped not in available:
                cached = cloud.read(package, bundle)
                if cached is None:
                    print('Not packaged or cached:', name)
                    continue
                payload, source = cached
                if not quiet:print('Official CDN cache:', name, len(payload))
            else:
                payload = archive.read(zipped)
                source = {'origin': 'apk_zip', 'apk_sha256': SHA256, 'path': zipped}
                if not quiet:print(name, len(payload))
            if len(payload) != bundle['size'] or hashlib.md5(payload).hexdigest() != bundle['hash']:
                raise ValueError('Selected source failed size/MD5 verification: ' + name)
            # Replace the directory entry: version archives may share hard links.
            temporary = destination / (name + '.part')
            temporary.write_bytes(payload)
            temporary.replace(destination / name)
            source.update(manifest=selected.version, apk_manifest=selected.apk_version)
            source.update(bundle_size=len(payload), bundle_md5=bundle['hash'],
                          bundle_sha256=hashlib.sha256(payload).hexdigest())
            sources[name] = dict(source, bundle_hash=bundle['hash'])
        source_path.write_text(json.dumps(sources, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--extract', help='Regex matching source bundle names (optional)')
    parser.add_argument('--package', choices=['DefaultPackage', 'Launch', 'HybridCLR', 'Boot', 'WwiseBnk'], default='DefaultPackage')
    parser.add_argument('--manifest', help='Source profile; defaults to official-source.json active')
    args = parser.parse_args()
    download()
    if args.extract:
        extract(args.extract, args.package, args.manifest)
