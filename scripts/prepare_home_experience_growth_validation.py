"""Stage local experience transactions with the selected original query code."""
import hashlib
import json
from pathlib import Path
import shutil
from validation_assemblies import stage_runtime_modules
from validation_workspace import validation_directory

ROOT = Path(__file__).resolve().parents[1]


def prepare():
    work = validation_directory()
    project = work/'unity'
    stage_runtime_modules(project, ROOT/'unity/Assets/Game/Runtime',
                          ['Zjcs.Offline.Progression', 'Zjcs.Official.HomeExperienceRules'])
    folder = project/'Assets/ExperienceGrowth'; folder.mkdir(exist_ok=True)
    support = ROOT/'unity/Assets/Game/Runtime/Offline/HomeExperienceUI'
    for name in ('OfflineHomeExperienceProjection', 'OfflineHomeExperienceEnvironmentSource', 'OfflineHomeExperienceConfiguration'):
        shutil.copy2(support/(name+'.cs'), folder/(name+'.cs'))
    source = (support/'OfflineHomeExperiencePlayer.cs').read_text('utf8')
    start = source.index('    public sealed class OfflineHomeExperienceEnvironment\n')
    end = source.index('    // A committed profile projection.', start)
    (folder/'Environment.cs').write_text('using Zjcs.Official.HomeExperienceRules;\nnamespace Zjcs.Offline.HomeExperienceUI {\n'+source[start:end]+'}\n', 'utf8')
    shutil.copy2(ROOT/'scripts/fixtures/home_experience_growth_validation.cs', folder/'HomeExperienceGrowthValidation.cs')
    (folder/'ExperienceGrowth.asmdef').write_text(json.dumps(dict(name='Zjcs.Validation.ExperienceGrowth', references=[
        'Zjcs.Offline.Progression','Zjcs.Offline.State','Zjcs.Official.HomeExperienceRules','Unity.Newtonsoft.Json'], includePlatforms=['Editor'])),'utf8')
    for relative in ('Assets/Game/Resources/Home/experience-rules.json','ProjectSettings/ProjectVersion.txt'):
        path = project/relative; path.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(ROOT/'unity'/relative,path)
    packages=project/'Packages';packages.mkdir(exist_ok=True)
    (packages/'manifest.json').write_text(json.dumps(dict(dependencies={
        'com.unity.nuget.newtonsoft-json':'3.2.1','com.unity.modules.jsonserialize':'1.0.0',
        'com.unity.modules.animation':'1.0.0','com.unity.modules.audio':'1.0.0',
        'com.unity.modules.physics2d':'1.0.0','com.unity.modules.physics':'1.0.0'})),'utf8')
    files=[ROOT/'scripts/fixtures/home_experience_growth_validation.cs',Path(__file__),
        ROOT/'unity/Assets/Game/Runtime/Offline/Progression/CharacterExperienceState.cs',
        ROOT/'unity/Assets/Game/Runtime/Offline/Progression/CharacterProgressionInputs.cs',
        ROOT/'unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterProgressionService.cs',
        ROOT/'unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.Experience.cs',
        ROOT/'unity/Assets/Game/Runtime/Presentation/Home/HomeFeatureRuntime.cs',
        *[support/(name+'.cs') for name in ('OfflineHomeExperienceProjection','OfflineHomeExperiencePlayer')]]
    proof=dict(manifest='884_156692',runtime_bindings={p.relative_to(ROOT).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in files})
    proof['inputs']={p:hashlib.sha256((ROOT/p).read_bytes()).hexdigest() for p in (
        'assets/reference/home-experience-rules-source-manifest.json',
        'unity/Assets/Game/Runtime/Official/HomeExperienceRules/HomeExperiencePlayer.cs',
        'unity/Assets/Game/Runtime/Official/HomeExperienceRules/PlayerBlessLevelUtils.cs',
        'unity/Assets/Game/Resources/Home/experience-rules.json')}
    proof['scope']='Formal home growth uses the selected original query state on transaction candidates; existing local award allocation, receipt idempotency and rollback remain local adapters. Other character-page experience consumers and formal HUD reward playback are pending.'
    (ROOT/'assets/reference/home-experience-growth-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n','utf8')
    print('Staged actual local experience transactions in',project,flush=True)


if __name__=='__main__': prepare()
