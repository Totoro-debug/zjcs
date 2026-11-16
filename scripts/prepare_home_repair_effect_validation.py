"""Stage the original repair effects in a small isolated Unity project."""
import json
from pathlib import Path
import shutil

from official_apk import ROOT
from prepare_partner_card_native import sha
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    if not (work / '.zjcs-validation.json').is_file():
        raise ValueError('Owned validation workspace required')
    project = work / 'effect-unity'
    project.mkdir(exist_ok=True)
    receipt = json.loads((ROOT / 'assets/reference/home-repair-effect-native-source-manifest.json').read_bytes())
    paths = dict(receipt['installed_output_sha256'])
    for name in ('map13-native-pipeline-source-manifest.json', 'map13-pipeline-shaders-source-manifest.json',
                 'map13-pipeline-textures-source-manifest.json'):
        source = json.loads((ROOT / 'assets/reference/official-map' / name).read_bytes())
        for row in source['outputs']:
            paths[row['path']] = row['sha256']
            meta = ROOT / (row['path'] + '.meta')
            if meta.is_file():
                paths[row['path'] + '.meta'] = row.get('meta_sha256', sha(meta.read_bytes()))
    for relative, expected in paths.items():
        source = ROOT / relative
        if not source.is_file():
            continue  # folder metadata is recreated by the editor in this isolated project
        if sha(source.read_bytes()) != expected:
            raise ValueError('Validation asset differs: ' + relative)
        target = project / Path(relative).relative_to('unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    pipeline_shaders = ROOT / 'unity/Assets/Game/Runtime/Official/World/NativeWater/Shaders/Pipeline'
    shutil.copytree(pipeline_shaders, project / 'Assets/Game/Runtime/Official/World/NativeWater/Shaders/Pipeline',
        dirs_exist_ok=True)
    runtime = ROOT / 'unity/Assets/Game/Runtime/Presentation/Home'
    for name in ('HomeRepairEffectAssets.cs', 'HomeRepairEffectPlayer.cs'):
        for suffix in ('', '.meta'):
            source = runtime / (name + suffix)
            target = project / 'Assets/Runtime' / (name + suffix)
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)
    packages = project / 'Packages'
    packages.mkdir(exist_ok=True)
    dependencies = {'com.unity.ugui': '1.0.0', 'com.unity.textmeshpro': '3.0.6'}
    for name in ('com.unity.render-pipelines.universal', 'com.unity.render-pipelines.core'):
        source = ROOT / 'unity/Packages' / name
        if not source.exists():
            source, = (ROOT / 'unity/Library/PackageCache').glob(name + '@*')
        shutil.copytree(source, packages / name, dirs_exist_ok=True,
            ignore=shutil.ignore_patterns('Tests', 'Tests.meta', 'Documentation~', 'Samples~'))
        dependencies[name] = 'file:' + name
    for name in ('ui', 'imgui', 'animation', 'audio', 'jsonserialize', 'screencapture',
                 'assetbundle', 'physics', 'physics2d', 'particlesystem', 'imageconversion', 'unitywebrequest'):
        dependencies['com.unity.modules.' + name] = '1.0.0'
    (packages / 'manifest.json').write_text(json.dumps(dict(dependencies=dependencies), indent=2), 'utf8')
    settings = project / 'ProjectSettings'
    settings.mkdir(exist_ok=True)
    for name in ('ProjectVersion.txt', 'TagManager.asset'):
        shutil.copy2(ROOT / 'unity/ProjectSettings' / name, settings / name)
    editor = project / 'Assets/Editor'
    editor.mkdir(parents=True, exist_ok=True)
    shutil.copy2(ROOT / 'scripts/fixtures/home_repair_effect_validation.cs', editor / 'HomeRepairEffectValidation.cs')
    print('Staged isolated original repair effects:', project, flush=True)


if __name__ == '__main__':
    main()
