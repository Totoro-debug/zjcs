"""Verify original 10061 buff, proximity trigger and child-skill native inputs."""
import json
from pathlib import Path
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]


def main():
    entities = {}
    for name in ('skill-rules', 'battle-rules'):
        for key, value in json.loads((ROOT / f'unity/Assets/Official/Combat/{name}.json').read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(value)
    definitions = {key: entities[key] for key in ('11200', '11201', '11202', '11203', '24210', '10860', '10861', '10862')}
    skill = definitions['11200']['FightSkillComponent']
    assert skill['SkillType'] == 2 and skill['TargetType'] == 2 and skill['SkillTargetType'] == 1
    assert [row['ClassId'] for row in skill['HitList']] == [11201, 11202]
    random = definitions['11202']['FightHitRandomTargetComponent']
    assert random['MiniHitTargetCount'] == 3 and random['NotSameTarget'] and not random['AllowEmptyScope']
    assert len(random['HitList']) == 3 and all(row['ClassId'] == 11201 for row in random['HitList'])
    status = definitions['24210']['FightStatusComponent']
    assert status['DurationRound'] == 2 and status['RoundTarget'] == 2 and not status['CanDispersed']
    near = definitions['24210']['FightStatusMoveNearComponent']
    assert near['TryAtStart'] and near['TryAtRound'] and near['TryAtMoved'] and near['TryAtEnterRange']
    assert not near['TryAtStandRound'] and not near['TryAtInRangeMove'] and len(near['Range']) == 8
    child, = near['Skills']
    assert child['SkillId'] == 10860 and child['Source'] == 3 and child['Target'] == 5
    assert child['BasePercent'] == 1 and not child['AffectedByProp'] and not child['UseAI']
    defaults = []
    for key in ('11203', '10862'):
        damage = definitions[key]['FightDamageComponent']
        assert 'ExcludeCharacterTypes' not in damage['DisperseStatus']
        damage['DisperseStatus']['ExcludeCharacterTypes'] = []
        defaults.append(key + '/DisperseStatus/ExcludeCharacterTypes')
    rules = json.loads((ROOT / 'unity/Assets/Official/Combat/rules.json').read_bytes())
    item, = (row for row in rules['skill'] if row['ClassId'] == 10061)
    configuration = verify_selected_components(definitions, [item])
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    originals = [identity(code / 'common-source/Common' / (name + '.cs')) for name in (
        'FightSkillComponentInfo', 'FightHitFixedComponentInfo', 'FightHitRandomTargetComponentInfo',
        'FightDamageComponentImplement', 'FightStatusComponentImplement', 'FightStatusMoveNearComponent',
        'FightStatusMoveNearComponentInfo', 'FightStatusTriggerSkillCfg', 'StatusTriggerTarget', 'HitScopCfg')]
    originals.extend(identity(code / 'client-source' / (name + '.cs')) for name in (
        'MyFightStatusMoveNear', 'MyFightHitBase'))
    native, outputs, sources = [], [], []
    for name, ids, count in (
        ('skill-native-graph-assets-source-manifest.json', (11200, 10860), 16),
        ('first-promotion-status-assets-source-manifest.json', (24210,), 2)):
        path = ROOT / 'assets/reference/battle-animation' / name
        document = json.loads(path.read_bytes())
        selected = lambda entry: any('/' + str(key) + '/' in entry['path'] for key in ids)
        inputs = [entry for entry in document['native_inputs'] if selected(entry)]
        views = [entry for entry in document['outputs'] if selected(entry)]
        assert len(inputs) == len(views) == count
        for entry in inputs + views:
            assert identity(ROOT / entry['path']) == entry
        native.extend(inputs); outputs.extend(views); sources.append(identity(path))
    binding = ROOT / 'unity/Assets/Game/Resources/Combat/first-promotion-statuses.json'
    assert any(row['id'] == 24210 for row in json.loads(binding.read_bytes())['graphs'])
    proof = dict(manifest='884_156692', configuration=configuration,
        explicit_empty_dispel_defaults=defaults, original_code=originals,
        original_assemblies=[identity(code / 'managed/decoded/Assembly-CSharp.dll')],
        native_inputs=native, runtime_inputs=sources + outputs + [identity(binding)] + [identity(ROOT / path) for path in (
            'unity/Assets/Official/Combat/skill-rules.json', 'unity/Assets/Official/Combat/battle-rules.json',
            'unity/Assets/Official/Combat/rules.json', 'unity/Assets/Game/Resources/Combat/skill-playables.json')],
        verifier=identity(Path(__file__)),
        operation='APK-first exact verification of 10061 recipient graph, 24210 proximity conditions and original 10860 child. '
            'Historical runtime inputs retained. Behavior and missing server rules belong to docs/features/character/skills-fighter.md.')
    (ROOT / 'assets/reference/battle-rules/portable-flame-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Verified original 10061 eight entities, twelve code inputs and eighteen native inputs/outputs.')


if __name__ == '__main__':
    main()
