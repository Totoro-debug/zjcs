"""Prepare the real persisted main-player projection without copying render textures."""
import json
from pathlib import Path
import shutil

from official_apk import ROOT
from prepare_partner_star_god_validation import copy_modules
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    if not (work / '.zjcs-validation.json').is_file():
        raise ValueError('Owned validation workspace required')
    project = work / 'unity'
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    modules = {json.loads(path.read_bytes())['name']: path for path in runtime.rglob('*.asmdef')}
    selected, pending = set(), ['Zjcs.Offline.PartnersUI']
    while pending:
        name = pending.pop()
        if name in selected or name not in modules:
            continue
        selected.add(name)
        pending.extend(json.loads(modules[name].read_bytes()).get('references', []))
    copy_modules(project, runtime, modules, selected)
    editor = project / 'Assets/Editor'
    editor.mkdir(parents=True, exist_ok=True)
    shutil.copy2(ROOT / 'scripts/fixtures/partner_main_player_validation.cs', editor / 'PartnerMainPlayerValidation.cs')
    (editor / 'Validation.asmdef').write_text(json.dumps(dict(name='PartnerMainPlayerValidation',
        includePlatforms=['Editor'], references=sorted(selected | {'Unity.ugui', 'Unity.TextMeshPro'}))), 'utf8', newline='\n')
    base = ROOT / 'unity/Assets/Official/Combat/SceneCharacterAppearance/Resources/Combat/SceneAppearance'
    for name in ('AppearanceConfig.asset', 'AppearanceAnimNameCombineRule.asset', 'appearance-tables.json', 'skin-defaults.json'):
        for source in (base / name, base / (name + '.meta')):
            target = project / source.relative_to(ROOT / 'unity')
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
    source = ROOT / 'unity/Assets/Game/Resources/Map14/world-alone-preparation.json'
    target = project / source.relative_to(ROOT / 'unity')
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)
    # The native label assembly retains the original MessagePack data declarations.
    source = ROOT / 'unity/Assets/Official/World/Map/14/SharedSerialization/MessagePack.dll'
    for path in (source, Path(str(source) + '.meta')):
        target = project / path.relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, target)
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
    print('Prepared actual main-player projection and', len(selected), 'runtime modules:', project, flush=True)


if __name__ == '__main__':
    main()
