"""Migrate original map-display borrowing, recycling and entity bind selection."""
import json
import re

from official_apk import ROOT
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_story_attachment_runtime import CODE, record
from prepare_unity_story_playable_runtime import logging_adapter
from prepare_unity_story_sound_runtime import runtime_meta
from prepare_unity_map13_prop_parts import meta, dump
from official_native_yaml_verification import documents

NAMESPACE = 'Zjcs.Official.MapDisplays'
OUTPUT = ROOT / 'unity/Assets/Game/Runtime/Official/MapDisplays'
RECEIPT = ROOT / 'assets/reference/official-map/map14-map-display-runtime-source-manifest.json'

BOUNDARIES = '''using System;
using UnityEngine;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.Map13Entities;
using Pos2d = Zjcs.Official.World.NativeTerrain.Common.Pos2d;
namespace Zjcs.Official.MapDisplays
{
    // Required consumed state/query/resource inputs. These supply no map,
    // server-disable, ownership or shared entity transition rules.
    public interface IMapDisplayRoot
    {
        Transform MapRoot { get; }
        int CurrentMapClassId { get; }
        bool Destroying { get; }
        GroundType GetGridGroundType(Pos2d position);
    }
    public interface IMapDisplayQueries
    {
        EntityMapDisplayInfoParser Selector { get; }
        int GetMapGroupId(int mapClassId);
        string GetAreaTag(int mapClassId, int areaId);
        bool IsMapForbidLowMemoryDeal();
    }
    public interface IMapDisplayPosition { int QueryArea(Pos2d position); }
    public interface IMapDisplayEntityState
    {
        bool Removing { get; }
        Pos2d GridPosition { get; }
        string EntityTag { get; }
    }
    public interface IMyEntityBindPreEventReceiver : IEventReceiver
    { void MyEntity_OnBindPre(ECViewEntityData entity); }
    public interface IMapDisplayEntityEvents
    {
        void AddBindPreReceiver(IMyEntityBindPreEventReceiver receiver);
        void RemoveBindPreReceiver(IMyEntityBindPreEventReceiver receiver);
    }
    public abstract class MapDisplayAssetLease : IDisposable
    {
        public abstract bool IsValid { get; }
        public abstract int Status { get; }
        public abstract GameObject Result { get; }
        public abstract void Dispose();
    }
    public interface IMapDisplayResourceLoader { MapDisplayAssetLease LoadPrefab(string path); }
    public interface IMapDisplayInstantiation { GameObject Instantiate(GameObject prefab); }
    public static class MapDisplayServices
    {
        public static GameObject Instantiate(IServiceProvider provider, GameObject prefab) =>
            (provider as IMapDisplayInstantiation ?? throw new InvalidOperationException("Original map-display injection/instantiation is not bound.")).Instantiate(prefab);
        public static IMapDisplayEntityState State(ECViewEntityData data) => data as IMapDisplayEntityState ?? data?.GetComponent<IMapDisplayEntityState>() ??
            throw new InvalidOperationException("Original map-display shared entity state is not bound.");
    }
}
'''


def build():
    manifest = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    assemblies = [CODE / 'managed/decoded' / name for name in ('Assembly-CSharp.dll', 'Console.dll')]
    for path in assemblies:
        if sha(path.read_bytes()) != manifest['assemblies'][path.name]['decoded_sha256']:
            raise ValueError('Original map-display assembly changed')
    reused = ROOT / 'assets/reference/official-map/map13-entity-presentation-source-manifest.json'
    previous = json.loads(reused.read_bytes())
    if previous['manifest'] != '871_156598':
        raise ValueError('Original map-display selector version differs')
    selected_names = {'EntityMapDisplayInfoParser.cs', 'EntityMapDisplayInfo.cs', 'GroundType.cs'}
    dependencies = []
    for row in previous['outputs']:
        path = ROOT / row['path']
        if path.name in selected_names:
            if sha(path.read_bytes()) != row['sha256']:
                raise ValueError('Reused original selector changed: ' + row['path'])
            dependencies.append(record(path))
    if len(dependencies) != 3:
        raise ValueError('Original selector dependency selection differs')
    sources = [CODE / 'client-source' / (name + '.cs') for name in
               ('MapDisplayManager', 'MapDisplayPoolHandle', 'IMapDisplayManager', 'MyMapDisplay')]
    outputs, changes = [], []
    for path in sources:
        text = path.read_text(encoding='utf-8-sig')
        if path.stem == 'MapDisplayManager':
            text = text.replace('SceneScopeBackgroundServiceBase, IMapScopeBackgroundService, IMapDisplayManager', 'IDisposable, IMapDisplayManager')
            text = text.replace('protected override void Initialize()', 'public void Initialize()').replace('protected override void Dispose()', 'public void Dispose()')
            text = text.replace('\t\tbase.Initialize();\n', '').replace('\t\tbase.Dispose();\n', '')
            text = text.replace('IMapRootManagerProxy', 'IMapDisplayRoot').replace('IResourceLoader', 'IMapDisplayResourceLoader')
            text = text.replace('private IApplicationService ApplicationService { get; set; }', 'public GAction LowMemory { get; set; }\n\n\tpublic IMapDisplayQueries Queries { get; set; }')
            text = text.replace('Parsers.Instance.MapEntityFunctionDisableInfoParser.IsMapForbidLowMemoryDeal()', 'Queries.IsMapForbidLowMemoryDeal()')
            text = text.replace('ApplicationService.OnLowMemory.Subscribe(OnLowMemory)', 'LowMemory.Subscribe(OnLowMemory)')
            text = text.replace('MapInfo mapInfo = MapInfoManager.Instance.GetMapInfo(mapClassId);', 'int groupId = Queries.GetMapGroupId(mapClassId);')
            text = text.replace('Parsers.Instance.EntityMapDisplayInfoParser.Get(classId, mapInfo.MapGroupId,', 'Queries.Selector.Get(classId, groupId,')
            # Retain the original info-level diagnostic and its execution point.
            start = text.index('\t\tGLoggerInfoInterpolatedStringHandler message =')
            end = text.index('\n\t}', start)
            text = text[:start] + '\t\tUnityEngine.Debug.Log("MapDisplayManager.OnLowMemory ended");' + text[end:]
        elif path.stem == 'MapDisplayPoolHandle':
            text = text.replace('AssetOperationHandle<GameObject>', 'MapDisplayAssetLease').replace('IResourceLoader', 'IMapDisplayResourceLoader')
            text = text.replace('(int)mAssetOperationHandle.Status', 'mAssetOperationHandle.Status')
            text = text.replace('ServiceProviderExtensions.Instantiate(mServiceProvider,', 'MapDisplayServices.Instantiate(mServiceProvider,')
            text = text.replace('Pool.TryDequeue(ref component)', 'Pool.TryDequeue(out component)').replace('Pool.TryDequeue(ref iECViewDriver)', 'Pool.TryDequeue(out iECViewDriver)')
        elif path.stem == 'MyMapDisplay':
            text = re.sub(r'\bMyEntity\b', 'ECViewEntityBinding', text)
            text = re.sub(r'\bEcEntity\b', 'ECViewEntityData', text)
            text = text.replace('IMapRootManagerProxy', 'IMapDisplayRoot').replace('IMyMapPositionHandlerProxy', 'IMapDisplayPosition')
            text = text.replace('!ent.Removing', '!MapDisplayServices.State(ent).Removing')
            text = text.replace('ent.GridTransform.Pos', 'MapDisplayServices.State(ent).GridPosition')
            text = text.replace('Parsers.Instance.MapAreaInfoParser.Get(mMapClassId, MyMapPositionHandler.QueryArea(MapDisplayServices.State(ent).GridPosition)).AreaTag', 'Queries.GetAreaTag(mMapClassId, MyMapPositionHandler.QueryArea(MapDisplayServices.State(ent).GridPosition))')
            text = text.replace('MapRootManagerProxy.GridGround.Info.GetGridGroundType(', 'MapRootManagerProxy.GetGridGroundType(')
            text = text.replace('ent.GetEntityTag()', 'MapDisplayServices.State(ent).EntityTag')
            text = text.replace('mMyEntity.BindPreEvent += (IMyEntityBindPreEventReceiver)this;', '((IMapDisplayEntityEvents)mMyEntity).AddBindPreReceiver(this);')
            text = text.replace('mMyEntity.BindPreEvent -= (IMyEntityBindPreEventReceiver)this;', '((IMapDisplayEntityEvents)mMyEntity).RemoveBindPreReceiver(this);')
            text = text.replace('mMyEntity.RemoveEvent += (IMyEntityRemoveEventReceiver)this;', 'mMyEntity.AddRemoveReceiver(this);')
            text = text.replace('mMyEntity.RemoveEvent -= (IMyEntityRemoveEventReceiver)this;', 'mMyEntity.RemoveRemoveReceiver(this);')
            text = text.replace('\tprivate int mClassId;', '\tpublic IMapDisplayQueries Queries { get; set; }\n\n\tprivate int mClassId;')
        text = re.sub(r'(?m)^using (?:Common|Framework(?:\.[\w.]+)?|GameFramework);\n', '', text)
        text = re.sub(r'(?m)^\s*\[Inject\]\n', '\n', text)
        text = re.sub(r'(?m)^\s*//IL_[^\n]*\n', '\n', text)
        for typename in ('IMapDisplayRoot MapRootManagerProxy', 'IMapDisplayResourceLoader ResourceLoader',
                         'IServiceProvider ServiceProvider', 'IMapDisplayManager MapDisplayManager', 'IMapDisplayPosition MyMapPositionHandler'):
            text = text.replace('private ' + typename + ' { get; set; }', 'public ' + typename + ' { get; set; }')
        text, diagnostics = logging_adapter(text)
        declaration = re.search(r'(?m)^(?:\[|public )', text).start()
        text = ('// Complete original map-display application class; see source receipt.\nusing Object = UnityEngine.Object;\n'
                'using Zjcs.Official.EntityViews;\nusing Zjcs.Official.World.Map13Entities;\nusing Zjcs.Official.StoryPlayables;\n' +
                text[:declaration] + 'namespace ' + NAMESPACE + '\n{\n' + text[declaration:] + '\n}\n')
        target = OUTPUT / path.name
        write(target, text.encode('utf-8')); runtime_meta(target); outputs += [target, target.with_suffix('.cs.meta')]
        changes.append(dict(source=path.name, diagnosticBlocks=diagnostics))
    target = OUTPUT / 'MapDisplayBindings.cs'
    write(target, BOUNDARIES.encode('utf-8')); runtime_meta(target); outputs += [target, target.with_suffix('.cs.meta')]
    asmdef = OUTPUT / (NAMESPACE + '.asmdef')
    write(asmdef, encode(dict(name=NAMESPACE, rootNamespace=NAMESPACE, autoReferenced=False,
          references=['Zjcs.Official.EntityViews', 'Zjcs.Official.World.Map13Entities',
                      'Zjcs.Official.World.NativeTerrain', 'Zjcs.Official.StoryPlayables'])))
    runtime_meta(asmdef); runtime_meta(OUTPUT); outputs += [asmdef, asmdef.with_suffix('.asmdef.meta'), OUTPUT.with_suffix('.meta')]
    # Deliver every original selector row, and only explicitly migrated prefab
    # addresses. Missing formal entity displays fail closed in the local loader.
    table = ROOT / 'unity/Assets/Official/World/Map/14/Source/tables.json'
    table_proof = ROOT / 'assets/reference/official-map/map14-source-manifest.json'
    expected, = [r for r in json.loads(table_proof.read_bytes())['outputs'] if r['path'] == record(table)['path']]
    if sha(table.read_bytes()) != expected['sha256']:
        raise ValueError('Original map-display table changed')
    rows = json.loads(table.read_bytes())['Map/entity_map_display']
    if len(rows) != 1253:
        raise ValueError('Original selector row coverage differs')
    statue_proof = ROOT / 'assets/reference/official-map/map14-native-hero-statues-source-manifest.json'
    statue = json.loads(statue_proof.read_bytes())
    native_outputs = {r['path']: r['sha256'] for r in statue['outputs']}
    native = ROOT / 'unity/Assets/Game/Runtime/Offline/World/NativeMap14HeroStatues'
    catalog_script = native / 'NativeMap14DisplayCatalog.cs'
    displays = []
    for prefab in statue['prefabs']:
        path = ROOT / prefab['path']
        if sha(path.read_bytes()) != native_outputs[prefab['path']]:
            raise ValueError('Verified native display changed')
        selected, = [r for r in rows if r['EntityClassId'] == prefab['classId'] and r['MapClassId'] == 14]
        native_documents = documents(path.read_text(encoding='utf-8-sig'))
        root_id, = [int(body['m_GameObject']['fileID']) for kind,body in native_documents.values()
                    if kind=='Transform' and not body['m_Father']['fileID']]
        displays.append(dict(path=selected['DisplayPath'], prefab=dict(fileID=root_id, guid=prefab['guid'], type=3)))
    catalog = ROOT / 'unity/Assets/Game/Resources/Map14/Native/map-display-catalog.asset'
    body = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
                m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1, m_EditorHideFlags=0,
                m_Script=dict(fileID=11500000,guid=meta(catalog_script,'MonoImporter'),type=3), m_Name='Map14 original display catalog',
                m_EditorClassIdentifier='', manifest='871_156598', selectors=rows, displays=displays)
    write(catalog, ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n' + dump(dict(MonoBehaviour=body))).encode('utf-8'))
    meta(catalog,'NativeFormatImporter'); outputs += [catalog, catalog.with_suffix('.asset.meta')]
    for path in (catalog_script,native/'NativeMap14DisplayInstantiation.cs',native/'Zjcs.Offline.World.NativeMap14HeroStatues.asmdef'):
        meta(path,'MonoImporter' if path.suffix=='.cs' else 'AssemblyDefinitionImporter');outputs += [path,path.with_suffix(path.suffix+'.meta')]
    dependencies += [record(table),record(table_proof),record(statue_proof)]
    write(RECEIPT, encode(dict(manifest='871_156598', mapClassId=14, assemblies=[record(p) for p in assemblies],
        inputs=[record(p) for p in sources], dependencies=dependencies + [record(reused)], adaptations=changes,
        counts=dict(original_selector_rows=len(rows), native_display_addresses=len(displays)),
        tools=[record(ROOT / 'scripts/prepare_unity_map_display_runtime.py')], outputs=[record(p) for p in outputs],
        operation='Copy complete original MapDisplayManager, MapDisplayPoolHandle, MyMapDisplay and interface. Reuse the verified current-version original ordered selector. Preserve config identity pool keys, original asset-valid/status-2 branch, FIFO borrowing, loaned counts, null/non-Mono guards, hierarchy/activation order, reverse low-memory removal, disposed-handle behavior, bind-pre source fields and destroy/remove branches. Replace framework service lifecycle entry, dependency injection, consumed state/config/resource inputs and diagnostics with explicit host bindings. No map or entity rules are inferred.',
        limitations=['Formal original entity root/shared components, real map routing/input/UI/save and Windows Player remain required.'])) )
    print('Original map-display manager, FIFO pool and bind-pre component migrated.')


if __name__ == '__main__':
    build()
