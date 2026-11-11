"""Restore APK prefab appearance/placeholder/atlas bindings for native monsters."""
import argparse
import copy
import hashlib
import json
from pathlib import Path

import yaml
from UnityPy.classes import PPtr

from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from official_battle_spine_component import read_component
from official_manifest import read_asset_manifest
from prepare_unity_map13_native_scene import decode_mips, native_texture
from prepare_unity_profession_character_resources import guid, meta
from prepare_unity_profession_reward_sprites import Dumper, fields, source_header
from validation_workspace import validation_directory


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def build(entity_ids):
    work = validation_directory()
    destination = ROOT / 'unity/Assets/Official/Combat/NativeMonsterAppearance'
    native_root = ROOT / 'unity/Assets/Official/Combat/NativeMonsters'
    installed = json.loads((ROOT / 'assets/reference/battle-animation/live-monsters-source-manifest.json').read_bytes())
    models = {row['entityId']: row for row in json.loads((native_root / 'inventory.json').read_bytes())['models']}
    template_path = work / 'monster-texture-template.asset'
    template = template_path.read_text('utf-8')
    shader_path = native_root / 'OriginalSkeletonEffect.shader'
    sources, outputs, inputs, rows, bindings, textures = {}, {}, {}, [], [], {}
    apk, schemas = Apk.current(), CurrentSchemas()
    assert apk.version == installed['manifest'] == '884_156692'

    def record(obj):
        key = obj.assets_file.name + ':' + str(obj.path_id)
        value = dict(source=apk.provenance(obj), payload_sha256=sha(obj.get_raw_data()))
        if key in sources:
            assert sources[key] == value
        sources[key] = value
        return obj

    def pointer(owner, value):
        return record(PPtr(assetsfile=owner.assets_file, **value).deref()) if value['m_PathID'] else None

    def source_key(obj):
        return obj.assets_file.name + ':' + str(obj.path_id)

    def transform_path(go):
        transform, = [item.component.read() for item in go.read().m_Component if item.component.deref().type.name == 'Transform']
        return (transform_path(transform.m_Father.read().m_GameObject) + '/' if transform.m_Father.path_id else '') + go.read().m_Name

    def input_file(path):
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        inputs[path.with_name(path.name + '.meta').relative_to(ROOT).as_posix()] = sha(path.with_name(path.name + '.meta').read_bytes())
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
        text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!' + str(class_id) + ' &' + str(main_id) + '\n'
        return write(relative, (text + yaml.dump({type_name: value}, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)).encode('utf-8'), main_id)

    def scriptable(relative, name, script, value):
        input_file(script)
        data = dict(source_header(), m_Enabled=1, m_EditorHideFlags=0, m_Name=name, m_EditorClassIdentifier='',
                    m_Script=ref(script, 11500000, 3), **value)
        return native(relative, 114, 11400000, 'MonoBehaviour', data)

    def placeholder(obj):
        key = source_key(obj)
        if key not in textures:
            image = obj.read()
            rgba, mips, profile = decode_mips(image)
            path = write('Textures/' + sha(obj.get_raw_data()) + '.asset', native_texture(template, image, rgba), 2800000)
            textures[key] = dict(output=path.relative_to(ROOT).as_posix(), rgba_sha256=sha(rgba), mips=mips, profile=profile)
        return ROOT / textures[key]['output']

    runtime = ROOT / 'unity/Assets/Game/Runtime'
    catalog_script = runtime / 'Offline/ProfessionUI/BattleNativeMonsterAppearanceCatalog.cs'
    if not catalog_script.with_name(catalog_script.name + '.meta').exists():
        meta(catalog_script)
    implementation = [runtime / name for name in (
        'Offline/ProfessionUI/BattleNativeMonsterAppearanceCatalog.cs',
        'Offline/ProfessionUI/BattleNativeMonsterRenderer.cs',
        'Presentation/Combat/OfficialBattleMonsterAppearance.cs',
        'Presentation/Combat/OfficialBattleLiveMonsterModelView.cs',
        'Presentation/Combat/Zjcs.Presentation.Combat.asmdef')]
    for path in implementation:
        if not path.with_name(path.name + '.meta').exists():
            meta(path)
        if path.name in ('BattleNativeMonsterAppearanceCatalog.cs', 'OfficialBattleMonsterAppearance.cs'):
            metadata = path.with_name(path.name + '.meta')
            # Unity may add its normal MonoImporter block on first discovery.
            # These two new script identities use the declared LF checkout format.
            metadata.write_text('\n'.join(line.rstrip() for line in metadata.read_text('utf-8').splitlines()) + '\n',
                encoding='utf-8', newline='\n')
        input_file(path)
    validation = ROOT / 'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleSummonGenerationValidation.cs'
    input_file(validation)
    try:
        locations = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))
        by_path = {row['path']: row for row in locations}
        by_guid = {row['guid']: row for row in locations if row['guid']}
        config_location, = [row for row in locations if row['path'].endswith('/AppearanceConfig.asset')]
        config_obj = record(apk.asset(config_location['bundle'], config_location['path']))
        config = schemas.read(config_obj)
        paths = {row['SkeletonName']: row['Path'] for row in config['mAppearanceSkeletonPaths']}
        for entity_id in sorted(set(entity_ids)):
            model = models[entity_id]
            components = {}
            for obj in apk.objects(f'prefabs_ecentity_fight_fightmonster_{entity_id}'):
                if obj.type.name == 'MonoBehaviour':
                    name = obj.read(check_read=False).m_Script.read().m_ClassName
                    components.setdefault(name, []).append(obj)
            appearance_obj, = components['AppearanceRendererLite']
            property_obj, = components['MeshMaterialProperty']
            appearance = schemas.read(record(appearance_obj))
            prop = schemas.read(record(property_obj))
            assert appearance['LevelOfResolution'] == 1 and not appearance['RenderExistingMeshes']
            assert not prop['mAppearanceRenderer']['m_PathID']
            assert pointer(property_obj, prop['mAppearanceRendererLite']).path_id == appearance_obj.path_id
            mesh_obj = pointer(property_obj, prop['MeshRenderer'])
            assert mesh_obj.read_typetree() == model['meshRenderer']
            property_path = transform_path(property_obj.read(check_read=False).m_GameObject)
            renderer_path = transform_path(appearance_obj.read(check_read=False).m_GameObject)
            assert property_path == str(entity_id) and renderer_path.startswith(str(entity_id) + '/')
            renderer_obj = pointer(appearance_obj, appearance['SkeletonRenderer'])
            renderer = read_component(renderer_obj.get_raw_data(), renderer_obj.read(check_read=False).m_Script.read().m_ClassName)
            assert renderer == model['renderer']
            data_obj = pointer(renderer_obj, renderer['skeletonDataAsset'])
            data = schemas.read(data_obj)
            skeleton_name = data_obj.read(check_read=False).m_Name.removesuffix('_SkeletonData')
            assert skeleton_name == model['skeletonName']
            atlas_obj = pointer(data_obj, data['atlasAssets'][0])
            atlas = schemas.read(atlas_obj)
            atlas_path = paths[skeleton_name] + '/' + skeleton_name + '_Atlas.asset'
            assert apk.asset(by_path[atlas_path]['bundle'], atlas_path).get_raw_data() == atlas_obj.get_raw_data()
            loader_path = paths[skeleton_name] + '/' + skeleton_name + '_TextureLoader_Low.asset'
            loader_obj = record(apk.asset(by_path[loader_path]['bundle'], loader_path))
            loader = schemas.read(loader_obj)
            assert loader['LevelOfResolution'] == appearance['LevelOfResolution'] and not loader['skeletonDataAsset']['m_PathID']
            mappings, targets = [], []
            placeholder_paths = {}
            for mapping in loader['placeholderMap']:
                original_placeholder = pointer(loader_obj, mapping['placeholderTexture'])
                target_guid = mapping['targetTextureReference']['AssetReference']['mAssetGuid']
                assert not mapping['dyeTextureReference']['AssetReference']['mAssetGuid']
                location = by_guid[target_guid]
                target_obj = record(apk.asset(location['bundle'], location['path']))
                target_key = source_key(target_obj)
                assert installed['source_extraction']['sources'][target_key]['payload_sha256'] == sha(target_obj.get_raw_data())
                target_path = native_root / model['textures'][target_key]['file']
                assert sha(target_path.read_bytes()) == installed['outputs'][target_path.relative_to(ROOT).as_posix()]
                input_file(target_path)
                targets.append(dict(Guid=target_guid, Resource='', Texture=ref(target_path, 2800000)))
                converted = placeholder(original_placeholder)
                placeholder_paths[source_key(original_placeholder)] = converted
                mappings.append(dict(placeholderTexture=ref(converted, 2800000),
                    targetTextureReference=dict(AssetReference=dict(mAssetGuid=target_guid)),
                    dyeTextureReference=dict(AssetReference=dict(mAssetGuid=''))))
            materials, original_materials = [], []
            for value in atlas['materials']:
                material_obj = pointer(atlas_obj, value)
                original = material_obj.read_typetree()
                source_shader = pointer(material_obj, original['m_Shader'])
                assert source_shader.read().m_ParsedForm.m_Name == 'Universal Render Pipeline/Spine/SkeletonEffect-2D'
                # All bindings use the same verified source program, not a replacement shader.
                assert sha(source_shader.get_raw_data()) == installed['shader']['source']['source']['object_sha256']
                input_file(shader_path)
                material = copy.deepcopy(original)
                material['m_Shader'] = ref(shader_path, 4800000, 3)
                for name, environment in material['m_SavedProperties']['m_TexEnvs']:
                    obj = pointer(material_obj, environment['m_Texture'])
                    if obj is None:
                        environment['m_Texture'] = dict(fileID=0)
                        continue
                    key = source_key(obj)
                    if name == '_MainTex':
                        assert key in placeholder_paths
                        path = placeholder_paths[key]
                    else:
                        assert installed['source_extraction']['sources'][key]['payload_sha256'] == sha(obj.get_raw_data())
                        path = native_root / model['textures'][key]['file']
                        assert sha(path.read_bytes()) == installed['outputs'][path.relative_to(ROOT).as_posix()]
                        input_file(path)
                    environment['m_Texture'] = ref(path, 2800000)
                material = dict(source_header(), **fields(material), serializedVersion=8)
                material['m_SavedProperties'] = dict(serializedVersion=3,
                    **{name: [{k: v} for k, v in entries] for name, entries in material['m_SavedProperties'].items()})
                material_path = native('Materials/' + original['m_Name'] + '.mat', 21, 2100000, 'Material', material)
                materials.append(ref(material_path, 2100000))
                original_materials.append(dict(fields=original, source_key=source_key(material_obj)))
            metadata_obj = pointer(atlas_obj, atlas['atlasFile'])
            metadata = metadata_obj.read(check_read=False).m_Script.encode('utf-8', 'surrogateescape')
            metadata_path = write('Bindings/' + skeleton_name + '.atlas.txt', metadata)
            atlas_asset = scriptable('Bindings/' + skeleton_name + '-atlas.asset', atlas['m_Name'],
                runtime / 'Offline/ProfessionUI/ProfessionNativeTextureAtlas.cs',
                dict(sourcePath=atlas_path, sourceGuid=by_path[atlas_path]['guid'], originalMetadata=ref(metadata_path, 4900000, 3), materials=materials))
            keep = {name: copy.deepcopy(loader[name]) for name in ('maxPlaceholderSize', 'unloadAfterSecondsUnused',
                    'LevelOfResolution', 'PlaceholderPath', 'TargetTexturePath', 'DyeTexturePath')}
            loader_asset = scriptable('Bindings/' + skeleton_name + '-loader-low.asset', loader['m_Name'],
                runtime / 'Official/ProfessionUI/YooAssetTextureLoader.cs',
                dict(**keep, atlasAsset=ref(atlas_asset), skeletonDataAsset=dict(fileID=0), placeholderMap=mappings))
            manager = input_file(ROOT / 'unity/Assets/Official/ProfessionUI/WeaponTextureLoader/appearance-asset-manager-config.asset')
            resources = scriptable('Bindings/' + skeleton_name + '-textures.asset', skeleton_name + ' original appearance textures',
                runtime / 'Offline/ProfessionUI/ProfessionNativeWeaponTextureResources.cs', dict(loaderPath=loader_path,
                    loaderGuid=guid(loader_asset), loader=ref(loader_asset), atlas=ref(atlas_asset), managerConfig=ref(manager), textures=targets, additionalLoaders=[]))
            binding = dict(EntityId=entity_id, SkeletonName=skeleton_name, RendererPath=renderer_path.split('/', 1)[1],
                MaterialPropertyPath='', AppearanceEnabled=appearance['m_Enabled'], MaterialPropertyEnabled=prop['m_Enabled'], Textures=ref(resources), Materials=materials)
            bindings.append(binding)
            rows.append(dict(entityId=entity_id, appearance=appearance, material_property=prop, renderer_path=renderer_path,
                loader=loader, atlas=atlas, materials=original_materials, binding=binding))
            print(f'{entity_id}: original placeholder materials, atlas, Low texture loader and root property bound', flush=True)
        scriptable('Resources/Combat/NativeMonsterAppearance/catalog.asset', 'Original native monster appearance bindings', catalog_script, dict(bindings=bindings))
        for folder in sorted({parent for path in destination.rglob('*') if path.is_file() for parent in path.parents if parent.is_relative_to(destination)}):
            sidecar = folder.with_name(folder.name + '.meta')
            if not sidecar.exists():
                meta(folder, folder=True)
            outputs[sidecar.relative_to(ROOT).as_posix()] = sha(sidecar.read_bytes())
        return dict(manifest=apk.version, generator_sha256=sha(Path(__file__).read_bytes()), source_extraction=dict(sources=sources,
            schemas=schemas.records, models=rows), placeholder_conversion=textures, inputs=inputs, outputs=outputs,
            texture_template_sha256=sha(template_path.read_bytes()))
    finally:
        apk.archive.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--entities', nargs='+', type=int, default=[3181, 3182, 3183])
    args = parser.parse_args()
    receipt = build(args.entities)
    path = ROOT / 'assets/reference/battle-animation/native-monster-appearance-source-manifest.json'
    path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print(json.dumps(dict(models=len(receipt['source_extraction']['models']), outputs=len(receipt['outputs']))))


if __name__ == '__main__':
    main()
