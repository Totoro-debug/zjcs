"""Extract the selected original skill-level and per-cell movement Animator playables.

Only application components are read; no animation SDK implementation is used.
"""
import json
from pathlib import Path

from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed
from UnityPy.classes import PPtr

OUTPUT = ROOT / 'unity/Assets/Game/Resources/Combat/move-animators.json'
RECEIPT = ROOT / 'assets/reference/battle-animation/move-animators-source-manifest.json'


def main():
    apk, schemas = Apk.current(), CurrentSchemas()
    rules_path = ROOT / 'unity/Assets/Official/Combat/battle-rules.json'
    entities = json.loads(rules_path.read_bytes())['entities']
    skills = {int(value['FightRoleCharacterComponent'].get(key, 0))
              for value in entities.values() if 'FightRoleCharacterComponent' in value
              for key in ('MoveSkillId', 'BackMoveSkillId', 'FixSpeedMoveSkillId')} - {0}
    actions = {int(row['MoveBehaviourId']) for key in skills
               for row in entities[str(key)]['FightSkillComponent']['SourceMoveList']}
    assets = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))
    inputs, omitted = [], []

    def pointer(owner, value):
        return PPtr(**value, assetsfile=owner.assets_file).deref() if value['m_PathID'] else None

    def component(go, name):
        for ref in go.read().m_Component:
            obj = ref.component.deref()
            if obj.type.name == 'MonoBehaviour' and obj.read(check_read=False).m_Script.read().m_ClassName == name:
                return obj
        raise ValueError(f'Missing {name} on {go.path_id}')

    def read(obj):
        fields = schemas.read(obj)
        inputs.append(dict(source=apk.provenance(obj), fields=fields))
        return fields

    def walk(obj, time, output):
        cls = obj.read(check_read=False).m_Script.read().m_ClassName
        fields = read(obj)
        assert fields['mySpeed'] == 1 and fields['myFadeInDuration'] == 0 and fields['myFadeOutDuration'] == 0
        assert not fields['myTriggerList'] and not fields['myFadeoutTrigerList'] and not fields['myEndTrigerList']
        if cls == 'MyPlayableSequence':
            for row in fields['myPlayables']:
                assert row['myTime'] >= 0
                walk(pointer(obj, row['myPlayable']), time + row['myTime'], output)
        elif cls == 'MyPlayableAnimator':
            output.append(dict(kind=0, time=time, speed=fields['mySpeed'],
                animation='', clip_resource='', origin_clip_name='',
                trigger_param=fields['myTriggerParam'], pre_state_name_allow_list=fields['PreStateNameAllowList'],
                my_bool_param=fields['myBoolParam'], my_bool_value=bool(fields['myBoolValue']),
                my_float_param=fields['myFloatParam'], my_float_value=fields['myFloatValue'],
                int_param=fields['IntParam'], int_value=fields['IntValue'], my_duration=fields['myDuration'],
                layer_name=fields['LayerName'], layer_weight=fields['LayerWeight'],
                end_resume_param=bool(fields['myEndResumeParam'])))
        else:
            assert cls in ('MyPlayableTrack', 'MyPlayableFollowBone', 'MyPlayableSound'), cls
            omitted.append(dict(type=cls, time=time, source=apk.provenance(obj)))

    result = dict(manifest=apk.version, skills=[], actions=[])
    try:
        for kind, ids, cls in (('skills', skills, 'MyFightSkill'), ('actions', actions, 'MyFightActionSkillMove')):
            for key in sorted(ids):
                asset = next(row for row in assets if row['path'].endswith('/' + str(key) + '.prefab'))
                owner = component(apk.asset(asset['bundle'], '/' + str(key) + '.prefab'), cls)
                fields = read(owner)
                if kind == 'skills':
                    assert all(fields[name]['m_PathID'] == 0 for name in
                               ('UpPlayablePrefab', 'DownPlayablePrefab', 'LeftPlayablePrefab', 'RightPlayablePrefab'))
                root = pointer(owner, fields['myPlayablePrefab'])
                playables = []
                if root:
                    walk(component(root, 'MyPlayableSequence'), 0., playables)
                result[kind].append(dict(id=key, playables=playables))
    finally:
        apk.archive.close()
    write_changed(OUTPUT, json.dumps(result, ensure_ascii=False, indent=2) + '\n')
    meta(OUTPUT)
    original = ROOT / '.tools/source-packages/current' / apk.version / 'official-code/client-source'
    runtime = ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleMovementAnimationCatalog.cs'
    validation = ROOT / 'unity/Assets/Game/Editor/HomeBattleHostPlayModeAcceptance.MoveAnimators.cs'
    for path in (runtime, validation):
        meta(path)
    proof = dict(manifest=apk.version, selection=identity(rules_path),
        application=[identity(original / name) for name in ('MyFightSkill.cs', 'MyFightActionSkillMove.cs',
                                                           'MyPlayableAnimator.cs')],
        framework=identity(original.parent / 'game-framework-source/GameFramework/MyPlayableSequence.cs'),
        objects=inputs, omitted_non_animator_nodes=omitted,
        outputs=[identity(path) for item in (OUTPUT, runtime, validation)
                 for path in (item, item.with_name(item.name + '.meta'))],
        generator=identity(Path(__file__).resolve()),
        integrations=['unity/Assets/Game/Runtime/Presentation/Combat/' + name for name in
                      ('OfficialBattleAnimationTimeline.cs', 'OfficialBattleAnimatorPlayback.cs', 'OfficialBattleSceneView.cs')],
        binding='Selected role movement skills play their original Animator cue once. Movement actions play their own cues for each actual world-space path segment. Action-owned playables stop at the cell boundary and restore parameters through the migrated original MyPlayableAnimator.',
        limits=['Follow-bone effects and sounds remain explicitly omitted; tracks are supplied by the separate grid-motion binding.',
                'Original skill-level infinite playables retain their own duration; no replacement finite duration is invented.',
                'Only attached models with all required original Animator parameters use these cues. Native models without an Animator retain their existing fallback until the original controller/rendering adapter is connected.',
                'Original Animator state changes do not prove that every state has converted visible bone/mesh motions.'])
    write_changed(RECEIPT, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Movement Animator records:', {kind: (len(result[kind]), sum(len(r['playables']) for r in result[kind]))
                                        for kind in ('skills', 'actions')})


if __name__ == '__main__':
    main()
