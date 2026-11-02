"""Create an owned Play Mode project for the installed original star-god page."""
import hashlib
import json
from pathlib import Path
import shutil

from official_apk import ROOT
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    if not (work / '.zjcs-validation.json').is_file(): raise ValueError('Owned validation workspace required')
    project = work / 'star-god-unity'
    if project.exists(): raise ValueError('Preserve the existing validation project')
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    modules = {json.loads(path.read_bytes())['name']: path for path in runtime.rglob('*.asmdef')}
    # Native effect components are reached through prefab GUIDs, not C# references.
    pending, selected, external = ['Zjcs.Official.PartnerRosterUI', 'Coffee.UIParticle'], set(), set()
    while pending:
        name = pending.pop()
        if name in selected: continue
        if name not in modules:
            external.add(name)
            continue
        selected.add(name)
        pending.extend(json.loads(modules[name].read_bytes()).get('references', []))
    if external - {'Unity.ugui', 'UnityEngine.UI', 'Unity.TextMeshPro', 'UniTask'}:
        raise ValueError('Inspect validation dependencies: ' + repr(external))
    for name in selected:
        folder = modules[name].parent
        for path in folder.rglob('*'):
            if not path.is_file() or path.suffix not in ('.cs', '.meta', '.asmdef'): continue
            parent = path.parent
            while parent != folder and not list(parent.glob('*.asmdef')): parent = parent.parent
            if parent != folder: continue
            target = project / 'Assets/Game/Runtime' / path.relative_to(runtime)
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(path, target)
    editor = project / 'Assets/Editor'
    editor.mkdir(parents=True)
    shutil.copy2(ROOT / 'scripts/fixtures/partner_star_god_validation.cs', editor / 'PartnerStarGodValidation.cs')
    (editor / 'Validation.asmdef').write_text(json.dumps(dict(name='PartnerStarGodValidation', includePlatforms=['Editor'],
        references=['Unity.ugui', 'Unity.TextMeshPro', 'Unity.RenderPipelines.Universal.Runtime', 'Unity.RenderPipelines.Core.Runtime',
            'Zjcs.Official.PartnerRosterUI', 'Zjcs.Official.Partners', 'Zjcs.Official.WorldUI',
            'Zjcs.Official.PartnersUI', 'Zjcs.Official.ProfessionUI'])), 'utf8', newline='\n')
    installed = json.loads((ROOT / 'assets/reference/partner-star-god-native-source-manifest.json').read_bytes())
    for name, expected in installed['installed_output_sha256'].items():
        source = ROOT / name
        if hashlib.sha256(source.read_bytes()).hexdigest() != expected: raise ValueError('Installed star-god asset changed: ' + name)
        target = editor / source.name if '/Runtime/' in name else project / source.relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    # AkEvent retains its original event ID; route the common click to its
    # already verified original PCM, shared with the native dialogue entry.
    clicks = json.loads((ROOT / 'assets/reference/partner-dialogue-entry-native-source-manifest.json').read_bytes())
    for name, expected in clicks['installed_output_sha256'].items():
        if '/Audio/Main_Cursor_Normal.wav' not in name: continue
        source = ROOT / name
        if hashlib.sha256(source.read_bytes()).hexdigest() != expected: raise ValueError('Original click PCM changed: ' + name)
        target = project / source.relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    (editor / 'star-god-fixture.json').write_text(json.dumps(dict(root=installed['roots']['NpcPanelStarGodContent'],
        **installed['star_god_images']), ensure_ascii=False), 'utf8', newline='\n')
    shutil.copytree(ROOT / 'unity/Assets/TextMesh Pro', project / 'Assets/TextMesh Pro')
    shutil.copy2(ROOT / 'unity/Assets/Official/World/Map/12/UI/Team/FZY4K.ttf', editor / 'FZY4K.ttf')
    packages = project / 'Packages'
    packages.mkdir()
    dependencies = {'com.unity.ugui': '1.0.0', 'com.unity.textmeshpro': '3.0.6'}
    for name in ('com.cysharp.unitask', 'com.unity.render-pipelines.universal'):
        shutil.copytree(ROOT / 'unity/Packages' / name, packages / name)
        dependencies[name] = 'file:' + name
    for name in ('ui', 'imgui', 'animation', 'audio', 'jsonserialize', 'imageconversion', 'screencapture',
                 'director', 'assetbundle', 'physics', 'physics2d', 'particlesystem'):
        dependencies['com.unity.modules.' + name] = '1.0.0'
    (packages / 'manifest.json').write_text(json.dumps(dict(dependencies=dependencies)), 'utf8', newline='\n')
    settings = project / 'ProjectSettings'
    settings.mkdir()
    shutil.copy2(ROOT / 'unity/ProjectSettings/ProjectVersion.txt', settings)
    print('Prepared installed star-god page and', len(selected), 'runtime assemblies:', project, flush=True)


if __name__ == '__main__':
    main()
