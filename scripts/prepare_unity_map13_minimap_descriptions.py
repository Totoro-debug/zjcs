"""Migrate complete original minimap region descriptions and explicit local service ports."""
from pathlib import Path
import re
from official_apk import ROOT
from prepare_unity_map13_sources import encode,write
from prepare_unity_map13_prop_parts import meta
from prepare_unity_map13_minimap_sources import record

SOURCE=ROOT/'.tools/source-packages/current/871_156598/official-code/client-source'
DEST=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapDescriptions'


def build():
    inputs=[];outputs=[]
    def emit(name,text):
        p=DEST/name;write(p,text.encode());meta(p,'MonoImporter' if p.suffix=='.cs' else 'AssemblyDefinitionImporter');outputs.append(record(p))
    for name in ['MiniMapRegionDescManager','MiniMapRegionDescItem','MapRegionDescItemUI','MiniMapRegionBoardLineManager']:
        p=SOURCE/(name+'.cs');inputs.append(record(p));text=p.read_text(encoding='utf-8-sig').replace('\r\n','\n')
        for ns in ['Common','Framework','Framework.DependencyInjection','Framework.Logging','Framework.Logging.InterpolatedStringHandler','R3']:text=text.replace('using '+ns+';\n','')
        text=re.sub(r'\bMiniMap(?= )','MiniMapDescriptionContext',text.replace('[Inject]\n\t','')).replace('MapRegionInfo','MiniMapRegionInfo').replace('DisposableBag','MiniMapDescriptionBag').replace('Disposable.AddTo','MiniMapDescriptionDisposable.AddTo').replace('GLoggerErrorInterpolatedStringHandler','MiniMapDescriptionErrorHandler').replace('Log.Error(','MiniMapDescriptionDiagnostic.Error(')
        text=text.replace('MeManager.MainPlayer','MiniMap.LabelServices.Player' if name=='MiniMapRegionDescManager' else ('mMiniMap.LabelServices.Player' if name=='MiniMapRegionDescItem' else 'Services.Player'))
        text=text.replace('Parsers.Instance.MapRootEntityLiteInfoPackParser.TryGetMapLiteData(',('MiniMap' if name=='MiniMapRegionDescManager' else 'mMinimap')+'.TryGetMapLiteData(')
        if name=='MiniMapRegionDescManager':
            text=text.replace('private GridMoverComponent mGridMover;','private IMiniMapGridMover mGridMover;').replace('MiniMapRegionInfoParser mapRegionInfoParser = Parsers.Instance.MiniMapRegionInfoParser;','MiniMapDescriptionContext mapRegionInfoParser = MiniMap;').replace('EntityUtils.GetMapSpawnCharacterEntityComponent<GridMoverComponent>(', 'MiniMap.GetSpawnGridMover(').replace('MapUtils.GetMapRegionPosList(', 'MiniMap.GetMapRegionPosList(').replace('Parsers.Instance.MiniMapRegionInfoParser.GetMapRegionDict(', 'MiniMap.GetMapRegionDict(').replace('UIUtils.Instantiate<MiniMapRegionDescItem>(value, DescSlot, EUIInstantiateFlag.AutoActive)', 'MiniMap.InstantiateDescription(value, DescSlot)').replace('mGridMover.Info.Walkable(Parsers.Instance, groundType)','mGridMover.Walkable(groundType)').replace('MiniMap.LabelServices.Player.GetWorldMapProgresses(mapClassId)','MiniMap.GetWorldMapProgresses(mapClassId)')
        elif name=='MiniMapRegionDescItem':
            text=text.replace('private IMapRootManagerProxy MapRootManagerProxy { get; set; }','private MiniMapDescriptionContext MapRootManagerProxy => mMiniMap;').replace('private IMyMapPositionHandlerProxy MyMapPositionHandler { get; set; }','private MiniMapDescriptionContext MyMapPositionHandler => mMiniMap;')
            text=text.replace('mMiniMap = mManager.MiniMap;','mMiniMap = mManager.MiniMap;\n\t\t\tServices = mMiniMap.LabelServices;\n\t\t\tmMapRegionDescItemUI.Services = Services;')
            text=text.replace('MapRootManagerProxy.MyGridVisibilityAgent.OnPosVisibilityUpdate','MapRootManagerProxy.OnPosVisibilityUpdate').replace('(Object)(object)MapRootManagerProxy.MyGridVisibilityAgent != (Object)null','MapRootManagerProxy.HasGridVisibilityAgent')
        elif name=='MapRegionDescItemUI':
            text=text.replace('private IResourceLoader ResourceLoader { get; set; }','public IMap13LabelServices Services { get; set; }\n\tprivate MiniMapDescriptionContext ResourceLoader => mMinimap;')
            text=text.replace('Parsers.Instance.MiniMapRegionInfoParser.Get(mMapClassId, mRegion)','minimap.GetRegion(mMapClassId, mRegion)').replace('mRegionStrengthDisplayUI.Init(mRegionCfg)','mRegionStrengthDisplayUI.Init(mRegionCfg.LabelInfo)').replace('MeManager.MainTeam != null && MeManager.MainTeam.TeamLeaderId != Services.Player.Id','Services.IsTeamMember')
            text=text.replace('Formula.Instance.CalcWorldMapRegionProgress(', 'Services.Data.CalcWorldMapRegionProgress(').replace('Formula.Int2Float(num)','((float)num / 10000f)').replace('LanguageUtils.GetRegionNameKey(', 'MiniMapDescriptionContext.GetRegionNameKey(')
            for action in ['Show','Hide']:
                for op in ['+','-']:text=text.replace('mMinimap.On'+action+'FullScreen '+op+'=', '_ = mMinimap.'+action+'FullScreenAction '+op)
            text=text.replace('MinimapRegionDisplayInfo minimapRegionDisplayInfo = Parsers.Instance.MinimapRegionDisplayInfoParser.Get(', 'MiniMapRegionDisplayInfo minimapRegionDisplayInfo = mMinimap.GetRegionDisplay(').replace('MinimapRegionDisplayInfo displayInfo','MiniMapRegionDisplayInfo displayInfo')
            text=text.replace('Parsers.Instance.EcDataCenterParser.GetCommonPrefabComponent<GridGroundComponent>(mMapClassId)','mMinimap.QueryCommonGridGround(mMapClassId)').replace('AssetOperationHandle<GameObject>','IMiniMapDescriptionAssetLease<GameObject>').replace('UIUtils.Instantiate(assetOperationHandle.Result, mHighlightSlotWithoutScale)','mMinimap.InstantiateDisplay(assetOperationHandle.Result, mHighlightSlotWithoutScale)')
            text=text.replace('Vector2.op_Implicit(new Vector2(minimapRegionDisplayInfo.CenterOffsetX, minimapRegionDisplayInfo.CenterOffsetY))','(Vector3)new Vector2(minimapRegionDisplayInfo.CenterOffsetX, minimapRegionDisplayInfo.CenterOffsetY)').replace('Vector2.op_Implicit(-((Component)this).transform.parent.localPosition)','(Vector2)(-((Component)this).transform.parent.localPosition)').replace('Vector2.op_Implicit(new Vector2(displayInfo.CenterOffsetX, displayInfo.CenterOffsetY))','(Vector3)new Vector2(displayInfo.CenterOffsetX, displayInfo.CenterOffsetY)')
        else:
            text=text.replace('mMinimap.TryGetMapLiteData','mMiniMap.TryGetMapLiteData').replace('Services.Player','mMiniMap.LabelServices.Player').replace('Parsers.Instance.MapRootEntityLiteInfoPackParser.TryGetMapLiteData(','mMiniMap.TryGetMapLiteData(').replace('MapUtils.GetMapRegionPosList(','mMiniMap.GetMapRegionPosList(')
            text=text.replace('((Component)(object)((Component)this).transform).GetSceneContainer();','mMiniMap.QuerySceneContainer(this);').replace('ServiceProviderExtensions.Instantiate<LineRendererDashedLine>(((Component)(object)((Component)this).transform).GetSceneContainer(), mDashedLinePrefab, ((Component)this).transform, false)','mMiniMap.InstantiateDisplay(mDashedLinePrefab.gameObject, transform).GetComponent<LineRendererDashedLine>()')
        at=text.index('public class ')
        emit(name+'.cs',text[:at]+'''namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using Zjcs.Official.World.NativeMap13Labels;
using Zjcs.Official.World.Map13GridVisibility;
using MapRegionStatus=Zjcs.Official.World.NativeMap13Labels.MapRegionStatus;
'''+text[at:]+'\n}\n')
    enum=ROOT/'.tools/source-packages/current/871_156598/official-code/common-source/Common/MapRegionType.cs';inputs.append(record(enum));emit('MapRegionType.cs',enum.read_text(encoding='utf-8-sig').replace('namespace Common;','namespace Zjcs.Official.World.Map13MiniMap\n{')+'\n}\n')
    maputils=ROOT/'.tools/source-packages/current/871_156598/official-code/common-source/MapUtils.cs';inputs.append(record(maputils))
    original=maputils.read_text(encoding='utf-8-sig');begin=original.index('\tpublic static Dictionary<int, List<Pos2d>> GetMapRegionPosList(');end=original.index('\n\tpublic static ',begin+1)
    method=original[begin:end].replace('public static Dictionary','public Dictionary',1).replace('Parsers.Instance.MapRootEntityLiteInfoPackParser.TryGetMapLiteData(','TryGetMapLiteData(').replace('Parsers.Instance.MapRegionInfoParser.AreaToRegion(','AreaToRegion(').replace('item.Deconstruct(ref num3, ref list);','num3 = item.Key;\n\t\t\tlist = item.Value;')
    begin=CONTEXT.index('        // Verbatim MapUtils.');end=CONTEXT.index('\n    }\n    internal struct',begin)
    emit('MiniMapDescriptionContext.cs',CONTEXT[:begin]+'        // Complete original MapUtils method; singleton lookup and IL deconstruction repaired.\n'+method+CONTEXT[end:])
    emit('MiniMapDescriptionDiagnostic.cs',DIAGNOSTIC)
    emit('Zjcs.Official.World.Map13MiniMapDescriptions.asmdef',encode(dict(name='Zjcs.Official.World.Map13MiniMapDescriptions',references=['Zjcs.Official.World.Map13MiniMap','Zjcs.Official.World.Map13MiniMapSizing','Zjcs.Official.World.Map13MiniMapRegions','Zjcs.Official.World.Map13MiniMapView','Zjcs.Official.World.Map13MiniMapCamera','Zjcs.Official.World.Map13MiniMapBorders','Zjcs.Official.World.Map13GridVisibility','Zjcs.Official.World.NativeTerrain','Zjcs.Official.World.NativeMap13Labels','Zjcs.Official.World.NativeLabelUI','Zjcs.Official.StoryPlayables'],autoReferenced=True)).decode());meta(DEST,folder=True)
    dependencies=[record(ROOT/'unity/Assets/Game/Runtime/Official/World/NativeMap13Labels'/p) for p in ['MapRegionInfo.cs','Map13LabelServices.cs','MapRegionStrengthDisplayUI.cs']]
    write(ROOT/'assets/reference/official-map/map13-minimap-descriptions-code-source-manifest.json',encode(dict(format=1,mapClassId=13,manifest='871_156598',inputs=inputs,outputs=outputs,dependencies=dependencies,generator=record(Path(__file__).resolve()),operation='Copy complete original description manager, item, full MapRegionDescItemUI, region border-line manager and MapRegionType. Preserve reverse area grouping, integer centers, junction/walkable and progress gates, text/rating/strength display, original village line/highlight and full-screen subscriptions, doubled-grid border path traversal with the original 1000-call limit, dashed-line pool and world coordinates. Adapt original singleton/resource/injection backends to explicit local context; reuse native label services, UIBase, formulas, language and original grid visibility. No UI layout or art substitute.',limitations=['Native region-description/highlight prefabs and complete root/local-context binding, formal map interactions/save and Windows Player must be verified.'])))
    print('Complete original minimap region descriptions migrated:',len(outputs),'files')


CONTEXT='''using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeMap13Labels;
using FullScreenAction=Zjcs.Official.StoryPlayables.GAction;
namespace Zjcs.Official.World.Map13MiniMap
{
    public interface IMiniMapGridMover { bool Walkable(GroundType groundType); }
    public interface IMiniMapDescriptionAssetLease<out T>:IDisposable { T Result { get; } }
    // Exact source fields needed by minimap; LabelInfo retains the original label formula inputs.
    public sealed class MiniMapRegionInfo
    {
        public MapRegionInfo LabelInfo;public List<int> Areas;public MapRegionType RegionTag;
        public int ClassId=>LabelInfo.ClassId;public int Region=>LabelInfo.Region;public int Level=>LabelInfo.Level;public long CombatRating=>LabelInfo.CombatRating;
    }
    public sealed class MiniMapRegionDisplayInfo
    {
        public float CenterOffsetX,CenterOffsetY,HighlightScaleX,HighlightScaleY;
        public string LinePrefabPath,HighlightPrefabPath,NameFormat;
    }
    public abstract class MiniMapDescriptionContext:MiniMapSizingContext
    {
        public abstract IMap13LabelServices LabelServices { get; }
        public abstract MiniMapSettings.MiniMapSetting MiniMapSetting { get; }
        public abstract FullScreenAction ShowFullScreenAction { get; }
        public abstract bool IsFullScreen { get; }
        public abstract bool HasGridVisibilityAgent { get; }
        public abstract event Action<Pos2d,bool,bool> OnPosVisibilityUpdate;
        public abstract bool TryGetMapLiteData(int map,out MapRootEntityLiteSingleInfo info);
        public abstract IMiniMapGridMover GetSpawnGridMover(int map);
        public abstract IReadOnlyDictionary<int,MiniMapRegionInfo> GetMapRegionDict(int map);
        public abstract MiniMapRegionInfo GetRegion(int map,int region);
        public abstract int AreaToRegion(int map,int area);
        public abstract int QueryRegion(Pos2d pos);
        public abstract List<int> GetWorldMapProgresses(int map);
        public abstract MiniMapRegionDisplayInfo GetRegionDisplay(int map,int region);
        public abstract object QueryCommonGridGround(int map);
        public abstract object QuerySceneContainer(Component component);
        public abstract MiniMapRegionDescItem InstantiateDescription(MiniMapRegionDescItem prefab,Transform parent);
        public abstract GameObject InstantiateDisplay(GameObject prefab,Transform parent);
        public abstract IMiniMapDescriptionAssetLease<GameObject> LoadPrefab(string path);
        public static string GetRegionNameKey(int map,int region)=>"region_"+map+"_"+region;
        // Verbatim MapUtils.GetMapRegionPosList traversal; only singleton lookups are explicit.
        public Dictionary<int,List<Pos2d>> GetMapRegionPosList(int map)
        {
            if(!TryGetMapLiteData(map,out var info))return null;
            var areas=new Dictionary<int,List<Pos2d>>();
            for(int y=info.MapGroundSize.Y-1;y>=0;y--)for(int x=info.MapGroundSize.X-1;x>=0;x--)
            {int area=info.PosDataArray[y][x].Area;if(area>=0){if(!areas.TryGetValue(area,out var positions))areas[area]=positions=new List<Pos2d>();positions.Add(new Pos2d(x,y));}}
            var regions=new Dictionary<int,List<Pos2d>>();foreach(var pair in areas){int region=AreaToRegion(map,pair.Key);if(!regions.TryGetValue(region,out var positions))regions[region]=positions=new List<Pos2d>();positions.AddRange(pair.Value);}return regions;
        }
    }
    internal struct MiniMapDescriptionBag:IDisposable
    {
        private List<IDisposable> values;public void Add(IDisposable value){if(values==null)values=new List<IDisposable>();values.Add(value);}
        public void Dispose(){if(values==null)return;foreach(var value in values)value.Dispose();values.Clear();}
    }
    internal static class MiniMapDescriptionDisposable { public static void AddTo<T>(T value,ref MiniMapDescriptionBag bag) where T:IDisposable=>bag.Add(value); }
}
'''
DIAGNOSTIC='''using System.Text;
namespace Zjcs.Official.World.Map13MiniMap
{
    internal struct MiniMapDescriptionErrorHandler
    {
        private StringBuilder text;public MiniMapDescriptionErrorHandler(int literals,int formatted,out bool enabled){text=new StringBuilder(literals);enabled=true;}
        public void AppendLiteral(string value)=>text.Append(value);public void AppendFormatted<T>(T value,int alignment=0,string format=null,string expression=null)=>text.Append(value);
        public override string ToString()=>text.ToString();
    }
    internal static class MiniMapDescriptionDiagnostic { public static void Error(ref MiniMapDescriptionErrorHandler message,object context,string member,string file,int line)=>UnityEngine.Debug.LogError(message.ToString()); }
}
'''

if __name__=='__main__':build()
