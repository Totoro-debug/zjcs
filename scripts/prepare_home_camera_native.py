"""Recover the complete selected HomeFreeCamera scene hierarchy for binding."""
import json
import hashlib
from pathlib import Path

from home_native_assets import write_staged_assets
from official_apk import ROOT
from prepare_current_partner_native import PartnerNativeRecovery, documents, identity, pointer
from prepare_partner_card_native import guid, sha
from validation_workspace import validation_directory
from prepare_home_main_camera_native import ancestors


def main():
    work = validation_directory()
    recovery = PartnerNativeRecovery()
    bundle = 'defaultpackage_assets_scenes_main.bundle'
    objects = recovery.apk.objects(bundle)
    root, = [obj for obj in objects if obj.type.name == 'GameObject' and obj.read().m_Name == 'HomeFreeCamera']
    recovery.recover('HomeFreeCamera', work / 'home-camera-native-recovery', bundle=bundle,
        native_relative='Scenes/Main.unity', source_root=root, native_root_id=605)
    recovery.finalize()
    native, output, panel, record = recovery.outputs[0]
    if panel['unresolved'] or len(panel['objectMap']) != 19:
        raise ValueError('Selected camera hierarchy is incomplete.')
    staged = work / 'home-camera-runtime'
    bindings = {
        'CinemachineVirtualCamera': ROOT / 'unity/Packages/com.unity.cinemachine/Runtime/Behaviours/CinemachineVirtualCamera.cs.meta',
        'CinemachinePipeline': ROOT / 'unity/Packages/com.unity.cinemachine/Runtime/Behaviours/CinemachinePipeline.cs.meta',
        'CinemachineTransposer': ROOT / 'unity/Packages/com.unity.cinemachine/Runtime/Components/CinemachineTransposer.cs.meta',
        'Volume': ROOT / 'unity/Library/PackageCache/com.unity.render-pipelines.core@14.0.11/Runtime/Volume/Volume.cs.meta',
    }
    for name in ('IgnoreUIDragController', 'CameraControlBrain', 'HomeCameraIniter', 'BloomIntensityVolumeRefreshTrigger'):
        bindings[name] = staged / (name + '.cs.meta')
    if {row['type'] for row in panel['scripts']} != set(bindings):
        raise ValueError('The selected camera component inventory changed.')
    for row in panel['scripts']:
        panel['documents'][row['nativeFileID']]['MonoBehaviour']['m_Script'] = dict(
            fileID=11500000, guid=guid(bindings[row['type']]), type=3)
    volume, = [obj for obj in objects if obj.path_id == 4186]
    profile = pointer(recovery.fields(volume)['sharedProfile'], volume)
    profile_fields = recovery.fields(profile)
    bloom, = [pointer(entry, profile) for entry in profile_fields['components']]
    settings, = [obj for obj in recovery.apk.objects('defaultpackage_assets_scriptableconfig_modules_camera.bundle')
                 if obj.path_id == 7111545618142754997]
    asset_paths = {
        'profile': recovery.native / 'Settings/Volume/Global Volume Profile.asset',
        'bloom': recovery.native / 'MonoBehaviour/Bloom_0.asset',
        'settings': recovery.native / 'ScriptableConfig/Modules/Camera/CameraControlSettings.asset',
    }
    for obj, expected in ((profile, 'VolumeProfile'), (bloom, 'Bloom'), (settings, 'CameraControlSettings')):
        if obj.read(check_read=False).m_Script.read().m_ClassName != expected:
            raise ValueError('Unexpected original camera asset type: ' + identity(obj))
    asset_docs = {key: documents(path.read_text('utf8')) for key, path in asset_paths.items()}
    if any(set(value) != {11400000} for value in asset_docs.values()):
        raise ValueError('Unexpected converted camera asset structure.')
    package = ROOT / 'unity/Library/PackageCache/com.unity.render-pipelines.core@14.0.11/Runtime/Volume/VolumeProfile.cs.meta'
    bloom_script = ROOT / 'unity/Packages/com.unity.render-pipelines.universal/Runtime/Overrides/Bloom.cs.meta'
    script_paths = dict(profile=package, bloom=bloom_script, settings=staged / 'CameraControlSettings.cs.meta')
    new_guids = {name: hashlib.md5(('zjcs:home-camera:884:' + name).encode()).hexdigest()
                 for name in ('prefab', 'profile', 'bloom', 'settings')}
    for name, obj in (('profile', profile), ('bloom', bloom), ('settings', settings)):
        target = asset_docs[name][11400000]['MonoBehaviour']
        target['m_Script'] = dict(fileID=11500000, guid=guid(script_paths[name]), type=3)
        for key, value in recovery.fields(obj).items():
            if key in ('m_GameObject', 'm_Enabled', 'm_Script', 'm_Name'): continue
            if name == 'profile' and key == 'components':
                value = [dict(fileID=11400000, guid=new_guids['bloom'], type=2)]
            elif name == 'bloom' and key == 'dirtTexture':
                if value['m_Value']['m_PathID']: raise ValueError('Unexpected nonempty original bloom texture.')
                value = dict(value, m_Value=dict(fileID=0))
            target[key] = value
    source_profile = identity(profile)
    if set(panel['external']) != {source_profile}:
        raise ValueError('Camera has unexpected external dependencies.')
    panel['documents'][4186]['MonoBehaviour']['sharedProfile'] = dict(
        fileID=11400000, guid=new_guids['profile'], type=2)
    queue = [(Path('HomeFreeCamera.prefab'), new_guids['prefab'], panel['documents'])]
    for name in ('profile', 'bloom', 'settings'):
        queue.append((Path(name + '.asset'), new_guids[name], asset_docs[name]))
    outputs = write_staged_assets(queue, work / 'home-camera-native-assets')
    source_assets = {}
    for name, obj in (('profile', profile), ('bloom', bloom), ('settings', settings)):
        source_assets[name] = dict(original=identity(obj), sha256=sha(obj.get_raw_data()),
                                   provenance=recovery.apk.provenance(obj), fields=recovery.fields(obj))
    proof = dict(manifest=recovery.apk.version, bundle=bundle, original=identity(root), recovery=record,
        ancestor_transforms=ancestors(root, recovery),
        input_sha256={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in
            (Path(__file__), ROOT / 'scripts/prepare_current_partner_native.py', ROOT / 'scripts/prepare_home_main_camera_native.py', native, Path(str(native) + '.meta'),
             *asset_paths.values(), *(Path(str(path) + '.meta') for path in asset_paths.values()),
             *(path for path in bindings.values() if path.is_relative_to(ROOT)),
             *(path for path in script_paths.values() if path.is_relative_to(ROOT)))},
        intermediate=dict(path=output.relative_to(work).as_posix(), sha256=sha(output.read_bytes())),
        source_scripts=panel['scripts'], external=panel['external'], unresolved=panel['unresolved'],
        source_assets=source_assets, outputs=outputs, staged_guids=new_guids,
        scope='Original HomeFreeCamera hierarchy, camera settings and bloom profile staged for isolated validation; runtime host binding pending.')
    (work / 'home-camera-native-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recovered original home camera hierarchy:', len(panel['objectMap']), 'objects;',
          len(panel['scripts']), 'scripts;', len(panel['unresolved']), 'unresolved references.', flush=True)


if __name__ == '__main__':
    main()
