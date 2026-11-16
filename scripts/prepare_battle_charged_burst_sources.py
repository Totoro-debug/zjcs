"""Verify the selected Charged Burst hit and its two original statuses."""
import json
from pathlib import Path
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]


def main():
    entities = {}
    for name in ('skill-rules.json', 'battle-rules.json'):
        for key, value in json.loads((ROOT / 'unity/Assets/Official/Combat' / name).read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(value)
    selected = {key: entities[key] for key in ('11000', '11001', '11002', '24191', '24219')}
    assert selected['11000']['FightSkillComponent']['HitList'][0]['ClassId'] == 11001
    assert selected['11001']['FightHitFixedComponent']['DamageId'] == 11002
    damage = selected['11002']['FightDamageComponent']
    assert [v['StatusId'] for v in damage['StatusList']] == [24219, 24191]
    for key in ('24191', '24219'):
        effect = selected[key]['FightStatusHitDmgAddPerComponent']
        assert effect['DurationSkillCount'] == 1 and effect['OnlySkillActions'] == [2]
        assert effect['OnlySkillType'] == 0 and effect['Damage'] and not effect['Cure']
    damage['DisperseStatus']['ExcludeCharacterTypes'] = []
    rules = json.loads((ROOT / 'unity/Assets/Official/Combat/rules.json').read_bytes())
    skill, = (row for row in rules['skill'] if row['ClassId'] == 10041)
    assert skill['EcEntityId'] == 11000
    configuration = verify_selected_components(selected, [skill])
    payload = json.loads((ROOT / 'unity/Assets/Official/Combat/skill-rules.json').read_bytes())
    props = [row for row in payload['tables']['entity_prop'] if row['EntityId'] in (24191, 24219)]
    assert len(props) == 2
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    originals = [identity(code / path) for path in (
        'common-source/Common/FightStatusHitDmgAddPerComponent.cs',
        'common-source/Common/FightStatusHitDmgAddPerComponentInfo.cs',
        'common-source/Common/FightStatusHitBaseComponentInfo.cs',
        'client-source/MyFightStatusHitDmgAddPer.cs')]
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline/Combat/FightConditionalHitProperties.cs'
    probe = ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleGraphCapabilityValidation.ChargedBurst.cs'
    proof = dict(manifest='884_156692', configuration=configuration, official_code=originals,
        runtime_inputs=[identity(runtime), identity(probe), identity(Path(str(probe) + '.meta'))],
        entity_properties=props,
        entity_property_source=identity(ROOT / 'unity/Assets/Official/Combat/skill-rules-source-manifest.json'),
        generator=identity(Path(__file__)),
        operation='Verify the selected APK configuration and decompiled input code; retain the separately verified local property source.')
    (ROOT / 'assets/reference/battle-rules/charged-burst-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print('Verified original 10041/11000/11001/11002/24191/24219 inputs.')


if __name__ == '__main__':
    main()
