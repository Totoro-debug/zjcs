"""Bind original embedded partner skeletons to the independent native geometry endpoint.

Read serialized component metadata and art only; never read animation SDK code.
"""
import copy
import hashlib
import io
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from inspect_partner_native_skill_skeletons import main as inspect_graphs
from prepare_unity_profession_character_resources import meta


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def normalize_metadata(path):
    path.write_text('\n'.join(line.rstrip() for line in path.read_text('utf8').splitlines()) + '\n', encoding='utf8', newline='\n')


def main():
    inspected = inspect_graphs()
    embedded = [(graph, node, component) for graph in inspected['graphs'] for node in graph['nodes']
        for component in node['components'] if component['type'] in ('SkeletonAnimation', 'SkeletonMecanim')]
    if len(embedded) != 1:
        raise ValueError('Review original embedded partner skeleton coverage')
    graph, node, animation = embedded[0]
    if graph['skill_id'] != 58550 or animation['skeleton_data_asset']['name'] != 'npc_11007_SkeletonData':
        raise ValueError('Review original embedded partner skeleton identity')
    native = ROOT / 'unity/Assets/Official/Combat/NativePartners'
    actor = next(row for row in json.loads((native / 'catalog.json').read_bytes())['fighters'] if row['npcId'] == 11007)
    if actor['renderer']['skeletonDataAsset']['m_PathID'] != animation['fields']['skeletonDataAsset']['m_PathID']:
        raise ValueError('Embedded skill and actor use different original skeleton data')
    parts, = [n for n in graph['nodes'] if n['path'] == node['path'] + '/0']
    components = {c['type']: c for c in node['components']}
    combine = components['AppearanceCombineSkin']['fields']
    separator = components['DynamicSkeletonRenderSeparator']['fields']
    settings = copy.deepcopy(animation['fields'])
    if (combine['CombineSkins'] or combine['UseRandomSkin'] or settings['initialSkinName']
            or settings['separatorSlotNames'] or settings['clearStateOnDisable']
            or settings['updateTiming'] != 1 or settings['unscaledTime']
            or len(separator['partsRenderers']) != 1 or not separator['copyMeshRendererFlags']
            or not separator['copyPropertyBlock'] or not separator['OptimizeRender']):
        raise ValueError('Bind additional original embedded animation/skin/separator branches')
    skeleton = json.loads((native / actor['skeleton']).read_bytes())
    if skeleton.get('physics') or skeleton['animations'][settings['_animationName']].get('events'):
        raise ValueError('Bind embedded physics/events before drawing')
    settings['animationName'] = settings.pop('_animationName')
    loader = components['SkeletonRendererHighTexturePreview']['texture_loader']['fields']
    mapping, = loader['placeholderMap']
    if loader['LevelOfResolution'] != 2 or mapping['dyeTextureReference']['AssetReference']['mAssetGuid']:
        raise ValueError('Review original embedded high texture binding')
    texture_guid = mapping['targetTextureReference']['AssetReference']['mAssetGuid']
    destination = ROOT / 'unity/Assets/Official/Combat/NativePartnerAssets/Resources/Combat/NativePartnerAssets'
    apk = Apk.current()
    try:
        if apk.version != inspected['manifest']:
            raise ValueError('Preserve selected partner source version')
        asset, = [row for row in read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))
            if row['guid'] == texture_guid]
        obj = apk.asset(asset['bundle'], asset['path'])
        texture = obj.read()
        if texture.m_MipCount != 1:
            raise ValueError('Restore every embedded high texture mip')
        pixels = texture.image.convert('RGBA')
        buffer = io.BytesIO(); pixels.save(buffer, format='PNG')
        target = destination / 'npc-11007-skill-high.png'
        target.write_bytes(buffer.getvalue())
        if not Path(str(target) + '.meta').exists():
            meta(target)
        original_texture = dict(source=apk.provenance(obj), object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(),
            guid=texture_guid, name=texture.m_Name, width=texture.m_Width, height=texture.m_Height,
            encoded_sha256=hashlib.sha256(texture.get_image_data()).hexdigest(),
            rgba_sha256=hashlib.sha256(pixels.tobytes()).hexdigest(), mip_count=texture.m_MipCount,
            color_space=texture.m_ColorSpace, readable=texture.m_IsReadable,
            filter_mode=texture.m_TextureSettings.m_FilterMode, aniso=texture.m_TextureSettings.m_Aniso,
            mip_bias=texture.m_TextureSettings.m_MipBias, wrap_u=texture.m_TextureSettings.m_WrapU,
            wrap_v=texture.m_TextureSettings.m_WrapV, wrap_w=texture.m_TextureSettings.m_WrapW)
    finally:
        apk.archive.close()
    material, = node['mesh_renderers'][0]['materials']
    actor_material, = json.loads((native / actor['materials']).read_bytes())
    original_proof = json.loads((ROOT / 'assets/reference/partner-native-battle-source-manifest.json').read_bytes())
    if (json.loads(json.dumps(material['fields'])) != actor_material['fields']
            or original_proof['sources'][material['source_key']]['payload_sha256'] !=
                inspected['sources'][material['source_key']]['object_sha256']):
        raise ValueError('Embedded original material differs from the actor material')
    followers = [dict(path=n['path'].split('/', 2)[2], source_key=c['source_key'], fields=c['fields'])
        for n in graph['nodes'] for c in n['components'] if c['type'] == 'AttachmentFollower']
    row = dict(skill_id=graph['skill_id'], field=graph['field'], prefab=node['path'].split('/')[1],
        path=node['path'].split('/', 2)[2], parts_path=parts['path'].split('/', 2)[2],
        npc_id=11007, skeleton_data_name=animation['skeleton_data_asset']['name'], renderer=settings,
        mesh_renderer=node['mesh_renderers'][0]['fields'], skins=['default'],
        high_texture='Combat/NativePartnerAssets/npc-11007-skill-high', texture=original_texture,
        followers=followers, original_components=node['components'], original_parts=parts['components'])
    payload = destination / 'skill-skeleton-bindings.json'
    payload.write_text(json.dumps(dict(manifest=apk.version, graphs=[row]), ensure_ascii=False, indent=2) + '\n', encoding='utf8', newline='\n')
    if not Path(str(payload) + '.meta').exists():
        meta(payload)
    for path in (Path(str(payload) + '.meta'), Path(str(target) + '.meta')):
        normalize_metadata(path)
    inputs = [native / 'catalog.json', native / actor['package'], native / actor['materials'], native / actor['skeleton'],
        ROOT / 'scripts/inspect_partner_native_skill_skeletons.py',
        ROOT / 'unity/Assets/Official/Combat/NativePartnerAssets/Resources/Combat/NativePartnerAssets/catalog.asset',
        ROOT / 'assets/reference/partner-native-battle-assets-source-manifest.json']
    inputs += [ROOT / '.tools/source-packages/official-code/client-source' / name for name in
        ('SkeletonRendererHighTexturePreview.cs', 'AppearanceCombineSkin.cs', 'AppearanceSystem.SpineExt/DynamicSkeletonRenderSeparator.cs')]
    inputs.append(ROOT / '.tools/source-packages/official-code/aot-metadata/DummyDll/spine-unity.dll')
    inputs += [ROOT / 'unity/Assets/Game/Runtime' / name for name in
        ('Offline/ProfessionUI/PartnerNativeBattleActor.cs', 'Offline/ProfessionUI/PartnerNativeBattleMesh.cs',
         'Offline/ProfessionUI/PartnerNativeBattleRendererPort.cs', 'Offline/ProfessionUI/PartnerNativeSkillSkeleton.cs',
         'Offline/BattleNativePlayables/OfflineBattleNativePlayableScope.cs',
         'Presentation/Combat/OfficialBattleSkillGraphPlayback.cs')]
    proof = dict(manifest=apk.version, generator=identity(Path(__file__).resolve()),
        inputs=[identity(path) for path in inputs], sources=inspected['sources'],
        component_schemas=inspected['component_schemas'], component_counts=inspected['component_counts'],
        graph_count=len(inspected['graphs']), embedded_skeleton_count=len(embedded),
        update_timing_metadata=dict(type='Spine.Unity.UpdateTiming', ManualUpdate=0, InUpdate=1, InFixedUpdate=2, InLateUpdate=3,
            extraction='Enum Field/Constant metadata only; no SDK method body inspected.'),
        outputs=[identity(path) for path in (payload, Path(str(payload) + '.meta'), target, Path(str(target) + '.meta'))],
        operation='Preserve original prefab hierarchy; bind the same original NPC skeleton package, high atlas texture, animation settings, single separator child and attachment follower before Awake. Normalize trailing YAML whitespace in installed metadata. Independent native geometry only; no SDK algorithms read or reconstructed.')
    receipt = ROOT / 'assets/reference/partner-native-skill-skeletons-source-manifest.json'
    receipt.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf8', newline='\n')
    print('Bound one original embedded partner skeleton across 93 skill prefab variants:', original_texture)


if __name__ == '__main__':
    main()
