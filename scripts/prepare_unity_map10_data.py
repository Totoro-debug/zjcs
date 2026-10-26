"""Build a provenance-checked Unity map data payload from the pinned APK."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import sys
import msgpack

from official_apk import Apk, ROOT
from official_ec_codec import Formats
from official_map_codec import entities, ground
from official_tables import Constructors


OUTPUT = ROOT / 'unity/Assets/Official/World/Map/10'
TABLES = {
    'map': ('config_luban_map', 'Assets/Config/Luban/Map/map.g.bytes', 'TbMapInfo'),
    'map_area': ('config_luban_map', 'Assets/Config/Luban/Map/map_area.g.bytes', 'TbMapAreaInfo'),
    'map_region': ('config_luban_map', 'Assets/Config/Luban/Map/map_region.g.bytes', 'TbMapRegionInfo'),
    'entity_extend_parameter': ('config_luban_map', 'Assets/Config/Luban/Map/entity_extend_parameter.g.bytes', 'TbEntityExtendParameterInfo'),
    'map_talk': ('config_luban_map', 'Assets/Config/Luban/Map/map_talk.g.bytes', 'TbMapTalkInfo'),
    'conditions': ('config_luban', 'Assets/Config/Luban/conditions.g.bytes', 'TbConditionInfo'),
    'map_stage': ('config_luban', 'Assets/Config/Luban/map_stage.g.bytes', 'TbMapToStageInfo'),
    'stage': ('config_luban', 'Assets/Config/Luban/stage.g.bytes', 'TbStageInfo'),
    'story': ('config_luban', 'Assets/Config/Luban/story.g.bytes', 'TbStoryInfo'),
    'ec_entity_file_index': ('config_luban_ec', 'Assets/Config/Luban/EC/ecentity_file_index.g.bytes', 'TbEcEntityFileIndexInfo'),
    'entity_grid_mover': ('config_luban_ec', 'Assets/Config/Luban/EC/entity_grid_mover.g.bytes', 'TbEntityGridMoverInfo'),
}
MAP_SOURCES = {
    'navigation': ('config_ec_maprootentitylite', 'Assets/Config/EC/MapRootEntityLite/10.g.bytes'),
    'placements': ('config_mapinfowrap', 'Assets/Config/MapInfoWrap/mapinfowrap_10.g.bytes'),
}
CHARACTER_SOURCE_BUNDLE = 'config_ec'


def text_asset_bytes(obj):
    return obj.read().m_Script.encode('utf-8', 'surrogateescape')


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def json_bytes(value):
    return (json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + '\n').encode('utf-8')


def source_record(apk, obj, path, payload):
    object_bytes = obj.get_raw_data()
    return dict(apk.provenance(obj), object=path, raw_size=len(object_bytes),
                raw_sha256=sha256(object_bytes), payload_size=len(payload), payload_sha256=sha256(payload))


def write_meta(path, importer):
    relative = path.relative_to(ROOT / 'unity').as_posix()
    guid = hashlib.md5(('zjcs-official:' + relative).encode('utf-8')).hexdigest()
    if importer == 'folderAsset':
        content = f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    else:
        content = f"fileFormatVersion: 2\nguid: {guid}\n{importer}:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    meta = Path(str(path) + '.meta')
    if not meta.exists() or meta.read_text(encoding='utf-8') != content:
        meta.write_text(content, encoding='utf-8', newline='\n')


def table_record(constructors, apk, bundle, path, table_type, predicate):
    obj = apk.asset(bundle, path)
    payload = text_asset_bytes(obj)
    records = constructors.table(payload, table_type)
    selected = [record for record in records if predicate(record)]
    source = source_record(apk, obj, path, payload)
    return selected, source


def build(apk=None, output=OUTPUT):
    apk = apk or Apk()
    constructors = Constructors()
    inputs = []

    decoded = {}
    for key, (bundle, path) in MAP_SOURCES.items():
        obj = apk.asset(bundle, path)
        raw = text_asset_bytes(obj)
        record = source_record(apk, obj, path, raw)
        inputs.append(record)
        decoded[key] = ground(raw) if key == 'navigation' else entities(raw)

    if decoded['navigation']['id'] != 10 or decoded['placements']['id'] != 10:
        raise ValueError('Pinned map records do not identify ClassId 10')
    if len(decoded['placements']['entities']) != 82:
        raise ValueError('Map10 placement count differs from pinned source')

    character_class_id = decoded['navigation']['character']
    file_index_obj = apk.asset(TABLES['ec_entity_file_index'][0], TABLES['ec_entity_file_index'][1])
    file_index_payload = text_asset_bytes(file_index_obj)
    file_index = constructors.table(file_index_payload, TABLES['ec_entity_file_index'][2])['EntityFileIndex']
    file_keys = [name for name, class_ids in file_index.items() if character_class_id in class_ids]
    if len(file_keys) != 1:
        raise ValueError('Map10 character does not resolve to one pinned EC file index entry')
    character_path = f'Assets/Config/EC/{file_keys[0]}.g.bytes'
    character_obj = apk.asset(CHARACTER_SOURCE_BUNDLE, character_path)
    character_payload = text_asset_bytes(character_obj)
    ec_file = msgpack.unpackb(character_payload, raw=False, strict_map_key=False)
    character_entities = [row for row in ec_file[0] if row[1] == character_class_id and row[0] == 0]
    if len(character_entities) != 1:
        raise ValueError('Map10 character source does not contain one common prefab record')
    mover_components = [payload for component_type, payload in character_entities[0][2]
                        if component_type == 'Common.GridMoverComponent']
    if len(mover_components) != 1 or mover_components[0] is None:
        raise ValueError('Map10 character common prefab has no unique GridMoverComponent')
    mover_info = Formats().component('Common.GridMoverComponent', mover_components[0])
    mover_source = source_record(apk, character_obj, character_path, character_payload)

    mover_table_obj = apk.asset(TABLES['entity_grid_mover'][0], TABLES['entity_grid_mover'][1])
    mover_table_payload = text_asset_bytes(mover_table_obj)
    mover_rows = constructors.table(mover_table_payload, TABLES['entity_grid_mover'][2])
    mover_keys = {mover_info['WalkableGroundTypeKey'], mover_info['WalkableGroundTypeServerOverrideKey']}
    mover_configs = {row['Id']: row for row in mover_rows if row['Id'] in mover_keys}
    if mover_keys != set(mover_configs):
        raise ValueError('Map10 character mover keys are absent from TbEntityGridMoverInfo')
    mover_table_source = source_record(apk, mover_table_obj, TABLES['entity_grid_mover'][1], mover_table_payload)
    file_index_source = source_record(apk, file_index_obj, TABLES['ec_entity_file_index'][1], file_index_payload)

    map_rows, map_source = table_record(
        constructors, apk, *TABLES['map'], lambda row: row['ClassId'] == 10)
    if len(map_rows) != 1:
        raise ValueError('Expected one TbMapInfo record for ClassId 10')
    map_info = map_rows[0]
    if (map_info['MapBgmId'], map_info['NextMapClassId'], map_info['MapGroundPath'], map_info['MiniMapPath']) != (
            10, 11, 'Map_10/MapGround_10', 'Map_10/MiniMap/MiniMap_10'):
        raise ValueError('Map10 bindings differ from pinned TbMapInfo')
    inputs.append(map_source)

    area_rows, area_source = table_record(
        constructors, apk, *TABLES['map_area'], lambda row: row['ClassId'] == 10)
    region_rows, region_source = table_record(
        constructors, apk, *TABLES['map_region'], lambda row: row['ClassId'] == 10)
    condition_ids = {row['AccessibleConditionId'] for row in region_rows if row['AccessibleConditionId'] > 0}
    condition_rows, condition_source = table_record(
        constructors, apk, *TABLES['conditions'], lambda row: row['ClassId'] in condition_ids)
    if {row['ClassId'] for row in condition_rows} != condition_ids:
        raise ValueError('Map10 regions reference missing official access conditions')
    expected_region_conditions = {
        1201: (0, 10000), 1202: (1, 10000), 1203: (2, 10000), 1204: (3, 10000),
    }
    for row in condition_rows:
        condition = row['Condition']
        if condition.get('_type') != 'WorldMapProgressCondition' \
                or condition.get('MapClassId') != 10 \
                or (condition.get('Region'), condition.get('Progress')) != expected_region_conditions[row['ClassId']]:
            raise ValueError('Map10 access condition differs from pinned WorldMapProgressCondition data')
    entity_ids = {placement['entity'] for placement in decoded['placements']['entities']}
    entity_rows, entity_source = table_record(
        constructors, apk, *TABLES['entity_extend_parameter'],
        lambda row: row['EntityClassId'] in entity_ids)
    map_talk_rows, map_talk_source = table_record(
        constructors, apk, *TABLES['map_talk'],
        lambda row: row['EntityId'] in {f'entity_{entity_id}_name' for entity_id in entity_ids})
    story_names = {row['TalkId'] for row in map_talk_rows if row['TalkId']}
    story_rows, story_source = table_record(
        constructors, apk, *TABLES['story'], lambda row: row['StoryName'] in story_names)
    if story_names != {row['StoryName'] for row in story_rows}:
        raise ValueError('Map10 map-talk story names are not all present in TbStoryInfo')

    map_stage_rows, map_stage_source = table_record(
        constructors, apk, *TABLES['map_stage'], lambda row: row['MapClassId'] == 10)
    stage_ids = {row['StageId'] for row in map_stage_rows}
    stage_rows, stage_source = table_record(
        constructors, apk, *TABLES['stage'], lambda row: row['Id'] in stage_ids)
    if stage_ids != {row['Id'] for row in stage_rows}:
        raise ValueError('Map10 map-stage IDs are not all present in TbStageInfo')
    inputs.extend((area_source, region_source, condition_source, entity_source, map_talk_source, story_source,
                   map_stage_source, stage_source, file_index_source, mover_source, mover_table_source))
    move_mask = 0
    for ground_type in mover_configs[mover_info['WalkableGroundTypeKey']]['GroundTypes'] or []:
        move_mask |= 1 << ground_type

    def record_id(row):
        for key in ('ClassId', 'EntityClassId', 'MapClassId', 'StageId', 'Id', 'StoryName', 'EntityId'):
            if key in row:
                return str(row[key])
        raise ValueError('Official world config has no stable record key')

    navigation = decoded['navigation']
    sorted_inputs = sorted(inputs, key=lambda item: (item['bundle'], item['path_id']))
    inputs_json = json.dumps(sorted_inputs, ensure_ascii=False, sort_keys=True, separators=(',', ':'))
    inputs_sha256 = sha256(inputs_json.encode('utf-8'))
    definition = {
        'ClassId': 10,
        'MapGroupId': decoded['placements']['group'],
        'MapTagId': decoded['placements']['tag'],
        'MapBgmId': map_info['MapBgmId'],
        'NextMapClassId': map_info['NextMapClassId'],
        'SourceManifest': 'Assets/Official/World/Map/10/source-manifest.json',
        'SourceManifestSha256': inputs_sha256,
        'OriginalGroundPrefab': 'Assets/Prefabs/Map/Map_10/MapGround_10.prefab',
        'OriginalMiniMapPrefab': 'Assets/Prefabs/Map/Map_10/MiniMap/MiniMap_10.prefab',
        'CharacterMover': {
            'CharacterClassId': character_class_id,
            'MoveActionId': mover_info['MoveActionId'],
            'WalkableGroundTypeKey': mover_info['WalkableGroundTypeKey'],
            'WalkableGroundTypeServerOverrideKey': mover_info['WalkableGroundTypeServerOverrideKey'],
            'WalkableGroundTypes': sorted(mover_configs[mover_info['WalkableGroundTypeKey']]['GroundTypes'] or []),
            'ServerOverrideGroundTypes': sorted(mover_configs[mover_info['WalkableGroundTypeServerOverrideKey']]['GroundTypes'] or []),
            'MoveMask': move_mask,
        },
        'MaxRegionProgress': [{'Region': region, 'Value': value}
                              for region, value in sorted(decoded['placements']['progress'].items())],
        'Navigation': {
            'Width': navigation['size'][0],
            'Height': navigation['size'][1],
            'Origin': {'X': navigation['origin'][0], 'Y': navigation['origin'][1], 'Z': navigation['origin'][2]},
            'CharacterId': navigation['character'],
            'MemberId': navigation['member'],
            'Rows': [{'Cells': [{'RawValues': cell} for cell in row]} for row in navigation['cells']],
            'AreaMappings': [{'Cell': cell, 'Value': value} for cell, value in navigation['areas']],
            'MaxSteps': navigation['max_steps'],
        },
        'Entities': [
            {
                'Id': row['id'], 'Region': row['region'], 'Refresh': row['refresh'],
                'Public': row['public'], 'Private': row['private'], 'Grid': row['grid'],
                'Direction': row['direction'], 'Unique': row['unique'], 'Area': row['area'],
                'EntityId': row['entity'], 'TypeId': row['type'],
                'Stages': [{'Key': str(key), 'ValueJson': json.dumps(value, ensure_ascii=False, separators=(',', ':'))}
                           for key, value in sorted(row['stages'].items(), key=lambda item: str(item[0]))],
            }
            for row in decoded['placements']['entities']
        ],
        'Configs': [
            {'TableName': name, 'RecordId': record_id(row),
             'Json': json.dumps(row, ensure_ascii=False, separators=(',', ':'), sort_keys=True),
             'Sha256': sha256(json_bytes(row)), 'SourceBundle': source['bundle'],
             'SourcePathId': source['path_id']}
            for name, records, source in (
                ('TbMapInfo', map_rows, map_source), ('TbMapAreaInfo', area_rows, area_source),
                ('TbMapRegionInfo', region_rows, region_source), ('TbConditionInfo', condition_rows, condition_source),
                ('TbEntityExtendParameterInfo', entity_rows, entity_source),
                ('TbMapTalkInfo', map_talk_rows, map_talk_source), ('TbStoryInfo', story_rows, story_source),
                ('TbMapToStageInfo', map_stage_rows, map_stage_source), ('TbStageInfo', stage_rows, stage_source),
                ('TbEntityGridMoverInfo', [mover_configs[key] for key in sorted(mover_configs)], mover_table_source))
            for row in records
        ],
    }
    output_bytes = json_bytes(definition)
    manifest = {
        'format': 1,
        'manifest': '854_156383',
        'mapClassId': 10,
        'sourceInputsJson': inputs_json,
        'sourceInputsSha256': inputs_sha256,
        'output': {'path': 'map10.json', 'size': len(output_bytes), 'sha256': sha256(output_bytes)},
        'inputs': sorted_inputs,
    }
    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    (output / 'map10.json').write_bytes(output_bytes)
    (output / 'source-manifest.json').write_bytes(json_bytes(manifest))
    for directory in (ROOT / 'unity/Assets/Official/World', ROOT / 'unity/Assets/Official/World/Map', output):
        write_meta(directory, 'folderAsset')
    for filename in ('map10.json', 'source-manifest.json'):
        write_meta(output / filename, 'TextScriptImporter')
    return manifest


if __name__ == '__main__':
    result = build()
    print(f"Map10 JSON SHA256: {result['output']['sha256']}")
