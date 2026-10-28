"""Migrate the original private-region minimap manager and player UI lifecycle."""
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import encode,sha,source_path,write
from prepare_unity_map13_prop_parts import meta
from prepare_unity_map13_minimap_sources import record

DEST=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapRegions'
CODE=ROOT/'.tools/source-packages/current/871_156598/official-code/client-source'


def build():
    outputs=[];inputs=[]
    def emit(name,text):
        path=DEST/name;write(path,text.encode());meta(path,'MonoImporter' if path.suffix=='.cs' else 'AssemblyDefinitionImporter');outputs.append(record(path))
    path=CODE/'MapMeCharacterUIBase.cs';inputs.append(record(path));text=path.read_text(encoding='utf-8-sig').replace('\r\n','\n')
    text=text.replace('using Framework.DependencyInjection;','using Zjcs.Official.World.NativeMap13Labels;')
    text=text.replace('IMyMapController','IMiniMapCharacterController').replace('IMapMyCharacterManagerProxy','IMiniMapCharacterManager')
    text=text.replace('\t[Inject]\n\tprivate IMiniMapCharacterManager MapMyCharacterManager { get; set; }','\tpublic IMiniMapCharacterManager MapMyCharacterManager { get; set; }')
    at=text.index('public class ');text=text[:at]+'namespace Zjcs.Official.World.Map13MiniMap\n{\n'+text[at:]+'\n}\n';emit('MapMeCharacterUIBase.cs',text)
    path=CODE/'MiniMapPrivateRegionMaskManager.cs';inputs.append(record(path));text=path.read_text(encoding='utf-8-sig').replace('\r\n','\n')
    begin=text.index('\t[StructLayout(LayoutKind.Auto)]');end=text.index('\tprivate const int kInactivedRegionZ',begin);text=text[:begin]+text[end:]
    text=text.replace('using Common;','using Zjcs.Official.World.NativeTerrain.Common;\nusing Zjcs.Official.ProfessionUI;')
    for line in ['using System.Diagnostics;','using System.Runtime.CompilerServices;','using System.Runtime.InteropServices;','using Cysharp.Threading.Tasks.CompilerServices;','using Framework;','using Framework.DependencyInjection;','using R3;']:text=text.replace(line+'\n','')
    text=text.replace('public MiniMap MiniMap;','public MiniMapRegionContext MiniMap;').replace('private DisposableBag mDisposableBag;','private readonly MiniMapRegionAssetBag mDisposableBag = new MiniMapRegionAssetBag();')
    begin=text.index('\t[Inject]');end=text.index('\tprotected override void Start()',begin);text=text[:begin]+text[end:]
    text=text.replace('MapMyEntityUtils.GetMapStatusListInTeam(MapRootManagerProxy, MiniMap.MapClassId)','MiniMap.GetMapStatusListInTeam(MiniMap.MapClassId)').replace('MapRootManagerProxy.CurrentMapClassId','MiniMap.CurrentMapClassId').replace('MeManager.MainPlayer != null','MiniMap.HasMainPlayer').replace('MeManager.MainPlayer.OnMapStatusUpdate','MiniMap.OnMapStatusUpdate')
    old='AssetOperationHandle<GameObject> assetOperationHandle = Disposable.AddTo<AssetOperationHandle<GameObject>>(ResourceLoader.LoadAssetByGuid<GameObject>(data.RegionTilemapAssetGuidProxy.AssetGuid), ref mDisposableBag);'
    new='IMiniMapRegionAssetLease assetOperationHandle = mDisposableBag.Add(MiniMap.LoadAssetByGuid(data.RegionTilemapAssetGuidProxy.AssetGuid));'
    if old not in text:raise ValueError('Original resource retention boundary changed')
    text=text.replace(old,new).replace('ServiceProviderExtensions.Instantiate(ServiceProvider, assetOperationHandle.Result, data.RegionTilemapSlot, false)','MiniMap.InstantiateRegion(assetOperationHandle.Result, data.RegionTilemapSlot, false)')
    begin=text.index('\t[AsyncStateMachine(')
    text=text[:begin]+'''\t// The original compiler-generated state machine awaits this exact player-loop
\t// timing, then destroys only null/inactive entries in dictionary order.
\tprivate async UniTask DoLelayDestroYield(float delayS, CancellationToken cancellationToken)
\t{
\t\tawait UniTask.WaitForSeconds(delayS, false, (PlayerLoopTiming)8, cancellationToken, false);
\t\tmCacheList.Clear();
\t\tforeach (KeyValuePair<int, GameObject> current in mRegionGameObjectDict)
\t\t{
\t\t\tGameObject value = current.Value;
\t\t\tif (value == null || !value.activeSelf)
\t\t\t{
\t\t\t\tmCacheList.Add(current.Key);
\t\t\t\tif (value != null) Object.Destroy(value);
\t\t\t}
\t\t}
\t\tforeach (int key in mCacheList) mRegionGameObjectDict.Remove(key);
\t\tmCancellationTokenSource = null;
\t}
}
'''
    at=text.index('public class ');text=text[:at]+'namespace Zjcs.Official.World.Map13MiniMap\n{\nusing Object = UnityEngine.Object;\n'+text[at:]+'\n}\n';emit('MiniMapPrivateRegionMaskManager.cs',text)
    inputs.append(record(CODE.parent/'common-source/Common/MapRegionStatus.cs'))
    inputs.append(record(ROOT/'unity/Assets/Game/Runtime/Official/World/NativeTerrain/Common/MapRegionStatus.cs'))
    emit('MiniMapRegionContext.cs','''using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.World.Map13MiniMap
{
    // Consumed original MiniMap/resource/session surfaces. The full original
    // MiniMap supplies these from its actual controller and local map state.
    public abstract class MiniMapRegionContext : MonoBehaviour
    {
        public abstract int MapClassId { get; }
        public abstract int CurrentMapClassId { get; }
        public abstract bool HasMainPlayer { get; }
        public abstract IMapViewRangeHandler ViewRangeHandler { get; }
        public abstract event Action<IMapViewRangeHandler> OnVisibleRangeUpdate;
        public abstract event Action<int,int,MapRegionStatus,MapRegionStatus> OnMapStatusUpdate;
        public abstract List<MapRegionStatus> GetMapStatusListInTeam(int mapClassId);
        public abstract bool IsRangeInViewRange(Pos2d min,Pos2d max);
        public abstract IMiniMapRegionAssetLease LoadAssetByGuid(string guid);
        public abstract GameObject InstantiateRegion(GameObject prefab,Transform parent,bool worldPositionStays);
    }
    public interface IMiniMapRegionAssetLease : IDisposable { GameObject Result { get; } }
    internal sealed class MiniMapRegionAssetBag : IDisposable
    {
        private readonly List<IMiniMapRegionAssetLease> leases=new List<IMiniMapRegionAssetLease>();
        public IMiniMapRegionAssetLease Add(IMiniMapRegionAssetLease lease){leases.Add(lease);return lease;}
        public void Dispose(){foreach(var lease in leases)lease.Dispose();leases.Clear();}
    }
}
''')
    emit('Zjcs.Official.World.Map13MiniMapRegions.asmdef',encode(dict(name='Zjcs.Official.World.Map13MiniMapRegions',references=['Zjcs.Official.World.Map13MiniMapView','Zjcs.Official.World.NativeTerrain','Zjcs.Official.World.NativeMap13Labels','Zjcs.Official.ProfessionUI','UniTask'],autoReferenced=True)).decode())
    meta(DEST,folder=True)
    write(ROOT/'assets/reference/official-map/map13-minimap-region-manager-code-source-manifest.json',encode(dict(format=1,mapClassId=13,manifest='871_156598',inputs=inputs,outputs=outputs,generator=record(Path(__file__).resolve()),operation='Copy the complete original MapMeCharacterUIBase and MiniMapPrivateRegionMaskManager. Preserve serialized fields, initialization/controller subscriptions, region-public Z switching, reverse region traversal, original GUID loading/lease lifetime, inactive-cache reuse, exact ten-second cancellation/reset and LateUpdate await timing. Express the original compiler-generated async state machine as equivalent async C#; replace only namespaces and consumed MiniMap/session/resource interfaces. Reuse original UIBase and AssetGuidProxy.',limitations=['Manager core only; full original MiniMap and authoritative local status/resource/controller binding, formal input/save and Windows Player remain required.'])))
    print('Original minimap region manager migrated:',len(outputs),'files')


if __name__=='__main__':build()
