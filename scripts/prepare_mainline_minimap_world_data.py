"""Bind installed world geometry to the selected original minimap configuration."""
import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_map_codec import ground, unpack, entities
from official_tables import Constructors

MAPS = (10, 11, 12, 13, 14, 16)
CODE = ROOT / '.tools/source-packages/current/884_156692/official-code'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes()))


def build():
    apk = Apk.current()
    try:
        if apk.version != '884_156692':
            raise ValueError('Review the selected world/minimap source version')
        code_manifest = CODE.parent / 'code-manifest.json'
        if sha((CODE / 'global-metadata.dat').read_bytes()) != json.loads(code_manifest.read_bytes())['metadata_sha256']:
            raise ValueError('Selected constructor metadata differs from its extraction receipt')
        constructors = Constructors(CODE)
        shared_receipt = ROOT / 'assets/reference/official-map/map14-shared-ec-runtime-source-manifest.json'
        shared_source = json.loads(shared_receipt.read_bytes())
        shared_payload = ROOT / 'unity/Assets/Official/World/Map/14/SharedRuntime/Console.bytes'
        original_console = next(row for row in shared_source['assemblies'] if row['path'].endswith('/Console.dll'))
        if shared_source['manifest'] != '871_156598' or sha(shared_payload.read_bytes()) != original_console['sha256']:
            raise ValueError('Preserve the separately recorded historical original entity IL')
        inputs = []

        def payload(bundle, suffix):
            obj = apk.asset(bundle, suffix)
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            inputs.append(dict(source=apk.provenance(obj), payload_sha256=sha(raw)))
            return raw

        def table(name, kind):
            return constructors.table(payload('config_luban_map', '/' + name + '.g.bytes'), kind)

        maps = table('map', 'TbMapInfo')
        regions = table('map_region', 'TbMapRegionInfo')
        areas = table('map_area', 'TbMapAreaInfo')
        tags = table('map_logic_entity_tag', 'TbMapLogicEntityTagInfo')
        # Find the actual configured object rather than guessing the CSV address.
        addresses = [address for address, _ in apk.container('config_luban_map')
            if address.rsplit('/', 1)[-1].replace('_', '').lower() == 'minimapregiondisplay.g.bytes']
        if len(addresses) != 1:
            raise ValueError('Original minimap region-display table identity is ambiguous: ' + repr(addresses))
        displays = constructors.table(payload('config_luban_map', addresses[0]), 'TbMinimapRegionDisplayInfo')
        bindings = []
        for mid in MAPS:
            definition_path = ROOT / f'unity/Assets/Official/World/Map/{mid}/map{mid}.json'
            definition = json.loads(definition_path.read_bytes())
            nav = definition['Navigation']
            current = ground(payload('config_ec_maprootentitylite', f'/{mid}.g.bytes'))
            expected = dict(id=mid, character=nav['CharacterId'], member=nav['MemberId'],
                size=[nav['Width'], nav['Height']], origin=[nav['Origin'][k] for k in 'XYZ'],
                cells=[[c['RawValues'] for c in row['Cells']] for row in nav['Rows']],
                areas=[[row['Cell'], row['Value']] for row in nav['AreaMappings']], max_steps=nav['MaxSteps'])
            if current != expected:
                raise ValueError('Installed world navigation differs from the selected actual minimap ground: ' + str(mid))
            installed = next(json.loads(row['Json']) for row in definition['Configs'] if row['TableName'] == 'TbMapInfo')
            selected = next(row for row in maps if row['ClassId'] == mid)
            for key in ('MinimapConfigId', 'MapGroupId', 'MiniMapPath'):
                if installed[key] != selected[key]:
                    raise ValueError('World/minimap identity differs: ' + str(mid) + '/' + key)
            installed_regions = [json.loads(row['Json']) for row in definition['Configs'] if row['TableName'] == 'TbMapRegionInfo']
            selected_regions = [row for row in regions if row['ClassId'] == mid]
            geometry = lambda rows: {row['Region']: (row['Areas'], row['RegionTag']) for row in rows}
            if geometry(installed_regions) != geometry(selected_regions):
                raise ValueError('World/minimap area-to-region geometry differs: ' + str(mid))
            current_placements = entities(payload('config_mapinfowrap', f'/mapinfowrap_{mid}.g.bytes'))
            placement_fields = lambda rows: {row['id']: (row['entity'], row['grid'], row['area'], row['region']) for row in rows}
            installed_placements = [dict(id=row['Id'], entity=row['EntityId'], grid=row['Grid'],
                area=row['Area'], region=row['Region']) for row in definition['Entities']]
            if current_placements['id'] != mid or placement_fields(current_placements['entities']) != placement_fields(installed_placements):
                raise ValueError('Installed marker placements differ from the selected original world: ' + str(mid))
            bindings.append(dict(mapClassId=mid, definition=record(definition_path),
                cells=nav['Width'] * nav['Height'], regions=len(installed_regions),
                checked_navigation_fields=list(current), checked_map_fields=['MinimapConfigId', 'MapGroupId', 'MiniMapPath'],
                checked_region_fields=['Region', 'Areas', 'RegionTag'],
                checked_placements=len(installed_placements), checked_placement_fields=['Id', 'EntityId', 'Grid', 'Area', 'Region']))
        selected_displays = [row for row in displays if row['MapClassId'] in MAPS]
        transport_pack = unpack(payload('config_map', '/MapTransportEntityInfoPack.g.bytes'))
        if not isinstance(transport_pack, list) or len(transport_pack) != 1 or not isinstance(transport_pack[0], dict):
            raise ValueError('Review the original transport MessagePack layout')
        transports = []
        transport_destinations = []
        for mid, entries in transport_pack[0].items():
            if mid not in MAPS:
                continue
            definition = json.loads((ROOT / f'unity/Assets/Official/World/Map/{mid}/map{mid}.json').read_bytes())
            placements = {row['Id']: row for row in definition['Entities']}
            for placement_id, row in entries.items():
                if len(row) != 6 or row[:2] != [placement_id, mid] or placement_id not in placements:
                    raise ValueError('Original transport placement identity differs')
                target_path = ROOT / f'unity/Assets/Official/World/Map/{row[4]}/map{row[4]}.json'
                transport_destinations.append(target_path)
                target = next(value for value in json.loads(target_path.read_bytes())['Entities'] if value['Id'] == row[2])
                if row[3] != target['Grid'] or row[5] != target['Region']:
                    raise ValueError('Original transport target differs from the installed destination')
                transports.append(dict(MapClassId=mid, LoEntityId=placement_id, TargetLoEntityId=row[2],
                    TargetPos=dict(X=row[3][0], Y=row[3][1]), TargetMapClassId=row[4], TargetRegion=row[5]))
        transport_destination = ROOT / 'unity/Assets/Game/Resources/MainlineMiniMap/current-entity-data.json'
        transport_destination.write_text(json.dumps(dict(Manifest=apk.version, Transports=transports,
            Areas=[dict(MapClassId=row['ClassId'], Area=row['Area'], AreaTag=row['AreaTag']) for row in areas if row['ClassId'] in MAPS],
            EntityTags=[row for row in tags if row['MapClass'] in MAPS]),
            ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        transport_meta = Path(str(transport_destination) + '.meta')
        if not transport_meta.is_file():
            transport_meta.write_text('fileFormatVersion: 2\nguid: '
                + hashlib.md5(b'zjcs:mainline-minimap-entity-data:884').hexdigest() + '\n', 'utf8', newline='\n')
        destination = ROOT / 'unity/Assets/Game/Resources/MainlineMiniMap/current-region-displays.json'
        destination.write_text(json.dumps(dict(Manifest=apk.version, Displays=selected_displays),
            ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        meta = Path(str(destination) + '.meta')
        if not meta.is_file():
            meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:mainline-minimap-region-displays:884').hexdigest() + '\n', 'utf8', newline='\n')
        sources = [Path(__file__), code_manifest, CODE / 'global-metadata.dat',
            ROOT / 'scripts/official_map_codec.py', ROOT / 'scripts/official_tables.py',
            CODE / 'common-source/Common/MinimapRegionDisplayInfoParser.cs',
            CODE / 'common-source/Common/MapRegionInfoParser.cs', CODE / 'common-source/Common/GridMoverComponentInfo.cs',
            CODE / 'common-source/Common/MapRootEntityLiteSingleInfo.cs',
            CODE / 'common-source/Common/MapRegionEntityInfoManager.cs',
            CODE / 'common-source/Common/EcContainer.cs',
            *[CODE / 'common-source/Common' / (name + '.cs') for name in (
                'WorldStageComponent', 'WorldStageActiveComponent', 'GridTransformComponent',
                'GridTransformComponentInfo', 'WorldActiveableEntityComponent', 'WayPointComponent', 'WayPointComponentImplement',
                'StageResultTransportComponent', 'EntitySourceComponent', 'EntityOwnershipData')],
            # This existing unchanged IL payload has its own historical source
            # receipt. Do not relabel it as the selected current configuration.
            ROOT / 'assets/reference/official-map/map14-shared-ec-runtime-source-manifest.json',
            ROOT / 'assets/reference/official-map/map14-shared-view-source-manifest.json',
            ROOT / 'unity/Assets/Official/World/Map/14/SharedRuntime/Console.bytes',
            ROOT / 'unity/Assets/Official/World/Map/14/SharedRuntime/hero-shared-prefabs.json',
            ROOT / 'unity/Assets/Game/Resources/Map14/Native/shared-assembly-catalog.asset',
            CODE / 'common-source/Common/MapLogicEntityTagInfoParser.cs', CODE / 'common-source/Common/MapEntityExtra.cs',
            CODE / 'common-source/Common/MapTransportEntityInfoPackParser.cs',
            CODE / 'common-source/Common/MapTransportEntityTargetInfo.cs',
            CODE / 'common-source/MessagePack.Formatters.Common/MapTransportEntityTargetInfoFormatter.cs']
        sources.extend(transport_destinations)
        for name in constructors.sources:
            sources.extend(path for folder in ('Common', 'Common.LubanGen')
                if (path := CODE / 'common-source' / folder / (name + '.cs')).is_file())
        receipt = dict(format=1, manifest=apk.version, inputs=inputs, sources=[record(p) for p in dict.fromkeys(sources)],
            shared_entity_payload=dict(manifest=shared_source['manifest'], payload=record(shared_payload),
                source_receipt=record(shared_receipt),
                operation='Borrow existing original entity/view objects; read native grid conversion/body, stage state/activity and component fields. Forward the original stage event and release only the owned receiver. The historical payload is not relabeled as current IL; this adapter supplies no spawn or server state rules.'),
            bindings=bindings, transport_targets=len(transports),
            outputs=[record(destination), record(meta), record(transport_destination), record(transport_meta)],
            integration=[record(ROOT / path) for path in (
                'unity/Assets/Game/Runtime/Offline/World/MainlineMiniMapEntities.meta',
                'unity/Assets/Game/Runtime/Offline/World/MainlineMiniMapEntities/MainlineMiniMapSharedEntity.cs',
                'unity/Assets/Game/Runtime/Offline/World/MainlineMiniMapEntities/MainlineMiniMapSharedEntity.cs.meta',
                'unity/Assets/Game/Runtime/Offline/World/MainlineMiniMapEntities/Zjcs.Offline.World.MainlineMiniMapEntities.asmdef',
                'unity/Assets/Game/Runtime/Offline/World/MainlineMiniMapEntities/Zjcs.Offline.World.MainlineMiniMapEntities.asmdef.meta',
                'unity/Assets/Game/Editor/Map13MiniMap/OfficialMainlineMiniMapRootAcceptance.SharedEntities.cs',
                'unity/Assets/Game/Editor/Map13MiniMap/OfficialMainlineMiniMapRootAcceptance.SharedEntities.cs.meta',
                'unity/Assets/Game/Editor/Map13MiniMap/Zjcs.Editor.Map13.MiniMap.asmdef',
                'unity/Assets/Game/Runtime/Offline/World/MainlineMiniMap/MainlineMiniMapEntityIndex.cs',
                'unity/Assets/Game/Runtime/Offline/World/MainlineMiniMap/MainlineMiniMapEntityIndex.cs.meta',
                'unity/Assets/Game/Runtime/Offline/World/MainlineMiniMap/MainlineMiniMapWorldData.cs',
                'unity/Assets/Game/Runtime/Offline/World/MainlineMiniMap/MainlineMiniMapWorldData.cs.meta',
                'unity/Assets/Game/Runtime/Offline/World/MainlineMiniMap/MainlineMiniMapWorldQueries.cs',
                'unity/Assets/Game/Runtime/Offline/World/MainlineMiniMap/MainlineMiniMapWorldQueries.cs.meta',
                'unity/Assets/Game/Runtime/Offline/World/MainlineMiniMap/MainlineMiniMap.cs',
                'unity/Assets/Game/Runtime/Offline/World/MainlineMiniMap/MainlineMiniMapBindings.cs',
                'unity/Assets/Game/Runtime/Offline/World/MainlineMiniMap/MainlineMiniMapLabels.cs',
                'unity/Assets/Game/Runtime/Offline/World/MainlineMiniMap/MainlineNativeMiniMapRoot.cs',
                'unity/Assets/Game/Runtime/Offline/World/MainlineMiniMap/Zjcs.Offline.World.MainlineMiniMap.asmdef',
                'unity/Assets/Game/Editor/Map13MiniMap/OfficialMainlineMiniMapRootAcceptance.EntityIndex.cs',
                'unity/Assets/Game/Editor/Map13MiniMap/OfficialMainlineMiniMapRootAcceptance.EntityIndex.cs.meta',
                'unity/Assets/Game/Editor/Map13MiniMap/OfficialMainlineMiniMapRootAcceptance.WorldData.cs',
                'unity/Assets/Game/Editor/Map13MiniMap/OfficialMainlineMiniMapRootAcceptance.WorldData.cs.meta',
                'unity/Assets/Game/Editor/Map13MiniMap/OfficialMainlineMiniMapRootAcceptance.cs',
                'unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMiniMapModule.ResourcePoints.cs',
                'scripts/prepare_mainline_minimap_offline_validation.py',
                'scripts/test_mainline_minimap_native_bindings.py')],
            operation='Compare every installed navigation field/cell, map/minimap identity, region area membership, and every marker placement ID/class/grid/area/region against the selected actual ground and map-info wrap. Preserve historical definition versions; no whole-version equivalence. Extract original region-display rows, area tags, logic tags and transport MessagePack records. Verify each transport target against its installed destination. Bind original root and marker-factory static queries to this one instance-owned projection; live state and commands remain with the owning map adapter.')
        target = ROOT / 'assets/reference/official-map/mainline-minimap-world-data-source-manifest.json'
        target.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Bound original minimap world data:', len(bindings), 'maps,', sum(r['cells'] for r in bindings),
            'cells,', len(selected_displays), 'display rows', flush=True)
    finally:
        apk.archive.close()


if __name__ == '__main__':
    build()
