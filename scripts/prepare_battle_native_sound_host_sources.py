"""Record original inputs and explicit bindings for the native battle sound host."""
from pathlib import Path
import hashlib
import json
import re

ROOT = Path(__file__).resolve().parents[1]
SDK = ROOT / '.tools/wwise/UnityIntegration-2022.1.18.8567.3199'
EXPORT = ROOT / '.tools/source-packages/current/884_156692/native-conversion/native-export-all'
OUTPUT = ROOT / 'assets/reference/battle-animation/native-sound-host-source-manifest.json'


def identity(path):
    raw = path.read_bytes()
    return dict(path=path.relative_to(ROOT).as_posix(), bytes=len(raw), sha256=hashlib.sha256(raw).hexdigest())


def main():
    sdk_source = ROOT / 'assets/reference/battle-animation/wwise-windows-integration-source-manifest.json'
    sdk_records = {entry['path']: entry for entry in json.loads(sdk_source.read_bytes())['files']}
    inputs = []
    for relative in (
        'Wwise/MonoBehaviour/Runtime/AkInitializer.cs',
        'Wwise/MonoBehaviour/Runtime/AkSoundEngineController.cs',
        'Wwise/MonoBehaviour/Runtime/AkGameObj.cs',
        'Wwise/MonoBehaviour/Runtime/AkGameObjEnvironmentData.cs',
        'Wwise/API/Runtime/Handwritten/Common/AkWwiseInitializationSettings.cs',
        'Wwise/API/Runtime/Handwritten/Common/AkSoundEngine.cs',
        'Wwise/API/Runtime/Plugins/Windows/x86_64/Profile/AkSoundEngine.dll',
        'Wwise/API/Runtime/Plugins/Windows/x86_64/Release/AkSoundEngine.dll',
    ):
        record = identity(SDK / relative)
        assert record['sha256'] == sdk_records[record['path']]['sha256'], relative
        inputs.append(record)
    for relative in (
        'launch-source/Launch/WwiseManager.cs',
        'launch-source/Launch/WwiseGlobalVolumnService.cs',
        'launch-source/Launch/WwiseManagerConfig.cs',
        'client-source/GameSoundSpeedController.cs',
    ):
        inputs.append(identity(ROOT / '.tools/source-packages/official-code' / relative))

    export_receipt = EXPORT.with_name(EXPORT.name + '-source-manifest.json')
    exports = {entry['path']: entry for entry in json.loads(export_receipt.read_bytes())['outputs']}

    def original(relative):
        path = EXPORT / 'ExportedProject/Assets' / relative
        record = identity(path)
        assert record['sha256'] == exports[path.relative_to(EXPORT).as_posix()]['sha256']
        inputs.append(record)
        return path.read_text(encoding='utf-8-sig')

    config = original('Launch/ScriptableConfig/WwiseManagerConfig.asset')
    controls = []
    for field, name, reference, identifier in (
        ('AllSoundRTPC', 'Volume_ALL', '05CB33A4-6F56-44CF-AB5A-6F78D18BE5A2', 3240620755),
        ('MusicRTPC', 'Volume_BGM', '1B6CB520-BCCA-4A47-B44D-8911B2C0FB91', 3659619896),
        ('SpeakRTPC', 'Volume_Voice', 'BD2522BD-D640-42FE-83A9-05F4FA08839C', 3676256460),
        ('SoundRTPC', 'Volume_SFX', '62AFCD1C-4BC1-4BEA-B45D-89B8E416516F', 3673881719),
    ):
        text = original('MonoBehaviour/' + reference + '.asset')
        meta = original('MonoBehaviour/' + reference + '.asset.meta')
        guid, = re.findall(r'^guid: ([0-9a-f]{32})$', meta, re.M)
        assert f'objectName: {name}' in text and f'id: {identifier}' in text
        assert re.search(r'  ' + field + r':.*?WwiseObjectReference: \{fileID: 11400000, guid: ' + guid + r', type: 2\}', config, re.S)
        controls.append(dict(field=field, name=name, id=identifier, asset_guid=guid))
    game_base = original('Prefabs/Fight/GameBaseComponent.prefab')
    speed = original('Wwise/ScriptableObjects/GameParameter/3436FE43-4069-45C1-B983-D5C66E28FA48.asset')
    speed_meta = original('Wwise/ScriptableObjects/GameParameter/3436FE43-4069-45C1-B983-D5C66E28FA48.asset.meta')
    speed_guid, = re.findall(r'^guid: ([0-9a-f]{32})$', speed_meta, re.M)
    assert 'objectName: Other_GameSpeed' in speed and 'id: 414249091' in speed
    assert re.search(r'  SpeedRTPC:.*?WwiseObjectReference: \{fileID: 11400000, guid: ' + speed_guid + r', type: 2\}', game_base, re.S)
    outputs = []
    for relative in (
        'unity/Assets/Game/Runtime/Offline/BattleNativePlayables/OfflineBattleWwiseSoundHost.cs',
        'unity/Assets/Game/Runtime/Offline/BattleNativePlayables/OfflineBattleWwiseSoundHost.cs.meta',
        'unity/Assets/Game/Runtime/Offline/BattleNativePlayables/OfflineBattleWwiseRuntime.cs',
        'unity/Assets/Game/Runtime/Offline/BattleNativePlayables/OfflineBattleWwiseRuntime.cs.meta',
        'unity/Assets/Game/Runtime/Offline/BattleNativePlayables/OfflineBattleWwiseRuntime.Bgm.cs',
        'unity/Assets/Game/Runtime/Offline/BattleNativePlayables/OfflineBattleWwiseRuntime.Bgm.cs.meta',
        'unity/Assets/Game/Runtime/Offline/BattleNativePlayables/OfflineBattleThemeSelection.cs',
        'unity/Assets/Game/Runtime/Offline/BattleNativePlayables/OfflineBattleThemeSelection.cs.meta',
        'unity/Assets/Game/Runtime/Offline/BattleNativePlayables/Zjcs.Offline.BattleNativePlayables.asmdef',
        'unity/Assets/Game/Editor/BattleSounds/OfficialBattleWwiseSoundAcceptance.cs',
        'unity/Assets/Game/Editor/BattleSounds/OfficialBattleWwiseSoundAcceptance.cs.meta',
        'unity/Assets/Game/Editor/BattleSounds/OfficialBattleWwiseSoundAcceptance.Controller.cs',
        'unity/Assets/Game/Editor/BattleSounds/OfficialBattleWwiseSoundAcceptance.Controller.cs.meta',
        'unity/Assets/Game/Editor/BattleSounds/OfficialBattleWwiseSoundAcceptance.Bgm.cs',
        'unity/Assets/Game/Editor/BattleSounds/OfficialBattleWwiseSoundAcceptance.Bgm.cs.meta',
        'unity/Assets/Game/Editor/BattleSounds/OfficialBattleWwiseSoundAcceptance.BgmScenes.cs',
        'unity/Assets/Game/Editor/BattleSounds/OfficialBattleWwiseSoundAcceptance.BgmScenes.cs.meta',
        'unity/Assets/Game/Editor/BattleSounds/OfficialBattleWwiseSoundAcceptance.Theme.cs',
        'unity/Assets/Game/Editor/BattleSounds/OfficialBattleWwiseSoundAcceptance.Theme.cs.meta',
        'unity/Assets/Game/Editor/BattleSounds/Zjcs.Editor.BattleSounds.asmdef',
        'unity/Assets/Game/Runtime/Validation/BattlePublic/OfficialBattlePublicFlowValidation.cs',
        'unity/Assets/Game/Runtime/Validation/BattlePublic/OfficialBattlePublicFlowValidation.Sound.cs',
        'unity/Assets/Game/Runtime/Validation/BattlePublic/OfficialBattlePublicFlowValidation.Sound.cs.meta',
        'unity/Assets/Game/Runtime/Validation/BattlePublic/Zjcs.Validation.BattlePublic.asmdef',
        'unity/Assets/Game/Runtime/Presentation/Home/BattleThemePlayerValidation.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/BattleThemePlayerValidation.cs.meta',
        'unity/Assets/Game/Runtime/Presentation/Home/BattleThemePlayerValidation.Sound.cs',
        'unity/Assets/Game/Runtime/Presentation/Home/BattleThemePlayerValidation.Sound.cs.meta',
    ):
        outputs.append(identity(ROOT / relative))
    receipt = dict(manifest='884_156692', sdk_version='2022.1.18.8567',
        sdk_source=identity(sdk_source),
        runtime_source=identity(ROOT / 'assets/reference/battle-animation/windows-sound-runtime-source-manifest.json'),
        settings_export=identity(export_receipt), inputs=inputs, volume_rtpcs=controls,
        game_speed_rtpc=dict(id=414249091, name='Other_GameSpeed', asset_guid=speed_guid),
        application_lifecycle=dict(
            source='AkSoundEngineController.OnApplicationPause/OnApplicationFocus/ActivateAudio',
            pause_render_anyway=False, focus_render_anyway='ActivePlatformSettings.RenderDuringFocusLoss',
            enabled='AkWwiseInitializationSettings.Instance.SuspendAudioDuringFocusLoss',
            resume=['WakeupFromSuspend', 'RenderAudio'], suspend=['Suspend', 'RenderAudio'],
            editor_callbacks='no-op', callback_order='apply each callback directly'),
        borrowed_vector_exports=['CSharp_AkWorldTransform_Position', 'CSharp_AkImageSourceParams_sourcePosition_get',
            'CSharp_AkReflectionPathInfo_imageSource_get', 'CSharp_AkDiffractionPathInfo_emitterPos_get'],
        vector_value_export='CSharp_ConvertAkVectorToAkVector64',
        listener_source=identity(ROOT / 'assets/reference/battle-animation/sound-listener-source-manifest.json'),
        outputs=outputs, tool=identity(Path(__file__)), validation_tools=[
            identity(ROOT / 'unity/Assets/Game/Editor/BattleMovementEffects/OfficialBattlePublicPlayerBuild.cs'),
            identity(ROOT / 'scripts/test_battle_public_player.ps1'),
            identity(ROOT / 'scripts/test_battle_sound_lifecycle.ps1'),
            identity(ROOT / 'scripts/test_battle_sound_suspend.py')])
    OUTPUT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print(json.dumps(dict(inputs=len(inputs), outputs=len(outputs), original_volume_controls=len(controls))))


if __name__ == '__main__':
    main()
