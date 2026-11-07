"""Bind full original embedded monster skeleton data without reading animation SDK code."""
import hashlib
import json
import re
from pathlib import Path
import yaml

from official_apk import Apk, ROOT
from current_component_schemas import CurrentSchemas
from official_battle_spine_component import read_component
from official_manifest import read_asset_manifest
from convert_official_skeletons import reader_type, WHEEL_SHA256
from prepare_native_character_geometry import atlas_regions
from prepare_battle_live_monsters import package
from prepare_unity_profession_character_resources import meta
from prepare_unity_map13_native_scene import decode_mips, native_texture
from validation_workspace import validation_directory
from UnityPy.classes.PPtr import PPtr

DESTINATION = ROOT / 'unity/Assets/Official/Combat/EmbeddedMonsterSkeletons'
RESOURCE = DESTINATION / 'Resources/Combat/EmbeddedMonsterSkeletons'
GRAPH_ROOT = ROOT / 'unity/Assets/Official/Combat/SkillGraphRuntime/Resources'
RECEIPT = ROOT / 'assets/reference/battle-animation/embedded-monster-skeletons-source-manifest.json'
SELECTED = (18160, 18620, 59470, 59940)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes()))


def main():
    work = validation_directory()
    if work.parent != ROOT / 'output/validation' or not (work / '.zjcs-validation.json').is_file():
        raise ValueError('Use an owned embedded skeleton validation workspace.')
    graph_receipt = ROOT / 'assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json'
    graph = json.loads(graph_receipt.read_bytes())
    installed = {row['path']: row['sha256'] for row in graph['outputs']}
    apk, schemas = Apk.current(), CurrentSchemas()
    if apk.version != graph['manifest'] or apk.version != '884_156692':
        raise ValueError('Review the original embedded skeleton version.')
    Reader, _ = reader_type()
    sources, outputs, reused, records, textures = {}, {}, {}, [], []
    locations = {row['guid']: row for row in read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive)) if row['guid']}

    def record(obj):
        key = obj.assets_file.name + ':' + str(obj.path_id)
        sources[key] = dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()))
        return obj

    def pointer(owner, value):
        return record(PPtr(**value, assetsfile=owner.assets_file).deref())

    def text(obj):
        return record(obj).read(check_read=False).m_Script.encode('utf-8', 'surrogateescape')

    def write(path, raw):
        path.parent.mkdir(parents=True, exist_ok=True)
        if path.exists() and path.read_bytes() != raw:
            before = json.loads(RECEIPT.read_bytes())
            if before['outputs'].get(path.relative_to(ROOT).as_posix()) != sha(path.read_bytes()):
                raise ValueError('Preserve changed embedded skeleton output: ' + str(path))
        path.write_bytes(raw)
        if not Path(str(path) + '.meta').exists():
            meta(path)
        for item in (path, Path(str(path) + '.meta')):
            outputs[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())

    def walk(go, path=''):
        go = record(go)
        components = [record(ref.component.deref()) for ref in go.read().m_Component]
        transform, = [obj for obj in components if obj.type.name in ('Transform', 'RectTransform')]
        yield go, path, components
        for child in transform.read().m_Children:
            child_go = child.read().m_GameObject.deref()
            name = child_go.read().m_Name
            yield from walk(child_go, path + '/' + name if path else name)

    try:
        for entity in SELECTED:
            original, = [row for row in graph['originals'] if row['id'] == str(entity) + ':myPlayablePrefab']
            root, = [obj for obj in apk.objects(original['source']['bundle'])
                     if obj.type.name == 'GameObject' and obj.path_id == int(original['source']['path_id'])]
            if sha(root.get_raw_data()) != original['original_game_object_sha256']:
                raise ValueError('The original embedded skill root changed.')
            nodes = list(walk(root))
            extras = []
            for owner, path, components in nodes:
                for obj in components:
                    if obj.type.name != 'MonoBehaviour':
                        continue
                    kind = obj.read(check_read=False).m_Script.read().m_ClassName
                    if kind in ('AppearanceCombineSkin', 'SkeletonRendererHighTexturePreview',
                                'DynamicSkeletonRenderSeparator', 'SkeletonPartsRenderer',
                                'AttachmentFollower', 'SkeletonRendererFollower'):
                        fields = schemas.read(obj)
                        extra = dict(path=path, type=kind, fields=fields, original=obj.assets_file.name + ':' + str(obj.path_id))
                        if kind == 'SkeletonRendererHighTexturePreview':
                            loader = pointer(obj, fields['mTextureLoader'])
                            extra['loader'] = schemas.read(loader)
                        extras.append(extra)
            for owner, path, components in nodes:
                for obj in components:
                    if obj.type.name != 'MonoBehaviour' or obj.read(check_read=False).m_Script.read().m_ClassName != 'SkeletonAnimation':
                        continue
                    renderer = read_component(obj.get_raw_data(), 'SkeletonAnimation')
                    renderer['m_Enabled'] = bool(obj.read(check_read=False).m_Enabled)
                    if (renderer['updateTiming'] != 1 or renderer['clearStateOnDisable']
                            or renderer['physicsMovementRelativeTo']['m_PathID']):
                        raise ValueError('Bind additional original embedded timing/physics branches.')
                    data_obj = pointer(obj, renderer['skeletonDataAsset'])
                    data = schemas.read(data_obj)
                    if len(data['atlasAssets']) != 1 or any(data[key] for key in ('skeletonDataModifiers', 'fromAnimation', 'toAnimation', 'duration', 'zSlots')):
                        raise ValueError('Bind additional original embedded skeleton data branches.')
                    binary = text(pointer(data_obj, data['skeletonJSON']))
                    reader = Reader()
                    skeleton = reader.convert(binary)
                    if reader.inp.index != len(binary):
                        raise ValueError('Embedded original skeleton was not fully consumed.')
                    atlas_obj = pointer(data_obj, data['atlasAssets'][0])
                    atlas = schemas.read(atlas_obj)
                    if len(atlas['materials']) != 1 or atlas['textureLoadingMode'] != 0 or atlas['onDemandTextureLoader']['m_PathID']:
                        raise ValueError('Bind additional original atlas material/loading branches.')
                    regions = atlas_regions(text(pointer(atlas_obj, atlas['atlasFile'])).decode('utf-8'))
                    name = data_obj.read(check_read=False).m_Name.removesuffix('_SkeletonData')
                    package_path = RESOURCE / (name + '.bytes')
                    write(package_path, package(entity, data['scale'], skeleton, regions))
                    material_obj = pointer(atlas_obj, atlas['materials'][0])
                    material = material_obj.read()
                    material_path = GRAPH_ROOT / ('Combat/SkillGraphs/AppearanceAssets/Monster/' + name + '/' + material.m_Name + '.mat')
                    relative = material_path.relative_to(ROOT).as_posix()
                    if installed.get(relative) != sha(material_path.read_bytes()):
                        raise ValueError('Verified original converted embedded material changed: ' + relative)
                    reused[relative] = identity(material_path)
                    visible_path, high_texture = path, ''
                    local = {row['type']: row for row in extras if row['path'] == path}
                    if local:
                        combine = local['AppearanceCombineSkin']['fields']
                        separator = local['DynamicSkeletonRenderSeparator']['fields']
                        if (combine['CombineSkins'] or combine['UseRandomSkin'] or not combine['WorkOnPlaying']
                                or len(separator['partsRenderers']) != 1 or not separator['copyPropertyBlock']
                                or not separator['copyMeshRendererFlags'] or not separator['OptimizeRender']):
                            raise ValueError('Bind additional original embedded skin/separator branches.')
                        part, = [row for row in extras if row['type'] == 'SkeletonPartsRenderer' and row['path'].startswith(path + '/')]
                        visible_path = part['path']
                        loader = local['SkeletonRendererHighTexturePreview']['loader']
                        mapping, = loader['placeholderMap']
                        if loader['LevelOfResolution'] != 2 or mapping['dyeTextureReference']['AssetReference']['mAssetGuid']:
                            raise ValueError('Bind additional original embedded high texture branches.')
                        location = locations[mapping['targetTextureReference']['AssetReference']['mAssetGuid']]
                        texture_obj = record(apk.asset(location['bundle'], location['path']))
                        texture = texture_obj.read()
                        rgba, mips, profile = decode_mips(texture)
                        template_path = GRAPH_ROOT / ('Combat/SkillGraphs/AppearanceAssets/Monster/' + name + '/' + name + '_placeholder.texture.asset')
                        template = template_path.read_text('utf-8')
                        if installed.get(template_path.relative_to(ROOT).as_posix()) != sha(template_path.read_bytes()) or '  m_TextureFormat: 4\n' not in template:
                            raise ValueError('Verified current native RGBA32 texture template differs.')
                        template = template.replace('--- !u!28 &2800000\n', '--- !u!28 &1\n')
                        # The verified converted texture uses a flow mapping. The
                        # existing native serializer edits the same fields as block YAML.
                        template = re.sub(r'(?m)^  m_TextureSettings: (.+)$', lambda match:
                            '  m_TextureSettings:\n' + '\n'.join('    ' + line for line in
                                yaml.safe_dump(yaml.safe_load(match[1]), sort_keys=False).rstrip().splitlines()), template)
                        texture_path = RESOURCE / (name + '-high.asset')
                        write(texture_path, native_texture(template, texture, rgba))
                        high_texture = texture_path.relative_to(DESTINATION / 'Resources').with_suffix('').as_posix()
                        textures.append(dict(source=apk.provenance(texture_obj), object_sha256=sha(texture_obj.get_raw_data()),
                            source_guid=location['guid'], mips=mips, profile=profile, rgba_sha256=sha(rgba),
                            template=identity(template_path), output=identity(texture_path)))
                    mesh, = [component for component in components if component.type.name == 'MeshRenderer']
                    records.append(dict(skill_id=entity, prefab=root.read().m_Name, path=path, visible_path=visible_path,
                        skeleton_data_name=name, scale=data['scale'], renderer=renderer, mesh_renderer=mesh.read_typetree(),
                        package=package_path.relative_to(DESTINATION / 'Resources').with_suffix('').as_posix(),
                        material=material_path.relative_to(GRAPH_ROOT).with_suffix('').as_posix(), high_texture=high_texture,
                        skins=[renderer['initialSkinName'] or 'default'], original_components=extras,
                        original=obj.assets_file.name + ':' + str(obj.path_id),
                        counts={key: len(skeleton.get(key, [])) for key in ('bones', 'slots', 'skins', 'animations', 'ik', 'transform', 'path', 'physics')}))
            print('Original embedded skill recovered:', entity, flush=True)
    finally:
        apk.archive.close()
    if len(records) != 8:
        raise ValueError('Review embedded monster renderer coverage.')
    payload = RESOURCE / 'bindings.json'
    write(payload, (json.dumps(dict(manifest='884_156692', graphs=records), ensure_ascii=False, indent=2) + '\n').encode())
    for folder in sorted({parent for file in outputs for parent in (ROOT / file).parents if parent.is_relative_to(DESTINATION)}, reverse=True):
        if not Path(str(folder) + '.meta').exists():
            meta(folder, folder=True)
        outputs[Path(str(folder) + '.meta').relative_to(ROOT).as_posix()] = sha(Path(str(folder) + '.meta').read_bytes())
    RECEIPT.write_text(json.dumps(dict(manifest='884_156692', generator=identity(Path(__file__).resolve()),
        original_graph_receipt=identity(graph_receipt), sources=sources, component_schemas=schemas.records,
        decoder=dict(name='SkelToJson', version='1.0.1', license='MIT', wheel_sha256=WHEEL_SHA256),
        reused=list(reused.values()), textures=textures, outputs=outputs,
        implementation=[identity(ROOT / path) for path in (
            'unity/Assets/Game/Runtime/Offline/ProfessionUI/EmbeddedMonsterNativeSkeleton.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillGraphPlayback.cs',
            'unity/Assets/Game/Editor/OfficialEmbeddedMonsterSkeletonValidation.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleEmbeddedMonsterValidation.cs',
            'unity/Assets/Game/Runtime/Presentation/Home/SkillSummonPlayerValidation.cs')],
        operation='Read original serialized metadata and complete skeleton/atlas data through APK-first resolution. '
                  'Retain original material and texture references; only the original explicit high loader changes its texture. '
                  'Preserve all independent skeleton instances and original separator/follower bindings. No animation SDK code is read.'),
        ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Prepared embedded monster skeletons:', len(records), 'instances across', len(SELECTED), 'skill prefabs.')


if __name__ == '__main__':
    main()
