"""Verify original fire-hit scope definitions before binding offline scheduling."""
import json
from pathlib import Path
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]


def main():
    entities = json.loads((ROOT / 'unity/Assets/Official/Combat/battle-rules.json').read_bytes())['entities']
    definitions = {key: entities[key] for key in ('25045', '11580', '11581', '11582')}
    scope = definitions['25045']['FightStatusHitScopeComponent']
    assert scope['SkillEleTypes'] == [102] and scope['ConditionCount'] == 1
    assert abs(scope['Rate'] - 0.2) < 1e-7 and scope['SkillId'] == 11580
    configuration = verify_selected_components(definitions)
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    originals = [identity(code / path) for path in (
        'common-source/Common/FightStatusHitScopeComponent.cs',
        'common-source/Common/FightStatusHitScopeComponentInfo.cs',
        'client-source/MyFightStatusHitScope.cs')]
    proof = dict(manifest='884_156692', configuration=configuration, original_code=originals,
        verifier=identity(Path(__file__)),
        operation='APK-first exact verification of original fire element, condition threshold, probability, '
                  'trigger skill and burning-ground dependencies. Server scheduling is absent; confirmed '
                  'offline hit timing and checkpoint rules are maintained in docs/features/skills.md.')
    (ROOT / 'assets/reference/battle-rules/status-hit-scope-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print('Verified selected 25045/11580/11581/11582 definitions and three original code inputs.')


if __name__ == '__main__':
    main()
