"""Verify the original 10502 passive, triggered lightning and associated code."""
import json
from pathlib import Path
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]


def main():
    entities = {}
    for name in ('skill-rules.json', 'battle-rules.json'):
        for key, value in json.loads((ROOT / 'unity/Assets/Official/Combat' / name).read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(value)
    selected = {key: entities[key] for key in ('25000', '10530', '10531', '10026')}
    trigger = selected['25000']['FightStatusHitSkillComponent']
    assert trigger['HitSourceType'] == 2 and abs(trigger['Rate'] - .3) < .000001
    assert trigger['Damage'] and not trigger['Cure'] and trigger['SkillCfgs'][0]['SkillId'] == 10530
    assert selected['10530']['FightSkillComponent']['HitList'][0]['ClassId'] == 10531
    assert selected['10531']['FightHitFixedComponent']['DamageId'] == 10026
    # This omitted runtime field is the explicit empty default in the APK schema.
    selected['10026']['FightDamageComponent']['DisperseStatus']['ExcludeCharacterTypes'] = []
    rules = json.loads((ROOT / 'unity/Assets/Official/Combat/rules.json').read_bytes())
    skill, = (row for row in rules['skill'] if row['ClassId'] == 10502)
    assert skill['EcEntityId'] == 25000 and skill['PassiveStatusIdList'] == [25000]
    configuration = verify_selected_components(selected, [skill])
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    originals = [identity(code / path) for path in (
        'common-source/Common/FightStatusHitBaseComponentInfo.cs',
        'common-source/Common/FightStatusHitSkillComponentInfo.cs',
        'common-source/Common/FightStatusHitSkillComponent.cs',
        'common-source/Common/FightStatusComponentImplement.cs',
        'client-source/MyFightStatusHitSkill.cs')]
    proof = dict(manifest='884_156692', configuration=configuration, official_code=originals,
        explicit_empty_defaults=['10026/DisperseStatus/ExcludeCharacterTypes'],
        generator=identity(Path(__file__)), operation='Verify the selected APK configuration and decompiled input code.')
    (ROOT / 'assets/reference/battle-rules/thunder-mystery-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print('Verified original 10502/25000/10530/10531/10026 inputs.')


if __name__ == '__main__':
    main()
