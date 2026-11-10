"""Verify 10531's original death listener, two skills, hits and revival statuses."""
import json
import copy
from pathlib import Path
from verify_battle_skill_graph_rules import verify_selected_components, identity

ROOT = Path(__file__).resolve().parents[1]


def main():
    entities = {}
    for name in ('skill-rules.json', 'battle-rules.json'):
        for key, value in json.loads((ROOT / 'unity/Assets/Official/Combat' / name).read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(value)
    rules = json.loads((ROOT / 'unity/Assets/Official/Combat/rules.json').read_bytes())
    skill = next(row for row in rules['skill'] if row['ClassId'] == 10531)
    selected = (25014, 11470, 11471, 11472, 11473, 11474, 11490, 11491, 10005, 24233, 24083)
    definitions = {str(key): copy.deepcopy(entities[str(key)]) for key in selected}
    # Selected 884 adds an empty exclusion list; the historical payload omitted it.
    # Preserve both input identities and compare with that explicit no-filter default.
    for key in ('10005', '11472', '11474'):
        disperse = definitions[key]['FightDamageComponent']['DisperseStatus']
        assert 'ExcludeCharacterTypes' not in disperse
        disperse['ExcludeCharacterTypes'] = []
    assert definitions['24083']['FightStatusComponent']['ForbiddenOnApplicatorDie'] is False
    definitions['24083']['FightStatusComponent']['ForbiddenOnApplicatorDie'] = True
    verified = verify_selected_components(definitions, [skill])
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    inputs = [identity(code / name) for name in (
        'common-source/Common/FightStatusRoleDieComponent.cs',
        'common-source/Common/FightStatusRoleDieComponentInfo.cs',
        'common-source/Common/FightStatusBackToLifeComponent.cs',
        'common-source/Common/FightHitFixedComponent.cs',
        'client-source/MyFightStatusRoleDie.cs', 'client-source/MyFightHitFixed.cs')]
    record = dict(manifest='884_156692', configuration=verified, official_code=inputs,
        omitted_empty_defaults=['%s/FightDamageComponent/DisperseStatus/ExcludeCharacterTypes' % key
            for key in ('10005', '11472', '11474')],
        selected_runtime_adapter='24083/FightStatusComponent/ForbiddenOnApplicatorDie=true in FightOfficialCombatCatalog.CreateStatus',
        generator=identity(Path(__file__)),
        operation='APK-first exact field verification. Server scheduling and dead-character ordering '
        'are user-confirmed offline decisions maintained in docs/features/skills.md.')
    (ROOT / 'assets/reference/battle-rules/passive-revival-source-manifest.json').write_text(
        json.dumps(record, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print('Verified original 10531 -> 25014 -> 11470/11490 and revival/protection dependencies.')


if __name__ == '__main__':
    main()
