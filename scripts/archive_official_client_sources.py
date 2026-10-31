"""Archive all selected HybridCLR assemblies as unmodified ILSpy C# projects.

Uses the existing verified APK/CDN extraction pipeline, without running game code.
Temporary decompilation and logs live in a disposable validation workspace.
"""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import uuid

from official_apk import ROOT, CACHE
from official_source import OfficialSource
from prepare_official_sources import build
from refresh_current_character_meshes import preserve


FOLDERS = {
    'Assembly-CSharp.dll': 'client-source',
    'Console.dll': 'common-source',
    'Framework.dll': 'framework-source',
    'Launch.dll': 'launch-source',
    'Framework.FirstPass.Runtime.dll': 'framework-firstpass-source',
    'CustomWater.dll': 'custom-water-source',
    'GameFramework.Runtime.dll': 'game-framework-source',
    'GFrameCore.Extensions.Cloneable.dll': 'cloneable-source',
    'LoopScrollRect.Runtime.dll': 'loop-scrollrect-source',
    'Luban.dll': 'luban-source',
}


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(),
                bytes=path.stat().st_size, sha256=digest(path))


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    raw = (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode('utf8')
    if path.exists() and path.read_bytes() != raw:
        preserve(path)
    path.write_bytes(raw)


def archive():
    source = OfficialSource()
    destination = CACHE / 'current' / source.version
    dotnet = ROOT / '.tools/dotnet/dotnet.exe'
    ilspy = ROOT / '.tools/ilspycmd/tools/net10.0/any/ilspycmd.dll'
    version = subprocess.check_output([str(dotnet), str(ilspy), '--version'], text=True).strip()
    session = ROOT / 'output/validation' / ('client-decompile-' + uuid.uuid4().hex)
    session.mkdir(parents=True)
    previous = {key: os.environ.get(key) for key in ('ZJCS_VALIDATION_DIR', 'ZJCS_REPOSITORY_ROOT')}
    os.environ.update(ZJCS_VALIDATION_DIR=str(session), ZJCS_REPOSITORY_ROOT=str(ROOT))
    try:
        with tempfile.TemporaryDirectory(dir=session) as temporary:
            staging = Path(temporary)
            # Re-extract, verify, decrypt and decompile the five established projects.
            build(str(dotnet), ilspy, client=True, output_root=staging)
            receipt = json.loads((staging / 'code-manifest.json').read_bytes())
            if set(receipt['assemblies']) != set(FOLDERS):
                raise ValueError('Assembly inventory changed; explicitly review archive scope')
            established = {'client-source', 'common-source', 'framework-source',
                           'launch-source', 'framework-firstpass-source'}
            for name, folder in FOLDERS.items():
                if folder in established:
                    continue
                print('Decompiling', name, flush=True)
                subprocess.run([str(dotnet), str(ilspy), '-p',
                                str(staging / 'official-code/managed/decoded' / name),
                                '-o', str(staging / 'official-code' / folder)], check=True)
            existing = json.loads((destination / 'code-manifest.json').read_bytes())
            if (existing['assemblies'] != receipt['assemblies'] or
                    existing['metadata_sha256'] != receipt['metadata_sha256'] or
                    existing['apk_sha256'] != receipt['apk_sha256']):
                raise ValueError('Existing version archive differs from verified inputs')
            for name, row in receipt['assemblies'].items():
                for root in (destination, CACHE):
                    if digest(root / 'official-code/managed/decoded' / name) != row['decoded_sha256']:
                        raise ValueError('Existing decoded assembly differs: ' + name)
            projects = []
            for name, folder in FOLDERS.items():
                generated = staging / 'official-code' / folder
                if not list(generated.glob('*.csproj')) or not list(generated.rglob('*.cs')):
                    raise ValueError('Empty ILSpy project: ' + name)
                files = []
                for item in sorted(generated.rglob('*')):
                    if not item.is_file():
                        continue
                    relative = item.relative_to(staging)
                    for root in (destination, CACHE):
                        target = root / relative
                        target.parent.mkdir(parents=True, exist_ok=True)
                        if target.exists() and digest(target) != digest(item):
                            preserve(target)
                        shutil.copyfile(item, target)
                    files.append(record(destination / relative))
                projects.append(dict(assembly=name, folder=folder, files=files,
                                     csharp_files=sum(p['path'].endswith('.cs') for p in files)))
            manifest = dict(format=1, manifest=source.version, apk_manifest=source.apk_version,
                            apk_sha256=source.apk_sha256,
                            operation='Static IL decompilation; no source adaptation or game assembly execution',
                            tools=dict(version=version, ilspy=record(ilspy), dotnet=record(dotnet)),
                            generator=record(Path(__file__).resolve()),
                            input_receipt=record(destination / 'code-manifest.json'),
                            assemblies=receipt['assemblies'], projects=projects,
                            aot_scope='Native IL2CPP inputs and existing type stubs are separate; no recovered AOT method bodies claimed')
            local = destination / 'client-decompilation-manifest.json'
            write_json(local, manifest)
            summary = {key: value for key, value in manifest.items() if key not in ('projects', 'assemblies')}
            summary['archive_manifest'] = record(local)
            summary['projects'] = [dict(assembly=p['assembly'], folder=p['folder'],
                                        files=len(p['files']), csharp_files=p['csharp_files'],
                                        source=receipt['assemblies'][p['assembly']]) for p in projects]
            write_json(ROOT / 'assets/reference/official-client-code/source-manifest.json', summary)
            print('Archived', len(projects), 'projects;', sum(p['csharp_files'] for p in projects), 'C# files', flush=True)
    finally:
        for key, value in previous.items():
            if value is None:
                os.environ.pop(key, None)
            else:
                os.environ[key] = value
        # This session is created above; TemporaryDirectory has already stopped all work.
        session.rmdir()


if __name__ == '__main__':
    archive()
