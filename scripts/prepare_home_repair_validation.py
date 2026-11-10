"""Stage actual home repair code and native assets in an owned Unity project."""
import json
import argparse
import os
from pathlib import Path
import re
import shutil

from official_apk import ROOT
from validation_workspace import validation_directory
from validation_assemblies import stage_runtime_modules
from prepare_partner_input_dialog import metadata


def prepare(work, integration=False):
    project = work / ('integration-unity' if integration else 'unity')
    project.mkdir(exist_ok=True)
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    for folder in ('Official/HomeUI', 'Offline/HomeUI'):
        path = runtime / folder
        metadata(path, 'DefaultImporter', True)
        for item in path.iterdir():
            if item.suffix in ('.cs', '.asmdef'):
                metadata(item, 'AssemblyDefinitionImporter' if item.suffix == '.asmdef' else 'MonoImporter')
    metadata(runtime / 'Presentation/Home/HomeHudView.Repair.cs')
    roots = ['Zjcs.Offline.HomeUI', 'Zjcs.Offline.ProfessionUI', 'Zjcs.Official.TeamUI', 'Zjcs.Official.World.NativeText', 'Coffee.UIParticle']
    if integration:
        roots.append('Zjcs.Presentation.Home')
    selected = stage_runtime_modules(project, runtime, roots)
    shutil.copytree(ROOT / 'unity/Assets/Spine/Runtime', project / 'Assets/Spine/Runtime', dirs_exist_ok=True)
    shutil.copytree(ROOT / 'unity/Assets/TextMesh Pro', project / 'Assets/TextMesh Pro', dirs_exist_ok=True)
    packages = project / 'Packages'
    packages.mkdir(exist_ok=True)
    dependencies = {'com.unity.ugui': '1.0.0', 'com.unity.textmeshpro': '3.0.6', 'com.unity.nuget.newtonsoft-json': '3.2.1'}
    package_names = ['com.cysharp.unitask', 'com.unity.render-pipelines.universal', 'com.unity.render-pipelines.core']
    if integration:
        package_names.extend(('com.unity.cinemachine', 'com.unity.timeline'))
    for name in package_names:
        source = ROOT / 'unity/Packages' / name
        if not source.exists():
            matches = list((ROOT / 'unity/Library/PackageCache').glob(name + '@*'))
            if len(matches) != 1:
                raise ValueError('Resolve original project package: ' + name)
            source = matches[0]
        shutil.copytree(source, packages / name, dirs_exist_ok=True, ignore=shutil.ignore_patterns('Tests', 'Tests.meta', 'Documentation~', 'Samples~'))
        dependencies[name] = 'file:' + name
    for module in ('ui', 'imgui', 'animation', 'audio', 'jsonserialize', 'screencapture', 'director',
                   'assetbundle', 'physics', 'physics2d', 'particlesystem', 'imageconversion', 'unitywebrequest',
                   'unitywebrequesttexture', 'unitywebrequestaudio', 'video', 'tilemap'):
        dependencies['com.unity.modules.' + module] = '1.0.0'
    (packages / 'manifest.json').write_text(json.dumps(dict(dependencies=dependencies), indent=2), 'utf8')
    settings = project / 'ProjectSettings'
    settings.mkdir(exist_ok=True)
    shutil.copy2(ROOT / 'unity/ProjectSettings/ProjectVersion.txt', settings)
    # Stage the complete serialized dependency closure, preserving original GUIDs.
    by_guid = {}
    dependencies = set()
    for name in ('home-repair-native-source-manifest.json', 'partner-input-dialog-source-manifest.json',
                 'partner-friendship-window-source-manifest.json'):
        receipt = json.loads((ROOT / 'assets/reference' / name).read_bytes())
        dependencies.update(path for path in receipt['installed_output_sha256'] if not path.endswith('.meta'))
        dependencies.update(row['path'] if isinstance(row, dict) else row for row in receipt.get('reused', []))
        dependencies.update(row['path'] if isinstance(row, dict) else row for row in receipt.get('reused_assets', []))
    for name in ('map13-native-pipeline-source-manifest.json', 'map13-pipeline-shaders-source-manifest.json',
                 'map13-pipeline-textures-source-manifest.json'):
        receipt = json.loads((ROOT / 'assets/reference/official-map' / name).read_bytes())
        dependencies.update(row['path'] for row in receipt['outputs'])
    dependencies.update(path.relative_to(ROOT).as_posix() for path in
        (runtime / 'Official/World/NativeWater/Shaders/Pipeline').glob('*.shader'))
    for relative in dependencies:
        item = ROOT / (relative + '.meta')
        match = re.search(rb'(?m)^guid: ([0-9a-f]{32})', item.read_bytes())
        if match:
            by_guid[match[1].decode()] = Path(str(item)[:-5])
    pending = [ROOT / 'unity/Assets/Game/Resources/Home/OfficialHomeRepairAssetCatalog.asset',
               ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerFriendshipWindowAssetCatalog.asset',
               ROOT / 'unity/Assets/Official/World/Map/13/NativePipeline/UniversalRenderPipeline.asset']
    visited = set()
    while pending:
        source = pending.pop()
        if source in visited:
            continue
        visited.add(source)
        if source.suffix == '.cs':
            continue
        raw = source.read_bytes()
        target = project / source.relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        shutil.copy2(Path(str(source) + '.meta'), Path(str(target) + '.meta'))
        if raw.startswith(b'%YAML'):
            for key in set(re.findall(rb'guid: ([0-9a-f]{32})', raw)):
                if key.decode() in by_guid:
                    pending.append(by_guid[key.decode()])
    font_source = ROOT / 'unity/Assets/Official/World/Map/12/UI/Team/FZY4K.ttf'
    target = project / 'Assets/Resources/HomeValidationFont.ttf'
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(font_source, target)
    for relative in ('Assets/Official/Partners/partner-property-ui.json',):
        source = ROOT / 'unity' / relative
        target = project / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        shutil.copy2(Path(str(source) + '.meta'), Path(str(target) + '.meta'))
    editor = project / 'Assets/Editor'
    editor.mkdir(exist_ok=True)
    shutil.copy2(ROOT / 'scripts/fixtures/home_repair_native_validation.cs', editor / 'HomeRepairNativeValidation.cs')
    if not integration:
        shutil.copy2(runtime / 'Presentation/World/OfficialDungeonPopupMask.cs', editor / 'OfficialDungeonPopupMask.cs')
    fixture_references = {'Zjcs.Presentation.World'} if integration else set()
    (editor / 'Validation.asmdef').write_text(json.dumps(dict(name='HomeRepairNativeValidation', includePlatforms=['Editor'],
        references=sorted(set(roots) | fixture_references | {'UnityEngine.UI', 'Unity.TextMeshPro', 'Zjcs.Official.HomeUI',
            'Zjcs.Official.ProfessionUI', 'Zjcs.Official.WorldUI', 'Zjcs.Official.World.NativeLabelUI', 'Zjcs.Offline.PartnersUI',
            'Unity.RenderPipelines.Core.Runtime', 'Unity.RenderPipelines.Universal.Runtime', 'spine-unity', 'spine-csharp'}))), 'utf8')
    print('Staged', len(selected), 'runtime modules and', len(visited), 'native dependency assets', flush=True)
    return project


if __name__ == '__main__':
    if Path(os.environ['ZJCS_REPOSITORY_ROOT']).resolve() != ROOT:
        raise ValueError('Repository validation context required')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--integration', action='store_true')
    print(prepare(validation_directory(), parser.parse_args().integration))
