"""Stage the selected home camera sources for isolated Unity compilation."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess

from official_apk import ROOT
from prepare_partner_card_native import guid, sha
from validation_workspace import require_temporary_unity_project, validation_directory


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', required=True, type=Path)
    project = require_temporary_unity_project(parser.parse_args().project)
    work = validation_directory()
    receipt = json.loads((work / 'home-camera-runtime-source-manifest.json').read_bytes())
    if receipt['manifest'] != '884_156692': raise ValueError('Wrong selected official camera version.')
    for path, expected in receipt['inputs'].items():
        if sha((ROOT / path).read_bytes()) != expected: raise ValueError('Original camera source changed: ' + path)
    stage = work / 'home-camera-runtime'
    for path, expected in receipt['outputs'].items():
        if sha((stage / path).read_bytes()) != expected: raise ValueError('Staged camera code changed: ' + path)
    wanted = work / 'home-camera-validation-guids.txt'
    wanted.write_text(''.join('guid: ' + guid(stage / name) + '\n' for name in receipt['outputs'] if name.endswith('.meta')), 'utf8')
    found = subprocess.run(['rg', '-l', '-F', '-f', str(wanted), str(project / 'Assets'), '-g', '*.meta'],
                           capture_output=True, text=True)
    if found.returncode not in (0, 1): raise ValueError(found.stderr)
    target = project / 'Assets/Game/Runtime/Official/HomeCameraValidation'
    for existing in found.stdout.splitlines():
        if not Path(existing).is_relative_to(target): raise ValueError('Preserve existing camera GUID: ' + existing)
    shutil.copytree(stage, target, dirs_exist_ok=True)
    native = json.loads((work / 'home-camera-native-source-manifest.json').read_bytes())
    for path, expected in native['input_sha256'].items():
        if sha((ROOT / path).read_bytes()) != expected: raise ValueError('Original camera input changed: ' + path)
    assets = work / 'home-camera-native-assets'
    asset_target = project / 'Assets/Official/Home/CameraValidation'
    asset_target.mkdir(parents=True, exist_ok=True)
    for name, expected in native['outputs'].items():
        source = assets / name
        if sha(source.read_bytes()) != expected: raise ValueError('Staged camera asset changed: ' + name)
        dest = asset_target / name
        if dest.exists() and dest.read_bytes() != source.read_bytes():
            raise ValueError('Preserve existing isolated camera asset: ' + str(dest))
        shutil.copy2(source, dest)
    main = json.loads((work / 'home-main-camera-native-source-manifest.json').read_bytes())
    ui = main['ui_camera']
    for path, expected in {**main['inputs'], **ui['inputs']}.items():
        if sha((ROOT / path).read_bytes()) != expected: raise ValueError('Original UI camera input changed: ' + path)
    ui_assets = work / 'home-ui-camera-native-assets'
    ui_target = project / 'Assets/Official/Home/UICameraValidation'
    ui_target.mkdir(parents=True, exist_ok=True)
    for name, expected in ui['outputs'].items():
        source = ui_assets / name
        if sha(source.read_bytes()) != expected: raise ValueError('Staged UI camera asset changed: ' + name)
        dest = ui_target / name
        if dest.exists() and dest.read_bytes() != source.read_bytes():
            raise ValueError('Preserve existing isolated UI camera asset: ' + str(dest))
        shutil.copy2(source, dest)
    for path, expected in main['staged_main']['inputs'].items():
        if sha((ROOT / path).read_bytes()) != expected: raise ValueError('Original main camera input changed: ' + path)
    main_assets = work / 'home-main-camera-native-assets'
    main_target = project / 'Assets/Official/Home/MainCameraValidation'
    main_target.mkdir(parents=True, exist_ok=True)
    for name, expected in main['staged_main']['outputs'].items():
        source = main_assets / name
        if sha(source.read_bytes()) != expected: raise ValueError('Staged main camera asset changed: ' + name)
        dest = main_target / name
        if dest.exists() and dest.read_bytes() != source.read_bytes():
            raise ValueError('Preserve existing isolated main camera asset: ' + str(dest))
        shutil.copy2(source, dest)
    plugins = ROOT / 'unity/Assets/Official/Combat/NativeSound/Plugins'
    plugin_target = project / 'Assets/Official/Combat/NativeSound/Plugins'
    for source in plugins.rglob('*'):
        if not source.is_file(): continue
        dest = plugin_target / source.relative_to(plugins)
        dest.parent.mkdir(parents=True, exist_ok=True)
        if dest.exists() and dest.read_bytes() != source.read_bytes():
            raise ValueError('Preserve existing isolated Wwise plugin: ' + str(dest))
        if not dest.exists(): shutil.copy2(source, dest)
    installed = json.loads((ROOT / 'assets/reference/home-camera-native-source-manifest.json').read_bytes())['installed_output_sha256']
    for relative in ('Assets/Game/Runtime/Offline/HomeCamera/HomeCameraAssets.cs',
                     'Assets/Game/Runtime/Offline/HomeCamera/HomeCameraAssets.cs.meta',
                     'Assets/Game/Runtime/Offline/HomeCamera/HomeCameraSettingsStore.cs',
                     'Assets/Game/Runtime/Offline/HomeCamera/HomeCameraSettingsStore.cs.meta',
                     'Assets/Game/Runtime/Offline/HomeCamera/Zjcs.Offline.HomeCamera.asmdef',
                     'Assets/Game/Runtime/Offline/HomeCamera/Zjcs.Offline.HomeCamera.asmdef.meta',
                     'Assets/Game/Resources/Home/HomeCameraAssets.asset',
                     'Assets/Game/Resources/Home/HomeCameraAssets.asset.meta'):
        source = ROOT / 'unity' / relative
        dest = project / relative
        dest.parent.mkdir(parents=True, exist_ok=True)
        if dest.exists() and dest.read_bytes() != source.read_bytes():
            if installed.get('unity/' + relative) != sha(dest.read_bytes()):
                raise ValueError('Preserve existing isolated home camera catalog: ' + str(dest))
            shutil.copy2(source, dest)
        elif not dest.exists(): shutil.copy2(source, dest)
    fixture = project / 'Assets/Editor/HomeCameraValidation'
    fixture.mkdir(parents=True, exist_ok=True)
    source = (ROOT / 'scripts/fixtures/home_camera_native_validation.cs').read_text('utf8')
    for name, value in native['staged_guids'].items():
        source = source.replace('__' + name.upper() + '_GUID__', value)
    for name, value in ui['staged_guids'].items():
        source = source.replace('__UI_' + name.upper() + '_GUID__', value)
    source = source.replace('__MAIN_PREFAB_GUID__', main['staged_main']['prefab_guid'])
    parent_position = ui['ancestor_transforms'][0]['fields']['m_LocalPosition']
    for axis in ('x', 'y', 'z'):
        source = source.replace('__UI_PARENT_' + axis.upper() + '__', repr(parent_position[axis]))
    (fixture / 'HomeCameraNativeValidation.cs').write_text(source, 'utf8', newline='\n')
    (fixture / 'HomeCameraNativeValidation.asmdef').write_text(json.dumps(dict(
        name='HomeCameraNativeValidation', includePlatforms=['Editor'], references=[
            'Zjcs.Official.HomeCamera', 'Cinemachine', 'Unity.RenderPipelines.Core.Runtime',
            'Unity.RenderPipelines.Universal.Runtime', 'AK.Wwise.Unity.API', 'AK.Wwise.Unity.MonoBehaviour',
            'Zjcs.Offline.HomeCamera', 'Zjcs.Offline.State']), indent=2) + '\n', 'utf8', newline='\n')
    print('Staged selected official home camera compilation:', project, flush=True)


if __name__ == '__main__':
    main()
