"""Migrate complete original minimap filter UI, state and item components."""
import hashlib,json,re
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import write,encode,sha,meta
from prepare_unity_map13_minimap_sources import record

SOURCE=ROOT/'.tools/source-packages/current/871_156598/official-code'
DEST=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapMarkFilter'

def build():
    inputs=[];outputs=[]
    def read(relative):
        p=SOURCE/relative;inputs.append(record(p));return p.read_text(encoding='utf-8-sig').replace('\r\n','\n')
    def emit(name,text):
        p=DEST/name;write(p,text.encode());m=Path(str(p)+'.meta');guid=hashlib.md5(('zjcs-official:'+p.relative_to(ROOT/'unity').as_posix()).encode()).hexdigest()
        if m.exists() and not re.search(r'(?m)^guid: '+guid+r'\s*$',m.read_text(encoding='utf-8-sig')):raise ValueError('Unrelated filter metadata')
        importer='AssemblyDefinitionImporter' if p.suffix=='.asmdef' else 'MonoImporter';write(m,('fileFormatVersion: 2\nguid: '+guid+'\n'+importer+':\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode());outputs.append(dict(**record(p),meta_sha256=sha(m.read_bytes())))
    def wrap(text):
        text=re.sub(r'^using (?:Common|Framework(?:\.[\w.]+)?|R3|UnityEngine\.UI\.Ext);\n','',text,flags=re.M)
        at=re.search(r'(?m)^(?:\[|public )',text).start()
        return text[:at]+'''namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using EcEntityIdentifier=Zjcs.Official.StoryPlayables.EcEntityIdentifier;
using Zjcs.Official.World.NativeLabelUI;
using TabItem=Zjcs.Official.WorldUI.WorldTabItem;
using IGameObjectPoolItem=Zjcs.Official.ProfessionUI.IGameObjectPoolItem;
using Zjcs.Official.ProfessionUI;
'''+text[at:]+'\n}\n'
    text=read('client-source/MiniMapMarkUIFilter.cs')
    start=text.index('\t[Inject]');end=text.index('\n\tprivate bool mEntryState',start)
    text=text[:start]+'''    public IMiniMapMarkFilterServices Services { get;set; }
    public IMiniMapFilterPersistentState PersistentState { get;set; }
    private IMiniMapMarkFilterServices EventSystemManager=>Services;
    private IMiniMapMarkFilterServices ResourceLoader=>Services;
    private IMiniMapMarkFilterServices InputOperationModule=>Services;
    private MiniMapResourcePointManager ResourcePointManager=>((IMiniMapFilterMapContext)mMiniMap).ResourcePointManager;
'''+text[end:]
    text=text.replace('new GameObjectPool<MarkUIFilterEntryItem>(mEntryContent, mEntryItemPrefab)','Services.CreatePool<MarkUIFilterEntryItem>(mEntryContent, mEntryItemPrefab)').replace('new GameObjectPool<MiniMapMarkFilterItem>(mFilterContent, mFilterItemPrefab)','Services.CreatePool<MiniMapMarkFilterItem>(mFilterContent, mFilterItemPrefab)')
    text=text.replace('Parsers.Instance.MiniMapMarkInfoParser.GetAll()','Services.GetAllMarkInfos()').replace('foreach (KeyValuePair<int, AssetGuidProxy<GameObject>> item in mMiniMap.ResourcePointManager.ForeachLayer())','foreach (var item in ResourcePointManager.ForeachLayer())')
    text=text.replace('mMiniMap.ResourcePointManager.','ResourcePointManager.').replace('MapInfoManager.Instance.GetMapInfo(mMapClassId).MapGroupId','mMiniMap.MarkDataQueries.GetMapInfo(mMapClassId).MapGroupId')
    text=re.sub(r'\bMiniMapMarkInfo\b','MiniMapFilterMarkInfo',text);text=re.sub(r'\bMiniMap\b','MiniMapManagedContext',text)
    text=text.replace('AssetOperationHandle<Sprite>','IMiniMapDescriptionAssetLease<Sprite>');text=re.sub(r'\bDisposableBag\b','MiniMapSpawnDisposableBag',text)
    text=text.replace('ObservableSubscribeExtensions.Subscribe<Unit>(UnityEventExtensions.AsObservable((UnityEvent)(object)mEntryBtn.onClick, default(CancellationToken)), (Action<Unit>)OnEntryClick);','mEntryBtn.onClick.AddListener(OnEntryClick);')
    text=text.replace('ObservableSubscribeExtensions.Subscribe<Unit>(UnityEventExtensions.AsObservable((UnityEvent)(object)mButton.onClick, default(CancellationToken)), (Action<Unit>)OnSelectAllButtonClick);','mButton.onClick.AddListener(OnSelectAllButtonClick);')
    text=text.replace('OnEntryClick(Unit _)','OnEntryClick()').replace('OnSelectAllButtonClick(Unit _)','OnSelectAllButtonClick()')
    text=text.replace('HookOnInput<OnGlobalPointerDownResult>(', 'HookOnInput(').replace('UnhookOnInput<OnGlobalPointerDownResult>(', 'UnhookOnInput(')
    # Replace only compiler-generated interpolated logging construction.
    for function in ['RefreshEntryItems','AcquireMark']:
        marker='\t\t\t\t\tGLoggerErrorInterpolatedStringHandler' if function=='RefreshEntryItems' else '\t\t\t\tGLoggerErrorInterpolatedStringHandler'
        start=text.index(marker);finish=text.index('\n',text.index('Log.Error(ref message',start))
        indent=marker[:marker.index('GLogger')];text=text[:start]+indent+'Debug.LogError("MiniMapMarkUIFilter '+function+': Failed to load sprite for mark " + '+('miniMapMarkInfo' if function=='RefreshEntryItems' else 'markInfo')+'.Name + " with path " + '+('miniMapMarkInfo' if function=='RefreshEntryItems' else 'markInfo')+'.Image);'+text[finish:]
    emit('MiniMapMarkUIFilter.cs',wrap(text))
    for name in ['MiniMapMarkFilterItem','MarkUIFilterEntryItem','MultiTextsControler','MiniMapMarkUIFilterSetting']:
        text=read('client-source/'+name+'.cs')
        text=text.replace('[DatabaseProvider(typeof(PlayerDatabaseProvider))]\n','')
        emit(name+'.cs',wrap(text))
    for name in ['CollectSortGroupItem','BaseSortGroupItem','CollectSortType']:
        emit(name+'.cs',wrap(read('client-source/'+name+'.cs')))
    text=read('client-source/UnityEngine.UI.Ext/TabObjectTransition.cs').replace('namespace UnityEngine.UI.Ext;','using UnityEngine;').replace(' : TabTransition',' : Zjcs.Official.WorldUI.WorldTabTransition');emit('TabObjectTransition.cs',wrap(text))
    emit('IInputOperationResult.cs',wrap(read('client-source/IInputOperationResult.cs')))
    text=read('client-source/OnGlobalPointerDownResult.cs')
    start=text.index('\tpublic Vector2 ScreenPos');end=text.index('\n\tpublic OnGlobalPointerDownResult(',start)
    text=text[:start]+'\tpublic Vector2 ScreenPos { get;private set; }\n'+text[end:]
    text=text.replace('Vector2.op_Implicit(screenPos)','(Vector2)screenPos').replace('Vector2.op_Implicit(result.ScreenPos)','(Vector3)result.ScreenPos');emit('OnGlobalPointerDownResult.cs',wrap(text))
    # Config consumption projection: retain all six original fields, including nullable lists.
    read('common-source/Common/MiniMapMarkInfo.cs')
    emit('MiniMapMarkFilterPorts.cs',PORTS)
    assembly=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapMarkManagers/Zjcs.Official.World.Map13MiniMapMarkManagers.asmdef';inputs.append(record(assembly))
    refs=list(dict.fromkeys(['Zjcs.Official.World.Map13MiniMapMarkManagers','Zjcs.Official.World.Map13MiniMapResourcePoints','Zjcs.Official.WorldUI']+json.loads(assembly.read_bytes())['references']))
    emit('Zjcs.Official.World.Map13MiniMapMarkFilter.asmdef',encode(dict(name='Zjcs.Official.World.Map13MiniMapMarkFilter',references=refs,autoReferenced=True)).decode());meta(DEST,folder=True)
    dependencies=[record(ROOT/'unity/Assets/Game/Runtime/Official'/p) for p in ['ProfessionUI/GameObjectPool.cs','ProfessionUI/GameObjectPoolBase.cs','ProfessionUI/IGameObjectPoolItem.cs','WorldUI/WorldTabItem.cs','WorldUI/WorldTabGroup.cs','WorldUI/WorldTabTransition.cs','World/Map13MiniMapResourcePoints/MiniMapResourcePointManager.cs','World/Map13MiniMapMarkSpawn/MiniMapSpawnDisposableBag.cs']]
    write(ROOT/'assets/reference/official-map/map13-minimap-mark-filter-code-source-manifest.json',encode(dict(format=1,mapClassId=13,inputs=inputs,outputs=outputs,dependencies=dependencies,generator=record(Path(__file__).resolve()),operation='Copy complete original minimap filter, persistent setting, entry/filter item, multi-text controller and global pointer result components. Reuse complete original tab and generic UI pool implementations and the full pinned R3 disposable bag. Replace singleton/data/pool-scope/input/persistence/resource resolution with explicit ports; adapt the two unused Unit event payloads to direct UnityEvent listeners while preserving callback registration/lifetime/order. Retain every filter selection, absent-key default, resource-layer toggle, all-table persistence update, outside-click raycast, prefab filename lookup, supporter iteration, resource caching and source null/empty behavior. The six-field configuration type is a read-only consumption projection, not a replacement binary parser.',limitations=['Production mainline pool and local save adapters, official full-screen map/root integration, and Windows Player remain required.'])))
    print('Original minimap filter:',len(outputs),'outputs')

PORTS='''using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using Zjcs.Official.ProfessionUI;
namespace Zjcs.Official.World.Map13MiniMap
{
    public sealed class MiniMapFilterMarkInfo
    { public string Name,Image,Description;public bool ShowOnFilterEntry;public List<int> ShowOnMapGroupId,ShowOnMapClassId; }
    public interface IMiniMapFilterMapContext { MiniMapResourcePointManager ResourcePointManager { get; } }
    public interface IMiniMapFilterPersistentState
    { MiniMapMarkUIFilterSetting State { get; } UniTask WriteStateAsync(); }
    public interface IMiniMapMarkFilterServices
    {
        MiniMapFilterMarkInfo[] GetAllMarkInfos();
        GameObjectPool<T> CreatePool<T>(Transform root,GameObject prefab) where T:Component;
        IMiniMapDescriptionAssetLease<Sprite> LoadSprite(string path);
        IEnumerable<RaycastResult> ForeachRaycastResults(Vector2 position);
        void HookOnInput(Action<OnGlobalPointerDownResult> receiver);
        void UnhookOnInput(Action<OnGlobalPointerDownResult> receiver);
    }
}
'''

if __name__=='__main__':build()
