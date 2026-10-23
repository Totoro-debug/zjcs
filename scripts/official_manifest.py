"""Read the pinned APK's YooAsset V3 bundle table without guessing filenames.

The layout and BundleTable section ID are verified against its IL2CPP metadata.
FileHash is the MD5 of the stored file, before any asset decryption.
Stored dependency IDs are retained as opaque values. Resolve CAB references
from UnityFS directory names, rather than treating these IDs as row positions.
"""
import struct


class Reader:
    def __init__(self, data):
        self.data = data
        self.pos = 0

    def read(self, fmt):
        length = struct.calcsize('<' + fmt)
        if self.pos + length > len(self.data):
            raise ValueError('Truncated YooAsset manifest')
        result = struct.unpack_from('<' + fmt, self.data, self.pos)
        self.pos += length
        return result[0] if len(result) == 1 else result

    def string(self):
        length = self.read('H')
        end = self.pos + length
        if end > len(self.data):
            raise ValueError('Truncated manifest string')
        result = self.data[self.pos:end].decode('utf-8')
        self.pos = end
        return result


def read_manifest(data):
    reader = Reader(data)
    if reader.read('I') != 0x00594F4F or reader.string() != '3.0.0':
        raise ValueError('Expected YooAsset binary manifest V3')
    addressable, lower, guid, style, seed = reader.read('???IQ')
    pipeline, package, version = reader.string(), reader.string(), reader.string()
    sections = {}
    count = reader.read('I')
    if count != 10:
        raise ValueError(f'Unexpected V3 section count: {count}')
    for _ in range(count):
        kind, offset, length, records = reader.read('IIII')
        if kind in sections or offset + length > len(data):
            raise ValueError('Invalid manifest section bounds')
        sections[kind] = (offset, length, records)
    if 4 not in sections:
        raise ValueError('Missing BundleTable section')
    offset, length, count = sections[4]
    reader = Reader(data[offset:offset + length])
    bundles = []
    for index in range(count):
        record_offset = reader.pos
        dependencies = [reader.read('I') for _ in range(reader.read('H'))]
        unity_crc, file_crc, size, encrypted = reader.read('IIQ?')
        name, file_hash = reader.string(), reader.string()
        tags = [reader.string() for _ in range(reader.read('H'))]
        if (not name.endswith(('.bundle', '.rawfile')) or '/' in name or '\\' in name
                or len(file_hash) != 32 or any(c not in '0123456789abcdef' for c in file_hash)
                or any(d >= count for d in dependencies)):
            raise ValueError(f'Invalid manifest bundle #{index}')
        bundles.append({'name': name, 'hash': file_hash, 'size': size, 'record_offset': record_offset,
                        'unity_crc': unity_crc, 'file_crc': file_crc,
                        'encrypted': encrypted, 'dependencies': dependencies, 'tags': tags,
                        'file': file_hash + '.' + name.rsplit('.', 1)[1]})
    if reader.pos != length or len({b['name'] for b in bundles}) != count:
        raise ValueError('Bundle table has trailing data or duplicate names')
    return {'package': package, 'version': version, 'pipeline': pipeline,
            'output_name_style': style, 'seed': seed, 'bundles': bundles}


def inventory_apk(apk, verify_packaged=False):
    import hashlib
    import zipfile

    packages, bundles = {}, []
    with zipfile.ZipFile(apk) as archive:
        files = set(archive.namelist())
        for package in ['DefaultPackage', 'Boot', 'Launch', 'HybridCLR', 'WwiseBnk']:
            source = f'assets/yoo/{package}/PackageManifest_{package}_854_156383.v3.bytes'
            data = archive.read(source)
            manifest = read_manifest(data)
            if manifest['package'] != package or manifest['version'] != '854_156383':
                raise ValueError('Unexpected APK resource manifest')
            summary = {'total': len(manifest['bundles']), 'packaged': 0,
                       'missing': 0, 'missing_bytes': 0,
                       'manifest_sha256': hashlib.sha256(data).hexdigest()}
            for bundle in manifest['bundles']:
                path = f'assets/yoo/{package}/{bundle["file"]}'
                packaged = path in files
                bundle = dict(bundle, package=package, packaged=packaged)
                if packaged:
                    if archive.getinfo(path).file_size != bundle['size']:
                        raise ValueError(f'Packaged file size mismatch: {path}')
                    if verify_packaged:
                        with archive.open(path) as stream:
                            if hashlib.file_digest(stream, 'md5').hexdigest() != bundle['hash']:
                                raise ValueError(f'Packaged file MD5 mismatch: {path}')
                    summary['packaged'] += 1
                else:
                    summary['missing'] += 1
                    summary['missing_bytes'] += bundle['size']
                bundles.append(bundle)
            packages[package] = summary
    return {'manifest': '854_156383', 'packages': packages, 'bundles': bundles}


def read_asset_manifest(data):
    """Read V3 AssetTable GUIDs with exact section and row byte boundaries."""
    manifest = read_manifest(data)
    reader = Reader(data)
    reader.read('I'); reader.string(); reader.read('???IQ')
    for _ in range(3): reader.string()
    sections = [reader.read('IIII') for _ in range(reader.read('I'))]
    # Bundle IDs address the name-hash lookup order, whose values are byte
    # offsets into BundleTable. They are not physical BundleTable row indices.
    _, lookup_offset, lookup_length, bundle_count = next(row for row in sections if row[0] == 10)
    if lookup_length != bundle_count * 12:
        raise ValueError('Unexpected V3 bundle name lookup layout')
    bundle_offsets = struct.unpack_from('<' + str(bundle_count) + 'I', data, lookup_offset + bundle_count * 8)
    bundle_by_offset = {row['record_offset']: row['name'] for row in manifest['bundles']}
    _, offset, length, count = next(row for row in sections if row[0] == 2)
    reader = Reader(data[offset:offset + length])
    assets = []
    for _ in range(count):
        bundle_id = reader.read('I')
        address, path, guid = [reader.string() for _ in range(3)]
        tags = [reader.string() for _ in range(reader.read('H'))]
        if bundle_id >= bundle_count or bundle_offsets[bundle_id] not in bundle_by_offset or not path.startswith('Assets/') or (guid and (len(guid) != 32 or any(c not in '0123456789abcdef' for c in guid))):
            raise ValueError('Invalid V3 asset record')
        assets.append(dict(address=address, path=path, guid=guid, tags=tags, bundle=bundle_by_offset[bundle_offsets[bundle_id]]))
    if reader.pos != length:
        raise ValueError('Asset table has trailing bytes')
    return assets


if __name__ == '__main__':
    import argparse
    import json
    from fetch_official_assets import APK, CACHE, download

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--verify-packaged', action='store_true', help='Verify packaged file MD5 as well as size')
    parser.add_argument('--manifest', help='Explicit historical manifest; defaults to the active official source')
    args = parser.parse_args()
    download()
    from official_source import OfficialSource
    inventory = OfficialSource(args.manifest).inventory(args.verify_packaged)
    destination = CACHE / 'cloud-research/bundle-inventory.json'
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(inventory, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(inventory['packages'], ensure_ascii=False, indent=2))
    print('Inventory:', destination)
