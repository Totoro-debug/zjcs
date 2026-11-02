"""Prepare real current portrait resources and runtime dependencies in an owned project."""
import hashlib
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys

from official_apk import ROOT
from prepare_partner_star_god_validation import copy_modules
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    project = work / 'unity'
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    modules = {json.loads(p.read_bytes())['name']: p for p in runtime.rglob('*.asmdef')}
    pending, selected = ['Zjcs.Offline.ProfessionUI'], set()
    while pending:
        name = pending.pop()
        if name in selected or name not in modules: continue
        selected.add(name); pending.extend(json.loads(modules[name].read_bytes()).get('references', []))
    copy_modules(project, runtime, modules, selected)
    print('Copied', len(selected), 'runtime assemblies', flush=True)
    editor = project / 'Assets/Editor'; editor.mkdir(parents=True, exist_ok=True)
    shutil.copy2(ROOT / 'scripts/fixtures/partner_player_portrait_validation.cs', editor / 'PartnerPlayerPortraitValidation.cs')
    (editor / 'Validation.asmdef').write_text(json.dumps(dict(name='PartnerPlayerPortraitValidation', includePlatforms=['Editor'],
        references=sorted(selected | {'Unity.ugui', 'Unity.TextMeshPro', 'Unity.RenderPipelines.Universal.Runtime', 'Unity.RenderPipelines.Core.Runtime'}))), 'utf8', newline='\n')
    receipt = json.loads((ROOT / 'assets/reference/partner-player-portraits-source-manifest.json').read_bytes())
    if '--sync' in sys.argv:
        copied_path = work / 'portrait-validation-inputs.json'
        copied = json.loads(copied_path.read_bytes())
        for relative, expected in receipt['installed_output_sha256'].items():
            source = ROOT / relative
            if hashlib.sha256(source.read_bytes()).hexdigest() != expected:
                raise ValueError('Original portrait changed: ' + relative)
            target = project / source.relative_to(ROOT / 'unity')
            target.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(source, target)
            copied[relative] = expected
        copied_path.write_text(json.dumps(copied, indent=2) + '\n', 'utf8', newline='\n')
        print('Refreshed owned portrait runtime/assets and capture fixture', flush=True)
        return
    paths = subprocess.run(['git', 'ls-files', '-z', 'unity/Assets'], cwd=ROOT, check=True, capture_output=True).stdout
    files = {p.decode() for p in paths.split(b'\0') if p}
    files.update(receipt['installed_output_sha256'])
    index, resources = {}, {}
    for relative in files:
        if relative.endswith('.meta'):
            path = ROOT / relative
            if not path.is_file() or not Path(str(path)[:-5]).is_file(): continue
            key = re.search(rb'(?m)^guid: ([0-9a-f]{32})', path.read_bytes())
            if key: index[key[1]] = Path(str(path)[:-5])
        elif '/Resources/' in relative:
            resource = relative.split('/Resources/', 1)[1].rsplit('.', 1)[0]
            resources[resource] = None if resource in resources else ROOT / relative
    print('Indexed installed native dependencies', len(index), flush=True)
    pending = [resources[name] for name in ('Partners/OfficialPartnerPlayerPortraitResources',
        'ProfessionUI/scene-weapon-sources', 'Combat/SceneAppearance/appearance-tables',
        'Combat/SceneAppearance/skin-defaults', 'Combat/SceneAppearance/AppearanceAnimNameCombineRule')]
    characters = ROOT / 'unity/Assets/Game/Resources/Combat/SceneCharacters/appearance-resources.asset'
    for resource in re.findall(r'Resource: ([^}\s]+)', characters.read_text('utf8')):
        pending.append(resources[resource])
    # Runtime texture pages are addressed by GUID-to-Resources strings.
    for relative, path in resources.items():
        if relative.startswith('Combat/SceneCharacters/Textures/'):
            pending.append(path)
    visited, copied = set(), {}
    while pending:
        source = pending.pop()
        if source in visited: continue
        visited.add(source)
        if source.suffix in ('.cs', '.asmdef'): continue
        for path in (source, Path(str(source) + '.meta')):
            target = project / path.relative_to(ROOT / 'unity')
            target.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(path, target)
            copied[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
        raw = source.read_bytes()
        if raw.startswith(b'%YAML'):
            for key in set(re.findall(rb'guid: ([0-9a-f]{32})', raw)):
                if key.startswith(b'000000') or key not in index: continue
                pending.append(index[key])
    packages = project / 'Packages'; packages.mkdir(exist_ok=True)
    dependencies = {'com.unity.ugui': '1.0.0', 'com.unity.textmeshpro': '3.0.6'}
    for name in ('com.cysharp.unitask', 'com.unity.render-pipelines.universal'):
        shutil.copytree(ROOT / 'unity/Packages' / name, packages / name, dirs_exist_ok=True)
        dependencies[name] = 'file:' + name
    for name in ('ui', 'imgui', 'animation', 'audio', 'jsonserialize', 'imageconversion', 'screencapture',
                 'director', 'assetbundle', 'physics', 'physics2d', 'particlesystem'):
        dependencies['com.unity.modules.' + name] = '1.0.0'
    (packages / 'manifest.json').write_text(json.dumps(dict(dependencies=dependencies)), 'utf8', newline='\n')
    settings = project / 'ProjectSettings'; settings.mkdir(exist_ok=True)
    for name in ('ProjectVersion.txt', 'TagManager.asset'):
        shutil.copy2(ROOT / 'unity/ProjectSettings' / name, settings / name)
    (work / 'portrait-validation-inputs.json').write_text(json.dumps(copied, indent=2) + '\n', 'utf8', newline='\n')
    print('Prepared original portrait studio and', len(copied), 'native dependency files:', project, flush=True)


if __name__ == '__main__': main()
