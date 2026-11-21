"""Verify Fighter 10016's original point target, dash, damage and native graph."""
import json
from pathlib import Path
from verify_battle_skill_graph_rules import identity, verify_selected_components

ROOT = Path(__file__).resolve().parents[1]


def main():
    entities = {}
    for name in ('skill-rules', 'battle-rules'):
        for key, value in json.loads((ROOT / f'unity/Assets/Official/Combat/{name}.json').read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(value)
    definitions = {key: entities[key] for key in ('10680', '10681', '10682', '10026')}
    skill = definitions['10680']['FightSkillComponent']
    assert skill['SkillTargetType'] == 2 and skill['TargetType'] == 1
    assert skill['Range'] == [dict(X=0, Y=5), dict(X=0, Y=-5), dict(X=5, Y=0), dict(X=-5, Y=0)]
    hit_ref, = skill['HitList']
    assert hit_ref['ClassId'] == 10681 and hit_ref['ActOnSouce'] == 1
    move, = skill['SourceMoveList']
    assert move['MoveBehaviourId'] == 10682 and move['Distance'] == -1
    assert move['Offset'] == dict(X=0, Y=0) and move['SkillMoveDirection'] == 1
    assert move['TowardsTargetPos'] and move['AiMustMoveTo']
    assert not move['AIIgnore'] and not move['CanExpandOffset'] and not move['DoMoveConditions']
    action = definitions['10682']['FightActionSkillMoveComponent']
    assert action['PassHeight'] == 3 and action['MoveDuration'] == .10000000149011612
    assert action['MoveSpeed'] == 0 and not action['ForceToTargetPos']
    hit = definitions['10681']['FightHitFixedComponent']
    assert hit['DamageId'] == 10026 and hit['DamageBasicProp'] == 1
    assert hit['DamageProp'] == 200 and hit['FixedDamageProp'] == 190
    assert hit['DamageActionType'] == 4 and hit['IsTrueDamage']
    assert [entry['Pos'] for entry in hit['Scope']] == [dict(X=0, Y=y) for y in range(1, 5)]
    damage = definitions['10026']['FightDamageComponent']
    assert not damage['StatusList'] and damage['ChildSkillCfg']['SkillId'] == 0
    assert 'ExcludeCharacterTypes' not in damage['DisperseStatus']
    # Compare the selected explicit empty default without changing historical runtime bytes.
    damage['DisperseStatus']['ExcludeCharacterTypes'] = []
    rules = json.loads((ROOT / 'unity/Assets/Official/Combat/rules.json').read_bytes())
    item, = (row for row in rules['skill'] if row['ClassId'] == 10016)
    assert item['EcEntityId'] == 10680
    configuration = verify_selected_components(definitions, [item])
    code = ROOT / '.tools/source-packages/current/884_156692/official-code'
    originals = [identity(code / 'common-source/Common' / (name + '.cs')) for name in (
        'FightSkillComponentInfo', 'FightSKillMoveCfg', 'SkillTargetType',
        'FightActionSkillMoveComponent', 'FightActionSkillMoveComponentInfo',
        'FightHitFixedComponentInfo', 'FightDamageComponentImplement')]
    originals.append(identity(code / 'client-source/MyFightActionSkillMove.cs'))
    source = ROOT / 'assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json'
    manifest = json.loads(source.read_bytes())
    inputs = [entry for entry in manifest['native_inputs'] if '/10680/' in entry['path']]
    outputs = [entry for entry in manifest['outputs'] if '/10680/' in entry['path']]
    assert len(inputs) == len(outputs) == 12
    for entry in inputs + outputs:
        assert identity(ROOT / entry['path']) == entry
    event = ROOT / ('unity/Assets/Official/Combat/SkillGraphRuntime/Resources/Combat/'
        'SkillGraphs/Wwise/ScriptableObjects/Event/1D66C9BB-7396-417B-92A1-B227F0FE848F.asset')
    assert 'objectName: Skill_10016\n  id: 3744235477\n' in event.read_text(encoding='utf8')
    sounds = ROOT / 'unity/Assets/Official/Combat/NativeSound/Resources/Combat/Wwise/catalog.json'
    assert dict(id=3744235477, name='Skill_10016') in json.loads(sounds.read_bytes())['events']
    proof = dict(manifest='884_156692', configuration=configuration,
        explicit_empty_dispel_defaults=['10026/DisperseStatus/ExcludeCharacterTypes'],
        original_code=originals,
        original_assemblies=[identity(code / 'managed/decoded/Assembly-CSharp.dll')],
        native_inputs=inputs, native_sound_inputs=[identity(event), identity(event.with_suffix('.asset.meta')), identity(sounds)],
        validation_inputs=[identity(ROOT / path) for path in (
            'scripts/test_battle_lightning_dash.ps1',
            'scripts/test_first_promotion_skill_player.ps1',
            'scripts/verify_lightning_dash_receipt.py',
            'scripts/test_acquired_active_skill_flow.ps1',
            'unity/Assets/Game/Runtime/Presentation/Home/SkillDamageTestValidation.cs',
            'unity/Assets/Game/Runtime/Presentation/Home/SkillDamageTestValidation.AcquiredActive.cs',
            'unity/Assets/Game/Runtime/Presentation/Home/SkillDamageTestValidation.AcquiredActive.cs.meta',
            'unity/Assets/Game/Runtime/Presentation/Home/Zjcs.Presentation.Home.asmdef',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleGraphCapabilityValidation.LightningDash.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleGraphCapabilityValidation.LightningDash.cs.meta')],
        runtime_inputs=[identity(source)] + outputs + [identity(ROOT / path) for path in (
            'unity/Assets/Official/Combat/skill-rules.json',
            'unity/Assets/Official/Combat/battle-rules.json',
            'unity/Assets/Official/Combat/rules.json',
            'unity/Assets/Official/Skills/fidelity-texts.json',
            'unity/Assets/Game/Resources/Combat/skill-playables.json',
            'unity/Assets/Game/Runtime/Offline/Combat/FightBattleExecutor.LightningDash.cs',
            'unity/Assets/Game/Runtime/Offline/Combat/FightBattleExecutor.LightningDash.cs.meta')],
        verifier=identity(Path(__file__)),
        operation='APK-first exact verification of 10016/10680 point targeting, original '
            '10682 movement and 10681/10026 damage, retaining historical runtime input identity. '
            'Server AI point selection/path generation is absent. Implementation decisions '
            'and current validation limits belong to docs/character/skills-fighter.md.')
    (ROOT / 'assets/reference/battle-rules/lightning-dash-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Verified original 10016/10680 point-target dash, damage and 12 native graph bindings.')


if __name__ == '__main__':
    main()
