"""Verify original lifespan-reduction definitions before binding offline scheduling."""
import json
from pathlib import Path
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]


def main():
    entities = json.loads((ROOT / 'unity/Assets/Official/Combat/battle-rules.json').read_bytes())['entities']
    definitions = {key: entities[key] for key in ('24072', '25112', '10712', '11142')}
    assert definitions['24072']['FightStatusReduceStatusLifeComponent']['ReduceRoundCount'] == 2
    assert definitions['10712']['FightStatusComponent']['DurationRound'] == 5
    assert definitions['11142']['FightStatusComponent']['DurationRound'] == 2
    configuration = verify_selected_components(definitions)
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    originals = [identity(code / path) for path in (
        'common-source/Common/FightStatusReduceStatusLifeComponent.cs',
        'common-source/Common/FightStatusReduceStatusLifeComponentInfo.cs',
        'common-source/Common/SourceEntityGameplayTagFilter.cs',
        'client-source/MyFightStatusReduceStatusLife.cs')]
    proof = dict(manifest='884_156692', configuration=configuration, original_code=originals,
        verifier=identity(Path(__file__)),
        operation='APK-first exact verification of original reduction count, tag filter, summon trigger '
                  'and lifetime/end components. Server scheduling is absent; the confirmed offline '
                  'timing and persistence rules are maintained in docs/features/skills.md.')
    (ROOT / 'assets/reference/battle-rules/status-life-reduction-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print('Verified selected 24072/25112/10712/11142 definitions and four original code inputs.')


if __name__ == '__main__':
    main()
