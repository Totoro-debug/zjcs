"""Verify skill-graph configurations and record the confirmed charge and transient stack inputs."""
import json
from pathlib import Path

from prepare_battle_skill_combo_sources import ROOT, identity
from verify_battle_skill_graph_rules import verify_selected_components


def main():
    selected = json.loads((ROOT / 'official-source.json').read_text(encoding='utf-8-sig'))
    version = selected['active']
    assert version == '884_156692', 'Review a changed official source version.'
    current = ROOT / '.tools/source-packages/current' / version
    receipt = current / 'code-manifest.json'
    code = json.loads(receipt.read_text(encoding='utf-8-sig'))
    assert code['manifest'] == version
    assert code['apk_sha256'] == selected['profiles'][version]['apk']['sha256']
    assemblies = []
    for name in ('Assembly-CSharp.dll', 'Console.dll'):
        record = identity(current / 'official-code/managed/decoded' / name)
        assert record['sha256'] == code['assemblies'][name]['decoded_sha256']
        assemblies.append(record)
    source_names = ['client-source/MyFightSkillLogic.cs'] + [
        'common-source/Common/' + name + '.cs' for name in (
            'FightSkillLogicComponent', 'FightSkillLogicComponentInfo', 'EFightBlackBoardKeyValue',
            'FightHandleCfgNewIntInBB', 'FightHandleCfgNewInBB', 'FightHandleNewIntInBB',
            'FightHandleCfgChangeStatusStack', 'FightHandleChangeStatusStack',
            'FightHandleCfgGetStatusStackCount', 'FightHandleGetStatusStackCount',
            'FightHandleCfgDamageScaleBase', 'FightHandleCfgDamageScaleByScale',
            'FightHandleDamageScale', 'FightHandleDamageScaleByScale',
            'EntityHasAnyStatusByEntityKeyFilter', 'IteratingAgentStatusClassIdFilter')]
    paths = [ROOT / 'unity/Assets/Official/Combat' / name
             for name in ('skill-rules.json', 'battle-rules.json')]
    entities = {}
    for path in paths:
        for key, value in json.loads(path.read_text(encoding='utf-8-sig'))['entities'].items():
            entities.setdefault(key, {}).update(value)
    definitions = {key: entities[key] for key in ('13820', '13830', '13840', '50210',
                                                '24031', '24266', '24697', '24823')}
    item_path = ROOT / 'unity/Assets/Official/Combat/rules.json'
    skill_items = [row for row in json.loads(item_path.read_text(encoding='utf-8-sig'))['skill']
                   if row['EcEntityId'] in (13820, 13830, 13840, 50210)]
    assert [(row['ClassId'], row['EcEntityId']) for row in skill_items] == [
        (10196, 13820), (10197, 13830), (10198, 13840), (16020, 50210)]
    logic_definitions = {key: value for key, value in entities.items()
                         if 'FightSkillLogicComponent' in value}
    assert len(logic_definitions) == 15, 'Review the complete skill-logic configuration scope.'
    selected_configuration = verify_selected_components(
        {**logic_definitions, **definitions}, skill_items)
    for key in ('13830', '13840'):
        graph = definitions[key]['FightSkillLogicComponent']
        assert not any(graph[name] for name in ('OnDoHit', 'OnSkillStart', 'OnHitFinish'))
        assert [handle['_type'] for handle in graph['OnDoHitTransient']['HandleCfgs']] == [
            'FightHandleCfgNewIntInBB', 'FightHandleCfgChangeStatusStack']
    charge_graph = definitions['13820']['FightSkillLogicComponent']
    assert not charge_graph['OnDoHit'] and not charge_graph['OnHitFinish']
    assert [handle['_type'] for handle in charge_graph['OnSkillStart']['HandleCfgs']] == [
        'FightHandleCfgNewIntInBB', 'FightHandleCfgGetStatusStackCount', 'FightHandleCfgChangeStatusStack']
    charge_scale = charge_graph['OnDoHitTransient']['HandleCfgs']
    assert len(charge_scale) == 1 and charge_scale[0]['_type'] == 'FightHandleCfgDamageScaleByScale'
    assert charge_scale[0]['ScaleValue'] == 700
    current_graph = definitions['50210']['FightSkillLogicComponent']
    assert not any(current_graph[name] for name in ('OnDoHit', 'OnSkillStart', 'OnHitFinish'))
    read, scale = current_graph['OnDoHitTransient']['HandleCfgs']
    assert read['_type'] == 'FightHandleCfgGetStatusStackCount' and read['StatusId'] == 24823
    assert read['SourceEntityKey']['Id'] == 1 and read['StackCountKey']['Id'] == 719180550
    assert not read['StackCountKey']['IsPersistentBlackboard']
    assert scale['_type'] == 'FightHandleCfgDamageScaleByScale' and scale['ScaleValue'] == 2000
    assert scale['ScaleCountKey']['Id'] == read['StackCountKey']['Id']
    assert not scale['ScaleCountKey']['IsPersistentBlackboard'] and scale['StorageAsAbs']
    assert not definitions['50210']['FightSkillComponent']['CanTriggerChild']
    output = ROOT / 'assets/reference/battle-rules/skill-logic-stack-source-manifest.json'
    proof = dict(manifest=version, code_receipt=identity(receipt), assemblies=assemblies,
        official_sources=[identity(current / 'official-code' / name) for name in source_names],
        configuration_inputs=[identity(path) for path in paths + [item_path]],
        definitions=definitions, skill_items=skill_items, selected_configuration=selected_configuration,
        configuration_only_skill_logic_ids=sorted(
            (int(key) for key in logic_definitions if key not in ('13820', '13830', '13840', '50210'))),
        generator=identity(Path(__file__)),
        operation='Verify complete components for all 15 skill-logic entities against the selected version. '
            'Preserve the two transient stack graphs, the 13820 charge start/scale graph and status configuration. '
            'The APK exposes empty client/handler classes; execution uses the confirmed per-target, pre-HP, '
            'miss/immune/shield and child-status rules. Existing stack lifecycle is shared; '
            'The confirmed 13820 cast snapshot consumes valid charge instances at start and scales each damage '
            'segment by 1 + 0.07 * snapshot, truncating toward zero before shield/HP settlement; '
            'the confirmed 50210 graph rereads all valid owner 24823 stacks for each segment without consuming '
            'them and scales damage and AbsoluteDamage by 1 + 0.2 * current stacks, independently of CanTriggerChild; '
            'other skill graphs remain outside this implementation.')
    output.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Verified all 15 original skill-logic configurations; recorded 13820, 13830/13840 and 50210 inputs.')


if __name__ == '__main__':
    main()
