"""Prepare a small isolated Unity project containing the actual progression assemblies."""
import json
import os
from pathlib import Path
import shutil
from validation_assemblies import stage_runtime_modules

ROOT = Path(__file__).resolve().parents[1]

def prepare(work):
    project = work / 'unity'
    assets = project / 'Assets'
    if project.exists():
        raise FileExistsError('Keep the existing partner validation project')
    stage_runtime_modules(project, ROOT / 'unity/Assets/Game/Runtime', ['Zjcs.Offline.Progression'],
                          runtime_destination='Assets', allowed_external={'Unity.Newtonsoft.Json'})
    (assets / 'Editor').mkdir(parents=True)
    shutil.copy2(ROOT / 'scripts/fixtures/partner_gift_transaction_validation.cs', assets / 'Editor/PartnerGiftTransactionValidation.cs')
    shutil.copy2(ROOT / 'scripts/fixtures/partner_roster_transaction_validation.cs', assets / 'Editor/PartnerRosterTransactionValidation.cs')
    (assets / 'Editor/Validation.asmdef').write_text(json.dumps(dict(name='PartnerGiftValidation', includePlatforms=['Editor'],
        references=['Zjcs.Offline.Progression', 'Zjcs.Offline.State', 'Zjcs.Official.Partners', 'Unity.Newtonsoft.Json'])), encoding='utf8')
    dependencies=json.loads((ROOT/'unity/Packages/manifest.json').read_bytes())['dependencies']
    packages=project/'Packages';packages.mkdir()
    (packages/'manifest.json').write_text(json.dumps(dict(dependencies={name:version for name,version in dependencies.items()
        if name.startswith('com.unity.modules.') or name=='com.unity.nuget.newtonsoft-json'})), 'utf8')
    (project / 'ProjectSettings').mkdir()
    shutil.copy2(ROOT / 'unity/ProjectSettings/ProjectVersion.txt', project / 'ProjectSettings/ProjectVersion.txt')
    return project

if __name__ == '__main__':
    work = Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()
    if not work.is_relative_to(ROOT / 'output/validation'):
        raise ValueError('An owned repository validation workspace is required')
    print(prepare(work))
