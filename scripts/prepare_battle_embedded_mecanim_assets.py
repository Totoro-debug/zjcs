"""Bind verified original 3205 Mecanim graph, appearance and native skeleton."""
import hashlib, json, os, re, sys
import yaml
from pathlib import Path
ROOT = Path(os.environ['ZJCS_REPOSITORY_ROOT'])
sys.path.insert(0, str(ROOT / 'scripts'))
from official_apk import Apk
from official_manifest import read_asset_manifest
from prepare_battle_game_animators import documents
from validation_workspace import validation_directory
from prepare_unity_profession_character_resources import guid, meta
from prepare_unity_profession_reward_sprites import Dumper, source_header
from prepare_unity_map13_native_scene import native_texture


def sha(raw): return hashlib.sha256(raw).hexdigest()
def build(destination):
    work = validation_directory()
    original = json.loads((work / 'inspection.json').read_bytes())
    graph = json.loads((work / 'monster_3205-native-graph.json').read_bytes())
    assert original['manifest'] == '884_156692'
    package = (work / 'monster_3205.native.bytes').read_bytes()
    assert sha(package) == original['native_package_sha256']
    assert original['bone_count'] == 42 and len(original['animation_names']) == 14
    destination = Path(destination)
    source = ROOT / 'unity/Assets/Official/Combat/SkillGraphRuntime/Resources/Combat/SkillGraphs'
    model = source / 'AppearanceAssets/Monster/monster_3205'
    prefab = source / 'Prefabs/EcEntity/Fight/FightSkill/57780/playable_16040_SourceToHit.prefab'
    assert prefab.is_file()
    prefab_docs=documents(prefab.read_bytes())
    prefab_file_id,=[key for key,value in prefab_docs.items() if value.get('m_Name')=='playable_16040_SourceToHit']
    outputs = {}
    def write(relative, raw, native=False):
        path = destination / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        if path.exists() and path.read_bytes() != raw: raise ValueError('Preserve changed embedded source: ' + str(path))
        path.write_bytes(raw)
        if not path.with_name(path.name + '.meta').exists(): meta(path, native=native)
        for item in (path, path.with_name(path.name + '.meta')):
            outputs[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
        return path
    def ref(path, kind=11400000, type_=2):
        return dict(fileID=kind, guid=guid(path), type=type_)
    def bind_asset(relative, name, script, values):
        native = source_header()
        native.update(m_Enabled=1, m_EditorHideFlags=0, m_Name=name, m_EditorClassIdentifier='',
            m_Script=dict(fileID=11500000, guid=guid(ROOT / ('unity/Assets/Game/Runtime/' + script)), type=3))
        native.update(values)
        raw = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n' +
            yaml.dump({'MonoBehaviour': native}, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)).encode('utf-8')
        return write(relative, raw, native=True)
    row = dict(entityId=3205, skeletonName=graph['skeleton_location']['SkeletonName'],
        rendererKind='SkeletonMecanim', renderer=original['renderer'], skeletonData=original['skeleton_data'],
        skins=[original['renderer']['initialSkinName'] or 'default'], transforms=[],
        meshRenderer=original['mesh_renderer'], animator=original['animator'],
        nativeController='monster_3205_Controller.controller', package='monster_3205.native.bytes',
        materials=[dict(fields=value['fields']) for value in original['materials']], textures={},
        embeddedPrefab=dict(source=original['root'], root_sha256=original['root_sha256'], path=original['skeleton_path']))
    inventory = write('inventory.json', json.dumps(dict(manifest=original['manifest'],models=[row]),ensure_ascii=False,indent=2).encode())
    native = write('monster_3205.native.bytes', package)
    shader_match = re.search(r'm_Shader: \{fileID: (\d+), guid: ([0-9a-f]{32}), type: (\d+)\}', (model / 'monster_3205_Material.mat').read_text('utf-8'))
    assert shader_match
    native_assets = bind_asset('native-assets.asset','Original embedded monster 3205',
        'Offline/ProfessionUI/BattleNativeMonsterAssets.cs',dict(inventory=ref(inventory,4900000,3),
        shader=dict(fileID=int(shader_match[1]),guid=shader_match[2],type=int(shader_match[3])),
        shaderBindings=[],bindings=[dict(File='monster_3205.native.bytes',Asset=ref(native,4900000,3)),
        dict(File='monster_3205_Controller.controller',Asset=ref(model / 'monster_3205_Controller.controller',9100000))]))
    apk=Apk.current()
    try:
        source_record=original['low_texture']['source']
        locations=read_asset_manifest(apk.source.manifest_bytes('DefaultPackage',apk.archive))
        low_location,=[value for value in locations if value['guid']==original['low_texture']['guid']]
        texture_obj=apk.asset(low_location['bundle'],low_location['path'])
        assert sha(texture_obj.get_raw_data()) == original['low_texture']['object_sha256']
        template_path = ROOT / 'unity/Assets/Official/Combat/NativeMonsters/textures/028b262edfbfcb08422b9de8d4c4fe7202b81cff5fead1aa9e6e5671fe71071d.asset'
        template = template_path.read_text('utf-8').replace('--- !u!28 &2800000','--- !u!28 &1',1)
        rgba=(work / 'monster_3205-low.rgba.bytes').read_bytes()
        assert sha(rgba)==original['low_texture']['rgba_sha256']
        texture=write('Resources/Combat/EmbeddedMonsterMecanim/monster_3205-Low.asset', native_texture(template,texture_obj.read(),rgba))
    finally: apk.archive.close()
    atlas = bind_asset('atlas.asset','monster_3205_Atlas','Offline/ProfessionUI/ProfessionNativeTextureAtlas.cs',
        dict(sourcePath=graph['skeleton_location']['Path']+'/monster_3205_Atlas.asset',sourceGuid=original['atlas_location']['guid'],
        originalMetadata=ref(model/'monster_3205.atlas.txt',4900000,3),materials=[ref(model/'monster_3205_Material.mat',2100000)]))
    selected,=[value for value in original['texture_loaders'] if value['fields']['LevelOfResolution']==1]
    fields=selected['fields']
    loader=bind_asset('monster_3205_TextureLoader_Low.asset',selected['name'],'Official/ProfessionUI/YooAssetTextureLoader.cs',
        dict(maxPlaceholderSize=fields['maxPlaceholderSize'],unloadAfterSecondsUnused=fields['unloadAfterSecondsUnused'],
        LevelOfResolution=fields['LevelOfResolution'],PlaceholderPath=fields['PlaceholderPath'],TargetTexturePath=fields['TargetTexturePath'],DyeTexturePath=fields['DyeTexturePath'],
        atlasAsset=ref(atlas),skeletonDataAsset=dict(fileID=0),placeholderMap=[dict(placeholderTexture=ref(model/'monster_3205_placeholder.texture.asset',2800000),
        targetTextureReference=dict(AssetReference=dict(mAssetGuid=original['low_texture']['guid'])),dyeTextureReference=dict(AssetReference=dict(mAssetGuid='')))]))
    loader_path=graph['skeleton_location']['Path']+'/monster_3205_TextureLoader_Low.asset'
    resources=bind_asset('texture-resources.asset','Original embedded monster 3205 low texture','Offline/ProfessionUI/ProfessionNativeWeaponTextureResources.cs',
        dict(loaderPath=loader_path,loaderGuid=guid(loader),loader=ref(loader),atlas=ref(atlas),
        managerConfig=ref(ROOT/'unity/Assets/Official/ProfessionUI/WeaponTextureLoader/appearance-asset-manager-config.asset'),
        textures=[dict(Guid=original['low_texture']['guid'],Resource='Combat/EmbeddedMonsterMecanim/monster_3205-Low',Texture=dict(fileID=0))],additionalLoaders=[]))
    bind_asset('Resources/Combat/EmbeddedMonsterMecanim/catalog.asset','Original embedded skill Mecanim source',
        'Offline/ProfessionUI/EmbeddedMonsterMecanimCatalog.cs',dict(sourcePrefab=ref(prefab,int(prefab_file_id),3),skeletonPath=original['skeleton_path'],
        modelId=3205,nativeAssets=ref(native_assets),textureResources=ref(resources)))
    for folder in sorted({parent for path in destination.rglob('*') if path.is_file() for parent in path.parents if parent.is_relative_to(destination)}):
        sidecar=folder.with_name(folder.name+'.meta')
        if not sidecar.exists(): meta(folder,folder=True)
        outputs[sidecar.relative_to(ROOT).as_posix()]=sha(sidecar.read_bytes())
    return dict(manifest=original['manifest'],source=original,controller_graph=graph,outputs=outputs)

def main():
    from prepare_battle_embedded_mecanim_sources import build as extract
    from verify_battle_skill_graph_rules import verify_selected_components
    extract()
    work = validation_directory()
    entities = {}
    for name in ('skill-rules.json', 'battle-rules.json'):
        for key, value in json.loads((ROOT / 'unity/Assets/Official/Combat' / name).read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(value)
    definitions = {key: entities[key] for key in ('57780', '57781', '57782', '57784')}
    items = json.loads((ROOT / 'unity/Assets/Official/Combat/rules.json').read_bytes())['skill']
    item, = [row for row in items if row['ClassId'] == 14657]
    assert item['EcEntityId'] == 57780
    assert [row['ClassId'] for row in definitions['57780']['FightSkillComponent']['HitList']] == [57781]
    links = verify_selected_components(definitions, [item])
    for script in ('EmbeddedMonsterMecanimCatalog.cs', 'EmbeddedMonsterMecanimResources.cs'):
        path = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI' / script
        if not Path(str(path) + '.meta').exists(): meta(path)
    destination = ROOT / 'unity/Assets/Official/Combat/EmbeddedMonsterMecanim'
    proof = build(destination)
    converted_receipt = ROOT / 'assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json'
    converted = json.loads(converted_receipt.read_bytes())
    expected = {entry['path']: entry['sha256'] for entry in converted['outputs']}
    source_root = ROOT / 'unity/Assets/Official/Combat/SkillGraphRuntime/Resources/Combat/SkillGraphs'
    referenced = [source_root / 'Prefabs/EcEntity/Fight/FightSkill/57780/playable_16040_SourceToHit.prefab',
        *sorted((source_root / 'AppearanceAssets/Monster/monster_3205').glob('*'))]
    proof['reused_converted_inputs'] = []
    for path in referenced:
        if not path.is_file(): continue
        relative = path.relative_to(ROOT).as_posix()
        assert sha(path.read_bytes()) == expected[relative], 'Existing original graph asset changed: ' + relative
        proof['reused_converted_inputs'].append(dict(path=relative, sha256=expected[relative]))
    shared = ROOT / 'unity/Assets/Official/ProfessionUI/WeaponTextureLoader/appearance-asset-manager-config.asset'
    proof['shared_appearance_infrastructure'] = dict(path=shared.relative_to(ROOT).as_posix(), sha256=sha(shared.read_bytes()),
        source_receipt='assets/reference/profession-ui/weapon-texture-loader-source-manifest.json', source_manifest='871_156598',
        scope='Existing common manager service. This non-UI Low loader does not request UI dye materials.')
    proof['implementation'] = [dict(path=path, sha256=sha((ROOT / path).read_bytes())) for path in (
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/EmbeddedMonsterMecanimCatalog.cs',
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/EmbeddedMonsterMecanimResources.cs',
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/BattleNativeMonsterRenderer.Prefab.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialEmbeddedMecanimProbe.cs',
        'unity/Assets/Game/Runtime/Presentation/CharacterCreation/CharacterCreationFlowController.cs',
        'unity/Assets/Game/Editor/OfficialEmbeddedMecanimValidation.cs')]
    proof.update(generator_sha256=sha(Path(__file__).read_bytes()),
        source_generator_sha256=sha((ROOT / 'scripts/prepare_battle_embedded_mecanim_sources.py').read_bytes()),
        original_skill_links=links,
        operation='APK-first embedded 3205 Mecanim with original graph, skeleton, materials, low texture and appearance loader. '
            'Source-prefab identity prevents same-name cross-bundle binding. No unused 57784 combat branch is activated.')
    receipt = ROOT / 'assets/reference/battle-animation/embedded-mecanim-source-manifest.json'
    receipt.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Installed verified embedded Mecanim assets:', len(proof['outputs']))


if __name__ == '__main__':
    main()
