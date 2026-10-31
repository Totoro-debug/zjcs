"""Export verified official union inputs to a separate native Unity cache.

Never overwrites the working game's assets or its stable GUIDs. Shader YAML
retains Android programs for subsequent Windows migration, never dummy shaders.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import socket
import subprocess
import time
import urllib.parse
import urllib.request

from official_apk import ROOT
from official_source import OfficialSource
from validation_workspace import temporary_validation_directory


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def convert(cache, inputs_subdir='inputs', export_name='native-export'):
    cache = cache.resolve()
    version = OfficialSource().version
    allowed = (ROOT / '.tools/source-packages/current' / version).resolve()
    if not cache.is_relative_to(allowed):
        raise ValueError('Use the selected current conversion cache')
    receipt_path = cache / 'source-manifest.json'
    receipt = json.loads(receipt_path.read_bytes())
    selection_path = cache / 'changed-inputs-source-manifest.json'
    if inputs_subdir == 'changed-inputs':
        selection = json.loads(selection_path.read_bytes())
        if selection['manifest'] != version or selection['input_manifest_sha256'] != sha(receipt_path):
            raise ValueError('Changed input selection differs from current source')
        receipt = dict(receipt, inputs=selection['inputs'])
    if receipt['manifest'] != version:
        raise ValueError('Converter source version differs')
    if not re.fullmatch(r'native-export(?:-[a-z0-9]+)?', export_name):
        raise ValueError('Use a native-export cache name')
    destination = cache / export_name
    if destination.exists():
        raise FileExistsError('Retain the existing native export; do not overwrite it')
    for row in receipt['inputs']:
        if sha(cache / row['output']) != row['decoded_sha256']:
            raise ValueError('Converter input changed: ' + row['output'])
    code = allowed / 'code-manifest.json'
    assemblies = json.loads(code.read_bytes())
    if assemblies['manifest'] != version:
        raise ValueError('Assembly source version differs')
    managed = cache / inputs_subdir / 'Managed'
    managed.mkdir(exist_ok=True)
    for name, record in assemblies['assemblies'].items():
        original = allowed / 'official-code/managed/decoded' / name
        if sha(original) != record['decoded_sha256']:
            raise ValueError('Decoded assembly hash differs: ' + name)
        shutil.copyfile(original, managed / name)
    # Resolve original AOT fields before any editor compatibility dependencies.
    # These descriptors have stub methods and are converter inputs only.
    aot = allowed/'official-code/aot-metadata'
    aot_manifest = json.loads((aot/'source-manifest.json').read_bytes())
    if aot_manifest['manifest'] != version:
        raise ValueError('AOT descriptor source version differs')
    for row in aot_manifest['outputs']:
        if sha(ROOT/row['path']) != row['sha256']:
            raise ValueError('AOT descriptor recovery changed: '+row['path'])
    editor = ROOT / 'unity-tools.json'
    editor_config = json.loads(editor.read_bytes())
    editor_path = Path(editor_config['editorExecutable'])
    engine = editor_path.parent / 'Data/Managed/UnityEngine'
    dependencies = sorted((aot/'DummyDll').glob('*.dll')) + list(engine.glob('*.dll'))
    mono = editor_path.parent / 'Data/MonoBleedingEdge/lib/mono/unityjit-win32'
    dependencies += list(mono.glob('*.dll')) + list((mono/'Facades').glob('*.dll'))
    package_names = ('UnityEngine.UI', 'Unity.TextMeshPro', 'Unity.Timeline', 'Unity.Mathematics', 'Cinemachine', 'Coffee.UIParticle', 'UniTask', 'spine-csharp', 'spine-unity')
    dependencies += [ROOT/'unity/Library/ScriptAssemblies'/(name+'.dll') for name in package_names]
    dependency_records = []
    used_dependency_names = set()
    for original in dependencies:
        if not original.is_file():
            continue
        if original.name in used_dependency_names:
            continue
        used_dependency_names.add(original.name)
        if original.name in assemblies['assemblies']:
            continue  # Real hot-update implementations take precedence over AOT stubs.
        target = cache / inputs_subdir / 'Managed' / original.name
        target.parent.mkdir(exist_ok=True)
        shutil.copyfile(original, target)
        dependency_records.append(dict(path=str(original), sha256=sha(original), output=target.relative_to(cache).as_posix(),
                                       role='original-aot-type-descriptor' if original.is_relative_to(aot) else 'editor-type-fallback'))
    if not any(Path(row['path']).name == 'UnityEngine.CoreModule.dll' for row in dependency_records):
        raise FileNotFoundError('Matching editor UnityEngine.CoreModule.dll is required for script fields')
    with temporary_validation_directory('official-resource-export-') as temporary:
        work = Path(temporary)
        runner = work / 'runner'
        runner.mkdir()
        for name in ('AssetRipper.GUI.Free.exe', 'capstone.dll'):
            shutil.copyfile(ROOT / '.tools/assetripper-1.3.14' / name, runner / name)
        with socket.socket() as listener:
            listener.bind(('127.0.0.1', 0))
            port = listener.getsockname()[1]
        process = subprocess.Popen([str(runner / 'AssetRipper.GUI.Free.exe'), '--headless', '--port', str(port),
                                    '--log-path', str(work / 'ripper.log')], cwd=runner,
                                   creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0),
                                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        base = 'http://127.0.0.1:' + str(port)
        settings = dict(ShaderExportMode='Yaml', ScriptExportMode='DllExportWithoutRenaming',
                        SpriteExportMode='Yaml', TextExportMode='Bytes', ImageExportFormat='Png',
                        AudioExportFormat='PreferWav', BundledAssetsExportMode='DirectExport',
                        ScriptContentLevel='Level2', ScriptLanguageVersion='AutoSafe',
                        LightmapTextureExportFormat='Yaml', DefaultVersion='2022.3.57f1',
                        TargetVersion='2022.3.57f1', ExportUnreadableAssets='on')
        def post(endpoint, data):
            request = urllib.request.Request(base + endpoint,
                        data=urllib.parse.urlencode(data).encode(), method='POST')
            with urllib.request.urlopen(request, timeout=7200) as response:
                response.read()
        try:
            deadline = time.monotonic() + 60
            while True:
                try:
                    urllib.request.urlopen(base + '/openapi.json', timeout=2).close()
                    break
                except OSError:
                    if process.poll() is not None or time.monotonic() >= deadline:
                        raise RuntimeError('Private converter failed to start')
                    time.sleep(.25)
            post('/Settings/Update', settings)
            print('Loading verified union inputs', flush=True)
            post('/LoadFolder', dict(path=str(cache / inputs_subdir)))
            print('Exporting native Unity project', flush=True)
            post('/Export/UnityProject', dict(path=str(work / 'export')))
        finally:
            process.terminate()
            process.wait(timeout=30)
            time.sleep(1)
        exported = work / 'export'
        if not (exported / 'ExportedProject/Assets').is_dir():
            raise RuntimeError('Converter produced no Unity project')
        log = (work / 'ripper.log').read_text('utf8', errors='replace')
        issues = [line for line in log.splitlines() if re.search(r'error|exception|failed|unable|could not', line, re.I)]
        files = [dict(path=p.relative_to(exported).as_posix(), sha256=sha(p), bytes=p.stat().st_size)
                 for p in exported.rglob('*') if p.is_file()]
        if exported.resolve().parent != work.resolve() or not destination.resolve().is_relative_to(allowed):
            raise ValueError('Native output move is outside the verified workspace/cache')
        exported.rename(destination)
        result = dict(manifest=version, input_manifest_sha256=sha(receipt_path), code_manifest_sha256=sha(code),
                      type_dependencies=dependency_records, aot_manifest_sha256=sha(aot/'source-manifest.json'),
                      selected_inputs=inputs_subdir, selection_sha256=sha(selection_path) if inputs_subdir == 'changed-inputs' else None,
                      generator_sha256=sha(Path(__file__)), exporter_sha256=sha(runner/'AssetRipper.GUI.Free.exe'),
                      settings=settings, outputs=files, conversion_issues=issues, export_directory=export_name,
                      completed=not any('[Error]' in line for line in log.splitlines()),
                      limitations=['Separate native conversion cache; existing game assets retained.',
                                   'Script binding, managed-reference registries, Windows shaders, exact texture mips and runtime validation remain module-specific.'])
        (cache/(export_name+'-source-manifest.json')).write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', 'utf8')
        print('Exported files:', len(files), 'conversion issue lines:', len(issues), flush=True)
        if not result['completed']:
            raise RuntimeError('Native export is incomplete; partial files retained at '+str(destination))
        return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cache', type=Path, required=True)
    parser.add_argument('--inputs-subdir', choices=['inputs', 'changed-inputs'], default='inputs')
    parser.add_argument('--export-name', default='native-export')
    args = parser.parse_args()
    convert(args.cache, args.inputs_subdir, args.export_name)
