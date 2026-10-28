"""Restore original Map13 grid-visibility code and region activation grids."""
from pathlib import Path
import json
import re

from official_apk import Apk, ROOT
from official_ec_codec import Formats, Reader
from prepare_unity_map13_sources import encode, sha, write
from prepare_unity_map13_prop_parts import meta
from prepare_unity_story_attachment_runtime import CODE, method, record
from prepare_unity_story_playable_runtime import logging_adapter

NS = 'Zjcs.Official.World.Map13GridVisibility'
DEST = ROOT / 'unity/Assets/Game/Runtime/Official/World/Map13GridVisibility'
DATA = ROOT / 'unity/Assets/Official/World/Map/13/Resources/Map13/grid-visibility.json'
PROOF = ROOT / 'assets/reference/official-map/map13-grid-visibility-source-manifest.json'


def build():
    manifest = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    assembly = CODE / 'managed/decoded/Console.dll'
    assert sha(assembly.read_bytes()) == manifest['assemblies'][assembly.name]['decoded_sha256']
    inputs, outputs, adaptations = [], [], []

    def source(name):
        path = CODE / 'common-source/Common' / (name + '.cs')
        inputs.append(path)
        return path.read_text(encoding='utf-8-sig')

    def emit(name, text):
        text = re.sub(r'(?m)^using (?:MessagePack|Newtonsoft.Json|AStar|Framework[\w.]*|GFrameCore[\w.]*|Microsoft[\w.]*|ZLogger);\n', '', text)
        text = text.replace('namespace Common;', '')
        text = re.sub(r'(?m)^\s*\[(?:Require|MessagePackObject|Key|JsonIgnore|IgnoreMember)(?:\([^\n]*\))?\]\n', '\n', text)
        text = text.replace('MySerializable', 'VisibilitySerializable').replace('EcComponentInfo', 'VisibilityComponentInfo')
        text = text.replace('EcComponent<GridVisibilityAgentComponentInfo>', 'VisibilityComponentSource<GridVisibilityAgentComponentInfo>')
        text = re.sub(r'\bEcEntity\b', 'VisibilityEntity', text)
        header, _, body = text.partition('public ')
        attributes = ''.join(re.findall(r'(?m)^\[[^\n]+\]\n', header))
        header = re.sub(r'(?m)^\[[^\n]+\]\n', '', header)
        text = ('// Migrated original shared visibility source; see Map13 source receipt.\n'
                'using Zjcs.Official.World.Map13EntityEvents;\n'
                'using Zjcs.Official.World.NativeTerrain.Common;\n'
                + header + '\nnamespace ' + NS + '\n{\n' + attributes + 'public ' + body + '\n}\n')
        path = DEST / (name + '.cs')
        write(path, ('\n'.join(line.rstrip() for line in text.splitlines()) + '\n').encode())
        meta(path, 'MonoImporter'); outputs.extend([path, Path(str(path) + '.meta')])

    emit('VisibilityComponentInfo', source('EcComponentInfo'))
    for name in ['MapGroundGridBoolData', 'MapActivedRegionGridInfo', 'GridVisibilityAgentComponentInfo']:
        emit(name, source(name))
    text = source('MapActivedRegionGridInfoEnumerator')
    text = text.replace('public struct MapActivedRegionGridInfoEnumerator(MapActivedRegionGridInfo info)', 'public struct MapActivedRegionGridInfoEnumerator')
    text = text.replace('private MapActivedRegionGridInfo mInfo = info;', 'private MapActivedRegionGridInfo mInfo;\n\tpublic MapActivedRegionGridInfoEnumerator(MapActivedRegionGridInfo info) { mInfo = info; }')
    emit('MapActivedRegionGridInfoEnumerator', text)

    text, changes = logging_adapter(source('GridVisibilityCompressUtils'))
    adaptations.append(dict(source='GridVisibilityCompressUtils', logger_blocks=changes))
    emit('GridVisibilityCompressUtils', text)

    text = source('GridVisibilityAgentComponent')
    # Replace only expanded logger statements, retaining each original catch,
    # recovery, assignment and event dispatch around them.
    def zlogger(match):
        block = match[0]
        exception = re.search(r'Exception ex2 = (\w+);', block)
        adaptations.append(dict(source='GridVisibilityAgentComponent', diagnostic_sha256=sha(block.encode())))
        return match['indent'] + ('UnityEngine.Debug.LogException(' + exception[1] + ');' if exception else '// Original diagnostic logger adapted; no state mutation.')
    text = re.sub(r'(?m)^(?P<indent>[ \t]*)ILogger logger = Logger;\n[\s\S]*?^(?P=indent)GZLoggerExtensions\.\w+\([^\n]+;', zlogger, text)
    text = re.sub(r'(?m)^(?P<indent>[ \t]*)GLoggerDebugInterpolatedStringHandler message = new GLoggerDebugInterpolatedStringHandler[^\n]+;\n[\s\S]*?^(?P=indent)Log\.Debug\([^\n]+;', lambda m:m['indent']+'// Original diagnostic logger adapted; no state mutation.', text)
    text = text.replace(', IMessagePackSerializationCallbackReceiver', ', IVisibilitySerializationCallbacks')
    text = text.replace('MeManager.MainPlayer?.Id', 'Host.MainPlayerId')
    text = text.replace('MeManager.Instance.MapData.MapCharacter', 'Host.MainMapCharacter')
    text = text.replace('EntityUtils.CalcAllVisiblePos(', 'Host.CalcAllVisiblePos(')
    text = text.replace('KeyValuePair<EcEntityIdentifier, IMapCharacter>', 'KeyValuePair<object, IMapCharacter>')
    assert not any(word in text for word in ['GLogger', 'GZLogger', 'ZLoggerError', 'MeManager', 'ILogger logger'])
    emit('GridVisibilityAgentComponent', text)

    text, changes = logging_adapter(source('GridVisibilityAgentHandle'))
    text = text.replace('EntityUtils.TryGetCampOwnerEntity(', 'mGridVisibilityAgent.Host.TryGetCampOwnerEntity(')
    text = text.replace('EntityUtils.GetLoEntityIncludePosList(', 'mGridVisibilityAgent.Host.GetLoEntityIncludePosList(')
    text = text.replace('EntityUtils.GetMapSpawnCharacterEntityComponent<IMapCharacter>(entity.Container.ClassId).ViewRange', 'mGridVisibilityAgent.Host.GetSpawnCharacterViewRange(entity.Container.ClassId)')
    adaptations.append(dict(source='GridVisibilityAgentHandle', logger_blocks=changes))
    emit('GridVisibilityAgentHandle', text)
    for name in ['IEcContainerStartEntityEventReceiver', 'EcContainerStartEntityEventDelegate',
                 'IGridTransformPosUpdatedEventReceiver', 'GridTransformPosUpdatedEventDelegate']:
        emit(name, source(name))

    text = source('MyMath')
    subset = text[:text.index('\n\tpublic static bool LoopOut(')]
    subset += '\n' + method(text, 'public static List<Pos2d> GetLoopOutList(') + '\n}\n'
    subset, changes = logging_adapter(subset)
    subset = subset.replace('public static class MyMath', 'public static class VisibilityRangeMath').replace('static MyMath()', 'static VisibilityRangeMath()')
    adaptations.append(dict(source='MyMath', selected='Original range cache constructor, GetInDistancePosList, GetLoopOutPosList, GetLoopOutList', logger_blocks=changes))
    emit('VisibilityRangeMath', subset)
    text = source('EntityHandler')
    selected = '\n'.join(method(text, signature) for signature in ['public List<Pos2d> CalcAllVisiblePos(', 'public IEnumerable<Pos2d> GetAllVisiblePosEnumerator('])
    selected = selected.replace('public List', 'public static List').replace('public IEnumerable', 'public static IEnumerable').replace('MyMath.', 'VisibilityRangeMath.')
    emit('VisibilityRangeQuery', 'using System.Collections.Generic;\nusing System.Linq;\npublic static class VisibilityRangeQuery\n{\n' + selected + '\n}\n')

    for name in ['GridTransformComponentInfo', 'GridTransformComponentBodyPosInfo', 'GridTransformComponentBodyCrossBlockedInfo']:
        emit(name, source(name))
    transform = source('GridTransformComponent')
    selected = '\n'.join(method(transform, signature) for signature in [
        'public static Pos2d CalcLocalToWorldPos(', 'public static Pos2d CalcLocalToWorldDir('])
    selected += '\n' + method(source('MyMath'), 'public static Vector2 Rotate(')
    selected = selected.replace('MyMath.Rotate(', 'Rotate(')
    selected, changes = logging_adapter(selected)
    adaptations.append(dict(source='GridTransformComponent/MyMath',selected='Original body coordinate and rotation queries',logger_blocks=changes))
    emit('VisibilityBodyGeometry', 'using System;\nusing UnityEngine;\npublic static class VisibilityBodyGeometry\n{\n'+selected+'\n}\n')

    boundaries = ROOT / 'scripts/fixtures/map13_grid_visibility_boundaries.cs'
    inputs.append(boundaries); emit('VisibilitySourceBoundaries', boundaries.read_text(encoding='utf-8-sig'))
    asm = DEST / (NS + '.asmdef')
    write(asm, encode(dict(name=NS,rootNamespace=NS,references=['Zjcs.Official.World.Map13EntityEvents','Zjcs.Official.World.NativeTerrain'],autoReferenced=True)))
    meta(asm, 'AssemblyDefinitionImporter'); meta(DEST, folder=True)
    outputs.extend([asm,Path(str(asm)+'.meta'),Path(str(DEST)+'.meta')])

    apk = Apk.current()
    obj = apk.asset('config_ec_mapregionactiveunlockposinfo', '/13.g.bytes')
    raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
    formats = Formats(CODE); reader = Reader(raw)
    decoded = formats.read(reader, 'MapRegionActiveUnlockPosSingleInfo')
    assert reader.pos == len(raw) and decoded['MapClassId'] == 13 and len(decoded['UnlockPosDict']) == 106
    rows = [dict(region=int(identity),**value) for identity,value in decoded['UnlockPosDict'].items()]
    navigation = ROOT / 'unity/Assets/Official/World/Map/13/Source/navigation.json'
    templates = navigation.with_name('templates.json')
    inputs.extend([navigation, templates])
    nav = json.loads(navigation.read_bytes())
    character = json.loads(templates.read_bytes())[str(nav['character'])]['WorldMapCharacterComponent']
    write(DATA, encode(dict(manifest=apk.version,mapClassId=13,size=nav['size'],origin=nav['origin'],viewRange=character['ViewRange'],regions=rows)))
    meta(DATA, 'TextScriptImporter'); outputs.extend([DATA,Path(str(DATA)+'.meta')])
    entity_source = navigation.with_name('entities.json'); inputs.append(entity_source)
    original_entities = json.loads(entity_source.read_bytes())
    original_templates = json.loads(templates.read_bytes())
    body_templates = []
    for identity, value in original_templates.items():
        if 'GridTransformComponent' not in value: continue
        info = dict(value['GridTransformComponent'])
        info['RealCenter'] = dict(x=info['RealCenter'][0], y=info['RealCenter'][1])
        body_templates.append(dict(classId=int(identity),body=info))
    body_entities = []
    for identity, value in original_entities.items():
        transform = value.get('LoGridTransformComponent')
        if transform is None: continue
        body_entities.append(dict(placementId=int(identity),classId=value['LoEntityComponent']['SpanwInfo']['ClassId'],
            transform=transform,campMembers=value.get('LoEntityCampComponent',{}).get('CampMonsterEntities'),
            campOwner=value.get('LoEntityCampMonsterComponent',{}).get('CampLoEntityId',0)))
    body_data = DATA.with_name('grid-entities.json')
    write(body_data, encode(dict(manifest=apk.version,mapClassId=13,templates=body_templates,entities=body_entities)))
    meta(body_data,'TextScriptImporter'); outputs.extend([body_data,Path(str(body_data)+'.meta')])
    inputs.extend(formats.sources.values())
    write(PROOF, encode(dict(manifest=apk.version,mapClassId=13,
        method='Copy complete original shared grid visibility component/handle, bit-array data and codec, event dispatch specializations, original region activation enumerator, GridTransform body information/cache classes and original range/rotation queries. Restore all source placement transforms and camp links. Adapt namespace, serialization attributes, diagnostic loggers and explicit consumed entity/map interfaces; no server reward or display/UI replacement.',
        coverage='Original rule/component migration and explicit consumption contracts, with all 148 original grid body templates and 2690 placement transforms. Runtime integration and verification scope are recorded separately; this source receipt alone does not establish formal scene or Player completion.',
        official_config=dict(apk.provenance(obj),payload_sha256=sha(raw),object_sha256=sha(obj.get_raw_data()),payload_bytes=len(raw)),
        regions=len(rows),body_templates=len(body_templates),placements=len(body_entities),activated_grid_entries=sum(len(r['RegionActivedGrids'] or []) for r in rows),extended_grid_entries=sum(len(r['RegionExtendVisiableGrids'] or []) for r in rows),
        inputs=[record(p) for p in sorted(set(inputs))],outputs=[record(p) for p in sorted(set(outputs))],
        assemblies=[record(assembly)],adaptations=adaptations,tools=[record(Path(__file__).resolve())])))
    print('Restored original visibility code and',len(rows),'region activation grid lists',flush=True)


if __name__ == '__main__':build()
