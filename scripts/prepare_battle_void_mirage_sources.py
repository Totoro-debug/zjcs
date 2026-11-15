"""Verify the selected original 10556 threshold and protection configurations."""
import json
from pathlib import Path
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]


def main():
    entities = {}
    for name in ('skill-rules.json', 'battle-rules.json'):
        for key, value in json.loads((ROOT / 'unity/Assets/Official/Combat' / name).read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(value)
    selected = {key: entities[key] for key in ('25022', '24213', '24209')}
    threshold = selected['25022']['FightStatusHpLessThanComponent']
    assert threshold['HpLessThanPercentage'] == .5 and threshold['MaxCount'] == 1
    assert not threshold['TryAtStart'] and not threshold['IsInvokeOnDie']
    assert threshold['StatusList'] == [24213, 24209]
    for key in ('24213', '24209'):
        status = selected[key]['FightStatusComponent']
        assert status['DurationRound'] == 2 and not status['CanDispersed']
    # The current source schema adds an explicit false default absent from the historical payload.
    threshold['ReportSkillTriggerCount'] = False
    rules = json.loads((ROOT / 'unity/Assets/Official/Combat/rules.json').read_bytes())
    skill, = (row for row in rules['skill'] if row['ClassId'] == 10556)
    assert skill['EcEntityId'] == 25022 and skill['PassiveStatusIdList'] == [25022]
    configuration = verify_selected_components(selected, [skill])
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    originals = [identity(code / path) for path in (
        'common-source/Common/FightStatusHpLessThanComponent.cs',
        'common-source/Common/FightStatusHpLessThanComponentInfo.cs',
        'common-source/Common/FightStatusShieldPersistentComponentInfo.cs',
        'common-source/Common/FightStatusPropComponentInfo.cs',
        'client-source/MyFightStatusHpLessThan.cs')]
    proof = dict(manifest='884_156692', configuration=configuration, official_code=originals,
        explicit_defaults=['25022/ReportSkillTriggerCount=false'], generator=identity(Path(__file__)),
        operation='Verify the selected APK configuration and decompiled input code.')
    (ROOT / 'assets/reference/battle-rules/void-mirage-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print('Verified original 10556/25022/24213/24209 inputs.')


if __name__ == '__main__':
    main()
