"""Read pinned raw bundles. Parsing and conversion belong to the caller.

The reader owns its APK archive; use a context manager or close() explicitly.
No network requests are made, and an invalid APK entry never falls back to CDN.
"""
from dataclasses import dataclass
import hashlib
from types import MappingProxyType
from typing import Mapping
import zipfile


@dataclass(frozen=True)
class VerifiedBundle:
    data: bytes
    provenance: Mapping


def verify_payload(data, bundle, *, receipt=None, identity='bundle'):
    if len(data) != bundle['size'] or hashlib.md5(data).hexdigest() != bundle['hash']:
        raise ValueError(f'Official manifest size/MD5 mismatch: {identity}')
    digest = hashlib.sha256(data).hexdigest()
    if receipt is not None and (digest != receipt['sha256']
            or receipt['md5'] != bundle['hash'] or receipt['size'] != bundle['size']):
        raise ValueError(f'Official CDN cache failed verification: {identity}')
    return digest


class OfficialBundleReader:
    def __init__(self, source=None, *, reuse_current_cache=False):
        from official_source import OfficialSource
        from official_cloud_cache import CloudCache
        self.source = source or OfficialSource()
        with self.source.apk.open('rb') as stream:
            if hashlib.file_digest(stream, 'sha256').hexdigest() != self.source.apk_sha256:
                raise ValueError('Pinned APK checksum mismatch')
        self.archive = zipfile.ZipFile(self.source.apk)
        self._manifests = {}
        self._indexes = {}
        self.available = set(self.archive.namelist())
        try:
            self.cloud = CloudCache(self.source.cloud_path, self.source.version)
            self.cloud_source = self.source
            if reuse_current_cache and not self.source.profile.get('cloud_cache_retained', True):
                self.cloud_source = OfficialSource()
                self.cloud = CloudCache(self.cloud_source.cloud_path, self.cloud_source.version)
        except BaseException:
            self.close()
            raise

    def manifest(self, package):
        if package not in self._manifests:
            manifest = self.source.manifest(package, self.archive)
            self._manifests[package] = manifest
            self._indexes[package] = {row['name']: dict(row) for row in manifest['bundles']}
        return self._manifests[package]

    def read(self, package, bundle_name):
        self.manifest(package)
        bundle = self._indexes[package][bundle_name]
        entry = f'assets/yoo/{package}/{bundle["file"]}'
        if entry in self.available:
            data = self.archive.read(entry)
            digest = verify_payload(data, bundle, identity=bundle_name)
            source = dict(origin='apk_zip', apk_entry=entry, manifest=self.source.version,
                          apk_manifest=self.source.apk_version, apk_sha256=self.source.apk_sha256,
                          raw_size=len(data), raw_sha256=digest)
        else:
            expected_version = self.cloud_source.manifests[package]['version']
            actual_version = self.cloud.versions.get(package)
            cached = self.cloud.read(package, bundle)
            if cached is None:
                raise FileNotFoundError(f'Not packaged or cached: {bundle_name}')
            if actual_version != expected_version:
                raise ValueError(f'Official CDN cache manifest version mismatch: {package}')
            data, source = cached
        return VerifiedBundle(data, MappingProxyType(source))

    def close(self):
        self.archive.close()

    def __enter__(self):
        return self

    def __exit__(self, *args):
        self.close()
