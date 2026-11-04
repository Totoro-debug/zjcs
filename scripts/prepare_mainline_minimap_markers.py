"""Bind placement/refresh-selected official minimap markers once for all mainline maps."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from official_manifest import read_asset_manifest
from official_map_codec import entities
from official_tables import Constructors
from mainline_minimap_native_sprite import restore_sprite
from prepare_mainline_map15_native_ground import GroundSource, guid, script_id, sha
from prepare_mainline_minimap_native_root import MiniMapRootBinding
from validation_workspace import validation_directory

MAPS = (10, 11, 12, 13, 14, 16)


class MiniMapMarkerBinding(MiniMapRootBinding):
    def __init__(self, work):
        super().__init__(work, 13)
        self.destination = ROOT / 'unity/Assets/Official/World/Shared/NativeMiniMapMarkersCurrent'
        self.receipt_path = ROOT / 'assets/reference/official-map/mainline-current-minimap-markers-source-manifest.json'

    def native_guid(self, name):
        return hashlib.md5(('zjcs:mainline-minimap-markers:' + name).encode()).hexdigest()

    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT / 'unity/Assets/Game/Runtime/Official/World'
        for folder, names in {
            'Map13MiniMapMarks': ('MiniMapOnlyMarkUI', 'MiniMapWayPointMarkUI', 'MiniMapEntryMarkUI'),
            'Map13MiniMapMarkSupports': ('MapEntityTipDotNodeLinker', 'MiniMapActiveByNPCVisibleSupporter',
                'MiniMapActiveSwitcherSupport', 'MiniMapPeriodicitySwitcherSupport', 'MiniMapWorldStageSwitcherSupport',
                'MinimapMarkKeepSizeObjSupport', 'MinimapMarkTipDotLinkerSupport'),
        }.items():
            for name in names:
                path = runtime / folder / (name + '.cs')
                ref = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
                self.scripts[name] = ref
                self.class_ids[script_id('', name)] = ref
                self.script_guids.add(ref['guid'])
                self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=ref,
                    sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + '.meta').read_bytes()))

    def load(self, source_guid):
        if source_guid in self.redirects:
            return self.redirects[source_guid]
        return restore_sprite(self, source_guid, super().load(source_guid))

    def selection(self):
        constructors = Constructors(self.cache.parent / 'official-code')
        sources = []

        def payload(bundle, suffix):
            obj = self.recovery.apk.asset(bundle, suffix)
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            sources.append(dict(source=self.recovery.apk.provenance(obj), payload_sha256=sha(raw)))
            return raw

        def table(name, constructor):
            return constructors.table(payload('config_luban_map', '/' + name + '.g.bytes'), constructor)

        extend = table('entity_extend_parameter', 'TbEntityExtendParameterInfo')
        displays = table('entity_map_ui_display', 'TbEntityMapUIDisplayInfo')
        groups = table('map_entity_group', 'TbMapEntityGroupInfo')
        maps = table('map', 'TbMapInfo')
        selection = []
        for map_id in MAPS:
            placement = entities(payload('config_mapinfowrap', f'/mapinfowrap_{map_id}.g.bytes'))
            info, = [row for row in maps if row['ClassId'] == map_id]
            if placement['id'] != map_id:
                raise ValueError('Original placement map identity differs')
            classes = {row['entity'] for row in placement['entities']}
            refresh = {row['refresh'] for row in placement['entities']} - {0}
            variants = [row for row in groups if row['Group'] in refresh]
            if refresh != {row['Group'] for row in variants}:
                raise ValueError('Missing official refresh group: ' + str(map_id))
            classes.update(entity for row in variants for entity in row['Entitys'])
            # Keep all original area/tag alternatives: the runtime selector owns
            # precedence, while this catalog supplies its complete dependencies.
            marks = {row['MiniMapMark'] for row in extend if row['EntityClassId'] in classes}
            marks.update(row['MiniMapMark'] for row in displays if row['EntityClassId'] in classes
                and row['GroupId'] in (0, info['MapGroupId']) and row['MapClassId'] in (0, map_id))
            selection.append(dict(mapClassId=map_id, groupId=info['MapGroupId'], entityClasses=sorted(classes),
                refreshGroups=sorted(refresh), refreshVariants=[row['Id'] for row in variants], markers=sorted(marks - {''})))
        for name in constructors.sources:
            for folder in ('common-source/Common', 'common-source/Common.LubanGen'):
                path = self.cache.parent / 'official-code' / folder / (name + '.cs')
                if path.is_file():
                    self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        return selection, sources

    def build(self):
        if self.recovery.apk.version != '884_156692':
            raise ValueError('Review marker selection against the selected official version')
        selection, inputs = self.selection()
        manifest = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        bindings, sources, scripts = [], [], set()
        for name in sorted({name for row in selection for name in row['markers']}):
            row, = [row for row in manifest if row['path'] == f'Assets/Prefabs/UI/Map/MiniMap/MiniMapMark/{name}.prefab']
            pointer, record = self.bind_prefab(GroundSource(13, row['path'], row['bundle']), Path('Markers') / (name + '.prefab'))
            sources.append(dict(name=name, source=record))
            scripts.update(s['type'] for s in self.recovery.outputs[-1][2]['scripts'] if not s['namespace'])
            bindings.append(dict(OfficialPath='UI/Map/MiniMap/MiniMapMark/' + name + '.prefab', Asset=pointer))

        equivalence = []
        receipts = list((ROOT / 'assets/reference/official-map').glob('map13-minimap*-code-source-manifest.json'))
        receipts.append(ROOT / 'assets/reference/official-map/map13-label-core-code-source-manifest.json')
        for name in sorted(scripts):
            path, = (self.cache.parent / 'official-code').rglob(name + '.cs')
            digest = sha(path.read_bytes())
            matches = [(p, row) for p in receipts for row in json.loads(p.read_bytes()).get('inputs', [])
                if row['path'].endswith('/' + name + '.cs') and row['sha256'] == digest]
            if not matches:
                raise ValueError('Review changed official marker code: ' + name)
            receipt, previous = matches[0]
            self.inputs[path.relative_to(ROOT).as_posix()] = digest
            self.inputs[receipt.relative_to(ROOT).as_posix()] = sha(receipt.read_bytes())
            equivalence.append(dict(selected=path.relative_to(ROOT).as_posix(), reused=previous))

        path = ROOT / 'unity/Assets/Game/Runtime/Offline/World/NativeMap13/NativeMap13AssetCatalog.cs'
        script = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
        self.script_guids.add(script['guid'])
        catalog_guid = self.native_guid('catalog')
        self.script_guids.add(catalog_guid)
        catalog = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
            m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1, m_EditorHideFlags=0,
            m_Script=script, m_Name='MainlineMiniMapMarkers', m_EditorClassIdentifier='', Bindings=bindings)
        self.queue.append((Path('Resources/MainlineMiniMap/current-marker-assets.asset'), catalog_guid,
            {11400000: dict(MonoBehaviour=catalog)}))
        for p in (path, Path(str(path) + '.meta'), Path(__file__), ROOT / 'scripts/mainline_minimap_native_sprite.py',
                ROOT / 'scripts/prepare_unity_map13_native_sprite_yaml.py', ROOT / 'scripts/official_map_codec.py',
                ROOT / 'scripts/official_tables.py', self.cache.parent / 'code-manifest.json'):
            self.inputs[p.relative_to(ROOT).as_posix()] = sha(p.read_bytes())
        self.write_outputs(sources, dict(fileID=11400000, guid=catalog_guid, type=2),
            maps=selection, selection_inputs=inputs, original_code_equivalence=equivalence,
            scope='Complete original marker prefabs for placed entities and all configured refresh variants on six mainline maps. Area/tag alternatives remain available to the original runtime selector. Formal map-host integration remains separate.')
        receipt = json.loads(self.receipt_path.read_bytes())
        receipt.pop('mapClassId')
        receipt['sprite_geometry'] = [dict(path=(self.destination / row['output']).relative_to(ROOT).as_posix(),
            **row['sprite']) for row in self.proof.values() if 'sprite' in row]
        self.receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        self.recovery.apk.archive.close()


if __name__ == '__main__':
    MiniMapMarkerBinding(validation_directory()).build()
