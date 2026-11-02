"""Stage the common ground Interface and original dependency closure in an owned project."""
from concurrent.futures import ThreadPoolExecutor
import json
from pathlib import Path
import re
import shutil
import subprocess

from official_apk import ROOT
from prepare_partner_star_god_validation import copy_modules
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    if not (work / '.zjcs-validation.json').is_file():
        raise ValueError('Owned validation workspace required')
    project = work / 'native-ground-unity'
    if project.exists():
        raise ValueError('Preserve the existing isolated ground project')
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    modules = {json.loads(p.read_bytes())['name']: p for p in runtime.rglob('*.asmdef')}
    fixture = ROOT / 'unity/Assets/Game/Editor/MainlineNativeGround'
    pending = json.loads((fixture / 'Zjcs.Editor.MainlineNativeGround.asmdef').read_bytes())['references']
    pending += ['Zjcs.Official.World.NativeWater', 'Zjcs.Official.World.NativeText']
    selected, external = set(), set()
    while pending:
        name = pending.pop()
        if name in selected:
            continue
        if name not in modules:
            external.add(name)
            continue
        selected.add(name)
        pending.extend(json.loads(modules[name].read_bytes()).get('references', []))
    if external - {'Unity.ugui', 'Unity.TextMeshPro', 'UniTask', 'Unity.RenderPipelines.Universal.Runtime', 'Unity.RenderPipelines.Core.Runtime'}:
        raise ValueError('Inspect ground validation dependencies: ' + repr(external))
    copy_modules(project, runtime, modules, selected)
    shutil.copytree(fixture, project / 'Assets/Game/Editor/MainlineNativeGround')
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
