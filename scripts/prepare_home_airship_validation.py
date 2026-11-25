"""Stage airship sources, original prefabs and their native Unity test dependencies."""
import json
import shutil
from pathlib import Path

from official_apk import ROOT
from validation_assemblies import stage_runtime_modules
from validation_workspace import validation_directory


def prepare():
    work = validation_directory()
    if work == ROOT / 'output/validation':
        raise ValueError('Use an owned validation workspace.')
    project = work / 'unity'
    if project.exists():
        raise FileExistsError('Retain the existing validation project: ' + str(project))
    project.mkdir()
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    roots = ['Zjcs.Official.HomeFurniture', 'Zjcs.Offline.HomeAirship', 'Zjcs.Offline.NativeSpine',
             'Zjcs.Official.HomeTipDots', 'Zjcs.Official.HomeEntityBindings', 'Zjcs.Official.StoryNodes',
             'Zjcs.Official.World.NativeText', 'Zjcs.Official.World.MainlineMiniMapHud', 'Zjcs.Offline.HomeSceneInput']
    stage_runtime_modules(project, runtime, roots)
    for folder in ('Spine/Runtime', 'TextMesh Pro'):
        shutil.copytree(ROOT / 'unity/Assets' / folder, project / 'Assets' / folder, dirs_exist_ok=True)
    packages = project / 'Packages'
    packages.mkdir(exist_ok=True)
    dependencies = {'com.unity.ugui': '1.0.0', 'com.unity.textmeshpro': '3.0.6', 'com.unity.nuget.newtonsoft-json': '3.2.1'}
    for name in ('com.cysharp.unitask', 'com.unity.render-pipelines.universal', 'com.unity.render-pipelines.core',
                 'com.unity.cinemachine', 'com.unity.timeline'):
        source = ROOT / 'unity/Packages' / name
        if not source.exists():
            matches = list((ROOT / 'unity/Library/PackageCache').glob(name + '@*'))
            if len(matches) != 1: raise ValueError('Resolve validation package: ' + name)
            source = matches[0]
        shutil.copytree(source, packages / name, dirs_exist_ok=True,
                        ignore=shutil.ignore_patterns('Tests', 'Tests.meta', 'Documentation~', 'Samples~'))
        dependencies[name] = 'file:' + name
    for name in ('ui', 'imgui', 'animation', 'audio', 'jsonserialize', 'screencapture', 'director', 'assetbundle',
                 'physics', 'physics2d', 'particlesystem', 'imageconversion', 'unitywebrequest',
                 'unitywebrequesttexture', 'unitywebrequestaudio', 'video', 'tilemap'):
        dependencies['com.unity.modules.' + name] = '1.0.0'
    (packages / 'manifest.json').write_text(json.dumps({'dependencies': dependencies}, indent=2), encoding='utf8')
    settings = project / 'ProjectSettings'
    settings.mkdir()
    shutil.copy2(ROOT / 'unity/ProjectSettings/ProjectVersion.txt', settings)

    receipt = json.loads((ROOT / 'assets/reference/home-airship-native-source-manifest.json').read_text(encoding='utf8'))
    outputs = set(receipt['outputs'])
    for name in ('map13-native-pipeline-source-manifest.json', 'map13-pipeline-shaders-source-manifest.json',
                 'map13-pipeline-textures-source-manifest.json'):
        data = json.loads((ROOT / 'assets/reference/official-map' / name).read_text(encoding='utf8'))
        outputs.update(row['path'] for row in data['outputs'])
    pipeline = ROOT / 'unity/Assets/Official/World/Map/13/NativePipeline'
    outputs.update(path.relative_to(ROOT).as_posix() for path in pipeline.rglob('*') if path.is_file())
    shader_dir = runtime / 'Official/World/NativeWater/Shaders/Pipeline'
    outputs.update(path.relative_to(ROOT).as_posix() for path in shader_dir.glob('*.shader'))
    for name in list(outputs):
        if not name.endswith('.meta') and (ROOT / (name + '.meta')).is_file(): outputs.add(name + '.meta')
    for name in outputs:
        source = ROOT / name
        target = project / source.relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    editor = project / 'Assets/Editor'
    editor.mkdir(parents=True, exist_ok=True)
    for source, target in (('home_airship_native_import.cs', 'AirshipImportProbe.cs'),
                           ('home_airship_native_visual.cs', 'AirshipVisualProbe.cs')):
        shutil.copy2(ROOT / 'scripts/fixtures' / source, editor / target)
    print('Staged independent airship Unity validation:', project)
    return project


if __name__ == '__main__':
    prepare()
