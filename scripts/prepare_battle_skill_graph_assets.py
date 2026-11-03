"""Convert every active-version graph referenced by an official FightSkill entity."""
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
DEST = ROOT / 'unity/Assets/Official/Combat/SkillGraphs/Resources/Combat/SkillGraphs'
DATA = ROOT / 'unity/Assets/Game/Resources/Combat/skill-native-graphs.json'
RECEIPT = ROOT / 'assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json'
EXPORT_PREFIX = Path('Prefabs/EcEntity/Fight/FightSkill')


def main():
    work = Path(os.environ['ZJCS_VALIDATION_DIR']).resolve()
    assert work.parent == (ROOT / 'output/validation').resolve()
    assert (work / '.zjcs-validation.json').is_file()

    audit = subprocess.run([sys.executable, str(AUDIT)], check=True, capture_output=True,
                           text=True, encoding='utf-8').stdout
    source = json.loads(audit)
    roots, skills = {}, []
    for row in source['skills']:
        bindings = {}
        for field, ref in row['playables'].items():
            if 'path' not in ref:
                raise ValueError(f"Official skill {row['skill_id']} has an unresolved {field} reference.")
            root_path = (EXPORT_PREFIX / ref['path']).as_posix()
            key = f"{row['skill_id']}:{field}"
            roots[key] = root_path
            bindings[field] = key
        skills.append(dict(skill_entity_id=row['skill_id'], entity_prefab=row['entity_prefab'],
                           entity_prefab_sha256=row['entity_prefab_sha256'], playables=bindings))

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
        runtime_receipts=('track-runtime-source-manifest.json',), generator=Path(__file__),
        reference_scripts={-210918009: dict(name='WwiseEventReference',
            native_asset=EVENT_ASSET,
            runtime='unity/Assets/Game/Runtime/Official/ProfessionUI/WwiseEventReference.cs',
            receipt='assets/reference/profession-ui/audio-source-manifest.json')},
        external_scripts={354902358: dict(name='SkeletonAnimation', namespace='Spine.Unity',
            native_meta='Plugins/spine-unity.dll.meta',
            runtime='unity/Assets/Spine/Runtime/spine-unity/Components/SkeletonAnimation.cs',
            source='unity/Assets/Spine/Runtime/spine-unity/Components/SkeletonAnimation.cs'),
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
        source_scripts={1708465776: dict(name='DynamicSkeletonRenderSeparator',
            namespace='AppearanceSystem.SpineExt',
            source='AppearanceSystem.SpineExt/DynamicSkeletonRenderSeparator.cs',
            runtime='unity/Assets/Game/Runtime/Official/BattleSkillPlayables/DynamicSkeletonRenderSeparator.cs'),
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

    rows = json.loads(DATA.read_text(encoding='utf-8'))['graphs']
    resources = {row['id']: row['resource'] for row in rows}
    catalog = dict(manifest=source['manifest'], skills=[])
    for skill in skills:
        catalog['skills'].append(dict(
            skill_entity_id=skill['skill_entity_id'],
            playables={field: resources[key] for field, key in skill['playables'].items()}))
    write_changed(DATA, json.dumps(catalog, ensure_ascii=False, indent=2) + '\n')
    meta(DATA)

    proof = json.loads(RECEIPT.read_text(encoding='utf-8'))
    proof['official_entity_binding_source'] = source
    proof['outputs'] = [identity(DATA) if row['path'] == DATA.relative_to(ROOT).as_posix()
                        else row for row in proof['outputs']]
    write_changed(RECEIPT, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Active-version skill graph bindings:', len(skills),
          'official skill entities;', len(roots), 'directed graph roots;',
          len(resources), 'converted graph assets.')


if __name__ == '__main__':
    main()
