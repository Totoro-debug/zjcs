"""Stage selected official home camera controllers and dependency ports."""
import hashlib
import json
import re
from pathlib import Path

from official_apk import ROOT
import prepare_unity_map13_camera_controllers as camera
from prepare_partner_card_native import sha
from validation_workspace import validation_directory

CODE = ROOT / '.tools/source-packages/current/884_156692/official-code'
NAMESPACE = 'Zjcs.Official.HomeCamera'
CLIENT = [name for name in camera.CLIENT if not name.startswith('WorldCamera')]
CLIENT += ['HomeCameraController', 'HomeCameraIniter', 'OrthographicCameraExtendAdaptor',
           'IInputOperationModule', 'InputOperationLayer']


def main():
    work = validation_directory()
    folder = work / 'home-camera-runtime'
    folder.mkdir(parents=True, exist_ok=True)
    camera.NAMESPACE = NAMESPACE
    camera.CODE = CODE
    sources, outputs = {}, {}
    archived_path = ROOT / 'assets/reference/official-client-code/source-manifest.json'
    archived = json.loads(archived_path.read_bytes())
    if archived['manifest'] != '884_156692': raise ValueError('Wrong official camera code archive.')
    archive_path = ROOT / archived['archive_manifest']['path']
    if sha(archive_path.read_bytes()) != archived['archive_manifest']['sha256']:
        raise ValueError('Original decompilation manifest changed.')
    archive = json.loads(archive_path.read_bytes())
    if archive['manifest'] != archived['manifest']: raise ValueError('Mismatched official camera archive.')
    files = {row['path']: row['sha256'] for project in archive['projects'] for row in project['files']}
    sources[archived_path.relative_to(ROOT).as_posix()] = sha(archived_path.read_bytes())
    sources[archive_path.relative_to(ROOT).as_posix()] = sha(archive_path.read_bytes())
    for name in ('Assembly-CSharp.dll', 'Console.dll', 'Launch.dll', 'Framework.FirstPass.Runtime.dll', 'GameFramework.Runtime.dll'):
        assembly = CODE / 'managed/decoded' / name
        expected = archive['assemblies'][name]['decoded_sha256']
        if sha(assembly.read_bytes()) != expected: raise ValueError('Original assembly changed: ' + name)
        sources[assembly.relative_to(ROOT).as_posix()] = expected

    def write(name, content, importer='MonoImporter'):
        path = folder / name
        raw = content.encode('utf8')
        path.write_bytes(raw)
        key = hashlib.md5(('zjcs:home-camera:884:' + name).encode()).hexdigest()
        meta = ('fileFormatVersion: 2\nguid: ' + key + '\n' + importer + ':\n  externalObjects: {}\n').encode()
        Path(str(path) + '.meta').write_bytes(meta)
        outputs[name] = sha(raw)
        outputs[name + '.meta'] = sha(meta)

    for area, names in (('client-source', CLIENT), ('common-source', camera.COMMON)):
        for name in names:
            path = CODE / area / (name + '.cs')
            if not path.is_file(): raise FileNotFoundError(path)
            raw = path.read_text('utf8')
            if name == 'OrthographicCameraExtendAdaptor': raw = raw.replace('\t[Rename("宽高比")]\n', '')
            sources[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
            write(name + '.cs', camera.port(raw, name))
    path, text = camera.camera_utils()
    sources[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    raw = path.read_text('utf8')
    start, end = camera.span(raw, '\tpublic static Vector2 GetOrthographicCameraScreenScale(Bounds bounds)')
    method = camera.clean(raw[start:end])
    position = text.rfind('\n}\n}')
    if position < 0: raise ValueError('CameraUtils wrapper changed.')
    write('CameraUtils.cs', text[:position] + '\n' + method + text[position:])
    contract = ROOT / 'unity/Assets/Game/Runtime/Official/World/NativeCamera/CameraMigrationContracts.cs'
    sources[contract.relative_to(ROOT).as_posix()] = sha(contract.read_bytes())
    contract_text = contract.read_text('utf8').replace('Zjcs.Official.World.NativeCamera', NAMESPACE)
    start, end = camera.span(contract_text, '    public interface IInputOperationModule')
    write('CameraMigrationContracts.cs', contract_text[:start] + contract_text[end:])
    safe = CODE / 'framework-firstpass-source/Framework/SafeAction.cs'
    raw = safe.read_text('utf8')
    start, end = camera.span(raw, 'public class SafeAction<T1>')
    selected = raw[start:end]
    log_start = selected.index('\t\t\t\tGLoggerErrorInterpolatedStringHandler message')
    log_end = selected.index('\n', selected.index('\t\t\t\tLog.Error(', log_start))
    selected = selected[:log_start] + '\t\t\t\tUnityEngine.Debug.LogException(exception);' + selected[log_end:]
    sources[safe.relative_to(ROOT).as_posix()] = sha(safe.read_bytes())
    write('SafeAction.cs', 'using System;\nusing System.Collections.Generic;\nnamespace ' + NAMESPACE
          + '\n{\n' + selected + '\n}\n')
    for name in ('IBloomIntensityService', 'EBloomIntensityLevel', 'BloomIntensityVolumeRefreshTrigger'):
        path = CODE / 'launch-source' / (name + '.cs')
        raw = path.read_text('utf8')
        sources[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        if name == 'BloomIntensityVolumeRefreshTrigger':
            raw = camera.clean(raw)
            raw = raw.replace('private IBloomIntensityService BloomIntensityService', 'public IBloomIntensityService BloomIntensityService')
            start = raw.index('\t\t\tGLoggerErrorInterpolatedStringHandler message')
            end = raw.index('\n', raw.index('\t\t\tLog.Error(', start))
            raw = raw[:start] + '\t\t\tDebug.LogError("BloomIntensityVolumeRefreshTrigger: Volume missing on " + name);' + raw[end:]
            raw = raw.replace('profile.TryGet<Bloom>(ref mBloom)', 'profile.TryGet<Bloom>(out mBloom)')
        first = __import__('re').search(r'(?m)^(?:\[|public |internal )', raw).start()
        write(name + '.cs', raw[:first] + 'namespace ' + NAMESPACE + '\n{\n' + raw[first:].rstrip() + '\n}\n')
    path = CODE / 'client-source/UIBloomSwitch.cs'
    raw = camera.clean(path.read_text('utf8'))
    raw = raw.replace('private IUIBloomManager UIBloomManager', 'public IUIBloomManager UIBloomManager')
    sources[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    contract_source = CODE / 'client-source/IUIBloomManager.cs'
    sources[contract_source.relative_to(ROOT).as_posix()] = sha(contract_source.read_bytes())
    write('IUIBloomManager.cs', 'using System;\nnamespace ' + NAMESPACE + '\n{\n'
          + 'public interface IUIBloomManager { bool BloomActive { get; }\n'
          + 'void RegisterActiveListener(Action<bool> callback);\n'
          + 'void UnregisterActiveListener(Action<bool> callback); }\n}\n')
    first = __import__('re').search(r'(?m)^(?:\[|public |internal )', raw).start()
    write('UIBloomSwitch.cs', raw[:first] + 'namespace ' + NAMESPACE + '\n{\n' + raw[first:].rstrip() + '\n}\n')
    for name in ('BloomIntensitySettingState', 'PerformanceSettingState', 'EFrameRateMode', 'ESettingsLevel', 'ERenderQualityLevel'):
        path = CODE / 'launch-source' / (name + '.cs')
        raw = camera.clean(path.read_text('utf8'))
        sources[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        first = re.search(r'(?m)^(?:\[|public |internal )', raw).start()
        write(name + '.cs', raw[:first] + 'namespace ' + NAMESPACE + '\n{\n' + raw[first:].rstrip() + '\n}\n')
    performance = CODE / 'framework-firstpass-source/Framework.Device/PerformanceLevel.cs'
    raw = performance.read_text('utf8').replace('namespace Framework.Device;', 'namespace ' + NAMESPACE + ';')
    # Unity's bundled C# compiler does not support file-scoped namespaces.
    raw = raw.replace('namespace ' + NAMESPACE + ';', 'namespace ' + NAMESPACE + '\n{') + '\n}\n'
    sources[performance.relative_to(ROOT).as_posix()] = sha(performance.read_bytes())
    write('PerformanceLevel.cs', raw)
    for name in ('Framework.Device/IDevice.cs', 'Framework.Storage/IPersistentState.cs'):
        path = CODE / 'framework-firstpass-source' / name
        sources[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    write('BloomPersistenceContracts.cs', 'using System.Threading.Tasks;\nnamespace ' + NAMESPACE + '\n{\n'
          + 'public interface IPersistentState<T> { T State { get; } Task WriteStateAsync(); }\n'
          + 'public interface IDevice { PerformanceLevel GetPerformanceLevel(); }\n}\n')
    path = CODE / 'launch-source/BloomIntensityService.cs'
    sources[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    raw = camera.clean(path.read_text('utf8'))
    raw = raw.replace(': BackgroundServiceBase, IBloomIntensityService', ': IBloomIntensityService')
    raw = raw.replace('protected override void Initialize()', 'public void Initialize()')
    raw = raw.replace('UniTaskExtensions.Forget(', 'CameraAsync.Observe(')
    raw = re.sub(r'^\s*GLogger\w+InterpolatedStringHandler message\d*;\s*$', '', raw, flags=re.M)
    raw = re.sub(r'^\s*bool enabled;\s*$', '', raw, flags=re.M)
    pattern = re.compile(r'(?m)^(\s*)(?:GLogger\w+InterpolatedStringHandler\s+)?message\d* = new GLogger\w+InterpolatedStringHandler\(')
    while match := pattern.search(raw):
        start = match.start()
        marker = raw.index('if (enabled)', match.end())
        _, end = camera.span(raw, 'if (enabled)') if raw.find('if (enabled)') == marker else camera.span(raw[marker:], 'if (enabled)')
        if raw.find('if (enabled)') != marker: end += marker
        log_start = raw.index('Log.', end)
        log_end = raw.index('\n', log_start)
        line = raw[log_start:log_end]
        severity = 'LogError' if line.startswith('Log.Error(') else 'Log'
        replacement = ('Debug.LogException(exception);' if 'Log.Error(exception,' in line else
                       'Debug.' + severity + '("BloomIntensityService: " + nameof(BloomIntensityService));')
        raw = raw[:start] + '\n' + match.group(1) + replacement + raw[log_end:]
    if 'GLogger' in raw or 'UniTask' in raw or 'Log.Error(' in raw:
        raise ValueError('Unresolved original Bloom service adaptation.')
    first = re.search(r'(?m)^(?:\[|public |internal )', raw).start()
    write('BloomIntensityService.cs', raw[:first] + 'namespace ' + NAMESPACE + '\n{\n' + raw[first:].rstrip() + '\n}\n')
    for area, name in (('launch-source', 'CameraPerformanceSetter'), ('client-source', 'FollowSceneActiveSelfController')):
        path = CODE / area / (name + '.cs')
        sources[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        raw = camera.clean(path.read_text('utf8'))
        if name == 'CameraPerformanceSetter':
            raw = raw.replace('private IBloomIntensityService BloomIntensityService', 'public IBloomIntensityService BloomIntensityService')
        first = re.search(r'(?m)^(?:\[|public |internal )', raw).start()
        write(name + '.cs', raw[:first] + 'namespace ' + NAMESPACE + '\n{\n' + raw[first:].rstrip() + '\n}\n')
    scene_type = CODE / 'game-framework-source/GameFramework/SceneType.cs'
    sources[scene_type.relative_to(ROOT).as_posix()] = sha(scene_type.read_bytes())
    raw = scene_type.read_text('utf8').replace('namespace GameFramework;', 'namespace ' + NAMESPACE + '\n{') + '\n}\n'
    write('SceneType.cs', raw)
    for name in ('GameFramework/SceneData.cs',):
        path = CODE / 'game-framework-source' / name
        sources[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    write('CameraCullingContracts.cs', 'using System;\nusing UnityEngine;\nnamespace ' + NAMESPACE + '\n{\n'
          + 'public interface ICameraPopup { bool FullScreenDisplay { get; } }\n'
          + 'public interface ICameraWindowEvents { event Action<GameObject> OnWindowOpened; event Action<GameObject> OnWindowClosing; bool TryGetPopup(GameObject go, out ICameraPopup popup); }\n'
          + 'public interface ICameraSceneEvents { event Action<CameraSceneData> OnPreLoadScene; }\n'
          + 'public sealed class CameraSceneData { public SceneType SceneType; }\n'
          + 'public interface ICameraPerformanceSetting { bool BloomEnabled { get; } }\n}\n')
    path = CODE / 'client-source/CameraAttrs.cs'
    sources[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    raw = camera.clean(path.read_text('utf8'))
    write('CameraAttrs.cs', 'namespace ' + NAMESPACE + '\n{\n' + raw.rstrip() + '\n}\n')
    path = CODE / 'client-source/CameraSceneLayerCuller.cs'
    sources[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    raw = camera.clean(path.read_text('utf8'))
    raw = raw.replace('using GameFramework;\n', '')
    raw = raw.replace('private IPerformanceSetting PerformanceSetting', 'public ICameraPerformanceSetting PerformanceSetting')
    raw = raw.replace('\tprivate int mFullScreenWindowsCounter;', '\tpublic ICameraWindowEvents WindowEvents { get; set; }\n\tpublic ICameraSceneEvents SceneEvents { get; set; }\n\tprivate int mFullScreenWindowsCounter;')
    raw = raw.replace('WindowManager.Instance', 'WindowEvents').replace('MySceneManager.Instance', 'SceneEvents')
    raw = raw.replace('PopupWindow popupWindow = default;', 'ICameraPopup popupWindow = default;')
    raw = raw.replace('go.TryGetComponent<PopupWindow>(ref popupWindow)', 'WindowEvents.TryGetPopup(go, out popupWindow)')
    raw = raw.replace('Action<SceneData>', 'Action<CameraSceneData>').replace('OnScenceLoad(SceneData sceneData)', 'OnScenceLoad(CameraSceneData sceneData)')
    raw = raw.replace('LayerMask.op_Implicit(mCamera.cullingMask)', 'mCamera.cullingMask')
    raw = raw.replace('LayerMask.op_Implicit(mFullScreenUICullingMask)', 'mFullScreenUICullingMask')
    raw = raw.replace('LayerMask.op_Implicit(mCacheCameraLayer)', 'mCacheCameraLayer')
    raw = re.sub(r'^\s*bool enabled;\s*$', '', raw, flags=re.M)
    while match := pattern.search(raw):
        start = match.start()
        marker = raw.index('if (enabled)', match.end())
        _, end = camera.span(raw[marker:], 'if (enabled)'); end += marker
        log_start = raw.index('Log.', end); log_end = raw.index('\n', log_start)
        line = raw[log_start:log_end]
        severity = 'LogError' if line.startswith('Log.Error(') else 'LogWarning' if line.startswith('Log.Warn(') else 'Log'
        raw = raw[:start] + '\n' + match.group(1) + 'Debug.' + severity + '("CameraSceneLayerCuller");' + raw[log_end:]
    if 'GLogger' in raw or 'LayerMask.op_Implicit' in raw or 'WindowManager' in raw or 'PopupWindow' in raw:
        raise ValueError('Unresolved original main camera culling adaptation.')
    first = re.search(r'(?m)^(?:\[|public |internal )', raw).start()
    write('CameraSceneLayerCuller.cs', raw[:first] + 'namespace ' + NAMESPACE + '\n{\n' + raw[first:].rstrip() + '\n}\n')
    definition = json.dumps(dict(name=NAMESPACE, references=['Cinemachine', 'Unity.RenderPipelines.Core.Runtime',
        'Unity.RenderPipelines.Universal.Runtime'], autoReferenced=True), indent=2) + '\n'
    write(NAMESPACE + '.asmdef', definition, 'AssemblyDefinitionImporter')
    for path in (Path(__file__), ROOT / 'scripts/prepare_unity_map13_camera_controllers.py'):
        sources[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    for path, digest in sources.items():
        if path.startswith(CODE.relative_to(ROOT).as_posix() + '/') and '/managed/decoded/' not in path:
            if files.get(path) != digest: raise ValueError('Original decompiled source changed: ' + path)
    receipt = dict(manifest='884_156692', inputs=sources, outputs=outputs,
        scope='Selected original home/story camera controllers and service ports; scene binding and validation pending.')
    (work / 'home-camera-runtime-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Staged selected official home camera runtime:', len(outputs) // 2, 'files', flush=True)


if __name__ == '__main__':
    main()
