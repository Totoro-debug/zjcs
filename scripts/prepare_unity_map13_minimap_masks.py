"""Migrate original active/offline minimap mask managers and local visibility ports."""
import re
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import encode,write
from prepare_unity_map13_prop_parts import meta
from prepare_unity_map13_minimap_sources import record

SOURCE=ROOT/'.tools/source-packages/current/871_156598/official-code/client-source'
COMMON=SOURCE.parent/'common-source/Common'
DEST=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapMasks'


def build():
    inputs=[];outputs=[]
    def emit(name,text):
        p=DEST/name;write(p,text.encode());meta(p,'MonoImporter' if p.suffix=='.cs' else 'AssemblyDefinitionImporter');outputs.append(record(p))
    for name in ['MiniMapManagerBase','MiniMapMaskManagerBase','MiniMapMaskManager','OfflineMiniMapMaskManager']:
        p=SOURCE/(name+'.cs');inputs.append(record(p));text=p.read_text(encoding='utf-8-sig').replace('\r\n','\n')
        for ns in ['Common','Framework.DependencyInjection']:text=text.replace('using '+ns+';\n','')
        text=re.sub(r'\bMiniMap(?= )','MiniMapMaskContext',text.replace('[Inject]\n\t',''))
        text=text.replace('private IMapRootManagerProxy MapRootManagerProxy { get; set; }','private MiniMapMaskContext MapRootManagerProxy => MiniMap;').replace('private IMapMyCharacterManagerProxy MapMyCharacterManager { get; set; }','private MiniMapMaskContext MyCharacterContext => MiniMap;')
        text=text.replace('MapRootManagerProxy.MyContainer.ClassId','MapRootManagerProxy.CurrentMapClassId').replace('Parsers.Instance.MapRootEntityLiteInfoPackParser.TryGetMapLiteData(','MiniMap.TryGetMapLiteData(').replace('UIUtils.GetTilePosByGrid(','MiniMap.GetTilePosByGrid(')
        text=text.replace('MyGridVisibilityAgent','MiniMapVisibilityAgent').replace('MapRootManagerProxy.MiniMapVisibilityAgent','MapRootManagerProxy.VisibilityAgent').replace('myMapController.MapCharacter.ViewRange','MiniMap.CharacterViewRange').replace('MapMyCharacterManager.MyMeUiCharacter.GridPos','MyCharacterContext.CharacterGridPos')
        text=text.replace('MapRegionEntityInfoManager.Instance.ForEachMapRegionActivedAllGrid(','MiniMap.ForEachMapRegionActivedAllGrid(')
        text=text.replace('(DetectChunkCullingBounds)1','(TilemapRenderer.DetectChunkCullingBounds)1')
        # The original agent is a Unity object; keep Unity destroyed-object equality.
        at=text.index('public ')
        emit(name+'.cs',text[:at]+'''namespace Zjcs.Official.World.Map13MiniMap
{
using Object=UnityEngine.Object;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13GridVisibility;
'''+text[at:]+'\n}\n')
    for name in ['PlayerOfflineMiniMapDataResponse','PlayerOfflineMiniMapLiteEntityData','MapWorldActiveableEntityType']:
        p=COMMON/(name+'.cs');inputs.append(record(p));text=p.read_text(encoding='utf-8-sig').replace('\r\n','\n').replace('using MessagePack;\n','').replace('namespace Common;','namespace Zjcs.Official.World.Map13MiniMap\n{').replace(' : MySerializable','').replace('public override ushort GetOpCode','public ushort GetOpCode');text=re.sub(r'\s*\[(?:MessagePackObject\(false\)|Key\(\d+\))\]\s*\n','\n',text);emit(name+'.cs',text+'\n}\n')
    ui=SOURCE/'UIUtils.cs';inputs.append(record(ui));text=ui.read_text(encoding='utf-8-sig');begin=text.index('\tpublic static Vector3Int GetTilePosByGrid(');end=text.index('\n\tpublic static ',begin+1);method=text[begin:end].replace('public static Vector3Int','public Vector3Int',1).replace('MapUtils.MapGridToPos(mapClassId, grid)','GridToWorld(mapClassId, grid)');emit('MiniMapMaskContext.cs',CONTEXT.replace('        // Original tile conversion inserted here.',method))
    emit('Zjcs.Official.World.Map13MiniMapMasks.asmdef',encode(dict(name='Zjcs.Official.World.Map13MiniMapMasks',references=['Zjcs.Official.World.Map13MiniMap','Zjcs.Official.World.Map13MiniMapDescriptions','Zjcs.Official.World.Map13MiniMapSizing','Zjcs.Official.World.Map13MiniMapRegions','Zjcs.Official.World.Map13MiniMapView','Zjcs.Official.World.Map13MiniMapCamera','Zjcs.Official.World.Map13GridVisibility','Zjcs.Official.World.NativeTerrain','Zjcs.Official.WorldUI','Zjcs.Official.World.NativeMap13Labels'],autoReferenced=True)).decode());meta(DEST,folder=True)
    dependencies=[record(ROOT/'unity/Assets/Game/Runtime/Official/World'/relative) for relative in ['Map13MiniMapRegions/MapMeCharacterUIBase.cs','Map13MiniMap/MiniMapMaskHandler.cs','Map13GridVisibility/GridVisibilityCompressUtils.cs','Map13GridVisibility/MapGroundGridBoolData.cs']]
    write(ROOT/'assets/reference/official-map/map13-minimap-masks-code-source-manifest.json',encode(dict(format=1,mapClassId=13,manifest='871_156598',inputs=inputs,outputs=outputs,dependencies=dependencies,generator=record(Path(__file__).resolve()),operation='Copy complete original MiniMapManagerBase, mask base, active and offline mask managers, plus complete original offline minimap response/lite records and entity enum. Keep exact locked-tile arrays and reverse grid traversal, visible bounds/drag formulas (including original integer centers and aspect in both target extents), active-region fill, original visibility codec, per-frame unlock batching and full-screen subscriptions. Only namespace, transport annotations/base and map/player/grid singletons become explicit local ports. Reuse original UIBase/controller/mask handler/grid visibility.',limitations=['Native mask manager/prefab bindings, actual root/local-state provider, Play Mode rendering/input/save and Windows Player remain required.'])))
    print('Complete original active/offline minimap mask managers migrated:',len(outputs),'files')


CONTEXT='''using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.World.NativeTerrain.Common;
using Zjcs.Official.World.Map13GridVisibility;
namespace Zjcs.Official.World.Map13MiniMap
{
    public abstract class MiniMapVisibilityAgent:MonoBehaviour
    {
        public abstract GridVisibilityAgentComponent Component { get; }
        public abstract event Action<Pos2d,bool,bool> OnPosVisibilityUpdate;
    }
    public abstract class MiniMapMaskContext:MiniMapDescriptionContext
    {
        public abstract MiniMapVisibilityAgent VisibilityAgent { get; }
        public abstract int CharacterViewRange { get; }
        public abstract Pos2d CharacterGridPos { get; }
        public abstract IEnumerable<Pos2d> ForEachMapRegionActivedAllGrid(int map,int region);
        public Vector3 GridToWorld(int map,Pos2d grid)
        {
            if(!TryGetMapLiteData(map,out var info))throw new InvalidOperationException("Original minimap map data is unavailable.");
            return info.GridToPos(grid);
        }
        // Original tile conversion inserted here.
    }
}
'''

if __name__=='__main__':build()
