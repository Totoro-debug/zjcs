"""Verify the original Knight shield-presence passive without replacing runtime inputs."""
import json
from pathlib import Path
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]


def main():
    entities = {}
    for name in ('skill-rules', 'battle-rules'):
        for key, definition in json.loads((ROOT / f'unity/Assets/Official/Combat/{name}.json').read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(definition)
    definitions = {key: entities[key] for key in ('25020', '24212', '11160', '11161', '11162', '24170', '24435')}
    condition = definitions['25020']['FightStatusActionExistComponent']
    assert condition == dict(StatusReplacePropertyValues=[], StatusId=24212, Conditions=[60])
    for key in ('25020', '24212'):
        status = definitions[key]['FightStatusComponent']
        assert not status['IsOpenStack'] and not status['CanDispersed']
        assert status['DurationRound'] == -1
    rules = json.loads((ROOT / 'unity/Assets/Official/Combat/rules.json').read_bytes())
    skill, = (row for row in rules['skill'] if row['ClassId'] == 10540)
    assert skill['EcEntityId'] == 25020 and skill['PassiveStatusIdList'] == [25020]
    guard, = (row for row in rules['skill'] if row['ClassId'] == 10046)
    assert guard['EcEntityId'] == 11160
    assert definitions['11162']['FightDamageComponent']['StatusList'][0]['StatusId'] == 24170
    # Same explicit empty version default already verified for the full promotion closure.
    # Project only this named field; retain the historical input payload and its hash.
    disperse = definitions['11162']['FightDamageComponent']['DisperseStatus']
    assert 'ExcludeCharacterTypes' not in disperse
    disperse['ExcludeCharacterTypes'] = []
    configuration = verify_selected_components(definitions, [skill, guard])
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    originals = [identity(code / path) for path in (
        'common-source/Common/FightStatusActionExistComponent.cs',
        'common-source/Common/FightStatusActionExistComponentInfo.cs',
        'common-source/Common/FightStatusComponentImplement.cs',
        'common-source/Common/FightStatusPropComponent.cs',
        'common-source/Common/FightStatusPropComponentInfo.cs',
        'common-source/Common/DamageDisperseStatusCfg.cs',
        'client-source/MyFightStatusActionExist.cs')]
    asset_path = ROOT / 'assets/reference/battle-animation/first-promotion-status-assets-source-manifest.json'
    assets = json.loads(asset_path.read_bytes())
    native = [entry for entry in assets['native_inputs']
        if any(f'/{entity}/{entity}.prefab' in entry['path'] for entity in (25020, 24212))]
    assert len(native) == 4
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
        + graphs[entity] + '.prefab') for entity in (25020, 24212)]
    for path in views:
        record, = (entry for entry in assets['outputs'] if entry['path'] == path.relative_to(ROOT).as_posix())
        assert identity(path) == record
    proof = dict(manifest='884_156692', configuration=configuration, original_code=originals,
        explicit_empty_dispel_defaults=['11162/DisperseStatus/ExcludeCharacterTypes'],
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
        operation='APK-first exact verification of 10540 -> 25020 -> 24212, action-60 presence '
            'condition, original status properties and native bindings. The shared/client '
            'action-presence classes do not contain server execution. Offline decisions and '
            'verification limits belong to docs/features/skills-knight.md.')
    (ROOT / 'assets/reference/battle-rules/shield-attack-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Verified original 10540/25020/24212 configuration, code and native status bindings.')


if __name__ == '__main__':
    main()
