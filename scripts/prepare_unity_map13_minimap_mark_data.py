"""Migrate the six complete original live/offline minimap marker data classes."""
import re
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import encode,write,meta
from prepare_unity_map13_minimap_sources import record
from prepare_unity_map13_minimap_mark_core import method

SOURCE=ROOT/'.tools/source-packages/current/871_156598/official-code'
DEST=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapMarkData'

def logger(text):
    pattern=r'(?:GLoggerErrorInterpolatedStringHandler )?message = new GLoggerErrorInterpolatedStringHandler\([\s\S]*?Log\.Error\(ref message,[^\n]*\);'
    def replace(match):
        parts=[]
        for line in match[0].splitlines():
            literal=re.search(r'message\.AppendLiteral\(("(?:[^"\\]|\\.)*")\);',line)
            formatted=re.search(r'message\.AppendFormatted\((.*), 0, null, "(?:[^"\\]|\\.)*"\);',line)
            if literal:parts.append(literal[1])
            if formatted:parts.append(formatted[1])
        if not parts:raise ValueError('Unconverted original marker diagnostic')
        return 'UnityEngine.Debug.LogError(string.Concat(new object[] { '+', '.join(parts)+' }));'
    text=re.sub(pattern,replace,text)
    return text.replace('\t\tGLoggerErrorInterpolatedStringHandler message;\n','').replace('\t\tbool enabled;\n','')

def build():
    inputs=[];outputs=[]
    def read(p):inputs.append(record(p));return p.read_text(encoding='utf-8-sig').replace('\r\n','\n')
    def emit(name,text):
        p=DEST/name;write(p,text.encode());meta(p,'AssemblyDefinitionImporter' if p.suffix=='.asmdef' else 'MonoImporter');outputs.append(record(p))
    def wrap(text):
        text=re.sub(r'^using (?:Common|Framework(?:\.[\w.]+)?);\n','',text,flags=re.M)
        at=text.index('public ')
        return text[:at]+'''namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.World.NativeLabelUI.Framework;
'''+text[at:]+'\n}\n'
    types={'EcEntity':'IMiniMapMarkEntity','GridTransformComponent':'IMiniMapMarkGrid','WorldStageComponent':'IMiniMapMarkStage','WayPointComponent':'IMiniMapMarkWayPoint','WorldActiveableEntityComponent':'IMiniMapMarkWorldEntry','EntityExtendParameterInfo':'MiniMapMarkExtendInfo','MapWorldLoEntityInfoWrap':'MiniMapMarkLoEntityInfo','MapInfo':'MiniMapMarkMapInfo'}
    for name in ['MiniMapEntityMark','MiniMapEntityWayPointMark','MiniMapEntityEntryMark','MiniMapOfflineMark','MiniMapOfflineWayPointMark','MiniMapOfflineEntryMark']:
        text=read(SOURCE/'client-source'/(name+'.cs'))
        text=re.sub(r'\t\[Inject\]\n\tprivate IResourceLoader ResourceLoader \{ get; set; \}\n','',text)
        text=re.sub(r'\t\[Inject\]\n\tprivate IMapRootManagerProxy MapRootManagerProxy \{ get; set; \}','\tprivate IMiniMapMarkDataQueries MapRootManagerProxy => Queries;',text)
        text=re.sub(r'\t\[Inject\]\n\tprivate IMyMapPositionHandlerProxy MyMapPositionHandlerProxy \{ get; set; \}','\tprivate IMiniMapMarkDataQueries MyMapPositionHandlerProxy => Queries;',text)
        if name in ['MiniMapEntityMark','MiniMapOfflineMark']:
            anchor='public class '+name+' : MiniMapMarkBase\n{'
            text=text.replace(anchor,anchor+'\n\tprotected IMiniMapMarkDataQueries Queries { get; }\n')
            signature='public '+name+('(int mapClassId, int region, EcEntity entity)' if name=='MiniMapEntityMark' else '(int mapClassId, int loEntId, int entClassId, int state)')
            text=text.replace(signature,signature[:-1]+', IMiniMapMarkDataQueries queries)')
            constructor=method(text,signature[:-1]+', IMiniMapMarkDataQueries queries)');modified=constructor.replace('{','{\n\t\tQueries = queries;',1);text=text.replace(constructor,modified)
        else:
            signature='public '+name+('(int mapClassId, int region, EcEntity entity)' if name.startswith('MiniMapEntity') else '(int mapClassId, int loEntId, int entClassId, int state)')
            text=text.replace(signature,signature[:-1]+', IMiniMapMarkDataQueries queries)')
            text=text.replace(': base(mapClassId, region, entity)',': base(mapClassId, region, entity, queries)').replace(': base(mapClassId, loEntId, entClassId, state)',': base(mapClassId, loEntId, entClassId, state, queries)')
        text=logger(text)
        text=text.replace('Parsers.Instance.MapAreaInfoParser.Get(MapClassId, MapUtils.GridToArea(MapClassId, entity.GridTransform.Pos)).AreaTag','Queries.GetAreaTag(MapClassId, Queries.GridToArea(MapClassId, entity.GridTransform.Pos))')
        text=text.replace('Parsers.Instance.MapAreaInfoParser.Get(MapClassId, EntityWrapInfo.PosInfo.Area).AreaTag','Queries.GetAreaTag(MapClassId, EntityWrapInfo.PosInfo.Area)')
        text=text.replace('MapInfoManager.Instance.GetMapInfo(','Queries.GetMapInfo(')
        for original,replacement in [('Parsers.Instance.EntityMapUIDisplayInfoParser.Get(','Queries.GetEntityMapUIDisplay('),('Parsers.Instance.EntityExtendParameterInfoParser.Get(','Queries.GetEntityExtend('),('MapRegionEntityInfoManager.Instance.GetContainerEntityInfo(','Queries.GetContainerEntityInfo('),('Parsers.Instance.MapLogicEntityTagInfoParser.GetEntityTag(','Queries.GetEntityTag('),('Parsers.Instance.EcDataCenterParser.GetCommonPrefab(','Queries.GetCommonPrefab('),('MapUtils.GridToArea(','Queries.GridToArea('),('MapUtils.MapGridToPos(','Queries.MapGridToPos(')]:text=text.replace(original,replacement)
        text=text.replace('MapUtils.GetMapEntityNameKey(Entity, Entity.Container.ClassId)','MiniMapMarkDataUtils.GetMapEntityNameKey(Queries, Entity, Entity.ContainerClassId)')
        text=text.replace('MapUtils.GetMapEntityNameKey(EntityClassId, MapClassId, area, entityTag)','MiniMapMarkDataUtils.GetMapEntityNameKey(Queries, EntityClassId, MapClassId, area, entityTag)')
        text=text.replace('LanguageUtils.GetEcEntityDescTextKeyArg(MapClassId, Entity)','MiniMapMarkDataUtils.GetEcEntityDescTextKeyArg(Queries, MapClassId, Entity)').replace('LanguageUtils.GetLoEntityDescTextKeyArg(MapClassId, LoEntityId)','MiniMapMarkDataUtils.GetLoEntityDescTextKeyArg(Queries, MapClassId, LoEntityId)')
        text=text.replace('LanguageUtils.TextKeyArgWrap','MiniMapMarkTextKeyArg').replace('handlers[i].Init(Entity);','handlers[i].Init(Entity.Original);')
        text=text.replace('MeManager.MainPlayer.RegionIsActived(','Queries.RegionIsActived(').replace('MeManager.Instance.MeMapId','Queries.CurrentMeMapId').replace('MeManager.MainPlayer.DoEnterMap(','Queries.DoEnterMap(').replace('WayPointComponent.IsUsable(','MiniMapMarkWayPointRules.IsUsable(Queries, ')
        # Factory selection is unchanged. The original DI-supplied query services
        # are explicit constructor inputs, not ambient fabricated entity state.
        if name in ['MiniMapEntityMark','MiniMapOfflineMark']:
            signature='public static '+name+' Create('
            original=method(text,signature);new=original
            for subtype in ['MiniMapEntityEntryMark','MiniMapEntityWayPointMark','MiniMapEntityMark'] if name=='MiniMapEntityMark' else ['MiniMapOfflineEntryMark','MiniMapOfflineWayPointMark','MiniMapOfflineMark']:
                args='mapClassId, region, entity' if name=='MiniMapEntityMark' else 'mapClassId, loEntId, entClassId, state'
                count=3 if name=='MiniMapEntityMark' else 4
                new=new.replace('serviceProvider.CreateInstance<'+subtype+'>(new object['+str(count)+'] { '+args+' })','new '+subtype+'('+args+', MiniMapMarkDataUtils.RequiredQueries(serviceProvider))')
            text=text.replace(original,new)
        for old,new in types.items():text=re.sub(r'\b'+old+r'\b',new,text)
        emit(name+'.cs',wrap(text))
    source_utils=read(SOURCE/'common-source/MapUtils.cs');language=read(SOURCE/'common-source/Common/LanguageUtils.cs')
    signatures=['public static string GetMapEntityNameKey(EcEntity ent, int mapClassId)','public static string GetMapEntityNameKey(int entClassId, int mapClassId, int area, string tag)','public static string GetMapEntityDescKey(EcEntity ent, int mapClassId)','public static string GetMapEntityDescKey(int entClassId, int mapClassId, int area, string tag)']
    helpers=[]
    for signature in signatures:
        text=method(source_utils,signature).replace('('+signature.split('(',1)[1],'(IMiniMapMarkDataQueries queries, '+signature.split('(',1)[1],1)
        text=text.replace('GridToArea(mapClassId,','queries.GridToArea(mapClassId,').replace('return GetMapEntityNameKey(','return GetMapEntityNameKey(queries, ').replace('return GetMapEntityDescKey(','return GetMapEntityDescKey(queries, ')
        text=text.replace('MapInfoManager.Instance.GetMapInfo(','queries.GetMapInfo(').replace('GetEntityMapUIDisplayInfo(entClassId, mapInfo.MapGroupId, mapClassId, area, tag)','queries.GetEntityMapUIDisplay(entClassId, mapInfo.MapGroupId, mapClassId, queries.GetAreaTag(mapClassId,area), tag)')
        text=text.replace('LanguageUtils.GetEcEntityNameKey(entClassId)','$"entity_{entClassId}_name"').replace('LanguageUtils.GetEcEntityDescKey(entClassId)','$"entity_{entClassId}_desc"')
        text=re.sub(r'\bMapInfo\b','MiniMapMarkMapInfo',text);text=re.sub(r'\bEcEntity\b','IMiniMapMarkEntity',text);helpers.append(text)
    for signature in ['public static TextKeyArgWrap GetEcEntityDescTextKeyArg(int mapClassId, EcEntity ecEntity)','public static TextKeyArgWrap GetLoEntityDescTextKeyArg(int mapClassId, int loEntId)']:
        text=method(language,signature);text=text.replace('('+signature.split('(',1)[1],'(IMiniMapMarkDataQueries queries, '+signature.split('(',1)[1],1)
        text=text.replace('MapUtils.GetMapEntityDescKey(','GetMapEntityDescKey(queries, ').replace('EntityDescParamsUtils.GetEntityDescParams(','EntityDescParamsUtils.GetEntityDescParams(queries, ').replace('LoEntityDescParamsUtils.GetLoEntityDescParams(','LoEntityDescParamsUtils.GetLoEntityDescParams(queries, ').replace('MapRegionEntityInfoManager.Instance.GetContainerEntityInfo(','queries.GetContainerEntityInfo(').replace('Parsers.Instance.MapLogicEntityTagInfoParser.GetEntityTag(','queries.GetEntityTag(')
        text=text.replace('TextKeyArgWrap','MiniMapMarkTextKeyArg').replace('MapWorldLoEntityInfoWrap','MiniMapMarkLoEntityInfo');text=re.sub(r'\bEcEntity\b','IMiniMapMarkEntity',text);helpers.append(text)
    emit('MiniMapMarkDataUtils.cs',wrap('using System;\npublic static class MiniMapMarkDataUtils\n{\npublic static IMiniMapMarkDataQueries RequiredQueries(IServiceProvider provider)=>(IMiniMapMarkDataQueries)provider.GetService(typeof(IMiniMapMarkDataQueries))??throw new InvalidOperationException("Original minimap entity queries are unbound.");\n'+'\n'.join(helpers)+'\n}\n'))
    for name in ['IEntityDescParamsProvider','ILoEntityDescParamsProviders','EntityStageResultTransportDescParamsProvider','LoEntityStageResultTransportDescParamsProvider','EntityDescParamsUtils','LoEntityDescParamsUtils']:
        text=read(SOURCE/'common-source'/(name+'.cs'))
        for old,new in [('EcEntity','IMiniMapMarkEntity'),('MapWorldLoEntityInfoWrap','MiniMapMarkLoEntityInfo'),('StageResultTransportComponent','IMiniMapMarkStageTransport'),('MapTransportEntityTargetInfo','MiniMapMarkTransportTargetInfo')]:text=re.sub(r'\b'+old+r'\b',new,text)
        for function in ['TryGetParams','GetEntityDescParams','GetLoEntityDescParams']:
            text=text.replace(function+'(int mapClassId,',function+'(IMiniMapMarkDataQueries queries, int mapClassId,')
        text=text.replace('item.TryGetParams(mapClassId,','item.TryGetParams(queries, mapClassId,')
        text=text.replace('MapRegionEntityInfoManager.Instance.GetContainerEntityInfo(','queries.GetContainerEntityInfo(').replace('Parsers.Instance.MapTransportEntityInfoPackParser.GetMapTransportTargetInfo(','queries.GetMapTransportTargetInfo(')
        text=text.replace('LanguageUtils.GetMapNameKey(mapClassId)','$"Mainland_{mapClassId}"').replace('LanguageUtils.GetRegionNameKey(mapClassId, containerEntityInfo.PosInfo.Region)','$"region_{mapClassId}_{containerEntityInfo.PosInfo.Region}"').replace('LanguageUtils.GetRegionNameKey(mapTransportTargetInfo.TargetMapClassId, mapTransportTargetInfo.TargetRegion)','$"region_{mapTransportTargetInfo.TargetMapClassId}_{mapTransportTargetInfo.TargetRegion}"')
        emit(name+'.cs',wrap(text))
    waypoint=read(SOURCE/'common-source/Common/WayPointComponentImplement.cs')
    static=method(waypoint,'public static bool IsUsable(int mapClassId, int region)').replace('IsUsable(int','IsUsable(IMiniMapMarkDataQueries queries, int').replace('MeManager.MainPlayer == null','!queries.HasMainPlayer').replace('MeManager.MainPlayer.GetMapRegionStatus(','queries.GetMapRegionStatus(')
    instance=method(waypoint,'bool IWayPointComponent.IsUsable()').replace('bool IWayPointComponent.IsUsable()','public static bool IsEntityUsable(IMiniMapMarkDataQueries queries, Pos2d position)').replace('MeManager.MainMap?.Container == null','!queries.HasMainMapContainer').replace('MeManager.MainMap.Container.ClassId','queries.MainMapClassId').replace('MapUtils.GridToRegion(classId, Component.GridTransform.Pos)','queries.GridToRegion(classId, position)').replace('return IsUsable(classId, region)','return IsUsable(queries, classId, region)')
    emit('MiniMapMarkWayPointRules.cs',wrap('public static class MiniMapMarkWayPointRules\n{\n'+static+'\n'+instance+'\n}\n'))
    emit('MiniMapMarkDataPorts.cs',PORTS)
    emit('Zjcs.Official.World.Map13MiniMapMarkData.asmdef',encode(dict(name='Zjcs.Official.World.Map13MiniMapMarkData',references=['Zjcs.Official.World.Map13MiniMapMarks','Zjcs.Official.World.NativeTerrain','Zjcs.Official.World.Map13GridVisibility','Zjcs.Official.World.Map13Entities','Zjcs.Official.World.NativeLabelUI','UniTask'],autoReferenced=True)).decode())
    meta(DEST,folder=True)
    write(ROOT/'assets/reference/official-map/map13-minimap-mark-data-code-source-manifest.json',encode(dict(format=1,mapClassId=13,inputs=inputs,outputs=outputs,generator=record(Path(__file__).resolve()),reused=[record(ROOT/'unity/Assets/Game/Runtime/Official/World/Map13GridVisibility/GridTransformComponentInfo.cs'),record(ROOT/'unity/Assets/Game/Runtime/Official/World/Map13Entities/EntityMapUIDisplayInfo.cs')],operation='Copy all six original live/offline marker classes, including constructors, icon precedence, status/null branches, body-center arithmetic, original factory type selection and transfer/entry dispatch. Recover original name/description lookup helpers. Replace singleton/DI consumption with explicit typed read/command ports; configuration/placement projections expose only consumed fields and preserve actual source identity. Keep original live versus offline position/body-range differences. Adapt logging backend only.',limitations=['Bind all ports to actual local entities/config/state and original description-parameter/waypoint/transfer implementations; validate all original body shapes, marker states, display lookup and actions before formal minimap integration.'])))
    print('Original live/offline minimap marker data:',len(outputs),'outputs')

PORTS='''using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13GridVisibility;
using Zjcs.Official.World.Map13Entities;
namespace Zjcs.Official.World.Map13MiniMap
{
    // Read-only projections of the exact original entity/configuration objects.
    // Commands delegate to the local map backend; these ports invent no state.
    public interface IMiniMapMarkEntity
    {
        object Original { get; }
        int ClassId { get; }
        int ContainerClassId { get; }
        IMiniMapMarkGrid GridTransform { get; }
        T GetComponent<T>() where T:class;
        string GetEntityTag();
    }
    public interface IMiniMapMarkGrid
    {
        IMiniMapMarkEntity Entity { get; }
        GridTransformComponentInfo Info { get; }
        Pos2d Pos { get; }
        List<Pos2d> GetBodyRange();
        Vector3 GridToPosition(Pos2d position);
    }
    public interface IMiniMapMarkStage { int State { get; } bool StateEqual(int state);bool IsActive(); }
    public interface IMiniMapMarkStageTransport { int TargetLoEntityId { get; } }
    public interface IMiniMapMarkWayPoint { IMiniMapMarkEntity Entity { get; } bool IsUsable(); }
    public interface IMiniMapMarkWorldEntry { IMiniMapMarkEntity Entity { get; } IMiniMapMarkGrid GridTransform { get; } bool Active { get; } }
    public sealed class MiniMapMarkExtendInfo
    {
        public int CompleteState;
        public string DialogIconPrefab;
    }
    public sealed class MiniMapMarkMapInfo { public int MapGroupId; }
    public sealed class MiniMapMarkTransportTargetInfo { public int TargetMapClassId,TargetRegion; }
    public sealed class MiniMapMarkPositionInfo { public Pos2d Pos;public int Area,Region; }
    public sealed class MiniMapMarkSpawnEntityInfo { public int ClassId; }
    public sealed class MiniMapMarkLoEntityInfo
    {
        public int LoEntityId;
        public MiniMapMarkPositionInfo PosInfo;
        public MiniMapMarkSpawnEntityInfo DefSpawnEntityInfo;
    }
    public interface IMiniMapMarkDataQueries
    {
        int GridToArea(int map,Pos2d position);
        string GetAreaTag(int map,int area);
        MiniMapMarkMapInfo GetMapInfo(int map);
        EntityMapUIDisplayInfo GetEntityMapUIDisplay(int entityClassId,int mapGroupId,int map,string areaTag,string entityTag);
        MiniMapMarkExtendInfo GetEntityExtend(int entityClassId);
        MiniMapMarkLoEntityInfo GetContainerEntityInfo(int map,int loEntityId);
        string GetEntityTag(int map,int loEntityId);
        IMiniMapMarkEntity GetCommonPrefab(int entityClassId);
        Vector3 MapGridToPos(int map,Pos2d position);
        MiniMapMarkTransportTargetInfo GetMapTransportTargetInfo(int map,int loEntityId);
        bool RegionIsActived(int map,int region);
        int QueryRegion(Pos2d position);
        int CurrentMeMapId { get; }
        bool HasMainPlayer { get; }
        bool HasMainMapContainer { get; }
        int MainMapClassId { get; }
        int GridToRegion(int map,Pos2d position);
        MapRegionStatus GetMapRegionStatus(int map,int region);
        UniTask<bool> DoTransfer(IMiniMapMarkEntity entity);
        void DoEnterMap(int map,int loEntityId);
    }
}
'''

if __name__=='__main__':build()
