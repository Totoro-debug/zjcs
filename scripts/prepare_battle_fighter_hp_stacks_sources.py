"""Verify original 10582/10589 HP unit triggers, layers and healing reductions."""
import json
from pathlib import Path
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]


def main():
    entities = {}
    for name in ('skill-rules', 'battle-rules'):
        for key, value in json.loads((ROOT / f'unity/Assets/Official/Combat/{name}.json').read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(value)
    ids = ('25063', '25094', '24299', '24379', '25132', '25133')
    definitions = {key: entities[key] for key in ids}
    for source, stack, reduction in (('25063', 24299, 25133), ('25094', 24379, 25132)):
        for component, target in (('FightStatusHpDecreaseUnitComponent', stack),
                                  ('FightStatusHpIncreaseUnitComponent', reduction)):
            trigger = definitions[source][component]
            assert trigger['TargetType'] == 2 and trigger['UnitHpPer'] == .15000000596046448
            assert trigger['MaxCount'] == trigger['InvokeMaxCountOnHpChange'] == -1
            assert trigger['TryAtStart'] and not trigger['TriggerWhenHpLessThan0'] and not trigger['Skills']
            application, = trigger['StatusList']
            assert application['StatusId'] == target and application['ApplyTarget'] == 2
            assert application['BasePercent'] == 1 and not application['AffectedByProp']
        status = definitions[str(stack)]['FightStatusComponent']
        assert status['IsOpenStack'] and status['MaxStackedCount'] == 5
        assert status['DurationRound'] == -1 and not status['CanDispersed']
        assert status['InitStackCount'] == status['AddStackedCount'] == 1
        change = definitions[str(reduction)]['FightStatusStackChangeComponent']
        assert change['MaxCount'] == 5
        assert change['ChangeStatusIdAndChangeValue'] == [dict(StatusId=stack, ChangeValue=-1)]
        assert definitions[str(reduction)]['FightStatusComponent']['DurationRound'] == 0
    rules = json.loads((ROOT / 'unity/Assets/Official/Combat/rules.json').read_bytes())
    items = [row for row in rules['skill'] if row['ClassId'] in (10582, 10589)]
    assert {row['ClassId']: row['EcEntityId'] for row in items} == {10582: 25063, 10589: 25094}
    configuration = verify_selected_components(definitions, items)
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    originals = [identity(code / 'common-source/Common' / (name + '.cs')) for name in (
        'FightStatusHpDecreaseUnitComponent', 'FightStatusHpDecreaseUnitComponentInfo',
        'FightStatusHpIncreaseUnitComponent',
        'FightStatusStackChangeComponent', 'FightStatusStackChangeComponentInfo',
        'FightStatusPropComponent', 'FightStatusComponentImplement', 'StatusTargetType')]
    originals.extend(identity(code / 'client-source' / (name + '.cs')) for name in (
        'MyFightStatusHpDecreaseUnit', 'MyFightStatusHpIncreaseUnit', 'MyFightStatusStackChange'))
    source = ROOT / 'assets/reference/battle-animation/first-promotion-status-assets-source-manifest.json'
    document = json.loads(source.read_bytes())
    selected = lambda entry: any('/' + key + '/' in entry['path'] for key in ids)
    inputs = [entry for entry in document['native_inputs'] if selected(entry)]
    outputs = [entry for entry in document['outputs'] if selected(entry)]
    assert len(inputs) == len(outputs) == 18
    for entry in inputs + outputs:
        assert identity(ROOT / entry['path']) == entry
    binding = ROOT / 'unity/Assets/Game/Resources/Combat/first-promotion-statuses.json'
    graphs = {row['id'] for row in json.loads(binding.read_bytes())['graphs']}
    assert all(int(key) in graphs for key in ids)
    proof = dict(manifest='884_156692', configuration=configuration,
        original_code=originals,
        original_assemblies=[identity(code / 'managed/decoded/Assembly-CSharp.dll')],
        native_inputs=inputs, runtime_inputs=[identity(source), identity(binding)] + outputs + [identity(ROOT / path) for path in (
            'unity/Assets/Official/Combat/skill-rules.json',
            'unity/Assets/Official/Combat/battle-rules.json',
            'unity/Assets/Official/Combat/rules.json',
            'unity/Assets/Official/Skills/fidelity-texts.json')],
        verifier=identity(Path(__file__)),
        operation='APK-first exact verification of Fighter 10582/10589 original HP-unit '
            'components, five-layer statuses and healing stack-reduction entities with '
            'existing native assets. Runtime inputs are unchanged. Missing server threshold '
            'semantics and current validation limits belong to docs/features/character/skills-fighter.md.')
    (ROOT / 'assets/reference/battle-rules/fighter-hp-stacks-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Verified original 10582/10589 HP-unit components, six entities and 18 native bindings.')


if __name__ == '__main__':
    main()
