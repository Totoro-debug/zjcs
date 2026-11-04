"""Restore every selected original weapon High texture and its native bindings.

The complete loader/atlas is read from the APK first, then the matching source
cache. Existing native placeholders/programs are reused only by source hash.
"""
import copy
import hashlib
import json
import re
from pathlib import Path

import astc_encoder
import yaml
from UnityPy.classes import PPtr
from UnityPy.enums import TextureFormat
from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from current_component_schemas import CurrentSchemas
from prepare_battle_scene_appearance_runtime import identity, write_changed
from prepare_unity_profession_character_resources import asset, guid, meta
from prepare_unity_profession_reward_sprites import Dumper, fields, source_header
from prepare_unity_map13_native_scene import decode_mips
from validation_workspace import validation_directory


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    work = validation_directory()
    assert (work / '.zjcs-validation.json').is_file()
    apk, schemas = Apk.current(), CurrentSchemas()
    assert apk.version == '884_156692'
    table_raw = apk.source.manifest_bytes('DefaultPackage', apk.archive)
    rows = read_asset_manifest(table_raw)
    by_guid = {row['guid']: row for row in rows if row['guid']}
    table = {row['path']: row for row in rows}
    prior_path = ROOT / 'assets/reference/profession-ui/weapon-texture-loader-source-manifest.json'
    prior = json.loads(prior_path.read_bytes())
    prior_sources = {(r['source']['bundle'], r['source']['path_id']): r for r in prior['sources']}
    old_textures = {prior_sources[r['source']['bundle'], r['source']['path_id']]['payload_sha256']: r for r in prior['textures']}
    old_shaders = {r['payload_sha256']: r for r in prior['shaders']}
    shader_receipt = ROOT / '.tools/source-packages/current/884_156692/native-conversion/all-assets/windows-shaders/source-manifest.json'
    shader_cache = {r['source']['object_sha256']: r for r in json.loads(shader_receipt.read_bytes())['shaders']}
    destination = ROOT / 'unity/Assets/Official/Combat/WeaponTextures'
    payloads = destination / 'Resources/Combat/WeaponHighPayloads'
    shaders = ROOT / 'unity/Assets/Game/Runtime/Official/CombatWeaponShaders'
    outputs, sources, reused = [], {}, []
    texture_records, material_records, shader_records = [], [], []
    for directory in (destination, destination / 'Bindings', destination / 'Resources',
                      destination / 'Resources/Combat', payloads,
                      destination / 'Resources/Combat/Weapons'):
        directory.mkdir(parents=True, exist_ok=True)
        meta(directory, folder=True); outputs.append(Path(str(directory) + '.meta'))

    def source(obj):
        digest = sha(obj.get_raw_data())
        key = obj.assets_file.name + ':' + str(obj.path_id)
        row = dict(source=apk.provenance(obj), payload_sha256=digest)
        if key in sources: assert sources[key] == row
        else: sources[key] = row
        return digest

    def pointer(obj, value):
        return PPtr(**value, assetsfile=obj.assets_file).deref() if value['m_PathID'] else None

    def checked(row):
        path = ROOT / row['path']
        raw = path.read_bytes()
        actual = identity(path)
        if actual != row:
            assert sha(raw.replace(b'\r\n', b'\n')) == row['sha256'], row['path']
            actual.update(prior_expected_sha256=row['sha256'], adaptation='Only CRLF checkout encoding differs; all LF-normalized bytes match.')
        reused.append(actual)
        sidecar = Path(str(path) + '.meta')
        reused.append(identity(sidecar))
        return path

    def reference(path, file_id):
        return dict(fileID=file_id, guid=guid(path), type=2)

    def native(path, kind, file_id, value):
        binary = value.get('_typelessdata')
        serial = dict(value, _typelessdata='ZJCS_TEXTURE_BYTES') if binary is not None else value
        text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!' + str(kind) + ' &' + str(file_id) + '\n'
        text += yaml.dump({{21:'Material', 28:'Texture2D'}[kind]: serial}, Dumper=Dumper,
                          sort_keys=False, allow_unicode=True, width=1000000)
        if binary is not None: text = text.replace('_typelessdata: ZJCS_TEXTURE_BYTES', '_typelessdata: ' + binary)
        write_changed(path, text); meta(path)
        write_changed(Path(str(path) + '.meta'), 'fileFormatVersion: 2\nguid: ' + guid(path)
            + '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: ' + str(file_id) + '\n')
        outputs.extend((path, Path(str(path) + '.meta')))
        return reference(path, file_id)

    texture_cache = {}
    def texture(obj, high=False):
        digest = source(obj)
        key = (digest, high)
        if key in texture_cache: return texture_cache[key]
        data = obj.read(); original = obj.read_typetree(); original.pop('image data')
        encoded = data.get_image_data()
        old = old_textures.get(digest)
        if not high:
            assert old is not None and old['encoded_sha256'] == sha(encoded), 'Restore changed current weapon placeholder explicitly'
            path = checked(old['output'])
            row = dict(binding=reference(path, 2800000), output=identity(path))
        else:
            assert not data.m_MipsStripped and data.m_ImageCount == 1 and data.m_TextureDimension == 2
            if 'ASTC' in TextureFormat(data.m_TextureFormat).name:
                pixels, mips, profile = decode_mips(data); fmt = 4
            else:
                fmt, pixels, profile = int(data.m_TextureFormat), encoded, 'original-uncompressed'
                assert fmt in (1, 4), (data.m_Name, fmt)
                mips, offset = [], 0
                for level in range(data.m_MipCount):
                    width, height = max(1, data.m_Width >> level), max(1, data.m_Height >> level)
                    count = width * height * (1 if fmt == 1 else 4)
                    mips.append(dict(level=level, width=width, height=height, rgba_offset=offset,
                                     rgba_size=count, rgba_sha256=sha(pixels[offset:offset+count])))
                    offset += count
                assert offset == len(pixels)
            path = payloads / (digest + '.asset')
            value = dict(source_header(), **fields(copy.deepcopy(original)))
            value.update(serializedVersion=2, m_TextureFormat=fmt, m_CompleteImageSize=len(pixels),
                         m_PlatformBlob='', m_StreamData=dict(serializedVersion=2, offset=0, size=0, path=''))
            value['m_TextureSettings']['serializedVersion'] = 2
            value['image data'], value['_typelessdata'] = len(pixels), pixels.hex()
            binding = native(path, 28, 2800000, value)
            row = dict(source=apk.provenance(obj), payload_sha256=digest, original_fields=original,
                encoded_sha256=sha(encoded), target_format=fmt, profile=profile, mips=mips,
                pixels_sha256=sha(pixels), binding=binding, output=identity(path),
                resource='Combat/WeaponHighPayloads/' + digest)
            texture_records.append(row)
        texture_cache[key] = row
        return row

    shader_bindings = {}
    def shader(obj):
        digest = source(obj)
        if digest in shader_bindings: return shader_bindings[digest]
        original = obj.read(); name = original.m_ParsedForm.m_Name
        old = old_shaders.get(digest)
        if old:
            path = checked(old['output'])
            row = dict(source=apk.provenance(obj), payload_sha256=digest, original_name=name, output=identity(path), reused=True)
        else:
            cached = shader_cache[digest]; path_cache = shader_receipt.parent / cached['path']
            assert sha(path_cache.read_bytes()) == cached['sha256']
            text = path_cache.read_text(encoding='utf-8')
            assert text.startswith('Shader "' + name + '"')
            # The verified cache already contains the complete Windows programs
            # and original render state. Only the asset name is namespaced.
            native_name = 'Zjcs/Official/Combat/Weapons/' + digest
            text = text.replace('Shader "' + name + '"', 'Shader "' + native_name + '"', 1)
            fallback = original.m_ParsedForm.m_FallbackName
            if fallback:
                candidates = [r.deref() for r in original.m_Dependencies if r.m_PathID]
                candidates = [r for r in candidates if r.read().m_ParsedForm.m_Name == fallback]
                assert len(candidates) == 1, (name, fallback)
                dependency = shader(candidates[0]); fallback_path = next(ROOT / r['output']['path'] for r in shader_records
                    if guid(ROOT / r['output']['path']) == dependency['guid'])
                fallback_name = re.search(r'^Shader "([^"]+)"', fallback_path.read_text(encoding='utf-8'))[1]
                text, count = re.subn(r'(?m)^Fallback (?:Off|"[^"]*")$', 'Fallback "' + fallback_name + '"', text)
                assert count == 1, name
            shaders.mkdir(parents=True, exist_ok=True); meta(shaders, folder=True)
            if Path(str(shaders) + '.meta') not in outputs: outputs.append(Path(str(shaders) + '.meta'))
            path = shaders / (digest + '.shader'); write_changed(path, text); meta(path)
            outputs.extend((path, Path(str(path) + '.meta')))
            row = dict(source=apk.provenance(obj), payload_sha256=digest, original_name=name,
                cache=identity(path_cache), programs=cached['programs'], stripped_passes=cached['stripped_passes'],
                output=identity(path), reused=False)
        shader_records.append(row)
        shader_bindings[digest] = dict(fileID=4800000, guid=guid(path), type=3)
        return shader_bindings[digest]

    material_cache = {}
    def material(obj):
        digest = source(obj)
        if digest in material_cache: return material_cache[digest]
        original = obj.read_typetree(); value = copy.deepcopy(original)
        value['m_Shader'] = shader(pointer(obj, original['m_Shader']))
        for _, entry in value['m_SavedProperties']['m_TexEnvs']:
            target = pointer(obj, entry['m_Texture'])
            entry['m_Texture'] = texture(target)['binding'] if target is not None else dict(fileID=0)
        native_fields = dict(source_header(), **fields(value)); native_fields['serializedVersion'] = 8
        native_fields['m_SavedProperties'] = dict(serializedVersion=3, **{
            k:[{name:value} for name,value in pairs] for k,pairs in native_fields['m_SavedProperties'].items()})
        path = destination / 'Bindings' / (digest + '.mat')
        binding = native(path, 21, 2100000, native_fields)
        material_records.append(dict(source=apk.provenance(obj), payload_sha256=digest,
            original_fields=original, binding=binding, output=identity(path)))
        material_cache[digest] = binding
        return binding

    loader_path = 'Assets/AppearanceAssets/Character/pWeapon/primaryWeapon_TextureLoader_High.asset'
    original_loader = apk.asset(table[loader_path]['bundle'], loader_path)
    source(original_loader); loader = schemas.read(original_loader)
    assert loader['LevelOfResolution'] == 2 and loader['skeletonDataAsset']['m_PathID'] == 0
    original_atlas = pointer(original_loader, loader['atlasAsset'])
    source(original_atlas); atlas = schemas.read(original_atlas)
    assert len(loader['placeholderMap']) == len(atlas['materials']) == 398
    addresses, mappings, materials, mapping_rows = {}, [], [], []
    for index, (mapping, matref) in enumerate(zip(loader['placeholderMap'], atlas['materials'])):
        placeholder = pointer(original_loader, mapping['placeholderTexture'])
        original_material = pointer(original_atlas, matref)
        mat_fields = original_material.read_typetree()
        main = next(v['m_Texture'] for k,v in mat_fields['m_SavedProperties']['m_TexEnvs'] if k == '_MainTex')
        assert source(pointer(original_material, main)) == source(placeholder), index
        converted = copy.deepcopy(mapping)
        converted['placeholderTexture'] = texture(placeholder)['binding']
        mappings.append(converted); materials.append(material(original_material))
        for field in ('targetTextureReference', 'dyeTextureReference'):
            asset_guid = mapping[field]['AssetReference']['mAssetGuid']
            if asset_guid and asset_guid not in addresses:
                entry = by_guid[asset_guid]
                row = texture(apk.asset(entry['bundle'], entry['path']), high=True)
                addresses[asset_guid] = row['resource']
        mapping_rows.append(dict(index=index, placeholder=placeholder.read().m_Name,
            main=mapping['targetTextureReference']['AssetReference']['mAssetGuid'],
            dye=mapping['dyeTextureReference']['AssetReference']['mAssetGuid']))
        if (index + 1) % 10 == 0: print('Restored current weapon High mappings', index + 1, '/ 398', flush=True)
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI'
    atlas_path = 'Assets/AppearanceAssets/Character/pWeapon/primaryWeapon_Atlas.asset'
    metadata = destination / 'Bindings/atlas-original.json'
    write_changed(metadata, json.dumps(atlas, ensure_ascii=False, separators=(',', ':')) + '\n'); meta(metadata)
    outputs.extend((metadata, Path(str(metadata) + '.meta')))
    native_atlas = destination / 'Bindings/atlas.asset'
    atlas_ref = asset(native_atlas, atlas['m_Name'], runtime / 'ProfessionNativeTextureAtlas.cs', dict(
        sourcePath=atlas_path, sourceGuid=table[atlas_path]['guid'],
        originalMetadata=dict(fileID=4900000, guid=guid(metadata), type=3), materials=materials))
    outputs.extend((native_atlas, Path(str(native_atlas) + '.meta')))
    native_loader = destination / 'Bindings/texture-loader-high.asset'
    loader_fields = {k:copy.deepcopy(loader[k]) for k in ('maxPlaceholderSize', 'unloadAfterSecondsUnused',
        'LevelOfResolution', 'PlaceholderPath', 'TargetTexturePath', 'DyeTexturePath')}
    loader_fields.update(atlasAsset=atlas_ref, skeletonDataAsset=dict(fileID=0), placeholderMap=mappings)
    loader_ref = asset(native_loader, loader['m_Name'], ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI/YooAssetTextureLoader.cs', loader_fields)
    outputs.extend((native_loader, Path(str(native_loader) + '.meta')))
    config_path = 'Assets/ScriptableConfig/Modules/Appearance/AppearanceAssetManagerConfig.asset'
    original_config = apk.asset(table[config_path]['bundle'], config_path)
    source(original_config); config = schemas.read(original_config)
    native_config = destination / 'Bindings/asset-manager-config.asset'
    config_ref = asset(native_config, config['m_Name'], ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI/AppearanceAssetManagerConfig.cs', dict(
        UIDyeShader=shader(pointer(original_config, config['UIDyeShader'])),
        DefaultSkeletonMaterial=material(pointer(original_config, config['DefaultSkeletonMaterial']))))
    outputs.extend((native_config, Path(str(native_config) + '.meta')))
    model_row, = [r for r in rows if r['path'].endswith('/primaryWeapon_SkeletonData.asset')]
    model_object = apk.asset(model_row['bundle'], model_row['path'])
    source(model_object); model_fields = schemas.read(model_object)
    model_binary = pointer(model_object, model_fields['skeletonJSON'])
    model_hash = source(model_binary)
    model_proof_path = ROOT / 'assets/reference/profession-ui/weapon-resources-source-manifest.json'
    model_proof = json.loads(model_proof_path.read_bytes())
    assert model_hash in {r['payload_sha256'] for r in model_proof['sources']}
    model_native_path = ROOT / 'unity/Assets/Official/ProfessionUI/WeaponResources/primary-weapon.bytes'
    model_expected, = [r for r in model_proof['outputs'] if r['path'] == model_native_path.relative_to(ROOT).as_posix()]
    assert identity(model_native_path) == model_expected
    skin_names = sorted({Path(r['path']).parent.name for r in rows if '/PlayerPart/pWeapon_' in r['path']
        and r['path'].endswith('_Integrated.prefab') and '_UI_' not in r['path']})
    assert len(skin_names) == 390
    catalog = destination / 'Resources/Combat/Weapons/texture-resources.asset'
    asset(catalog, 'Complete current original weapon High textures', runtime / 'ProfessionNativeWeaponTextureResources.cs', dict(
        loaderPath=loader_path, loaderGuid=table[loader_path]['guid'], loader=loader_ref, atlas=atlas_ref,
        managerConfig=config_ref, textures=[dict(Guid=g, Resource=p) for g,p in sorted(addresses.items())]))
    outputs.extend((catalog, Path(str(catalog) + '.meta')))
    validation = ROOT / 'unity/Assets/Game/Editor/Combat/AnimatorPlayback'
    validation_files = [validation / (name + '.cs') for name in
        ('OfficialBattlePlayerModelClipValidation', 'OfficialBattlePlayerModelClipValidation.HighTextures')]
    for path in validation_files: meta(path)
    code = ROOT / '.tools/source-packages/current/884_156692'
    code_receipt = code / 'code-manifest.json'; assembly = code / 'official-code/managed/decoded/Assembly-CSharp.dll'
    assert identity(assembly)['sha256'] == json.loads(code_receipt.read_bytes())['assemblies'][assembly.name]['decoded_sha256']
    proof = dict(manifest=apk.version, asset_table_sha256=sha(table_raw), code_receipt=identity(code_receipt), assembly=identity(assembly),
        loader=dict(path=loader_path, guid=table[loader_path]['guid'], source=apk.provenance(original_loader), original_fields=loader),
        model=dict(source=apk.provenance(model_binary), payload_sha256=model_hash,
            native=identity(model_native_path), prior_receipt=identity(model_proof_path)),
        atlas=dict(path=atlas_path, guid=table[atlas_path]['guid'], source=apk.provenance(original_atlas)),
        mappings=mapping_rows, texture_addresses=[dict(Guid=g, Resource=p) for g,p in sorted(addresses.items())],
        sources=list(sources.values()), textures=texture_records, materials=material_records, shaders=shader_records,
        prior_receipt=identity(prior_path), reused=list({r['path']:r for r in reused}.values()),
        current_shader_cache=identity(shader_receipt), outputs=[identity(p) for p in sorted(set(outputs))],
        generator=identity(Path(__file__)), runtime=[identity(runtime / (name + '.cs')) for name in
            ('ProfessionNativeWeaponTextureResources', 'OfflineProfessionWeaponTextureResources')],
        validation=[identity(p) for path in validation_files for p in (path, Path(str(path) + '.meta'))],
        decoder=dict(module=identity(Path(astc_encoder.__file__)),
            binaries=[identity(p) for p in sorted(Path(astc_encoder.__file__).parent.rglob('*.pyd'))]),
        helpers=[identity(ROOT / 'scripts' / name) for name in ('current_component_schemas.py',
            'prepare_unity_map13_native_scene.py', 'prepare_unity_profession_reward_sprites.py')],
        binding='Combat/Weapons/texture-resources',
        operation='Retain every selected original High mapping/GUID and complete atlas/material/config dependency. Reuse source-identical native placeholders/programs, decode all remaining High ASTC mips to RGBA32 preserving native flags and samplers, and bind each payload through an owned lazy local request. No animation SDK code or replacement art is used.',
        limits=['Complete native weapon prefabs, their particles/sprites/bone controllers, full weapon physics and production character factory remain required. This texture module does not prove those flows.'])
    write_changed(ROOT / 'assets/reference/battle-animation/weapon-high-textures-source-manifest.json',
                  json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    write_changed(work / 'weapon-high-texture-cases.json', json.dumps(dict(textures=texture_records,
        addresses=proof['texture_addresses'], mappings=mapping_rows, skins=skin_names), ensure_ascii=False, separators=(',', ':')) + '\n')
    apk.archive.close()
    print('Restored complete weapon High resources:', len(mappings), 'mappings,', len(addresses),
          'GUIDs and', len(texture_records), 'native texture payloads.', flush=True)


if __name__ == '__main__':
    main()
