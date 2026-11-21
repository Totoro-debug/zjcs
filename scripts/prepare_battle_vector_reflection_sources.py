"""Verify the original Knight blocked-hit vector reflection passive without replacing runtime inputs."""
import json
from pathlib import Path
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]


def main():
    entities = {}
    for name in ('skill-rules', 'battle-rules'):
        for key, definition in json.loads((ROOT / f'unity/Assets/Official/Combat/{name}.json').read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(definition)
    definitions = {key: entities[key] for key in ('25021', '11230', '11231', '11232', '24435')}
    trigger, = definitions['25021']['FightStatusLogicComponent']['Triggers']
    assert trigger['_type'] == 'FightOnAfterBeDamageTriggerCfg'
    filters = trigger['Flow']['FilterCondition']['SubFilters']
    assert len(filters) == 4 and filters[0]['IsBlock']
    assert all(not value for key, value in filters[0].items() if key not in ('_type', 'IsBlock'))
    cfg, = trigger['Flow']['HandleCfgs'][1]['TriggerSkillCfgs']
    assert cfg['SkillId'] == 11230 and cfg['Source'] == 2 and cfg['Target'] == 1
    hit = definitions['11231']['FightHitFixedComponent']
    assert hit['DamageId'] == 11232 and hit['DamageBasicProp'] == 2
    assert hit['DamageProp'] == 200 and hit['FixedDamageProp'] == 190
    rules = json.loads((ROOT / 'unity/Assets/Official/Combat/rules.json').read_bytes())
    skill, = (row for row in rules['skill'] if row['ClassId'] == 10541)
    assert skill['EcEntityId'] == 25021 and skill['PassiveStatusIdList'] == [25021]
    # Same explicit empty version default already verified for the full promotion closure.
    # Project only this named field; retain the historical input payload and its hash.
    disperse = definitions['11232']['FightDamageComponent']['DisperseStatus']
    assert 'ExcludeCharacterTypes' not in disperse
    disperse['ExcludeCharacterTypes'] = []
    configuration = verify_selected_components(definitions, [skill])
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    originals = [identity(code / ('common-source/Common/' + name + '.cs')) for name in (
        'FightOnAfterBeDamageTriggerCfg', 'DamageTypeFilter', 'DamageSkillCanTriggerStatusFilter',
        'EntityHasActionTypeByEntityKeyFilter', 'SameFactionByEcIdKeyFilter',
        'FightHandleCfgBBEcId2EcEntityAndStore', 'FightHandleCfgTriggerSkills',
        'FightHandleTriggerSkillsByStatus', 'FightHandleCfgFireTriggerStatus',
        'FightStatusTriggerSkillCfg', 'StatusTriggerTarget', 'FightHitFixedComponentInfo')]
    asset_path = ROOT / 'assets/reference/battle-animation/first-promotion-status-assets-source-manifest.json'
    assets = json.loads(asset_path.read_bytes())
    native = [entry for entry in assets['native_inputs']
        if any(f'/{entity}/{entity}.prefab' in entry['path'] for entity in (25021,))]
    assert len(native) == 2
    for entry in native:
        assert identity(ROOT / entry['path']) == entry
        if entry['path'].endswith('.prefab'):
            text = (ROOT / entry['path']).read_text(encoding='utf-8')
            for field in ('myEffect', 'myEndEffect', 'LoopEffectPlayable', 'myPlayablePrefab',
                    'StackedPlayablePrefab', 'InvokePlayable', 'StackPlayable', 'MasterCastPlayable'):
                assert f'  {field}: {{fileID: 0}}' in text
            assert '  ShowSpriteAbovePlayer: 1' in text and '  StackPlayableMap: []' in text
    binding = ROOT / 'unity/Assets/Game/Resources/Combat/first-promotion-statuses.json'
    graphs = {row['id']: row['resource'] for row in json.loads(binding.read_bytes())['graphs']}
    views = [binding] + [ROOT / ('unity/Assets/Official/Combat/FirstPromotionStatuses/Resources/'
        + graphs[entity] + '.prefab') for entity in (25021,)]
    for path in views:
        record, = (entry for entry in assets['outputs'] if entry['path'] == path.relative_to(ROOT).as_posix())
        assert identity(path) == record
    graph_path = ROOT / 'assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json'
    graph = json.loads(graph_path.read_bytes())
    graph_native = [entry for entry in graph['native_inputs'] if '/FightSkill/11230/' in entry['path']]
    graph_views = [ROOT / entry['path'] for entry in graph['outputs'] if '/FightSkill/11230/' in entry['path']]
    assert len(graph_native) == 14 and len(graph_views) == 14
    for entry in graph_native + [entry for entry in graph['outputs'] if '/FightSkill/11230/' in entry['path']]:
        assert identity(ROOT / entry['path']) == entry
    proof = dict(manifest='884_156692', configuration=configuration, original_code=originals,
        explicit_empty_dispel_defaults=['11232/DisperseStatus/ExcludeCharacterTypes'],
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
        operation='APK-first exact verification of 10541 -> 25021 -> 11230 -> 11231 -> 11232, '
            'blocked-hit graph, original defence-based damage and existing native bindings. '
            'Server execution is absent. Offline decisions and verification limits '
            'belong to docs/character/skills-knight.md.')
    (ROOT / 'assets/reference/battle-rules/vector-reflection-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Verified original 10541/25021/11230 blocked-hit graph, code and native bindings.')


if __name__ == '__main__':
    main()
