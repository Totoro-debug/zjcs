"""Verify original launch hits and the two airborne status definitions, APK first."""
import json
from pathlib import Path

from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]


def main():
    entities = json.loads((ROOT / 'unity/Assets/Official/Combat/battle-rules.json').read_bytes())['entities']
    definitions = {key: entities[key] for key in ('10581', '10611', '11121', '24269', '24270')}
    for key in ('10581', '10611', '11121'):
        assert definitions[key]['FightHitFixedComponent']['AirCombo'] == {
            'AirComboStatusId': 24269, 'BasePercent': 1.0, 'AffectedByProp': False}
    air = definitions['24269']
    landing = definitions['24270']
    assert air['ActionComponent']['ActionType'] == 66
    assert landing['ActionComponent']['ActionType'] == 67
    assert air['FightStatusComponent']['DurationRound'] == -1
    assert landing['FightStatusComponent']['DurationRound'] == -1
    remove = air['FightStatusDamageRemoveComponent']
    assert remove['StatusDamageTypeCfg'] == dict(IsDamage=True, IsCure=False, IsBlock=True, IsCrit=False)
    assert remove['StatusAutoRemoveCfg']['MaxCount'] == 1
    assert remove['StatusAutoRemoveCfg']['RemoveTarget'] == 6
    damage = air['FightStatusDamageSkillComponent']
    assert damage['Damage'] and not damage['Block'] and damage['DamageMove'] == 2
    assert damage['ConditionCount'] == 1 and damage['MaxCount'] == -1
    timer = damage['AutoRemove']
    assert timer['ResetTimerWhenInvoke'] and abs(timer['Duration'] - .7) < 1e-7
    assert timer['RemoveTarget'] == 6
    successor, = timer['RemoveStatusCfgs']
    assert successor['StatusId'] == 24270 and successor['BasePercent'] == 1
    assert not successor['AffectedByProp'] and successor['ApplyTarget'] == 2
    timer = landing['StatusAutoRemoveComponent']['AutoRemove']
    assert not timer['ResetTimerWhenInvoke'] and abs(timer['Duration'] - .8) < 1e-7
    assert timer['RemoveTarget'] == 6 and timer['RemoveStatusCfgs'] == []
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    paths = (
        'common-source/Common/HitAirComboCfg.cs',
        'common-source/Common/FightStatusAutoRemoveCfg.cs',
        'common-source/Common/FightStatusDamageTypeCfg.cs',
        'common-source/Common/FightStatusDamageRemoveComponentInfo.cs',
        'common-source/Common/FightStatusDamageSkillComponentInfo.cs',
        'common-source/Common/StatusAutoRemoveComponentInfo.cs',
        'client-source/MyFightStatusDamageRemove.cs',
        'client-source/MyFightStatusDamageSkill.cs',
        'client-source/MyStatusAutoRemove.cs',
    )
    proof = dict(manifest='884_156692', configuration=verify_selected_components(definitions),
        original_code=[identity(code / path) for path in paths], verifier=identity(Path(__file__)),
        operation='APK-first exact verification of three launch hits, action types, block-damage conjunction, non-displacement reset and original second-based successor timers. User-confirmed offline scheduling is maintained in docs/features/skills.md.')
    output = ROOT / 'assets/reference/battle-rules/air-combo-source-manifest.json'
    output.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print('Verified three original launch hits, airborne/recovery states and nine original code inputs.')


if __name__ == '__main__':
    main()
