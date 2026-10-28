"""Port complete official minimap resource-point selection/board/icon lifecycle."""
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import write, encode
from prepare_unity_map13_prop_parts import meta
from prepare_unity_map13_minimap_sources import record

DEST=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapResourcePoints'
SOURCE=ROOT/'.tools/source-packages/current/871_156598/official-code/client-source'


def build():
    inputs=[];outputs=[]
    def source(name):
        p=SOURCE/(name+'.cs');inputs.append(record(p));return p.read_text(encoding='utf-8-sig').replace('\r\n','\n')
    def emit(name,text):
        p=DEST/name;write(p,text.encode());meta(p,'MonoImporter' if p.suffix=='.cs' else 'AssemblyDefinitionImporter');outputs.append(record(p))
    def wrap(text):
        for ns in ['Common','Framework','Framework.DependencyInjection','Framework.Logging','Framework.Logging.InterpolatedStringHandler','R3']:
            text=text.replace('using '+ns+';\n','')
        start=text.index('public ');attribute=text.rfind('\n[',0,start)
        if attribute>=0:start=attribute+1
        text=text.replace('AssetGuidProxy<','Zjcs.Official.ProfessionUI.AssetGuidProxy<')
        return text[:start]+'namespace Zjcs.Official.World.Map13MiniMap\n{\nusing Object=UnityEngine.Object;\nusing Zjcs.Official.World.NativeMap13Labels;\nusing Zjcs.Official.World.NativeTerrain.Common;\nusing MapRegionStatus=Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;\n'+text[start:]+'\n}\n'
    def replace(text,old,new):
        if text.count(old)!=1:raise ValueError('Original source surface changed: '+old[:100])
        return text.replace(old,new)
    for name in ['MiniMapResourcePointData','AreaMiniMapResourcePointData','IMiniMapResourcePointBoard']:
        emit(name+'.cs',wrap(source(name)))
    text=source('MiniMapResourcePointManager')
    text=replace(text,'\t[Inject]\n\tprivate IResourceLoader ResourceLoader { get; set; }','\tprivate MiniMapResourcePointContext ResourceLoader => mMinimap;\n\tpublic MiniMapResourcePointContext Context => mMinimap;')
    text=replace(text,'\t[Inject]\n\tprivate IServiceProvider ServiceProvider { get; set; }','')
    text=text.replace('MiniMap mMinimap','MiniMapResourcePointContext mMinimap').replace('Init(MiniMap minimap)','Init(MiniMapResourcePointContext minimap)')
    text=replace(text,'mMinimap = minimap;','mMinimap = minimap;\n\t\tServices = minimap.LabelServices;')
    text=replace(text,'ItemMapLocationData locationInfos = Parsers.Instance.ItemMapLocationInfoParser.GetLocationInfos(itemId);','MiniMapResourcePointItemLocationData locationInfos = mMinimap.GetItemLocationInfo(itemId);')
    text=replace(text,'MapWorldRefreshGroupLocationInfo byAwardRule = Parsers.Instance.MapWorldRefreshGroupLocationInfoParser.GetByAwardRule(ruleId);','MiniMapResourcePointRefreshGroupInfo byAwardRule = mMinimap.GetRefreshGroupsByAwardRule(ruleId);')
    text=replace(text,'MapWorldRefreshGroupLocationInfo byMonsterId = Parsers.Instance.MapWorldRefreshGroupLocationInfoParser.GetByMonsterId(monsterId);','MiniMapResourcePointRefreshGroupInfo byMonsterId = mMinimap.GetRefreshGroupsByMonsterId(monsterId);')
    text=replace(text,'ResourceLoader.LoadAssetByGuid<GameObject>(value2.AssetGuid)','ResourceLoader.LoadPrefabByGuid(value2.AssetGuid)')
    text=replace(text,'ServiceProviderExtensions.Instantiate(ServiceProvider, assetOperationHandle.Result, ((Component)this).transform, false);','mMinimap.InstantiatePrefab(assetOperationHandle.Result, ((Component)this).transform, false);')
    text=text.replace('AssetOperationHandle<GameObject>','IResourcePointAssetLease<GameObject>').replace('DisposableBag','MiniMapResourcePointDisposableBag').replace('Disposable.AddTo<','MiniMapResourcePointDisposable.AddTo<')
    text=text.replace('GLoggerErrorInterpolatedStringHandler','MiniMapResourcePointErrorHandler').replace('Log.Error(','MiniMapResourcePointDiagnostic.Error(')
    emit('MiniMapResourcePointManager.cs',wrap(text))
    text=source('MiniMapResourcePointBoard')
    text=replace(text,'public int RefreshGroup => mRefreshGroup;','public int RefreshGroup => mRefreshGroup;\n\tpublic MiniMapResourcePointContext Context => mManager.Context;')
    text=replace(text,'mAreaDataDict = new Dictionary<int, AreaMiniMapResourcePointData>();','Services = Context.LabelServices;\n\t\tmAreaDataDict = new Dictionary<int, AreaMiniMapResourcePointData>();')
    text=replace(text,'MapEntityGroupInfo mapEntityGroupInfo = Parsers.Instance.MapEntityGroupInfoParser.Get(mRefreshGroup);','MiniMapResourcePointEntityGroupInfo mapEntityGroupInfo = Context.GetRefreshGroupInfo(mRefreshGroup);')
    text=text.replace('MeManager.MainPlayer != null','Context.HasMainPlayer').replace('MeManager.MainPlayer.','Context.')
    text=replace(text,'MapRegionInfoParser mapRegionInfoParser = Parsers.Instance.MapRegionInfoParser;','MiniMapResourcePointContext mapRegionInfoParser = Context;')
    text=text.replace('GLoggerErrorInterpolatedStringHandler','MiniMapResourcePointErrorHandler').replace('Log.Error(','MiniMapResourcePointDiagnostic.Error(')
    emit('MiniMapResourcePointBoard.cs',wrap(text))
    text=source('MiniMapResourcePointTapItem')
    text=replace(text,'\t[Inject]\n\tprivate IResourceLoader ResourceLoader { get; set; }','\tprivate MiniMapResourcePointContext ResourceLoader => ((MiniMapResourcePointBoard)mPointBoard).Context;')
    text=text.replace('AssetOperationHandle<Sprite>','IResourcePointAssetLease<Sprite>').replace('DisposableBag','MiniMapResourcePointDisposableBag').replace('Disposable.AddTo<','MiniMapResourcePointDisposable.AddTo<')
    emit('MiniMapResourcePointTapItem.cs',wrap(text))
    emit('MiniMapResourcePointContext.cs','''using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.World.NativeMap13Labels;
using MapRegionStatus=Zjcs.Official.World.NativeTerrain.Common.MapRegionStatus;
namespace Zjcs.Official.World.Map13MiniMap
{
    // The original configuration, player-state and resource-loader surfaces.
    // Concrete map integration supplies the selected local official data.
    public interface MiniMapResourcePointContext
    {
        int MapClassId { get; }
        bool HasMainPlayer { get; }
        IMap13LabelServices LabelServices { get; }
        event Action<int,int,MapRegionStatus,MapRegionStatus> OnMapStatusUpdate;
        MiniMapResourcePointEntityGroupInfo GetRefreshGroupInfo(int refreshGroup);
        int AreaToRegion(int mapClassId,int area);
        MapRegionStatus GetMapRegionStatus(int mapClassId,int region);
        MiniMapResourcePointItemLocationData GetItemLocationInfo(int itemId);
        MiniMapResourcePointRefreshGroupInfo GetRefreshGroupsByAwardRule(int ruleId);
        MiniMapResourcePointRefreshGroupInfo GetRefreshGroupsByMonsterId(int monsterId);
        IResourcePointAssetLease<GameObject> LoadPrefabByGuid(string guid);
        IResourcePointAssetLease<Sprite> LoadSprite(string path);
        GameObject InstantiatePrefab(GameObject prefab,Transform parent,bool worldPositionStays);
    }
    // Projections retain the consumed original nullable object/field boundaries.
    public sealed class MiniMapResourcePointEntityGroupInfo {public string Icon;}
    public sealed class MiniMapResourcePointItemLocationData {public IReadOnlyList<int> RuleIds;}
    public sealed class MiniMapResourcePointRefreshGroupInfo {public IEnumerable<int> RefreshGroupSet;}
    public interface IResourcePointAssetLease<out T>:IDisposable where T:UnityEngine.Object {T Result {get;}}
    internal struct MiniMapResourcePointDisposableBag:IDisposable
    {
        private List<IDisposable> leases;
        public void Add(IDisposable lease){if(leases==null)leases=new List<IDisposable>();leases.Add(lease);}
        public void Dispose(){if(leases==null)return;foreach(var lease in leases)lease.Dispose();leases.Clear();}
    }
    internal static class MiniMapResourcePointDisposable
    {
        public static T AddTo<T>(T lease,ref MiniMapResourcePointDisposableBag bag) where T:IDisposable {bag.Add(lease);return lease;}
    }
}
''')
    p=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMap/MiniMapDiagnostic.cs';inputs.append(record(p));text=p.read_text(encoding='utf-8-sig').replace('GLoggerErrorInterpolatedStringHandler','MiniMapResourcePointErrorHandler').replace('class Log','class MiniMapResourcePointDiagnostic')
    text=text.replace('internal static class MiniMapResourcePointDiagnostic\n    {','internal static class MiniMapResourcePointDiagnostic\n    {\n        public static void Error(System.Exception exception, ref MiniMapResourcePointErrorHandler message, object context, string member, string file, int line) { Debug.LogError(message.ToString()+"\\n"+exception); }')
    emit('MiniMapResourcePointDiagnostic.cs',text)
    emit('Zjcs.Official.World.Map13MiniMapResourcePoints.asmdef',encode(dict(name='Zjcs.Official.World.Map13MiniMapResourcePoints',references=['Zjcs.Official.World.NativeTerrain','Zjcs.Official.World.NativeMap13Labels','Zjcs.Official.ProfessionUI'],autoReferenced=True)).decode());meta(DEST,folder=True)
    dependencies=[record(ROOT/'unity/Assets/Game/Runtime/Official/World/NativeMap13Labels'/name) for name in ['MapUIBase.cs','UIBase.cs','MonoDictionary.cs']]
    write(ROOT/'assets/reference/official-map/map13-minimap-resource-points-code-source-manifest.json',encode(dict(format=1,mapClassId=13,manifest='871_156598',inputs=inputs,outputs=outputs,dependencies=dependencies,generator=record(Path(__file__).resolve()),operation='Copy all six complete original resource-point manager/board/tap/data classes. Reuse original MapUIBase/UIBase lifecycle, MonoDictionary and AssetGuidProxy. Preserve selection versus guide selection, cached board creation, area grouping, Public-only visibility, whole-board status-event refresh, custom-icon ordering/cache and retained asset leases. Replace only online DI/resource/parser/player accesses by consumed local interfaces, and logging backend; preserve original formulas, branch order and lifecycle.',limitations=['Local source-port interfaces require concrete official map/config/resource integration. Native resource-board prefabs, actual input/render/save and Windows Player remain required.'])))
    print('Complete original minimap resource-point code migrated:',len(outputs),'files')


if __name__=='__main__':build()
