"""Prepare an isolated project compiling actual partner helpers and shared UI code."""
import json
import os
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[1]
WORLD = ('WorldStateSwitcher', 'WorldMultiLanguageText', 'WorldBattleReportTextService',
         'WorldTabGroup', 'WorldTabItem', 'WorldTabTransition')


def prepare(work):
    project = work / 'unity'
    assets = project / 'Assets'
    shutil.copytree(ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI', assets / 'PartnersUI')
    target = assets / 'WorldUI'
    target.mkdir(parents=True)
    for name in WORLD:
        for extension in ('.cs', '.cs.meta'):
            shutil.copy2(ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI' / (name + extension), target)
    (target / 'WorldUI.asmdef').write_text(json.dumps(dict(name='Zjcs.Official.WorldUI',
        references=['Unity.ugui', 'Unity.TextMeshPro'])), 'utf8')
    (assets / 'Editor').mkdir()
    shutil.copy2(ROOT / 'scripts/fixtures/partner_ui_helper_validation.cs', assets / 'Editor/PartnerUiHelperValidation.cs')
    (assets / 'Editor/Validation.asmdef').write_text(json.dumps(dict(name='PartnerUiHelperValidation', includePlatforms=['Editor'],
        references=['Unity.ugui', 'Unity.TextMeshPro', 'Zjcs.Official.WorldUI', 'Zjcs.Official.PartnersUI'])), 'utf8')
    packages = project / 'Packages'
    packages.mkdir()
    dependencies = {'com.unity.ugui': '1.0.0', 'com.unity.textmeshpro': '3.0.6'}
    for module in ('ui', 'imgui', 'animation', 'jsonserialize', 'screencapture'):
        dependencies['com.unity.modules.' + module] = '1.0.0'
    (packages / 'manifest.json').write_text(json.dumps(dict(dependencies=dependencies)), 'utf8')
    settings = project / 'ProjectSettings'
    settings.mkdir()
    shutil.copy2(ROOT / 'unity/ProjectSettings/ProjectVersion.txt', settings)
    return project


if __name__ == '__main__':
    work = Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()
    if Path(os.environ['ZJCS_REPOSITORY_ROOT']).resolve() != ROOT or not work.is_relative_to(ROOT / 'output/validation'):
        raise ValueError('Set the owned validation workspace and repository environment')
    print(prepare(work))
