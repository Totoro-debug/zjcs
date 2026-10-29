"""Recover the complete original minimap marker spawn manager and pool."""
import re,json,argparse,hashlib
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import write,meta,encode,sha
from prepare_unity_map13_minimap_sources import record
from prepare_unity_map13_minimap_mark_core import method

SOURCE=ROOT/'.tools/source-packages/current/871_156598/official-code/client-source'
DEST=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapMarkSpawn'

def build(r3_bag):
    inputs=[];outputs=[]
    def read(name):
        p=SOURCE/(name+'.cs');inputs.append(record(p));return p.read_text(encoding='utf-8-sig').replace('\r\n','\n')
    def emit(name,text):
        p=DEST/name;write(p,text.encode())
        guid=hashlib.md5(('zjcs-official:'+p.relative_to(ROOT/'unity').as_posix()).encode()).hexdigest()
        importer='AssemblyDefinitionImporter' if p.suffix=='.asmdef' else 'MonoImporter' if p.suffix=='.cs' else 'TextScriptImporter'
        metadata=p.with_name(p.name+'.meta')
        if metadata.exists() and not re.search(r'(?m)^guid: '+guid+r'\s*$',metadata.read_text(encoding='utf-8-sig')):raise ValueError('Unrelated marker spawn metadata')
        write(metadata,('fileFormatVersion: 2\nguid: '+guid+'\n'+importer+':\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode())
        outputs.append(dict(**record(p),meta_sha256=sha(metadata.read_bytes())))
    def wrap(text):
        text=re.sub(r'^using (?:Common|Framework(?:\.[\w.]+)?|R3);\n','',text,flags=re.M)
        at=text.index('[RequireComponent' if '[RequireComponent' in text else 'public ')
        return text[:at]+'''namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using EcEntityIdentifier=Zjcs.Official.StoryPlayables.EcEntityIdentifier;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeMap13Labels;
'''+text[at:]+'\n}\n'
    text=read('MiniMapMarkSpawnManager')
    text=text.replace('[Inject]\n\tprivate IServiceProvider ServiceProvider { get; set; }','public IServiceProvider ServiceProvider { get; set; }')
    text=text.replace('[Inject]\n\tprivate IPerformanceMiniMapMarkProxy PerformanceMiniMapMarkProxy { get; set; }','public IMiniMapMarkPerformance PerformanceMiniMapMarkProxy { get; set; }')
    text=text.replace('ServiceProvider.CreateInstance<MiniMapMarkPoolHandler>(new object[1] { ((Component)this).transform })','new MiniMapMarkPoolHandler(((Component)this).transform, mMinimap)')
    text=text.replace('MapInfoManager.Instance.GetMapInfo(mMinimap.MapClassId)','mMinimap.MarkDataQueries.GetMapInfo(mMinimap.MapClassId)').replace('MapUtils.GetMapEntityMiniMapMark(ent, mapInfo.MapGroupId, mMinimap.MapClassId, ent.GetEntityTag())','mMinimap.GetMapEntityMiniMapMark(ent, mapInfo.MapGroupId, mMinimap.MapClassId, ent.GetEntityTag())')
    text=text.replace('MiniMapMarkUIBase miniMapMarkUIBase = default;\n\t\tif (mMarkUiDict.Remove(preEntId, ref miniMapMarkUIBase))','if (mMarkUiDict.Remove(preEntId, out MiniMapMarkUIBase miniMapMarkUIBase))')
    text=text.replace('SpawnMarkCacheData value2 = default;\n\t\tif (mNeedSpawnMarkDataDict.Remove(preEntId, ref value2))','if (mNeedSpawnMarkDataDict.Remove(preEntId, out SpawnMarkCacheData value2))')
    text=text.replace('MiniMapMarkUIBase miniMapMarkUIBase = default;\n\t\tif (mMarkUiDict.Remove(entId, ref miniMapMarkUIBase))','if (mMarkUiDict.Remove(entId, out MiniMapMarkUIBase miniMapMarkUIBase))')
    text=text.replace('item.Deconstruct(ref ecEntityIdentifier, ref spawnMarkCacheData);','ecEntityIdentifier = item.Key;\n\t\t\tspawnMarkCacheData = item.Value;')
    text=text.replace('MapEntityTipDotNodeLinker componentInChildren = ((Component)value).GetComponentInChildren<MapEntityTipDotNodeLinker>(true);','MiniMapMarkTipDotLink componentInChildren = ((Component)value).GetComponentInChildren<MiniMapMarkTipDotLink>(true);')
    for a,b in [('EcEntity','IMiniMapSpawnEntity'),('GridGroundComponent','IMiniMapMarkGridGround'),('GamePosData','IMiniMapMarkCell'),('MapInfo','MiniMapMarkMapInfo'),('MiniMap','MiniMapSpawnContext')]:text=re.sub(r'\b'+a+r'\b',b,text)
    emit('MiniMapMarkSpawnManager.cs',wrap(text))
    text=read('MiniMapMarkPoolHandler')
    start=text.index('\t[StructLayout(');end=text.index('\tprivate Transform mDefaultSlot;')
    text=text[:start]+text[end:]
    text=text.replace('[Inject]\n\tprivate IResourceLoader ResourceLoader { get; set; }','private MiniMapSpawnContext Context { get; }')
    text=text.replace('MiniMapMarkPoolHandler(Transform defaultSlot)','MiniMapMarkPoolHandler(Transform defaultSlot, MiniMapSpawnContext context)').replace('mDefaultSlot = defaultSlot;','mDefaultSlot = defaultSlot;\n\t\tContext = context;')
    text=re.sub(r'\bDisposableBag\b','MiniMapSpawnDisposableBag',text).replace('Disposable.AddTo<AssetOperationHandle<GameObject>>(ResourceLoader.LoadPrefab(markPath), ref mDisposableBag)','MiniMapSpawnDisposable.AddTo(Context.LoadMarkPrefab(markPath), ref mDisposableBag)')
    text=text.replace('ServiceProviderExtensions.Instantiate(((Component)(object)mDefaultSlot).GetSceneContainer(), prefab, slot, false)','Context.InstantiateMark(prefab, slot, false)')
    start=text.index('\t[AsyncStateMachine(');text=text[:start]+'''private async UniTask DoLelayDestroYield(float delayS, CancellationToken cancellationToken)
    {
        await UniTask.WaitForSeconds(delayS, false, (PlayerLoopTiming)8, cancellationToken, false);
        foreach (LinkedList<MiniMapMarkUIBase> current in mMarkPoolDict.Values)
        {
            if (current.Count > 0)
            {
                LinkedListNode<MiniMapMarkUIBase> node = current.First;
                do
                {
                    GameObject gameObject = node.Value.gameObject;
                    LinkedListNode<MiniMapMarkUIBase> previous = node;
                    node = previous.Next;
                    current.Remove(previous);
                    Object.Destroy(gameObject);
                } while (node != null);
            }
        }
        mCancellationTokenSource = null;
    }
}
'''
    for prefix in ['using System.Diagnostics;\n','using System.Runtime.CompilerServices;\n','using System.Runtime.InteropServices;\n','using Cysharp.Threading.Tasks.CompilerServices;\n']:text=text.replace(prefix,'')
    emit('MiniMapMarkPoolHandler.cs',wrap(text))
    emit('IMiniMapMarkSpawnSupporter.cs',wrap(read('IMiniMapMarkSpawnSupporter')))
    emit('ETipDotLevelType.cs',wrap(read('ETipDotLevelType')))
    emit('MiniMapSpawnContext.cs',PORTS)
    r3_bag=r3_bag.resolve();inputs.append(record(r3_bag))
    if sha(r3_bag.read_bytes())!='45fe592d7201b19219329c15fc87760a4372e2560d1d0bbbb9e0f4bdbed69fdd':raise ValueError('Pinned R3 DisposableBag source changed')
    text=r3_bag.read_text(encoding='utf-8-sig').replace('\r\n','\n')
    text=text.replace('namespace R3;','using System;\nnamespace Zjcs.Official.World.Map13MiniMap\n{').replace('DisposableBag','MiniMapSpawnDisposableBag').replace('IDisposable[]?','IDisposable[]').replace('items = new IDisposable[capacity];','this = default;\n        items = new IDisposable[capacity];')+'\n}\n'
    emit('MiniMapSpawnDisposableBag.cs',text)
    license=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapWindows/R3-LICENSE.txt'
    if not license.exists():license=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapWindows/LICENSE-R3.txt'
    inputs.append(record(license));emit('R3-LICENSE.txt',license.read_text(encoding='utf-8-sig'))
    emit('Zjcs.Official.World.Map13MiniMapMarkSpawn.asmdef',encode(dict(name='Zjcs.Official.World.Map13MiniMapMarkSpawn',references=list(dict.fromkeys(['Zjcs.Official.World.Map13MiniMapMarkData','Zjcs.Official.World.Map13MiniMapMarks']+json.loads((ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapMarks/Zjcs.Official.World.Map13MiniMapMarks.asmdef').read_bytes())['references'])),autoReferenced=True)).decode())
    meta(DEST,folder=True)
    write(ROOT/'assets/reference/official-map/map13-minimap-mark-spawn-code-source-manifest.json',encode(dict(format=1,mapClassId=13,inputs=inputs,outputs=outputs,generator=record(Path(__file__).resolve()),r3=dict(repository='https://github.com/Cysharp/R3',commit='a8d5fb3c2d0afd124223084e94c9e0ee73497510',path='src/R3/DisposableBag.cs',license='MIT'),operation='Copy the complete original spawn manager, marker pool, supporter and tip level enum. Recover asynchronous delayed pool destruction from its original state machine. Preserve dictionary/list iteration, resident marks, original replacement-cache identity, last-in pool reuse, disposal and event ordering. Replace original services with explicit read/load/instantiate/performance ports. Reuse pinned complete R3 DisposableBag, adapting namespace, nullable array syntax and explicit constructor default initialization for Unity C#.',limitations=['Bind actual entity identities, original grid cell ordering, original marker assets and tip-dot state before formal map validation.'])))
    print('Original minimap marker spawn:',len(outputs),'outputs')

PORTS='''using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.World.Map13MiniMap
{
    public interface IMiniMapSpawnEntity:IMiniMapMarkEntity { EcEntityIdentifier Id { get; } }
    public interface IMiniMapMarkCell { bool ExistEntitys();IEnumerable<IMiniMapSpawnEntity> Entitys { get; } }
    public interface IMiniMapMarkGridGround { IMiniMapMarkCell GetPosData(Pos2d position); }
    public interface IMiniMapMarkPerformance
    { void RegisterSpawnManager(MiniMapMarkSpawnManager manager);void UnregisterSpawnManager(MiniMapMarkSpawnManager manager); }
    public interface IMiniMapMarkTipState { void SetState(ETipDotLevelType value); }
    // Binding bridge to the original tip-dot node component; no replacement UI.
    public sealed class MiniMapMarkTipDotLink:MonoBehaviour { public IMiniMapMarkTipState TipLevelState; }
    public abstract class MiniMapSpawnContext:MiniMapMarkContext
    {
        public abstract IMiniMapMarkDataQueries MarkDataQueries { get; }
        public abstract bool IsEntityInViewRange(IMiniMapSpawnEntity entity);
        public abstract string GetMapEntityMiniMapMark(IMiniMapSpawnEntity entity,int groupId,int map,string tag);
        public abstract IMiniMapDescriptionAssetLease<GameObject> LoadMarkPrefab(string path);
        public abstract GameObject InstantiateMark(GameObject prefab,Transform parent,bool worldPositionStays);
    }
    internal static class MiniMapSpawnDisposable
    {
        public static T AddTo<T>(T value,ref MiniMapSpawnDisposableBag bag) where T:IDisposable{bag.Add(value);return value;}
    }
}
'''

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--r3-bag',type=Path,required=True);args=parser.parse_args();build(args.r3_bag)
