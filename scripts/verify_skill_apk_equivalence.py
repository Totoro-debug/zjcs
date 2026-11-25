"""Verify that a newer official APK contains the exact referenced skill inputs."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import urllib.request
import zipfile

import fetch_official_cloud_assets as cloud
from fetch_latest_official_sources import HEADERS
from official_source import OfficialSource, ROOT


def references(value, entries):
    if isinstance(value, dict):
        if 'apk_entry' in value:
            name, digest = value['apk_entry'], value.get('raw_sha256')
            if not digest or (name in entries and entries[name] != digest):
                raise ValueError('Missing or inconsistent original APK object digest: ' + name)
            entries[name] = digest
        for child in value.values():
            references(child, entries)
    elif isinstance(value, list):
        for child in value:
            references(child, entries)


def latest_identity(url):
    request = urllib.request.Request(url, headers=dict(HEADERS, Range='bytes=0-0'))
    with urllib.request.urlopen(request, timeout=60) as response:
        content_range = response.headers.get('Content-Range', '')
        identity = response.headers.get('ETag', '').strip('"').lower()
        if response.status != 206 or not re.fullmatch(r'bytes 0-0/\d+', content_range):
            raise ValueError('Official APK range identity is invalid')
        if not re.fullmatch(r'[0-9a-f]{32}', identity) or response.read(2) != b'P':
            raise ValueError('Official APK identity or ZIP header is invalid')
        return identity, int(content_range.split('/')[-1])


def verify(metadata_path, receipt_paths):
    metadata = json.loads(metadata_path.read_bytes())
    selected = OfficialSource()
    if not metadata.get('zip_crc_verified') or metadata['url'] != selected.profile['apk']['url']:
        raise ValueError('Latest APK was not fully verified against the official URL')
    apk = ROOT / metadata['path']
    if not apk.is_file() or apk.stat().st_size != metadata['size']:
        raise ValueError('Latest APK is absent or its size differs')
    if latest_identity(metadata['url']) != (metadata['md5'], metadata['size']):
        raise ValueError('The official APK changed; reacquire it before verification')
    with apk.open('rb') as stream:
        if hashlib.file_digest(stream, 'sha256').hexdigest() != metadata['sha256']:
            raise ValueError('Latest APK SHA256 differs from its verified download')
    with apk.open('rb') as stream:
        if hashlib.file_digest(stream, 'md5').hexdigest() != metadata['md5']:
            raise ValueError('Latest APK MD5 differs from the official identity')

    request = urllib.request.Request(cloud.CONFIG_URL, headers=HEADERS)
    with urllib.request.urlopen(request, timeout=60) as response:
        configuration = json.load(response)
    live = configuration.get(str(metadata['build_version']), configuration.get('default'))
    if not isinstance(live, dict) or str(live['ResVersion']) != selected.profile['resource_version']:
        raise ValueError('Official live resource selection differs from the selected source')
    for manifest in selected.profile['manifests']:
        with urllib.request.urlopen(manifest['url'], timeout=60) as response:
            data = response.read()
        if hashlib.sha256(data).hexdigest() != manifest['sha256']:
            raise ValueError('Official live manifest changed: ' + manifest['package'])

    entries, inputs = {}, []
    for path in receipt_paths:
        raw = path.read_bytes()
        document = json.loads(raw)
        if document.get('manifest') != selected.version:
            raise ValueError('Receipt source differs from the selected live manifest: ' + str(path))
        references(document, entries)
        inputs.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(raw).hexdigest()))
    if not entries:
        raise ValueError('No original APK objects were referenced by the receipts')
    with zipfile.ZipFile(apk) as archive:
        for name, expected in sorted(entries.items()):
            with archive.open(name) as stream:
                actual = hashlib.file_digest(stream, 'sha256').hexdigest()
            if actual != expected:
                raise ValueError('Latest APK object changed: ' + name)
    if latest_identity(metadata['url']) != (metadata['md5'], metadata['size']):
        raise ValueError('The official APK changed while it was being verified')
    return dict(official_apk={key: metadata[key] for key in ('url', 'size', 'md5', 'sha256', 'manifest')},
                live_manifest=selected.version, live_resource_version=live['ResVersion'],
                input_receipts=inputs, apk_entries=[dict(path=name, sha256=digest)
                                                    for name, digest in sorted(entries.items())])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-metadata', type=Path, required=True)
    parser.add_argument('--receipt', type=Path, action='append', required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--write', action='store_true')
    args = parser.parse_args()
    result = verify(args.source_metadata.resolve(), [path.resolve() for path in args.receipt])
    encoded = json.dumps(result, ensure_ascii=False, indent=2) + '\n'
    if args.write:
        args.output.write_text(encoded, encoding='utf-8')
    elif args.output.read_text(encoding='utf-8') != encoded:
        raise ValueError('Recorded APK equivalence proof differs')
    print('Latest official APK and live manifests verified:', len(result['apk_entries']), 'referenced entries')


if __name__ == '__main__':
    main()
