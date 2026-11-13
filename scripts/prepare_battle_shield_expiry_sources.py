"""Verify the original cure-to-shield expiry configuration and 12750 skill chain."""
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
    definitions = {key: entities[key] for key in ('24435', '12750', '12751', '12752')}
    selected = deepcopy(definitions)
    disperse = selected['12752']['FightDamageComponent']['DisperseStatus']
    assert 'ExcludeCharacterTypes' not in disperse
    assert disperse['Count'] == 0 and disperse['BasePercent'] == 0 and disperse['TargetTypes'] == []
    disperse['ExcludeCharacterTypes'] = []
    shield = definitions['24435']['FightStatusShieldPersistentComponent']
    assert shield['CustomShield'] and shield['ConvertShieldToHpOnEnd']
    assert shield['DonotShieldToHpOnEndByRemove'] and not shield['IsConvertShieldToHp']
    assert shield['SkillCfg']['SkillId'] == 12750
    skill = definitions['12750']['FightSkillComponent']
    assert not skill['CanTriggerChild'] and skill['CanTriggerStatusEntityIds'] == []
    configuration = verify_selected_components(selected)
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    originals = [identity(code / path) for path in (
        'common-source/Common/FightStatusShieldPersistentComponent.cs',
        'common-source/Common/FightStatusShieldPersistentComponentInfo.cs',
        'common-source/Common/FightStatusShieldBaseComponent.cs',
        'common-source/Common/BattleFormulaHandler.cs',
        'client-source/MyFightStatusShieldPersistent.cs')]
    proof = dict(manifest='884_156692', configuration=configuration, original_code=originals,
        runtime_schema_difference=dict(entity='12752', component='FightDamageComponent',
            path='DisperseStatus.ExcludeCharacterTypes', runtime_manifest='854_156383',
            runtime_field='absent', selected_value=[], runtime_component=definitions['12752'],
            effect='Empty excluded-character filter on disabled zero-count, zero-probability dispel; input unchanged.'),
        verifier=identity(Path(__file__)),
        operation='APK-first exact verification of 24435 and the 12750/12751/12752 chain. '
                  'The user-confirmed offline enablement and expiry amount are maintained in docs/features/skills.md; '
                  'they are not claimed as recovered server code.')
    (ROOT / 'assets/reference/battle-rules/shield-expiry-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    print('Verified original 24435/12750/12751/12752 definitions and five code inputs.')


if __name__ == '__main__':
    main()
