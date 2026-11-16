"""Stage selected original home camera pointer and zoom operations."""
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
NAMES = ('IInputOperation', 'InputOperationBase', 'IgnoreUIInputOperationBase',
         'GlobalPointerDownOperation', 'GlobalPointerHoldOperation', 'GlobalDragBeginOperation',
         'GlobalDragOperation', 'GlobalDragEndOperation', 'IgnoreUIDragBeginOperation',
         'GlobalZoomOperation', 'GlobalZoomBeginOperation', 'IgnoreUIZoomBeginOperation',
         'OnGlobalPointerDownResult', 'OnGlobalPointerHoldResult', 'OnGlobalDragBeginResult',
         'OnGlobalZoomResult', 'OnGlobalZoomBeginResult', 'InputOperationCallbackInfo')


def main():
    work = validation_directory()
    folder = work / 'home-camera-input'
    folder.mkdir(parents=True, exist_ok=True)
    camera.NAMESPACE = NAMESPACE
    source_receipt = ROOT / 'assets/reference/official-client-code/source-manifest.json'
    receipt = json.loads(source_receipt.read_bytes())
    if receipt['manifest'] != '884_156692': raise ValueError('Wrong original input source version.')
    archive_path = ROOT / receipt['archive_manifest']['path']
    if sha(archive_path.read_bytes()) != receipt['archive_manifest']['sha256']:
        raise ValueError('Original decompilation archive changed.')
    archive = json.loads(archive_path.read_bytes())
    files = {row['path']: row['sha256'] for project in archive['projects'] for row in project['files']}
    assembly = CODE / 'managed/decoded/Assembly-CSharp.dll'
    if sha(assembly.read_bytes()) != archive['assemblies']['Assembly-CSharp.dll']['decoded_sha256']:
        raise ValueError('Original client assembly changed.')
    inputs = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in
              (Path(__file__), ROOT / 'scripts/prepare_unity_map13_camera_controllers.py',
               source_receipt, archive_path, assembly)}
    layer = CODE / 'client-source/InputOperationLayer.cs'
    inputs[layer.relative_to(ROOT).as_posix()] = sha(layer.read_bytes())
    if files.get(layer.relative_to(ROOT).as_posix()) != sha(layer.read_bytes()):
        raise ValueError('Original input-layer source changed.')
    outputs = {}

    def write(name, content, importer='MonoImporter'):
        path = folder / name
        raw = content.encode('utf8')
        path.write_bytes(raw)
        key = hashlib.md5(('zjcs:home-camera-input:884:' + name).encode()).hexdigest()
        meta = ('fileFormatVersion: 2\nguid: ' + key + '\n' + importer + ':\n  externalObjects: {}\n').encode()
        Path(str(path) + '.meta').write_bytes(meta)
        outputs[name] = sha(raw)
        outputs[name + '.meta'] = sha(meta)

    for name in NAMES:
        path = CODE / 'client-source' / (name + '.cs')
        raw = path.read_text('utf8')
        relative = path.relative_to(ROOT).as_posix()
        inputs[relative] = sha(path.read_bytes())
        if files.get(relative) != inputs[relative]: raise ValueError('Original input source changed: ' + name)
        if name == 'InputOperationBase':
            start = raw.index('\t\t\tGLoggerErrorInterpolatedStringHandler message')
            end = raw.index('\n', raw.index('\t\t\tLog.Error(', start))
            raw = raw[:start] + '\t\t\tUnityEngine.Debug.LogException(exception);' + raw[end:]
        if name == 'IgnoreUIInputOperationBase':
            raw = raw.replace('private IEventSystemManager EventSystemManager', 'public IEventSystemManager EventSystemManager')
        raw = raw.replace('Input.GetMouseButtonDown(', 'CameraPointerInput.GetMouseButtonDown(')
        raw = raw.replace('Input.GetMouseButton(', 'CameraPointerInput.GetMouseButton(')
        raw = raw.replace('Input.mousePosition', 'CameraPointerInput.MousePosition')
        raw = raw.replace('Input.touchCount', 'CameraPointerInput.TouchCount')
        raw = raw.replace('Input.GetTouch(', 'CameraPointerInput.GetTouch(')
        write(name + '.cs', camera.port(raw, name).replace('CameraVectorCast.Convert(', 'CameraInputVectorCast.Convert('))
    write('CameraPointerInput.cs', '''using System;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Zjcs.Official.HomeCamera
{
    // The local host supplies process-local input in tests; default delegates
    // read Unity's own input state in the Windows player.
    public static class CameraPointerInput
    {
        public static Func<int, bool> MouseButtonDown = Input.GetMouseButtonDown;
        public static Func<int, bool> MouseButton = Input.GetMouseButton;
        public static Func<Vector3> Position = () => Input.mousePosition;
        public static Func<int> Touches = () => Input.touchCount;
        public static Func<int, Touch> Touch = Input.GetTouch;
        public static bool GetMouseButtonDown(int button) => MouseButtonDown(button);
        public static bool GetMouseButton(int button) => MouseButton(button);
        public static Vector3 MousePosition => Position();
        public static int TouchCount => Touches();
        public static Touch GetTouch(int index) => Touch(index);
    }
    public interface IEventSystemManager
    {
        bool EventSystemValid { get; }
        bool TryGetFirstRaycastResult(Vector2 screenPos, out RaycastResult result);
    }
    internal static class CameraInputVectorCast
    {
        public static Vector3 Convert(Vector2 value) => value;
        public static Vector2 Convert(Vector3 value) => value;
    }
}
''')
    path = CODE / 'client-source/InputOperationModule.cs'
    raw = camera.clean(path.read_text('utf8'))
    relative = path.relative_to(ROOT).as_posix()
    inputs[relative] = sha(path.read_bytes())
    if files.get(relative) != inputs[relative]: raise ValueError('Original input module source changed.')
    raw = raw.replace(': BackgroundServiceBase, IInputOperationModule, IUpdatable', ': IInputOperationModule')
    raw = raw.replace('\tprivate IServiceProvider mServiceProvider;\n', '')
    constructor = '''\tpublic InputOperationModule(IEnumerable<IInputOperation> inputOperations)
\t{
\t\tif (inputOperations == null) throw new ArgumentNullException(nameof(inputOperations));
\t\tmInputOperations = new List<IInputOperation>();
\t\tmCallbackExecutionBuffer = new Dictionary<Type, List<long>>();
\t\tmCallbacksMatcher = new Dictionary<long, InputOperationCallbackInfo>();
\t\tmSignaturesMatcher = new Dictionary<Delegate, long>();
\t\tmBlockSignatures = new Dictionary<int, HashSet<long>>();
\t\tforeach (InputOperationLayer value in Enum.GetValues(typeof(InputOperationLayer)))
\t\t\tmBlockSignatures.Add(value.GetHashCode(), new HashSet<long>());
\t\tforeach (IInputOperation operation in inputOperations)
\t\t{
\t\t\tmInputOperations.Add(operation);
\t\t\tmCallbackExecutionBuffer.Add(operation.ResultType, new List<long>());
\t\t\tmBlockSignatures.Add(operation.ResultType.GetHashCode(), new HashSet<long>());
\t\t}
\t}'''
    start, end = camera.span(raw, '\tpublic InputOperationModule(IServiceProvider serviceProvider)')
    raw = raw[:start] + constructor + raw[end:]
    start, end = camera.span(raw, '\tprivate void LoadOperations()')
    raw = raw[:start] + raw[end:]
    raw = raw.replace('void IUpdatable.Update()', 'public void Update()')
    raw = raw.replace('void IInputOperationModule.HookOnInput<T>(Action<T> callback)',
                      'public void HookOnInput<T>(Action<T> callback) where T : IInputOperationResult')
    raw = raw.replace('void IInputOperationModule.UnhookOnInput<T>(Action<T> callback)',
                      'public void UnhookOnInput<T>(Action<T> callback) where T : IInputOperationResult')
    raw = raw.replace('long IInputOperationModule.Block<T>()', 'public long Block<T>() where T : IInputOperationResult')
    raw = raw.replace('long IInputOperationModule.Block(InputOperationLayer layer)', 'public long Block(InputOperationLayer layer)')
    raw = raw.replace('void IInputOperationModule.Unblock(long signature)', 'public void Unblock(long signature)')
    raw = raw.replace('mSignaturesMatcher.Remove((Delegate)callback, ref key)', 'mSignaturesMatcher.TryGetValue((Delegate)callback, out key)')
    raw = raw.replace('\t\t\tmCallbacksMatcher.Remove(key);', '\t\t\tmSignaturesMatcher.Remove((Delegate)callback);\n\t\t\tmCallbacksMatcher.Remove(key);')
    pattern = re.compile(r'(?m)^(\s*)(?:GLogger\w+InterpolatedStringHandler\s+)?message\d* = new GLogger\w+InterpolatedStringHandler\(')
    while match := pattern.search(raw):
        marker = raw.index('if (enabled)', match.end())
        _, end = camera.span(raw[marker:], 'if (enabled)'); end += marker
        log_start = raw.index('Log.', end); log_end = raw.index('\n', log_start)
        line = raw[log_start:log_end]
        severity = 'LogError' if line.startswith('Log.Error(') else 'Log'
        raw = raw[:match.start()] + '\n' + match.group(1) + 'UnityEngine.Debug.' + severity + '("InputOperationModule");' + raw[log_end:]
    if any(item in raw for item in ('GLogger', 'ReflectionClassManager', 'IUpdatable', 'mServiceProvider')):
        raise ValueError('Unresolved original input-module adaptation.')
    first = re.search(r'(?m)^(?:\[|public |internal )', raw).start()
    write('InputOperationModule.cs', raw[:first] + 'namespace ' + NAMESPACE + '\n{\n' + raw[first:].rstrip() + '\n}\n')
    write('Zjcs.Official.HomeCameraInput.asmdef', json.dumps(dict(name='Zjcs.Official.HomeCameraInput',
        references=['Zjcs.Official.HomeCamera', 'UnityEngine.UI'], autoReferenced=True), indent=2) + '\n',
        'AssemblyDefinitionImporter')
    proof = dict(manifest='884_156692', inputs=inputs, outputs=outputs,
        scope='Selected original pointer/drag/zoom operations; complete input-module and home host binding remain pending.')
    (work / 'home-camera-input-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Staged original camera input operations:', len(outputs) // 2, flush=True)


if __name__ == '__main__':
    main()
