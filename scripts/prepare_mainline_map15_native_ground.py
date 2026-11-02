"""Bind the selected original Map15 entry ground and its complete native closure.

Reuses the verified native-resource binder and independent skeleton data format.
No animation SDK runtime is copied or loaded.
"""
import hashlib
import json
from pathlib import Path
import re
import struct

import yaml
from UnityPy.enums import ClassIDType

from official_apk import ROOT
from prepare_current_partner_native import documents, identity, pointer
from prepare_partner_card_native import PartnerCardBinding, Dumper as BindingDumper, guid, script_id, sha
from prepare_unity_map15_native_terrain import SCRIPT_TARGETS, unity_script_path
from prepare_unity_profession_skeleton_data import string, block
from prepare_native_character_geometry import atlas_regions
from convert_official_skeletons import reader_type, WHEEL_SHA256
from validation_workspace import validation_directory

DEST = ROOT / 'unity/Assets/Official/World/Map/15/NativeCurrent'
RECEIPT = ROOT / 'assets/reference/official-map/map15-current-ground-source-manifest.json'
NAME = 'MapGround_Tower15_SnowFloor1'
BUNDLE = 'defaultpackage_assets_prefabs_map_map_tower15_snowfloor1.bundle'
MODULE = ROOT / 'unity/Assets/Game/Runtime/Presentation/World/MainlineNativeGround'


class Dumper(BindingDumper):
    pass


# Unity's empty string/byte-buffer fields are blank YAML scalars. PyYAML reads
# them as None; writing the word "null" changes string/hex payloads in Unity.
Dumper.add_representer(type(None), lambda dumper, _: dumper.represent_scalar('tag:yaml.org,2002:null', ''))
def mapping(dumper, value):
    return dumper.represent_mapping('tag:yaml.org,2002:map', value.items(),
        flow_style=bool(value) and all(item is not None and not isinstance(item, (dict, list)) for item in value.values()))
Dumper.add_representer(dict, mapping)


def stable(name):
    return hashlib.md5(('zjcs:mainline-current-ground:' + name).encode()).hexdigest()


class GroundBinding(PartnerCardBinding):
    def register_scripts(self):
        paths = {name: unity_script_path(path) for name, path in SCRIPT_TARGETS.items()}
        paths.update(MyTile=unity_script_path('Game/Runtime/Official/World/NativeTerrain/MyTile.cs'),
            SkeletonAnimation=MODULE / 'MainlineGroundAnimation.cs',
            TMP_FontAsset=ROOT / 'unity/Library/PackageCache/com.unity.textmeshpro@3.0.6/Scripts/Runtime/TMP_FontAsset.cs')
        namespaces = dict(SkeletonAnimation='Spine.Unity', Light2D='UnityEngine.Rendering.Universal',
            UniversalAdditionalCameraData='UnityEngine.Rendering.Universal', TextMeshPro='TMPro',
            TMP_FontAsset='TMPro', TMPEffect='TMPro', ContentSizeFitter='UnityEngine.UI')
        for name, path in paths.items():
            if not path.is_file() or not Path(str(path) + '.meta').is_file():
                raise ValueError('Original ground script binding is unavailable: ' + str(path))
            full_name = '.'.join(filter(None, [namespaces.get(name, ''), name]))
            ref = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
            self.scripts[full_name] = ref
            self.class_ids[script_id(namespaces.get(name, ''), name)] = ref
            self.script_sources[full_name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=ref,
                sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + '.meta').read_bytes()))
        self.script_guids = {p['guid'] for p in self.scripts.values()}
        self.original_refs = {}
        self.skeletons = []

    def native_document(self, source_guid, obj):
        path = self.native_path(source_guid, obj)
        key = path.relative_to(self.cache / 'native-export-all').as_posix()
        raw = path.read_bytes()
        if sha(raw) != self.checked[key] or sha(Path(str(path) + '.meta').read_bytes()) != self.checked[key + '.meta']:
            raise ValueError('Selected native source changed: ' + key)
        self.inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
        docs = documents(raw.decode('utf8'))
        doc, = docs.values()
        content, = doc.values()
        self.learn(self.recovery.fields(obj), content, obj)
        return content

    def load(self, source_guid):
        if source_guid in self.redirects:
            return self.redirects[source_guid]
        obj = self.objects[source_guid]
        if obj.type.name == 'MonoBehaviour' and obj.read(check_read=False).m_Script.read().m_ClassName == 'SkeletonDataAsset':
            return self.skeleton(source_guid, obj)
        if obj.type.name == 'Mesh':
            # Mesh typeless buffers must retain Unity's native YAML encoding.
            # No external PPtrs occur in these original prefab preview meshes.
            native = self.native_document(source_guid, obj)
            path = self.native_path(source_guid, obj)
            raw = path.read_bytes()
            if re.search(rb'guid: [0-9a-f]{32}', raw):
                raise ValueError('Bind additional native mesh dependencies explicitly')
            target = path.relative_to(self.native)
            result = dict(fileID=4300000, guid=stable(source_guid), type=2)
            self.redirects[source_guid] = result
            self.script_guids.add(result['guid'])
            self.queue.append((target, result['guid'], raw))
            self.proof[identity(obj)] = dict(source=self.recovery.apk.provenance(obj), type='Mesh',
                original_sha256=sha(obj.get_raw_data()), native_path=path.relative_to(ROOT).as_posix(),
                output=target.as_posix(), guid=result['guid'],
                geometry=dict(vertices=native['m_VertexData']['m_VertexCount'],
                    data_bytes=native['m_VertexData']['m_DataSize'], native_yaml_preserved=True))
            return result
        before = len(self.queue)
        result = super().load(source_guid)
        if len(self.queue) > before:
            target, _, data = self.queue[-1]
            new = stable(source_guid)
            result['guid'] = new
            self.queue[-1] = target, new, data
            self.proof[identity(obj)]['guid'] = new
            self.script_guids.add(new)  # Already bound local reference in generated data assets.
        return result

    def replace(self, value):
        if isinstance(value, dict) and set(value) == {'class', 'ns', 'asm'} and value['class'] == 'StateGameObjectConfig' and value['asm'] == 'Framework':
            # These state switchers belong to the original world-label classes,
            # rather than the partner window's separately migrated UI framework.
            target = ROOT / 'unity/Assets/Game/Runtime/Official/World/NativeLabelUI/StateGameObjectConfig.cs'
            self.inputs[target.relative_to(ROOT).as_posix()] = sha(target.read_bytes())
            return dict(value, ns='Zjcs.Official.World.NativeLabelUI', asm='Zjcs.Official.World.NativeLabelUI')
        if isinstance(value, dict) and 'm_Script' in value and value['m_Script'].get('guid', '').startswith('0000000000000000'):
            # Native Tile keeps Unity's own built-in MonoScript reference.
            return {key: item if key == 'm_Script' else self.replace(item) for key, item in value.items()}
        return super().replace(value)

    def skeleton(self, source_guid, obj):
        fields = self.recovery.fields(obj)
        native = self.native_document(source_guid, obj)
        if (len(fields['atlasAssets']) != 1 or any(fields[k] for k in
                ('skeletonDataModifiers', 'fromAnimation', 'toAnimation', 'duration', 'zSlots'))):
            raise ValueError('Migrate additional original ground skeleton settings: ' + fields['m_Name'])
        atlas_obj = pointer(fields['atlasAssets'][0], obj)
        atlas_fields = self.recovery.fields(atlas_obj)
        atlas = self.native_document(native['atlasAssets'][0]['guid'], atlas_obj)
        if len(atlas_fields['materials']) != 1:
            raise ValueError('Bind all original ground atlas pages')
        material = self.load(atlas['materials'][0]['guid'])
        original = pointer(fields['skeletonJSON'], obj)
        raw = original.read().m_Script.encode('utf8', 'surrogateescape')
        reader_class, _ = reader_type()
        reader = reader_class()
        decoded = json.loads(raw) if raw.lstrip().startswith(b'{') else reader.convert(raw)
        if not raw.lstrip().startswith(b'{') and reader.inp.index != len(raw):
            raise ValueError('Original ground skeleton bytes were not completely consumed')
        atlas_text = pointer(atlas_fields['atlasFile'], atlas_obj).read().m_Script.encode('utf8', 'surrogateescape')
        regions = atlas_regions(atlas_text.decode('utf-8-sig'))
        if len({r['page'] for r in regions}) != 1:
            raise ValueError('Bind additional original ground atlas pages')
        if any(decoded.get(kind) for kind in ('ik', 'transform', 'physics')):
            raise ValueError('Migrate additional original ground constraints')
        data = (b'ZJCSWRLD' + struct.pack('<I', 1) + string(self.recovery.apk.version)
            + string(fields['m_Name']) + struct.pack('<f', fields['scale'])
            + block(json.dumps(decoded, ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode())
            + block(json.dumps(regions, ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode()))
        payload = Path('Skeletons') / (stable(source_guid) + '.bytes')
        payload_guid = stable('payload:' + source_guid)
        self.queue.append((payload, payload_guid, data))
        self.script_guids.add(payload_guid)
        target = Path('Skeletons') / (stable(source_guid) + '.asset')
        target_guid = stable(source_guid)
        result = dict(fileID=11400000, guid=target_guid, type=2)
        self.redirects[source_guid] = result
        self.script_guids.add(target_guid)
        data_script = MODULE / 'MainlineGroundSkeletonData.cs'
        self.script_guids.add(guid(Path(str(data_script) + '.meta')))
        self.script_sources['MainlineGroundSkeletonData'] = dict(path=data_script.relative_to(ROOT).as_posix(),
            sha256=sha(data_script.read_bytes()), meta_sha256=sha(Path(str(data_script) + '.meta').read_bytes()))
        doc = dict(MonoBehaviour=dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0),
            m_PrefabInstance=dict(fileID=0), m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1,
            m_EditorHideFlags=0, m_Script=dict(fileID=11500000, guid=guid(Path(str(data_script) + '.meta')), type=3),
            m_Name=fields['m_Name'], m_EditorClassIdentifier='', Payload=dict(fileID=4900000, guid=payload_guid, type=3),
            Material=material))
        self.queue.append((target, target_guid, {11400000: doc}))
        self.proof[identity(obj)] = dict(source=self.recovery.apk.provenance(obj), type='SkeletonDataAsset',
            original_sha256=sha(obj.get_raw_data()), output=target.as_posix(), guid=target_guid)
        self.skeletons.append(dict(source=self.recovery.apk.provenance(obj), fields=fields,
            skeleton_source=self.recovery.apk.provenance(original), skeleton_sha256=sha(raw),
            atlas_source=self.recovery.apk.provenance(atlas_obj), atlas_sha256=sha(atlas_text),
            decoded_sha256=sha(json.dumps(decoded, sort_keys=True, separators=(',', ':')).encode()),
            bones=len(decoded['bones']), slots=len(decoded['slots']), animations=list(decoded['animations']),
            paths=decoded.get('path', []), output=target.as_posix()))
        return result

    def build_ground(self):
        self.recovery.recover(NAME, self.work / 'map15-ground-native', bundle=BUNDLE,
            native_relative='Prefabs/Map/Map_Tower15_SnowFloor1/' + NAME + '.prefab')
        self.recovery.finalize()
        source, _, card, record = self.recovery.outputs[0]
        source_key = source.relative_to(self.cache / 'native-export-all').as_posix()
        if sha(source.read_bytes()) != self.checked[source_key]:
            raise ValueError('Original selected ground prefab export changed')
        for key, ref in card['external'].items():
            self.objects[ref['guid']] = self.original(key)
        for key, file_id in card['objectMap'].items():
            obj = self.original(key)
            self.learn(self.recovery.fields(obj), next(iter(card['documents'][file_id].values())), obj)
        for script in card['scripts']:
            fields = card['documents'][script['nativeFileID']]['MonoBehaviour']
            full_name = '.'.join(filter(None, [script['namespace'], script['type']]))
            fields['m_Script'] = self.scripts[full_name]
        prefab_guid = stable('root:' + NAME)
        target = Path('Resources/Map15/Native/current-ground.prefab')
        self.queue.append((target, prefab_guid, card['documents']))
        outputs = {}
        index = 0
        while index < len(self.queue):
            target, target_guid, data = self.queue[index]
            index += 1
            if isinstance(data, dict):
                try:
                    data = self.replace(data)
                except Exception as error:
                    raise ValueError('Native ground dependency failed in ' + target.as_posix()) from error
                raw = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
                for file_id, content in data.items():
                    kind, = content
                    raw += '--- !u!' + str(int(ClassIDType[kind])) + ' &' + str(file_id) + '\n'
                    raw += yaml.dump(content, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
                raw = raw.encode('utf8')
            else:
                raw = data
            path = DEST / target
            path.parent.mkdir(parents=True, exist_ok=True)
            if path.is_file() and path.read_bytes() != raw:
                previous = json.loads(RECEIPT.read_bytes()) if RECEIPT.is_file() else {}
                if sha(path.read_bytes()) != previous.get('outputs', {}).get(path.relative_to(ROOT).as_posix()):
                    raise ValueError('Preserve changed native ground output: ' + str(path))
            path.write_bytes(raw)
            extension = path.suffix
            importer = ('PrefabImporter' if extension == '.prefab' else 'ShaderImporter' if extension == '.shader'
                else 'TextScriptImporter' if extension in ('.bytes', '.txt', '.json') else 'NativeFormatImporter')
            metadata = Path(str(path) + '.meta')
            if not metadata.exists():
                metadata.write_text('fileFormatVersion: 2\nguid: ' + target_guid + '\n' + importer + ':\n  externalObjects: {}\n', 'utf8')
            if guid(metadata) != target_guid:
                raise ValueError('Installed native ground GUID changed')
            outputs[path.relative_to(ROOT).as_posix()] = sha(raw)
            outputs[metadata.relative_to(ROOT).as_posix()] = sha(metadata.read_bytes())
        for folder in (DEST, *sorted(p for p in DEST.rglob('*') if p.is_dir())):
            metadata = Path(str(folder) + '.meta')
            if not metadata.exists():
                metadata.write_text('fileFormatVersion: 2\nguid: ' + stable('folder:' + folder.relative_to(DEST).as_posix())
                    + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n', 'utf8')
            outputs[metadata.relative_to(ROOT).as_posix()] = sha(metadata.read_bytes())
        receipt = dict(format=1, manifest=self.recovery.apk.version, mapClassId=15,
            input_receipts=self.inputs, root_source=record, root_pointer=dict(fileID=card['root'], guid=prefab_guid, type=3),
            objects=self.proof, skeletons=self.skeletons, scripts=self.script_sources, outputs=outputs,
            format_reader_sha256=WHEEL_SHA256, tools={p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in
                [Path(__file__), *[ROOT / 'scripts' / name for name in ('prepare_current_partner_native.py',
                    'prepare_partner_card_native.py', 'prepare_unity_profession_skeleton_data.py',
                    'prepare_native_character_geometry.py', 'convert_official_skeletons.py')]]},
            operation='Pair original CAB/PPtr hierarchy and complete selected native fields. Bind exact native GUID dependencies, verified current Windows textures/shaders, and independent original skeleton/atlas data. Preserve native fileIDs, tile sprites, labels, transforms and renderers. Historical terrain exports remain separate.')
        RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Bound current original Map15 ground:', len(outputs), 'outputs,', len(self.skeletons), 'skeletons', flush=True)


if __name__ == '__main__':
    GroundBinding(validation_directory()).build_ground()
