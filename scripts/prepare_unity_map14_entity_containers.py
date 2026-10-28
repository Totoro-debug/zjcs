"""Migrate the original client container and both client entity handlers."""
import json
import re
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_story_sound_runtime import runtime_meta
from prepare_unity_map14_entity_loading import diagnostics

CODE = ROOT / '.tools/source-packages/current/871_156598/official-code'
OUTPUT = ROOT / 'unity/Assets/Game/Runtime/Official/EntityContainers'
RECEIPT = ROOT / 'assets/reference/official-map/map14-entity-containers-source-manifest.json'

def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes()))

def prepare():
    manifest = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    if manifest['manifest'] != '871_156598':
        raise ValueError('Original client container source version differs')
    assembly = CODE / 'managed/decoded/Assembly-CSharp.dll'
    if sha(assembly.read_bytes()) != manifest['assemblies'][assembly.name]['decoded_sha256']:
        raise ValueError('Original client container assembly changed')
    sources = [CODE / 'client-source' / (name + '.cs') for name in
               ('MyContainer', 'MyClientEntityHandler', 'MyClientStoryNPCEntityHandler', 'IMyContainerHandler', 'IMyMapEntityLoadHandler')]
    for name in ('Add', 'Remove', 'PrepareReplace', 'Replace'):
        sources += [CODE / 'client-source/Common' / ('MyContainer' + name + 'EntityEventDelegate.cs'),
                    CODE / 'client-source/Common' / ('IMyContainer' + name + 'EntityEventReceiver.cs')]
    outputs, adaptations = [], []
    for path in sources:
        container_diagnostics = 0
        text = path.read_text(encoding='utf-8-sig')
        text = re.sub(r'(?m)^using System;\n', '', text)
        text = re.sub(r'(?m)^using (?:Common|Framework(?:\.[\w.]+)?|Microsoft.Extensions.Logging|GFrameCore.Extensions.ZLogger|ZLogger);\n', '', text)
        text = text.replace('namespace Common;\n', '')
        text = re.sub(r'(?m)^\s*//IL_[^\n]*\n', '\n', text)
        if path.stem == 'MyContainer':
            text = text.replace('public class MyContainer : MonoBehaviour, IEcContainerStartEntityEventReceiver, IEventReceiver, IEcContainerDestroyEntityEventReceiver, IEcContainerReplaceEntityEventReceiver, IEcContainerPrepareReplaceEntityEventReceiver',
                                'public class MyContainer : Zjcs.Official.EntityLifecycle.MyContainer, IEntityLoadContainer')
            text = re.sub(r'\bContainer\b', 'SharedContainer', text)
            text = text.replace('public EcContainer SharedContainer;', '''public IClientEntityContainer SharedContainer;
    public IClientEntityServices SharedServices { get; set; }
    public override IECViewMapRootLifetime Container => SharedContainer;
    MonoBehaviour IEntityLoadContainer.Host => this;
    Zjcs.Official.EntityLifecycle.MyContainer IEntityLoadContainer.LifecycleContainer => this;
    IEntityLoadSharedContainer IEntityLoadContainer.Container => SharedContainer;
    Transform IEntityLoadContainer.EntitySlot => myEntitySlot;
    private IDisposable startSubscription, destroySubscription, prepareSubscription, replaceSubscription;''')
            text = text.replace('[Inject]\n\tprivate IMyContainerHandler MyContainerHandler', 'public IMyContainerHandler MyContainerHandler')
            text = text.replace('public ILogger Logger =>', 'public override ILogger Logger =>')
            text = text.replace('public MyEntity MyRootEntity { get; private set; }', 'private MyEntity mMyRootEntity;\n\tpublic override MyEntity MyRootEntity => mMyRootEntity;')
            text = text.replace('MyRootEntity = ', 'mMyRootEntity = ')
            text = text.replace('public static ILogger CreateLogger', 'public new static ILogger CreateLogger')
            text = text.replace('return Log.CreateLogger(categoryName);', 'return Debug.unityLogger;')
            mapping = [('StartEntity', 'StartEntity', 'startSubscription'),
                       ('DestroyEntity', 'DestroyEntity', 'destroySubscription'),
                       ('PrepareReplaceEntity', 'PrepareReplaceEntity', 'prepareSubscription'),
                       ('ReplaceEntity', 'ReplaceEntity', 'replaceSubscription')]
            callbacks = dict(StartEntity='Start', DestroyEntity='Destroy', PrepareReplaceEntity='PrepareReplace', ReplaceEntity='Replace')
            for event, interface, subscription in mapping:
                text = text.replace('SharedContainer.' + event + 'Event += (IEcContainer' + interface + 'EventReceiver)this;',
                                    subscription + ' = SharedContainer.Subscribe' + event + '(Container_OnEntity' + callbacks[event] + ');')
                text = text.replace('SharedContainer.' + event + 'Event -= (IEcContainer' + interface + 'EventReceiver)this;',
                                    subscription + '?.Dispose();')
            text = re.sub(r'void IEcContainer\w+EventReceiver\.', 'public void ', text)
            text = text.replace('mMyEntityDict.Remove(preEntId, ref myEntity)', 'mMyEntityDict.Remove(preEntId, out myEntity)')
            # Only the two expanded ZLogger diagnostic blocks change.
            text, count = re.subn(r'(?m)^(?P<i>\s*)ILogger logger = Logger;\n.*?^(?P=i)GZLoggerExtensions.Error\([^\n]+;',
                                 lambda m: m['i'] + 'Debug.LogError("Original client container diagnostic");', text, flags=re.S)
            if count != 2:
                raise ValueError('Original container diagnostic shape changed')
            container_diagnostics = count
        elif path.stem in ('MyClientEntityHandler', 'MyClientStoryNPCEntityHandler'):
            text = text.replace('Parsers.Instance.EcDataCenterParser.GetCommonPrefab(classId)', 'myContainer.SharedServices.GetCommonPrefab(classId)')
            text = text.replace('EntityUtils.InstantiateEntity(commonPrefab)', 'myContainer.SharedServices.Instantiate(commonPrefab)')
            text = text.replace('ecEntity.Container = myContainer.Container;', 'myContainer.SharedServices.SetContainer(ecEntity, myContainer.SharedContainer);')
            text = text.replace('ecEntity.Id = NewClientEntityID();', 'myContainer.SharedServices.SetIdentifier(ecEntity, NewClientEntityID());')
            text = text.replace('ecEntity.Awake();', 'myContainer.SharedServices.Awake(ecEntity);')
            text = text.replace('EntitySourceComponent component = entity.GetComponent<EntitySourceComponent>();',
                                'IClientEntitySource component = myContainer.SharedServices.Source(entity);')
            text = text.replace('GridTransformComponent component2 = entity.GetComponent<GridTransformComponent>();',
                                'IClientEntityGrid component2 = myContainer.SharedServices.Grid(entity);')
            text = text.replace('MeManager.MainPlayer.Id', 'myContainer.SharedServices.MainPlayerId')
            text = text.replace('TryGetComponent<MyTransform>(ref myTransform)', 'TryGetComponent<MyTransform>(out myTransform)')
            text = text.replace('\t\tMoveAgent moveAgent = default;\n', '')
            text = text.replace('!((Component)ent).gameObject.TryGetComponent<MoveAgent>(ref moveAgent)', '!myContainer.SharedServices.HasMoveAgent(ent)')
            text = text.replace('((Component)ent).gameObject.AddComponent<MoveAgent>();', 'myContainer.SharedServices.AddMoveAgent(ent);')
        elif path.stem == 'IMyContainerHandler':
            text = text.replace('GridGroundComponent', 'EntityGridGroundData')
        text = re.sub(r'\bEcContainer\b', 'IClientEntityContainer', text)
        text = re.sub(r'\bEcEntity\b', 'ECViewEntityData', text)
        text = re.sub(r'\bMySerializable\b', 'object', text)
        text, count = diagnostics(text)
        if 'GZLogger' in text or 'ZLogger' in text:
            raise ValueError('Unresolved original container diagnostic')
        first = re.search(r'(?m)^public ', text).start()
        imports = ('// Original client container implementation; see entity container source receipt.\n'
                   'using System;\nusing Object = UnityEngine.Object;\nusing Zjcs.Official.EntityLoading;\n'
                   'using Zjcs.Official.EntityLifecycle;\nusing Zjcs.Official.EntityViews;\n'
                   'using Zjcs.Official.StoryPlayables;\nusing Zjcs.Official.EntityTipDotViews;\n'
                   'using Zjcs.Official.World.NativeTerrain.Common;\n'
                   'using Zjcs.Official.World.Map13EntityEvents;\n'
                   'using IEventReceiver = Zjcs.Official.World.Map13EntityEvents.IEventReceiver;\n'
                   'using EcEntitySourceType = Zjcs.Official.World.Map13EntityVisibilityViews.EcEntitySourceType;\n')
        text = imports + text[:first] + 'namespace Zjcs.Official.EntityContainers\n{\n' + text[first:] + '\n}\n'
        target = OUTPUT / path.name; write(target, text.encode('utf-8')); runtime_meta(target)
        outputs += [target, Path(str(target) + '.meta')]
        adaptations.append(dict(source=path.name, diagnosticBlocks=count + container_diagnostics))
    binding = OUTPUT / 'EntityContainerBindings.cs'
    if not binding.exists():
        raise ValueError('Explicit original shared container service bindings must exist')
    runtime_meta(binding); outputs += [binding, Path(str(binding) + '.meta')]
    target = OUTPUT / 'Zjcs.Official.EntityContainers.asmdef'
    write(target, encode(dict(name='Zjcs.Official.EntityContainers', rootNamespace='Zjcs.Official.EntityContainers',
          autoReferenced=False, references=['Zjcs.Official.EntityLoading', 'Zjcs.Official.EntityLifecycle',
              'Zjcs.Official.EntityViews', 'Zjcs.Official.StoryPlayables', 'Zjcs.Official.EntityTipDotViews',
              'Zjcs.Official.World.NativeTerrain', 'Zjcs.Official.World.Map13EntityEvents',
              'Zjcs.Official.World.Map13EntityVisibilityViews'])))
    runtime_meta(target); runtime_meta(OUTPUT); outputs += [target, Path(str(target) + '.meta'), Path(str(OUTPUT) + '.meta')]
    editor = ROOT / 'unity/Assets/Game/Editor/Map14/EntityContainers'
    target = editor / 'Zjcs.Editor.Map14.EntityContainers.asmdef'
    write(target, encode(dict(name='Zjcs.Editor.Map14.EntityContainers', rootNamespace='Zjcs.Editor.Map14',
          includePlatforms=['Editor'], autoReferenced=False, references=[
              'Zjcs.Official.EntityContainers', 'Zjcs.Official.EntityLoading', 'Zjcs.Official.EntityLifecycle',
              'Zjcs.Official.EntityViews', 'Zjcs.Official.EntityTipDotViews', 'Zjcs.Official.StoryPlayables',
              'Zjcs.Official.World.NativeTerrain', 'Zjcs.Official.World.Map13EntityEvents',
              'Zjcs.Official.World.Map13EntityVisibilityViews', 'Zjcs.Editor.Map14.EntityLoading'])))
    for path in editor.iterdir():
        if path.is_file() and path.suffix != '.meta':
            runtime_meta(path); outputs += [path, Path(str(path) + '.meta')]
    runtime_meta(editor); outputs.append(Path(str(editor) + '.meta'))
    dependencies = [ROOT / 'unity/Assets/Game/Runtime' / path for path in (
        'Official/EntityLifecycle/MyEntity.cs', 'Official/EntityLifecycle/EntityLifecycleBindings.cs',
        'Official/EntityLoading/IEntityLoadHandler.cs', 'Official/EntityLoading/MyEntityLoadHandler.cs',
        'Official/EntityLoading/EntityLoadingBindings.cs', 'Official/EntityTipDotViews/EntityOwnershipData.cs',
        'Official/EntityTipDotViews/EntityOwnershipType.cs',
        'Official/World/Map13EntityVisibilityViews/EcEntitySourceType.cs',
        'Official/World/Map13EntityEvents/IEventReceiver.cs',
        'Official/World/Map13EntityEvents/EventDelegateBasicBase.cs')]
    write(RECEIPT, encode(dict(manifest='871_156598', mapClassId=14, assemblies=[record(assembly)],
          inputs=[record(p) for p in sources], tools=[record(Path(__file__).resolve())], adaptations=adaptations,
          outputs=[record(p) for p in outputs], dependencies=[record(p) for p in dependencies],
          validation=dict(entry='Zjcs.Editor.Map14.OfficialMap14EntityContainerAcceptance.Run',
              graphicalPlayMode=True,
              checks=['Four subscription and teardown order',
                      'Root/group registration and start-before-add events',
                      'Prepare/replace dictionary identity and event order',
                      'Deferred fade then end/remove/event/unload order',
                      'Negative client identifiers and source ownership/grid assignment',
                      'Story NPC uniqueness and removal callbacks',
                      'Immediate and bulk removal',
                      'Original entity loader and four-statue display regression'],
              boundary='Container event and state services are explicit fixture inputs. Native shared event/data adapter, movement, formal world input/UI/save and Player are not covered.'),
          operation='Copy the complete original client container and both client entity handlers with four original event receiver/delegate pairs. Preserve registration, component start, fade/end/remove/unload order, shared start/destroy/prepare/replace subscription order, replacement dictionary identity, negative client identifiers and story NPC uniqueness. Explicit shared-state/movement/ownership services adapt existing native entity data, without implementing world/server rules.',
          limitations=['Formal shared entity projection, native prefab loader, movement and client container map-host binding remain required.'])))
    print('Migrated original client container and both client entity handlers:', len(sources), 'source files')

if __name__ == '__main__':
    prepare()
