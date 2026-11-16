"""Verify the selected official Wind Shadow passive and its child graph."""
import json
from pathlib import Path
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]


def main():
    entities = {}
    for name in ('skill-rules.json', 'battle-rules.json'):
        for key, value in json.loads((ROOT / 'unity/Assets/Official/Combat' / name).read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(value)
    selected = {key: entities[key] for key in ('25038', '11670', '11671', '11672', '24232')}
    trigger = selected['25038']['FightStatusHitSkillComponent']
    assert trigger['ConditionCount'] == 3 and abs(trigger['Rate'] - .3) < .00001
    assert trigger['HitSkillElementTypes'] == [100] and trigger['SkillCfgs'][0]['SkillId'] == 11670
    damage = selected['11672']['FightDamageComponent']['StatusList'][0]
    assert damage['StatusId'] == 24232 and damage['BasePercent'] == .5 and damage['AffectedByProp']
    delay = selected['24232']['FightStatusRoundIntervalComponent']
    assert abs(delay['RoundIntervalPercent'] - .2) < .00001 and not delay['FastForward']
    # The current decoder exposes an empty field absent from the historical export.
    selected['11672']['FightDamageComponent']['DisperseStatus']['ExcludeCharacterTypes'] = []
    rules = json.loads((ROOT / 'unity/Assets/Official/Combat/rules.json').read_bytes())
    skill, = (row for row in rules['skill'] if row['ClassId'] == 10569)
    assert skill['EcEntityId'] == 25038 and skill['PassiveStatusIdList'] == [25038]
    configuration = verify_selected_components(selected, [skill])
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    originals = [identity(code / path) for path in (
        'common-source/Common/FightStatusHitSkillComponent.cs',
        'common-source/Common/FightStatusHitSkillComponentInfo.cs',
        'common-source/Common/FightStatusHitBaseComponent.cs',
        'common-source/Common/FightStatusHitBaseComponentInfo.cs',
        'common-source/Common/FightStatusRoundIntervalComponentInfo.cs')]
    runtime = ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleGraphCapabilityValidation.WindShadow.cs'
    proof = dict(manifest='884_156692', configuration=configuration, official_code=originals,
        runtime_inputs=[identity(runtime), identity(Path(str(runtime) + '.meta'))],
        generator=identity(Path(__file__)),
        operation='Verify the selected APK configuration and decompiled input code.')
    (ROOT / 'assets/reference/battle-rules/wind-shadow-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print('Verified original 10569/25038/11670/11671/11672/24232 inputs.')


if __name__ == '__main__':
    main()
