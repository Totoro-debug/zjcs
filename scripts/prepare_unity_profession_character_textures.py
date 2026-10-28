"""Export actual core player pages through current original texture-loader GUIDs."""
import copy
import hashlib
import json
from pathlib import Path
import re
import uuid

from official_apk import Apk
from official_manifest import read_asset_manifest
from map13_native_schemas import NativeSchemas
from map13_entity_native_schemas import install
from prepare_unity_profession_native_inputs import ROOT, CACHE
from prepare_unity_profession_native_curves import record
from prepare_native_character_geometry import atlas_regions
from prepare_unity_profession_reward_sprites import Dumper, fields, source_header
from prepare_unity_map13_native_scene import decode_mips
import yaml
from UnityPy.classes import PPtr
from UnityPy.enums import TextureFormat
import astc_encoder


def payload_hash(obj):
    return hashlib.sha256(obj.get_raw_data()).hexdigest()


def meta(path, guid, main_id=None):
    text = 'fileFormatVersion: 2\nguid: ' + guid + '\n'
    if main_id:
        text += 'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: ' + str(main_id) + '\n'
    path.with_name(path.name + '.meta').write_text(text, encoding='utf-8', newline='\n')


def build():
    apk = Apk.current()
    if apk.version != '871_156598': raise ValueError('Verify original character texture version')
    code = ROOT / '.tools/source-packages/current/871_156598/official-code'
    schemas = NativeSchemas(code, unity_project=ROOT / 'unity'); schema_proof = install(schemas, code)
    manifest_bytes = apk.source.manifest_bytes('DefaultPackage', apk.archive)
    table = {r['guid']: r for r in read_asset_manifest(manifest_bytes) if r['guid']}
    attachment_proof_path = ROOT / 'assets/reference/profession-ui/native-attachments-source-manifest.json'
    attachment_proof = json.loads(attachment_proof_path.read_bytes())
    if attachment_proof['manifest'] != apk.version: raise ValueError('Verified attachment version changed')
    inputs, models, selections, pages, textures, exported = [], [], [], [], [], {}

    def export(obj, role):
        identity = (obj.assets_file.name, obj.path_id)
        if identity in exported: return exported[identity]
        texture = obj.read()
        if texture.m_MipsStripped or texture.m_ImageCount != 1 or texture.m_TextureDimension != 2:
            raise ValueError('Verify original character texture topology: ' + texture.m_Name)
        encoded = texture.get_image_data()
        if 'ASTC' in TextureFormat(texture.m_TextureFormat).name:
            pixels, mips, profile = decode_mips(texture); target_format = 4
        else:
            target_format = int(texture.m_TextureFormat)
            if target_format not in (1, 4): raise ValueError('Verify original character texture format')
            pixels, mips, profile, offset = encoded, [], 'original-uncompressed', 0
            for level in range(texture.m_MipCount):
                w, h = max(1, texture.m_Width >> level), max(1, texture.m_Height >> level)
                size = w * h * (1 if target_format == 1 else 4); chunk = pixels[offset:offset + size]
                mips.append(dict(level=level, width=w, height=h, rgba_offset=offset, rgba_size=size,
                                 rgba_sha256=hashlib.sha256(chunk).hexdigest())); offset += size
            if offset != len(pixels): raise ValueError('Original mip bytes not consumed')
        original = obj.read_typetree(); original.pop('image data')
        native = source_header(); native.update(fields(copy.deepcopy(original)))
        native.update(serializedVersion=2, m_TextureFormat=target_format, m_CompleteImageSize=len(pixels), m_PlatformBlob='',
                      m_StreamData=dict(serializedVersion=2, offset=0, size=0, path=''))
        native['image data'] = len(pixels); native['_typelessdata'] = pixels.hex()
        suffix = {'targetTextureReference': 'main', 'dyeTextureReference': 'dye', 'placeholderTexture': 'placeholder'}[role]
        target = ROOT / 'unity/Assets/Official/ProfessionUI/CharacterTextures' / (texture.m_Name + '.' + suffix + '.texture2d.asset')
        target.parent.mkdir(parents=True, exist_ok=True)
        guid = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs/profession/native-texture/' + obj.assets_file.name + ':' + str(obj.path_id)).hex
        text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!28 &2800000\n' + yaml.dump({'Texture2D': native}, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
        # Existing payloads belong to the already verified attachment export.
        # Require exact bytes before reuse; never overwrite a changed payload.
        if target.exists():
            verified = next((r for r in attachment_proof['textures'] if r['output']['path'] == target.relative_to(ROOT).as_posix()), None)
            previous = ROOT / 'assets/reference/profession-ui/character-textures-source-manifest.json'
            if verified is None and previous.exists():
                verified = next((r for r in json.loads(previous.read_bytes())['textures'] if r['output']['path'] == target.relative_to(ROOT).as_posix()), None)
            if verified is not None and record(target) != verified['output'] or target.read_bytes() != text.encode('utf-8'):
                raise ValueError('Existing original texture differs from verified deterministic export: ' + str(target))
        else:
            target.write_text(text, encoding='utf-8', newline='\n'); meta(target, guid, 2800000)
        if re.search(r'(?m)^guid: ([0-9a-f]{32})$', target.with_name(target.name + '.meta').read_text())[1] != guid:
            raise ValueError('Original texture asset identity changed')
        row = dict(role=role, source=apk.provenance(obj), payload_sha256=payload_hash(obj),
                   encoded_sha256=hashlib.sha256(encoded).hexdigest(), original_fields=original, profile=profile,
                   target_format=target_format, mips=mips, output=record(target), meta=record(target.with_name(target.name + '.meta')), guid=guid)
        textures.append(row); exported[identity] = row
        return row

    for sex in ('male', 'female'):
        source_path = CACHE / 'skeletons' / sex / 'skeleton.json'; atlas_path = source_path.with_name('atlas.txt')
        for p in (source_path, atlas_path):
            if record(p) not in attachment_proof['inputs']: raise ValueError('Verified original skeleton/atlas changed')
            inputs.append(record(p))
        source = json.loads(source_path.read_bytes()); regions = {r['name']: r for r in atlas_regions(atlas_path.read_text(encoding='utf-8'))}
        gender = 'M' if sex == 'male' else 'F'
        names = ['default', f'body_100_{gender}_01', f'body_200_{gender}_01', 'hair_G_01' if sex == 'male' else 'hair_F_01']
        required = set()
        for skin in source['skins']:
            if skin['name'] not in names: continue
            for slot in skin.get('attachments', {}).values():
                for key, value in slot.items():
                    if value.get('type', 'region') in ('region', 'mesh'):
                        required.add(regions[value.get('path', value.get('name', key))]['page'])
        selections.append(dict(sex=sex, skins=names, pages=sorted(required)))
        loader_obj = next(o for o in apk.objects('appearanceassets_character_model_' + sex + '_1')
                          if o.type.name == 'MonoBehaviour' and o.read(check_read=False).m_Name == 'character_' + sex.title() + '_1_TextureLoader_High')
        loader, _ = schemas.read(loader_obj)
        models.append(dict(source=apk.provenance(loader_obj), payload_sha256=payload_hash(loader_obj), fields=loader))
        pending = set(required)
        for mapping in loader['placeholderMap']:
            placeholder = PPtr(**mapping['placeholderTexture'], assetsfile=loader_obj.assets_file).deref()
            page = placeholder.read().m_Name.removesuffix('_placeholder')
            if page not in required: continue
            if page not in pending: raise ValueError('Duplicate original placeholder page: ' + sex + '/' + page)
            pending.remove(page)
            row = dict(sex=sex, page=page, placeholder=export(placeholder, 'placeholderTexture'))
            for role in ('targetTextureReference', 'dyeTextureReference'):
                guid = mapping[role]['AssetReference']['mAssetGuid']; row[role + '_guid'] = guid
                row[role] = export(apk.asset(table[guid]['bundle'], table[guid]['path']), role) if guid else None
            if row['targetTextureReference'] is None: raise ValueError('Original core main texture is absent')
            pages.append(row)
        if pending: raise ValueError('Original required pages not resolved: ' + str(pending))
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeTextureCatalog.cs'
    validation = ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionCharacterTextureValidation.cs'
    catalog = ROOT / 'unity/Assets/Game/Resources/ProfessionUI/character-textures.asset'
    script_guid = re.search(r'(?m)^guid: ([0-9a-f]{32})$', runtime.with_name(runtime.name + '.meta').read_text())[1]
    entries = []
    for row in pages:
        def pointer(value): return {'fileID': 2800000, 'guid': value['guid'], 'type': 2} if value else {'fileID': 0}
        entries.append(dict(Sex=row['sex'], Name=row['page'], MainGuid=row['targetTextureReference_guid'], DyeGuid=row['dyeTextureReference_guid'],
                            Placeholder=pointer(row['placeholder']), Main=pointer(row['targetTextureReference']), Dye=pointer(row['dyeTextureReference'])))
    native = source_header(); native.update(m_Enabled=1, m_EditorHideFlags=0, m_Script={'fileID': 11500000, 'guid': script_guid, 'type': 3},
                                            m_Name='Original core character texture bindings', m_EditorClassIdentifier='', pages=entries)
    catalog.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n' + yaml.dump({'MonoBehaviour': native}, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000), encoding='utf-8', newline='\n')
    meta(catalog, uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs/profession/core-character-texture-catalog').hex, 11400000)
    fixture = CACHE / 'typed/character-textures-validation.json'
    fixture.write_text(json.dumps(dict(pages=[dict(sex=r['sex'], page=r['page'], main_guid=r['targetTextureReference_guid'], dye_guid=r['dyeTextureReference_guid']) for r in pages],
        textures=[dict(path=r['output']['path'].removeprefix('unity/'), guid=r['guid'], width=r['original_fields']['m_Width'], height=r['original_fields']['m_Height'], mip_count=r['original_fields']['m_MipCount'], texture_format=r['target_format'], settings=r['original_fields']['m_TextureSettings'], mips=r['mips']) for r in textures]), ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    result = dict(format=1, manifest=apk.version, inputs=inputs, baseline_attachments=record(attachment_proof_path), models=models,
                  selections=selections, pages=[dict(sex=r['sex'], page=r['page'], main_guid=r['targetTextureReference_guid'], dye_guid=r['dyeTextureReference_guid'],
                    placeholder=r['placeholder']['output'], main=r['targetTextureReference']['output'], dye=r['dyeTextureReference']['output'] if r['dyeTextureReference'] else None) for r in pages],
                  textures=textures, asset_table_sha256=hashlib.sha256(manifest_bytes).hexdigest(), schema=schema_proof,
                  schema_reference_inputs=[record(p) if p.is_relative_to(ROOT) else dict(path=str(p), sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in schemas.reference_inputs],
                  catalog=record(catalog), catalog_meta=record(catalog.with_name(catalog.name + '.meta')), runtime=record(runtime), runtime_meta=record(runtime.with_name(runtime.name + '.meta')),
                  validation=record(validation), validation_meta=record(validation.with_name(validation.name + '.meta')),
                  generator=record(Path(__file__)), decoder_source=record(ROOT / 'scripts/prepare_unity_map13_native_scene.py'), fixture=record(fixture),
                  decoder=dict(package='astc_encoder_py', version='0.1.12', binaries=[record(p) for p in sorted(Path(astc_encoder.__file__).parent.rglob('*.pyd'))]),
                  operation='APK-first current-version core skin attachment pages resolve through actual original high-resolution placeholder mappings and AssetTable GUIDs. Preserve original placeholders and decoded main/dye mip payloads, color space and samplers as native Unity Texture2D assets; explicit local catalog references include original resource GUIDs. No PNG re-compression, alternate art or baked actor.',
                  limitations=['Only default, core warrior/mage clothing and the two selected original hair skins are covered. Other outfits, parts and profession-rank visuals require their actual original pages.',
                               'Texture resource binding is not physics, native actor/page rendering or Windows Player validation; payloads are not claimed in a complete asset snapshot.'],
                  validated_scope='Pending Unity import/compile, actual Play Mode catalog and exact GPU mip validation.')
    (ROOT / 'assets/reference/profession-ui/character-textures-source-manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Exported current original core character pages:', len(pages), 'bindings,', len(textures), 'distinct textures')
    return result


if __name__ == '__main__':
    build()
