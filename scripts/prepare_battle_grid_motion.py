"""Extract original role movement tracks from the selected APK, then verified CDN cache.

Only game application components and Unity serialized curves are read. Existing
rule payloads retain their historical provenance; their action IDs select assets.
"""
import json
from pathlib import Path

from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed
from UnityPy.classes import PPtr

OUTPUT = ROOT / 'unity/Assets/Game/Resources/Combat/grid-motion.json'
RECEIPT = ROOT / 'assets/reference/battle-animation/grid-motion-source-manifest.json'


def main():
    apk, schemas = Apk.current(), CurrentSchemas()
    rules_path = ROOT / 'unity/Assets/Official/Combat/battle-rules.json'
    entities = json.loads(rules_path.read_bytes())['entities']
    skill_ids = {int(value['FightRoleCharacterComponent'].get(field, 0))
                 for value in entities.values() if 'FightRoleCharacterComponent' in value
                 for field in ('MoveSkillId', 'BackMoveSkillId', 'FixSpeedMoveSkillId')} - {0}
    action_ids = {int(row['MoveBehaviourId']) for key in skill_ids
                  for row in entities[str(key)]['FightSkillComponent']['SourceMoveList']}
    assets = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))
    bindings, inputs = [], []

    def pointer(owner, value):
        return PPtr(**value, assetsfile=owner.assets_file).deref() if value['m_PathID'] else None

    def component(obj, name):
        for ref in obj.read().m_Component:
            candidate = ref.component.deref()
            if candidate.type.name == 'MonoBehaviour' and candidate.read(check_read=False).m_Script.read().m_ClassName == name:
                return candidate
        raise ValueError(f'Missing original {name} on {obj.path_id}')

    def read(obj):
        fields = schemas.read(obj)
        inputs.append(dict(source=apk.provenance(obj), fields=fields))
        return fields

    def curve(value):
        # JSON has no Infinity; preserve Unity's step tangents explicitly.
        import math
        # These prefabs all use native serialized constant extrapolation (2).
        # Its integer identity is NOT UnityEngine.WrapMode.Loop (also 2).
        assert value['m_PreInfinity'] == 2 and value['m_PostInfinity'] == 2
        keys = []
        for row in value['m_Curve']:
            key = dict(row)
            for field in ('inSlope', 'outSlope'):
                slope = key[field]
                key[field + 'Infinite'] = math.isinf(slope)
                if math.isinf(slope):
                    key[field] = 0.0
                if math.isnan(slope):
                    raise ValueError('Original curve contains NaN')
            keys.append(key)
        return dict(keys=keys, pre=value['m_PreInfinity'], post=value['m_PostInfinity'])

    for action_id in sorted(action_ids):
        source = next(row for row in assets if row['path'].endswith('/' + str(action_id) + '.prefab'))
        root = apk.asset(source['bundle'], '/' + str(action_id) + '.prefab')
        action = component(root, 'MyFightActionSkillMove')
        action_fields = read(action)
        playable_root = pointer(action, action_fields['myPlayablePrefab'])
        sequence = component(playable_root, 'MyPlayableSequence')
        sequence_fields = read(sequence)
        tracks = []
        for row in sequence_fields['myPlayables']:
            child = pointer(sequence, row['myPlayable'])
            if child.read(check_read=False).m_Script.read().m_ClassName == 'MyPlayableTrack':
                tracks.append((row, child))
        assert len(tracks) == 1
        sequence_row, track = tracks[0]
        fields = read(track)
        apply_source = component(pointer(track, fields['m_GameObject']), 'ApplyBySource')
        read(apply_source)
        assert sequence_fields['mySpeed'] == 1 and fields['mySpeed'] == 1, (action_id, sequence_fields['mySpeed'], fields['mySpeed'])
        assert all(value[key] == 0 for value in (sequence_fields, fields)
                   for key in ('myFadeInDuration', 'myFadeOutDuration'))
        assert not fields['myUseTrackRotation'] and not fields['myUseTrackScale']
        assert not fields['myFollowTarget'] and not fields['mIsZEquipY']
        assert not fields['myTrackTriggerList'] and fields['myTrackEndStopTime'] == 0
        for key, attach in (('myStartAttachPos', 0), ('myEndAttachPos', 1)):
            value = fields[key]
            assert value['AttachTo'] == attach and not value['myBoneName']
            assert not any(value['myOffset'].values())
        record = dict(action_id=action_id, delay=sequence_row['myTime'], speed=fields['myTrackSpeed'], fixed_duration=bool(fields['myFixDuration']),
                      range_scale=fields['myXScaleByRange'], flip_source=bool(fields['myFlipBySourceX']),
                      flip_target=bool(fields['myFlipByTargetY']), flip_direction=bool(fields['mFlipByDirX']),
                      curve_duration=1.0, has_curve=False)
        curve_object = pointer(track, fields['myTrack'])
        if curve_object:
            assert curve_object.read(check_read=False).m_Script.read().m_ClassName == 'MyCurveTrack'
            data = read(curve_object)
            assert data['myLoopType'] == 0
            record.update(has_curve=True, curve_duration=data['myDuration'],
                          x=curve(data['myCurveX']), y=curve(data['myCurveY']))
        bindings.append(record)

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    write_changed(OUTPUT, json.dumps(dict(manifest=apk.version, tracks=bindings), ensure_ascii=False, indent=2, allow_nan=False) + '\n')
    meta(OUTPUT)
    # Raw source fields in the receipt also preserve step tangents as JSON strings.
    def safe(value):
        import math
        if isinstance(value, float) and math.isinf(value):
            return 'Infinity' if value > 0 else '-Infinity'
        if isinstance(value, dict):
            return {key: safe(child) for key, child in value.items()}
        if isinstance(value, list):
            return [safe(child) for child in value]
        return value
    original = ROOT / '.tools/source-packages/current' / apk.version / 'official-code/client-source'
    runtime_files = ['unity/Assets/Game/Runtime/Official/Combat/FightOfficialCombatCatalog.Movement.cs',
                     'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleGridMotion.cs',
                     'unity/Assets/Game/Editor/HomeBattleHostPlayModeAcceptance.GridMotion.cs']
    output_paths = [OUTPUT, OUTPUT.with_name(OUTPUT.name + '.meta')]
    output_paths += [ROOT / path for name in runtime_files for path in (name, name + '.meta')]
    common = original.parent / 'common-source/Common'
    proof = dict(manifest=apk.version, rule_selection=identity(rules_path),
                 application=[identity(original / name) for name in ('MyFightActionSkillMove.cs', 'MyPlayableTrack.cs', 'MyTrack.cs', 'MyCurveTrack.cs')],
                 shared=[identity(common / name) for name in ('FightActionSkillMoveComponent.cs', 'FightRoleCharacterComponentInfo.cs')],
                 apply_source=identity(original.parent / 'game-framework-source/GameFramework/ApplyBySource.cs'),
                 objects=safe(inputs), outputs=[identity(path) for path in output_paths],
                 integrations=['unity/Assets/Game/Runtime/Official/Combat/FightBattleExecutor.cs',
                               'unity/Assets/Game/Runtime/Official/Combat/FightBattleState.cs',
                               'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneView.cs',
                               'unity/Assets/Game/Editor/HomeBattleHostPlayModeAcceptance.cs'],
                 generator=identity(Path(__file__).resolve()),
                 binding='All normal/back/fixed-speed movement action IDs selected by the existing role rules. Source and target use the entity origin. Restore original track duration/speed, curve keys/tangents, distance mapping and mirroring. Native serialized constant infinity mode 2 maps to public WrapMode.ClampForever rather than public enum value 2 (Loop). Rotation and scale application are disabled by every selected prefab.',
                 limits=['Skill-specific displacement actions outside the role movement set, blocked effects, sounds and bone-follow effects are not included.',
                         'Movement tracks have selected-current asset provenance; the existing rule payload keeps its own original version. No rule values are replaced or relabeled.',
                         'Role configurations with BackMoveSkillId=0 provide no original retreat action. Existing offline retreat behavior is retained with action ID 0 and an explicit IsRetreat event flag; it is not counted as a restored original track.'])
    write_changed(RECEIPT, json.dumps(proof, ensure_ascii=False, indent=2, allow_nan=False) + '\n')
    print(f'Extracted {len(bindings)} original role movement tracks from {len(skill_ids)} configured skills.')


if __name__ == '__main__':
    main()
