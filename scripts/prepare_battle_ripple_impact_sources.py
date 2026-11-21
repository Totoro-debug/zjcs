"""Verify the original Knight last-shield ripple impact passive without replacing runtime inputs."""
import json
from pathlib import Path
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]


def main():
    entities = {}
    for name in ('skill-rules', 'battle-rules'):
        for key, definition in json.loads((ROOT / f'unity/Assets/Official/Combat/{name}.json').read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(definition)
    definitions = {key: entities[key] for key in ('25030', '11330', '11331', '11332', '24266', '24435')}
    action_end = definitions['25030']['FightStatusActionEndComponent']
    assert action_end['ActionTypes'] == [60] and action_end['IsTargetTypeAllEnd'] and action_end['Rate'] == 1
    cfg, = action_end['Skills']
    assert cfg['SkillId'] == 11330 and cfg['Source'] == 2 and cfg['Target'] == 2
    hit = definitions['11331']['FightHitFixedComponent']
    assert hit['DamageId'] == 11332 and len(hit['Scope']) == 13
    assert definitions['11330']['FightSkillComponent']['ElementType'] == 101
    assert hit['DamageBasicProp'] == 1 and hit['DamageProp'] == 200 and hit['FixedDamageProp'] == 190
    cold = definitions['24266']['FightStatusComponent']
    assert cold['DurationRound'] == 2 and cold['MaxStackedCount'] == 10
    statuses = definitions['11332']['FightDamageComponent']['StatusList']
    assert len(statuses) == 3 and all(row['StatusId'] == 24266 and row['BasePercent'] == 1 for row in statuses)
    rules = json.loads((ROOT / 'unity/Assets/Official/Combat/rules.json').read_bytes())
    skill, = (row for row in rules['skill'] if row['ClassId'] == 10563)
    assert skill['EcEntityId'] == 25030 and skill['PassiveStatusIdList'] == [25030]
    # Same explicit empty version default already verified for the full promotion closure.
    # Project only this named field; retain the historical input payload and its hash.
    disperse = definitions['11332']['FightDamageComponent']['DisperseStatus']
    assert 'ExcludeCharacterTypes' not in disperse
    disperse['ExcludeCharacterTypes'] = []
    configuration = verify_selected_components(definitions, [skill])
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    originals = [identity(code / ('common-source/Common/' + name + '.cs')) for name in (
        'FightStatusActionEndComponent', 'FightStatusActionEndComponentInfo', 'FightStatusComponentImplement',
        'FightStatusShieldPersistentComponentInfo', 'FightHitFixedComponentInfo',
        'StatusTriggerTarget', 'FightStatusTriggerSkillCfg', 'ActionType')]
    originals.append(identity(code / 'client-source/MyFightStatusActionEnd.cs'))
    asset_path = ROOT / 'assets/reference/battle-animation/first-promotion-status-assets-source-manifest.json'
    assets = json.loads(asset_path.read_bytes())
    native = [entry for entry in assets['native_inputs']
        if any(f'/{entity}/' in entry['path'] for entity in (25030, 24266))]
    assert len(native) == 8
    for entry in native:
        assert identity(ROOT / entry['path']) == entry
    binding = ROOT / 'unity/Assets/Game/Resources/Combat/first-promotion-statuses.json'
    graphs = {row['id']: row['resource'] for row in json.loads(binding.read_bytes())['graphs']}
    assert all(entity in graphs for entity in (25030, 24266))
    views = [ROOT / entry['path'] for entry in assets['outputs']
        if any(f'/{entity}/' in entry['path'] for entity in (25030, 24266))]
    assert len(views) == 8
    for path in views:
        record, = (entry for entry in assets['outputs'] if entry['path'] == path.relative_to(ROOT).as_posix())
        assert identity(path) == record
    views.append(binding)
    graph_path = ROOT / 'assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json'
    graph = json.loads(graph_path.read_bytes())
    graph_native = [entry for entry in graph['native_inputs'] if '/FightSkill/11330/' in entry['path']]
    graph_views = [ROOT / entry['path'] for entry in graph['outputs'] if '/FightSkill/11330/' in entry['path']]
    assert len(graph_native) == 8 and len(graph_views) == 8
    for entry in graph_native + [entry for entry in graph['outputs'] if '/FightSkill/11330/' in entry['path']]:
        assert identity(ROOT / entry['path']) == entry
    proof = dict(manifest='884_156692', configuration=configuration, original_code=originals,
        explicit_empty_dispel_defaults=['11332/DisperseStatus/ExcludeCharacterTypes'],
        original_assemblies=[identity(code / 'managed/decoded/Assembly-CSharp.dll')],
        native_inputs=native + graph_native,
        runtime_inputs=[identity(ROOT / path) for path in (
            'unity/Assets/Official/Combat/skill-rules.json',
            'unity/Assets/Official/Combat/battle-rules.json',
            'unity/Assets/Official/Combat/rules.json',
            'unity/Assets/Official/Skills/fidelity-texts.json',
            'unity/Assets/Official/Skills/source-manifest.json')]
            + [identity(asset_path), identity(graph_path)] + [identity(path) for path in views + graph_views],
        verifier=identity(Path(__file__)),
        operation='APK-first exact verification of 10563 -> 25030 -> 11330 -> 11331 -> 11332, '
            'last-shield action-end configuration, original water damage and three cold applications and existing native bindings. '
            'Server execution is absent. Offline decisions and verification limits '
            'belong to docs/character/skills-knight.md.')
    (ROOT / 'assets/reference/battle-rules/ripple-impact-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Verified original 10563/25030/11330 last-shield action-end configuration, code and native bindings.')


if __name__ == '__main__':
    main()
