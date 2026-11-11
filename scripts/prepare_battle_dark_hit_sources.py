"""Verify the original dark-hit passive and its status-application dependencies."""
import json
from pathlib import Path
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]


def main():
    entities = json.loads((ROOT / 'unity/Assets/Official/Combat/battle-rules.json').read_bytes())['entities']
    definitions = {key: entities[key] for key in ('25031', '24222')}
    handler = definitions['25031']['FightStatusDoDamageHandleComponent']
    assert handler['HitSkillElementTypes'] == [104] and handler['IgnoreHitSkills'] == [11360]
    assert handler['IgnoreTeammate'] and not handler['RequireDamageResult']
    assert handler['Rate'] == 1 and handler['MaxTriggerCount'] == -1
    assert handler['DoDamageSourceCfg']['HitSourceType'] == 2
    assert handler['RoundCountCfg'] is None and handler['SkillCfgs'] == []
    status, = handler['StatusCfgs']
    assert status['StatusId'] == 24222 and status['ApplyTarget'] == 5
    assert status['AffectedByProp'] and abs(status['BasePercent'] - .4) < 1e-7
    configuration = verify_selected_components(definitions)
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    originals = [identity(code / path) for path in (
        'common-source/Common/FightStatusDoDamageHandleComponent.cs',
        'common-source/Common/FightStatusDoDamageHandleComponentInfo.cs',
        'common-source/Common/FightStatusDoDamageStatusSourceCfg.cs',
        'common-source/Common/FightStatusTriggerStatusCfg.cs',
        'client-source/MyFightStatusDoDamageHandle.cs')]
    proof = dict(manifest='884_156692', configuration=configuration, original_code=originals,
        verifier=identity(Path(__file__)),
        operation='APK-first exact verification of dark-hit filters, original probability, '
                  'target status and empty client handler. Confirmed offline event timing is '
                  'maintained in docs/features/skills.md; verification alone does not implement it.')
    (ROOT / 'assets/reference/battle-rules/dark-hit-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print('Verified original 25031/24222 definitions and five code inputs.')


if __name__ == '__main__':
    main()
