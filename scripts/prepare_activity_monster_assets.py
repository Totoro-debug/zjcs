#!/usr/bin/env python3
"""Recover original activity MonsterIcon skeletons and High texture dependencies."""
import io
import json
import struct
import argparse
from pathlib import Path

from official_apk import ROOT
from official_manifest import read_asset_manifest
from prepare_current_partner_native import PartnerNativeRecovery, identity, pointer, digest
from prepare_character_creation_native_data import read_aligned_string
from prepare_native_character_geometry import atlas_regions
from prepare_partner_native_battle import package
from convert_official_skeletons import reader_type, WHEEL_SHA256
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    recovery = PartnerNativeRecovery()
    apk = recovery.apk
    if apk.version != '884_156692': raise ValueError('Review the activity monster source version')
    table = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))
    sources, outputs = {}, {}
    destination = work / 'detail-actor-data'
    destination.mkdir(exist_ok=True)
    reader_class, binary_input = reader_type()

    def by_id(key):
        cab, ident = key.rsplit(':', 1)
        return recovery.apk.env.get_cab(cab).objects[int(ident)]

    def record(obj):
        sources[identity(obj)] = dict(source=apk.provenance(obj), payload_sha256=digest(obj.get_raw_data()))
        return obj

    def at(owner, offset):
        file_id, path_id = struct.unpack_from('<iq', owner.get_raw_data(), offset)
        return record(pointer(dict(m_FileID=file_id, m_PathID=path_id), owner))

    def write(relative, raw):
        path = destination / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)
        outputs[relative] = digest(raw)
        return relative

    def text(obj):
        return record(obj).read(check_read=False).m_Script.encode('utf8', 'surrogateescape')

    def document(relative, data):
        return write(relative, json.dumps(data, ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode('utf8'))

    def location(path):
        rows = [row for row in table if row['path'] == path]
        if len(rows) != 1:
            raise ValueError('Original detail resource is ambiguous: ' + path)
        return record(apk.asset(rows[0]['bundle'], rows[0]['path']))

    def recover_skeleton(npc_id, name, skeleton_asset, split_asset=None, resolution=None, npc_ids=None):
        raw = record(skeleton_asset).get_raw_data()
        _, offset = read_aligned_string(raw, 28)
        if struct.unpack_from('<i', raw, offset)[0] != 1:
            raise ValueError('Review original detail atlas count: ' + name)
        atlas_asset = at(skeleton_asset, offset + 4)
        scale = struct.unpack_from('<f', raw, offset + 16)[0]
        binary = at(skeleton_asset, offset + 20)
        binary_raw = text(binary)
        reader = reader_class()
        data = reader.convert(binary_raw)
        if reader.inp.index != len(binary_raw):
            raise ValueError('Unconsumed original detail skeleton: ' + name)
        if split_asset is not None:
            split_fields = recovery.fields(record(split_asset))
            document(name + '/split-metadata.json', split_fields)
            for row in split_fields['ListAnimationContent']:
                animation_name = row['ContentName']
                if not any(animation_name.startswith('npc_' + str(value) + '/') for value in (npc_ids or [npc_id])):
                    continue
                asset_guid = row['TextAsset']['mAssetGuid']
                locations = [value for value in table if value['guid'] == asset_guid]
                if len(locations) != 1:
                    raise ValueError('Original split animation address is ambiguous: ' + animation_name)
                location_row = locations[0]
                animation_obj = record(apk.asset(location_row['bundle'], location_row['path']))
                animation_raw = text(animation_obj)
                reader.inp = binary_input(animation_raw)
                reader.inp.strings = split_fields['StringArray']
                actual_name = reader.inp.read_string()
                if actual_name != animation_name:
                    raise ValueError('Original split animation name differs')
                data['animations'][animation_name] = reader._read_animation()
                if reader.inp.index != len(animation_raw):
                    raise ValueError('Unconsumed original split animation: ' + animation_name)
                write(name + '/split/' + digest(animation_raw) + '.bytes', animation_raw)
        atlas_raw = atlas_asset.get_raw_data()
        _, atlas_offset = read_aligned_string(atlas_raw, 28)
        atlas_bytes = text(at(atlas_asset, atlas_offset + 16))
        regions = atlas_regions(atlas_bytes.decode('utf8'))
        mappings, loader_info = {}, None
        if resolution is not None:
            config_row, = [row for row in table if row['path'].endswith('/AppearanceConfig.asset')]
            config = recovery.fields(record(apk.asset(config_row['bundle'], config_row['path'])))
            skeleton_name = skeleton_asset.read(check_read=False).m_Name.removesuffix('_SkeletonData')
            base_path, = [row['Path'] for row in config['mAppearanceSkeletonPaths'] if row['SkeletonName'] == skeleton_name]
            loader_path = base_path + '/' + skeleton_name + '_TextureLoader_' + {1: 'Low', 2: 'High'}[resolution] + '.asset'
            loader_obj = location(loader_path)
            loader = recovery.fields(loader_obj)
            if loader['LevelOfResolution'] != resolution:
                raise ValueError('Original UI texture resolution differs')
            loader_info = dict(path=loader_path, fields=document(name + '/texture-loader.json', loader))
            for row in loader['placeholderMap']:
                placeholder = record(pointer(row['placeholderTexture'], loader_obj))
                key = identity(placeholder)
                if key in mappings: raise ValueError('Duplicate original UI texture placeholder')
                mappings[key] = row
        material_count = struct.unpack_from('<i', atlas_raw, atlas_offset + 28)[0]
        materials = []
        for index in range(material_count):
            obj = at(atlas_asset, atlas_offset + 32 + index * 12)
            value = obj.read()
            textures, runtime_textures = [], []
            for property_name, environment in value.m_SavedProperties.m_TexEnvs:
                if not environment.m_Texture.path_id:
                    continue
                target = record(environment.m_Texture.deref())
                texture = target.read()
                pixels = texture.image
                buffer = io.BytesIO()
                pixels.save(buffer, format='PNG')
                textures.append(dict(property=property_name, original=identity(target), name=texture.m_Name,
                    width=texture.m_Width, height=texture.m_Height, rgba_sha256=digest(pixels.convert('RGBA').tobytes()),
                    fields={key: getattr(texture.m_TextureSettings, key) for key in (
                        'm_FilterMode', 'm_Aniso', 'm_MipBias', 'm_WrapU', 'm_WrapV', 'm_WrapW')},
                    output=write('textures/' + digest(buffer.getvalue()) + '.png', buffer.getvalue())))
                if property_name == '_MainTex' and resolution is not None:
                    if identity(target) not in mappings: raise ValueError('Original UI atlas placeholder is unmapped')
                    mapping = mappings[identity(target)]
                    for field, runtime_property in (('targetTextureReference', '_MainTex'), ('dyeTextureReference', '_DyeTex')):
                        asset_guid = mapping[field]['AssetReference']['mAssetGuid']
                        if not asset_guid: continue
                        asset_row, = [row for row in table if row['guid'] == asset_guid]
                        high_obj = record(apk.asset(asset_row['bundle'], asset_row['path']))
                        high = high_obj.read()
                        if high.m_MipCount != 1: raise ValueError('Restore every original UI texture mip: ' + asset_row['path'])
                        high_pixels = high.image.convert('RGBA')
                        high_buffer = io.BytesIO(); high_pixels.save(high_buffer, format='PNG')
                        runtime_textures.append(dict(property=runtime_property, original=identity(high_obj),
                            placeholder=identity(target), guid=asset_guid, path=asset_row['path'], name=high.m_Name,
                            width=high.m_Width, height=high.m_Height, mip_count=high.m_MipCount, color_space=high.m_ColorSpace,
                            rgba_sha256=digest(high_pixels.tobytes()), encoded_sha256=digest(high.get_image_data()),
                            fields={key: getattr(high.m_TextureSettings, key) for key in (
                                'm_FilterMode', 'm_Aniso', 'm_MipBias', 'm_WrapU', 'm_WrapV', 'm_WrapW')},
                            output=write('runtime-textures/' + digest(high_buffer.getvalue()) + '.png', high_buffer.getvalue())))
            materials.append(dict(original=identity(obj), fields=obj.read_typetree(), textures=textures, runtimeTextures=runtime_textures))
        prefix = name + '/'
        native_package = package(npc_id, scale,
            json.dumps(data, ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode('utf8'),
            json.dumps(regions, ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode('utf8'))
        result = dict(npcId=npc_id, source=identity(skeleton_asset), scale=scale, textureLoader=loader_info,
            npcIds=npc_ids or [npc_id], splitSource=identity(split_asset) if split_asset is not None else None,
            metadata=document(prefix + 'metadata.json', recovery.fields(skeleton_asset)),
            skeleton=document(prefix + 'skeleton.json', data),
            binary=write(prefix + 'original.skel.bytes', binary_raw),
            atlas=write(prefix + 'original.atlas.txt', atlas_bytes),
            regions=document(prefix + 'regions.json', regions),
            materials=document(prefix + 'materials.json', materials),
            package=write(prefix + 'native.bytes', b'ZJCSPNUI' + native_package[8:]),
            counts={key: len(data.get(key, [])) for key in ('bones', 'slots', 'skins', 'animations', 'ik', 'transform', 'path', 'physics')},
            animationNames=list(data['animations']))
        print(name, result['counts'], flush=True)
        return result

    rows, templates, skeleton_paths = [], [], []
    try:
        for icon_id in (3703, 3704, 3705, 3707, 3708, 3709):
            name = f'MonsterIcon_{icon_id}'
            path = f'Assets/Prefabs/Appearance/Monster/{icon_id}/{name}.prefab'
            original = location(path)
            bundle = apk.provenance(original)['bundle']
            recovery.recover(name, work / 'native-monsters', bundle=bundle,
                native_relative=path.removeprefix('Assets/'))
            model = recovery.outputs[-1][2]
            graphic, = [row for row in model['scripts'] if row['type'] == 'SkeletonGraphic']
            skeleton = record(pointer(recovery.fields(by_id(graphic['original']))['skeletonDataAsset'], by_id(graphic['original'])))
            appearance, = [row for row in model['scripts'] if row['type'] == 'AppearanceUILite']
            appearance_fields = recovery.fields(by_id(appearance['original']))
            if appearance_fields['LevelOfResolution'] != 2: raise ValueError('Review original monster texture resolution')
            rows.append(recover_skeleton(icon_id, name, skeleton, resolution=2))
            templates.append(dict(npcId=icon_id, name=name, path=path, bundle=bundle))
        recovery.finalize()
        proof = dict(manifest=apk.version, sources=sources, outputs=outputs, actors=rows,
            unresolved_models=[], live_templates=[], model_templates=templates,
            inputs={str(p.relative_to(ROOT)).replace('\\','/'):digest(p.read_bytes()) for p in (
                Path(__file__), ROOT / 'scripts/prepare_partner_detail_actor_data.py',
                ROOT / 'scripts/prepare_current_partner_native.py', ROOT / 'scripts/convert_official_skeletons.py',
                ROOT / 'scripts/prepare_native_character_geometry.py', ROOT / 'scripts/prepare_partner_native_battle.py')},
            decoder=dict(wheel_sha256=WHEEL_SHA256),
            scope='Six original activity monster icon skeletons and original High texture mappings. Native binding and Play Mode pending.')
        (work / 'partner-detail-actor-data-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
        print('Original activity monster data recovered:',len(rows),flush=True)
    finally:
        apk.archive.close()


if __name__ == '__main__': main()
