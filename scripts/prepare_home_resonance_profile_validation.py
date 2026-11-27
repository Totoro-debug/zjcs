"""Stage the real resonance property source and durable profile against selected inputs."""
import hashlib
import json
from pathlib import Path
import shutil
from validation_assemblies import stage_runtime_modules
from validation_workspace import validation_directory
from official_source import OfficialSource

ROOT = Path(__file__).resolve().parents[1]


def prepare():
    project = validation_directory() / 'unity'
    stage_runtime_modules(project, ROOT / 'unity/Assets/Game/Runtime', ['Zjcs.Offline.Progression'])
    folder = project / 'Assets/ResonanceValidation'; folder.mkdir(exist_ok=True)
    for name in ('OfflineHomeNoBlessConfiguration', 'OfflineHomeNoBlessPlayerView'):
        shutil.copy2(ROOT / f'unity/Assets/Game/Runtime/Offline/HomePlayerHealthUI/{name}.cs', folder / f'{name}.cs')
    shutil.copy2(ROOT / 'scripts/fixtures/home_resonance_profile_validation.cs', folder / 'HomeResonanceProfileValidation.cs')
    # Reuse only the generic muted Play lifecycle, not its experience assertions.
    lifecycle = (ROOT / 'scripts/fixtures/home_experience_growth_validation.cs').read_text('utf8')
    lifecycle = 'using System;\nusing UnityEditor;\nusing UnityEditor.SceneManagement;\nusing UnityEngine;\n' + lifecycle[lifecycle.index('[InitializeOnLoad]'):]
    lifecycle = lifecycle.replace('HomeExperienceGrowth', 'HomeResonanceProfile').replace('ActualExperienceTransactions', 'ActualResonanceTransactions')
    (folder / 'HomeResonanceProfilePlayValidation.cs').write_text(lifecycle, 'utf8')
    (folder / 'ResonanceValidation.asmdef').write_text(json.dumps(dict(name='Zjcs.Validation.HomeResonance', references=[
        'Zjcs.Offline.Progression', 'Zjcs.Offline.State', 'Zjcs.Offline.Equipment', 'Zjcs.Offline.Skills',
        'Zjcs.Official.HomeExperienceRules', 'Zjcs.Official.HomeNoBlessRules', 'Unity.Newtonsoft.Json'], includePlatforms=['Editor'])), 'utf8')
    for relative in ('Assets/Game/Resources/Home/no-bless-properties.json','ProjectSettings/ProjectVersion.txt'):
        target=project/relative; target.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(ROOT/'unity'/relative,target)
    packages=project/'Packages'; packages.mkdir(exist_ok=True)
    (packages/'manifest.json').write_text(json.dumps(dict(dependencies={
        'com.unity.nuget.newtonsoft-json':'3.2.1','com.unity.modules.jsonserialize':'1.0.0',
        'com.unity.modules.assetbundle':'1.0.0',
        'com.unity.modules.animation':'1.0.0','com.unity.modules.audio':'1.0.0',
        'com.unity.modules.physics2d':'1.0.0','com.unity.modules.physics':'1.0.0'})), 'utf8')
    code=OfficialSource().prepared_path/'official-code/common-source/Common'
    paths=[
        (code/'PlayerData.cs').relative_to(ROOT).as_posix(),
        (code/'EquipResonanceInfoParser.cs').relative_to(ROOT).as_posix(),
        'unity/Assets/Official/Equipment/Runtime/equipment-runtime-rules.json',
        'unity/Assets/Game/Resources/Home/no-bless-properties.json',
        'unity/Assets/Game/Runtime/Official/HomeNoBlessRules/EquipResonanceInfoParser.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineMainlineProgressSession.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineEquipmentPropSource.cs',
        'unity/Assets/Game/Runtime/Offline/Progression/Zjcs.Offline.Progression.asmdef',
        'unity/Assets/Game/Runtime/Offline/HomePlayerHealthUI/OfflineHomeNoBlessPlayerView.cs',
        'scripts/fixtures/home_resonance_profile_validation.cs',
        'scripts/fixtures/home_experience_growth_validation.cs',
        'scripts/prepare_home_resonance_profile_validation.py']
    receipt=dict(scope='Reuse the recovered original parser with each catalog\'s own selected rows; persist the original activated field separately from equipment eligibility. No activation UI or server activation transition is supplied.',
                 equipment_manifest=json.loads((ROOT/'assets/reference/equipment-runtime-source-manifest.json').read_bytes())['manifest'],
                 home_manifest=json.loads((ROOT/'unity/Assets/Game/Resources/Home/no-bless-properties.json').read_bytes())['manifest'],
                 sha256={path:hashlib.sha256((ROOT/path).read_bytes()).hexdigest() for path in paths})
    (ROOT/'assets/reference/home-resonance-profile-source-manifest.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n','utf8')
    print('Staged durable resonance profile and shared property source in',project,flush=True)


if __name__=='__main__': prepare()
