"""Extract the original embedded 3205 Mecanim source and its complete native dependencies."""
import hashlib
import json
import os
import sys
from pathlib import Path

from official_apk import Apk, ROOT
from current_component_schemas import CurrentSchemas
from official_battle_spine_component import read_component
from convert_official_skeletons import reader_type
from prepare_native_character_geometry import atlas_regions
from prepare_battle_live_monsters import package
from UnityPy.classes.PPtr import PPtr
from validation_workspace import validation_directory
from official_manifest import read_asset_manifest
from prepare_unity_map13_native_scene import decode_mips
from prepare_battle_game_animators import documents, verify_graph
from prepare_battle_live_monsters import verify_monster_motion


def build():
    OUT = validation_directory()
    apk, schemas = Apk.current(), CurrentSchemas()
    assert apk.version == '884_156692'
    bundle = 'defaultpackage_assets_prefabs_ecentity_fight_fightskill_57780.bundle'
    sources = {}
    def digest(raw): return hashlib.sha256(raw).hexdigest()
    def record(obj):
        key = obj.assets_file.name + ':' + str(obj.path_id)
        sources[key] = dict(source=apk.provenance(obj), object_sha256=digest(obj.get_raw_data()))
        return obj

    def pointer(owner, value):
        return record(PPtr(**value, assetsfile=owner.assets_file).deref())
    def text(obj):
        return record(obj).read(check_read=False).m_Script.encode('utf-8', 'surrogateescape')
    def walk(go, path=''):
        record(go)
        components = [record(ref.component.deref()) for ref in go.read().m_Component]
        transform, = [obj for obj in components if obj.type.name in ('Transform', 'RectTransform')]
        yield go, path, components
        for child in transform.read().m_Children:
            child_go = child.read().m_GameObject.deref()
            name = child_go.read().m_Name
            yield from walk(child_go, path + '/' + name if path else name)

    try:
        root, = [obj for obj in apk.objects(bundle) if obj.type.name == 'GameObject'
                 and obj.read().m_Name == 'playable_16040_SourceToHit']
        nodes = list(walk(root))
        authored_active = {path: bool(go.read().m_IsActive) for go, path, components in nodes}
        assert not authored_active['track (2)'] and not authored_active['track (2)/Eff_Npc_WolfStone_Atk']
        assert authored_active['track (2)/Eff_Npc_WolfStone_Atk/MonsterModel_3205']
        renderers, component_types, appearance_fields = [], [], []
        for owner, path, components in nodes:
            for obj in components:
                if obj.type.name != 'MonoBehaviour': continue
                base = obj.read(check_read=False)
                kind = base.m_Script.read().m_ClassName
                component_types.append(dict(path=path, type=kind))
                if kind == 'AppearanceRendererLite':
                    appearance_fields.append(dict(path=path, fields=schemas.read(obj)))
                if kind == 'SkeletonMecanim': renderers.append((owner, path, components, obj))
        owner, path, components, renderer_obj = renderers.pop()
        assert not renderers, 'Review additional original embedded Mecanim instances.'
        renderer = read_component(renderer_obj.get_raw_data(), 'SkeletonMecanim')
        renderer['m_Enabled'] = bool(renderer_obj.read(check_read=False).m_Enabled)
        data_obj = pointer(renderer_obj, renderer['skeletonDataAsset'])
        data = schemas.read(data_obj)
        assert data_obj.read(check_read=False).m_Name.startswith('monster_3205')
        assert len(data['atlasAssets']) == 1
        binary = text(pointer(data_obj, data['skeletonJSON']))
        Reader, _ = reader_type()
        reader = Reader()
        skeleton = reader.convert(binary)
        assert reader.inp.index == len(binary), 'Unconsumed original skeleton data.'
        atlas_obj = pointer(data_obj, data['atlasAssets'][0])
        atlas = schemas.read(atlas_obj)
        regions = atlas_regions(text(pointer(atlas_obj, atlas['atlasFile'])).decode('utf-8'))
        payload = package(3205, data['scale'], skeleton, regions)
        native_path = OUT / 'monster_3205.native.bytes'
        native_path.write_bytes(payload)
        (OUT / 'monster_3205.original.skel.bytes').write_bytes(binary)
        mesh_obj, = [obj for obj in components if obj.type.name == 'MeshRenderer']
        mesh_renderer = mesh_obj.read_typetree()
        animator_obj, = [obj for obj in components if obj.type.name == 'Animator']
        animator = animator_obj.read_typetree()
        controller_obj = pointer(animator_obj, animator['m_Controller'])
        controller = controller_obj.read_typetree()
        assert controller_obj.type.name == 'AnimatorController'
        clips = []
        for ref in controller['m_AnimationClips']:
            clip_obj = pointer(controller_obj, ref)
            fields = clip_obj.read_typetree()
            settings = fields.get('m_AnimationClipSettings', fields.get('m_MuscleClip', {}))
            clips.append(dict(name=fields['m_Name'], type=clip_obj.type.name,
                start=settings.get('m_StartTime'), stop=settings.get('m_StopTime'),
                source=apk.provenance(clip_obj), object_sha256=digest(clip_obj.get_raw_data())))
        materials = [pointer(atlas_obj, value) for value in atlas['materials']]
        loaders = []
        material_bundles = sorted({apk.provenance(obj)['bundle'] for obj in materials})
        for material_bundle in material_bundles:
            for obj in apk.objects(material_bundle):
                if obj.type.name != 'MonoBehaviour': continue
                base = obj.read(check_read=False)
                if base.m_Name.startswith('monster_3205_TextureLoader_'):
                    record(obj)
                    loaders.append(dict(name=base.m_Name, fields=schemas.read(obj), source=apk.provenance(obj)))
        original_appearance, = appearance_fields
        selected_loader, = [value for value in loaders
            if value['fields']['LevelOfResolution'] == original_appearance['fields']['LevelOfResolution']]
        mapping, = selected_loader['fields']['placeholderMap']
        assert not mapping['dyeTextureReference']['AssetReference']['mAssetGuid']
        low_guid = mapping['targetTextureReference']['AssetReference']['mAssetGuid']
        locations = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))
        location, = [value for value in locations if value['guid'] == low_guid]
        low_obj = record(apk.asset(location['bundle'], location['path']))
        low_texture = low_obj.read()
        rgba, mips, profile = decode_mips(low_texture)
        (OUT / 'monster_3205-low.rgba.bytes').write_bytes(rgba)
        low_texture_info = dict(guid=low_guid, name=low_texture.m_Name,
            width=low_texture.m_Width, height=low_texture.m_Height,
            rgba_sha256=digest(rgba), rgba_bytes=len(rgba), profile=profile, mips=mips,
            source=apk.provenance(low_obj), object_sha256=digest(low_obj.get_raw_data()))
        native_root = ROOT / '.tools/source-packages/current' / apk.version / 'native-conversion'
        native_receipt_path = native_root / 'native-export-all-source-manifest.json'
        native_receipt = json.loads(native_receipt_path.read_bytes())
        assert native_receipt['completed'] and native_receipt['manifest'] == apk.version
        assert digest((native_root / 'source-manifest.json').read_bytes()) == native_receipt['input_manifest_sha256']
        native_export = native_root / native_receipt['export_directory']
        verified = {value['path']: value['sha256'] for value in native_receipt['outputs']}
        native_controller_path = native_export / 'ExportedProject/Assets/AppearanceAssets/Monster/monster_3205/monster_3205_Controller.controller'
        native_paths = [native_controller_path, *sorted(native_controller_path.parent.glob('*.anim'))]
        native_documents, native_names, native_inputs = {}, {}, {}
        import re
        guid_pattern = re.compile(r'guid: ([0-9a-f]{32})')
        for value in native_paths:
            raw, sidecar = value.read_bytes(), Path(str(value) + '.meta')
            assert digest(raw) == verified[value.relative_to(native_export).as_posix()]
            assert digest(sidecar.read_bytes()) == verified[sidecar.relative_to(native_export).as_posix()]
            native_documents[value] = documents(raw)
            name = next(iter(native_documents[value].values()))['m_Name']
            native_names[guid_pattern.search(sidecar.read_text('utf-8'))[1]] = name
            native_inputs[value.relative_to(ROOT).as_posix()] = dict(sha256=digest(raw), meta_sha256=digest(sidecar.read_bytes()))
        native_animator_graph = verify_graph(controller_obj, native_documents[native_controller_path], native_names, verify_monster_motion)
        native_clips = {next(iter(native_documents[value].values()))['m_Name']: next(iter(native_documents[value].values())) for value in native_paths if value.suffix == '.anim'}
        assert set(native_clips) == {value['name'] for value in clips}
        for value in clips:
            assert abs(native_clips[value['name']]['m_AnimationClipSettings']['m_StopTime'] - value['stop']) < 0.000001
        config_location, = [value for value in locations if value['path'].endswith('/AppearanceConfig.asset')]
        config_obj = record(apk.asset(config_location['bundle'], config_location['path']))
        config = schemas.read(config_obj)
        skeleton_location, = [value for value in config['mAppearanceSkeletonPaths'] if value['SkeletonName'] == 'monster_3205']
        atlas_location, = [value for value in locations if value['path'] == skeleton_location['Path'] + '/monster_3205_Atlas.asset']
        assert apk.asset(atlas_location['bundle'], atlas_location['path']).get_raw_data() == atlas_obj.get_raw_data()
        (OUT / 'monster_3205-original-controller.json').write_text(json.dumps(controller, ensure_ascii=False, indent=2), encoding='utf-8')
        (OUT / 'monster_3205-native-graph.json').write_text(json.dumps(dict(verification=native_animator_graph, inputs=native_inputs, skeleton_location=skeleton_location), ensure_ascii=False, indent=2), encoding='utf-8')
        result = dict(manifest=apk.version, bundle=bundle, authored_active=authored_active, appearance_fields=appearance_fields, texture_loaders=loaders, low_texture=low_texture_info, root=apk.provenance(root),
            root_sha256=digest(root.get_raw_data()), skeleton_path=path, renderer=renderer,
            skeleton_data=data, skeleton_data_name=data_obj.read(check_read=False).m_Name,
            atlas_location=atlas_location,
            skeleton_bytes=len(binary), skeleton_sha256=digest(binary),
            native_package_sha256=digest(payload), native_package_bytes=len(payload),
            animation_names=sorted(skeleton['animations']), bone_count=len(skeleton['bones']),
            mesh_renderer=mesh_renderer, animator=animator, controller_name=controller.get('m_Name'), clips=clips,
            materials=[dict(name=obj.read().m_Name, fields=obj.read_typetree(), source=apk.provenance(obj),
                object_sha256=digest(obj.get_raw_data())) for obj in materials],
            component_types=component_types, sources=sources)
        (OUT / 'inspection.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
        print(json.dumps(dict(native_package_bytes=len(payload), source_bones=len(skeleton['bones']),
            source_animations=sorted(skeleton['animations']), controller_clips=len(clips),
            output=str(OUT)), ensure_ascii=False), flush=True)
    finally:
        apk.archive.close()


if __name__ == "__main__":
    build()
