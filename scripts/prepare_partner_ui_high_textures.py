"""Recover the complete selected High loader used by the original partner avatar."""
import copy
import hashlib
import json
import re
import uuid
from pathlib import Path

import yaml
import astc_encoder
from UnityPy.classes import PPtr
from UnityPy.enums import TextureFormat
from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from prepare_battle_scene_appearance_runtime import identity, write_changed
from prepare_unity_profession_character_resources import guid, meta
from prepare_unity_profession_reward_sprites import Dumper, fields, source_header
from prepare_unity_map13_native_scene import decode_mips
from validation_workspace import validation_directory


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    work = validation_directory()
    assert (work / '.zjcs-validation.json').is_file()
    apk = Apk.current()
    assert apk.version == '884_156692'
    schemas = CurrentSchemas()
    table_raw = apk.source.manifest_bytes('DefaultPackage', apk.archive)
    table = {row['guid']: row for row in read_asset_manifest(table_raw) if row['guid']}
    source_guid = '7747b1622b6dcd941ac31d869760ad8d'
    entry = table[source_guid]
    assert entry['path'].endswith('/character_model_TextureLoader_High.asset')
    original = apk.asset(entry['bundle'], entry['path'])
    original_fields = schemas.read(original)
    assert original_fields['LevelOfResolution'] == 2
    assert original_fields['atlasAsset']['m_PathID'] == original_fields['skeletonDataAsset']['m_PathID'] == 0
    scene_proof = ROOT / 'assets/reference/battle-animation/scene-texture-source-manifest.json'
    scene = json.loads(scene_proof.read_bytes())
    assert scene['manifest'] == apk.version and scene['asset_table_sha256'] == sha(table_raw)
    known = {row['payload_sha256']: row for row in scene['textures']}
    known_objects = {(row['source']['bundle_hash'], row['source']['path_id']): row for row in scene['textures']}
    previous_path = ROOT / 'assets/reference/partner-ui-high-textures-source-manifest.json'
    previous = json.loads(previous_path.read_bytes()) if previous_path.is_file() else {}
    previous_textures = {(row['source']['bundle_hash'], row['source']['path_id']): row
                         for row in previous.get('textures', [])}
    root = ROOT / 'unity/Assets/Official/Partners/Appearance'
    destination = root / 'HighTextures'
    payloads = destination / 'Resources/Partners/HighTexturePayloads'
    outputs, textures, reused = [], [], []
    for folder in (destination, destination / 'Bindings', destination / 'Resources',
                   destination / 'Resources/Partners', payloads):
        folder.mkdir(parents=True, exist_ok=True)
        meta(folder, folder=True)
        outputs.append(Path(str(folder) + '.meta'))

    def reference(path, file_id):
        return dict(fileID=file_id, guid=guid(path), type=2)

    def native(path, kind, file_id, values):
        name = {28: 'Texture2D', 114: 'MonoBehaviour'}[kind]
        binary = values.get('_typelessdata')
        serial = dict(values, _typelessdata='ZJCS_NATIVE_TEXTURE_BYTES') if binary is not None else values
        text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!' + str(kind) + ' &' + str(file_id) + '\n'
        text += yaml.dump({name: serial}, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
        if binary is not None:
            text = text.replace('_typelessdata: ZJCS_NATIVE_TEXTURE_BYTES', '_typelessdata: ' + binary)
        write_changed(path, text)
        meta(path)
        write_changed(Path(str(path) + '.meta'), 'fileFormatVersion: 2\nguid: ' + guid(path)
            + '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: ' + str(file_id) + '\n')
        outputs.extend((path, Path(str(path) + '.meta')))
        return reference(path, file_id)

    def checked_reuse(row):
        path = ROOT / row['output']['path']
        assert identity(path) == row['output']
        return path

    cache = {}
    def texture(obj, resource):
        provenance = apk.provenance(obj)
        object_key = (provenance['bundle_hash'], provenance['path_id'])
        digest = sha(obj.get_raw_data())
        if object_key in cache:
            row = cache[object_key]
            assert not resource or row.get('resource')
            return row
        prior = known.get(digest) if resource else known_objects.get(object_key)
        data = obj.read()
        encoded = data.get_image_data()
        existing = previous_textures.get(object_key) if resource else None
        if existing is not None and existing['payload_sha256'] == digest and existing['encoded_sha256'] == sha(encoded):
            path = checked_reuse(existing)
            row = dict(existing)
            textures.append(row)
            outputs.extend((path, Path(str(path) + '.meta')))
        elif prior is not None and prior['payload_sha256'] == digest and prior['encoded_sha256'] == sha(encoded) and (not resource or prior.get('resource')):
            checked_reuse(prior)
            row = dict(prior)
            reused.append(dict(source=apk.provenance(obj), payload_sha256=digest, native=prior['output'],
                meta=identity(Path(str(ROOT / prior['output']['path']) + '.meta'))))
        else:
            assert resource, 'High loader placeholder differs from the verified current original atlas'
            fields_original = obj.read_typetree()
            fields_original.pop('image data')
            assert not data.m_MipsStripped and data.m_ImageCount == 1 and data.m_TextureDimension == 2
            if 'ASTC' in TextureFormat(data.m_TextureFormat).name:
                pixels, mips, profile = decode_mips(data)
                fmt = 4
            else:
                fmt, pixels, profile = int(data.m_TextureFormat), encoded, 'original-uncompressed'
                assert fmt in (1, 4)
                mips, offset = [], 0
                for level in range(data.m_MipCount):
                    width, height = max(1, data.m_Width >> level), max(1, data.m_Height >> level)
                    count = width * height * (1 if fmt == 1 else 4)
                    mips.append(dict(level=level, width=width, height=height, rgba_offset=offset,
                        rgba_size=count, rgba_sha256=sha(pixels[offset:offset + count])))
                    offset += count
                assert offset == len(pixels)
            key = obj.assets_file.name + ':' + str(obj.path_id)
            token = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs/partner-ui-high/884/' + key).hex
            path = payloads / (token + '.asset')
            value = dict(source_header(), **fields(copy.deepcopy(fields_original)))
            value.update(serializedVersion=2, m_TextureFormat=fmt, m_CompleteImageSize=len(pixels),
                m_PlatformBlob='', m_StreamData=dict(serializedVersion=2, offset=0, size=0, path=''))
            value['m_TextureSettings']['serializedVersion'] = 2
            value['image data'] = len(pixels)
            value['_typelessdata'] = pixels.hex()
            binding = native(path, 28, 2800000, value)
            row = dict(source=apk.provenance(obj), payload_sha256=digest, original_fields=fields_original,
                encoded_sha256=sha(encoded), profile=profile, target_format=fmt, mips=mips,
                pixels_sha256=sha(pixels), binding=binding, output=identity(path),
                resource='Partners/HighTexturePayloads/' + token)
            textures.append(row)
        cache[object_key] = row
        return row

    mappings, addresses = [], {}
    for index, mapping in enumerate(original_fields['placeholderMap']):
        placeholder = PPtr(**mapping['placeholderTexture'], assetsfile=original.assets_file).deref()
        row = texture(placeholder, False)
        value = copy.deepcopy(mapping)
        value['placeholderTexture'] = row['binding']
        for field in ('targetTextureReference', 'dyeTextureReference'):
            asset_guid = mapping[field]['AssetReference']['mAssetGuid']
            if not asset_guid or asset_guid in addresses:
                continue
            asset = table[asset_guid]
            row = texture(apk.asset(asset['bundle'], asset['path']), True)
            addresses[asset_guid] = row['resource']
        mappings.append(value)
        if (index + 1) % 25 == 0:
            print('Restored', index + 1, '/', len(original_fields['placeholderMap']), 'original High mappings', flush=True)
    assert len({row['placeholderTexture']['guid'] for row in mappings}) == len(mappings), 'Distinct original placeholders were aliased'
    loader_path = destination / 'Bindings/texture-loader-high.asset'
    source_loader = ROOT / 'unity/Assets/Official/Combat/SceneCharacterTextures/Bindings/texture-loader-low.asset'
    loader = yaml.safe_load(source_loader.read_text('utf8').split('MonoBehaviour:', 1)[1])
    for field in ('m_Name', 'maxPlaceholderSize', 'unloadAfterSecondsUnused', 'LevelOfResolution',
                  'PlaceholderPath', 'TargetTexturePath', 'DyeTexturePath'):
        loader[field] = original_fields[field]
    loader['placeholderMap'] = mappings
    loader_binding = native(loader_path, 114, 11400000, loader)
    source_catalog = ROOT / 'unity/Assets/Game/Resources/Combat/SceneCharacters/appearance-resources.asset'
    catalog = yaml.safe_load(source_catalog.read_text('utf8').split('MonoBehaviour:', 1)[1])
    catalog.update(m_Name='Original current partner UI High appearance resources', loaderGuid=source_guid,
        loader=loader_binding, textures=[dict(Guid=g, Resource=p) for g, p in sorted(addresses.items())])
    catalog_path = root / 'Resources/Partners/appearance-resources.asset'
    native(catalog_path, 114, 11400000, catalog)
    proof = dict(manifest=apk.version, asset_table_sha256=sha(table_raw), source=apk.provenance(original),
        original_sha256=sha(original.get_raw_data()), path=entry['path'], guid=source_guid, original_fields=original_fields,
        current_scene_receipt=identity(scene_proof), scene_catalog=identity(source_catalog),
        mappings=len(mappings), texture_addresses=catalog['textures'], textures=textures, reused=reused,
        outputs=[identity(path) for path in outputs], generator=identity(Path(__file__)),
        decoder=dict(module=identity(Path(astc_encoder.__file__)),
            binaries=[identity(path) for path in sorted(Path(astc_encoder.__file__).parent.rglob('*.pyd'))]),
        helpers=[identity(ROOT / 'scripts' / name) for name in ('current_component_schemas.py',
            'prepare_unity_map13_native_scene.py', 'prepare_unity_profession_reward_sprites.py')],
        operation='APK first and matching verified cache: preserve all original High mappings/GUIDs/loader fields. Reuse only raw-object and encoded-image-identical current texture payloads. Decode every remaining original High ASTC mip to Windows RGBA32 with original samplers/color spaces. Reuse the same verified current skins, skeletons and placeholder atlases; no Low target is substituted.')
    write_changed(ROOT / 'assets/reference/partner-ui-high-textures-source-manifest.json',
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Restored complete partner UI High loader:', len(mappings), 'mappings,', len(addresses),
        'texture GUIDs,', len(textures), 'new native textures,', len(reused), 'verified reused objects.', flush=True)


if __name__ == '__main__':
    main()
