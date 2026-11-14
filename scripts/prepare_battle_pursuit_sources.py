"""Verify the original Knight debuff-conditioned pursuit passive without replacing runtime inputs."""
import json
from pathlib import Path
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]


def main():
    entities = {}
    for name in ('skill-rules', 'battle-rules'):
        for key, definition in json.loads((ROOT / f'unity/Assets/Official/Combat/{name}.json').read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(definition)
    definitions = {key: entities[key] for key in ('25008', '24009', '24020', '60421')}
    condition = definitions['25008']['FightStatusHitDmgAddPerComponent']
    assert condition['HitSourceType'] == 2 and condition['Damage'] and not condition['Cure']
    assert condition['Rate'] == 1 and condition['DurationSkillCount'] == -1
    assert not condition['ScalePropByStatusTypeCount']
    status_filter = condition['StatusType']
    assert status_filter['IsCondition'] and status_filter['Target'] == 5
    assert status_filter['AnyStatusTypes'] == [2, 3] and status_filter['AnyLogicTags'] == []
    assert status_filter['CountType'] == 1 and not status_filter['IsCalculateStack']
    rules = json.loads((ROOT / 'unity/Assets/Official/Combat/rules.json').read_bytes())
    skill, = (row for row in rules['skill'] if row['ClassId'] == 10516)
    assert skill['EcEntityId'] == 25008 and skill['PassiveStatusIdList'] == [25008]
    assert skill['PassivePropFactors'] == {'73': 2500}
    configuration = verify_selected_components(definitions, [skill])
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    originals = [identity(code / path) for path in (
        'common-source/Common/FightStatusHitDmgAddPerComponent.cs',
        'common-source/Common/FightStatusHitDmgAddPerComponentInfo.cs',
        'common-source/Common/FightStatusHitBaseComponent.cs',
        'common-source/Common/FightStatusHitBaseComponentInfo.cs',
        'common-source/Common/FightStatusStatusTypeCfg.cs',
        'common-source/Common/StatusTriggerTarget.cs',
        'common-source/Common/StatusType.cs',
        'client-source/MyFightStatusHitDmgAddPer.cs')]
    asset_path = ROOT / 'assets/reference/battle-animation/first-promotion-status-assets-source-manifest.json'
    assets = json.loads(asset_path.read_bytes())
    native = [entry for entry in assets['native_inputs']
        if any(f'/{entity}/{entity}.prefab' in entry['path'] for entity in (25008,))]
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
        + graphs[entity] + '.prefab') for entity in (25008,)]
    for path in views:
        record, = (entry for entry in assets['outputs'] if entry['path'] == path.relative_to(ROOT).as_posix())
        assert identity(path) == record
    proof = dict(manifest='884_156692', configuration=configuration, original_code=originals,
        original_assemblies=[identity(code / 'managed/decoded/Assembly-CSharp.dll')],
        native_inputs=native,
        runtime_inputs=[identity(ROOT / path) for path in (
            'unity/Assets/Official/Combat/skill-rules.json',
            'unity/Assets/Official/Combat/battle-rules.json',
            'unity/Assets/Official/Combat/rules.json',
            'unity/Assets/Official/Skills/fidelity-texts.json',
            'unity/Assets/Official/Skills/source-manifest.json')]
            + [identity(asset_path)] + [identity(path) for path in views],
        verifier=identity(Path(__file__)),
        operation='APK-first exact verification of 10516 -> 25008, target status-type 2/3 condition, '
            'unscaled original increments, passive resilience factor and native bindings. '
            'Server execution is unavailable; offline decisions and verification limits '
            'belong to docs/features/skills-knight.md.')
    (ROOT / 'assets/reference/battle-rules/pursuit-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Verified original 10516/25008 configuration, code and native status bindings.')


if __name__ == '__main__':
    main()
