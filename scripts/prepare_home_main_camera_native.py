"""Recover the selected original main camera and audio-listener hierarchy."""
import json
import hashlib
from pathlib import Path

from home_native_assets import write_staged_assets
from official_apk import ROOT
from prepare_current_partner_native import PartnerNativeRecovery, documents, identity, pointer
from prepare_partner_card_native import guid, sha
from validation_workspace import validation_directory


def ancestors(root, recovery):
    transform = root.read().m_Component[0].component.deref().read().m_Father
    result = []
    while transform.path_id:
        obj = transform.deref()
        value = obj.read()
        result.append(dict(original=identity(obj), name=value.m_GameObject.read().m_Name,
                           fields=recovery.fields(obj), sha256=sha(obj.get_raw_data()),
                           provenance=recovery.apk.provenance(obj)))
        transform = value.m_Father
    return result


def main():
    work = validation_directory()
    recovery = PartnerNativeRecovery()
    bundle = 'defaultpackage_assets_scenes_main.bundle'
    objects = recovery.apk.objects(bundle)
    camera, = [obj for obj in objects if obj.type.name == 'GameObject' and obj.path_id == 4
               and obj.read().m_Name == 'Main Camera']
    recovery.recover('Main Camera', work / 'home-main-camera-native-recovery', bundle=bundle,
                     native_relative='Scenes/Main.unity', source_root=camera, native_root_id=4)
    ui, = [obj for obj in objects if obj.type.name == 'GameObject' and obj.path_id == 1105
           and obj.read().m_Name == 'UICamera']
    recovery.recover('UICamera', work / 'home-main-camera-native-recovery', bundle=bundle,
                     native_relative='Scenes/Main.unity', source_root=ui, native_root_id=1105)
    recovery.finalize()
    native, output, panel, record = recovery.outputs[0]
    ui_native, ui_output, ui_panel, ui_record = recovery.outputs[1]
    ui_camera, = [obj for obj in objects if obj.type.name == 'Camera' and obj.path_id == 1360]
    if set(panel['unresolved']) != {identity(ui_camera)} or identity(ui_camera) not in ui_panel['objectMap']:
        raise ValueError('Unexpected main-to-UI camera stack references.')
    staged = work / 'home-camera-runtime'
    core = ROOT / 'unity/Library/PackageCache/com.unity.render-pipelines.core@14.0.11/Runtime/Volume'
    script_paths = {
        'UniversalAdditionalCameraData': ROOT / 'unity/Packages/com.unity.render-pipelines.universal/Runtime/UniversalAdditionalCameraData.cs.meta',
        'UIBloomSwitch': staged / 'UIBloomSwitch.cs.meta',
        'Volume': core / 'Volume.cs.meta',
        'BloomIntensityVolumeRefreshTrigger': staged / 'BloomIntensityVolumeRefreshTrigger.cs.meta',
    }
    if {row['type'] for row in ui_panel['scripts']} != set(script_paths):
        raise ValueError('Unexpected original UI camera component inventory.')
    for row in ui_panel['scripts']:
        ui_panel['documents'][row['nativeFileID']]['MonoBehaviour']['m_Script'] = dict(
            fileID=11500000, guid=guid(script_paths[row['type']]), type=3)
    volume, = [obj for obj in objects if obj.type.name == 'MonoBehaviour' and obj.path_id == 5036]
    profile = pointer(recovery.fields(volume)['sharedProfile'], volume)
    bloom, = [pointer(item, profile) for item in recovery.fields(profile)['components']]
    if set(ui_panel['external']) != {identity(profile)}:
        raise ValueError('Unexpected external UI camera resources.')
    asset_paths = dict(profile=recovery.native / 'Settings/Volume/UIVolume.asset',
                       bloom=recovery.native / 'MonoBehaviour/Bloom.asset')
    asset_scripts = dict(profile=core / 'VolumeProfile.cs.meta',
                         bloom=ROOT / 'unity/Packages/com.unity.render-pipelines.universal/Runtime/Overrides/Bloom.cs.meta')
    keys = {name: hashlib.md5(('zjcs:home-ui-camera:884:' + name).encode()).hexdigest()
            for name in ('prefab', 'profile', 'bloom')}
    queue = []
    source_assets = {}
    for name, obj in (('profile', profile), ('bloom', bloom)):
        expected = 'VolumeProfile' if name == 'profile' else 'Bloom'
        if obj.read(check_read=False).m_Script.read().m_ClassName != expected:
            raise ValueError('Unexpected original UI camera volume asset.')
        docs = documents(asset_paths[name].read_text('utf8'))
        if set(docs) != {11400000}: raise ValueError('Unexpected UI camera asset structure.')
        target = docs[11400000]['MonoBehaviour']
        target['m_Script'] = dict(fileID=11500000, guid=guid(asset_scripts[name]), type=3)
        for key, value in recovery.fields(obj).items():
            if key in ('m_GameObject', 'm_Enabled', 'm_Script', 'm_Name'): continue
            if name == 'profile' and key == 'components':
                value = [dict(fileID=11400000, guid=keys['bloom'], type=2)]
            elif name == 'bloom' and key == 'dirtTexture':
                if value['m_Value']['m_PathID']: raise ValueError('Unexpected UI bloom dirt texture.')
                value = dict(value, m_Value=dict(fileID=0))
            target[key] = value
        queue.append((Path(name + '.asset'), keys[name], docs))
        source_assets[name] = dict(original=identity(obj), sha256=sha(obj.get_raw_data()),
                                   provenance=recovery.apk.provenance(obj), fields=recovery.fields(obj))
    ui_panel['documents'][5036]['MonoBehaviour']['sharedProfile'] = dict(fileID=11400000, guid=keys['profile'], type=2)
    queue.append((Path('UICamera.prefab'), keys['prefab'], ui_panel['documents']))
    ui_outputs = write_staged_assets(queue, work / 'home-ui-camera-native-assets')
    main_bindings = {
        'UniversalAdditionalCameraData': script_paths['UniversalAdditionalCameraData'],
        'CinemachineBrain': ROOT / 'unity/Packages/com.unity.cinemachine/Runtime/Behaviours/CinemachineBrain.cs.meta',
        'CameraSceneLayerCuller': staged / 'CameraSceneLayerCuller.cs.meta',
        'CameraPerformanceSetter': staged / 'CameraPerformanceSetter.cs.meta',
        'AkGameObj': ROOT / 'unity/Assets/Game/Runtime/Official/Wwise/MonoBehaviour/Runtime/AkGameObj.cs.meta',
        'AkAudioListener': ROOT / 'unity/Assets/Game/Runtime/Official/Wwise/MonoBehaviour/Runtime/AkAudioListener.cs.meta',
        'FollowSceneActiveSelfController': staged / 'FollowSceneActiveSelfController.cs.meta',
    }
    if {row['type'] for row in panel['scripts']} != set(main_bindings):
        raise ValueError('Unexpected original main camera component inventory.')
    for row in panel['scripts']:
        panel['documents'][row['nativeFileID']]['MonoBehaviour']['m_Script'] = dict(
            fileID=11500000, guid=guid(main_bindings[row['type']]), type=3)
    camera_data = panel['documents'][3760]['MonoBehaviour']
    if camera_data['m_Cameras'] != [dict(originalCAB=ui_camera.assets_file.name, originalPathID=ui_camera.path_id)]:
        raise ValueError('The original main camera stack changed.')
    # The original stack points at an instantiated scene camera. It must be
    # rebound to the instantiated UI camera, not the prefab asset's component.
    camera_data['m_Cameras'] = []
    main_guid = hashlib.md5(b'zjcs:home-main-camera:884:prefab').hexdigest()
    main_outputs = write_staged_assets([(Path('MainCamera.prefab'), main_guid, panel['documents'])],
                                       work / 'home-main-camera-native-assets')
    proof = dict(manifest=recovery.apk.version, original=identity(camera), bundle=bundle, recovery=record,
                 ancestor_transforms=ancestors(camera, recovery),
                 source_scripts=panel['scripts'], unresolved=panel['unresolved'], external=panel['external'],
                 staged_main=dict(outputs=main_outputs, prefab_guid=main_guid,
                     required_runtime_stack_binding=identity(ui_camera),
                     inputs={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in
                             (*main_bindings.values(), ROOT / 'assets/reference/battle-animation/sound-listener-source-manifest.json')}),
                 ui_camera=dict(original=identity(ui), recovery=ui_record, source_scripts=ui_panel['scripts'],
                                ancestor_transforms=ancestors(ui, recovery),
                                unresolved=ui_panel['unresolved'], external=ui_panel['external'],
                                source_assets=source_assets, outputs=ui_outputs, staged_guids=keys,
                                inputs={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in
                                        (*asset_paths.values(), *(Path(str(path) + '.meta') for path in asset_paths.values()),
                                         *script_paths.values(), *asset_scripts.values())},
                                stack_camera=identity(ui_camera), stack_camera_file_id=ui_panel['objectMap'][identity(ui_camera)],
                                intermediate=dict(path=ui_output.relative_to(work).as_posix(), sha256=sha(ui_output.read_bytes()))),
                 inputs={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in
                         (Path(__file__), ROOT / 'scripts/prepare_current_partner_native.py', native,
                          Path(str(native) + '.meta'))},
                 intermediate=dict(path=output.relative_to(work).as_posix(), sha256=sha(output.read_bytes())),
                 scope='Original main and UI camera hierarchies recovered, including their camera-stack relationship; script, Wwise and Windows bindings pending.')
    (work / 'home-main-camera-native-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recovered selected main camera:', len(panel['objectMap']), 'objects;', len(panel['scripts']),
          'scripts;', len(panel['unresolved']), 'unresolved references.', flush=True)


if __name__ == '__main__':
    main()
