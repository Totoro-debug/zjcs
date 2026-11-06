"""Prepare real health persistence and the original avatar widget in an owned project."""
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys

from official_apk import ROOT
from validation_assemblies import stage_runtime_modules
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    if not (work / '.zjcs-validation.json').is_file():
        raise ValueError('Owned validation workspace required')
    project = work / 'health-unity'
    if project.exists() and '--sync' not in sys.argv:
        raise ValueError('Preserve existing validation project')
    receipt = work / 'health-validation-inputs.json'
    previous = json.loads(receipt.read_bytes()) if receipt.exists() else {}
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    pending = ['Zjcs.Offline.PartnersUI', 'Coffee.UIParticle']
    selected = stage_runtime_modules(project, runtime, pending)
    editor = ROOT / 'unity/Assets/Game/Editor/PlayerHealth'
    shutil.copytree(editor, project / 'Assets/Game/Editor/PlayerHealth', dirs_exist_ok=True)
    names = subprocess.run(['git', 'ls-files', '-z', 'unity/Assets'], cwd=ROOT,
                           check=True, capture_output=True).stdout
    metas = [ROOT / path.decode() for path in names.split(b'\0') if path.endswith(b'.meta')]

    def entry(meta):
        path = Path(str(meta)[:-5])
        if not path.is_file():
            return None
        match = re.search(rb'(?m)^guid: ([0-9a-f]{32})', meta.read_bytes())
        return (match[1], path) if match else None

    with ThreadPoolExecutor(max_workers=16) as pool:
        index = dict(value for value in pool.map(entry, metas) if value)
    pending = [ROOT / 'unity/Assets/Official/Partners/UI/Panel/NpcPanel.prefab']
    if 'Zjcs.Official.SharedSerialization' in selected:
        pending.append(ROOT / 'unity/Assets/Official/World/Map/14/SharedSerialization/MessagePack.dll')
    visited, copied, unresolved = set(), {}, set()
    while pending:
        source = pending.pop()
        if source in visited:
            continue
        visited.add(source)
        if source.suffix in ('.cs', '.asmdef'):
            continue
        for path in (source, Path(str(source) + '.meta')):
            target = project / path.relative_to(ROOT / 'unity')
            target.parent.mkdir(parents=True, exist_ok=True)
            if target.exists() and target.read_bytes() != path.read_bytes():
                expected = previous.get(path.relative_to(ROOT).as_posix())
                if hashlib.sha256(target.read_bytes()).hexdigest() != expected:
                    raise ValueError('Preserve modified validation input: ' + str(target))
            shutil.copy2(path, target)
            copied[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
        raw = source.read_bytes()
        if raw.startswith(b'%YAML'):
            for guid in set(re.findall(rb'guid: ([0-9a-f]{32})', raw)):
                if guid in index:
                    pending.append(index[guid])
                elif not guid.startswith(b'000000'):
                    unresolved.add(guid)
    # Every remaining script GUID must resolve to the actual Unity packages.
    for folder in (ROOT / 'unity/Packages', ROOT / 'unity/Library/PackageCache'):
        for package in folder.iterdir():
            if not package.is_dir() or not package.name.startswith(('com.unity.textmeshpro',
                    'com.unity.ugui', 'com.unity.render-pipelines.', 'com.unity.shadergraph')):
                continue
            for meta in package.rglob('*.meta'):
                match = re.search(rb'(?m)^guid: ([0-9a-f]{32})', meta.read_bytes())
                if match:
                    unresolved.discard(match[1])
    if unresolved:
        raise ValueError('Unbound original prefab dependencies: ' + repr(sorted(unresolved)))
    destination = project / 'Assets/Resources/Map14/world-alone-preparation.json'
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(ROOT / 'unity/Assets/Game/Resources/Map14/world-alone-preparation.json', destination)
    packages = project / 'Packages'
    packages.mkdir(exist_ok=True)
    dependencies = {'com.unity.ugui': '1.0.0', 'com.unity.textmeshpro': '3.0.6',
                    'com.unity.nuget.newtonsoft-json': '3.2.1'}
    for name in ('com.cysharp.unitask', 'com.unity.render-pipelines.universal'):
        shutil.copytree(ROOT / 'unity/Packages' / name, packages / name, dirs_exist_ok=True)
        dependencies[name] = 'file:' + name
    for name in ('ui', 'imgui', 'animation', 'audio', 'jsonserialize', 'imageconversion',
                 'screencapture', 'director', 'assetbundle', 'physics', 'physics2d', 'particlesystem'):
        dependencies['com.unity.modules.' + name] = '1.0.0'
    (packages / 'manifest.json').write_text(json.dumps(dict(dependencies=dependencies)), 'utf8', newline='\n')
    settings = project / 'ProjectSettings'
    settings.mkdir(exist_ok=True)
    for name in ('ProjectVersion.txt', 'TagManager.asset'):
        shutil.copy2(ROOT / 'unity/ProjectSettings' / name, settings / name)
    receipt.write_text(json.dumps(copied, indent=2) + '\n', 'utf8', newline='\n')
    print('Prepared', len(selected), 'runtime assemblies and', len(copied), 'original dependency files', flush=True)


if __name__ == '__main__':
    main()
