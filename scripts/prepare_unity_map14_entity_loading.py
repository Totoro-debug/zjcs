"""Migrate the original client entity loader and its TTL pool dependencies."""
import json
import re
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_story_sound_runtime import runtime_meta

CODE = ROOT / '.tools/source-packages/current/871_156598/official-code'
OUTPUT = ROOT / 'unity/Assets/Game/Runtime/Official/EntityLoading'
RECEIPT = ROOT / 'assets/reference/official-map/map14-entity-loading-source-manifest.json'


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes()))


def diagnostics(text):
    pattern = re.compile(r'(?m)^(?P<i>[ \t]*)(?:GLogger\w+InterpolatedStringHandler )?message\d* = new GLogger\w+InterpolatedStringHandler\([^\n]+\n.*?^(?P=i)Log\.(?P<kind>Info|Warn|Error)\((?:(?P<error>exception), )?ref message\d*,[^\n]+;', re.S)
    def replace(match):
        if match['error']:
            return match['i'] + 'UnityEngine.Debug.LogException(' + match['error'] + ');'
        method = dict(Info='Log', Warn='LogWarning', Error='LogError')[match['kind']]
        return match['i'] + 'UnityEngine.Debug.' + method + '("Original entity loading diagnostic");'
    result, count = pattern.subn(replace, text)
    result = re.sub(r'(?m)^\s*(?:GLogger\w+InterpolatedStringHandler message\d*|bool enabled\d*);\n', '', result)
    if 'GLogger' in result or re.search(r'\bLog\.(?:Info|Warn|Error)', result):
        raise ValueError('Original entity loader diagnostic was not fully adapted')
    return result, count


def prepare():
    manifest = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    if manifest['manifest'] != '871_156598':
        raise ValueError('Original entity loading source version differs')
    assemblies = [CODE / 'managed/decoded' / name for name in
                  ('Assembly-CSharp.dll', 'Framework.dll', 'Framework.FirstPass.Runtime.dll')]
    for path in assemblies:
        if sha(path.read_bytes()) != manifest['assemblies'][path.name]['decoded_sha256']:
            raise ValueError('Original entity loading assembly changed: ' + path.name)
    contracts = [CODE / 'client-source/IMyEntityIndexHandler.cs',
                 CODE / 'framework-firstpass-source/Framework/IResourceLoader.cs',
                 CODE / 'framework-firstpass-source/Framework/IApplicationService.cs',
                 CODE / 'common-source/Common/MapEntityFunctionDisableInfoParser.cs']
    dependencies = [ROOT / 'unity/Assets/Game/Runtime' / path for path in (
        'Official/StoryPlayableManager/GameObjectPoolBase.cs',
        'Official/StoryPlayableManager/StoryPlayableInstantiation.cs',
        'Official/StoryPlayables/IGameObjectPoolItem.cs',
        'Official/StoryPlayables/ProfilerScope.cs',
        'Official/StoryPlayables/EcEntityIdentifier.cs',
        'Official/EntityLifecycle/MyEntity.cs',
        'Official/EntityViews/ECViewEntityBinding.cs')]
    sources = {name: CODE / 'framework-source' / (name + '.cs') for name in
               ('GameObjectPoolEmbed', 'TTLGameObjectPool', 'TTLGameObjectPoolCollection')}
    sources['SortedValueDictionary'] = CODE / 'framework-firstpass-source/Framework.Collections/SortedValueDictionary.cs'
    sources.update({name: CODE / 'client-source' / (name + '.cs') for name in ('IEntityLoadHandler', 'MyEntityLoadHandler')})
    outputs, adaptations = [], []
    for name, path in sources.items():
        text = path.read_text(encoding='utf-8-sig')
        text = re.sub(r'(?m)^using (?:Framework(?:\.[\w.]+)?|Microsoft.Extensions.DependencyInjection|R3|Common);\n', '', text)
        text = text.replace('namespace Framework.Collections;\n', '')
        text = re.sub(r'(?m)^\s*//IL_[^\n]*\n', '\n', text)
        if name == 'GameObjectPoolEmbed':
            text = text.replace('ServiceProviderExtensions.Instantiate(((Component)(object)rootNode).GetSceneContainer(), goPrefab, slot, false)',
                                'StoryPlayableInstantiation.Instantiate(rootNode, goPrefab, slot, false)')
        elif name == 'TTLGameObjectPoolCollection':
            text = text.replace('private DisposableBag mDisposableBag;', 'private readonly IDisposable mExpirySubscription;')
            first = text.index('\tpublic TTLGameObjectPoolCollection()')
            last = text.index('\n\tpublic void Dispose()', first)
            text = text[:first] + '''\tpublic TTLGameObjectPoolCollection(IEntityPoolScheduler scheduler)
\t{
\t\tmExpirySubscription = scheduler.Subscribe(new TimeSpan(0, 0, 2), CheckExpire);
\t}
''' + text[last:]
            text = text.replace('mDisposableBag.Dispose();', 'mExpirySubscription.Dispose();')
            text = text.replace('return ((Dictionary<TKey, TTLGameObjectPool<TTLGameObjectPool<TValue>>>)(object)mPoolDict).TryAdd(key, (TTLGameObjectPool<TTLGameObjectPool<TValue>>)(object)pool);',
                                'return mPoolDict.TryAdd(key, pool);')
            text = text.replace('public unsafe void', 'public void').replace('private unsafe void', 'private void')
            text = re.sub(r'\t\tTKey val = default;\n\t\tTTLGameObjectPool<TValue> tTLGameObjectPool = default;\n', '', text)
            text = re.sub(r'\t\t\t\(\(KeyValuePair[^\n]+\n', '\t\t\tvar tTLGameObjectPool = item.Value;\n', text)
        elif name == 'MyEntityLoadHandler':
            text = text.replace('public struct MyEntityRequestInfo(EcEntity entity, bool newEntity, MyEntityProvider goProvider, Action<MyEntity> callback)', 'public struct MyEntityRequestInfo')
            for field, parameter in (('Entity', 'entity'), ('NewEntity', 'newEntity'), ('GoProvider', 'goProvider'), ('Callback', 'callback')):
                text, declarations = re.subn(r'(?m)^(\t\tpublic [^\n;=]+ ' + field + ') = ' + parameter + ';', r'\1;', text)
                if declarations != 1:
                    raise ValueError('Original request field changed: ' + field)
            needle = '\t\tpublic EcEntity Entity;'
            constructor = '''\t\tpublic MyEntityRequestInfo(EcEntity entity, bool newEntity, MyEntityProvider goProvider, Action<MyEntity> callback)
\t\t{ Entity = entity; NewEntity = newEntity; GoProvider = goProvider; Callback = callback; }

'''
            assert needle in text
            text = text.replace(needle, constructor + needle, 1)
            text = text.replace('private DisposableBag mDisposableBag;', 'private IDisposable mLowMemorySubscription;')
            text = text.replace('mDisposableBag.Dispose();', 'mLowMemorySubscription?.Dispose();')
            text = text.replace(' = new TTLGameObjectPoolCollection<int, MyEntity>();', ';')
            text = text.replace('ServiceProvider = serviceProvider;', 'ServiceProvider = serviceProvider;\n\t\tmMyEntityPoolCollection = new TTLGameObjectPoolCollection<int, MyEntity>(EntityLoadServices.Require<IEntityPoolScheduler>(serviceProvider));', 1)
            text = text.replace('ServiceProviderServiceExtensions.GetService<', 'EntityLoadServices.Require<')
            text = text.replace('Disposable.AddTo<IDisposable>(ApplicationService.OnLowMemory.Subscribe(OnLowMemory), ref mDisposableBag);', 'mLowMemorySubscription = ApplicationService.SubscribeLowMemory(OnLowMemory);')
            text = text.replace('Parsers.Instance.MapEntityFunctionDisableInfoParser.IsMapForbidLowMemoryDeal()', 'EntityLoadServices.Require<IEntityLoadFacts>(ServiceProvider).IsMapForbidLowMemoryDeal')
            text = text.replace('Parsers.Instance.GameSettingsParser.Get().ECIsUseGameObjectPool', 'EntityLoadServices.Require<IEntityLoadFacts>(ServiceProvider).UseGameObjectPool')
            text = text.replace('Parsers.Instance.EcDataCenterParser.GetCommonPrefab(classId)', 'EntityLoadServices.Require<IEntityLoadFacts>(ServiceProvider).GetCommonPrefab(classId)')
            text = text.replace('Parsers.Instance.EcDataCenterParser.GetCommonPrefab(entityClassId)', 'EntityLoadServices.Require<IEntityLoadFacts>(ServiceProvider).GetCommonPrefab(entityClassId)')
            text = text.replace('ServiceProviderExtensions.Instantiate(Provider, mPrefab, ((Component)parent).transform.position + localPosition, Quaternion.identity, parent)',
                                'EntityLoadServices.Require<IEntityLoadInstantiation>(Provider).Instantiate(mPrefab, parent.position + localPosition, Quaternion.identity, parent)')
            text = text.replace('MyContainer mMyContainer', 'IEntityLoadContainer mMyContainer').replace('MyContainer container', 'IEntityLoadContainer container')
            text = text.replace('EcContainer container', 'IEntityLoadSharedContainer container')
            text = text.replace('((Component)mMyContainer)', 'mMyContainer.Host').replace('(Object)(object)mMyContainer == (Object)null', '(Object)(object)mMyContainer.Host == (Object)null')
            text = text.replace('mMyContainer.myEntitySlot', 'mMyContainer.EntitySlot')
            text = text.replace('orCreateMyEntity.Bind(mMyContainer, entity, newEntity)', 'orCreateMyEntity.Bind(mMyContainer.LifecycleContainer, entity, newEntity)')
            text = text.replace('if (entity.Removing)', 'if (EntityLoadServices.Require<IEntityLoadFacts>(ServiceProvider).IsRemoving(entity))')
            text = text.replace('''\t\tGridTransformComponent gridTransform = ent.GridTransform;
\t\tif (gridTransform == null || gridTransform.GridGround == null)''', '''\t\tif (!EntityLoadServices.Require<IEntityLoadFacts>(ServiceProvider).HasGridGround(ent))''')
            text = text.replace('mMyEntityGoCache.Deconstruct(ref num, ref myEntityProvider);', 'num = mMyEntityGoCache.Key;\n\t\t\tmyEntityProvider = mMyEntityGoCache.Value;')
        if name in ('MyEntityLoadHandler', 'IEntityLoadHandler'):
            text = re.sub(r'\bEcEntity\b', 'ECViewEntityData', text)
        text, count = diagnostics(text)
        first = re.search(r'(?m)^public ', text).start()
        imports = ('// Original client/framework implementation; see entity loading source receipt.\n'
                   'using Object = UnityEngine.Object;\nusing Zjcs.Official.EntityLifecycle;\n'
                   'using Zjcs.Official.EntityViews;\nusing Zjcs.Official.StoryPlayables;\n'
                   'using Zjcs.Official.StoryPlayableManager;\n')
        text = imports + text[:first] + 'namespace Zjcs.Official.EntityLoading\n{\n' + text[first:] + '\n}\n'
        target = OUTPUT / (name + '.cs')
        write(target, text.encode('utf-8')); runtime_meta(target)
        outputs += [target, PathMeta(target)]
        adaptations.append(dict(source=name, diagnosticBlocks=count))
    binding = OUTPUT / 'EntityLoadingBindings.cs'
    if not binding.exists():
        raise ValueError('Explicit local entity loading bindings must exist before generation')
    runtime_meta(binding); outputs += [binding, PathMeta(binding)]
    assembly = OUTPUT / 'Zjcs.Official.EntityLoading.asmdef'
    write(assembly, encode(dict(name='Zjcs.Official.EntityLoading', rootNamespace='Zjcs.Official.EntityLoading',
          autoReferenced=False, references=['Zjcs.Official.EntityLifecycle', 'Zjcs.Official.EntityViews',
              'Zjcs.Official.StoryPlayables', 'Zjcs.Official.StoryPlayableManager'])))
    runtime_meta(assembly); runtime_meta(OUTPUT); outputs += [assembly, PathMeta(assembly), PathMeta(OUTPUT)]
    editor = ROOT / 'unity/Assets/Game/Editor/Map14/EntityLoading'
    editor_assembly = editor / 'Zjcs.Editor.Map14.EntityLoading.asmdef'
    write(editor_assembly, encode(dict(name='Zjcs.Editor.Map14.EntityLoading', rootNamespace='Zjcs.Editor.Map14',
          includePlatforms=['Editor'], autoReferenced=False, references=[
              'Zjcs.Official.EntityLoading', 'Zjcs.Official.EntityLifecycle', 'Zjcs.Official.EntityViews',
              'Zjcs.Official.StoryPlayables', 'Zjcs.Official.StoryPlayableManager',
              'Zjcs.Official.World.Map13EntityEvents', 'Zjcs.Editor.Map14.HeroStatues'])))
    for path in editor.iterdir():
        if path.is_file() and path.suffix != '.meta':
            runtime_meta(path); outputs += [path, PathMeta(path)]
    runtime_meta(editor); outputs.append(PathMeta(editor))
    write(RECEIPT, encode(dict(manifest='871_156598', mapClassId=14,
          assemblies=[record(p) for p in assemblies], inputs=[record(p) for p in list(sources.values()) + contracts],
          tools=[record(Path(__file__).resolve())], adaptations=adaptations, outputs=[record(p) for p in outputs],
          dependencies=[record(p) for p in dependencies],
          validation=dict(entry='Zjcs.Editor.Map14.OfficialMap14EntityLoadingAcceptance.Run',
              graphicalPlayMode=True, visualRegression='Original four-statue prefab lifecycle and three actual rendered frames',
              checks=['Out-of-order asynchronous and reentrant request FIFO',
                      'Pending/ready prefab component lookup and resource coalescing',
                      'Root-first and entity-id sorted initial loading',
                      'Removed-before-load immediate end and null callback',
                      'Embedded object reset/rebind, return position and activity',
                      'Original sixty-second TTL and expiry',
                      'Queued/in-use retention and unused resource release on low memory',
                      'Late completion after disposal, subscription and resource disposal',
                      'Creation and destruction with pooling disabled']),
          operation='Copy the original complete MyEntityLoadHandler, IEntityLoadHandler, embedded pool, TTL pool, collection and sorted expiry dictionary. Preserve FIFO completion, root-first/entity-id initial load, delayed removed-entity recovery, original pool settings/60 second TTL, low-memory retention, callback order and original collection disposal behavior. Explicit host interfaces consume original entity/settings/resource/low-memory facts and scene injection. Adapt C# 12 construction and ILSpy generic artifacts; supply the original two-second R3 interval through an owned platform scheduler.',
          limitations=['The formal native entity catalog, client container, shared entity projection, scheduler and local resource provider must be bound by the map host. No initial map entity, server visibility or save rule is invented.'])))
    print('Migrated original entity loader and TTL pools:', len(sources), 'source files')


def PathMeta(path):
    return Path(str(path) + '.meta')


if __name__ == '__main__':
    prepare()
