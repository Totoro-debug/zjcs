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
        self.prepared_path = CACHE / 'current' / self.version
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

    def verify_code_files(self, paths):
        """Verify consumed C# against the selected assembly/decompilation receipts."""
        code_path = self.prepared_path / 'code-manifest.json'
        archive_path = self.prepared_path / 'client-decompilation-manifest.json'
        code_raw, archive_raw = code_path.read_bytes(), archive_path.read_bytes()
        code, archive = json.loads(code_raw), json.loads(archive_raw)
        for document in (code, archive):
            if document['manifest'] != self.version or document['apk_sha256'] != self.apk_sha256:
                raise ValueError('Official code receipt belongs to a different source')
        if (archive['input_receipt']['path'] != code_path.relative_to(ROOT).as_posix()
                or archive['input_receipt']['sha256'] != hashlib.sha256(code_raw).hexdigest()
                or archive['assemblies'] != code['assemblies']):
            raise ValueError('Official decompilation and assembly receipts differ')
        records, owners = {}, {}
        for project in archive['projects']:
            for record in project['files']:
                if record['path'] in records:
                    raise ValueError('Duplicate official code identity: ' + record['path'])
                records[record['path']] = record
                owners[record['path']] = project['assembly']
        result = {
            code_path.relative_to(ROOT).as_posix(): hashlib.sha256(code_raw).hexdigest(),
            archive_path.relative_to(ROOT).as_posix(): hashlib.sha256(archive_raw).hexdigest(),
        }
        assemblies = set()
        for value in paths:
            path = Path(value).resolve()
            if not path.is_relative_to((self.prepared_path / 'official-code').resolve()):
                raise ValueError('Consumed code is outside the selected source: ' + str(path))
            name = path.relative_to(ROOT).as_posix()
            record = records.get(name)
            raw = path.read_bytes()
            digest = hashlib.sha256(raw).hexdigest()
            if not record or len(raw) != record['bytes'] or digest != record['sha256']:
                raise ValueError('Consumed code differs from its official decompilation: ' + name)
            result[name] = digest
            assemblies.add(owners[name])
        for name in sorted(assemblies):
            path = self.prepared_path / 'official-code/managed/decoded' / name
            digest = hashlib.sha256(path.read_bytes()).hexdigest()
            if digest != code['assemblies'][name]['decoded_sha256']:
                raise ValueError('Decoded official assembly changed: ' + name)
            result[path.relative_to(ROOT).as_posix()] = digest
        return result

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
