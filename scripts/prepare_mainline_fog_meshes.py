"""Reproduce region Freeform meshes with pinned Unity 2022.3 URP LightUtility.

The isolated Editor tool does not add a URP or Mathematics dependency to the game.
Its inputs are the verified APK shape path and unmodified Unity Graphics sources.
"""
from pathlib import Path
import argparse
import base64
import hashlib
import json
import shutil
import subprocess
import tarfile
import urllib.request
from prepare_unity_map10_entities import ROOT, sha256, json_bytes, write_meta

CACHE = ROOT / '.tools/mainline-completion'
SOURCES = {
    'ArraySlice.cs': '27b787c8a7e50c25d06e3162763e268f29d68687',
    'Clipper.cs': '697cb357ba9c69d16276700277714e3cefadd314',
    'LightUtility.cs': '0694566496bcd3baec41295612593024ca406b73',
    'PlanarGraph.cs': '26cffbaeefabbc1da41ce204b8a70fa95f66d1c9',
    'Refinery.cs': '0bc72a9d79f5f24a970830d11fd27daa8265ba3e',
    'Smoothen.cs': 'ba04aa9f2fdac27d5f2b7594c7ea5fb29f648763',
    'Tessellator.cs': 'ccc19dd7c7a8c00f0b6e525e1cae27c0d99f4a58',
    'UTess.cs': 'cd083edb4f2cc3dcef08f1822b9bf6df1f7fb95f',
    'LICENSE.md': '280cbef18410c053df4199fa5348005454c958fd',
}
MATH_URL = 'https://packages.unity.com/com.unity.mathematics/-/com.unity.mathematics-1.2.6.tgz'
MATH_SHA = '7e6138f7db1f905ae10e4ce47b568a5c40e6870f887cb418a912a0ce629474ad'
DEST = ROOT / 'unity/Assets/Official/World/Map/11/Resources/Map11/Fog/Meshes'
RECEIPT = ROOT / 'assets/reference/official-map/map11-light-meshes-source-manifest.json'


def download(url):
    return urllib.request.urlopen(urllib.request.Request(url, headers={'User-Agent': 'zjcs-native-light-converter'})).read()


def build(editor, prepare_only=False, map_id=11):
    if map_id not in (11, 12): raise ValueError('Unsupported world light mesh source')
    work = CACHE if map_id == 11 else ROOT / '.tools/map12-fog'
    project = work / 'native-fog-mesh-project/unity'
    destination = ROOT / f'unity/Assets/Official/World/Map/{map_id}/Resources/Map{map_id}/Fog/Meshes'
    receipt = ROOT / f'assets/reference/official-map/map{map_id}-light-meshes-source-manifest.json'
    mesh_input = work / 'fog-mesh-input.json'
    expected = len(json.loads(mesh_input.read_bytes())['regions'])
    tool = project / 'Assets/FogMeshTool'
    tool.mkdir(parents=True, exist_ok=True)
    records = []
    for name, blob in SOURCES.items():
        source = CACHE / 'urp14' / name
        if not source.exists():
            source.parent.mkdir(parents=True, exist_ok=True)
            source.write_bytes(base64.b64decode(json.loads(download('https://api.github.com/repos/Unity-Technologies/Graphics/git/blobs/'+blob))['content']))
        raw = source.read_bytes()
        if hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest() != blob:
            raise ValueError('Pinned Unity Graphics blob mismatch: '+name)
        shutil.copyfile(source, tool / name)
        records.append(dict(repository='Unity-Technologies/Graphics', blob=blob, file=name, sha256=sha256(raw)))
    package = CACHE / 'unity-mathematics-1.2.6.tgz'
    if not package.exists():
        package.write_bytes(download(MATH_URL))
    if sha256(package.read_bytes()) != MATH_SHA:
        raise ValueError('Pinned Unity Mathematics package mismatch')
    # Extract only the runtime assembly and package/license metadata. The official
    # Tests subtree requires NUnit and is unrelated to this Editor asset tool.
    package_root = project / 'Packages/com.unity.mathematics'
    package_root.mkdir(parents=True, exist_ok=True)
    with tarfile.open(package, 'r:gz') as archive:
        for member in archive.getmembers():
            relative = Path(member.name).relative_to('package')
            if relative.parts and relative.parts[0] not in ('Unity.Mathematics', 'package.json', 'LICENSE.md'):
                continue
            target = package_root / relative
            if not target.resolve().is_relative_to(package_root.resolve()):
                raise ValueError('Unexpected Unity package member path')
            if member.isdir():
                target.mkdir(parents=True, exist_ok=True)
            elif member.isfile():
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(archive.extractfile(member).read())
    (project/'Packages/manifest.json').write_bytes(json_bytes(dict(dependencies={
        'com.unity.mathematics':'file:com.unity.mathematics', 'com.unity.modules.jsonserialize':'1.0.0'})))
    (project/'ProjectSettings').mkdir(exist_ok=True)
    shutil.copyfile(ROOT/'unity/ProjectSettings/ProjectVersion.txt', project/'ProjectSettings/ProjectVersion.txt')
    source = Path(__file__).with_name('mainline_fog_mesh_tool.cs')
    shutil.copyfile(source, tool / 'FogMeshGenerator.cs')
    (tool/'FogMeshTool.asmdef').write_bytes(json_bytes(dict(name='Zjcs.NativeFogMeshTool',
        references=['Unity.Mathematics'], includePlatforms=['Editor'], allowUnsafeCode=True)))
    if prepare_only:
        print(project)
        return
    log = work/'native-fog-mesh-project/editor.log'
    quote = lambda value: "'" + str(value).replace("'", "''") + "'"
    arguments = ['-batchmode','-projectPath',str(project),'-executeMethod',
        'Zjcs.FogTool.FogMeshGenerator.Run','-fog-input',str(mesh_input),'-logFile',str(log)]
    command = '. ' + quote(ROOT/'scripts/unity_internal_display.ps1') + '\n'
    command += '$fogProcess = Start-UnityOnInternalDisplay -Editor ' + quote(editor) + ' -ArgumentList @('
    command += ','.join(map(quote, arguments)) + ')\n$fogProcess.WaitForExit()\nexit $fogProcess.ExitCode'
    completed = subprocess.run(['pwsh','-NoProfile','-Command',command], check=False)
    if completed.returncode or f'Native region light meshes generated: {expected}' not in log.read_text(encoding='utf-8'):
        raise RuntimeError('Native light mesh generation failed: '+str(log))
    paths = sorted((project/'Assets/FogGenerated').glob('*.asset'))
    if len(paths) != expected:
        raise ValueError('Native region mesh count differs from source')
    destination.mkdir(parents=True, exist_ok=True)
    outputs = []
    for path in paths:
        target = destination/path.name
        shutil.copyfile(path, target)
        write_meta(target, 'NativeFormatImporter')
        meta = Path(str(target)+'.meta')
        text = meta.read_text(encoding='utf-8')
        if 'mainObjectFileID' not in text:
            meta.write_text(text.replace('  externalObjects: {}','  externalObjects: {}\n  mainObjectFileID: 4300000'),encoding='utf-8',newline='\n')
        outputs.append(dict(path=target.relative_to(ROOT).as_posix(), sha256=sha256(target.read_bytes())))
    write_meta(destination, 'DefaultImporter')
    receipt.write_bytes(json_bytes(dict(manifest='871_156598' if map_id == 12 else '854_156383', editor='2022.3.57f1c2', unityGraphics=records,
        mathematics=dict(url=MATH_URL,sha256=MATH_SHA),
        shapeInput=dict(path=destination.parent.joinpath('region-lights.json').relative_to(ROOT).as_posix(),
            sha256=sha256(destination.parent.joinpath('region-lights.json').read_bytes())),
        meshInputSha256=sha256(mesh_input.read_bytes()),
        operation='Unmodified Unity LightUtility.GenerateShapeMesh, UTess and Clipper; original native shape, falloff and vertex colors.',
        converter=dict(path=Path(__file__).relative_to(ROOT).as_posix(),sha256=sha256(Path(__file__).read_bytes())),
        editorTool=dict(path=source.relative_to(ROOT).as_posix(),sha256=sha256(source.read_bytes())),outputs=outputs)))
    print('Verified native light meshes:',len(outputs))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--editor',default='D:/Tool/Unity/Editor/Unity.exe')
    parser.add_argument('--prepare-only',action='store_true')
    parser.add_argument('--map-id',type=int,choices=(11,12),default=11)
    args = parser.parse_args()
    build(args.editor,args.prepare_only,args.map_id)
