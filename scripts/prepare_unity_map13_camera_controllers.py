"""Port the original camera controllers and drag mathematics without changing gameplay formulas."""
import json
import re
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import encode, meta, sha, source_path, write

CODE = ROOT / '.tools/source-packages/current/871_156598/official-code'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/World/NativeCamera'
BINDINGS = ROOT / 'unity/Assets/Game/Resources/Map13/Native/camera-controller-bindings.json'
SETTINGS = ROOT / 'unity/Assets/Game/Resources/Map13/Native/camera-settings.json'
CAMERA_SOURCE = ROOT / 'unity/Assets/Official/World/Map/13/Source/world-camera.json'
PROOF = ROOT / 'assets/reference/official-map/map13-camera-controller-source-manifest.json'
NAMESPACE = 'Zjcs.Official.World.NativeCamera'
CLIENT = ['CameraBaseController', 'CameraMonoProxy', 'ICameraController', 'ICameraControllerDriven',
    'WorldCameraBaseController', 'WorldCameraDragController', 'WorldCameraFollowController',
    'WorldCameraComplexController', 'WorldCameraLookAheadController', 'CameraStoryController',
    'CameraControlBrain', 'OrthographicCameraResolutionAdaptor', 'CameraControlUtils',
    'IgnoreUIDragController', 'DragControllerBase', 'DragControllerData', 'DragControlUtils',
    'MovementType', 'DragControllerManager', 'GmCameraControlData', 'ICameraControlManager', 'IDragControllerManager',
    'IInputOperationResult', 'GameObjectInputResult', 'OnGameObjectZoomBeginResult', 'OnGameObjectZoomResult',
    'OnGameObjectZoomEndResult', 'OnGameObjectDragBeginResult', 'OnGameObjectDragEndResult',
    'OnIgnoreUIZoomBeginResult', 'OnIgnoreUIDragBeginResult', 'OnGlobalDragResult', 'OnGlobalDragEndResult']
COMMON = ['CameraControlEnv', 'CameraBaseSetting', 'WorldCameraBaseSetting', 'WorldCameraControlSetting',
    'WorldCameraComplexSetting', 'WorldCameraLookAheadSettings', 'CameraControlSettings',
    'CameraStorySettings', 'CameraDanceSettings', 'HomeCameraControlSetting', 'GameCameraControlSetting1']


def code_meta(path):
    identity = sha(source_path(path).encode('utf8'))[:32]
    write(Path(str(path) + '.meta'), ('fileFormatVersion: 2\nguid: ' + identity + '\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode('utf8'))


def recorded(path): return dict(path=source_path(path), sha256=sha(path.read_bytes()))


def span(text, marker):
    start = text.index(marker); opening = text.index('{', start); depth = 1; end = opening + 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}'); end += 1
    return start, end


def replace_block(text, marker, replacement):
    start, end = span(text, marker)
    return text[:start] + replacement + text[end:]


def clean(text):
    text = re.sub(r'^\s*//.*\n', '', text, flags=re.M)
    text = re.sub(r'^using (?:Framework(?:\.[\w.]+)?|Common|Cysharp[\w.]*|System.Diagnostics|System.Runtime.InteropServices);\n', '', text, flags=re.M)
    text = text.replace('\t[Inject]\n', '').replace('[ScriptableObjectConfig]\n', '')
    text = text.replace('Vector2.op_Implicit(', 'CameraVectorCast.Convert(').replace('Object.op_Implicit(', '(bool)(')
    text = re.sub(r'[ \t]+$', '', text, flags=re.M)
    text = re.sub(r'\n\s*\n', '\n', text)
    text = text.replace('using UnityEngine;\n', 'using UnityEngine;\nusing Object = UnityEngine.Object;\n')
    # Reconstruct C# auto-properties emitted with the newer decompiler's field keyword.
    pattern = r'(?m)^(\s*)(public\s+\w+\s+\w+)\s*\n\s*\{\s*\[CompilerGenerated\]\s*(?:readonly\s+)?get\s*\{\s*return field;\s*\}\s*\[CompilerGenerated\]\s*(private\s+)?set\s*\{\s*field = value;\s*\}\s*\}'
    text = re.sub(pattern, lambda m: m[1] + m[2] + ' { get; ' + (m[3] or '') + 'set; }', text)
    return text


def async_port(text, name):
    start, end = span(text, '\t[StructLayout(LayoutKind.Auto)]')
    text = text[:start] + text[end:]
    text = text.replace('private IMapRootManagerProxy MapRootManagerProxy { get; set; }', 'public IMapRootManagerProxy MapRootManagerProxy { get; set; }')
    text = text.replace('UniTaskExtensions.Forget(MoveAbruptly(pos));', 'CameraAsync.Observe(MoveAbruptly(pos));')
    marker = '\t[AsyncStateMachine('
    start = text.index(marker); method_start = text.index('\n\tprivate UniTask MoveAbruptly', start)
    _, end = span(text, '\tprivate UniTask MoveAbruptly')
    method = '\tprivate async System.Threading.Tasks.Task MoveAbruptly(Vector3 pos)\n\t{\n\t\tawait MapRootManagerProxy.PrepareInstantly(pos);\n\t\tbase.SetViewPos(pos, immediately: true);\n\t}'
    return text[:start] + method + text[end:]


def port(text, name):
    # Async state machines must be reconstructed before removing their attributes/usings.
    if name in ('WorldCameraBaseController', 'CameraStoryController'): text = async_port(text, name)
    text = clean(text)
    if name == 'DragControllerManager': text = text.replace(': BackgroundServiceBase, IDragControllerManager', ': IDragControllerManager')
    if name == 'CameraBaseController':
        text = text.replace('protected IInputOperationModule InputOperationModule', 'public IInputOperationModule InputOperationModule')
        text = text.replace('protected IScriptableObjectConfig<CameraControlSettings> mCameraControlSettings', 'public CameraSettingsValue mCameraControlSettings')
        text = text.replace('protected GmCameraControlData GmCameraControlData', 'public GmCameraControlData GmCameraControlData')
    if name == 'WorldCameraDragController':
        text = text.replace('private IMapRootManagerProxy MapRootManagerProxy', 'public IMapRootManagerProxy MapRootManagerProxy')
        text = text.replace('public IMapRootManagerProxy MapRootManagerProxy', 'public new IMapRootManagerProxy MapRootManagerProxy')
        text = text.replace('info.Size.X', 'info.Size.x').replace('info.Size.Y', 'info.Size.y')
    if name == 'DragControllerBase':
        text = text.replace('private IDragControllerManager DragControllerManager', 'public IDragControllerManager DragControllerManager')
        text = text.replace('Input.mousePosition', 'PointerPosition()').replace('Input.touchCount', 'TouchCount()')
        text = text.replace('\tpublic DragControllerData Data;', '\tpublic System.Func<Vector3> PointerPosition { get; set; } = () => Input.mousePosition;\n\tpublic System.Func<int> TouchCount { get; set; } = () => Input.touchCount;\n\tpublic DragControllerData Data;')
        text = text.replace('\t\t((MonoBehaviour)this)._002Ector();\n', '')

        text = text.replace('ref Vector2 reference = ref mVelocity;\n\t\t\t\t\tint num2 = i;\n\t\t\t\t\treference[num2] *=', 'mVelocity[i] *=')
        text = text.replace('reference = ref val3;\n\t\t\t\t\tnum2 = i;\n\t\t\t\t\treference[num2] +=', 'val3[i] +=')

    if name == 'IgnoreUIDragController': text = text.replace('protected IInputOperationModule InputOperationModule', 'public IInputOperationModule InputOperationModule')
    if name == 'CameraControlBrain':
        text = text.replace('private IServiceProvider ServiceProvider { get; set; }', 'public Action<object> InjectDependencies { private get; set; }')
        text = text.replace('private ICameraControlManager CameraControlManager', 'public ICameraControlManager CameraControlManager')
        text = text.replace('ServiceProviderExtensions.Inject(ServiceProvider, (object)value);', 'InjectDependencies(value);')
        start = text.index('\t\t\tGLoggerDebugInterpolatedStringHandler message')
        end = text.index('\n', text.index('\t\t\tLog.Debug(', start))
        text = text[:start] + '\t\t\tDebug.Log("CurController:" + cameraController.GetType());' + text[end:]
        start = text.index('\t\t\t\t\tGLoggerWarnInterpolatedStringHandler message')
        end = text.index('\n', text.index('\t\t\t\t\tLog.Warn(', start))
        text = text[:start] + '\t\t\t\t\tDebug.LogWarning(name + " missing camera MonoBehaviour: " + typeof(T).Name);' + text[end:]
    if name == 'CameraControlSettings':
        start = text.index('\t\t\tGLoggerErrorInterpolatedStringHandler message')
        end = text.index('\n', text.index('\t\t\tLog.Error(', start))
        text = text[:start] + '\t\t\tDebug.LogError(cameraControlEnv + " not supported");' + text[end:]
    if 'return field;' in text or 'GLogger' in text or '_002Ector' in text or 'UniTask' in text:
        raise ValueError('Unresolved C# adaptation: ' + name)
    # Keep all original fields and method order. Namespace isolation is the only type rename.
    first = re.search(r'(?m)^(?:\[|public |internal )', text).start()
    return text[:first] + 'namespace ' + NAMESPACE + '\n{\n' + text[first:].rstrip() + '\n}\n'


def camera_utils():
    path = CODE / 'client-source/CameraUtils.cs'; text = path.read_text(encoding='utf8')
    methods = []
    for marker in ('\tpublic static Bounds GetOrthographicCameraViewBounds(CinemachineVirtualCamera camera)',
        '\tpublic static Bounds GetOrthographicCameraViewBounds(Vector3 center, float orthographicSize, float aspect)',
        '\tpublic static Camera GetMainCamera()'):
        start,end = span(text, marker); methods.append(clean(text[start:end]))
    # Preserve the original non-orthographic guard and arithmetic; logger is a local Unity log.
    methods.insert(0, '''public static Bounds GetOrthographicCameraViewBounds(Camera camera)
    {
        if (!camera.orthographic) { Debug.LogError("The camera is not orthographic:" + camera.name + " scene:" + camera.scene.name); return default; }
        return GetOrthographicCameraViewBounds(camera.transform.position, camera.orthographicSize, camera.aspect);
    }''')
    for marker in ('\tpublic static Vector2 GetOrthographicCameraScreenScale(Camera camera)',):
        start,end=span(text,marker); methods.append(clean(text[start:end]))
    return path, 'using Cinemachine;\nusing UnityEngine;\nnamespace ' + NAMESPACE + '\n{\npublic static class CameraUtils\n{\n' + '\n'.join(methods) + '\n}\n}\n'


def build():
    receipt = ROOT / 'assets/reference/official-map/map13-source-manifest.json'
    pinned = json.loads(receipt.read_bytes())
    if pinned['manifest'] != '871_156598': raise ValueError('Unexpected original code version')
    for assembly in pinned['assemblies'].values():
        if sha((ROOT / assembly['path']).read_bytes()) != assembly['decoded_sha256']: raise ValueError('Original assembly changed')
    sources=[]; outputs=[]
    for area, names in (('client-source',CLIENT),('common-source',COMMON)):
        for name in names:
            path=CODE/area/(name+'.cs'); sources.append(recorded(path))
            target=DEST/(name+'.cs'); write(target,port(path.read_text(encoding='utf8'),name).encode('utf8')); code_meta(target); outputs.extend([recorded(target),recorded(Path(str(target)+'.meta'))])
    original, adapted=camera_utils(); sources.append(recorded(original)); target=DEST/'CameraUtils.cs';write(target,adapted.encode('utf8'));code_meta(target);outputs.extend([recorded(target),recorded(Path(str(target)+'.meta'))])
    camera = json.loads(CAMERA_SOURCE.read_bytes())
    fields = next(o['fields'] for o in camera['objects'] if o['identity'] == camera['settings'])
    settings = {k:v for k,v in fields.items() if not k.startswith('m_')}
    settings['WorldCameraControlSetting']['ComplexSetting']['AllowViewMissTarget'] = bool(settings['WorldCameraControlSetting']['ComplexSetting']['AllowViewMissTarget'])
    write(SETTINGS, encode(settings)); meta(SETTINGS); outputs.extend([recorded(SETTINGS), recorded(Path(str(SETTINGS)+'.meta'))])

    boolean_fields = {'Horizontal', 'Vertical', 'Inertia', 'UseMultipleRectangleBorders', 'AllowViewMissTarget', 'm_ShowDebugText', 'm_ShowCameraFrustum', 'm_IgnoreTimeScale'}
    def typed(value):
        if isinstance(value, dict): return {k: bool(v) if k in boolean_fields else typed(v) for k,v in value.items()}
        if isinstance(value, list): return [typed(v) for v in value]
        return value
    objects = {o['identity']:o for o in camera['objects']}
    records=[]
    for node in camera['nodes']:
        for key in node['components']:
            obj=objects[key]
            if obj['type'] not in {'CameraControlBrain','OrthographicCameraResolutionAdaptor','IgnoreUIDragController','Cinemachine.CinemachineVirtualCamera','Cinemachine.CinemachineTransposer','Cinemachine.CinemachineBrain'}: continue
            fields={k:v for k,v in obj['fields'].items() if k not in {'m_GameObject','m_Script','m_Name','m_Enabled'}}
            # CAB pointers remain in the complete source. Runtime binding below is explicit.
            fields={k:v for k,v in fields.items() if not (isinstance(v,dict) and set(v)=={'m_FileID','m_PathID'})}
            records.append(dict(path=node['path'],identity=key,type=obj['type'],fieldsJson=json.dumps(typed(fields),ensure_ascii=False,separators=(',',':'))))
    write(BINDINGS,encode(dict(manifest='871_156598',records=records))); meta(BINDINGS);outputs.extend([recorded(BINDINGS),recorded(Path(str(BINDINGS)+'.meta'))])

    proof=dict(manifest='871_156598',mapClassId=13,officialCode=sources,inputs=[recorded(receipt), recorded(CAMERA_SOURCE)],dependencyContracts=[recorded(DEST/'CameraMigrationContracts.cs')],tools=[recorded(Path(__file__).resolve())],outputs=outputs,
        operation='Copy original camera settings, current controller modes, story controller, drag bounds/inertia/elasticity and typed input results. Preserve original values, formulas, branch ordering and lifecycle ordering. Repair decompiler auto-properties, vector operators, base constructor artifacts and await state machines; adapt framework injection, Unity logging and pointer access through explicit local dependency contracts. Await partition preparation before an abrupt camera move.',
        limitations=['Controller code migration only. Complete original WorldCamera prefab/component/resource binding, original world-camera initializer, Windows input routing, actual Map13 gameplay and Player verification remain required.'])
    write(PROOF,encode(proof));print('Original camera controllers ported:',len(sources),'sources',len(outputs),'outputs',flush=True)

if __name__ == '__main__': build()
