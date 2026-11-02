"""Bind original regional minimap geometry through the shared native prefab converter."""
import argparse
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from official_manifest import read_asset_manifest
from prepare_current_partner_native import identity, pointer
from prepare_mainline_map15_native_ground import GroundBinding, GroundSource, guid, script_id, sha
from validation_workspace import validation_directory


class MiniMapRegionsBinding(GroundBinding):
    def __init__(self, work, map_id):
        address = f'Assets/Prefabs/Map/Map_{map_id}/MiniMap/MiniMap_{map_id}.prefab'
        super().__init__(work, GroundSource(map_id, address,
            f'defaultpackage_assets_prefabs_map_map_{map_id}_minimap.bundle'))
        self.destination = ROOT / f'unity/Assets/Official/World/Map/{map_id}/NativeMiniMapCurrent'
        self.receipt_path = ROOT / f'assets/reference/official-map/map{map_id}-current-minimap-regions-source-manifest.json'
        self.tile_fields = []

    def native_guid(self, name):
        return hashlib.md5(f'zjcs:mainline-minimap-regions:Map{self.source.map_id}:{name}'.encode()).hexdigest()

    def load(self, source_guid):
        before = len(self.queue)
        result = super().load(source_guid)
        obj = self.objects.get(source_guid)
        if len(self.queue) == before or obj.type.name != 'MonoBehaviour':
            return result
        script = obj.read(check_read=False).m_Script.read()
        if (script.m_Namespace, script.m_ClassName) != ('UnityEngine.Tilemaps', 'Tile'):
            return result
        # The full native export retains only the Tile header. Recover its
        # serialized payload with the existing strict current assembly schema.
        source = self.recovery.fields(obj)
        target = self.queue[-1][2][11400000]['MonoBehaviour']
        expected = {'m_Sprite', 'm_Color', 'm_Transform', 'm_InstancedGameObject', 'm_Flags', 'm_ColliderType'}
        if not expected <= source.keys():
            raise ValueError('Original Tile fields were not completely decoded')

        def linked(raw):
            if not raw['m_PathID']:
                return dict(fileID=0)
            original = pointer(raw, obj)
            candidates = [key for key, value in self.objects.items() if identity(value) == identity(original)]
            if len(candidates) != 1:
                raise ValueError('Original Tile dependency must resolve to one exact native GUID: ' + identity(original))
            return self.load(candidates[0])

        target.update(m_Sprite=linked(source['m_Sprite']), m_Color=source['m_Color'],
            m_Transform={'e' + key[1:]: value for key, value in source['m_Transform'].items()},
            m_InstancedGameObject=linked(source['m_InstancedGameObject']),
            m_Flags=source['m_Flags'], m_ColliderType=source['m_ColliderType'])
        self.tile_fields.append(dict(original=identity(obj), fields=source,
            original_sha256=sha(obj.get_raw_data())))
        return result

    def register_scripts(self):
        super().register_scripts()
        for name, relative in dict(
                MiniMapPrivateRegionMaskManager='Official/World/Map13MiniMapRegions/MiniMapPrivateRegionMaskManager.cs',
                PreviewTool='Official/WorldUI/WorldPreviewTool.cs').items():
            path = ROOT / 'unity/Assets/Game/Runtime' / relative
            ref = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
            self.scripts[name] = ref
            self.class_ids[script_id('', name)] = ref
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=ref,
                sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + '.meta').read_bytes()))
            self.script_guids.add(ref['guid'])

    def build_regions(self):
        map_id = self.source.map_id
        # Verify equivalence per original file. Historical receipts remain intact.
        historical_path = ROOT / 'assets/reference/official-map/map13-minimap-region-manager-code-source-manifest.json'
        historical = json.loads(historical_path.read_bytes())
        equivalence = []
        for name in ('MiniMapPrivateRegionMaskManager', 'MapMeCharacterUIBase'):
            previous, = [r for r in historical['inputs'] if r['path'].endswith('/' + name + '.cs')]
            current = self.cache.parent / 'official-code/client-source' / (name + '.cs')
            if sha(current.read_bytes()) != previous['sha256']:
                raise ValueError('Compare and migrate the selected original minimap implementation: ' + name)
            self.inputs[current.relative_to(ROOT).as_posix()] = sha(current.read_bytes())
            equivalence.append(dict(selected=current.relative_to(ROOT).as_posix(), historical=previous))
        self.inputs[historical_path.relative_to(ROOT).as_posix()] = sha(historical_path.read_bytes())
        self.inputs[Path(__file__).relative_to(ROOT).as_posix()] = sha(Path(__file__).read_bytes())

        self.recovery.recover(Path(self.source.prefab_address).stem, self.work / f'map{map_id}-minimap-source',
            bundle=self.source.bundle, native_relative=self.source.prefab_address.removeprefix('Assets/'))
        self.recovery.finalize()
        _, _, card, full_source = self.recovery.outputs[-1]
        root_fields = next(value['MonoBehaviour'] for value in card['documents'].values()
            if 'MonoBehaviour' in value and 'GroundTileDict' in value['MonoBehaviour'])
        if root_fields['MapClassId'] != map_id:
            raise ValueError('Original minimap map identity differs')
        manager_fields = next(value['MonoBehaviour'] for value in card['documents'].values()
            if 'MonoBehaviour' in value and 'mRegionTilemapDatas' in value['MonoBehaviour'])
        rows = manager_fields['mRegionTilemapDatas']
        if len(rows) != len(manager_fields['mRegionTilemapSlots']) or not rows:
            raise ValueError('Original minimap region slot/data closure differs')
        ground_id = manager_fields['m_GameObject']['fileID']
        ground_key, = [key for key, value in card['objectMap'].items() if value == ground_id]
        ground, ground_source = self.bind_prefab(self.source,
            Path(f'Resources/Map{map_id}/Native/mini-map-ground.prefab'),
            source_root=self.original(ground_key), native_root_id=ground_id)
        # MiniMap is an injected resource/session port in the migrated manager.
        # All other original subtree fields, slots and ordering remain intact.
        for wrapper in self.queue[-1][2].values():
            fields = wrapper.get('MonoBehaviour', {})
            if 'mRegionTilemapDatas' in fields:
                fields['MiniMap'] = dict(fileID=0)

        manifest = self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive)
        asset_records = read_asset_manifest(manifest)
        regions, sources, bindings = [], [], []
        prefix = self.source.prefab_address.rsplit('/', 1)[0]
        for region, row in enumerate(rows):
            address = prefix + f'/MinimapRegionTilemap/RegionGround_{region}.prefab'
            original_guid = row['RegionTilemapAssetGuidProxy']['mAssetGuid']
            asset, = [record for record in asset_records if record['path'] == address]
            if asset['guid'] != original_guid or asset['bundle'] != self.source.bundle:
                raise ValueError('Original minimap resource GUID/path/bundle binding differs')
            pointer, source = self.bind_prefab(GroundSource(map_id, address, self.source.bundle),
                Path(f'Regions/RegionGround_{region}.prefab'))
            regions.append(dict(OriginalGuid=original_guid, Prefab=pointer))
            sources.append(source)
            bindings.append(asset)

        script = ROOT / 'unity/Assets/Game/Runtime/Presentation/World/MainlineMiniMapRegions/MainlineMiniMapRegionAssets.cs'
        catalog = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0),
            m_PrefabInstance=dict(fileID=0), m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0),
            m_Enabled=1, m_EditorHideFlags=0,
            m_Script=dict(fileID=11500000, guid=guid(Path(str(script) + '.meta')), type=3),
            m_Name='mini-map-regions', m_EditorClassIdentifier='', MapClassId=map_id, Ground=ground, Regions=regions)
        self.queue.append((Path(f'Resources/Map{map_id}/Native/mini-map-regions.asset'),
            self.native_guid('catalog'), {11400000: dict(MonoBehaviour=catalog)}))
        self.script_guids.add(catalog['m_Script']['guid'])
        self.inputs[script.relative_to(ROOT).as_posix()] = sha(script.read_bytes())
        self.write_outputs(ground_source, ground, full_minimap_source=full_source,
            original_region_sources=sources, original_code_equivalence=equivalence,
            recovered_tile_fields=self.tile_fields,
            resource_manifest_sha256=sha(manifest), original_resource_bindings=bindings,
            scope='Original Ground subtree and regional Tilemaps only. MiniMap/session/controller/resource injection uses the shared runtime; masks, marks, UI and formal map integration are separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--map-id', type=int, choices=(10, 11, 12, 13, 14, 16), required=True)
    args = parser.parse_args()
    MiniMapRegionsBinding(validation_directory(), args.map_id).build_regions()
