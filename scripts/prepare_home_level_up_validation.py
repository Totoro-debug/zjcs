"""Stage selected level-up notification and its full native dependency closure."""
import json
import re
from pathlib import Path
import shutil

from official_apk import ROOT
from validation_assemblies import stage_runtime_modules
from validation_workspace import validation_directory


def prepare(work):
    project = work/'unity'; project.mkdir(exist_ok=True)
    runtime = ROOT/'unity/Assets/Game/Runtime'
    roots = ['Zjcs.Official.HomeLevelUpUI', 'Zjcs.Offline.ProfessionUI', 'Zjcs.Official.World.NativeText', 'Coffee.UIParticle']
    stage_runtime_modules(project, runtime, roots)
    for relative in ('Assets/Spine/Runtime', 'Assets/TextMesh Pro'):
        shutil.copytree(ROOT/'unity'/relative, project/relative, dirs_exist_ok=True)
    packages = project/'Packages'; packages.mkdir(exist_ok=True)
    dependencies = {'com.unity.ugui': '1.0.0', 'com.unity.textmeshpro': '3.0.6', 'com.unity.nuget.newtonsoft-json': '3.2.1'}
    for name in ('com.cysharp.unitask', 'com.unity.render-pipelines.universal', 'com.unity.render-pipelines.core',
                 'com.unity.cinemachine', 'com.unity.timeline'):
        source = ROOT/'unity/Packages'/name
        if not source.exists():
            matches = list((ROOT/'unity/Library/PackageCache').glob(name+'@*'))
            if len(matches) != 1: raise ValueError('Resolve original project package: '+name)
            source = matches[0]
        shutil.copytree(source, packages/name, dirs_exist_ok=True, ignore=shutil.ignore_patterns('Tests', 'Tests.meta', 'Documentation~', 'Samples~'))
        dependencies[name] = 'file:'+name
    for name in ('ui', 'imgui', 'animation', 'audio', 'jsonserialize', 'screencapture', 'director', 'assetbundle',
                 'physics', 'physics2d', 'particlesystem', 'imageconversion', 'unitywebrequest', 'unitywebrequesttexture',
                 'unitywebrequestaudio', 'video', 'tilemap'):
        dependencies['com.unity.modules.'+name] = '1.0.0'
    (packages/'manifest.json').write_text(json.dumps(dict(dependencies=dependencies), indent=2)+'\n', 'utf8', newline='\n')
    settings = project/'ProjectSettings'; settings.mkdir(exist_ok=True)
    for name in ('ProjectVersion.txt', 'TagManager.asset'):
        shutil.copy2(ROOT/'unity/ProjectSettings'/name, settings/name)
    receipt = json.loads((ROOT/'assets/reference/home-level-up-native-source-manifest.json').read_bytes())
    paths = set(receipt['installed_output_sha256']) | {row['path'] for row in receipt['reused']}
    for relative in paths:
        source = ROOT/relative
        if not source.is_file(): continue
        target = project/source.relative_to(ROOT/'unity'); target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        if source.suffix != '.meta': shutil.copy2(Path(str(source)+'.meta'), Path(str(target)+'.meta'))
    by_guid = {}
    for meta in (ROOT/'unity/Assets').rglob('*.meta'):
        source = Path(str(meta)[:-5])
        if source.is_file():
            match = re.search(rb'(?m)^guid: ([0-9a-f]{32})', meta.read_bytes())
            if match: by_guid[match[1]] = source
    pending = [ROOT/'unity'/relative for relative in (
        'Assets/Official/Home/UI/LevelUp/LevelUpNotifyTip.prefab',
        'Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset')]
    visited = set()
    while pending:
        source = pending.pop()
        if source in visited or source.suffix == '.cs': continue
        visited.add(source); raw = source.read_bytes()
        target = project/source.relative_to(ROOT/'unity'); target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target); shutil.copy2(Path(str(source)+'.meta'), Path(str(target)+'.meta'))
        if raw.startswith(b'%YAML'):
            for key in set(re.findall(rb'guid: ([0-9a-f]{32})', raw)):
                if key in by_guid: pending.append(by_guid[key])
    shader = runtime/'Official/World/NativeWater/Shaders/Pipeline'
    shutil.copytree(shader, project/shader.relative_to(ROOT/'unity'), dirs_exist_ok=True)
    for relative in ('Assets/Official/Partners/partner-property-ui.json', 'Assets/Official/ProfessionUI/Fonts/FontAssetFontConfig.json'):
        source = ROOT/'unity'/relative; target = project/relative; target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target); shutil.copy2(Path(str(source)+'.meta'), Path(str(target)+'.meta'))
    target = project/'Assets/Resources/HomeValidationFont.ttf'; target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(ROOT/'unity/Assets/Official/World/Map/12/UI/Team/FZY4K.ttf', target)
    folder = project/'Assets/Editor'
    folder.mkdir(exist_ok=True)
    # This exact path is an intermediate fixture copied by the earlier staging
    # attempt in this owned project; it is not part of the notification test.
    (folder/'HomeRepairNativeValidation.cs').unlink(missing_ok=True)
    shutil.copy2(ROOT/'scripts/fixtures/home_level_up_native_validation.cs', folder/'HomeLevelUpNativeValidation.cs')
    assembly = folder/'Validation.asmdef'
    definition = dict(name='Zjcs.HomeLevelUp.NativeValidation', includePlatforms=['Editor'],
        references=roots+['UnityEngine.UI', 'Unity.TextMeshPro', 'Zjcs.Official.ProfessionUI', 'Zjcs.Official.WorldUI', 'Zjcs.Offline.PartnersUI',
            'Zjcs.Official.World.NativeLabelUI', 'Unity.Newtonsoft.Json', 'Unity.RenderPipelines.Core.Runtime', 'Unity.RenderPipelines.Universal.Runtime'])
    assembly.write_text(json.dumps(definition, indent=2)+'\n', 'utf8', newline='\n')
    print('Staged complete selected level-up notification into', project, flush=True)
    return project


if __name__ == '__main__': prepare(validation_directory())
