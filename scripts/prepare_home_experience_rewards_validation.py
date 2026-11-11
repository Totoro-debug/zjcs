"""Stage the original experience reward validation in an owned full project."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil

from official_apk import ROOT
from validation_assemblies import stage_runtime_modules
from validation_workspace import require_temporary_unity_project


def stage(project):
    project = require_temporary_unity_project(project)
    catalog = project/'Assets/Game/Resources/Home/OfficialHomeAvatarAssetCatalog.asset'
    if not catalog.exists():
        raise ValueError('Use a full owned project with the installed original home avatar.')
    receipt = json.loads((ROOT/'assets/reference/home-experience-rewards-source-manifest.json').read_bytes())
    for section in ('inputs', 'outputs'):
        for relative, expected in receipt[section].items():
            if hashlib.sha256((ROOT/relative).read_bytes()).hexdigest() != expected:
                raise ValueError('Original experience reward dependency changed: '+relative)
    stage_runtime_modules(project, ROOT/'unity/Assets/Game/Runtime', ['Zjcs.Official.HomeExperienceRewards'])
    source = ROOT/'unity/Assets/Game/Runtime/Official/HomeExperienceRewards.meta'
    shutil.copy2(source, project/'Assets/Game/Runtime/Official/HomeExperienceRewards.meta')
    target = project/'Assets/HomeExperienceRewardsValidation'
    target.mkdir(parents=True, exist_ok=True)
    shutil.copy2(ROOT/'scripts/fixtures/home_experience_rewards_validation.cs', target/'HomeExperienceRewardsValidation.cs')
    references = ['Zjcs.Official.HomeExperienceRewards', 'Zjcs.Official.HomeHudUI',
                  'Zjcs.Official.ProfessionUI', 'UnityEngine.UI',
                  'Unity.RenderPipelines.Universal.Runtime', 'Unity.RenderPipelines.Core.Runtime']
    (target/'HomeExperienceRewardsValidation.asmdef').write_text(json.dumps(dict(
        name='Zjcs.HomeExperienceRewards.NativeValidation', references=references,
        includePlatforms=['Editor']), indent=2)+'\n', 'utf8', newline='\n')
    print('Staged original experience rewards validation into', project)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--project', type=Path, required=True)
    stage(parser.parse_args().project)
