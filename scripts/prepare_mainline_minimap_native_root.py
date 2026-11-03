"""Recover the complete selected original minimap prefab for the shared module."""
import argparse
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from official_manifest import read_asset_manifest
from prepare_current_partner_native import documents, identity, pointer as original_pointer
from prepare_mainline_minimap_regions import MiniMapRegionsBinding
from prepare_mainline_map15_native_ground import GroundSource, guid, script_id, sha
from validation_workspace import validation_directory


def integration_sources():
    module = ROOT / 'unity/Assets/Game/Runtime/Offline/World/MainlineMiniMap'
    paths = sorted(p for p in module.iterdir() if p.is_file())
    paths += [ROOT / 'scripts/prepare_mainline_minimap_offline_validation.py']
    paths += sorted((ROOT / 'unity/Assets/Game/Editor/Map13MiniMap').glob('OfficialMainlineMiniMapRootAcceptance*.cs*'))
    return [dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p.read_bytes())) for p in paths]


def refresh_receipt_integration(receipt_path):
    receipt = json.loads(receipt_path.read_bytes())
    tool = Path(__file__).relative_to(ROOT).as_posix()
    for name, expected in receipt['input_receipts'].items():
        if name != tool and sha((ROOT / name).read_bytes()) != expected:
            raise ValueError('Rebuild changed original input before refreshing integration: ' + name)
    for name, expected in receipt['outputs'].items():
        if sha((ROOT / name).read_bytes()) != expected:
            raise ValueError('Preserve changed original root output: ' + name)
    module = 'unity/Assets/Game/Runtime/Offline/World/MainlineMiniMap/'
    for script in receipt['scripts'].values():
        path = ROOT / script['path']
        meta = Path(str(path) + '.meta')
        if sha(meta.read_bytes()) != script['meta_sha256']:
            raise ValueError('Review changed original MonoScript metadata: ' + script['path'])
        if script['path'].startswith(module):
            if script['pointer'] != dict(fileID=11500000, guid=guid(meta), type=3):
                raise ValueError('Preserve original MonoScript binding: ' + script['path'])
            script['sha256'] = sha(path.read_bytes())
        elif sha(path.read_bytes()) != script['sha256']:
            raise ValueError('Rebuild changed official script before refreshing integration: ' + script['path'])
    receipt['integration'] = integration_sources()
    receipt['input_receipts'][tool] = sha(Path(__file__).read_bytes())
    receipt_path.write_text(json.dumps(receipt,ensure_ascii=False,indent=2)+'\n',encoding='utf8',newline='\n')
    print('Refreshed verified unchanged native integration:', receipt_path.name)


def refresh_integration(map_id, include_shared=False):
    refresh_receipt_integration(ROOT / f'assets/reference/official-map/map{map_id}-current-minimap-root-source-manifest.json')
    if include_shared:
        for kind in ('hud', 'renderer', 'panel'):
            refresh_receipt_integration(ROOT / f'assets/reference/official-map/mainline-current-minimap-{kind}-source-manifest.json')
    print('Refreshed verified unchanged native root integration:',map_id)


class MiniMapRootBinding(MiniMapRegionsBinding):
    def __init__(self, work, map_id):
        super().__init__(work, map_id)
        self.destination = ROOT / f'unity/Assets/Official/World/Map/{map_id}/NativeMiniMapRootCurrent'
        self.receipt_path = ROOT / f'assets/reference/official-map/map{map_id}-current-minimap-root-source-manifest.json'
        self.prefab_sources = []

    def native_guid(self, name):
        return hashlib.md5(f'zjcs:mainline-minimap-root:Map{self.source.map_id}:{name}'.encode()).hexdigest()

    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT / 'unity/Assets/Game/Runtime'
        paths = {'MiniMap': runtime / 'Offline/World/MainlineMiniMap/MainlineNativeMiniMapRoot.cs'}
        for folder, names in {
            'MainlineMiniMapRoot': ['OfflineMiniMap'],
            'Map13MiniMapMasks': ['MiniMapMaskManager', 'OfflineMiniMapMaskManager'],
            'Map13MiniMap': ['MiniMapMaskHandler'],
            'Map13MiniMapCharacter': ['MiniMapCharacterManager'],
            'Map13MiniMapMarkManagers': ['MiniMapPrivateMarkManager', 'MiniMapPublicMarkManager', 'OfflineMiniMapMarkManager'],
            'Map13MiniMapMarkClick': ['MiniMapMarkClick'],
            'Map13MiniMapMarkSpawn': ['MiniMapMarkSpawnManager'],
            'Map13MiniMapDescriptions': ['MiniMapRegionDescManager', 'MiniMapRegionBoardLineManager', 'MiniMapRegionDescItem', 'MapRegionDescItemUI'],
            'Map13MiniMapSizing': ['MiniMapKeepSizeHandler', 'MiniMapKeepSizeObj'],
            'Map13MiniMapResourcePoints': ['MiniMapResourcePointManager'],
            'NativeMap13Labels': ['MapRegionDescSupportUI', 'MapRegionStrengthDisplayUI', 'ShowRemainTimeInfo', 'UIColorByLevel'],
            'NativeTerrain': ['MapAreaTile'],
        }.items():
            paths.update({name: runtime / f'Official/World/{folder}/{name}.cs' for name in names})
        for name, path in paths.items():
            ref = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
            self.scripts[name] = ref
            self.class_ids[script_id('', name)] = ref
            self.script_guids.add(ref['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=ref,
                sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + '.meta').read_bytes()))

    def load(self, source_guid):
        if source_guid in self.redirects:
            return self.redirects[source_guid]
        obj = self.objects[source_guid]
        if obj.type.name in ('GameObject', 'MonoBehaviour', 'SpriteRenderer'):
            value = obj.read(check_read=False)
            owner = getattr(value, 'm_GameObject', None)
            named = owner.deref() if owner is not None and owner.path_id else obj
            path = self.native_path(source_guid, named)
            if path.suffix == '.prefab':
                address = 'Assets/' + path.relative_to(self.native).as_posix()
                manifest = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
                record, = [row for row in manifest if row['path'] == address]
                pointer, source = self.bind_prefab(GroundSource(self.source.map_id, address, record['bundle']),
                    Path('Dependencies') / path.relative_to(self.native))
                card = self.recovery.outputs[-1][2]
                pointer = dict(pointer, fileID=card['objectMap'][identity(obj)])
                self.redirects[source_guid] = pointer
                self.prefab_sources.append(source)
                return pointer
        custom_tile = False
        if obj.type.name == 'MonoBehaviour':
            script = obj.read(check_read=False).m_Script.read()
            custom_tile = script.m_Namespace == '' and script.m_ClassName in ('MyTile', 'MapAreaTile')
            if custom_tile or (script.m_Namespace, script.m_ClassName) == ('UnityEngine.Tilemaps', 'Tile'):
                sprite = self.recovery.fields(obj)['m_Sprite']
                if sprite['m_PathID']:
                    self.sprite_reference(original_pointer(sprite, obj))
        result = super().load(source_guid)
        if custom_tile:
            fields = self.recovery.fields(obj)
            _, _, docs = next(row for row in self.queue if row[1] == result['guid'])
            target = docs[result['fileID']]['MonoBehaviour']
            if fields['m_InstancedGameObject']['m_PathID']:
                raise ValueError('Bind original area Tile instantiated prefab')
            sprite = fields['m_Sprite']
            target.update(m_Sprite=self.sprite_reference(original_pointer(sprite,obj)) if sprite['m_PathID'] else dict(fileID=0),
                m_Color=fields['m_Color'], m_Transform={'e' + k[1:]: v for k,v in fields['m_Transform'].items()},
                m_InstancedGameObject=dict(fileID=0), m_Flags=fields['m_Flags'], m_ColliderType=fields['m_ColliderType'])
            if script.m_ClassName == 'MapAreaTile':
                target['Area'] = fields['Area']
            self.tile_fields.append(dict(original=identity(obj), fields=fields, original_sha256=sha(obj.get_raw_data())))
        return result

    def sprite_reference(self, obj):
        candidates = [key for key, value in self.objects.items() if identity(value) == identity(obj)]
        if not candidates:
            key, local = self.recovery.fields(obj)['m_RenderDataKey']
            render_guid = ''.join(format(key[f'data[{i}]'], '08x')[::-1] for i in range(4))
            name = self.name_key(obj.read(check_read=False).m_Name)
            matches = set()
            for candidate_name in [name] + [name + str(i) for i in range(100)]:
                for path in self.by_name.get(candidate_name, []):
                    if path.suffix != '.asset':
                        continue
                    docs = documents(path.read_text('utf8'))
                    if len(docs) == 1 and next(iter(docs.values())).get('Sprite', {}).get('m_RenderDataKey') == {render_guid: local}:
                        matches.add(path)
            path, = matches
            source_guid = guid(Path(str(path) + '.meta'))
            if source_guid in self.objects and identity(self.objects[source_guid]) != identity(obj):
                raise ValueError('Original sprite identity collides with a serialized dependency')
            self.objects[source_guid] = obj
            candidates = [source_guid]
        source_guid, = candidates
        return self.load(source_guid)

    def original_asset(self, obj):
        key = identity(obj)
        if key in self.original_refs:
            return self.original_refs[key]
        if obj.type.name != 'MonoBehaviour':
            return super().original_asset(obj)
        script = obj.read(check_read=False).m_Script.read()
        if (script.m_Namespace, script.m_ClassName) != ('UnityEngine.Tilemaps', 'Tile'):
            raise ValueError('Bind additional original inline minimap resources: ' + key)
        fields = self.recovery.fields(obj)
        if fields['m_InstancedGameObject']['m_PathID']:
            raise ValueError('Bind original mask Tile instantiated prefab')
        sprite = fields['m_Sprite']
        data = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0),
            m_PrefabInstance=dict(fileID=0), m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0),
            m_Enabled=1, m_EditorHideFlags=0, m_Script=self.tile_script, m_Name=fields['m_Name'],
            m_EditorClassIdentifier='', m_Sprite=self.sprite_reference(original_pointer(sprite, obj)) if sprite['m_PathID'] else dict(fileID=0),
            m_Color=fields['m_Color'], m_Transform={'e' + k[1:]: v for k, v in fields['m_Transform'].items()},
            m_InstancedGameObject=dict(fileID=0), m_Flags=fields['m_Flags'], m_ColliderType=fields['m_ColliderType'])
        target_guid = self.native_guid('original:' + key)
        target = Path('Tiles') / (sha(key.encode())[:16] + '.asset')
        result = dict(fileID=11400000, guid=target_guid, type=2)
        self.original_refs[key] = result
        self.script_guids.add(target_guid)
        self.queue.append((target, target_guid, {11400000: dict(MonoBehaviour=data)}))
        self.proof[key] = dict(source=self.recovery.apk.provenance(obj), original_sha256=sha(obj.get_raw_data()),
            fields=fields, output=target.as_posix(), guid=target_guid)
        return result

    def replace(self, value):
        if isinstance(value, dict) and set(value) == {'class', 'ns', 'asm'}:
            if value['class'] in ('StateGameObjectConfig', 'StateAlphaConfig', 'StateGraphicColorConfig'):
                return dict(value, ns='Zjcs.Official.World.NativeLabelUI', asm='Zjcs.Official.World.NativeLabelUI')
            if value['asm'] == 'Assembly-CSharp' and (ROOT / 'unity/Assets/Game/Runtime/Official/World/NativeMap13Labels' / (value['class'] + '.cs')).is_file():
                return dict(value, ns='Zjcs.Official.World.NativeMap13Labels', asm='Zjcs.Official.World.NativeMap13Labels')
        return super().replace(value)

    def write_outputs(self, *args, **kwargs):
        super().write_outputs(*args, **kwargs)
        # Imported Unity metadata may use CRLF and spaces after empty fields.
        # Record the same canonical bytes that this module commits to Git.
        for path in [Path(str(self.destination) + '.meta'), *self.destination.rglob('*.meta')]:
            raw = path.read_bytes()
            normalized = b'\n'.join(line.rstrip(b' \t\r') for line in raw.split(b'\n'))
            if normalized != raw:
                path.write_bytes(normalized)
        receipt = json.loads(self.receipt_path.read_bytes())
        for name in receipt['outputs']:
            if name.endswith('.meta'):
                receipt['outputs'][name] = sha((ROOT / name).read_bytes())
        receipt['metadata_text_normalization'] = 'UTF-8 LF; remove trailing spaces from empty importer fields without changing fields or GUIDs.'
        self.receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf8', newline='\n')

    def build_root(self):
        pointer, record = self.bind_prefab(self.source,
            Path(f'Resources/Map{self.source.map_id}/Native/mini-map-root.prefab'))
        card = self.recovery.outputs[-1][2]
        mask = next(row['MonoBehaviour'] for row in card['documents'].values()
            if 'MaskTile' in row.get('MonoBehaviour', {}))
        source_guid = mask['MaskTile']['guid']
        tile = self.objects[source_guid]
        script = tile.read(check_read=False).m_Script.read()
        if (script.m_Namespace, script.m_ClassName) != ('UnityEngine.Tilemaps', 'Tile'):
            raise ValueError('Original mask must reference the built-in Unity Tile')
        self.tile_script = self.native_document(source_guid, tile)['m_Script']
        area_code = self.cache.parent / 'official-code/client-source/MapAreaTile.cs'
        if sha(area_code.read_bytes()) != '5b9b0bedf22c5fca7d9078e40435fc0f98d7a5ea340bf51211f05002c6ebc33d':
            raise ValueError('Review the complete selected MapAreaTile declaration')
        self.inputs[area_code.relative_to(ROOT).as_posix()] = sha(area_code.read_bytes())
        equivalence = []
        receipts = [ROOT / 'assets/reference/official-map/mainline-minimap-root-source-manifest.json']
        receipts += sorted((ROOT / 'assets/reference/official-map').glob('map13-minimap*-code-source-manifest.json'))
        for name in sorted({row['type'] for row in card['scripts'] if not row['namespace']} - {'PreviewTool'}):
            original = self.cache.parent / 'official-code/client-source' / (name + '.cs')
            original_hash = sha(original.read_bytes())
            matches = []
            for receipt_path in receipts:
                data = json.loads(receipt_path.read_bytes())
                for row in data.get('inputs', []):
                    if row['path'].endswith('/' + name + '.cs') and row['sha256'] == original_hash:
                        matches.append(dict(receipt=receipt_path.relative_to(ROOT).as_posix(), original=row))
            if not matches:
                raise ValueError('Review and migrate the selected original minimap code: ' + name)
            selected = matches[0]
            self.inputs[selected['receipt']] = sha((ROOT / selected['receipt']).read_bytes())
            self.inputs[original.relative_to(ROOT).as_posix()] = original_hash
            equivalence.append(dict(selected=original.relative_to(ROOT).as_posix(), reused=selected))
        self.inputs[Path(__file__).relative_to(ROOT).as_posix()] = sha(Path(__file__).read_bytes())
        self.write_outputs(record, pointer, recovered_tile_fields=self.tile_fields,
            dependency_prefabs=self.prefab_sources,
            original_code_equivalence=equivalence,
            integration=integration_sources(),
            scope='Complete original root hierarchy and serialized references. The shared factory supplies local dependencies before activation; formal map/HUD/Player integration remains separate.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--map-id', type=int, choices=(10, 11, 12, 13, 14, 16), required=True)
    parser.add_argument('--refresh-integration', action='store_true', help='Verify unchanged native outputs and refresh shared factory/fixture hashes only')
    parser.add_argument('--include-shared', action='store_true', help='Also verify and refresh the shared HUD, renderer and fullscreen window integration')
    args = parser.parse_args()
    if args.refresh_integration:
        refresh_integration(args.map_id, args.include_shared)
    else:
        MiniMapRootBinding(validation_directory(), args.map_id).build_root()
