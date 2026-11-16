"""Verify the selected official Element Harmony passive and its stacking status."""
import json
from pathlib import Path
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]


def main():
    entities = {}
    for name in ('skill-rules.json', 'battle-rules.json'):
        data = json.loads((ROOT / 'unity/Assets/Official/Combat' / name).read_bytes())
        for key, value in data['entities'].items():
            entities.setdefault(key, {}).update(value)
    selected = {key: entities[key] for key in ('25111', '24388', '11200', '11201', '11203',
        '24210', '10860', '10861', '10862', '10910', '10911', '10912')}
    start = selected['25111']['FightStatusStartComponent']
    assert start['Setting']['_type'] == 'StatusStartSkillElementInfluenceHandleSetting'
    assert start['Setting']['InvokeType'] == 1 and not start['Setting']['ExpectElementList']
    assert [item['StatusId'] for item in start['StatusList']] == [24388]
    status = selected['24388']['FightStatusComponent']
    assert status['IsOpenStack'] and status['MaxStackedCount'] == 4 and not status['CanDispersed']
    rules = json.loads((ROOT / 'unity/Assets/Official/Combat/rules.json').read_bytes())
    skill, = (row for row in rules['skill'] if row['ClassId'] == 10602)
    assert skill['EcEntityId'] == 25111 and skill['PassiveStatusIdList'] == [25111]
    flame, = (row for row in rules['skill'] if row['ClassId'] == 10061)
    wind, = (row for row in rules['skill'] if row['ClassId'] == 10036)
    assert flame['EcEntityId'] == 11200 and wind['EcEntityId'] == 10910
    assert selected['11203']['FightDamageComponent']['StatusList'][0]['StatusId'] == 24210
    assert selected['24210']['FightStatusMoveNearComponent']['Skills'][0]['SkillId'] == 10860
    assert selected['10861']['FightHitFixedComponent']['DamageActionType'] == 4
    for key in ('11203', '10862'):
        selected[key]['FightDamageComponent']['DisperseStatus']['ExcludeCharacterTypes'] = []
    configuration = verify_selected_components(selected, [skill, flame, wind])
    payload = json.loads((ROOT / 'unity/Assets/Official/Combat/skill-rules.json').read_bytes())
    prop, = (row for row in payload['tables']['entity_prop'] if row['EntityId'] == 24388)
    assert prop['PropFactors'] == {'350': 3200} and payload['prop_names']['350'] == 'ElementMaster'
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    original = [identity(code / path) for path in (
        'common-source/Common/FightStatusStartComponent.cs',
        'common-source/Common/FightStatusStartComponentInfo.cs',
        'common-source/Common/StatusStartSkillElementInfluenceHandleSetting.cs',
        'common-source/Common/FightStatusInvokeElementCfg.cs',
        'common-source/Common/FightStatusDamageTypeCfg.cs',
        'common-source/Common/FightStatusMoveNearComponent.cs',
        'common-source/Common/FightStatusMoveNearComponentInfo.cs',
        'common-source/Common/FightStatusPropComponent.cs',
        'client-source/MyFightStatusStart.cs',
        'client-source/MyFightStatusMoveNear.cs')]
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline/Combat/FightBattleExecutor.ElementHarmony.cs'
    proof = dict(manifest='884_156692', configuration=configuration, official_code=original,
        runtime_inputs=[identity(runtime), identity(Path(str(runtime) + '.meta'))],
        entity_property=prop,
        entity_property_source=identity(ROOT / 'unity/Assets/Official/Combat/skill-rules-source-manifest.json'),
        generator=identity(Path(__file__)),
        operation='Verify the selected APK configuration and decompiled input code; retain the separately verified local property source.')
    (ROOT / 'assets/reference/battle-rules/element-harmony-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print('Verified original Element Harmony and the selected wind/flame damage graphs.')


if __name__ == '__main__':
    main()
