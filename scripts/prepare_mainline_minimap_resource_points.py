"""Bind each selected map's original resource boards, icons and configuration."""
import argparse
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from official_manifest import read_asset_manifest
from official_tables import Constructors
from prepare_current_partner_native import identity
from prepare_mainline_map15_native_ground import GroundSource, guid, script_id, sha
from prepare_mainline_minimap_native_root import MiniMapRootBinding
from validation_workspace import validation_directory
from mainline_minimap_native_sprite import restore_sprite


class ResourcePointBinding(MiniMapRootBinding):
    def __init__(self, work, map_id):
        super().__init__(work, map_id)
        self.destination = ROOT / f'unity/Assets/Official/World/Map/{map_id}/NativeMiniMapResourcesCurrent'
        self.receipt_path = ROOT / f'assets/reference/official-map/map{map_id}-current-minimap-resources-source-manifest.json'

    def native_guid(self, name):
        return hashlib.md5(f'zjcs:mainline-minimap-resources:Map{self.source.map_id}:{name}'.encode()).hexdigest()

    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT / 'unity/Assets/Game/Runtime/Official/World/Map13MiniMapResourcePoints'
        for name in ('MiniMapResourcePointBoard', 'MiniMapResourcePointTapItem'):
            path = runtime / (name + '.cs')
            ref = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
            self.scripts[name] = ref
            self.class_ids[script_id('', name)] = ref
            self.script_guids.add(ref['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=ref,
                sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + '.meta').read_bytes()))

    def text_asset(self, name, data):
        key = self.native_guid(name)
        self.script_guids.add(key)
        self.queue.append((Path(name), key, (json.dumps(data, ensure_ascii=False, indent=2) + '\n').encode()))
        return dict(fileID=4900000, guid=key, type=3)

    def load(self, source_guid):
        if source_guid in self.redirects:
            return self.redirects[source_guid]
        return restore_sprite(self, source_guid, super().load(source_guid))

    def configuration(self, wanted):
        code = self.cache.parent / 'official-code'
        constructors = Constructors(code)
        table_inputs = []

        def table(name, constructor):
            obj = self.recovery.apk.asset('config_luban_map', '/' + name + '.g.bytes')
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            table_inputs.append(dict(source=self.recovery.apk.provenance(obj), payload_sha256=sha(raw), constructor=constructor))
            return constructors.table(raw, constructor)

        all_groups = table('map_entity_group', 'TbMapEntityGroupInfo')
        groups = [row for row in all_groups if row['Id'] in wanted]
        if {row['Id'] for row in groups} != wanted:
            raise ValueError('Every original resource layer requires its official group configuration')
        data = dict(Groups=[dict(Id=row['Id'], Icon=row['Icon']) for row in groups],
            ItemLocations=table('item_map_location', 'TbItemMapLocationInfo'),
            WorldLocations=table('map_world_refresh_group_location', 'TbMapWorldRefreshGroupLocationInfo'),
            Regions=[row for row in table('map_region', 'TbMapRegionInfo') if row['ClassId'] == self.source.map_id])
        if not data['Regions']:
            raise ValueError('Original map region configuration is required even for an empty resource layer')
        for name in constructors.sources:
            candidates = [code / folder / (name + '.cs') for folder in ('common-source/Common', 'common-source/Common.LubanGen')]
            paths = [path for path in candidates if path.is_file()]
            for path in paths:
                self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        for path in (code.parent / 'code-manifest.json', ROOT / 'scripts/official_tables.py'):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        return data, table_inputs

    def write_outputs(self, *args, **kwargs):
        super().write_outputs(*args, **kwargs)
        receipt = json.loads(self.receipt_path.read_bytes())
        receipt['sprite_geometry'] = [dict(path=(self.destination / row['output']).relative_to(ROOT).as_posix(),
            **row['sprite']) for row in self.proof.values() if 'sprite' in row]
        self.receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')

    def build(self):
        if self.recovery.apk.version != '884_156692':
            raise ValueError('Review resource boards against the selected official version')
        self.recovery.recover(Path(self.source.prefab_address).stem, self.work / f'map{self.source.map_id}-resources-source',
            bundle=self.source.bundle, native_relative=self.source.prefab_address.removeprefix('Assets/'))
        self.recovery.finalize()
        card = self.recovery.outputs[-1][2]
        manager_id, manager = next((file_id, row['MonoBehaviour']) for file_id, row in card['documents'].items()
            if 'mResourceLayerPrefabGuidDict' in row.get('MonoBehaviour', {}))
        manager_key, = [key for key, file_id in card['objectMap'].items() if file_id == manager_id]
        original = self.original(manager_key)
        fields = self.recovery.fields(original)
        owner = original.read(check_read=False).m_GameObject.deref()
        manager_pointer, source_record = self.bind_prefab(self.source,
            Path(f'Resources/Map{self.source.map_id}/Native/resource-point-manager.prefab'),
            source_root=owner, native_root_id=manager['m_GameObject']['fileID'])

        layer = fields['mResourceLayerPrefabGuidDict']['mSerializeBuffer']
        wanted = {row['Key'] for row in layer}
        if len(wanted) != len(layer):
            raise ValueError('Duplicate original resource layer')
        data, table_inputs = self.configuration(wanted)
        manifest = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        boards, board_sources = [], []
        for entry in layer:
            original_guid = entry['Value']['mAssetGuid']
            row, = [row for row in manifest if row['guid'] == original_guid]
            pointer, record = self.bind_prefab(GroundSource(self.source.map_id, row['path'], row['bundle']),
                Path('Boards') / Path(row['path']).name)
            boards.append(dict(RefreshGroup=entry['Key'], OriginalGuid=original_guid, Prefab=pointer))
            board_sources.append(dict(refresh_group=entry['Key'], original_guid=original_guid, source=record))

        icons = []
        for icon_path in sorted({row['Icon'] for row in data['Groups']}):
            row, = [row for row in manifest if row['address'] == 'Assets/Image/' + icon_path]
            # The addressed PNG's main asset is a Texture2D; its exact Sprite
            # subasset is identified within the same official bundle.
            matches = [obj for obj in self.recovery.apk.objects(row['bundle'])
                if obj.type.name == 'Sprite' and obj.read(check_read=False).m_Name == Path(row['path']).stem]
            obj, = matches
            icons.append(dict(Path=icon_path, Sprite=self.sprite_reference(obj)))

        data_pointer = self.text_asset('resource-point-data.json', data)
        fields_pointer = self.text_asset('resource-point-manager-fields.json', fields)
        asset_script = ROOT / 'unity/Assets/Game/Runtime/Offline/World/NativeMap13MiniMapResources/MiniMapResourcePointAssets.cs'
        script_pointer = dict(fileID=11500000, guid=guid(Path(str(asset_script) + '.meta')), type=3)
        self.script_guids.add(script_pointer['guid'])
        self.inputs[asset_script.relative_to(ROOT).as_posix()] = sha(asset_script.read_bytes())
        catalog_guid = self.native_guid('catalog')
        self.script_guids.add(catalog_guid)
        catalog = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
            m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1, m_EditorHideFlags=0,
            m_Script=script_pointer, m_Name=f'Map{self.source.map_id}MiniMapResourcePoints', m_EditorClassIdentifier='',
            ManagerPrefab=manager_pointer, Boards=boards, Icons=icons, SourceData=data_pointer, ManagerFields=fields_pointer)
        self.queue.append((Path(f'Resources/Map{self.source.map_id}/Native/current-resource-point-assets.asset'),
            catalog_guid, {11400000: dict(MonoBehaviour=catalog)}))

        code_receipt_path = ROOT / 'assets/reference/official-map/map13-minimap-resource-points-code-source-manifest.json'
        code_receipt = json.loads(code_receipt_path.read_bytes())
        equivalence = []
        for row in code_receipt['inputs']:
            if not row['path'].endswith('.cs') or '/client-source/' not in row['path']:
                continue
            path = self.cache.parent / 'official-code/client-source' / Path(row['path']).name
            if sha(path.read_bytes()) != row['sha256']:
                raise ValueError('Migrate changed official resource-point implementation: ' + path.name)
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
            equivalence.append(dict(selected=path.relative_to(ROOT).as_posix(), reused=row))
        self.inputs[code_receipt_path.relative_to(ROOT).as_posix()] = sha(code_receipt_path.read_bytes())
        self.inputs[Path(__file__).relative_to(ROOT).as_posix()] = sha(Path(__file__).read_bytes())
        for name in ('prepare_unity_map13_native_sprite_yaml.py', 'mainline_minimap_native_sprite.py'):
            sprite_tool = ROOT / 'scripts' / name
            self.inputs[sprite_tool.relative_to(ROOT).as_posix()] = sha(sprite_tool.read_bytes())
        self.write_outputs(source_record, dict(fileID=11400000, guid=catalog_guid, type=2),
            manager_fields=fields, board_sources=board_sources, table_inputs=table_inputs,
            original_code_equivalence=equivalence,
            counts=dict(boards=len(boards), icons=len(icons), regions=len(data['Regions'])),
            scope='Original map-specific resource manager subtree, complete board prefabs, native icon geometry and current configuration. Empty original layers stay empty. Formal HUD/Player integration remains separate.')
        self.recovery.apk.archive.close()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--map-id', type=int, choices=(10, 11, 12, 13, 14, 16), required=True)
    args = parser.parse_args()
    ResourcePointBinding(validation_directory(), args.map_id).build()
