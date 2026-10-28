"""Migrate the complete official minimap visible-range handlers."""
from pathlib import Path
from official_apk import ROOT
from prepare_unity_map13_sources import encode,sha,source_path,write
from prepare_unity_map13_prop_parts import meta

DEST=ROOT/'unity/Assets/Game/Runtime/Official/World/Map13MiniMapView'
CODE=ROOT/'.tools/source-packages/current/871_156598/official-code/client-source'


def build():
    inputs=[];outputs=[]
    def emit(name,text):
        path=DEST/name;write(path,text.encode());meta(path,'MonoImporter' if path.suffix=='.cs' else 'AssemblyDefinitionImporter')
        outputs.append(dict(path=source_path(path),sha256=sha(path.read_bytes())))
    for name in ['IMapViewRangeHandler','MapViewRangeHandlerBase','MapAllViewRangeHandler','MapCharacterViewRangeHandler']:
        path=CODE/(name+'.cs');raw=path.read_bytes();inputs.append(dict(path=source_path(path),sha256=sha(raw)))
        text=raw.decode('utf-8-sig').replace('\r\n','\n')
        text=text.replace('using Common;','using Zjcs.Official.World.NativeTerrain.Common;')
        text=text.replace('using Framework;','using Zjcs.Official.World.NativeMap13Labels;\nusing Zjcs.Official.EntityLifecycle;')
        text=text.replace('GridTransformComponent gridTransform','IMiniMapEntityBody gridTransform')
        text=text.replace('GridTransformComponentBodyPosInfo bodyRangeInfo in gridTransform.Info.BodyRangeInfos','MiniMapBodyOffset bodyRangeInfo in gridTransform.BodyRangeInfos')
        text=text.replace('IMapMyCharacterManagerProxy','IMiniMapCharacterManager').replace('IMyMapController','IMiniMapCharacterController')
        at=text.index('public ')
        text=text[:at]+'namespace Zjcs.Official.World.Map13MiniMap\n{\nusing Object = UnityEngine.Object;\n'+text[at:]+'\n}\n'
        emit(name+'.cs','// Complete original visible-range implementation; see Map13 minimap view source receipt.\n'+text)
    emit('MiniMapViewInputs.cs','''using System;
using System.Collections.Generic;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.World.NativeTerrain.Common;
namespace Zjcs.Official.World.Map13MiniMap
{
    // Consumed original grid/character surfaces. The formal map provides actual
    // original body offsets, the player entity and controller-change events.
    public sealed class MiniMapBodyOffset { public Pos2d Pos; }
    public interface IMiniMapEntityBody
    {
        Pos2d Pos { get; }
        IEnumerable<MiniMapBodyOffset> BodyRangeInfos { get; }
    }
    public interface IMiniMapCharacterController { MyGridTransform MyGridTransform { get; } }
    public interface IMiniMapCharacterManager
    {
        IMiniMapCharacterController MyMeUiCharacter { get; }
        event Action<IMiniMapCharacterController,IMiniMapCharacterController> OnMeMapControllerUpdated;
    }
}
''')
    emit('Zjcs.Official.World.Map13MiniMapView.asmdef',encode(dict(name='Zjcs.Official.World.Map13MiniMapView',references=['Zjcs.Official.World.NativeTerrain','Zjcs.Official.World.NativeMap13Labels','Zjcs.Official.EntityLifecycle'],autoReferenced=True)).decode())
    meta(DEST,folder=True)
    path=ROOT/'assets/reference/official-map/map13-minimap-view-code-source-manifest.json'
    write(path,encode(dict(format=1,manifest='871_156598',mapClassId=13,inputs=inputs,outputs=outputs,
        generator=dict(path=source_path(Path(__file__)),sha256=sha(Path(__file__).read_bytes())),
        operation='Copy the complete original IMapViewRangeHandler, MapViewRangeHandlerBase, MapAllViewRangeHandler and MapCharacterViewRangeHandler. Preserve rectangle overlap boundaries, original visible-cell order, half-range ceiling, position comparison, subscription and controller replacement/disposal order. Replace only namespaces and consumed body/controller interfaces; reuse the complete migrated original MyGridTransform and GAction implementations.',
        limitations=['Runtime core only; original MiniMap_13 prefab, formal player/controller events, camera transitions, resource visibility and Windows Player remain required.'])))
    print('Original minimap visible-range handlers:',len(outputs),'files')


if __name__=='__main__':build()
