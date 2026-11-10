"""Convert complete official skill/hit entities and their original graph closure."""
import json
import os
import subprocess
import sys
from pathlib import Path

from official_apk import ROOT
from prepare_battle_cast_assets import EVENT_ASSET
from prepare_battle_status_orbit_assets import SCRIPTS, build
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed

AUDIT = ROOT / 'scripts/inspect_battle_skill_graph_bindings.py'
DEST = ROOT / 'unity/Assets/Official/Combat/SkillGraphRuntime/Resources/Combat/SkillGraphs'
DATA = ROOT / 'unity/Assets/Game/Resources/Combat/skill-native-graphs.json'
RECEIPT = ROOT / 'assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json'
EXPORT_PREFIX = Path('Prefabs/EcEntity/Fight/FightSkill')


def main():
    work = Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()
    assert work.parent == (ROOT / 'output/validation').resolve()
    assert (work / '.zjcs-validation.json').is_file()

    print("SkillGraphs: auditing official entity bindings", flush=True)
    audit = subprocess.run([sys.executable, str(AUDIT)], check=True, capture_output=True,
                           text=True, encoding='utf-8').stdout
    source = json.loads(audit)
    (work/'skill-graph-binding-audit.json').write_text(audit, encoding='utf-8')
    print(f"SkillGraphs: audited {len(source['skills'])} entities", flush=True)
    roots, skills, skill_entities = {}, [], {}
    for row in source['skills']:
        entity_key = f"skill:{row['skill_id']}:entity"
        entity_path = (EXPORT_PREFIX / row['entity_prefab']).as_posix()
        roots[entity_key] = entity_path
        skill_entities[row['skill_id']] = entity_path
        bindings = {}
        for field, ref in row['playables'].items():
            if 'path' not in ref:
                raise ValueError(f"Official skill {row['skill_id']} has an unresolved {field} reference.")
            root_path = (EXPORT_PREFIX / ref['path']).as_posix()
            key = f"{row['skill_id']}:{field}"
            roots[key] = root_path
            bindings[field] = key
        skills.append(dict(skill_entity_id=row['skill_id'], entity_key=entity_key, entity_prefab=row['entity_prefab'],
                           entity_prefab_sha256=row['entity_prefab_sha256'], playables=bindings))

    from inspect_battle_hit_graph_bindings import inspect as inspect_hits
    hit_source = inspect_hits()
    (work / 'hit-graph-binding-audit.json').write_text(
        json.dumps(hit_source, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    hits = []
    for row in hit_source['hits']:
        entity_key = f"hit:{row['hit_entity_id']}:entity"
        roots[entity_key] = (EXPORT_PREFIX / row['entity_prefab']).as_posix()
        bindings = {}
        for field, ref in row['playables'].items():
            key = f"hit:{row['hit_entity_id']}:{row['component_file_id']}:{field}"
            roots[key] = (EXPORT_PREFIX / ref['path']).as_posix()
            bindings[field] = key
        hits.append(dict(hit_entity_id=row['hit_entity_id'], client_type=row['client_type'],
            entity_key=entity_key,
            component_file_id=row['component_file_id'], game_object_file_id=row['game_object_file_id'],
            component_enabled=row['component_enabled'], source_to_me_anchor=row['source_to_me_anchor'],
            flags=row['flags'], playables=bindings))
    print(f"SkillGraphs: audited {len(hits)} original hit view bindings", flush=True)

    from inspect_battle_action_graph_bindings import inspect as inspect_actions
    rule_entities = json.loads((ROOT / 'unity/Assets/Official/Combat/battle-rules.json').read_bytes())['entities']
    action_ids = sorted(int(key) for key, components in rule_entities.items()
                        if 'FightActionSkillMoveComponent' in components)
    action_source = inspect_actions(action_ids)
    (work / 'action-graph-binding-audit.json').write_text(
        json.dumps(action_source, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    moves = []
    for row in action_source['actions']:
        action_id = row['action_entity_id']
        if row['view']['enabled'] != 1:
            raise ValueError(f'Review an originally disabled movement view: {action_id}')
        entity_key = f'action:{action_id}:entity'
        roots[entity_key] = (EXPORT_PREFIX / row['entity_prefab']).as_posix()
        bindings = {}
        for field, ref in row['view']['playables'].items():
            key = f'action:{action_id}:{field}'
            roots[key] = (EXPORT_PREFIX / ref['path']).as_posix()
            bindings[field] = key
        moves.append(dict(action_entity_id=action_id, entity_key=entity_key, playables=bindings))
    print(f'SkillGraphs: audited {len(moves)} original movement action bindings', flush=True)

    from battle_skill_entity_prefabs import SkillEntityManagedViews, skill_entity_scripts
    managed_views = SkillEntityManagedViews(skill_entities)

    mapped = dict(SCRIPTS)
    mapped.update({-822603460: ('StoryPlayables', 'ApplyBySource'),
        -94696243: ('StoryAnimation', 'MyPlayableOverrideAnimator'),
        205640859: ('StoryAnimation', 'MyPlayableAnimator'),
        1616130977: ('BattleSkillPlayables', 'AppearanceCombineSkin'),
        -424924989: ('BattleSkillPlayables', 'LinkEffect'),
        1913273902: ('BattleSkillPlayables', 'LinkEffectActiveConditionBase'),
        -383121192: ('BattleSkillPlayables', 'LinkEffectActiveConditionOnPlayablePlay'),
        -644764131: ('BattleSkillPlayables', 'MyPlayableCharacterPartVisibility'),
        1391534298: ('BattleSpinePlayables', 'MyPlayableFollowSpinePart'),
        -1421533442: ('BattleSpinePlayables', 'AttachmentFollower'),
        696733366: ('BattleSkillPlayables', 'MyPlayableCameraMove'),
        -1211289337: ('StoryFollowBone', 'ApplyOnStop'),
        -61675510: ('BattleSkillPlayables', 'MyPlayableFightCameraFollowerWeightModifier'),
        -782967297: ('BattleSkillPlayables', 'MyPlayableSpineBoneLookAt'),
        1382981218: ('BattleSkillPlayables', 'MyPlayableSpineClone'),
        -1957709185: ('BattleSkillPlayables', 'PlayableSpinePlayerCloneInit'),
        1122795596: ('StoryFollowBone', 'ApplyOnStopCheckSourceDie'),
        -1860136715: ('ProfessionUI', 'AppearanceAnimationPartHandler'),
        -713493980: ('BattleSkillPlayables', 'MyPlayableAfterimage'),
        -547320074: ('BattleSkillPlayables', 'MyPlayableFightCameraPerformance'),
        100375930: ('BattleStatusPlayables', 'ApplyOverrideToMaster'),
        2012015779: ('StoryAttachment', 'MyPlayableAttachToBone'),
        -1858338532: ('StoryAttachment', 'MyPlayableActiveBone'),
        1513771027: ('ProfessionUI', 'SkeletonRendererFollower'),
        495083993: ('StoryPlayables', 'MyPlayableLookAt'),
        666328883: ('World/Map13TreePlayables', 'ApplyByBone'),
        1640720687: ('StorySounds', 'MyPlayableSound'),
        -741389653: ('BattleTrackPlayables', 'MyPlayableTrack'),
        602813528: ('BattleTrackPlayables', 'MyTrack'),
        1801709843: ('BattleTrackPlayables', 'MyCurveTrack'),
        180732079: ('BattleTrackPlayables', 'AnimationTrack'),
        116049708: ('BattleTrackPlayables', 'ParabolicTrack')})
    build(DEST=DEST, DATA=DATA, RECEIPT=RECEIPT, ROOTS={}, graph_roots=roots,
        MAPPED=mapped, family='SkillGraphs', shader_prefix='SkillGraph_',
        native_preprocess=managed_views.preprocess,
        runtime_receipts=('track-runtime-source-manifest.json',
            'spine-clone-runtime-source-manifest.json',
            'hit-view-runtime-source-manifest.json',
            'skill-prefab-runtime-source-manifest.json',
            'skill-move-runtime-source-manifest.json'), generator=Path(__file__),
        reference_scripts={-210918009: dict(name='WwiseEventReference',
            native_asset=EVENT_ASSET,
            runtime='unity/Assets/Game/Runtime/Official/ProfessionUI/WwiseEventReference.cs',
            receipt='assets/reference/profession-ui/audio-source-manifest.json'),
            -1752031980: dict(name='AkEvent',
                native_asset='Prefabs/EcEntity/Fight/FightSkill/11030/playable11031.prefab',
                runtime='unity/Assets/Game/Runtime/Official/ProfessionUI/AkEvent.cs',
                receipt='assets/reference/profession-ui/audio-source-manifest.json'),
            -130519023: dict(name='AkGameObj',
                native_asset='Prefabs/EcEntity/Fight/FightSkill/11030/playable11031.prefab',
                runtime='unity/Assets/Game/Runtime/Official/ProfessionUI/AkGameObj.cs',
                receipt='assets/reference/profession-ui/audio-source-manifest.json')},
        external_scripts={354902358: dict(name='SkeletonAnimation', namespace='Spine.Unity',
            native_meta='Plugins/spine-unity.dll.meta',
            runtime='unity/Assets/Spine/Runtime/spine-unity/Components/SkeletonAnimation.cs',
            source='unity/Assets/Spine/Runtime/spine-unity/Components/SkeletonAnimation.cs'),
            1791324088: dict(name='SkeletonMecanim', namespace='Spine.Unity',
                native_meta='Plugins/spine-unity.dll.meta',
                runtime='unity/Assets/Spine/Runtime/spine-unity/Components/SkeletonMecanim.cs',
                source='unity/Assets/Spine/Runtime/spine-unity/Components/SkeletonMecanim.cs',
                verify_file_id_hash=True),
            337831424: dict(name='TimelineAsset', namespace='UnityEngine.Timeline',
                native_meta='Plugins/Unity.Timeline.dll.meta',
                runtime='unity/Packages/com.unity.timeline/Runtime/TimelineAsset.cs',
                source='unity/Packages/com.unity.timeline/Runtime/TimelineAsset.cs',
                verify_file_id_hash=True),
            106799970: dict(name='SkeletonDataAsset', namespace='Spine.Unity',
            native_meta='Plugins/spine-unity.dll.meta',
            runtime='unity/Assets/Spine/Runtime/spine-unity/Asset Types/SkeletonDataAsset.cs',
            source='unity/Assets/Spine/Runtime/spine-unity/Asset Types/SkeletonDataAsset.cs'),
            1497261852: dict(name='SpineAtlasAsset', namespace='Spine.Unity',
            native_meta='Plugins/spine-unity.dll.meta',
            runtime='unity/Assets/Spine/Runtime/spine-unity/Asset Types/SpineAtlasAsset.cs',
            source='unity/Assets/Spine/Runtime/spine-unity/Asset Types/SpineAtlasAsset.cs'),
            1339610166: dict(name='SkeletonPartsRenderer', namespace='Spine.Unity',
            native_meta='Plugins/spine-unity.dll.meta',
            runtime='unity/Assets/Spine/Runtime/spine-unity/Components/SkeletonRenderSeparator/SkeletonPartsRenderer.cs',
            source='unity/Assets/Spine/Runtime/spine-unity/Components/SkeletonRenderSeparator/SkeletonPartsRenderer.cs'),
            502961096: dict(name='SkeletonRendererHighTexturePreview', namespace='',
            native_meta='Plugins/spine-unity.dll.meta',
            runtime='unity/Assets/Game/Runtime/Official/BattleSkillPlayables/SkeletonRendererHighTexturePreview.cs',
            source='unity/Assets/Game/Runtime/Official/BattleSkillPlayables/SkeletonRendererHighTexturePreview.cs',
            verify_file_id_hash=True)},
        source_scripts={**hit_entity_scripts(), **skill_entity_scripts(), **action_entity_scripts(),
            1126251125: dict(name='MyPtSubPlayable', namespace='GameFramework',
                source_root='game-framework-source/GameFramework', source='MyPtSubPlayable.cs',
                target_namespace='Zjcs.Official.StoryPlayableManager',
                runtime='unity/Assets/Game/Runtime/Official/StoryPlayableManager/MyPtSubPlayable.cs'),
            2132115665: dict(name='MyPtStopPlayable', namespace='GameFramework',
                source_root='game-framework-source/GameFramework', source='MyPtStopPlayable.cs',
                target_namespace='Zjcs.Official.StoryPlayableManager',
                runtime='unity/Assets/Game/Runtime/Official/StoryPlayableManager/MyPtStopPlayable.cs'),
            34885126: dict(name='AppearanceRendererLite', namespace='AppearanceSystem',
                source='AppearanceSystem/AppearanceRendererLite.cs', target_namespace='Zjcs.Official.ProfessionUI',
                runtime='unity/Assets/Game/Runtime/Official/ProfessionUI/AppearanceRendererLite.cs'),
            1708465776: dict(name='DynamicSkeletonRenderSeparator',
            namespace='AppearanceSystem.SpineExt',
            source='AppearanceSystem.SpineExt/DynamicSkeletonRenderSeparator.cs',
            runtime='unity/Assets/Game/Runtime/Official/BattleSkillPlayables/DynamicSkeletonRenderSeparator.cs'),
            2111475641: dict(name='AppearanceCharacter', namespace='AppearanceSystem.Runtime',
            target_namespace='Zjcs.Official.CombatAppearance',
            source='AppearanceSystem.Runtime/AppearanceCharacter.cs',
            runtime='unity/Assets/Game/Runtime/Official/CombatAppearance/AppearanceCharacter.cs'),
            -1254263949: dict(name='AppearanceAnimation', namespace='AppearanceSystem.Runtime',
            target_namespace='', source='AppearanceSystem.Runtime/AppearanceAnimation.cs',
            runtime='unity/Assets/Game/Runtime/Official/BattleSkillPlayables/AppearanceAnimation.cs'),
            2084312037: dict(name='PreviewTool', namespace='', source_root='framework-source',
            source='PreviewTool.cs',
            runtime='unity/Assets/Game/Runtime/Official/BattleSkillPlayables/PreviewTool.cs'),
            -1369127073: dict(name='YooAssetTextureLoader', namespace='AppearanceSystem.Runtime',
            target_namespace='Zjcs.Official.ProfessionUI',
            source='AppearanceSystem.Runtime/YooAssetTextureLoader.cs',
            runtime='unity/Assets/Game/Runtime/Official/ProfessionUI/YooAssetTextureLoader.cs'),
            -624567827: dict(name='MyPlayableGroundEffect', namespace='',
            source='MyPlayableGroundEffect.cs',
            runtime='unity/Assets/Game/Runtime/Official/BattleSkillPlayables/MyPlayableGroundEffect.cs'),
            -194148604: dict(name='MyPlayableApplyObj', namespace='GameFramework',
            source_root='game-framework-source/GameFramework', source='MyPlayableApplyObj.cs',
            target_namespace='Zjcs.Official.StoryPlayables',
            runtime='unity/Assets/Game/Runtime/Official/StoryPlayables/MyPlayableApplyObj.cs'),
            2062756666: dict(name='ApplyByCustom', namespace='GameFramework',
            source_root='game-framework-source/GameFramework', source='ApplyByCustom.cs',
            target_namespace='Zjcs.Official.StoryPlayables',
            runtime='unity/Assets/Game/Runtime/Official/StoryPlayables/ApplyByCustom.cs'),
            1420873720: dict(name='TargetSourceInitByGameCamera', namespace='',
            source='TargetSourceInitByGameCamera.cs',
            runtime='unity/Assets/Game/Runtime/Official/BattleSkillPlayables/TargetSourceInitByGameCamera.cs'),
            -1154579867: dict(name='SpineBoneAfterimageRTParamsProvider', namespace='',
            source='SpineBoneAfterimageRTParamsProvider.cs',
            runtime='unity/Assets/Game/Runtime/Official/BattleSkillPlayables/SpineBoneAfterimageRTParamsProvider.cs'),
            -1055498065: dict(name='RTRenderer', namespace='', source='RTRenderer.cs',
            runtime='unity/Assets/Game/Runtime/Official/BattleSkillPlayables/RTRenderer.cs'),
            -2038699262: dict(name='GrapplingLineCtrl', namespace='', source='GrapplingLineCtrl.cs',
            runtime='unity/Assets/Game/Runtime/Official/BattleSkillPlayables/GrapplingLineCtrl.cs'),
            1371266483: dict(name='MyPlayableGrapplingLine', namespace='', source='MyPlayableGrapplingLine.cs',
            runtime='unity/Assets/Game/Runtime/Official/BattleSkillPlayables/MyPlayableGrapplingLine.cs')},
        limits=['Converted roots are taken from active-version FightSkill entity prefab fields. '
                'Production battle entity binding and runtime Playable lifecycle are tracked separately.'])
    if managed_views.restore_count != {54383: 2}:
        raise ValueError('Original skill managed views must be restored for dependency scanning and conversion.')

    rows = json.loads(DATA.read_text(encoding='utf-8'))['graphs']
    resources = {row['id']: row['resource'] for row in rows}
    catalog = dict(manifest=source['manifest'], skills=[])
    for skill in skills:
        catalog['skills'].append(dict(
            skill_entity_id=skill['skill_entity_id'],
            entity_resource=resources[skill['entity_key']],
            # JsonUtility cannot deserialize dynamic object keys (the original
            # fields are default/0/1/2/3), so persist the same binding as a
            # deterministic typed list consumed by Unity.
            playables=[dict(field=field, resource=resources[key])
                       for field, key in skill['playables'].items()]))
    catalog['hits'] = []
    for row in hits:
        entry = dict(row)
        entry['entity_resource'] = resources[entry.pop('entity_key')]
        entry['playables'] = [dict(field=field, resource=resources[key])
            for field, key in row['playables'].items()]
        catalog['hits'].append(entry)
    catalog['moves'] = []
    for row in moves:
        catalog['moves'].append(dict(action_entity_id=row['action_entity_id'],
            entity_resource=resources[row['entity_key']],
            playables=[dict(field=field, resource=resources[key])
                       for field, key in row['playables'].items()]))
    write_changed(DATA, json.dumps(catalog, ensure_ascii=False, indent=2) + '\n')
    meta(DATA)

    proof = json.loads(RECEIPT.read_text(encoding='utf-8'))
    proof['official_entity_binding_source'] = source
    proof['official_hit_entity_binding_source'] = hit_source
    proof['official_move_entity_binding_source'] = action_source
    proof['original_skill_managed_views'] = dict(
        registrations=managed_views.registrations, pointers=managed_views.pointer_bindings,
        schemas=managed_views.schemas.records,
        adapter=identity(ROOT / 'scripts/battle_skill_entity_prefabs.py'))
    proof['outputs'] = [identity(DATA) if row['path'] == DATA.relative_to(ROOT).as_posix()
                        else row for row in proof['outputs']]
    write_changed(RECEIPT, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    from prepare_battle_legacy_animation_bindings import prepare as prepare_legacy_animations
    prepare_legacy_animations()
    print('Active-version skill graph bindings:', len(skills),
          'official skill entities;', len(roots), 'directed graph roots;',
          len(set(resources.values())), 'distinct converted graph assets.')


def hit_entity_scripts():
    from inspect_battle_hit_graph_bindings import MD4
    import struct
    names = {
        'EntityLifecycle': ('MyEntity', 'MyGridTransform', 'MyTransform'),
        'BattleGridItemEntities': ('MyFightGameplayTag',),
        'BattleSkillPlayables': ('MyFightHitFixed', 'MyFightHitChained', 'MyFightHitSummon',
            'MyFightHitRandomTarget', 'MyFightHitCustomTarget', 'MyFightHitTriggerSkill',
            'MyFightHitExtHemophagia', 'MyFightHitTransientPropDiff',
            'MyFightHitTransientPropUpByCharacterType', 'MyFightDamageLimit',
            'MyFightHitExtApplyDamage', 'ApplySkillHitOffsetForPlayableTrackOnInit',
            'OverridePlayableTrackByHitOnInit', 'OverridePlayableAttachOffsetByHitOnInit')}
    return {struct.unpack('<i', MD4.new(('s\0\0\0' + name).encode()).digest()[:4])[0]:
        dict(name=name, namespace='', target_namespace='Zjcs.Official.' + folder,
             source=name + '.cs',
             runtime='unity/Assets/Game/Runtime/Official/' + folder + '/' + name + '.cs')
        for folder, entries in names.items() for name in entries}


def action_entity_scripts():
    from inspect_battle_hit_graph_bindings import MD4
    import struct
    return {struct.unpack('<i', MD4.new(('s\0\0\0' + name).encode()).digest()[:4])[0]:
        dict(name=name, namespace='', target_namespace='Zjcs.Official.' + folder,
             source=name + '.cs',
             runtime='unity/Assets/Game/Runtime/Official/' + folder + '/' + name + '.cs')
        for name, folder in (('MyAction', 'EntityActions'),
                             ('MyFightActionSkillMove', 'BattleSkillPlayables'))}


if __name__ == '__main__':
    main()
