"""Verify original absorption event configuration and its self-cure skill."""
import json
from copy import deepcopy
from pathlib import Path
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]


def main():
    entities = {}
    for name in ('skill-rules', 'battle-rules'):
        for key, definition in json.loads((ROOT / f'unity/Assets/Official/Combat/{name}.json').read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(definition)
    definitions = {key: entities[key] for key in ('25023', '10520', '10521', '10028')}
    # 854's runtime preset predates this empty optional 884 field. Compare the
    # complete selected component, and record that schema difference explicitly.
    selected = deepcopy(definitions)
    disperse = selected['10028']['FightDamageComponent']['DisperseStatus']
    assert 'ExcludeCharacterTypes' not in disperse
    assert disperse['Count'] == 0 and disperse['BasePercent'] == 0 and disperse['TargetTypes'] == []
    disperse['ExcludeCharacterTypes'] = []
    component = definitions['25023']['FightStatusHitCustomCureComponent']
    assert component['HitSourceType'] == 2 and component['Damage'] and not component['Cure']
    assert component['SkillId'] == 10520 and not component['IsSkillTargetPos']
    assert component['Rate'] == 1 and component['ConditionCount'] == 1 and component['MaxInvokeCount'] == -1
    assert not component['IsApplyOneHit'] and not component['ResetAtRoundStart']
    assert component['SuckHpLimitPropType'] == 0 and component['SuckHpLimitPercent'] == 0
    assert not definitions['10520']['FightSkillComponent']['CanTriggerChild']
    assert definitions['10520']['FightSkillComponent']['CanTriggerStatusEntityIds'] == [25136]
    configuration = verify_selected_components(selected)
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    originals = [identity(code / path) for path in (
        'common-source/Common/FightStatusHitCustomCureComponent.cs',
        'common-source/Common/FightStatusHitCustomCureComponentInfo.cs',
        'common-source/Common/FightStatusHitBaseComponentInfo.cs',
        'common-source/Common/BattleFormulaHandler.cs',
        'client-source/MyFightStatusHitCustomCure.cs')]
    proof = dict(manifest='884_156692', configuration=configuration, original_code=originals,
        runtime_schema_difference=dict(entity='10028', component='FightDamageComponent',
            path='DisperseStatus.ExcludeCharacterTypes', runtime_manifest='854_156383',
            runtime_field='absent', selected_value=[], runtime_component=definitions['10028'],
            effect='Empty excluded-character filter on disabled zero-probability, zero-count dispel; runtime input unchanged.'),
        verifier=identity(Path(__file__)),
        operation='APK-first exact verification of absorption hit filters and the original self-cure '
                  'skill chain. The independently verified SuckHp formula is recorded separately; '
                  'confirmed offline event timing and damage basis are maintained in docs/features/skills.md.')
    (ROOT / 'assets/reference/battle-rules/hit-custom-cure-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print('Verified original 25023/10520/10521/10028 definitions and five code inputs.')


if __name__ == '__main__':
    main()
