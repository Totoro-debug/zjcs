"""Migrate original MyEntity/component/transform lifecycle and native delegates."""
import json
import re

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_story_attachment_runtime import CODE, record
from prepare_unity_story_playable_runtime import logging_adapter
from prepare_unity_story_sound_runtime import runtime_meta

NAMESPACE = 'Zjcs.Official.EntityLifecycle'
OUTPUT = ROOT / 'unity/Assets/Game/Runtime/Official/EntityLifecycle'
RECEIPT = ROOT / 'assets/reference/official-map/map14-entity-lifecycle-source-manifest.json'

BOUNDARIES = '''using System;
using UnityEngine;
using Zjcs.Official.EntityViews;
using Zjcs.Official.EntityTransformViews;
namespace Zjcs.Official.EntityLifecycle
{
    // Consumed original container/component metadata. A formal host must bind
    // original shared rules and the root entity; these types implement none.
    public abstract class MyContainer : MonoBehaviour
    {
        public abstract IECViewMapRootLifetime Container { get; }
        public abstract MyEntity MyRootEntity { get; }
        public abstract ILogger Logger { get; }
        public static ILogger CreateLogger(string category) => Debug.unityLogger;
    }
    public abstract class EntityGridGroundData { }
    public interface IEntityComponentInfoSource { object ComponentInfo { get; } }
    public static class EntityLifecycleServices
    {
        public static object Info(object component)
        {
            if (component is ECGridTransformData grid) return grid.Info;
            return (component as IEntityComponentInfoSource ??
                throw new InvalidOperationException("Original shared component info is not bound.")).ComponentInfo;
        }
    }
}
'''


def build():
    manifest = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    assemblies = [CODE / 'managed/decoded' / name for name in ('Assembly-CSharp.dll', 'Console.dll')]
    for path in assemblies:
        if sha(path.read_bytes()) != manifest['assemblies'][path.name]['decoded_sha256']:
            raise ValueError('Original entity lifecycle assembly changed')
    event_receipt = ROOT / 'assets/reference/official-map/map13-entity-events-source-manifest.json'
    events = json.loads(event_receipt.read_bytes())
    if events['manifest'] != '871_156598':
        raise ValueError('Original event dispatcher version differs')
    dependencies = []
    for row in events['outputs']:
        path = ROOT / row['path']
        if path.suffix == '.cs':
            if sha(path.read_bytes()) != row['sha256']:
                raise ValueError('Reused original event dispatcher changed')
            dependencies.append(record(path))
    sources = [CODE / 'client-source' / (name + '.cs') for name in
               ('MyEntity', 'MyComponent', 'MyTransform', 'MyGridTransform', 'IMyVisibility', 'IEmptyInfoMyComponent')]
    for name in ('BindPre', 'Bind', 'Start', 'StartPost', 'RemovePre', 'Remove', 'PrepareReplace', 'Replace'):
        sources += [CODE / 'client-source/Common' / ('MyEntity' + name + 'EventDelegate.cs'),
                    CODE / 'client-source/Common' / ('IMyEntity' + name + 'EventReceiver.cs')]
    outputs, changes = [], []
    for path in sources:
        text = path.read_text(encoding='utf-8-sig')
        text = re.sub(r'(?m)^using (?:Common|Framework(?:\.[\w.]+)?|Microsoft.Extensions.Logging|GFrameCore.Extensions.ZLogger|ZLogger);\n', '', text)
        text = text.replace('namespace Common;\n', '')
        text = re.sub(r'(?m)^\s*//IL_[^\n]*\n', '\n', text)
        text = re.sub(r'\bEcEntity\b', 'ECViewEntityData', text)
        text = re.sub(r'\bMySerializable\b', 'object', text)
        if path.stem == 'MyEntity':
            text = text.replace('TryGetComponent<MyTransform>(ref _Transform)', 'TryGetComponent<MyTransform>(out _Transform)')
            text = text.replace('TryGetComponent<MyGridTransform>(ref _GridTransform)', 'TryGetComponent<MyGridTransform>(out _GridTransform)')
        elif path.stem == 'MyComponent':
            text = text.replace('MyComponent<T, EcComponentInfoEmpty>', 'MyComponent<T, object>')
            text = text.replace(' where T : EcComponent', '').replace(' where TInfo : EcComponentInfo', '')
            text = re.sub(r'\bEcComponent\b', 'object', text)
            text = text.replace('(TInfo)Component._Info', '(TInfo)EntityLifecycleServices.Info(Component)')
        elif path.stem == 'MyGridTransform':
            text = text.replace('GridTransformComponentInfo', 'ECGridTransformInfo').replace('GridTransformComponent', 'ECGridTransformData')
            text = text.replace('GridGroundComponent', 'EntityGridGroundData').replace('MoveAgent', 'ECViewMoveAgent')
            text = text.replace('Component.PosUpdatedEvent += (IGridTransformPosUpdatedEventReceiver)this;', 'Component.AddPosUpdatedReceiver(this);')
            text = text.replace('Component.PosUpdatedEvent -= (IGridTransformPosUpdatedEventReceiver)this;', 'Component.RemovePosUpdatedReceiver(this);')
            # Preserve the catch position/recovery; adapt only the expanded logger.
            text, count = re.subn(r'catch \(Exception ex\)\n\t\t\{.*?\n\t\t\}',
                                 'catch (Exception ex)\n\t\t{\n\t\t\tUnityEngine.Debug.LogException(ex);\n\t\t}', text, flags=re.S)
            if count != 1:
                raise ValueError('Original legacy grid diagnostic changed')
        elif path.stem == 'MyTransform':
            text = text.replace('TransformComponent', 'ECTransformData')
        text, diagnostics = logging_adapter(text)
        first = re.search(r'(?m)^public ', text).start()
        imports = ('// Complete original client lifecycle class; see entity lifecycle source receipt.\n'
                   'using Object = UnityEngine.Object;\nusing Zjcs.Official.EntityViews;\n'
                   'using Zjcs.Official.EntityTransformViews;\nusing Zjcs.Official.StoryPlayables;\n'
                   'using Zjcs.Official.World.NativeTerrain.Common;\n'
                   'using Zjcs.Official.World.Map13EntityEvents;\n'
                   'using IEventReceiver = Zjcs.Official.World.Map13EntityEvents.IEventReceiver;\n')
        text = imports + text[:first] + 'namespace ' + NAMESPACE + '\n{\n' + text[first:] + '\n}\n'
        target = OUTPUT / path.name
        write(target, text.encode('utf-8')); runtime_meta(target); outputs += [target, target.with_suffix('.cs.meta')]
        changes.append(dict(source=path.name, diagnosticBlocks=diagnostics))
    target = OUTPUT / 'EntityLifecycleBindings.cs'
    write(target, BOUNDARIES.encode('utf-8')); runtime_meta(target); outputs += [target, target.with_suffix('.cs.meta')]
    asmdef = OUTPUT / (NAMESPACE + '.asmdef')
    write(asmdef, encode(dict(name=NAMESPACE, rootNamespace=NAMESPACE, autoReferenced=False,
          references=['Zjcs.Official.EntityViews','Zjcs.Official.EntityTransformViews','Zjcs.Official.StoryPlayables',
                      'Zjcs.Official.World.NativeTerrain','Zjcs.Official.World.Map13EntityEvents'])))
    runtime_meta(asmdef); runtime_meta(OUTPUT); outputs += [asmdef, asmdef.with_suffix('.asmdef.meta'), OUTPUT.with_suffix('.meta')]
    bridge = ROOT / 'unity/Assets/Game/Runtime/Offline/World/NativeMap14EntityLifecycle'
    for path in sorted(bridge.iterdir()):
        if path.is_file() and path.suffix != '.meta':
            runtime_meta(path)
            outputs += [path, path.with_suffix(path.suffix + '.meta')]
    runtime_meta(bridge); outputs.append(bridge.with_suffix('.meta'))
    write(RECEIPT, encode(dict(manifest='871_156598', mapClassId=14, assemblies=[record(p) for p in assemblies],
        inputs=[record(p) for p in sources], dependencies=dependencies+[record(event_receipt)], adaptations=changes,
        tools=[record(ROOT / 'scripts/prepare_unity_entity_lifecycle_runtime.py')], outputs=[record(p) for p in outputs],
        operation='Copy complete MyEntity, MyComponent including generic bases, MyTransform, MyGridTransform, native visibility/empty-info interfaces and eight original event receiver/delegate pairs. Reuse the verified original event dispatcher. Preserve BindPre before assigning entity/container/new flag, ordered component bind/start/end/remove/immediate loops, event order and exception recovery, cached component lookup, fade wait, replacement and pool reset. Preserve the original OnDestroy omission of RemovePreEvent.Clear. Shared entity/container/component-info/grid data are explicit required inputs; wire metadata and expanded logging adapt to existing Windows interfaces. No state/rule transitions are invented.',
        limitations=['Formal native entity prefab roots/shared rules, map input/UI/save and Player remain required. The separate Windows adapter forwards lifecycle interfaces to these original delegates.'])) )
    print('Complete original entity/component/legacy transform lifecycle migrated:',len(sources),'source files')


if __name__ == '__main__':
    build()
