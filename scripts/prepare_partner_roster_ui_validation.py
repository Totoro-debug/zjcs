"""Compile the actual roster views and complete runtime assembly dependency closure."""
import json
import os
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[1]


def prepare(work):
    project = work / 'unity'
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    modules = {json.loads(path.read_bytes())['name']: path for path in runtime.rglob('*.asmdef')}
    pending = ['Zjcs.Official.PartnerRosterUI', 'Zjcs.Offline.PartnersUI']
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
    if external != {'UniTask', 'Unity.TextMeshPro', 'Unity.ugui', 'UnityEngine.UI'}:
        raise ValueError('Unreviewed validation dependency: ' + repr(external))
    for name in selected:
        folder = modules[name].parent
        for path in folder.rglob('*'):
            if path.is_file() and path.suffix in ('.cs', '.meta', '.asmdef'):
                target = project / 'Assets/Game/Runtime' / path.relative_to(runtime)
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(path, target)
    editor = project / 'Assets/Editor'
    shutil.copytree(ROOT / 'unity/Assets/TextMesh Pro', project / 'Assets/TextMesh Pro')
    editor.mkdir()
    shutil.copy2(ROOT / 'scripts/fixtures/partner_roster_ui_validation.cs', editor / 'PartnerRosterUiValidation.cs')
    (editor / 'Validation.asmdef').write_text(json.dumps(dict(name='PartnerRosterUiValidation', includePlatforms=['Editor'],
        references=['Unity.ugui', 'Unity.TextMeshPro', 'Zjcs.Official.PartnerRosterUI', 'Zjcs.Official.PartnersUI',
                    'Zjcs.Official.Partners', 'Zjcs.Official.ProfessionUI', 'Zjcs.Official.WorldUI', 'Zjcs.Official.SkillsUI',
                    'Zjcs.Offline.PartnersUI', 'Zjcs.Offline.Progression', 'Zjcs.Offline.State'])), 'utf8')
    resources = project / 'Assets/Resources/Map14'
    resources.mkdir(parents=True)
    shutil.copy2(ROOT / 'unity/Assets/Game/Resources/Map14/world-alone-preparation.json', resources)
    shutil.copy2(ROOT / 'unity/Assets/Official/World/Map/12/UI/Team/FZY4K.ttf', editor / 'FZY4K.ttf')
    packages = project / 'Packages'
    packages.mkdir()
    shutil.copytree(ROOT / 'unity/Packages/com.cysharp.unitask', packages / 'com.cysharp.unitask')
    dependencies = {'com.unity.ugui': '1.0.0', 'com.unity.textmeshpro': '3.0.6', 'com.cysharp.unitask': 'file:com.cysharp.unitask'}
    for module in ('ui', 'imgui', 'animation', 'audio', 'jsonserialize', 'screencapture', 'director', 'assetbundle', 'physics', 'physics2d', 'particlesystem'):
        dependencies['com.unity.modules.' + module] = '1.0.0'
    (packages / 'manifest.json').write_text(json.dumps(dict(dependencies=dependencies)), 'utf8')
    settings = project / 'ProjectSettings'
    settings.mkdir()
    shutil.copy2(ROOT / 'unity/ProjectSettings/ProjectVersion.txt', settings)
    print('Copied actual source dependency closure:', len(selected), 'assemblies')
    return project


if __name__ == '__main__':
    work = Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()
    if Path(os.environ['ZJCS_REPOSITORY_ROOT']).resolve() != ROOT or not work.is_relative_to(ROOT / 'output/validation'):
        raise ValueError('An owned repository validation workspace is required')
    print(prepare(work))
