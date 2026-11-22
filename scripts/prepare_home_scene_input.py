"""Port selected native furniture click operations without replacing their hit rules."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from official_source import OfficialSource


DEST = ROOT / 'unity/Assets/Game/Runtime/Official/HomeSceneInput'
RECEIPT = ROOT / 'assets/reference/home-scene-input-source-manifest.json'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def prepare():
    source = OfficialSource()
    code = ROOT / '.tools/source-packages/current' / source.version / 'official-code'
    receipt = code.parent / 'code-manifest.json'
    identity = json.loads(receipt.read_bytes())
    assembly = code / 'managed/decoded/Assembly-CSharp.dll'
    if identity['manifest'] != source.version or sha(assembly.read_bytes()) != identity['assemblies']['Assembly-CSharp.dll']['decoded_sha256']:
        raise ValueError('Selected native scene input assembly differs')
    inputs = {p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in (receipt, assembly)}
    previous = json.loads(RECEIPT.read_bytes())['outputs'] if RECEIPT.exists() else {}
    outputs = {}

    def write(path, raw):
        name = path.relative_to(ROOT).as_posix()
        if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get(name):
            raise ValueError('Preserve another scene-input change: ' + name)
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)
        outputs[name] = sha(raw)

    def metadata(path, folder=False):
        token = hashlib.md5(('zjcs:home-scene-input:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        kind = 'DefaultImporter' if folder else 'AssemblyDefinitionImporter' if path.suffix == '.asmdef' else 'MonoImporter'
        raw = 'fileFormatVersion: 2\nguid: ' + token + '\n'
        if folder:
            raw += 'folderAsset: yes\n'
        raw += kind + ':\n  externalObjects: {}\n'
        write(Path(str(path) + '.meta'), raw.encode())

    def read(name, resolved=False):
        path = code / ('client-source-resolved' if resolved else 'client-source') / (name + '.cs')
        raw = path.read_bytes()
        inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
        if resolved:
            provenance = path.with_suffix('.decompilation.json')
            proof = json.loads(provenance.read_bytes())
            if proof['sha256'] != sha(raw):
                raise ValueError('Resolved native source differs: ' + name)
            for relative, expected in proof['inputs'].items():
                if sha((ROOT / relative).read_bytes()) != expected:
                    raise ValueError('Resolved source input differs: ' + relative)
                inputs[relative] = expected
            inputs[provenance.relative_to(ROOT).as_posix()] = sha(provenance.read_bytes())
        text = raw.decode('utf-8-sig').replace('\r\n', '\n')
        text = re.sub(r'(?m)^using [^\n]+;\n|^\s*//IL_[^\n]*\n|^\s*\[CompilerGenerated\]\n|^\s*\[Inject\]\n', '', text)
        # Unity's vector casts and decompiler backing fields are syntax only.
        text = re.sub(r'(public Vector2 \w+)\n\t\{.*?\n\t\}', r'\1 { get; private set; }', text, flags=re.S)
        return text

    def replace(text, old, new):
        if text.count(old) != 1:
            raise ValueError('Original scene-input boundary changed: ' + old)
        return text.replace(old, new)

    def body(text, signature):
        start = text.index('{', text.index(signature))
        depth = 1
        end = start + 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}')
            end += 1
        return re.sub(r'\s+', '', text[start:end])

    # New operations reuse the installed dispatcher rather than porting a
    # second copy. Verify the selected pointer-down method against that copy.
    down = read('GlobalPointerDownOperation')
    down = down.replace('Input.GetMouseButtonDown(0)', 'pointer.LeftDown')
    down = down.replace('Vector2.op_Implicit(Input.mousePosition)', 'pointer.Position')
    shared_input = ROOT / 'unity/Assets/Game/Runtime/Official/HomeMainUiControls/HomePointerInput.cs'
    installed_down = shared_input.read_text('utf-8-sig').split('public class GlobalPointerDownOperation', 1)[1]
    if body(down, 'public override bool UpdateResult()') != body(installed_down, 'public override bool UpdateResult()'):
        raise ValueError('Selected scene input and installed pointer-down behavior differ')

    header = '''// Original selected native scene input; explicit pointer, camera and UI owners.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.HomeSceneInput
{
using Object = UnityEngine.Object;
'''

    def emit(name, text):
        text = '\n'.join(line.rstrip() for line in (header + text + '\n}\n').split('\n'))
        if any(token in text for token in ('field;', 'Vector2.op_', 'Framework.', '[Inject]', 'Camera.main', 'Input.GetMouse')):
            raise ValueError('Unbound native scene input: ' + name)
        path = DEST / (name + '.cs')
        write(path, text.encode())
        metadata(path)

    text = read('OnGlobalClickResult')
    text = text.replace('Vector2.op_Implicit(screenPos)', '(Vector2)screenPos').replace('Vector2.op_Implicit(result.ScreenPos)', '(Vector3)result.ScreenPos')
    emit('OnGlobalClickResult', text)
    emit('GameObjectInputResult', read('GameObjectInputResult'))
    emit('OnGameObjectClickResult', read('OnGameObjectClickResult'))

    text = read('GlobalClickOperation')
    text = replace(text, 'private GlobalPointerDownOperation mGlobalPointerDownOperation = new GlobalPointerDownOperation();',
        '''private readonly GlobalPointerDownOperation mGlobalPointerDownOperation;
    private readonly IHomeSceneClickPointer clickPointer;
    public GlobalClickOperation(IHomeSceneClickPointer pointer, Action<Exception> reportException)
        : base(pointer, reportException)
    {
        clickPointer = pointer;
        mGlobalPointerDownOperation = new GlobalPointerDownOperation(pointer, reportException);
    }''')
    text = text.replace('Input.mousePosition', '(Vector3)pointer.Position').replace('Input.GetMouseButtonUp(0)', 'clickPointer.LeftUp')
    emit('GlobalClickOperation', text)

    text = read('IgnoreUIInputOperationBase')
    text = replace(text, 'private IEventSystemManager EventSystemManager { get; set; }',
        '''protected readonly IHomeSceneInputWorld world;
    protected IgnoreUIInputOperationBase(IHomeSceneClickPointer pointer, Action<Exception> reportException,
        IHomeSceneInputWorld world) : base(pointer, reportException)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }''')
    text = text.replace('EventSystemManager.', 'world.')
    emit('IgnoreUIInputOperationBase', text)

    text = read('GameObjectInputOperationBase')
    text = replace(text, 'public override InputOperationLayer Layer', '''protected GameObjectInputOperationBase(IHomeSceneClickPointer pointer, Action<Exception> reportException,
        IHomeSceneInputWorld world) : base(pointer, reportException, world) { }
    public override InputOperationLayer Layer''')
    text = text.replace('Camera.main', 'world.Camera')
    text = text.replace('Vector2.op_Implicit(world.Camera.ScreenToWorldPoint(Vector2.op_Implicit(screenPos)))', '(Vector2)world.Camera.ScreenToWorldPoint((Vector3)screenPos)')
    text = text.replace('Vector2.op_Implicit(screenPos)', '(Vector3)screenPos').replace('Vector2.op_Implicit(val.point)', '(Vector2)val.point')
    text = text.replace('ref val, 1000f', 'out val, 1000f')
    emit('GameObjectInputOperationBase', text)

    text = read('GameObjectClickOperation')
    text = replace(text, 'private GlobalPointerDownOperation mGlobalPointerDownOperation = new GlobalPointerDownOperation();',
        'private readonly GlobalPointerDownOperation mGlobalPointerDownOperation;')
    text = replace(text, 'private GlobalClickOperation mGlobalClickOperation = new GlobalClickOperation();',
        '''private readonly GlobalClickOperation mGlobalClickOperation;
    public GameObjectClickOperation(IHomeSceneClickPointer pointer, Action<Exception> reportException,
        IHomeSceneInputWorld world) : base(pointer, reportException, world)
    {
        mGlobalPointerDownOperation = new GlobalPointerDownOperation(pointer, reportException);
        mGlobalClickOperation = new GlobalClickOperation(pointer, reportException);
    }''')
    emit('GameObjectClickOperation', text)

    text = read('ClickableHomeObj', resolved=True)
    text = text.replace('protected IInputOperationModule InputOperationModule { get; set; }',
        'public IInputOperationModule InputOperationModule { get; set; }')
    text = text.replace('IHomeData', 'IHomeSceneClickContext').replace('private IHomeSceneClickContext HomeData', 'public IHomeSceneClickContext HomeData')
    text = text.replace('StoryUtils.IsAllNoTipWinClosed()', 'HomeData.AllNoTipWindowsClosed')
    emit('ClickableHomeObj', text)

    emit('HomeSceneInputPorts', '''public interface IHomeSceneClickPointer : IHomePointerSnapshot
{
    bool LeftUp { get; }
}
public interface IHomeSceneInputWorld
{
    Camera Camera { get; }
    bool EventSystemValid { get; }
    bool TryGetFirstRaycastResult(Vector2 screenPos, out RaycastResult result);
}
public interface IHomeSceneClickContext
{
    bool InMyHome { get; }
    bool AllNoTipWindowsClosed { get; }
}
''')
    definition = DEST / 'Zjcs.Official.HomeSceneInput.asmdef'
    write(definition, (json.dumps(dict(name='Zjcs.Official.HomeSceneInput', references=[
        'Unity.ugui', 'Zjcs.Official.HomeMainUiControls', 'Zjcs.Official.ProfessionUI']), indent=2) + '\n').encode())
    metadata(definition)
    metadata(DEST, True)
    offline = ROOT / 'unity/Assets/Game/Runtime/Offline/HomeSceneInput'
    definition = offline / 'Zjcs.Offline.HomeSceneInput.asmdef'
    write(definition, (json.dumps(dict(name='Zjcs.Offline.HomeSceneInput', references=[
        'Unity.ugui', 'Zjcs.Official.HomeMainUiControls', 'Zjcs.Official.HomeSceneInput']), indent=2) + '\n').encode())
    metadata(definition)
    metadata(offline / 'OfflineHomeSceneInput.cs')
    metadata(offline, True)
    read('EventSystemManager')
    for relative in ('unity/Assets/Game/Runtime/Official/HomeMainUiControls/HomePointerInput.cs',
                     'unity/Assets/Game/Runtime/Official/ProfessionUI/SafeAction.cs'):
        inputs[relative] = sha((ROOT / relative).read_bytes())
    validation = ROOT / 'unity/Assets/Game/Editor/HomeSceneInputValidation.cs'
    metadata(validation)
    proof = dict(manifest=source.version, inputs=inputs, outputs=outputs,
        validation_inputs={validation.relative_to(ROOT).as_posix(): sha(validation.read_bytes())},
        runtime_bindings={path.relative_to(ROOT).as_posix(): sha(path.read_bytes())
            for path in sorted(offline.iterdir()) if path.is_file()},
        generator_sha256=sha(Path(__file__).read_bytes()), adaptations=[
            'Existing original input dispatcher, inclusive screen bounds, pointer-down operation and SafeAction are reused.',
            'Mouse-up, main camera, EventSystem raycast and own-home/window state are supplied by explicit local owners.',
            'Original 20-pixel drag exclusion, saved down-screen position, target identity recheck, UI occlusion, 1000-unit Physics raycast, and press-time result are retained.',
            'Original collider collection, click callback before valid action, eligibility branches and destruction unhook are retained.',
            'Decompiler backing fields, implicit vector casts and out-parameter syntax repaired for Unity C#.'
        ], scope='Shared original native furniture input and clickable base. Native model/actor binding, Unity runtime validation and Windows delivery remain separate.')
    RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Restored original native home scene input:', len(outputs), 'outputs')


if __name__ == '__main__':
    prepare()
