"""Migrate the original Map13 minimap marker supporters and tip-node linker."""
import hashlib,json,re
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import write,encode,sha,meta
from prepare_unity_map13_minimap_sources import record
from prepare_unity_map13_minimap_mark_core import method

SOURCE=ROOT/'.tools/source-packages/current/871_156598/official-code'
DEST=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapMarkSupports'

def build():
    inputs=[];outputs=[]
    def read(relative):
        p=SOURCE/relative;inputs.append(record(p));return p.read_text(encoding='utf-8-sig').replace('\r\n','\n')
    def emit(name,text):
        p=DEST/name;write(p,text.encode());guid=hashlib.md5(('zjcs-official:'+p.relative_to(ROOT/'unity').as_posix()).encode()).hexdigest()
        m=Path(str(p)+'.meta')
        if m.exists() and not re.search(r'(?m)^guid: '+guid+r'\s*$',m.read_text(encoding='utf-8-sig')):raise ValueError('Unrelated marker supporter metadata')
        importer='AssemblyDefinitionImporter' if p.suffix=='.asmdef' else 'MonoImporter'
        write(m,('fileFormatVersion: 2\nguid: '+guid+'\n'+importer+':\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode());outputs.append(dict(**record(p),meta_sha256=sha(m.read_bytes())))
    def wrap(text):
        text=re.sub(r'^using (?:Common|Framework(?:\.[\w.]+)?);\n','',text,flags=re.M)
        at=re.search(r'(?m)^(?:\[|public )',text).start()
        return text[:at]+'''namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.NativeLabelUI;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.EntityLifecycle;
'''+text[at:]+'\n}\n'
    names=['MiniMapActiveSwitcherSupport','MiniMapWorldStageSwitcherSupport','MiniMapActiveByNPCVisibleSupporter','MiniMapPeriodicitySwitcherSupport','MinimapMarkKeepSizeObjSupport']
    for name in names:
        text=read('client-source/'+name+'.cs')
        text=re.sub(r'\bEcEntity\b','IMiniMapMarkEntity',text)
        text=re.sub(r'\bWorldStageComponent\b','IMiniMapManagedStage',text)
        text=re.sub(r'\bMySerializable\b','object',text)
        text=text.replace('stageComponent.StateUpdateEvent','((IMiniMapManagedStage)stageComponent).StateUpdateEvent')
        text=text.replace('MiniMap miniMap','MiniMapMarkContext miniMap').replace('MiniMap minimap','MiniMapMarkContext minimap') if name=='MinimapMarkKeepSizeObjSupport' else text
        # Preserve the original serialized field; Rename is editor-only metadata.
        text=re.sub(r'^\s*\[Rename\([^\n]*\)\]\n','\n',text,flags=re.M)
        if name=='MiniMapPeriodicitySwitcherSupport':
            text=text.replace('private MiniMapMarkUIBase mMarkUI;','private MiniMapMarkUIBase mMarkUI;\n\tprivate IMiniMapMarkSupportContext Context => (IMiniMapMarkSupportContext)mMarkUI.MiniMap;')
            text=text.replace('MeManager.MainPlayer.OnPeriodicityInteractionChanged','Context.SupportPlayer.OnPeriodicityInteractionChanged').replace('MeManager.MainPlayer != null','Context.SupportPlayer != null').replace('MeManager.MainPlayer.Id','Context.SupportPlayer.Id')
            text=text.replace('EntityUtils.GetPeriodicityInteractionId(','Context.SupportQueries.GetPeriodicityInteractionId(').replace('EntityUtils.GetEntityOwnerId(','MiniMapMarkSupportRules.GetEntityOwnerId(')
            text=text.replace('Parsers.Instance.MapEntityPeriodicityInteractionInfoPackParser.GetPeriodicityInteractionId(','Context.SupportQueries.GetOfflinePeriodicityInteractionId(')
            text=text.replace('Parsers.Instance.MapPeriodicityInteractionInfoParser.TryGet(','Context.SupportQueries.TryGetPeriodicity(')
            text=text.replace('MapMyEntityUtils.CheckCanPeriodicityInteraction(','MiniMapMarkSupportRules.CheckCanPeriodicityInteraction(Context, ')
        emit(name+'.cs',wrap(text))
    for name in ['INodeInstanceInfo','BaseNodeInstanceInfo','MapEntityNodeInstanceInfo','TipDotNodeKey','TipDotStateInfo']:
        text=read('client-source/'+name+'.cs');text=re.sub(r'\bEcEntity\b','IMiniMapMarkEntity',text)
        if name=='TipDotNodeKey':
            text=text.replace('public struct TipDotNodeKey(string graphName, string nodeName, INodeInstanceInfo instanceInfo)','public struct TipDotNodeKey')
            text=text.replace('public string GraphName = (string.IsNullOrEmpty(graphName) ? graphName : string.Intern(graphName));','public string GraphName;').replace('public string NodeName = (string.IsNullOrEmpty(nodeName) ? nodeName : string.Intern(nodeName));','public string NodeName;').replace('public INodeInstanceInfo InstanceInfo = instanceInfo;','''public INodeInstanceInfo InstanceInfo;
    public TipDotNodeKey(string graphName,string nodeName,INodeInstanceInfo instanceInfo)
    {
        GraphName=string.IsNullOrEmpty(graphName)?graphName:string.Intern(graphName);
        NodeName=string.IsNullOrEmpty(nodeName)?nodeName:string.Intern(nodeName);
        InstanceInfo=instanceInfo;
    }''')
        emit(name+'.cs',wrap(text))
    text=read('client-source/MinimapMarkTipDotLinkerSupport.cs')
    text=text.replace('MeManager.Instance.IsLeader','((IMiniMapMarkSupportContext)markUi.MiniMap).IsLeader').replace('miniMapEntityMark.Entity.Id','((IMiniMapSpawnEntity)miniMapEntityMark.Entity).Id')
    start=text.index('\t\t\tGLoggerErrorInterpolatedStringHandler');end=text.index('\n\t\t}',start)
    text=text[:start]+'\t\t\tDebug.LogError("小地图Mark[" + name + "]上的组件MinimapMarkTipDotLinkerSupport没挂载物件红点，请挂载或者删除此脚本");'+text[end:]
    emit('MinimapMarkTipDotLinkerSupport.cs',wrap(text))
    text=read('client-source/MapEntityTipDotNodeLinker.cs')
    text=text.replace('[Inject]\n\tprivate ITipDotSystemController Controller { get; set; }','public IMiniMapTipDotController Controller { get; set; }')
    text=text.replace('[Inject]\n\tprivate TipDotSystem TipDotSystem { get; set; }','public IMiniMapTipDotSystem TipDotSystem { get; set; }\n\tpublic IMiniMapMarkSupportContext Context { get; set; }\n\tpublic MiniMapManagedContext MiniMap { get; set; }')
    text=text.replace('TipDotSystem tipDotSystem','IMiniMapTipDotSystem tipDotSystem')
    text=text.replace('EcEntity ecEntity = MeManager.Instance.MapContainer?.GetEntity(entId);','IMiniMapSpawnEntity ecEntity = Context.HasMapContainer ? MiniMap.ManagedQueries.GetEntity(entId) : null;').replace('ecEntity.Container.ClassId','ecEntity.ContainerClassId')
    text=text.replace('MapInfo mapInfo = MapInfoManager.Instance.GetMapInfo(classId);','MiniMapMarkMapInfo mapInfo = MiniMap.MarkDataQueries.GetMapInfo(classId);')
    text=text.replace('MapUtils.GetMapEntityReddotGraphName(','MiniMapMarkSupportRules.GetMapEntityReddotGraphName(MiniMap, Context, ')
    start=text.index('\t\t\tGLoggerWarnInterpolatedStringHandler');end=text.index('\n\t\t}',start)
    text=text[:start]+'\t\t\tDebug.LogWarning("MapEntityTipDotNodeLinker未配置mContent字段, 请检查！Path: " + MiniMapMarkSupportRules.FullName(gameObject));'+text[end:]
    start=text.index('\t\t\tGLoggerErrorInterpolatedStringHandler');end=text.index('\n\t\t}',start)
    text=text[:start]+'\t\t\tDebug.LogError("地图[" + classId + "]上的物件[" + ecEntity.ClassId + "]并没有配红点图, 物件位置：" + ecEntity.GridTransform.Pos);'+text[end:]
    emit('MapEntityTipDotNodeLinker.cs',wrap(text))
    parser=read('common-source/Common/MapPeriodicityInteractionInfoParser.cs')
    helpers='\n'.join(method(parser,s) for s in ['public bool IsIgnoreCheck(int groupId, long OwnerId, long playerId)','public int CheckCanInteraction(int groupId, int count, bool isNegation = false)'])
    helpers=helpers.replace('public bool IsIgnoreCheck(','public static bool IsIgnoreCheck(IMiniMapMarkSupportQueries queries, ').replace('public int CheckCanInteraction(','public static int CheckCanInteraction(IMiniMapMarkSupportQueries queries, ').replace('MapPeriodicityInteractionInfo mapPeriodicityInteractionInfo = Get(groupId);','MiniMapPeriodicityInfo mapPeriodicityInteractionInfo = queries.GetPeriodicity(groupId);')
    source=read('client-source/MapMyEntityUtils.cs')
    helper=method(source,'public static int CheckCanPeriodicityInteraction(long ownerId, int periodicityInteractionId)')
    helper=helper.replace('CheckCanPeriodicityInteraction(long','CheckCanPeriodicityInteraction(IMiniMapMarkSupportContext context, long').replace('MapPeriodicityInteractionInfoParser mapPeriodicityInteractionInfoParser = Parsers.Instance.MapPeriodicityInteractionInfoParser;','IMiniMapMarkSupportQueries mapPeriodicityInteractionInfoParser = context.SupportQueries;').replace('mapPeriodicityInteractionInfoParser.IsIgnoreCheck(','IsIgnoreCheck(mapPeriodicityInteractionInfoParser, ').replace('mapPeriodicityInteractionInfoParser.CheckCanInteraction(','CheckCanInteraction(mapPeriodicityInteractionInfoParser, ').replace('MeManager.MainPlayer','context.SupportPlayer');helpers+='\n'+helper
    source=read('common-source/Common/EntityHandler.cs');helper=method(source,'public long GetEntityOwnerId(EcEntity ent, EntityOwnershipType filterType)')
    helper=helper.replace('public long','public static long').replace('EcEntity ent','IMiniMapMarkEntity ent').replace('EntitySourceComponent','IMiniMapManagedEntitySource');helpers+='\n'+helper
    source=read('common-source/MapUtils.cs');helper=method(source,'public static string GetMapEntityReddotGraphName(EcEntity ent, int groupId, int mapClassId, string tag)')
    helper=helper.replace('(EcEntity ent,','(MiniMapManagedContext minimap, IMiniMapMarkSupportContext context, IMiniMapMarkEntity ent,').replace('GridToArea(mapClassId,','minimap.MarkDataQueries.GridToArea(mapClassId,').replace('Parsers.Instance.MapAreaInfoParser.Get(mapClassId, key).AreaTag','minimap.MarkDataQueries.GetAreaTag(mapClassId, key)').replace('GroundType mapGridGroundType = Parsers.Instance.MapRootEntityLiteInfoPackParser.GetMapGridGroundType(mapClassId, pos, GroundType.IgnoreKey);','var mapGridGroundType = context.SupportQueries.GetGroundType(mapClassId, pos);').replace('EntityMapDisplayInfo entityMapDisplayInfo = Parsers.Instance.EntityMapDisplayInfoParser.Get(','MiniMapReddotDisplayInfo entityMapDisplayInfo = context.SupportQueries.GetEntityMapDisplay(').replace('Parsers.Instance.EntityExtendParameterInfoParser.Get(ent.ClassId)?.ReddotGraphName','context.SupportQueries.GetEntityExtendReddotGraphName(ent.ClassId)');helpers+='\n'+helper
    helpers+='\npublic static string FullName(GameObject value){var name=value.name;var parent=value.transform.parent;while(parent!=null){name=parent.name+"/"+name;parent=parent.parent;}return name;}\n'
    emit('MiniMapMarkSupportRules.cs',wrap('using UnityEngine;\npublic static class MiniMapMarkSupportRules\n{\n'+helpers+'\n}\n'))
    emit('MiniMapMarkSupportPorts.cs',PORTS)
    assembly=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapMarkManagers/Zjcs.Official.World.Map13MiniMapMarkManagers.asmdef';inputs.append(record(assembly))
    refs=list(dict.fromkeys(['Zjcs.Official.World.Map13MiniMapMarkManagers']+json.loads(assembly.read_bytes())['references']))
    emit('Zjcs.Official.World.Map13MiniMapMarkSupports.asmdef',encode(dict(name='Zjcs.Official.World.Map13MiniMapMarkSupports',references=refs,autoReferenced=True)).decode());meta(DEST,folder=True)
    write(ROOT/'assets/reference/official-map/map13-minimap-mark-supports-code-source-manifest.json',encode(dict(format=1,mapClassId=13,inputs=inputs,outputs=outputs,generator=record(Path(__file__).resolve()),operation='Copy complete original marker active/stage/NPC-visible/periodicity/keep-size/tip-node supporters, node keys/instance identities and linker. Copy original periodicity owner/count checks and display-over-extend reddot selection. Replace singleton and graph-service lookup with explicit local data/events and controller ports; adapt primary constructor for Unity C# and logging interpolation only. Preserve serialized fields, state ordering, original null behavior, subscription and release order.',limitations=['Actual selected marker prefabs, periodicity data/stage queries, original graph controller, map root binding and full map/Windows Player validation remain required.'])))
    print('Original minimap marker supporters:',len(outputs),'outputs')

PORTS='''using System;
using UnityEngine;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.World.Map13MiniMap
{
    // Projections only: no fabricated counts, graph levels, availability or owner IDs.
    public sealed class MiniMapPeriodicityInfo { public bool IsIgnoreOwner;public int PeriodicityMaxCount; }
    public sealed class MiniMapReddotDisplayInfo { public string ReddotGraphName; }
    public interface IMiniMapMarkSupportPlayer
    {
        long Id { get; }
        event Action<int,int> OnPeriodicityInteractionChanged;
        int GetWorldPeriodicityInteractionCount(int groupId);
    }
    public interface IMiniMapMarkSupportQueries
    {
        int GetPeriodicityInteractionId(IMiniMapMarkEntity entity);
        int GetOfflinePeriodicityInteractionId(int map,int loEntityId);
        bool TryGetPeriodicity(int id,out MiniMapPeriodicityInfo value);
        MiniMapPeriodicityInfo GetPeriodicity(int id);
        GroundType GetGroundType(int map,Pos2d position);
        MiniMapReddotDisplayInfo GetEntityMapDisplay(int entity,int group,int map,string area,GroundType ground,string tag);
        string GetEntityExtendReddotGraphName(int entity);
    }
    public interface IMiniMapMarkSupportContext
    {
        bool IsLeader { get; }
        bool HasMapContainer { get; }
        IMiniMapMarkSupportPlayer SupportPlayer { get; }
        IMiniMapMarkSupportQueries SupportQueries { get; }
    }
    public interface IMiniMapTipDotController
    {
        void LinkNodeStateChange(TipDotNodeKey key,GameObject owner,Action<TipDotStateInfo> callback);
        void UnlinkNodeStateChange(TipDotNodeKey key,GameObject owner,Action<TipDotStateInfo> callback);
    }
    public interface IMiniMapTipDotSystem
    {
        bool IsShowTipDot { get; }
        Action OnTipDotShowChanged { get;set; }
    }
}
'''

if __name__=='__main__':build()
