"""Select the configured current official APK and matching CDN resources.

The native APK and hot-update manifests have independent versions. Every source
manifest is pinned by SHA256 in the tracked official-source.json configuration.
"""
import hashlib
import json
from pathlib import Path
import zipfile

from official_manifest import read_manifest

ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / '.tools/source-packages'


class OfficialSource:
    def __init__(self, manifest=None):
        configuration = json.loads((ROOT / 'official-source.json').read_text(encoding='utf-8'))
        self.version = manifest or configuration['active']
        self.profile = configuration['profiles'][self.version]
        self.apk = ROOT / self.profile['apk']['path']
        self.apk_version = self.profile['apk']['manifest']
        self.apk_sha256 = self.profile['apk']['sha256']
        self.cloud_path = ROOT / self.profile['cloud_cache']
        self.manifests = {m['package']: m for m in self.profile['manifests']}
        self.cab_path = (CACHE / 'cab-index.json' if self.version == self.apk_version
                         else self.cloud_path / 'apk-cab-index.json')

    def manifest_bytes(self, package, archive=None):
        source = self.manifests[package]
        filename = f'PackageManifest_{package}_{source["version"]}.v3.bytes'
        if self.version == self.apk_version:
            if archive is None:
                with zipfile.ZipFile(self.apk) as opened:
                    data = opened.read(f'assets/yoo/{package}/{filename}')
            else:
                data = archive.read(f'assets/yoo/{package}/{filename}')
        else:
            data = (self.cloud_path / 'manifests' / package / filename).read_bytes()
        if hashlib.sha256(data).hexdigest() != source['sha256']:
            raise ValueError(f'Official {self.version}/{package} manifest SHA256 mismatch')
        if source.get('md5') and hashlib.md5(data).hexdigest() != source['md5']:
            raise ValueError(f'Official {self.version}/{package} manifest MD5 mismatch')
        parsed = read_manifest(data)
        if parsed['package'] != package or parsed['version'] != source['version']:
            raise ValueError(f'Official {self.version}/{package} manifest version mismatch')
        return data

    def manifest(self, package, archive=None):
        return read_manifest(self.manifest_bytes(package, archive))

    def inventory(self, verify_packaged=False):
        packages, bundles = {}, []
        with zipfile.ZipFile(self.apk) as archive:
            files = set(archive.namelist())
            for package in self.manifests:
                data = self.manifest_bytes(package, archive)
                manifest = read_manifest(data)
                summary = dict(total=len(manifest['bundles']), packaged=0, missing=0,
                               missing_bytes=0, manifest_sha256=hashlib.sha256(data).hexdigest(),
                               manifest=manifest['version'])
                for bundle in manifest['bundles']:
                    entry = f'assets/yoo/{package}/{bundle["file"]}'
                    packaged = entry in files
                    if packaged:
                        if archive.getinfo(entry).file_size != bundle['size']:
                            raise ValueError('APK entry size mismatch: ' + entry)
                        if verify_packaged:
                            with archive.open(entry) as stream:
                                if hashlib.file_digest(stream, 'md5').hexdigest() != bundle['hash']:
                                    raise ValueError('APK entry MD5 mismatch: ' + entry)
                        summary['packaged'] += 1
                    else:
                        summary['missing'] += 1
                        summary['missing_bytes'] += bundle['size']
                    bundles.append(dict(bundle, package=package, packaged=packaged,
                                        manifest=manifest['version']))
                packages[package] = summary
        return dict(manifest=self.version, apk_manifest=self.apk_version,
                    resource_version=self.profile['resource_version'], packages=packages, bundles=bundles)
