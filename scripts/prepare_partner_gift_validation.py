"""Prepare a small isolated Unity project containing the actual progression assemblies."""
import json
import os
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[1]

def prepare(work):
    project = work / 'unity'
    assets = project / 'Assets'
    modules = ('Offline/Progression', 'Offline/Equipment', 'Offline/Skills', 'Offline/State',
               'Official/SkillRules', 'Official/Partners')
    for module in modules:
        source = ROOT / 'unity/Assets/Game/Runtime' / module
        for path in source.rglob('*'):
            if path.is_file() and (path.suffix in ('.cs', '.asmdef', '.meta')):
                destination = assets / module / path.relative_to(source)
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(path, destination)
    (assets / 'Editor').mkdir(parents=True)
    destination = assets / 'Resources/Map14/world-alone-preparation.json'
    destination.parent.mkdir(parents=True)
    shutil.copy2(ROOT / 'unity/Assets/Game/Resources/Map14/world-alone-preparation.json', destination)
    shutil.copy2(ROOT / 'scripts/fixtures/partner_gift_transaction_validation.cs', assets / 'Editor/PartnerGiftTransactionValidation.cs')
    (assets / 'Editor/Validation.asmdef').write_text(json.dumps(dict(name='PartnerGiftValidation', includePlatforms=['Editor'],
        references=['Zjcs.Offline.Progression', 'Zjcs.Offline.State'])), encoding='utf8')
    (project / 'Packages').mkdir()
    (project / 'Packages/manifest.json').write_text('{"dependencies":{"com.unity.modules.jsonserialize":"1.0.0","com.unity.modules.animation":"1.0.0","com.unity.modules.audio":"1.0.0"}}', encoding='utf8')
    (project / 'ProjectSettings').mkdir()
    shutil.copy2(ROOT / 'unity/ProjectSettings/ProjectVersion.txt', project / 'ProjectSettings/ProjectVersion.txt')
    return project

if __name__ == '__main__':
    work = Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()
    if not work.is_relative_to(ROOT / 'output/validation'):
        raise ValueError('An owned repository validation workspace is required')
    print(prepare(work))
