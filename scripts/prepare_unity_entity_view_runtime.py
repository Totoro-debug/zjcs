"""Migrate original EC view lifecycle and client visibility application code.

Entity data/events, dependency injection, R3 frame subscriptions and map-root
lifetime remain explicit host boundaries. No substitute EC/gameplay is created.
"""
import json
import hashlib
from pathlib import Path
import re

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_story_attachment_runtime import CODE, record
from prepare_unity_story_playable_runtime import logging_adapter
from prepare_unity_story_sound_runtime import runtime_meta

NAMESPACE = 'Zjcs.Official.EntityViews'
OUTPUT = ROOT / 'unity/Assets/Game/Runtime/Official/EntityViews'
RECEIPT = ROOT / 'assets/reference/official-map/map14-entity-view-runtime-source-manifest.json'

BOUNDARY = '''using System;
using UnityEngine;
using Zjcs.Official.StoryPlayables;
namespace Zjcs.Official.EntityViews
{
    // Required source-entity consumption boundary; no entity rules or event
    // dispatch implementation are inferred by this migration.
    public abstract class ECViewEntityData
    {
        public abstract int ClassId { get; }
        public abstract EcEntityIdentifier Id { get; }
        public abstract T GetComponent<T>();
    }
    public abstract class ECViewEntityBinding : MonoBehaviour
    {
        public abstract ECViewEntityData Entity { get; }
        public abstract void SetClientVisibilitySupporter(IClientVisibilitySupporter value);
        public abstract void AddStartReceiver(IMyEntityStartEventReceiver value);
        public abstract void RemoveStartReceiver(IMyEntityStartEventReceiver value);
        public abstract void AddRemoveReceiver(IMyEntityRemoveEventReceiver value);
        public abstract void RemoveRemoveReceiver(IMyEntityRemoveEventReceiver value);
        public abstract void AddStartPostReceiver(IMyEntityStartPostEventReceiver value);
        public abstract void RemoveStartPostReceiver(IMyEntityStartPostEventReceiver value);
        public abstract void AddRemovePreReceiver(IMyEntityRemovePreEventReceiver value);
        public abstract void RemoveRemovePreReceiver(IMyEntityRemovePreEventReceiver value);
    }
    // The official base subscribes to EveryUpdate and PreLateUpdate. Its host
    // supplies those two frame streams and each independent disposable lease.
    public readonly struct ECViewFrameUnit { }
    public interface IECViewFrameSource
    {
        IDisposable SubscribeUpdate(Action<ECViewFrameUnit> callback);
        IDisposable SubscribePreLateUpdate(Action<ECViewFrameUnit> callback);
    }
    public interface IECViewInjection { void Inject(object value); }
    public interface IECViewMapRootLifetime { bool Destroying { get; } }
    internal static class ECViewServices
    {
        public static void Inject(IServiceProvider provider, object value)
        {
            if (!(provider is IECViewInjection injection))
                throw new InvalidOperationException("Original EC view dependency injection is not bound.");
            injection.Inject(value);
        }
        public static IECViewFrameSource Frames(IServiceProvider provider) =>
            provider.GetService(typeof(IECViewFrameSource)) as IECViewFrameSource ??
            throw new InvalidOperationException("Original EC view frame streams are not bound.");
    }
}
'''


def build():
    manifest = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    assemblies = [CODE / 'managed/decoded' / name for name in
                  ('Assembly-CSharp.dll', 'Framework.FirstPass.Runtime.dll', 'Console.dll')]
    for path in assemblies:
        if sha(path.read_bytes()) != manifest['assemblies'][path.name]['decoded_sha256']:
            raise ValueError('Selected original EC view assembly changed')
    names = ('ECViewDriver', 'IECViewDriver', 'IECViewBridge', 'IECView', 'ECViewBase',
             'ECVisibleControlerViewBase', 'ECVisibleControlerNormalView',
             'IMyEntityViewModelControler', 'MyEntityViewModelControlerUtils',
             'IClientVisibilitySupporter', 'IClientVisibilitySupporterFactory',
             'ClientVisibilitySupporterBase', 'MapECViewClientVisibilitySupporter',
             'MapClientVisibilitySupporterFactory', 'ConstantClientVisibilitySupporter',
             'ConstantClientVisibilitySupporterFactory')
    sources = [CODE / 'client-source' / (name + '.cs') for name in names]
    sources += [CODE / 'client-source/Common' / (name + '.cs') for name in
                ('IMyEntityStartEventReceiver', 'IMyEntityRemoveEventReceiver',
                 'IMyEntityStartPostEventReceiver', 'IMyEntityRemovePreEventReceiver')]
    sources += [CODE / 'framework-firstpass-source/Framework' / (name + '.cs')
                for name in ('IUpdatable', 'ILateUpdatable')]
    sources += [CODE / 'common-source/Common/IEventReceiver.cs']
    outputs, adaptations = [], []
    for path in sources:
        text = path.read_text(encoding='utf-8-sig')
        text = re.sub(r'(?m)^using (?:Common|GameFramework|Framework(?:\.[\w.]+)?|R3|Cysharp.Threading.Tasks.CompilerServices);\n', '', text)
        text = re.sub(r'(?m)^namespace (?:Common|Framework);\n', '', text)
        if path.stem == 'ECViewDriver':
            first = text.index('\t[StructLayout(LayoutKind.Auto)]')
            last = text.index('\tprivate static Comparison<IECView>')
            state_machine = text[first:last]
            for evidence in ('WithCancellation(execution, cancellationToken)', 'SuppressCancellationThrow()',
                             '((IECViewBridge)eCViewDriver).StopCoroutine(signature);'):
                if evidence not in state_machine: raise ValueError('Original driver async body changed')
            text = text[:first] + text[last:]
            first = text.index('\t[AsyncStateMachine(')
            last = text.index('\n\tpublic T GetView<T>()', first)
            text = text[:first] + '''\tprivate async UniTask RunTask(long signature, IEnumerator execution, CancellationToken cancellationToken)
    {
        try { await execution.WithCancellation(cancellationToken).SuppressCancellationThrow(); }
        catch (Exception exception) { UnityEngine.Debug.LogError("ECViewDriver RunTask: " + exception); }
        ((IECViewBridge)this).StopCoroutine(signature);
    }
''' + text[last:]
        text, blocks = logging_adapter(text)
        text = re.sub(r'\bMyEntity\b', 'ECViewEntityBinding', text)
        text = re.sub(r'\bEcEntity\b', 'ECViewEntityData', text)
        text = text.replace('IMapRootManagerProxy', 'IECViewMapRootLifetime')
        text = re.sub(r'(?m)^\s*\[Inject\]\n', '\n', text)
        text = text.replace('private IServiceProvider ServiceProvider { get; set; }',
                            'public IServiceProvider ServiceProvider { get; set; }')
        text = text.replace('private IClientVisibilitySupporterFactory ClientVisibilitySupporterFactory { get; set; }',
                            'public IClientVisibilitySupporterFactory ClientVisibilitySupporterFactory { get; set; }')
        text = text.replace('private IECViewMapRootLifetime MapRootManagerProxy { get; set; }',
                            'public IECViewMapRootLifetime MapRootManagerProxy { get; set; }')
        text = text.replace('ServiceProviderExtensions.Inject', 'ECViewServices.Inject')
        text = text.replace('mViewCoroutines.Remove(coroutineId, ref cancellationTokenSource)',
                            'mViewCoroutines.Remove(coroutineId, out cancellationTokenSource)')
        events = {'StartEvent': 'Start', 'RemoveEvent': 'Remove',
                  'StartPostEvent': 'StartPost', 'RemovePreEvent': 'RemovePre'}
        for name, mapped in events.items():
            text = re.sub(r'(RegisterEntity|mOwner)\.' + name + r' (\+=|-=) \(([^)]+)\)this;',
                          lambda m: m[1] + '.' + ('Add' if m[2] == '+=' else 'Remove') +
                          mapped + 'Receiver((' + m[3] + ')this);', text)
        if path.stem == 'ECViewBase':
            text = text.replace('Action<Unit>', 'Action<ECViewFrameUnit>').replace('Proxy(Unit _)', 'Proxy(ECViewFrameUnit _)')
            text = text.replace('ObservableSubscribeExtensions.Subscribe<Unit>(Observable.EveryUpdate(), (Action<ECViewFrameUnit>)UpdateProxy)',
                                'ECViewServices.Frames(mServiceProvider).SubscribeUpdate(UpdateProxy)')
            text = text.replace('ObservableSubscribeExtensions.Subscribe<Unit>(Observable.EveryUpdate(UnityFrameProvider.PreLateUpdate), (Action<ECViewFrameUnit>)LateUpdateProxy)',
                                'ECViewServices.Frames(mServiceProvider).SubscribePreLateUpdate(LateUpdateProxy)')
            if 'Observable' in text: raise ValueError('Unbound original frame-stream call')
        start = re.search(r'(?m)^(?:\[DefaultExecutionOrder\(-1\)\]|public (?:(?:abstract|static) )?(?:class|interface))', text)
        if not start: raise ValueError('Original EC view declaration missing: ' + path.name)
        text = ('// Migrated official application source; see EC view lifecycle receipt.\n'
                'using Object = UnityEngine.Object;\nusing Zjcs.Official.StoryPlayables;\n'
                'using Zjcs.Official.StorySourceFade;\n' + text[:start.start()] +
                'namespace ' + NAMESPACE + '\n{\n' + text[start.start():] + '\n}\n')
        target = OUTPUT / path.name
        text = re.sub(r'(?m)^\s*//IL_[^\n]*\n', '', text)
        write(target, text.encode())
        if path.stem == 'ECViewDriver':
            metadata = Path(str(target) + '.meta')
            guid = hashlib.md5(('zjcs-official:' + target.relative_to(ROOT / 'unity').as_posix()).encode()).hexdigest()
            raw = ('fileFormatVersion: 2\nguid: ' + guid + '\nMonoImporter:\n  externalObjects: {}\n'
                   '  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: -1\n'
                   '  icon: {instanceID: 0}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()
            if metadata.exists() and metadata.read_bytes().replace(b'\r\n', b'\n') != raw:
                raise ValueError('Unrelated original EC driver metadata')
            write(metadata, raw)
        else:
            runtime_meta(target)
        outputs += [target, Path(str(target) + '.meta')]
        adaptations.append(dict(source=path.relative_to(CODE).as_posix(), diagnosticBlocks=blocks))
    target = OUTPUT / 'ECViewEntityBinding.cs'; write(target, BOUNDARY.encode()); runtime_meta(target)
    outputs += [target, Path(str(target) + '.meta')]
    asmdef = OUTPUT / (NAMESPACE + '.asmdef')
    write(asmdef, encode(dict(name=NAMESPACE, rootNamespace=NAMESPACE,
          references=['Zjcs.Official.StoryPlayables', 'Zjcs.Official.StorySourceFade', 'UniTask'], autoReferenced=True)))
    runtime_meta(asmdef); runtime_meta(OUTPUT)
    outputs += [asmdef, Path(str(asmdef) + '.meta'), Path(str(OUTPUT) + '.meta')]
    write(RECEIPT, encode(dict(manifest='871_156598', mapClassId=14,
        assemblies=[record(p) for p in assemblies], inputs=[record(p) for p in sources],
        tools=[record(Path(__file__)), record(ROOT / 'scripts/prepare_unity_story_playable_runtime.py'),
               record(ROOT / 'scripts/prepare_unity_story_sound_runtime.py')],
        outputs=[record(p) for p in outputs], adaptations=adaptations,
        operation='Copy original ECViewDriver, view base/interfaces, normal visibility controller, map/constant visibility supporters/factories and event receiver contracts. Restore RunTask from its actual MoveNext. Preserve append traversal, sort/inject/init order, register/event/load/show/hide/pause/unload order, coroutine signatures/cancellation, original null asymmetries, per-key hide requests, force visibility, map-destroy suppression, child activation and fade reset. Only consumed entity data/event operations, injection, R3 update/pre-late-update subscription and map-root lifetime are explicit required host boundaries.',
        limitations=['Full entity data/event publisher, R3 stream provider and dependency-container integration are not implemented by abstract host boundaries. Original managed-reference view subclasses, prefab binding, formal map/gameplay/input/save and Windows Player remain required.'])))
    print('Original EC view lifecycle/visibility migrated:', len(sources), 'source files.')


if __name__ == '__main__': build()
