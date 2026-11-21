"""Verify original Fighter fatal-hit survival inputs without changing runtime payloads."""
import json
from pathlib import Path
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]


def main():
    entities = {}
    for name in ('skill-rules', 'battle-rules'):
        for key, definition in json.loads((ROOT / f'unity/Assets/Official/Combat/{name}.json').read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(definition)
    definitions = {key: entities[key] for key in ('25013', '10990', '10991', '10992', '24082')}
    limit = definitions['25013']['FightStatusHpLimitComponent']
    assert limit['LowerLimit'] == limit['MaxCount'] == 1
    assert not limit['AutoRemove'] and not limit['AutoRemoveImmediately']
    assert limit['ListenerSetting']['Type'] == 0 and not limit['PassTriggerTarget']
    cfg, = limit['SkillList']
    assert cfg['SkillId'] == 10990 and cfg['Source'] == cfg['Target'] == 2
    assert cfg['BasePercent'] == 1 and not cfg['UseAI']
    assert definitions['10990']['FightSkillComponent']['SkillType'] == 3
    hit = definitions['10991']['FightHitFixedComponent']
    assert hit['DamageId'] == 10992 and hit['DamageActionType'] == 5
    assert hit['DamageBasicProp'] == 1 and hit['DamageProp'] == 210 and hit['FixedDamageProp'] == 170
    application, = definitions['10992']['FightDamageComponent']['StatusList']
    assert application['StatusId'] == 24082 and application['BasePercent'] == 1
    status = definitions['24082']['FightStatusComponent']
    assert status['DurationRound'] == 2 and not status['CanDispersed']
    reduction = definitions['24082']['FightStatusDamageReduceAbsoluteComponent']
    assert reduction['Rate'] == 1 and reduction['Count'] == -1 and not reduction['IsCheckSkillTrigger']
    rules = json.loads((ROOT / 'unity/Assets/Official/Combat/rules.json').read_bytes())
    skill, = (row for row in rules['skill'] if row['ClassId'] == 10518)
    assert skill['EcEntityId'] == 25013 and skill['PassiveStatusIdList'] == [25013]
    # Preserve historical input bytes; the selected original explicitly defaults this list to empty.
    disperse = definitions['10992']['FightDamageComponent']['DisperseStatus']
    assert 'ExcludeCharacterTypes' not in disperse
    disperse['ExcludeCharacterTypes'] = []
    configuration = verify_selected_components(definitions, [skill])
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    originals = [identity(code / ('common-source/Common/' + name + '.cs')) for name in (
        'FightStatusHpLimitComponent', 'FightStatusHpLimitComponentInfo',
        'FightStatusHpLimitListenerSetting', 'FightStatusHpLimitListenerForTargetSetting',
        'FightStatusDamageReduceAbsoluteComponent', 'FightStatusDamageReduceAbsoluteComponentInfo',
        'FightStatusComponentImplement', 'FightHitFixedComponentInfo',
        'FightDamageComponentImplement', 'FightRoleFightComponent',
        'StatusTriggerTarget', 'FightStatusTriggerSkillCfg')]
    originals.append(identity(code / 'client-source/MyFightStatusHpLimit.cs'))
    native, views, source_manifests = [], [], []
    for filename, ids, expected in (
        ('first-promotion-status-assets-source-manifest.json', (25013, 24082), 6),
        ('skill-native-graph-assets-source-manifest.json', (10990,), 10)):
        path = ROOT / 'assets/reference/battle-animation' / filename
        doc = json.loads(path.read_bytes())
        selected = lambda entry: any(f'/{entity}/' in entry['path'] for entity in ids)
        inputs = [entry for entry in doc['native_inputs'] if selected(entry)]
        outputs = [entry for entry in doc['outputs'] if selected(entry)]
        assert len(inputs) == len(outputs) == expected
        for entry in inputs + outputs:
            assert identity(ROOT / entry['path']) == entry
        native.extend(inputs)
        views.extend(outputs)
        source_manifests.append(identity(path))
    binding = ROOT / 'unity/Assets/Game/Resources/Combat/first-promotion-statuses.json'
    graphs = {row['id']: row['resource'] for row in json.loads(binding.read_bytes())['graphs']}
    assert all(entity in graphs for entity in (25013, 24082))
    proof = dict(manifest='884_156692', configuration=configuration,
        explicit_empty_dispel_defaults=['10992/DisperseStatus/ExcludeCharacterTypes'],
        original_code=originals,
        original_assemblies=[identity(code / 'managed/decoded/Assembly-CSharp.dll')],
        native_inputs=native,
        runtime_inputs=[identity(ROOT / path) for path in (
            'unity/Assets/Official/Combat/skill-rules.json',
            'unity/Assets/Official/Combat/battle-rules.json',
            'unity/Assets/Official/Combat/rules.json',
            'unity/Assets/Official/Skills/fidelity-texts.json',
            'unity/Assets/Official/Skills/source-manifest.json')]
            + source_manifests + views + [identity(binding)],
        verifier=identity(Path(__file__)),
        operation='APK-first exact verification of 10518/25013, original 10990 healing graph '
            'and two-round 24082 absolute damage reduction with existing native bindings. '
            'Server fatal-hit execution is absent; offline decisions and validation limits '
            'belong to docs/character/skills-fighter.md.')
    (ROOT / 'assets/reference/battle-rules/indomitable-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Verified original 10518/25013/10990 fatal-hit survival configuration, code and native bindings.')


if __name__ == '__main__':
    main()
