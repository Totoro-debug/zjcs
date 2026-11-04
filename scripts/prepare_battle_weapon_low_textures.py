"""Restore the original Low loader required by complete weapon prefabs.

Reuse only source-identical High atlas/placeholder inputs. Texture GUIDs and
all encoded mip levels are read from the selected official resource version.
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
from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from prepare_battle_scene_appearance_runtime import identity, write_changed
from prepare_unity_map13_native_scene import decode_mips
from prepare_unity_profession_character_resources import asset, guid, meta
from prepare_unity_profession_reward_sprites import Dumper, fields, source_header
from validation_workspace import validation_directory


def sha(raw): return hashlib.sha256(raw).hexdigest()


def main():
    work = validation_directory()
    apk, schemas = Apk.current(), CurrentSchemas()
    assert apk.version == '884_156692'
    high_path = ROOT / 'assets/reference/battle-animation/weapon-high-textures-source-manifest.json'
    high = json.loads(high_path.read_bytes())
    assert high['manifest'] == apk.version
    table_raw = apk.source.manifest_bytes('DefaultPackage', apk.archive)
    table = {r['path']: r for r in read_asset_manifest(table_raw)}
    by_guid = {r['guid']: r for r in table.values() if r['guid']}
    destination = ROOT / 'unity/Assets/Official/Combat/WeaponLowTextures'
    payloads = destination / 'Resources/Combat/WeaponLowPayloads'
    bindings = destination / 'Bindings'
    catalog = destination / 'Resources/Combat/Weapons/full-texture-resources.asset'
    for p in (payloads, bindings, catalog.parent):
        p.mkdir(parents=True, exist_ok=True)
        while p != ROOT / 'unity/Assets':
            if not Path(str(p) + '.meta').exists(): meta(p, folder=True)
            p = p.parent
    outputs, reused, sources, textures = [], [], [], []

    def checked(row):
        p = ROOT / row['path']; raw = p.read_bytes()
        assert sha(raw) == row['sha256'] or sha(raw.replace(b'\r\n', b'\n')) == row['sha256'], p
        reused.append(identity(p)); reused.append(identity(Path(str(p) + '.meta')))
        return p

    def existing(path):
        row, = [r for r in high['outputs'] if r['path'] == path]
        return checked(row)

    def reference(path, file_id=11400000): return dict(fileID=file_id, guid=guid(path), type=2)

    def source(obj):
        row = dict(source=apk.provenance(obj), payload_sha256=sha(obj.get_raw_data()))
        sources.append(row); return row

    def pointer(obj, value): return PPtr(**value, assetsfile=obj.assets_file).deref()

    atlas = existing('unity/Assets/Official/Combat/WeaponTextures/Bindings/atlas.asset')
    high_loader = existing('unity/Assets/Official/Combat/WeaponTextures/Bindings/texture-loader-high.asset')
    manager = existing('unity/Assets/Official/Combat/WeaponTextures/Bindings/asset-manager-config.asset')
    loader_path = 'Assets/AppearanceAssets/Character/pWeapon/primaryWeapon_TextureLoader_Low.asset'
    original = apk.asset(table[loader_path]['bundle'], loader_path)
    source(original); loader = schemas.read(original)
    assert loader['LevelOfResolution'] == 1 and loader['skeletonDataAsset']['m_PathID'] == 0
    original_atlas = pointer(original, loader['atlasAsset'])
    assert apk.provenance(original_atlas) == high['atlas']['source']
    assert len(loader['placeholderMap']) == 398
    high_textures = {r['payload_sha256']: r for r in high['textures']}
    addresses, mappings, mapping_rows, cache = {}, [], [], {}
    for index, row in enumerate(loader['placeholderMap']):
        placeholder = pointer(original, row['placeholderTexture']); source(placeholder)
        high_row = high['mappings'][index]
        assert placeholder.read().m_Name == high_row['placeholder']
        prior_placeholder = high['loader']['original_fields']['placeholderMap'][index]['placeholderTexture']
        assert row['placeholderTexture'] == prior_placeholder
        # Both original loaders share exactly the same placeholder objects.
        material = checked(high['materials'][index]['output'])
        # Use the already explicit placeholder binding in the High native asset.
        original_placeholder_guid = re.search(r'_MainTex:\n\s+m_Texture: \{fileID: 2800000, guid: ([0-9a-f]{32}), type: 2\}', material.read_text())[1]
        converted = copy.deepcopy(row)
        converted['placeholderTexture'] = dict(fileID=2800000, guid=original_placeholder_guid, type=2)
        mappings.append(converted)
        for field in ('targetTextureReference', 'dyeTextureReference'):
            key = row[field]['AssetReference']['mAssetGuid']
            if not key or key in addresses: continue
            entry = by_guid[key]; obj = apk.asset(entry['bundle'], entry['path'])
            original_source = source(obj); digest = original_source['payload_sha256']
            if digest not in cache:
                data = obj.read(); original_fields = obj.read_typetree(); original_fields.pop('image data')
                assert not data.m_MipsStripped and data.m_ImageCount == 1 and data.m_TextureDimension == 2
                encoded = data.get_image_data()
                if digest in high_textures:
                    prior = high_textures[digest]; target = checked(prior['output'])
                    assert prior['encoded_sha256'] == sha(encoded)
                    record = dict(prior, reused_high=True)
                else:
                    if 'ASTC' in TextureFormat(data.m_TextureFormat).name:
                        pixels, mips, profile = decode_mips(data); fmt = 4
                    else:
                        fmt, pixels, profile = int(data.m_TextureFormat), encoded, 'original-uncompressed'
                        assert fmt in (1, 4), (data.m_Name, fmt)
                        mips, offset = [], 0
                        for level in range(data.m_MipCount):
                            width, height = max(1, data.m_Width >> level), max(1, data.m_Height >> level)
                            size = width * height * (1 if fmt == 1 else 4)
                            mips.append(dict(level=level, width=width, height=height, rgba_offset=offset,
                                rgba_size=size, rgba_sha256=sha(pixels[offset:offset + size])))
                            offset += size
                        assert offset == len(pixels)
                    target = payloads / (digest + '.asset')
                    value = dict(source_header(), **fields(copy.deepcopy(original_fields)))
                    value.update(serializedVersion=2, m_TextureFormat=fmt, m_CompleteImageSize=len(pixels),
                        m_PlatformBlob='', m_StreamData=dict(serializedVersion=2, offset=0, size=0, path=''))
                    value['m_TextureSettings']['serializedVersion'] = 2
                    value['image data'], value['_typelessdata'] = len(pixels), 'ZJCS_TEXTURE_BYTES'
                    text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!28 &2800000\n'
                    text += yaml.dump({'Texture2D': value}, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
                    text = text.replace('_typelessdata: ZJCS_TEXTURE_BYTES', '_typelessdata: ' + pixels.hex())
                    write_changed(target, text); meta(target)
                    write_changed(Path(str(target) + '.meta'), 'fileFormatVersion: 2\nguid: ' + guid(target)
                        + '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 2800000\n')
                    outputs.extend((target, Path(str(target) + '.meta')))
                    record = dict(original_source, original_fields=original_fields, encoded_sha256=sha(encoded),
                        target_format=fmt, profile=profile, mips=mips, pixels_sha256=sha(pixels),
                        output=identity(target), binding=reference(target, 2800000), resource='Combat/WeaponLowPayloads/' + digest)
                cache[digest] = record; textures.append(record)
            addresses[key] = cache[digest]['resource']
        mapping_rows.append(dict(index=index, placeholder=placeholder.read().m_Name,
            main=row['targetTextureReference']['AssetReference']['mAssetGuid'], dye=row['dyeTextureReference']['AssetReference']['mAssetGuid']))
        if (index + 1) % 50 == 0: print('Original Low mappings', index + 1, '/398', flush=True)
    native_loader = bindings / 'texture-loader-low.asset'
    loader_fields = {k: copy.deepcopy(loader[k]) for k in ('maxPlaceholderSize', 'unloadAfterSecondsUnused',
        'LevelOfResolution', 'PlaceholderPath', 'TargetTexturePath', 'DyeTexturePath')}
    loader_fields.update(atlasAsset=reference(atlas), skeletonDataAsset=dict(fileID=0), placeholderMap=mappings)
    low_ref = asset(native_loader, loader['m_Name'], ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI/YooAssetTextureLoader.cs', loader_fields)
    outputs.extend((native_loader, Path(str(native_loader) + '.meta')))
    combined = {r['Guid']: r['Resource'] for r in high['texture_addresses']}
    for key, value in addresses.items():
        assert key not in combined or combined[key] == value
        combined[key] = value
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeWeaponTextureResources.cs'
    asset(catalog, 'Complete original weapon High and Low bindings', runtime, dict(
        loaderPath=high['loader']['path'], loaderGuid=high['loader']['guid'], loader=reference(high_loader),
        atlas=reference(atlas), managerConfig=reference(manager),
        additionalLoaders=[dict(Path=loader_path, Guid=table[loader_path]['guid'], Loader=low_ref)],
        textures=[dict(Guid=k, Resource=v) for k, v in sorted(combined.items())]))
    outputs.extend((catalog, Path(str(catalog) + '.meta')))
    proof = dict(manifest=apk.version, high_source_receipt=identity(high_path), asset_table_sha256=sha(table_raw),
        loader=dict(path=loader_path, guid=table[loader_path]['guid'], source=apk.provenance(original), original_fields=loader),
        sources=sources, textures=textures, mappings=mapping_rows,
        texture_addresses=[dict(Guid=k, Resource=v) for k,v in sorted(addresses.items())],
        reused=list({r['path']: r for r in reused}.values()), outputs=[identity(p) for p in outputs],
        runtime=identity(runtime), generator=identity(Path(__file__)),
        validation=identity(ROOT / 'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattlePlayerModelClipValidation.HighTextures.cs'),
        decoder=dict(module=identity(Path(astc_encoder.__file__)), binaries=[identity(p) for p in sorted(Path(astc_encoder.__file__).parent.rglob('*.pyd'))]),
        binding='Combat/Weapons/full-texture-resources',
        operation='Preserve the complete original Low loader and original GUIDs; convert every mip with original flags/samplers. Reuse the byte-verified current High atlas, materials and identical placeholders; keep both explicit original loader addresses.',
        limits=['This dependency conversion does not yet prove complete native weapon prefab playback.'])
    write_changed(ROOT / 'assets/reference/battle-animation/weapon-low-textures-source-manifest.json', json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    skins = sorted(Path(r['path']).parent.name for r in table.values() if '/PlayerPart/pWeapon_' in r['path']
        and r['path'].endswith('_Integrated.prefab') and '_UI_' not in r['path'])
    for name, evidence in [('high',high),('low',proof)]:
        fixture = dict(textures=evidence['textures'], addresses=evidence['texture_addresses'], mappings=evidence['mappings'], skins=skins)
        write_changed(work / ('weapon-' + name + '-texture-cases.json'), json.dumps(fixture, ensure_ascii=False, separators=(',', ':')) + '\n')
    apk.archive.close()
    print('Restored original Low:', len(addresses), 'GUIDs,', len(textures), 'unique textures;', len(combined), 'combined GUIDs.')


if __name__ == '__main__': main()
