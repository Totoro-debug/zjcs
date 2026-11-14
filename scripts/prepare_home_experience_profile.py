"""Record and stage actual-profile native home experience composition."""
import argparse
import json
from pathlib import Path
import shutil

from official_apk import ROOT
from prepare_home_experience_rules import CODE, metadata, sha
from validation_workspace import require_temporary_unity_project


def build():
    inputs, outputs = {}, {}
    receipt = ROOT/'assets/reference/home-experience-rules-source-manifest.json'
    proof = json.loads(receipt.read_bytes())
    assert proof['manifest'] == '884_156692'
    assert proof['generator_sha256'] == sha(ROOT/'scripts/prepare_home_experience_rules.py')
    inputs[receipt.relative_to(ROOT).as_posix()] = sha(receipt)
    for group in ('inputs','outputs'):
        for relative, digest in proof[group].items():
            assert sha(ROOT/relative) == digest, relative
            if group == 'outputs': inputs[relative] = digest
    for relative in ('common-source/MsgPlayerBlessLevelUpdate.cs', 'common-source/ConsoleDataManager.cs',
                     'common-source/Common/PlayerBlessLevelUtils.cs', 'common-source/Common/Player.cs',
                     'common-source/Common/SubRank.cs', 'common-source/Common/Rank.cs'):
        p = CODE/relative; inputs[p.relative_to(ROOT).as_posix()] = sha(p)
    for relative in ('assets/reference/home-experience-rewards-source-manifest.json',
                     'assets/reference/home-avatar-native-source-manifest.json',
                     'scripts/fixtures/home_experience_profile_validation.cs',
                     'unity/Assets/Game/Runtime/Offline/CharacterCreation/CharacterCreationOfflineService.cs',
                     'unity/Assets/Game/Runtime/Official/HomeHudUI/HomePlayerExpSlider.cs',
                     'unity/Assets/Game/Runtime/Official/HomeHudUI/IHomeExperienceServices.cs',
                     'unity/Assets/Game/Runtime/Official/HomeExperienceRewards/HomeExperienceRewardBindings.cs'):
        p = ROOT/relative; inputs[relative] = sha(p)
    folder = ROOT/'unity/Assets/Game/Runtime/Offline/HomeExperienceUI'
    files = [*folder.glob('*.cs'), *folder.glob('*.asmdef')]
    for p in files:
        for output in (p, metadata(p)): outputs[output.relative_to(ROOT).as_posix()] = sha(output)
    p = metadata(folder, True); outputs[p.relative_to(ROOT).as_posix()] = sha(p)
    proof = dict(manifest='884_156692', generator_sha256=sha(Path(__file__)), inputs=inputs, outputs=outputs,
        offline_initial_rank=dict(subrank=1, rank=1, decision='User-approved beginner initialization for newly created characters only. No old-save migration.'),
        scope='Actual committed profile XP and blessing fields drive selected original query/parser code, native slider and original reward handler. New character creation persists the approved beginner rank, then the native query resolves its original level cap. External season/world/function-173/non-battle-property context remains explicitly provided by the owner. Read projection never rewrites existing canonical character progression. Formal HomeFeatureRuntime composition is pending.')
    target = ROOT/'assets/reference/home-experience-profile-source-manifest.json'
    target.write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
    return proof


def stage(project, proof):
    project = require_temporary_unity_project(project)
    rules = json.loads((ROOT/'assets/reference/home-experience-rules-source-manifest.json').read_bytes())
    for relative, digest in {**rules['outputs'], **proof['outputs']}.items():
        assert sha(ROOT/relative) == digest, relative
        source = ROOT/relative; target = project/source.relative_to(ROOT/'unity')
        target.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(source,target)
    relative = Path('Assets/Game/Runtime/Offline/CharacterCreation/CharacterCreationOfflineService.cs')
    shutil.copy2(ROOT/'unity'/relative, project/relative)
    folder = project/'Assets/HomeExperienceProfileValidation'; folder.mkdir(exist_ok=True)
    shutil.copy2(ROOT/'scripts/fixtures/home_experience_profile_validation.cs',folder/'HomeExperienceProfileValidation.cs')
    (folder/'HomeExperienceProfileValidation.asmdef').write_text(json.dumps(dict(name='Zjcs.HomeExperienceProfile.Validation',
        references=['Zjcs.Offline.HomeExperienceUI','Zjcs.Offline.Progression','Zjcs.Offline.State','Zjcs.Offline.CharacterCreation','Zjcs.Official.HomeExperienceRules',
                    'Zjcs.Official.HomeExperienceRewards','Zjcs.Official.HomeHudUI','Zjcs.Official.ProfessionUI',
                    'UnityEngine.UI','Unity.RenderPipelines.Universal.Runtime','Unity.RenderPipelines.Core.Runtime'],
        includePlatforms=['Editor']),indent=2)+'\n','utf8',newline='\n')
    print('Staged actual-profile experience composition in',project,flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__); parser.add_argument('--project',type=Path); args=parser.parse_args()
    proof = build()
    if args.project: stage(args.project,proof)
