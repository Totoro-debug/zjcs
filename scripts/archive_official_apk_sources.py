"""Statically decode the fixed APK's embedded assemblies separately from CDN code."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import zipfile

from official_apk import Apk, ROOT, UnityPy
from official_manifest import read_manifest
from official_source import OfficialSource
from prepare_official_sources import decode_assembly
from archive_official_client_sources import FOLDERS, record
from validation_workspace import validation_directory


def archive():
    source = OfficialSource()
    destination = source.prepared_path / 'official-code/apk'
    with source.apk.open('rb') as stream:
        if hashlib.file_digest(stream, 'sha256').hexdigest() != source.apk_sha256:
            raise ValueError('Selected APK hash differs')
    assemblies, inputs = {}, {}
    with zipfile.ZipFile(source.apk) as package:
        manifest_path = f'assets/yoo/HybridCLR/PackageManifest_HybridCLR_{source.apk_version}.v3.bytes'
        raw_manifest = package.read(manifest_path)
        manifest = read_manifest(raw_manifest)
        if manifest['version'] != source.apk_version:
            raise ValueError('Embedded assembly manifest differs')
        for row in manifest['bundles']:
            entry = 'assets/yoo/HybridCLR/' + row['file']
            raw = package.read(entry)
            if len(raw) != row['size'] or hashlib.md5(raw).hexdigest() != row['hash']:
                raise ValueError('Embedded assembly bundle differs: ' + entry)
            env = UnityPy.load(Apk.decode(raw))
            for obj in env.objects:
                if obj.type.name != 'TextAsset':
                    continue
                asset = obj.read()
                if not asset.m_Name.endswith('.dll'):
                    continue
                if asset.m_Name in assemblies:
                    raise ValueError('Duplicate APK assembly: ' + asset.m_Name)
                assemblies[asset.m_Name] = asset.m_Script.encode('utf8', 'surrogateescape')
                inputs[asset.m_Name] = dict(apk_entry=entry, bundle=row['name'], bundle_hash=row['hash'],
                    raw_sha256=hashlib.sha256(raw).hexdigest(), path_id=str(obj.path_id))
    if set(assemblies) != set(FOLDERS):
        raise ValueError('Review the changed APK assembly inventory')
    dotnet = ROOT / '.tools/dotnet/dotnet.exe'
    ilspy = ROOT / '.tools/ilspycmd/tools/net10.0/any/ilspycmd.dll'
    projects = []
    with tempfile.TemporaryDirectory(prefix='apk-code-', dir=validation_directory()) as temporary:
        staging = Path(temporary)
        managed = staging / 'managed/decoded'
        managed.mkdir(parents=True)
        (managed / 'Launch.dll').write_bytes(assemblies['Launch.dll'])
        def decompile(name, folder):
            print('APK decompile:', name, flush=True)
            subprocess.run([str(dotnet), str(ilspy), '-p', str(managed/name), '-o', str(staging/folder)],
                           check=True, capture_output=True,
                           creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
        decompile('Launch.dll', FOLDERS['Launch.dll'])
        decryptor = (staging/'launch-source/Launch/HCC.cs').read_text('utf8')
        if 'BitConverter.ToInt32(bytes, 0)' in decryptor and 'Buffer.BlockCopy(bytes, num + num2' in decryptor:
            layout = 'length-prefixed'
        elif 'TransformFinalBlock(bytes, 0, bytes.Length)' in decryptor:
            layout = 'whole'
        else:
            raise ValueError('Inspect the embedded APK decryptor')
        for name, raw in assemblies.items():
            decoded = decode_assembly(raw, layout)
            (managed/name).write_bytes(decoded)
            inputs[name]['decoded_sha256'] = hashlib.sha256(decoded).hexdigest()
        for name, folder in FOLDERS.items():
            if name != 'Launch.dll':
                decompile(name, folder)
            paths = list((staging/folder).rglob('*'))
            if not any(p.suffix == '.csproj' for p in paths) or not any(p.suffix == '.cs' for p in paths):
                raise ValueError('Empty APK decompilation: ' + name)
            projects.append(dict(assembly=name, folder=folder,
                                 csharp_files=sum(p.suffix == '.cs' for p in paths)))
        for item in staging.rglob('*'):
            if not item.is_file():
                continue
            target = destination/item.relative_to(staging)
            target.parent.mkdir(parents=True, exist_ok=True)
            if target.exists() and target.read_bytes() != item.read_bytes():
                raise ValueError('Existing APK code archive differs: ' + str(target))
            if not target.exists():
                shutil.copy2(item, target)
    result = dict(manifest=source.version, source_manifest=source.apk_version, apk_sha256=source.apk_sha256,
                  assembly_manifest_sha256=hashlib.sha256(raw_manifest).hexdigest(),
                  assembly_encryption=layout, generator=record(Path(__file__).resolve()),
                  operation='Static decompilation of embedded APK assemblies; no game assembly execution.',
                  assemblies=inputs, projects=projects,
                  decoded_directory=(destination/'managed/decoded').relative_to(ROOT).as_posix(),
                  outputs=[record(p) for p in sorted(destination.rglob('*')) if p.is_file()])
    (source.prepared_path/'apk-code-manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', 'utf8')
    print('Archived embedded APK assemblies:', len(assemblies), flush=True)


if __name__ == '__main__':
    archive()
