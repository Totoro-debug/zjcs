"""Restore original battle-partner appearance components and texture loaders."""
import copy
import hashlib
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / '.tools/python-packages'))
import yaml
from PIL import Image
from UnityPy.classes import PPtr
from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from official_battle_spine_component import read_component
from prepare_unity_map13_native_scene import decode_mips, native_texture
from prepare_unity_profession_character_resources import guid, meta
from prepare_unity_profession_reward_sprites import Dumper, fields, source_header
from validation_workspace import validation_directory


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def build():
    workspace = validation_directory()
    template_path = workspace / 'monster-texture-template.asset'
    template = template_path.read_text('utf-8')
    native_root = ROOT / 'unity/Assets/Official/Combat/NativePartners'
    destination = ROOT / 'unity/Assets/Official/Combat/NativePartnerAppearance'
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    inventory = native_root / 'catalog.json'
    models = json.loads(inventory.read_bytes())
    shader_proof_path = ROOT / 'assets/reference/partner-native-battle-assets-source-manifest.json'
    shader_proof = json.loads(shader_proof_path.read_bytes())
    shader_path = ROOT / 'unity/Assets/Official/Combat/NativePartnerAssets/OriginalSkeletonEffect.shader'
    inputs, outputs, sources, converted, bindings, originals = {}, {}, {}, {}, [], []
    apk, schemas = Apk.current(), CurrentSchemas()
    assert models['manifest'] == apk.version == shader_proof['manifest'] == '884_156692'

    def source_key(obj):
        return obj.assets_file.name + ':' + str(obj.path_id)

    def record(obj):
        sources[source_key(obj)] = dict(source=apk.provenance(obj), payload_sha256=sha(obj.get_raw_data()))
        return obj

    def pointer(owner, value):
        return record(PPtr(assetsfile=owner.assets_file, **value).deref()) if value['m_PathID'] else None

    def object_path(go):
        if isinstance(go, PPtr):
            go = go.deref()
        transform, = [ref.component.read() for ref in go.read().m_Component if ref.component.deref().type.name == 'Transform']
        return (object_path(transform.m_Father.read().m_GameObject) + '/' if transform.m_Father.path_id else '') + go.read().m_Name

    def input_file(path, sidecar=True):
        files = (path, path.with_name(path.name + '.meta')) if sidecar else (path,)
        for item in files:
            inputs[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
        return path

    def ref(path, file_id=11400000, kind=2):
        return dict(fileID=file_id, guid=guid(path), type=kind)

    def write(relative, raw, main_id=None):
        path = destination / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(raw)
        sidecar = path.with_name(path.name + '.meta')
        if not sidecar.exists():
            meta(path, native=main_id is not None)
            if main_id is not None:
                sidecar.write_text(sidecar.read_text('utf-8').replace('mainObjectFileID: 11400000', 'mainObjectFileID: ' + str(main_id)), encoding='utf-8', newline='\n')
        for item in (path, sidecar):
            outputs[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
        return path

    def native(relative, class_id, main_id, type_name, value):
        header = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!' + str(class_id) + ' &' + str(main_id) + '\n'
        return write(relative, (header + yaml.dump({type_name: value}, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)).encode('utf-8'), main_id)

    def scriptable(relative, name, script, value):
        if not script.with_name(script.name + '.meta').exists():
            meta(script)
        input_file(script)
        return native(relative, 114, 11400000, 'MonoBehaviour', dict(source_header(), m_Enabled=1, m_EditorHideFlags=0,
            m_Name=name, m_EditorClassIdentifier='', m_Script=ref(script, 11500000, 3), **value))

    def texture(obj):
        key = source_key(obj)
        if key not in converted:
            image = obj.read()
            rgba, mips, profile = decode_mips(image)
            path = write('Textures/' + sha(obj.get_raw_data()) + '.asset', native_texture(template, image, rgba), 2800000)
            converted[key] = dict(output=path.relative_to(ROOT).as_posix(), rgba_sha256=sha(rgba), mips=mips, profile=profile)
        return ROOT / converted[key]['output']

    try:
        input_file(inventory)
        input_file(shader_proof_path, False)
        input_file(shader_path)
        for name in ('PartnerNativeBattleAppearanceCatalog.cs', 'PartnerNativeBattleAppearance.cs',
                     'PartnerNativeBattleActor.cs', 'PartnerNativeBattleMesh.cs', 'PartnerNativeBattleRendererPort.cs',
                     'Zjcs.Offline.ProfessionUI.asmdef'):
            script = runtime / 'Offline/ProfessionUI' / name
            if not script.with_name(script.name + '.meta').exists():
                meta(script)
            input_file(script)
        for name in ('AppearanceRendererLite.cs', 'MeshMaterialProperty.cs', 'YooAssetTextureLoader.cs'):
            folder = 'Official/BattleMaterialPlayables' if name == 'MeshMaterialProperty.cs' else 'Official/ProfessionUI'
            input_file(runtime / folder / name)
        input_file(runtime / 'Validation/BattlePublic/OfficialBattlePublicFlowValidation.cs')
        input_file(runtime / 'Validation/BattlePublic/Zjcs.Validation.BattlePublic.asmdef')
        input_file(ROOT / 'unity/Assets/Game/Editor/BattleMovementEffects/OfficialBattlePublicMovementAcceptance.cs')
        input_file(ROOT / 'scripts/test_battle_public_player.ps1', False)
        for name in ('OfficialBattleAnimatorPlaybackAcceptance.PartnerMaterials.cs',
                     'OfficialBattleAnimatorPlaybackAcceptance.PetMaterials.cs'):
            script = ROOT / 'unity/Assets/Game/Editor/Combat/AnimatorPlayback' / name
            if not script.with_name(script.name + '.meta').exists():
                meta(script)
            input_file(script)
        original_client = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source'
        for name in ('AppearanceSystem/AppearanceRendererLite.cs', 'AppearanceSystem.Runtime/AppearanceAddressUtils.cs',
                     'MeshMaterialProperty.cs'):
            input_file(original_client / name, False)
        table = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))
        by_path = {row['path']: row for row in table}
        by_guid = {row['guid']: row for row in table if row['guid']}
        config_location, = [row for row in table if row['path'].endswith('/AppearanceConfig.asset')]
        config = schemas.read(record(apk.asset(config_location['bundle'], config_location['path'])))
        skeleton_paths = {row['SkeletonName']: row['Path'] for row in config['mAppearanceSkeletonPaths']}
        for model in models['fighters']:
            entity, npc = model['entityId'], model['npcId']
            parts = [model, *model['parts']]
            components = {}
            for obj in apk.objects(f'defaultpackage_assets_prefabs_ecentity_fight_fightnpc_{entity}.bundle'):
                if obj.type.name == 'MonoBehaviour':
                    components.setdefault(obj.read(check_read=False).m_Script.read().m_ClassName, []).append(obj)
            prop_obj, = components['MeshMaterialProperty']
            prop = schemas.read(record(prop_obj))
            assert not prop['MeshRenderer']['m_PathID'] and not prop['mAppearanceRenderer']['m_PathID']
            property_path = object_path(prop_obj.read(check_read=False).m_GameObject)
            assert property_path == str(entity)
            target_obj = pointer(prop_obj, prop['mAppearanceRendererLite'])
            target_path = object_path(target_obj.read(check_read=False).m_GameObject).split('/', 1)[1]
            renderers, original_parts = [], []
            for part in parts:
                appearance_obj, = [obj for obj in components['AppearanceRendererLite'] if schemas.read(obj) == part['appearance']]
                appearance = schemas.read(record(appearance_obj))
                assert appearance['LevelOfResolution'] == 1 and not appearance['RenderExistingMeshes']
                renderer_obj = pointer(appearance_obj, appearance['SkeletonRenderer'])
                kind = renderer_obj.read(check_read=False).m_Script.read().m_ClassName
                renderer = read_component(renderer_obj.get_raw_data(), kind)
                assert renderer == part['renderer']
                path = object_path(appearance_obj.read(check_read=False).m_GameObject)
                assert path == '/'.join(row['name'] for row in part['transforms'])
                data_obj = pointer(renderer_obj, renderer['skeletonDataAsset'])
                data = schemas.read(data_obj)
                assert len(data['atlasAssets']) == 1
                data_name = data_obj.read(check_read=False).m_Name
                skeleton_name = data_name.removesuffix('_SkeletonData')
                atlas_obj = pointer(data_obj, data['atlasAssets'][0])
                atlas = schemas.read(atlas_obj)
                atlas_path = skeleton_paths[skeleton_name] + '/' + skeleton_name + '_Atlas.asset'
                assert apk.asset(by_path[atlas_path]['bundle'], atlas_path).get_raw_data() == atlas_obj.get_raw_data()
                loader_path = part['textureLoader']['path']
                assert loader_path == skeleton_paths[skeleton_name] + '/' + skeleton_name + '_TextureLoader_Low.asset'
                loader_obj = record(apk.asset(by_path[loader_path]['bundle'], loader_path))
                loader = schemas.read(loader_obj)
                assert loader == json.loads(input_file(native_root / part['textureLoader']['fields']).read_bytes())
                assert loader['LevelOfResolution'] == 1 and not loader['skeletonDataAsset']['m_PathID']
                materials_data = json.loads(input_file(native_root / part['materials']).read_bytes())
                runtime_textures = [row for material in materials_data for row in material['runtimeTextures']]
                targets, mappings = {}, []
                for mapping in loader['placeholderMap']:
                    placeholder_obj = pointer(loader_obj, mapping['placeholderTexture'])
                    placeholder_path = texture(placeholder_obj)
                    converted_mapping = dict(placeholderTexture=ref(placeholder_path, 2800000))
                    for field in ('targetTextureReference', 'dyeTextureReference'):
                        target_guid = mapping[field]['AssetReference']['mAssetGuid']
                        converted_mapping[field] = dict(AssetReference=dict(mAssetGuid=target_guid))
                        if not target_guid:
                            continue
                        match, = [row for row in runtime_textures if row['guid'] == target_guid and row['placeholder'] == source_key(placeholder_obj)]
                        location = by_guid[target_guid]
                        assert match['path'] == location['path']
                        target = record(apk.asset(location['bundle'], location['path']))
                        image = target.read()
                        assert source_key(target) == match['source'] and image.m_MipCount == match['mip_count'] == 1
                        assert sha(image.get_image_data()) == match['encoded_sha256']
                        assert sha(image.image.convert('RGBA').tobytes()) == match['rgba_sha256']
                        target_path_file = input_file(native_root / match['file'])
                        with Image.open(target_path_file) as png:
                            assert png.size == (match['width'], match['height'])
                            assert sha(png.convert('RGBA').tobytes()) == match['rgba_sha256']
                        targets[target_guid] = dict(Guid=target_guid, Resource='', Texture=ref(target_path_file, 2800000, 3))
                    mappings.append(converted_mapping)
                materials, original_materials = [], []
                assert len(atlas['materials']) == len(materials_data) == 1
                for value, expected in zip(atlas['materials'], materials_data):
                    material_obj = pointer(atlas_obj, value)
                    original = material_obj.read_typetree()
                    assert json.loads(json.dumps(original)) == expected['fields']
                    source_shader = pointer(material_obj, original['m_Shader'])
                    assert sha(source_shader.get_raw_data()) == shader_proof['shader_source']['object_sha256']
                    material = copy.deepcopy(original)
                    material['m_Shader'] = ref(shader_path, 4800000, 3)
                    for _, environment in material['m_SavedProperties']['m_TexEnvs']:
                        original_texture = pointer(material_obj, environment['m_Texture'])
                        environment['m_Texture'] = ref(texture(original_texture), 2800000) if original_texture is not None else dict(fileID=0)
                    material = dict(source_header(), **fields(material), serializedVersion=8)
                    material['m_SavedProperties'] = dict(serializedVersion=3,
                        **{name: [{k: v} for k, v in pairs] for name, pairs in material['m_SavedProperties'].items()})
                    material_path = native('Materials/' + sha(material_obj.get_raw_data()) + '.mat', 21, 2100000, 'Material', material)
                    materials.append(ref(material_path, 2100000))
                    original_materials.append(dict(source_key=source_key(material_obj), fields=original))
                metadata_obj = pointer(atlas_obj, atlas['atlasFile'])
                metadata_file = input_file(native_root / part['atlas'])
                assert metadata_file.read_bytes() == metadata_obj.read(check_read=False).m_Script.encode('utf-8', 'surrogateescape')
                atlas_asset = scriptable('Bindings/' + skeleton_name + '-atlas.asset', atlas['m_Name'],
                    runtime / 'Offline/ProfessionUI/ProfessionNativeTextureAtlas.cs', dict(sourcePath=atlas_path,
                        sourceGuid=by_path[atlas_path]['guid'], originalMetadata=ref(metadata_file, 4900000, 3), materials=materials))
                keep = {name: copy.deepcopy(loader[name]) for name in ('maxPlaceholderSize', 'unloadAfterSecondsUnused',
                    'LevelOfResolution', 'PlaceholderPath', 'TargetTexturePath', 'DyeTexturePath')}
                loader_asset = scriptable('Bindings/' + skeleton_name + '-loader-low.asset', loader['m_Name'],
                    runtime / 'Official/ProfessionUI/YooAssetTextureLoader.cs', dict(**keep, atlasAsset=ref(atlas_asset),
                        skeletonDataAsset=dict(fileID=0), placeholderMap=mappings))
                manager = input_file(ROOT / 'unity/Assets/Official/ProfessionUI/WeaponTextureLoader/appearance-asset-manager-config.asset')
                resources = scriptable('Bindings/' + skeleton_name + '-textures.asset', skeleton_name + ' original partner textures',
                    runtime / 'Offline/ProfessionUI/ProfessionNativeWeaponTextureResources.cs', dict(loaderPath=loader_path,
                        loaderGuid=guid(loader_asset), loader=ref(loader_asset), atlas=ref(atlas_asset), managerConfig=ref(manager),
                        textures=list(targets.values()), additionalLoaders=[]))
                binding = dict(Path=path.split('/', 1)[1], SkeletonDataName=data_name, Enabled=bool(appearance['m_Enabled']),
                    LevelOfResolution=1, Textures=ref(resources), Materials=materials)
                renderers.append(binding)
                original_parts.append(dict(appearance=appearance, renderer_path=path, loader=loader, atlas=atlas,
                    materials=original_materials, binding=binding))
            assert target_path == renderers[0]['Path']
            binding = dict(NpcId=npc, EntityId=entity, PropertyPath='', AppearanceTargetPath=target_path,
                PropertyEnabled=bool(prop['m_Enabled']), Renderers=renderers)
            bindings.append(binding)
            originals.append(dict(npcId=npc, entityId=entity, property=prop, parts=original_parts, binding=binding))
            print(f'{npc}: restored {len(renderers)} original appearance renderer(s) and root material property', flush=True)
        catalog_script = runtime / 'Offline/ProfessionUI/PartnerNativeBattleAppearanceCatalog.cs'
        scriptable('Resources/Combat/NativePartnerAppearance/catalog.asset', 'Original native partner appearance bindings', catalog_script,
            dict(bindings=bindings))
        for folder in sorted({parent for file in destination.rglob('*') if file.is_file() for parent in file.parents if parent.is_relative_to(destination)}):
            sidecar = folder.with_name(folder.name + '.meta')
            if not sidecar.exists():
                meta(folder, folder=True)
            outputs[sidecar.relative_to(ROOT).as_posix()] = sha(sidecar.read_bytes())
        return dict(manifest=apk.version, generator_sha256=sha(Path(__file__).read_bytes()),
            source_extraction=dict(sources=sources, schemas=schemas.records, models=originals),
            placeholder_conversion=converted, inputs=inputs, outputs=outputs, texture_template_sha256=sha(template_path.read_bytes()))
    finally:
        apk.archive.close()


if __name__ == '__main__':
    receipt = build()
    path = ROOT / 'assets/reference/battle-animation/native-partner-appearance-source-manifest.json'
    path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print(json.dumps(dict(models=len(receipt['source_extraction']['models']), outputs=len(receipt['outputs']))))
