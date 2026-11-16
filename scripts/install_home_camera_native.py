"""Install selected original home camera components and explicit local assets."""
import json
from pathlib import Path
import subprocess

import yaml

from home_native_assets import install_staged_assets
from install_home_story_options import metadata
from official_apk import ROOT
from prepare_partner_card_native import Dumper, guid, sha
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    code = json.loads((work / 'home-camera-runtime-source-manifest.json').read_bytes())
    free = json.loads((work / 'home-camera-native-source-manifest.json').read_bytes())
    main = json.loads((work / 'home-main-camera-native-source-manifest.json').read_bytes())
    if {code['manifest'], free['manifest'], main['manifest']} != {'884_156692'}:
        raise ValueError('Original camera inputs must share the selected version.')
    ui = main['ui_camera']
    for inputs in (code['inputs'], free['input_sha256'], main['inputs'], ui['inputs'], main['staged_main']['inputs']):
        for path, expected in inputs.items():
            if sha((ROOT / path).read_bytes()) != expected: raise ValueError('Original camera input changed: ' + path)
    def unit_chain(chain, allow_first_translation=False):
        for index, row in enumerate(chain):
            fields = row['fields']
            if fields['m_LocalScale'] != dict(x=1, y=1, z=1) or fields['m_LocalRotation'] != dict(x=0, y=0, z=0, w=1):
                raise ValueError('The original camera ancestor requires full transform binding.')
            if not (allow_first_translation and index == 0) and any(fields['m_LocalPosition'][axis] for axis in ('x', 'y', 'z')):
                raise ValueError('The original camera ancestor requires additional position binding.')
    unit_chain(free['ancestor_transforms'])
    unit_chain(main['ancestor_transforms'])
    unit_chain(ui['ancestor_transforms'], allow_first_translation=True)
    code_folder = work / 'home-camera-runtime'
    target = ROOT / 'unity/Assets/Game/Runtime/Official/HomeCamera'
    previous_path = ROOT / 'assets/reference/home-camera-runtime-source-manifest.json'
    previous = json.loads(previous_path.read_bytes()) if previous_path.is_file() else {}
    for name, expected in code['outputs'].items():
        source, destination = code_folder / name, target / name
        if sha(source.read_bytes()) != expected: raise ValueError('Changed staged camera code: ' + name)
        if destination.exists() and destination.read_bytes() != source.read_bytes():
            if previous.get('installed_output_sha256', {}).get(destination.relative_to(ROOT).as_posix()) != sha(destination.read_bytes()):
                raise ValueError('Preserve existing home camera code: ' + str(destination))
    wanted = work / 'home-camera-install-guids.txt'
    wanted.write_text(''.join('guid: ' + guid(code_folder / name) + '\n'
        for name in code['outputs'] if name.endswith('.meta')), 'utf8')
    found = subprocess.run(['rg', '-l', '-F', '-f', str(wanted), 'unity/Assets', '-g', '*.meta'],
                           cwd=ROOT, capture_output=True, text=True)
    if found.returncode not in (0, 1): raise ValueError(found.stderr)
    for row in found.stdout.splitlines():
        if not (ROOT / row).is_relative_to(target): raise ValueError('Preserve existing camera GUID: ' + row)
    # Verify every asset before writing any production output.
    for folder, outputs in ((work / 'home-camera-native-assets', free['outputs']),
                            (work / 'home-ui-camera-native-assets', ui['outputs']),
                            (work / 'home-main-camera-native-assets', main['staged_main']['outputs'])):
        for name, expected in outputs.items():
            if sha((folder / name).read_bytes()) != expected: raise ValueError('Changed staged original camera asset: ' + name)
    target.mkdir(parents=True, exist_ok=True)
    folder_meta = metadata(target, 'DefaultImporter', folder=True)
    installed_code = {folder_meta.relative_to(ROOT).as_posix(): sha(folder_meta.read_bytes())}
    for name in code['outputs']:
        path = target / name
        if not path.exists() or path.read_bytes() != (code_folder / name).read_bytes():
            path.write_bytes((code_folder / name).read_bytes())
        installed_code[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    code['installed_output_sha256'] = installed_code
    code['scope'] = 'Selected original camera and Bloom components, with explicit local dependency contracts; home host binding remains pending.'
    destination = ROOT / 'unity/Assets/Official/Home/CameraNative'
    destination.mkdir(parents=True, exist_ok=True)
    destination_meta = metadata(destination, 'DefaultImporter', folder=True)
    installed = {destination_meta.relative_to(ROOT).as_posix(): sha(destination_meta.read_bytes())}
    for label, folder, outputs in (('Free', work / 'home-camera-native-assets', free['outputs']),
                                   ('UI', work / 'home-ui-camera-native-assets', ui['outputs']),
                                   ('Main', work / 'home-main-camera-native-assets', main['staged_main']['outputs'])):
        installed.update(install_staged_assets(work, folder, outputs, destination / label, 'home-camera-' + label.lower()))
    # Explicit prefab references prevent relying on editor-only asset lookup.
    offline = ROOT / 'unity/Assets/Game/Runtime/Offline/HomeCamera'
    offline_meta = metadata(offline, 'DefaultImporter', folder=True)
    installed[offline_meta.relative_to(ROOT).as_posix()] = sha(offline_meta.read_bytes())
    for name, importer in (('HomeCameraAssets.cs', 'MonoImporter'), ('HomeCameraSettingsStore.cs', 'MonoImporter'),
                           ('Zjcs.Offline.HomeCamera.asmdef', 'AssemblyDefinitionImporter')):
        path = offline / name
        meta = metadata(path, importer)
        for output in (path, meta): installed[output.relative_to(ROOT).as_posix()] = sha(output.read_bytes())
    parent = ui['ancestor_transforms'][0]['fields']['m_LocalPosition']
    data = dict(MonoBehaviour=dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0),
        m_PrefabInstance=dict(fileID=0), m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0),
        m_Enabled=1, m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000, guid=guid(Path(str(offline / 'HomeCameraAssets.cs') + '.meta')), type=3),
        m_Name='HomeCameraAssets', m_EditorClassIdentifier='',
        FreeCamera=dict(fileID=605, guid=free['staged_guids']['prefab'], type=3),
        MainCamera=dict(fileID=4, guid=main['staged_main']['prefab_guid'], type=3),
        UICamera=dict(fileID=1105, guid=ui['staged_guids']['prefab'], type=3),
        Settings=dict(fileID=11400000, guid=free['staged_guids']['settings'], type=2), UIParentPosition=parent))
    catalog = ROOT / 'unity/Assets/Game/Resources/Home/HomeCameraAssets.asset'
    raw = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'
           + yaml.dump(data, Dumper=Dumper, sort_keys=False)).encode('utf8')
    if catalog.exists() and catalog.read_bytes() != raw: raise ValueError('Preserve existing home camera catalog.')
    if not catalog.exists(): catalog.write_bytes(raw)
    catalog_meta = metadata(catalog, 'NativeFormatImporter')
    for path in (catalog, catalog_meta): installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    def remap_inputs(inputs):
        result = {}
        for path, value in inputs.items():
            source = ROOT / path
            if source.is_relative_to(code_folder): source = target / source.name
            result[source.relative_to(ROOT).as_posix()] = value
        return result
    free['input_sha256'] = remap_inputs(free['input_sha256'])
    ui['inputs'] = remap_inputs(ui['inputs'])
    main['staged_main']['inputs'] = remap_inputs(main['staged_main']['inputs'])
    free.pop('intermediate', None); main.pop('intermediate', None); ui.pop('intermediate', None)
    proof = dict(manifest='884_156692', free_camera=free, main_camera=main,
        installed_output_sha256=installed, code_receipt='assets/reference/home-camera-runtime-source-manifest.json',
        offline_settings_inputs={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in
            (ROOT / '.tools/source-packages/current/884_156692/official-code/framework-firstpass-source/Framework.Device/DesktopDevice.cs',
             ROOT / '.tools/source-packages/current/884_156692/official-code/launch-source/BloomIntensitySettingState.cs',
             ROOT / '.tools/source-packages/current/884_156692/official-code/launch-source/PerformanceSettingState.cs',
             ROOT / 'unity/Assets/Game/Runtime/Offline/State/LocalJsonSaveFile.cs')},
        validation_inputs={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in
                           (Path(__file__), ROOT / 'scripts/prepare_home_camera_validation.py',
                            ROOT / 'scripts/fixtures/home_camera_native_validation.cs')},
        scope='Selected native camera resources and explicit catalog; original scene camera stack must be connected to runtime instances before activation.')
    (ROOT / 'assets/reference/home-camera-runtime-source-manifest.json').write_text(
        json.dumps(code, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    (ROOT / 'assets/reference/home-camera-native-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed selected original home camera resources and explicit local catalog.', flush=True)


if __name__ == '__main__':
    main()
