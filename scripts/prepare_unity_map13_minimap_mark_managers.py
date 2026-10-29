"""Port original live/private/public/offline minimap marker managers and events."""
import json,re,hashlib
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import write,meta,encode,sha
from prepare_unity_map13_minimap_sources import record
from prepare_unity_map13_minimap_mark_core import method

SOURCE=ROOT/'.tools/source-packages/current/871_156598/official-code'
DEST=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapMarkManagers'

def build():
    inputs=[];outputs=[]
    def read(p):inputs.append(record(p));return p.read_text(encoding='utf-8-sig').replace('\r\n','\n')
    def emit(name,text):
        p=DEST/name;write(p,text.encode());guid=hashlib.md5(('zjcs-official:'+p.relative_to(ROOT/'unity').as_posix()).encode()).hexdigest();m=p.with_name(p.name+'.meta')
        if m.exists() and not re.search(r'(?m)^guid: '+guid+r'\s*$',m.read_text(encoding='utf-8-sig')):raise ValueError('Unrelated minimap marker manager metadata')
        importer='AssemblyDefinitionImporter' if p.suffix=='.asmdef' else 'MonoImporter'
        write(m,('fileFormatVersion: 2\nguid: '+guid+'\n'+importer+':\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode());outputs.append(dict(**record(p),meta_sha256=sha(m.read_bytes())))
    def wrap(text):
        text=re.sub(r'^using (?:Common|Framework(?:\.[\w.]+)?);\n','',text,flags=re.M).replace('namespace Common;\n','')
        at=re.search(r'(?m)^(?:\[|public )',text).start()
        return text[:at]+'''namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using EcEntityIdentifier=Zjcs.Official.StoryPlayables.EcEntityIdentifier;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13EntityEvents;
using Zjcs.Official.World.Map13EntityVisibilityViews;
using Zjcs.Official.World.Map13Entities;
using Zjcs.Official.EntityLifecycle;
'''+text[at:].replace('GAction<','Zjcs.Official.World.NativeMap13Labels.GAction<')+'\n}\n'
    replacements={'MapWorldLoEntityInfoWrap':'MiniMapMarkLoEntityInfo','EcEntity':'IMiniMapSpawnEntity','WorldStageComponent':'IMiniMapManagedStage','MySerializable':'object','EntityExtendParameterInfo':'MiniMapMarkExtendInfo','MapPartInfoBase':'MiniMapManagedMapPartInfo','MapInfo':'MiniMapMarkMapInfo','AreaAgentComponent':'IMiniMapManagedAreaAgent','GridMoverComponent':'IMiniMapManagedGridMover'}
    for name in ['MiniMapMarkManager','MiniMapPrivateMarkManager','MiniMapPublicMarkManager','OfflineMiniMapMarkManager']:
        text=read(SOURCE/'client-source'/(name+'.cs'))
        if name=='MiniMapMarkManager':
            text=text.replace('[Inject]\n\tprivate IMapRootManagerProxy MapRootManagerProxy { get; set; }','private MiniMapManagedContext MarkContext => (MiniMapManagedContext)MiniMap;\n\tprivate MiniMapManagedContext MapRootManagerProxy => MarkContext;')
            text=text.replace('item.Deconstruct(ref ecEntityIdentifier, ref miniMapMarkUIBase);','ecEntityIdentifier = item.Key;\n\t\t\tminiMapMarkUIBase = item.Value;')
        elif name in ['MiniMapPrivateMarkManager','MiniMapPublicMarkManager']:
            text=text.replace('{\n','{\n\tprivate MiniMapManagedContext MarkContext => (MiniMapManagedContext)MiniMap;\n',1)
        else:
            text=text.replace('public MiniMap MiniMap;','public MiniMapManagedContext MiniMap;\n\tprivate MiniMapManagedContext MarkContext => MiniMap;')
            text=text.replace('[Inject]\n\tprivate IServiceProvider ServiceProvider { get; set; }','public IServiceProvider ServiceProvider { get; set; }')
            text=text.replace('EntityExtendParameterInfoParser entityExtendParameterInfoParser = Parsers.Instance.EntityExtendParameterInfoParser;','IMiniMapMarkDataQueries entityExtendParameterInfoParser = MarkContext.MarkDataQueries;').replace('entityExtendParameterInfoParser.Get(','entityExtendParameterInfoParser.GetEntityExtend(')
            text=text.replace('UIUtils.InstantiateAsync(markPath, EntitySlot,','MarkContext.InstantiateMarkAsync(markPath, EntitySlot,')
        for old,new in [
            ('MapInfoManager.Instance.GetMapPartInfo(','MarkContext.ManagedQueries.GetMapPartInfo('),
            ('MapInfoManager.Instance.GetMapInfo(','MarkContext.MarkDataQueries.GetMapInfo('),
            ('MapRootManagerProxy.AreaAgent','MapRootManagerProxy.ManagedAreaAgent'),
            ('MapRootManagerProxy.GridGround','MapRootManagerProxy.ManagedGridGround'),
            ('MapMyEntityUtils.GetCurrentMapStatus()','MarkContext.ManagedQueries.GetCurrentMapStatus()'),
            ('Parsers.Instance.MapRegionInfoParser.Get(mapClassId, region).Areas','MarkContext.GetRegion(mapClassId, region).Areas'),
            ('Parsers.Instance.EntityExtendParameterInfoParser.Get(','MarkContext.MarkDataQueries.GetEntityExtend('),
            ('MapUtils.GetMapEntityMiniMapMark(ent, mapInfo.MapGroupId, mapClassId, ent.GetEntityTag())','MarkContext.GetMapEntityMiniMapMark(ent, mapInfo.MapGroupId, mapClassId, ent.GetEntityTag())'),
            ('MapUtils.GetMapEntityMiniMapMark(entClassId, mapInfo.MapGroupId, mapClassId, area, entityTag)','MiniMapManagedRules.GetMapEntityMiniMapMark(MarkContext, entClassId, mapInfo.MapGroupId, mapClassId, area, entityTag)'),
            ('MeManager.Instance.MapData.TryGetStatusByContainerId(','MarkContext.ManagedQueries.TryGetStatusByContainerId('),
            ('MeManager.Instance.MapContainer.GetEntity(','MarkContext.ManagedQueries.GetEntity('),
            ('MeManager.Instance.MapData.AddEntityEvent','MarkContext.ManagedEvents.AddEntityEvent'),
            ('MeManager.Instance.MapData.RemoveEntityEvent','MarkContext.ManagedEvents.RemoveEntityEvent'),
            ('MeManager.Instance.MapData.ReplaceEntityEvent','MarkContext.ManagedEvents.ReplaceEntityEvent'),
            ('MeManager.MainPlayer.OnMapStatusUpdate','MarkContext.ManagedEvents.OnMapStatusUpdate'),
            ('MeManager.MainPlayer != null','MarkContext.HasMainPlayer'),
            ('MapMyEntityUtils.EntityShowTipForMe(myEnt)','MiniMapManagedRules.EntityShowTipForMe(MarkContext, myEnt)'),
            ('MapRegionEntityInfoManager.Instance.GetContainerEntityInfo(','MarkContext.MarkDataQueries.GetContainerEntityInfo('),
            ('MapRegionEntityInfoManager.Instance.GetEntityByMapRegion(','MarkContext.ManagedQueries.GetEntityByMapRegion('),
            ('Parsers.Instance.MapLogicEntityTagInfoParser.GetEntityTag(','MarkContext.MarkDataQueries.GetEntityTag('),
        ]:text=text.replace(old,new)
        for old,new in replacements.items():text=re.sub(r'\b'+old+r'\b',new,text)
        emit(name+'.cs',wrap(text))
    for kind in ['EcContainerAddEntity','EcContainerRemoveEntity','EcContainerReplaceEntity','EcEntityWorldStageComponentStateUpdate']:
        for name in [kind+'EventDelegate','I'+kind+'EventReceiver']:
            text=read(SOURCE/'common-source/Common'/(name+'.cs'))
            for old,new in replacements.items():text=re.sub(r'\b'+old+r'\b',new,text)
            emit(name+'.cs',wrap(text))
    source=read(SOURCE/'common-source/Common/MapPartInfoBase.cs');enum=read(SOURCE/'common-source/Common/EnumExt.cs')
    members='\n'.join(method(source,s) for s in ['public bool IsRegionStatusMix()','public virtual bool RegionStatusSupported(MapRegionStatus status)','public virtual bool RegionStatusOnlySupport(MapRegionStatus status)'])
    members=members.replace('status.MapRegionStatusSupport(SupportPublicRegion, SupportPrivateRegion)','MiniMapManagedRules.MapRegionStatusSupport(status, SupportPublicRegion, SupportPrivateRegion)')
    emit('MiniMapManagedMapPartInfo.cs',wrap('public class MiniMapManagedMapPartInfo\n{\npublic bool SupportPublicRegion,SupportPrivateRegion;\n'+members+'\n}\n'))
    source=read(SOURCE/'client-source/MapMyEntityUtils.cs')
    helpers='\n'.join(method(source,s) for s in ['public static bool EntityShowTipForMe(EcEntity ent)','public static bool EntityShowTipForMe(EntitySourceComponent entitySource)'])
    helpers=helpers.replace('EntityShowTipForMe(EcEntity ent)','EntityShowTipForMe(MiniMapManagedContext context, IMiniMapSpawnEntity ent)').replace('EntityShowTipForMe(EntitySourceComponent entitySource)','EntityShowTipForMe(MiniMapManagedContext context, IMiniMapManagedEntitySource entitySource)').replace('return EntityShowTipForMe(ent.GetComponent<EntitySourceComponent>())','return EntityShowTipForMe(context, ent.GetComponent<IMiniMapManagedEntitySource>())').replace('MeManager.MainPlayer.Id','context.ManagedQueries.MainPlayerId').replace('Parsers.Instance.EntityExtendParameterInfoParser.Get(','context.MarkDataQueries.GetEntityExtend(').replace('EntityExtendParameterInfo','MiniMapMarkExtendInfo')
    helpers+='\n'+method(enum,'public static bool MapRegionStatusSupport(this MapRegionStatus status, bool supportPublic, bool supportPrivate)').replace('this MapRegionStatus','MapRegionStatus')
    map_utils=read(SOURCE/'common-source/MapUtils.cs')
    for signature in ['public static string GetMapEntityMiniMapMark(EcEntity ent, int groupId, int mapClassId, string tag)','public static string GetMapEntityMiniMapMark(int entClassId, int groupId, int mapClassId, int area, string tag)']:
        text=method(map_utils,signature).replace('('+signature.split('(',1)[1],'(MiniMapManagedContext context, '+signature.split('(',1)[1],1)
        text=text.replace('GridToArea(mapClassId,','context.MarkDataQueries.GridToArea(mapClassId,').replace('return GetMapEntityMiniMapMark(','return GetMapEntityMiniMapMark(context, ').replace('GetEntityMapUIDisplayInfo(entClassId, groupId, mapClassId, area, tag)','context.MarkDataQueries.GetEntityMapUIDisplay(entClassId, groupId, mapClassId, context.MarkDataQueries.GetAreaTag(mapClassId, area), tag)').replace('Parsers.Instance.EntityExtendParameterInfoParser.Get(','context.MarkDataQueries.GetEntityExtend(')
        text=re.sub(r'\bEcEntity\b','IMiniMapSpawnEntity',text);helpers+='\n'+text
    emit('MiniMapManagedRules.cs',wrap('public static class MiniMapManagedRules\n{\n'+helpers+'\n}\n'))
    ownership=read(SOURCE/'common-source/Common/EntityOwnershipData.cs').replace('using MessagePack;\n','').replace('[MessagePackObject(false)]\n','').replace(' : MySerializable','').replace('override ushort','ushort');ownership=re.sub(r'\t\[Key\(\d+\)\]\n','',ownership)
    emit('EntityOwnershipData.cs',wrap(ownership))
    emit('MiniMapManagedContext.cs',PORTS)
    assembly=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapMarkSpawn/Zjcs.Official.World.Map13MiniMapMarkSpawn.asmdef';inputs.append(record(assembly))
    references=list(dict.fromkeys(['Zjcs.Official.World.Map13MiniMapMarkSpawn','Zjcs.Official.World.Map13EntityEvents','Zjcs.Official.World.Map13EntityVisibilityViews','Zjcs.Official.World.Map13Entities','Zjcs.Official.EntityLifecycle']+json.loads(assembly.read_bytes())['references']))
    emit('Zjcs.Official.World.Map13MiniMapMarkManagers.asmdef',encode(dict(name='Zjcs.Official.World.Map13MiniMapMarkManagers',references=references,autoReferenced=True)).decode());meta(DEST,folder=True)
    write(ROOT/'assets/reference/official-map/map13-minimap-mark-managers-code-source-manifest.json',encode(dict(format=1,mapClassId=13,inputs=inputs,outputs=outputs,generator=record(Path(__file__).resolve()),operation='Copy complete original common/private/public/offline marker managers and their entity/stage event delegates/receivers. Copy original map status support, entity ownership visibility and ownership DTO. Replace singleton lookups with explicit actual data/area/session ports, preserving filters, loops, state updates, subscriptions, owner conditions, relative grid registration and asynchronous prefab callback ordering.',limitations=['Bind actual map regions, entities, stages/events, ownership, offline responses and original mark prefabs; run full map state/input/persistence and Windows Player validation.'])))
    print('Original minimap marker managers:',len(outputs),'outputs')

PORTS='''using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13Entities;
namespace Zjcs.Official.World.Map13MiniMap
{
    public interface IMiniMapManagedStage:IMiniMapMarkStage
    { IMiniMapSpawnEntity Entity { get; } EcEntityWorldStageComponentStateUpdateEventDelegate StateUpdateEvent { get;set; } }
    public interface IMiniMapManagedGridMover { }
    public interface IMiniMapManagedEntitySource { IMiniMapSpawnEntity Entity { get; } EntityOwnershipData Ownership { get; } }
    public interface IMiniMapManagedAreaAgent
    { Dictionary<EcEntityIdentifier,IMiniMapSpawnEntity> GetAreaEntity(int area);int GridToRegion(int map,Pos2d position); }
    public sealed class MiniMapManagedEvents
    {
        public EcContainerAddEntityEventDelegate AddEntityEvent=new EcContainerAddEntityEventDelegate();
        public EcContainerRemoveEntityEventDelegate RemoveEntityEvent=new EcContainerRemoveEntityEventDelegate();
        public EcContainerReplaceEntityEventDelegate ReplaceEntityEvent=new EcContainerReplaceEntityEventDelegate();
        public event Action<int,int,MapRegionStatus,MapRegionStatus> OnMapStatusUpdate;
        public void FireMapStatusUpdate(int map,int region,MapRegionStatus previous,MapRegionStatus value)=>OnMapStatusUpdate?.Invoke(map,region,previous,value);
    }
    public interface IMiniMapManagedQueries
    {
        MiniMapManagedMapPartInfo GetMapPartInfo(int map);
        List<MapRegionStatus> GetCurrentMapStatus();
        bool TryGetStatusByContainerId(long container,out MapRegionStatus status);
        IMiniMapSpawnEntity GetEntity(EcEntityIdentifier id);
        IEnumerable<MiniMapMarkLoEntityInfo> GetEntityByMapRegion(int map,int region);
        long MainPlayerId { get; }
    }
    public abstract class MiniMapManagedContext:MiniMapSpawnContext
    {
        public override string GetMapEntityMiniMapMark(IMiniMapSpawnEntity entity,int group,int map,string tag)=>MiniMapManagedRules.GetMapEntityMiniMapMark(this,entity,group,map,tag);
        public abstract IMiniMapManagedQueries ManagedQueries { get; }
        public abstract MiniMapManagedEvents ManagedEvents { get; }
        public abstract IMiniMapManagedAreaAgent ManagedAreaAgent { get; }
        public abstract IMiniMapMarkGridGround ManagedGridGround { get; }
        public abstract void InstantiateMarkAsync(string path,Transform parent,Action<MiniMapMarkUIBase> callback);
    }
}
'''

if __name__=='__main__':build()
