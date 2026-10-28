"""Restore the two original tree renderer sub-prefabs with native mesh targets.

Spine components are explicit independent geometry adapters. Original Unity
components, hierarchy, material bindings, source flags and component order stay
intact. These sub-prefabs do not replace their enclosing gameplay entities.
"""
from __future__ import annotations
import json
import copy
from pathlib import Path
import re
import struct
import uuid
import yaml

from official_apk import ROOT, Apk
from prepare_unity_map13_prop_hierarchies import sha, key, write, encode

SOURCE = ROOT / 'unity/Assets/Official/World/Map/13/Source'
DEST = ROOT / 'unity/Assets/Game/Resources/Map13/Native/PropParts'
RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Offline/World/NativeMap13Skeletons'
OFFICIAL = ROOT / 'unity/Assets/Game/Runtime/Official/World/Map13Rendering'
ART = ROOT / 'unity/Assets/Official/World/Map/13/NativePropPartsArt'
RECEIPT = ROOT / 'assets/reference/official-map/map13-native-prop-parts-source-manifest.json'


class UnityDumper(yaml.SafeDumper): pass
UnityDumper.add_representer(bool, lambda dumper, value: dumper.represent_scalar('tag:yaml.org,2002:int', '1' if value else '0'))
UnityDumper.add_representer(str, lambda dumper, value: dumper.represent_scalar(
    'tag:yaml.org,2002:str', value, style='"' if not value or re.fullmatch(r'[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?', value) else None))


def dump(value): return yaml.dump(value, Dumper=UnityDumper, allow_unicode=True, sort_keys=False, width=150)


def meta(path, importer='DefaultImporter', folder=False):
    target = Path(str(path) + '.meta')
    if not target.exists():
        guid = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs:map13-prop-parts:' + path.relative_to(ROOT).as_posix()).hex
        raw = 'fileFormatVersion: 2\nguid: ' + guid + '\n'
        if folder: raw += 'folderAsset: yes\n'
        raw += importer + ':\n  externalObjects: {}\n'
        if importer == 'MonoImporter':
            raw += '  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n'
        write(target, raw.encode())
    return re.search(rb'(?m)^guid: ([a-f0-9]{32})', target.read_bytes())[1].decode()


def ref(file_id=0, guid=None, typ=3):
    return dict(fileID=file_id, **(dict(guid=guid, type=typ) if guid else {}))


def build():
    source_path = SOURCE / 'native-prop-hierarchies.json'
    separator_path = SOURCE / 'native-prop-separators.json'
    hierarchy = json.loads(source_path.read_bytes())
    recipe = json.loads(separator_path.read_bytes())
    proof_path = RECEIPT.with_name('map13-native-prop-hierarchies-source-manifest.json')
    for output in json.loads(proof_path.read_bytes())['outputs']:
        if sha((ROOT / output['path']).read_bytes()) != output['sha256']:
            raise ValueError('Original hierarchy source changed: ' + output['path'])
    indexed = {r['key']: r for r in hierarchy['objects']}
    materials = {key(r['cab'], r['path_id']): ROOT / r['output']['path']
                 for r in json.loads((SOURCE / 'native-entity-material-bindings.json').read_bytes())['entries']}
    outputs = []
    sprite_outputs, sprite_inputs = {}, []
    graph = json.loads((SOURCE / 'native-entity-object-graph.json').read_bytes())
    graph_objects = {key(r['cab'], r['path_id']): r for r in graph['objects']}

    def sprite(identity):
        if identity in sprite_outputs: return sprite_outputs[identity]
        from prepare_unity_map13_entity_textures import decode
        from prepare_unity_map13_native_scene import native_texture
        apk = Apk.current(); source = graph_objects[identity]
        objects = apk.objects(source['input'])
        obj, = [o for o in objects if key(o.assets_file.name, o.path_id) == identity]
        if sha(obj.get_raw_data()) != source['payload_sha256']:
            raise ValueError('Original Snow sprite source changed')
        original = obj.read_typetree(check_read=True)
        if original['m_SpriteAtlas']['m_PathID'] or original['m_RD']['alphaTexture']['m_PathID'] or original['m_RD']['secondaryTextures']:
            raise ValueError('Additional original sprite atlas/texture binding needs migration')
        texture_ref, = [r for r in source['references'] if r['field'] == '.m_RD.texture']
        texid = key(texture_ref['cab'], texture_ref['path_id'])
        texture_obj, = [o for o in objects if key(o.assets_file.name, o.path_id) == texid]
        if sha(texture_obj.get_raw_data()) != graph_objects[texid]['payload_sha256']:
            raise ValueError('Original Snow texture changed')
        texture = texture_obj.read(check_read=True)
        rgba, mips, profile, native_format = decode(texture)
        template_path = ROOT / 'unity/Assets/Official/World/Map/13/NativeEntityTextures/50c5ac60e7dfbffee63c900a7d4d97b6/-9203820619277488241.texture.asset'
        template = template_path.read_text(encoding='utf-8-sig').replace('--- !u!28 &2800000\n', '--- !u!28 &1\n')
        native = native_texture(template, texture, rgba).decode()
        for field in ('m_TextureFormat', 'm_ForcedFallbackFormat', 'm_DownscaleFallback', 'm_IsAlphaChannelOptional'):
            value = native_format if field == 'm_TextureFormat' else int(getattr(texture, field))
            native, count = re.subn(r'(?m)^(  ' + field + r':)[^\n]*', lambda m: m[1] + ' ' + str(value), native)
            if count != 1: raise ValueError('Native texture schema differs')
        texture_path = ART / 'Building_tree_snow.texture.asset'
        write(texture_path, native.encode()); texture_guid = meta(texture_path, 'NativeFormatImporter'); outputs.append(texture_path)
        def relocate_sprite(value):
            if isinstance(value, dict):
                if set(value) == {'m_FileID', 'm_PathID'}:
                    if value['m_PathID']:
                        if value != original['m_RD']['texture']: raise ValueError('Additional native sprite pointer needs binding')
                        return ref(2800000, texture_guid, 2)
                    return ref()
                return {k: relocate_sprite(v) for k, v in value.items()}
            if isinstance(value, list): return [relocate_sprite(v) for v in value]
            return value
        native = relocate_sprite(copy.deepcopy(original))
        # Native Unity 2022.3 text serialization versions are part of the schema;
        # without them Unity reads the legacy layout despite valid buffer bytes.
        native['m_Rect'] = dict(serializedVersion=2, **native['m_Rect'])
        native['m_RD'] = dict(serializedVersion=3, **native['m_RD'])
        native['m_RD']['textureRect'] = dict(serializedVersion=2, **native['m_RD']['textureRect'])
        native['m_RD']['m_SubMeshes'] = [dict(serializedVersion=2, **r) for r in native['m_RD']['m_SubMeshes']]
        native['m_RD']['m_VertexData'] = dict(serializedVersion=3, **native['m_RD']['m_VertexData'])
        native['m_RD']['m_IndexBuffer'] = bytes(original['m_RD']['m_IndexBuffer']).hex()
        vertices = bytes(original['m_RD']['m_VertexData']['m_DataSize'])
        native['m_RD']['m_VertexData']['m_DataSize'] = len(vertices)
        native['m_RD']['m_VertexData']['_typelessdata'] = vertices.hex()
        native['m_RenderDataKey'] = {''.join(format(v, '08x') for v in original['m_RenderDataKey'][0].values()): original['m_RenderDataKey'][1]}
        path = ART / 'Building_tree_snow.sprite.asset'
        write(path, ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!213 &21300000\n' + dump({'Sprite': native})).encode())
        meta(path, 'NativeFormatImporter'); meta(ART, folder=True); outputs.append(path)
        sprite_inputs.append(dict(identity=identity, source=apk.provenance(obj), payload_sha256=sha(obj.get_raw_data()),
            vertices_sha256=sha(vertices), indices_sha256=sha(bytes(original['m_RD']['m_IndexBuffer'])),
            vertices=[dict(x=struct.unpack_from('<3f', vertices, i * 12)[0], y=struct.unpack_from('<3f', vertices, i * 12)[1])
                      for i in range(original['m_RD']['m_VertexData']['m_VertexCount'])],
            triangles=list(struct.unpack('<' + 'H' * (len(original['m_RD']['m_IndexBuffer']) // 2), bytes(original['m_RD']['m_IndexBuffer']))),
            pixels_per_unit=original['m_PixelsToUnits'], width=original['m_Rect']['width'], height=original['m_Rect']['height'],
            pivot=dict(x=original['m_Pivot']['x'] * original['m_Rect']['width'], y=original['m_Pivot']['y'] * original['m_Rect']['height']),
            texture=dict(source=apk.provenance(texture_obj), payload_sha256=sha(texture_obj.get_raw_data()),
                decoded_rgba_sha256=sha(rgba), mips=mips, profile=profile, native_format=native_format),
            serialization_template=dict(path=template_path.relative_to(ROOT).as_posix(), sha256=sha(template_path.read_bytes())),
            sprite_serialization_template=dict(path='unity/Assets/Official/World/Map/13/NativeScene/Effect/CustomWater/Textures/default_water_mask.asset',
                sha256=sha((ROOT / 'unity/Assets/Official/World/Map/13/NativeScene/Effect/CustomWater/Textures/default_water_mask.asset').read_bytes()))))
        sprite_outputs[identity] = path
        return path
    original = ROOT / '.tools/source-packages/current' / hierarchy['manifest'] / 'official-code/client-source/ResetMaterialPropertyBlock.cs'
    official = original.read_text(encoding='utf-8-sig').replace('\r\n', '\n')
    official = re.sub(r'^\s*//IL_[^\n]*\n', '', official, flags=re.M)
    official = official.replace('public class ResetMaterialPropertyBlock', 'namespace Zjcs.Official.World.Map13Rendering\n{\npublic class ResetMaterialPropertyBlock') + '\n}\n'
    write(OFFICIAL / 'ResetMaterialPropertyBlock.cs', official.encode())
    write(OFFICIAL / 'Zjcs.Official.World.Map13Rendering.asmdef', encode(dict(
        name='Zjcs.Official.World.Map13Rendering', rootNamespace='Zjcs.Official.World.Map13Rendering', references=[], autoReferenced=False)))
    meta(OFFICIAL, folder=True)
    for path in sorted(OFFICIAL.iterdir()):
        if path.suffix == '.cs': meta(path, 'MonoImporter'); outputs.append(path)
        elif path.suffix == '.asmdef': meta(path, 'AssemblyDefinitionImporter'); outputs.append(path)
    scripts = {}
    for name in ('NativeMap13PropMecanim', 'NativeMap13PartsBinding', 'NativeMap13PartRenderer'):
        path = RUNTIME / (name + '.cs')
        scripts[name] = meta(path, 'MonoImporter')
        outputs.append(path)
    scripts['ResetMaterialPropertyBlock'] = meta(OFFICIAL / 'ResetMaterialPropertyBlock.cs', 'MonoImporter')
    DEST.mkdir(parents=True, exist_ok=True); meta(DEST, folder=True)
    native_classes = dict(GameObject=1, Transform=4, MeshRenderer=23, MeshFilter=33, Animator=95, SpriteRenderer=212)
    adapted = {'Spine.Unity.SkeletonMecanim': 'NativeMap13PropMecanim',
               'Spine.Unity.SkeletonRenderSeparator': 'NativeMap13PartsBinding',
               'Spine.Unity.SkeletonPartsRenderer': 'NativeMap13PartRenderer',
               'ResetMaterialPropertyBlock': 'ResetMaterialPropertyBlock'}
    script_metadata = []
    apk = Apk.current()
    for original_type, replacement in adapted.items():
        component = next(r for r in hierarchy['objects'] if r['type'] == original_type)
        script_ref, = [r for r in component['references'] if r['field'] == '.m_Script']
        identity = key(script_ref['cab'], script_ref['path_id']); source = graph_objects[identity]
        obj, = [o for o in apk.objects(source['input']) if key(o.assets_file.name, o.path_id) == identity]
        fields = obj.read_typetree(check_read=True)
        if sha(obj.get_raw_data()) != source['payload_sha256'] or fields['m_ExecutionOrder'] != 0:
            raise ValueError('Original rendering script metadata/execution order changed')
        script_metadata.append(dict(original_type=original_type, source=apk.provenance(obj),
            payload_sha256=sha(obj.get_raw_data()), fields=fields, replacement=replacement, replacement_guid=scripts[replacement]))
    prefab_records = []
    for entry in recipe['separators']:
        cab = entry['renderer'].split(':')[0]
        selected = []
        for node in entry['nodes']:
            go = indexed[node['key']]
            selected.append(go)
            selected.extend(indexed[key(r['cab'], r['path_id'])] for r in go['references'] if r['field'].startswith('.m_Component['))
        selected = list({r['key']: r for r in selected}.values())
        ids = {r['key']: 100001 + i for i, r in enumerate(selected)}
        root = entry['nodes'][0]['key']
        owner_renderer = next(r for r in selected if r['type'] == 'MeshRenderer' and
            r['fields']['m_GameObject']['m_PathID'] == int(indexed[root]['path_id']))
        part_renderers = []
        for part in entry['parts']:
            pid = int(indexed[part]['path_id'])
            part_renderers.append(next(r for r in selected if r['type'] == 'MeshRenderer' and r['fields']['m_GameObject']['m_PathID'] == pid))
        animator = next(r for r in selected if r['type'] == 'Animator')
        separator = entry['source']

        def relocate(value, owner, field=''):
            if isinstance(value, dict):
                if set(value) == {'m_FileID', 'm_PathID'}:
                    if not value['m_PathID']: return ref()
                    references = [r for r in owner['references'] if r['field'] == field]
                    if len(references) != 1: raise ValueError('Original pointer is not explicit: ' + field)
                    identity = key(references[0]['cab'], references[0]['path_id'])
                    if identity in ids: return ref(ids[identity])
                    if identity in materials: return ref(2100000, meta(materials[identity]), 2)
                    if field == '.m_Sprite': return ref(21300000, meta(sprite(identity)), 2)
                    if field == '.m_Father' and owner['key'] == entry['nodes'][0]['transform_key']: return ref()
                    if field == '.m_Controller':
                        controller_key = sha(identity.encode())[:24]
                        path = ROOT / 'unity/Assets/Official/World/Map/13/NativePropAnimators/Controllers' / (controller_key + '.controller')
                        if not path.is_file(): raise ValueError('Original controller has not been restored')
                        return ref(9100000, meta(path), 2)
                    raise ValueError('Unbound native prefab pointer: ' + identity + ' / ' + field)
                return {k: relocate(v, owner, field + '.' + k) for k, v in value.items()}
            if isinstance(value, list): return [relocate(v, owner, field + '[' + str(i) + ']') for i, v in enumerate(value)]
            return value

        documents = []
        for record in selected:
            kind = record['type']; fields = record['fields']
            base = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=ref(), m_PrefabInstance=ref(), m_PrefabAsset=ref())
            if kind in native_classes:
                body = relocate(fields, record)
                if kind == 'GameObject':
                    if body.pop('m_Tag') != 0: raise ValueError('Additional original tag needs a binding')
                    body = dict(serializedVersion=6, m_Component=body['m_Component'], m_Layer=body['m_Layer'],
                                m_Name=body['m_Name'], m_TagString='Untagged', m_Icon=ref(), m_NavMeshLayer=0,
                                m_StaticEditorFlags=0, m_IsActive=body['m_IsActive'])
                elif kind == 'Transform': body = dict(serializedVersion=2, **body)
                elif kind == 'Animator': body = dict(serializedVersion=5, **body)
                base.update(body)
                class_id = native_classes[kind]
            else:
                if kind not in adapted: raise ValueError('Additional original renderer component needs migration: ' + kind)
                replacement = adapted[kind]
                base.update(m_GameObject=relocate(fields['m_GameObject'], record, '.m_GameObject'),
                    m_Enabled=fields['m_Enabled'], m_EditorHideFlags=0, m_Script=ref(11500000, scripts[replacement]),
                    m_Name='', m_EditorClassIdentifier='')
                if replacement == 'NativeMap13PropMecanim':
                    profile_keys = ('initialFlipX', 'initialFlipY', 'pmaVertexColors', 'tintBlack', 'useClipping', 'zSpacing', 'initialSkinName', 'translator')
                    path = ROOT / 'unity/Assets/Game/Resources/Map13/Native/Skeletons' / (entry['model'] + '.asset')
                    base.update(SourceKey=record['key'], ControllerKey=entry['nodes'][0]['controller'],
                        Data=ref(11400000, meta(path), 2), Profile={k: fields[k] for k in profile_keys},
                        Animator=ref(ids[animator['key']]), Parts=ref(ids[separator['key']]),
                        UpdateTiming=fields['updateTiming'], UpdateWhenInvisible=fields['updateWhenInvisible'],
                        ClearStateOnDisable=fields['clearStateOnDisable'])
                elif replacement == 'NativeMap13PartsBinding':
                    base.update(SourceKey=record['key'], SourceRenderer=ref(ids[owner_renderer['key']]),
                        Renderers=[ref(ids[r['key']]) for r in part_renderers], SeparatorSlots=entry['separatorSlots'],
                        CopyPropertyBlock=fields['copyPropertyBlock'], CopyMeshRendererFlags=fields['copyMeshRendererFlags'],
                        OptimizeRender=fields['OptimizeRender'])
                elif replacement == 'ResetMaterialPropertyBlock':
                    base['mRenderer'] = relocate(fields['mRenderer'], record, '.mRenderer')
                else: base['SourceKey'] = record['key']
                class_id = 114; kind = 'MonoBehaviour'
            documents.append('--- !u!' + str(class_id) + ' &' + str(ids[record['key']]) + '\n' +
                dump({kind: base}))
        path = DEST / sha(entry['renderer'].encode())[:24] / 'Tree.prefab'
        write(path, ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n' + ''.join(documents)).encode())
        meta(path.parent, folder=True)
        meta(path, 'PrefabImporter'); outputs.append(path)
        prefab_records.append(dict(renderer=entry['renderer'], path=path.relative_to(ROOT).as_posix(), objects=len(selected), gameObjects=len(entry['nodes'])))
    dependencies = [source_path, separator_path, proof_path, original, Path(__file__).resolve()]
    folders = [OFFICIAL, DEST, ART] + [DEST / sha(e['renderer'].encode())[:24] for e in recipe['separators']]
    all_outputs = sorted(set(outputs + [Path(str(p) + '.meta') for p in outputs] + [Path(str(p) + '.meta') for p in folders]))
    proof = dict(manifest=hierarchy['manifest'], mapClassId=13,
        scope='Original tree renderer sub-prefabs only; enclosing entity gameplay, sprites and interactions remain outside these assets.',
        method='Original native fields and component order; local PPtr/GUID relocation; independent geometry adapters replace Spine rendering components.',
        prefabs=prefab_records, sprite_sources=sprite_inputs, original_script_metadata=script_metadata,
        inputs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p.read_bytes())) for p in dependencies],
        outputs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p.read_bytes())) for p in all_outputs])
    write(RECEIPT, encode(proof)); print(json.dumps(prefab_records))


if __name__ == '__main__': build()
