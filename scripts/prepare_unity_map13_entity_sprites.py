"""Convert original native Sprite/Atlas packages to Windows texture payloads.

Sprite and SpriteAtlas objects stay byte-identical. Source type trees are made
explicit for the 2022.3.57f1 -> 2022.3.57f1c2 boundary. Only Texture2D encoding,
native target platform and additional exact-identity export aliases change.
"""
from __future__ import annotations
import collections
import copy
import gc
import hashlib
import json
from pathlib import Path
import struct

from official_apk import Apk, ROOT
import UnityPy
from UnityPy.enums import BuildTarget
from UnityPy.helpers.Tpk import get_typetree_node
from prepare_unity_map13_entity_textures import decode
from prepare_unity_map13_prop_hierarchies import encode, key, sha, write
from prepare_unity_map13_prop_parts import meta, dump

SOURCE = ROOT / 'unity/Assets/Official/World/Map/13/Source/native-entity-object-graph.json'
SOURCE_PROOF = ROOT / 'assets/reference/official-map/map13-native-entity-object-graph-source-manifest.json'
DEST = ROOT / 'unity/Assets/Official/World/Map/13/NativeEntitySprites'
CATALOG = ROOT / 'unity/Assets/Game/Resources/Map13/Native/entity-sprite-catalog.asset'
RUNTIME = ROOT / 'unity/Assets/Game/Runtime/Offline/World/NativeMap13EntityAssets'
RECEIPT = ROOT / 'assets/reference/official-map/map13-native-entity-sprites-source-manifest.json'
ALLOWED = {'AssetBundle', 'Sprite', 'SpriteAtlas', 'Texture2D'}


def asset_key(identity): return hashlib.sha256(identity.encode()).hexdigest()[:24]
def alias(identity): return '__zjcs/map13/native/' + asset_key(identity)


def decode_sprite_texture(texture):
    if texture.m_TextureFormat != 3: return decode(texture)
    # The original RGB24 images have no alpha channel. Expand to the native
    # Windows RGBA byte layout without resampling or changing any RGB value.
    original=texture.get_image_data();data=bytearray();mips=[];offset=0
    for level in range(texture.m_MipCount):
        width=max(1,texture.m_Width>>level);height=max(1,texture.m_Height>>level);size=width*height*3
        block=original[offset:offset+size]
        if len(block)!=size:raise ValueError('Truncated original RGB24 mip')
        rgba=bytearray(width*height*4);rgba[0::4]=block[0::3];rgba[1::4]=block[1::3];rgba[2::4]=block[2::3];rgba[3::4]=bytes([255])*(width*height)
        mips.append(dict(level=level,width=width,height=height,source_offset=offset,source_size=size,source_sha256=sha(block),rgba_offset=len(data),rgba_size=len(rgba),rgba_sha256=sha(rgba)))
        data.extend(rgba);offset+=size
    if offset!=len(original):raise ValueError('Original RGB24 mip bytes remain unconsumed')
    return bytes(data),mips,'OriginalRGB24OpaqueAlphaExpansion',4


def sprite_geometry(obj):
    fields = obj.read_typetree(check_read=True)
    rd = fields['m_RD']; data = rd['m_VertexData']; channels = data['m_Channels']
    if channels[0] != dict(stream=0, offset=0, format=0, dimension=3) or any(c['stream'] == 0 and c['dimension'] for c in channels[1:]):
        raise ValueError('Additional original Sprite vertex layout needs migration: ' + fields['m_Name'])
    raw = bytes(data['m_DataSize'])
    vertices = [dict(x=struct.unpack_from('<3f', raw, i * 12)[0], y=struct.unpack_from('<3f', raw, i * 12)[1]) for i in range(data['m_VertexCount'])]
    triangles = list(struct.unpack('<' + 'H' * (len(rd['m_IndexBuffer']) // 2), bytes(rd['m_IndexBuffer'])))
    return dict(name=fields['m_Name'], vertices=vertices, triangles=triangles, ppu=fields['m_PixelsToUnits'], rect=fields['m_Rect'], pivot=fields['m_Pivot'],
                vertices_sha256=sha(raw), indices_sha256=sha(bytes(rd['m_IndexBuffer'])))


def build():
    tool_hash=sha(Path(__file__).read_bytes())
    graph = json.loads(SOURCE.read_bytes()); proof = json.loads(SOURCE_PROOF.read_bytes()); apk = Apk.current()
    expected, = [o for o in proof['outputs'] if o['path'] == SOURCE.relative_to(ROOT).as_posix()]
    if sha(SOURCE.read_bytes()) != expected['sha256'] or graph['manifest'] != apk.version:
        raise ValueError('Selected original sprite object graph changed')
    indexed = {key(o['cab'], o['path_id']): o for o in graph['objects']}
    bindings, wanted = [], set()
    material_path=SOURCE.with_name('native-entity-material-bindings.json')
    materials={key(m['cab'],m['path_id']):m['output']['path'] for m in json.loads(material_path.read_bytes())['entries']}
    sprite_materials={}
    for owner in graph['objects']:
        if owner['type'] not in ('SpriteRenderer', 'SpriteMask'): continue
        refs = [r for r in owner['references'] if r['field'] == '.m_Sprite']
        if len(refs) > 1: raise ValueError('Original renderer has multiple Sprite references')
        sprite = key(refs[0]['cab'], refs[0]['path_id']) if refs else ''
        bindings.append(dict(sourceKey=key(owner['cab'], owner['path_id']), spriteKey=sprite,kind=owner['type']))
        if sprite and sprite not in sprite_materials:
            material_refs=[r for r in owner['references'] if r['field']=='.m_Materials[0]']
            if material_refs:sprite_materials[sprite]=materials[key(material_refs[0]['cab'],material_refs[0]['path_id'])]
        if sprite: wanted.add(sprite)
    bundles = collections.defaultdict(set)
    for identity in wanted: bundles[indexed[identity]['input']].add(identity)
    if len(bindings) != 808 or len(wanted) != 230 or len(bundles) != 62:
        raise ValueError('Original native entity sprite selection differs')
    DEST.mkdir(parents=True, exist_ok=True); meta(DEST, folder=True)
    packages, sprite_records, all_outputs, fields_checked = [], [], [], 0
    component_types = None
    for ordinal, (bundle_name, selection) in enumerate(sorted(bundles.items())):
        originals = apk.objects(bundle_name)
        files = {o.assets_file.name: o.assets_file for o in originals}
        if len(files) != 1: raise ValueError('Additional original sprite CAB closure needs explicit binding: ' + bundle_name)
        native = next(iter(files.values())); bundle = native.parent
        native_objects = list(native.objects.values())
        unknown = {o.type.name for o in native_objects} - ALLOWED
        excluded = []
        if unknown:
            if component_types is None:
                component_types = {key(o['cab'],o['path_id']):o['type'] for o in json.loads(SOURCE.with_name('native-entity-components.json').read_bytes())['components']}
            for obj in native_objects:
                if obj.type.name in ALLOWED: continue
                identity=key(native.name,obj.path_id); kind=component_types.get(identity,obj.type.name)
                excluded.append(dict(identity=identity,type=kind,native_type=obj.type.name,payload_sha256=sha(obj.get_raw_data())))
            # This is a separate image-only package, never an entity/prefab
            # replacement. Preserve the other original objects in the source
            # graph for their owning migrations. Reject any outgoing pointer
            # from a retained image to an excluded object below.
            native_objects=[o for o in native_objects if o.type.name in ALLOWED]
        elif native.externals: raise ValueError('Original sprite package has external CAB dependencies: ' + bundle_name)
        original_platform = int(native.target_platform)
        package_key = asset_key(bundle_name)
        records, textures, original_fields, expected_hashes = [], [], {}, {}
        for obj in native_objects:
            identity = key(native.name, obj.path_id); raw = obj.get_raw_data()
            recorded = indexed.get(identity)
            if recorded and sha(raw) != recorded.get('original_payload_sha256', recorded['payload_sha256']):
                raise ValueError('Original native Sprite object changed: ' + identity)
            original_fields[identity] = obj.read_typetree(check_read=True)
            records.append(dict(identity=identity, type=obj.type.name, original_payload_sha256=sha(raw)))
            expected_hashes[identity] = sha(raw)
            if identity in selection:
                if obj.type.name != 'Sprite': raise ValueError('Renderer source is not a native Sprite')
                if identity not in sprite_materials:raise ValueError('Original native sprite renderer material is not bound: '+identity)
                sprite_records.append(dict(key=identity, package=package_key, assetName=alias(identity), originalName=original_fields[identity]['m_Name'],material=sprite_materials[identity], **sprite_geometry(obj)))
        for obj in native_objects:
            identity = key(native.name, obj.path_id)
            if obj.type.name != 'Texture2D': continue
            texture = obj.read(check_read=True); data, mips, profile, target_format = decode_sprite_texture(texture)
            updated = copy.deepcopy(original_fields[identity]); updated['m_TextureFormat'] = target_format
            updated['m_CompleteImageSize'] = len(data); updated['image data'] = data
            updated['m_StreamData'] = dict(offset=0, size=0, path=''); updated['m_PlatformBlob'] = b''
            expected_hashes[identity] = sha(obj.save_typetree(updated))
            textures.append(dict(identity=identity, assetName=alias(identity), name=texture.m_Name, width=texture.m_Width, height=texture.m_Height,
                                 original_format=texture.m_TextureFormat, native_format=target_format, profile=profile, mips=mips, rgba_sha256=sha(data)))
        # Additional lookup names expose exact CAB/PID identities, including
        # dependencies which were not original addressable roots. Original
        # container/preload entries retain their values and order.
        asset_bundle, = [o for o in native_objects if o.type.name == 'AssetBundle']
        identity = key(native.name, asset_bundle.path_id); body = copy.deepcopy(original_fields[identity])
        if excluded:
            kept_ids={o.path_id for o in native_objects}
            body['m_PreloadTable']=[]
            body['m_Container']=[row for row in body['m_Container'] if row[1]['asset']['m_FileID']==0 and row[1]['asset']['m_PathID'] in kept_ids]
            if body['m_MainAsset']['asset']['m_PathID']:
                raise ValueError('Image-only package has an additional nonempty main asset')
            body['m_Dependencies']=[]
        first = len(body['m_PreloadTable'])
        payload = [o for o in native_objects if o.type.name != 'AssetBundle']
        body['m_PreloadTable'].extend(dict(m_FileID=0, m_PathID=o.path_id) for o in payload)
        if excluded:
            for _,entry in body['m_Container']:entry['preloadIndex']=first;entry['preloadSize']=len(payload)
        for obj in payload:
            name = alias(key(native.name, obj.path_id))
            if any(row[0] == name for row in body['m_Container']): raise ValueError('Native identity export alias collision')
            body['m_Container'].append((name, dict(preloadIndex=first, preloadSize=len(payload), asset=dict(m_FileID=0, m_PathID=obj.path_id))))
        expected_hashes[identity] = sha(asset_bundle.save_typetree(body))
        if excluded:
            def pointers(value):
                if isinstance(value,dict):
                    if set(value)=={'m_FileID','m_PathID'}:yield value
                    else:
                        for item in value.values():yield from pointers(item)
                elif isinstance(value,(list,tuple)):
                    for item in value:yield from pointers(item)
            for obj in payload:
                if any(p['m_PathID'] and (p['m_FileID'] or p['m_PathID'] not in kept_ids) for p in pointers(original_fields[key(native.name,obj.path_id)])):
                    raise ValueError('Image-only native closure has an external pointer')
            native.externals=[];old_types=list(native.types);types=[];type_ids={}
            for obj in native_objects:
                previous=obj.type_id
                if previous not in type_ids:type_ids[previous]=len(types);types.append(old_types[previous])
                obj.type_id=type_ids[previous]
            native.types=types;native.ref_types=[];native.objects={o.path_id:o for o in native_objects}
        native.target_platform = BuildTarget.StandaloneWindows64; native._m_target_platform = 19
        native._enable_type_tree = True
        for typ in native.types:
            if typ.node is None: typ.node = get_typetree_node(typ.class_id, native.version)
            if typ.type_dependencies is None: typ.type_dependencies = ()
            for index, node in enumerate(typ.node.traverse()):
                node.m_Index = index
                for field in ('m_TypeFlags', 'm_RefTypeHash', 'm_MetaFlag'): setattr(node, field, getattr(node, field) or 0)
                node.m_Version = node.m_Version or 1
        bundle.files = {native.name: native}  # Texture streams have been embedded.
        converted = bundle.save(packer='lz4'); loaded = UnityPy.load(converted)
        if len(loaded.objects) != len(records): raise ValueError('Native sprite object count changed')
        for obj in loaded.objects:
            identity = key(obj.assets_file.name, obj.path_id)
            if int(obj.assets_file.target_platform) != 19 or sha(obj.get_raw_data()) != expected_hashes[identity]:
                raise ValueError('Native Windows sprite object payload changed: ' + identity)
            actual = obj.read_typetree(check_read=True); original = original_fields[identity]
            if obj.type.name in ('Sprite', 'SpriteAtlas'):
                if actual != original: raise ValueError('Original Sprite/Atlas fields changed')
                fields_checked += 1
            elif obj.type.name == 'Texture2D':
                for field in ('m_TextureFormat', 'm_CompleteImageSize', 'image data', 'm_StreamData', 'm_PlatformBlob'): actual.pop(field); original = {k:v for k,v in original.items() if k != field}
                if actual != original: raise ValueError('Original native texture sampler/metadata fields changed')
                spec = next(t for t in textures if t['identity'] == identity)
                if sha(obj.read(check_read=True).get_image_data()) != spec['rgba_sha256']: raise ValueError('Native texture mip bytes changed')
            elif obj.type.name == 'AssetBundle':
                if excluded:
                    if actual!=body:raise ValueError('Image-only package closure changed')
                    continue
                if actual['m_PreloadTable'][:first] != original['m_PreloadTable'] or actual['m_Container'][:len(original['m_Container'])] != original['m_Container']:
                    raise ValueError('Original native container/preload entries changed')
                for field in original:
                    if field not in ('m_PreloadTable','m_Container') and actual[field] != original[field]: raise ValueError('Original bundle field changed: '+field)
        path = DEST / (package_key + '.windows.bundle.bytes'); write(path, converted); meta(path, 'TextScriptImporter')
        all_outputs.extend([path, Path(str(path) + '.meta')])
        packages.append(dict(key=package_key, bundle=bundle_name, source=apk.provenance(asset_bundle),
            original_platform=original_platform, target_platform=19, source_unity_version=native.unity_version,
            explicit_type_tree=True, native_objects=records, textures=textures,
            separate_nonimage_assets=excluded,
            output=dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(converted))))
        print(f'Native Sprite package {ordinal+1}/{len(bundles)}: {len(records)} objects, {len(textures)} textures, {len(converted)} bytes', flush=True)
        del originals, native_objects, native, bundle, loaded, original_fields, converted
        gc.collect()
    catalog_script = RUNTIME / 'NativeMap13EntitySpriteCatalog.cs'; script_guid = meta(catalog_script, 'MonoImporter')
    for name in ('NativeMap13EntitySpriteLibrary', 'NativeMap13EntitySpriteBinding'): meta(RUNTIME / (name + '.cs'), 'MonoImporter')
    fields = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),m_PrefabAsset=dict(fileID=0),m_GameObject=dict(fileID=0),m_Enabled=1,m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000,guid=script_guid,type=3),m_Name=CATALOG.stem,m_EditorClassIdentifier='',manifest=apk.version,
        packages=[dict(key=p['key'],sha256=p['output']['sha256'],data=dict(fileID=4900000,guid=meta(ROOT/p['output']['path']),type=3)) for p in packages],
        sprites=[{k:s[k] for k in ('key','package','assetName','originalName')} for s in sorted(sprite_records,key=lambda s:s['key'])],renderers=sorted(bindings,key=lambda b:b['sourceKey']))
    write(CATALOG,('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n'+dump(dict(MonoBehaviour=fields))).encode());meta(CATALOG,'NativeFormatImporter')
    all_outputs.extend([CATALOG,Path(str(CATALOG)+'.meta'),Path(str(DEST)+'.meta')])
    for name in ('NativeMap13EntitySpriteCatalog','NativeMap13EntitySpriteLibrary','NativeMap13EntitySpriteBinding'):
        p=RUNTIME/(name+'.cs');all_outputs.extend([p,Path(str(p)+'.meta')])
    inputs=[SOURCE,SOURCE_PROOF,material_path,SOURCE.with_name('native-entity-components.json'),Path(__file__).resolve(),ROOT/'scripts/prepare_unity_map13_entity_textures.py',ROOT/'scripts/prepare_unity_map13_prop_parts.py']
    if sha(Path(__file__).read_bytes())!=tool_hash:raise ValueError('Sprite converter changed during execution; rerun before recording provenance')
    write(RECEIPT,encode(dict(manifest=apk.version,mapClassId=13,
        method='Original Sprite/Atlas bytes and hierarchy reference identities preserved. Windows native texture encoding, embedded source type trees and additional CAB/PID lookup aliases. Original image roots/container order retained in image packages. Mixed source packages produce a separate image-only closure: every excluded nonimage object is recorded and remains part of the source graph for its owning terrain/entity/effect migration; no entity replacement is produced. Native image pointers must all remain internal and resolve to retained image objects. Source Unity version remains unchanged; compatibility must be proven by real Windows GPU execution.',
        renderer_bindings=len(bindings),original_null_bindings=sum(not b['spriteKey'] for b in bindings),sprites=sprite_records,packages=packages,unchanged_sprite_atlas_objects=fields_checked,
        inputs=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p.read_bytes())) for p in inputs],outputs=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p.read_bytes())) for p in sorted(set(all_outputs))],
        coverage='Native sprite resource library and explicit renderer source bindings. Full entity prefabs, component lifecycle, map inputs/saves and Windows Player remain pending.')))
    print('Original native entity sprites prepared:',len(packages),'packages,',len(sprite_records),'sprites,',len(bindings),'renderer bindings.',flush=True)


if __name__=='__main__':build()
