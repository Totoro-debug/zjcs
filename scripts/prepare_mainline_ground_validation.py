"""Stage the common ground Interface and original dependency closure in an owned project."""
from concurrent.futures import ThreadPoolExecutor
import json
import os
from pathlib import Path
import re
import shutil
import subprocess

from official_apk import ROOT
from validation_assemblies import stage_runtime_modules
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    if not (work / '.zjcs-validation.json').is_file():
        raise ValueError('Owned validation workspace required')
    project = work / 'native-ground-unity'
    if project.exists():
        raise ValueError('Preserve the existing isolated ground project')
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    fixture = ROOT / 'unity/Assets/Game/Editor/MainlineNativeGround'
    pending = json.loads((fixture / 'Zjcs.Editor.MainlineNativeGround.asmdef').read_bytes())['references']
    labels = os.environ.get('ZJCS_NATIVE_LABEL_CONTEXT') == '1'
    host_fixture = ROOT / 'unity/Assets/Game/Editor/Map13NativeWorld'
    if labels:
        pending += json.loads((host_fixture / 'Zjcs.Editor.Map13NativeWorld.asmdef').read_bytes())['references']
    pending += ['Zjcs.Official.World.NativeWater', 'Zjcs.Official.World.NativeText']
    selected = stage_runtime_modules(
        project, runtime, pending,
        allowed_external={'Unity.Newtonsoft.Json', 'Unity.ugui', 'Unity.TextMeshPro', 'UniTask', 'Unity.RenderPipelines.Universal.Runtime', 'Unity.RenderPipelines.Core.Runtime'})
    shutil.copytree(fixture, project / 'Assets/Game/Editor/MainlineNativeGround')
    if labels:
        shutil.copytree(host_fixture, project / 'Assets/Game/Editor/Map13NativeWorld')
    source = ROOT / 'unity'
    files = subprocess.check_output(['git', 'ls-files', '-co', '--exclude-standard', '-z', '--', 'unity/Assets'], cwd=ROOT).split(b'\0')
    metas = [ROOT / name.decode('utf8') for name in set(files) if name.endswith(b'.meta')]

    def identity(meta):
        path = Path(str(meta)[:-5])
        if not path.is_file():
            return None
        match = re.search(rb'(?m)^guid: ([0-9a-f]{32})', meta.read_bytes())
        return (match[1], path) if match else None

    with ThreadPoolExecutor(max_workers=16) as pool:
        index = dict(row for row in pool.map(identity, metas) if row)
    print('Indexed installed asset identities:', len(index), flush=True)
    package_guids = set()
    for folder in (source / 'Packages', source / 'Library/PackageCache'):
        for package in folder.iterdir():
            if package.is_dir() and package.name.startswith(('com.unity.textmeshpro', 'com.unity.ugui', 'com.unity.render-pipelines.', 'com.unity.shadergraph')):
                for meta in package.rglob('*.meta'):
                    match = re.search(rb'(?m)^guid: ([0-9a-f]{32})', meta.read_bytes())
                    if match:
                        package_guids.add(match[1])
    shared = (source / 'Assets/Game/Resources/Map12/Native/world-assets.asset').read_bytes()
    pending = [index[re.search(rb'(?m)^  ' + name + rb':.*guid: ([a-f0-9]{32})', shared)[1]]
        for name in (b'Pipeline', b'EnvironmentData', b'Font')]
    pending.append(source / 'Assets/Game/Resources/Map14/world-alone-preparation.json')
    # Package TextMeshPro.Awake waits for this Resources asset in the editor;
    # a serialized text component alone otherwise never generates its mesh.
    pending.append(source / 'Assets/TextMesh Pro/Resources/TMP Settings.asset')
    if labels:
        pending.extend(source / ('Assets/Game/Resources/Map13/Native/native-' + name + '-catalog.asset')
            for name in ('terrain', 'scene', 'environment-scene', 'region-labels', 'render-pipeline', 'text'))
        pending.append(source / 'Assets/Game/Resources/Map13/Native/map-env-data.asset')
        pending.append(source / 'Assets/Game/Resources/Map13/Native/combat-rating-config.asset')
        pending.extend((source / 'Assets/Game/Resources/Map13/Native').glob('*.json'))
        # Original label data is installed under separate native Resources roots.
        pending.extend(source.glob('Assets/Official/World/Map/13/**/Resources/Map13/Native/region-label-data.json'))
        shutil.copytree(source / 'Assets/StreamingAssets/Map13/NativeText',
            project / 'Assets/StreamingAssets/Map13/NativeText')
    if labels:
        pending.append(source / 'Assets/Game/Resources/Map16/Native/region-label-data.json')
    else:
        for map_id in (10, 11, 12, 13, 14, 16):
            pending.append(source / f'Assets/Official/World/Map/{map_id}/NativeMiniMapCurrent/Resources/Map{map_id}/Native/mini-map-regions.asset')
        for map_id in (15, 16):
            pending.append(source / f'Assets/Official/World/Map/{map_id}/NativeCurrent/Resources/Map{map_id}/Native/current-ground.prefab')
            pending.extend((source / f'Assets/Game/Resources/Map{map_id}/Native').glob('*.json'))
    visited = set()
    while pending:
        asset = pending.pop()
        if asset in visited:
            continue
        visited.add(asset)
        if asset.suffix in ('.cs', '.asmdef'):
            target = project / asset.relative_to(source)
            if not target.is_file():
                raise ValueError('Original asset needs an unstaged script assembly: ' + str(asset))
            continue
        for path in (asset, Path(str(asset) + '.meta')):
            target = project / path.relative_to(source)
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)
        raw = asset.read_bytes()
        if asset.suffix in ('.shader', '.cginc'):
            for name in re.findall(rb'#include\s+"([^"\r\n]+)"', raw):
                include = asset.parent / name.decode('utf8')
                if include.is_file():
                    pending.append(include)
                elif name.startswith(b'TMPro_'):
                    raise ValueError('Missing original TMP shader include: ' + str(include))
        if raw.startswith(b'%YAML'):
            for guid in set(re.findall(rb'guid: ([a-f0-9]{32})', raw)):
                if guid.startswith(b'000000') or guid in package_guids:
                    continue
                if guid not in index:
                    raise ValueError('Unbound original ground dependency: ' + guid.decode() + ' in ' + str(asset))
                pending.append(index[guid])
    shutil.copytree(source / 'Packages', project / 'Packages')
    settings = project / 'ProjectSettings'
    settings.mkdir()
    for name in ('ProjectVersion.txt', 'ProjectSettings.asset', 'TagManager.asset'):
        shutil.copy2(source / 'ProjectSettings' / name, settings / name)
    print('Prepared common ground validation:', len(selected), 'runtime assemblies,', len(visited), 'original assets;', project, flush=True)


if __name__ == '__main__':
    main()
