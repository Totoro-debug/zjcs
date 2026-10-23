"""Read the pinned APK and verified local CDN supplements. No network requests."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import struct
import sys
import zipfile
import uuid

ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / '.tools/source-packages'
sys.path.insert(0, str(ROOT / '.tools/character-art-python'))
import UnityPy


def canonical_bundle_name(record, index):
    """Resolve a stale CAB cache alias using the unchanged pinned manifest MD5."""
    name = record['bundle']
    digest = record.get('hash')
    if name in index:
        if digest and index[name] != digest:
            raise ValueError(f'CAB cache hash differs from pinned manifest: {name}')
        return name
    matches = [candidate for candidate, expected in index.items() if expected == digest]
    if len(matches) != 1:
        raise ValueError(f'CAB cache alias has no unique pinned manifest match: {name}')
    return matches[0]


class Apk:
    # Existing module rebuilders reproduce their recorded historical inputs.
    # Current source commands pass manifest=None or use Apk.current().
    def __init__(self, manifest='854_156383'):
        from official_source import OfficialSource
        self.source = OfficialSource(manifest)
        self.version = self.source.version
        APK, SHA256 = self.source.apk, self.source.apk_sha256
        with APK.open('rb') as stream:
            if hashlib.file_digest(stream, 'sha256').hexdigest() != SHA256:
                raise ValueError('Pinned APK checksum mismatch')
        self.archive = zipfile.ZipFile(APK)
        from official_cloud_cache import CloudCache
        manifest = self.source.manifest('DefaultPackage', self.archive)
        self.bundles = manifest['bundles']
        self.bundle_records = {b['name']: b for b in self.bundles}
        self.index = {b['name']: b['hash'] for b in self.bundles}
        self.cloud = CloudCache(self.source.cloud_path, self.version)
        self.available = set(self.archive.namelist())
        self.env = UnityPy.Environment()
        self.loaded = {}
        self.missing = set()
        self.cabs = json.loads(self.source.cab_path.read_text()) if self.source.cab_path.exists() else self.index_cabs()
        self.cabs = {k.lower(): v for k, v in self.cabs.items()}
        self.cabs.update({k: v for k, v in self.cloud.index_cabs(self.bundles).items() if k not in self.cabs})
        original = self.env.find_file

        def resolve(name, *args, **kwargs):
            key = name.rsplit('/', 1)[-1].lower()
            cached=self.env.get_cab(key)
            if cached is not None and key in self.cabs:return cached
            if key in ['unity_builtin_extra','globalgamemanagers','globalgamemanagers.assets','resources.assets','sharedassets0.assets','level0']:
                self.load_builtin('assets/bin/Data/data.unity3d')
            elif key=='unity default resources':
                self.load_builtin('assets/bin/Data/unity default resources')
            elif key in self.cabs:
                self.load(canonical_bundle_name(self.cabs[key], self.index))
            cached=self.env.get_cab(key)
            if cached is not None:return cached
            if key.startswith('cab-') or key in ['unity_builtin_extra','unity default resources']:
                raise FileNotFoundError('Dependency absent from pinned APK and verified CDN cache: '+name)
            return original(name, *args, **kwargs)

        self.env.find_file = resolve

    @classmethod
    def current(cls):
        return cls(manifest=None)

    def load_builtin(self, path):
        if path in self.loaded:return self.loaded[path]
        raw=self.archive.read(path)
        loaded=self.env.load_file(raw,name=path)
        self.loaded[path]=loaded
        assets=[loaded] if hasattr(loaded,'objects') else [v for v in loaded.files.values() if hasattr(v,'objects')]
        record=dict(bundle=path,hash=hashlib.md5(raw).hexdigest(),origin='apk_zip',apk_entry=path,
                    raw_size=len(raw),raw_sha256=hashlib.sha256(raw).hexdigest(),manifest=self.source.apk_version)
        for asset in assets:
            self.cabs[asset.name.lower()]=record
            self.env.register_cab(asset.name,asset)
        if path.endswith('/unity default resources') and len(assets)==1:
            self.env.register_cab('unity default resources',assets[0])
        return loaded

    @staticmethod
    def decode(data):
        if bytes(b ^ 0x40 for b in data[:7]) == b'UnityFS':
            return bytes(b ^ 0x40 for b in data)
        return data

    def index_cabs(self):
        from official_bundle_directory import cab_names
        result = {}
        available = set(self.archive.namelist())
        for name, digest in self.index.items():
            path = f'assets/yoo/DefaultPackage/{digest}.bundle'
            if path not in available:
                continue
            with self.archive.open(path) as stream:
                for cab in cab_names(stream):
                    result[cab] = {'bundle': name, 'hash': digest}
        self.source.cab_path.parent.mkdir(parents=True, exist_ok=True)
        temporary = self.source.cab_path.with_name(self.source.cab_path.name + '.' + uuid.uuid4().hex + '.tmp')
        temporary.write_text(json.dumps(result, indent=2), encoding='utf-8')
        temporary.replace(self.source.cab_path)
        return result

    def load(self, name):
        if not name.endswith('.bundle'):
            name = f'defaultpackage_assets_{name}.bundle'
        if name in self.loaded:
            return self.loaded[name]
        path = f'assets/yoo/DefaultPackage/{self.index[name]}.bundle'
        source = None
        if path in self.available:
            data = self.archive.read(path)
            source = dict(origin='apk_zip', apk_entry=path, manifest=self.version,
                          apk_manifest=self.source.apk_version, apk_sha256=self.source.apk_sha256,
                          raw_size=len(data), raw_sha256=hashlib.sha256(data).hexdigest())
        else:
            cached = self.cloud.read('DefaultPackage', self.bundle_records[name])
            if cached is None:
                self.missing.add(name)
                raise FileNotFoundError(f'Not packaged or cached: {name}')
            data, source = cached
        record = self.bundle_records[name]
        if len(data) != record['size'] or hashlib.md5(data).hexdigest() != record['hash']:
            raise ValueError('Official manifest size/MD5 mismatch: ' + name)
        bundle = self.env.load_file(self.decode(data), name=name)
        self.loaded[name] = bundle
        for asset in bundle.files.values():
            if hasattr(asset, 'objects'):
                self.cabs[asset.name.lower()] = dict({'bundle': name, 'hash': self.index[name]}, **(source or {}))
        return bundle

    def objects(self, name):
        bundle = self.load(name)
        return [obj for asset in bundle.files.values() if hasattr(asset, 'objects') for obj in asset.objects.values()]

    def container(self, name):
        bundle = self.load(name)
        for asset in bundle.files.values():
            if hasattr(asset, 'container'):
                yield from asset.container.items()

    def asset(self, name, suffix):
        return next(obj.deref() for path, obj in self.container(name) if path.endswith(suffix))

    def provenance(self, obj):
        key=obj.assets_file.name.lower()
        if key not in self.cabs and key in ['unity default resources','unity_builtin_extra']:
            self.load_builtin('assets/bin/Data/unity default resources' if key=='unity default resources' else 'assets/bin/Data/data.unity3d')
            native=self.env.get_cab(key)
            if native is None or obj.path_id not in native.objects or native.objects[obj.path_id].get_raw_data()!=obj.get_raw_data():
                raise ValueError('Builtin object differs from the pinned APK: '+key+':'+str(obj.path_id))
        record = self.cabs[key]
        source = {key: record[key] for key in ['origin', 'url', 'manifest', 'source_manifest',
                  'apk_manifest', 'apk_sha256', 'apk_entry', 'raw_size', 'raw_sha256'] if key in record}
        if record.get('origin') == 'official_cdn':
            source.update(bundle_size=record['size'], bundle_sha256=record['sha256'])
        return dict({'bundle': record['bundle'], 'bundle_hash': record['hash'], 'path_id': str(obj.path_id)}, **source)


class LubanReader:
    def __init__(self, data):
        self.data = data
        self.pos = 0

    def take(self, size):
        if size < 0 or self.pos + size > len(self.data):
            raise ValueError(f'Truncated data at {self.pos}, requested {size}')
        value = self.data[self.pos:self.pos + size]
        self.pos += size
        return value

    def uint(self):
        n = self.take(1)[0]
        if n < 0x80:
            return n
        if n < 0xc0:
            return ((n & 0x3f) << 8) | self.take(1)[0]
        if n < 0xe0:
            return ((n & 0x1f) << 16) | int.from_bytes(self.take(2), 'big')
        if n < 0xf0:
            return ((n & 0x0f) << 24) | int.from_bytes(self.take(3), 'big')
        if n == 0xf0:
            return int.from_bytes(self.take(4), 'big')
        raise ValueError(f'Invalid Luban integer {n:#x} at {self.pos - 1}')

    def integer(self):
        n = self.uint()
        return n if n < 2**31 else n - 2**32

    def string(self):
        return self.take(self.uint()).decode('utf-8')

    def scalar(self, kind):
        if kind == 'int':
            return self.integer()
        if kind == 'bool':
            n = self.take(1)[0]
            if n > 1:
                raise ValueError(f'Invalid boolean at {self.pos - 1}')
            return bool(n)
        if kind == 'string':
            return self.string()
        if kind == 'float':
            return struct.unpack('<f', self.take(4))[0]
        if kind == 'double':
            return struct.unpack('<d', self.take(8))[0]
        raise ValueError(kind)

    def collection(self, key_kind, value_kind=None):
        # Luban nullable collections carry a presence byte before their size.
        present = self.scalar('bool')
        if not present:
            return None
        size = self.uint()
        if size > 100000:
            raise ValueError('Invalid collection length')
        if value_kind is None:
            return [self.scalar(key_kind) for _ in range(size)]
        return {str(self.scalar(key_kind)): self.scalar(value_kind) for _ in range(size)}
