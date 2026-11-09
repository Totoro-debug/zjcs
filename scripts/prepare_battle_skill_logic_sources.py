"""Verify skill-graph configurations and record the confirmed charge, stack and burning-grid inputs."""
import hashlib
import json
import re
from pathlib import Path
from copy import deepcopy

from prepare_battle_skill_combo_sources import ROOT, identity
from verify_battle_skill_graph_rules import verify_selected_components


def verify_pet_bindings(rows):
    from official_apk import Apk
    from official_tables import Constructors
    apk = Apk.current()
    try:
        parser = Constructors(ROOT / '.tools/source-packages/current' / apk.version / 'official-code')
        mapping = {name: cls for cls, name in re.findall(
            r'new (Tb\w+)\(loader\("([^"]+)"\)\)', parser.source('Tables'))}
        obj = apk.asset('config_luban', '/pet_evolution.g.bytes')
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        wanted = {(row['ClassId'], row['EvolutionPhase']): row for row in rows}
        selected = [row for row in parser.table(raw, mapping['pet_evolution'])
                    if (row['ClassId'], row['EvolutionPhase']) in wanted]
        assert len(selected) == len(wanted)
        for row in selected:
            historical = wanted[row['ClassId'], row['EvolutionPhase']]
            for field in ('UIModel', 'FightEntityClassId', 'SupportSkills', 'EntityActiveSkills'):
                assert row[field] == historical[field], (row['ClassId'], row['EvolutionPhase'], field)
        constructors = []
        for name in sorted(parser.sources):
            path = parser.source_root / (name + '.cs')
            if not path.is_file():
                path = parser.source_root.parent / 'Common.LubanGen' / (name + '.cs')
            assert path.is_file(), name
            constructors.append(identity(path))
        return dict(manifest=apk.version, source=apk.provenance(obj),
                    object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(),
                    payload_sha256=hashlib.sha256(raw).hexdigest(), rows=selected, constructors=constructors,
                    verified_fields=['UIModel', 'FightEntityClassId', 'SupportSkills', 'EntityActiveSkills'])
    finally:
        apk.archive.close()


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
    source_names = ['client-source/MyFightSkillLogic.cs', 'client-source/MyFightStatusCureReduce.cs'] + [
        'common-source/Common/' + name + '.cs' for name in (
            'FightSkillLogicComponent', 'FightSkillLogicComponentInfo', 'EFightBlackBoardKeyValue',
            'FightHandleCfgCounterReset', 'FightHandleCounterReset', 'CounterGreaterEqualFilter',
            'FightHandleCfgApplyStatussBySkill', 'FightHandleApplyStatussBySkill',
            'LogicNotFilter', 'EntityStatusStackCountByEntityKeyFilter',
            'EntityHpPercentByEntityKeyFilter', 'FightHandleCfgCollectionEntitiesByFaction2Battle',
            'FightHandleCollectionEntitiesByFaction2Battle', 'FightHandleCfgSortEntitiesByHpPercent',
            'FightHandleSortEntitiesByHpPercent', 'IsSkillTargetFilter',
            'FightHandleCfgTriggerHemophagiaSkill', 'FightHandleTriggerHemophagiaSkill',
            'FightStatusCureReduceComponent', 'FightStatusCureReduceComponentInfo', 'FightStatusTriggerStatusCfg',
            'FightHandleCfgNewIntInBB', 'FightHandleCfgNewInBB', 'FightHandleNewIntInBB',
            'FightHandleCfgChangeStatusStack', 'FightHandleChangeStatusStack',
            'FightHandleCfgGetStatusStackCount', 'FightHandleGetStatusStackCount',
            'FightHandleCfgDamageScaleBase', 'FightHandleCfgDamageScaleByScale',
            'FightHandleDamageScale', 'FightHandleDamageScaleByScale',
            'EntityHasAnyStatusByEntityKeyFilter', 'IteratingAgentStatusClassIdFilter',
            'FightHandleCfgCollectionEntitiesByRoleRoot', 'FightHandleCollectionEntitiesByRoleRoot',
            'FightHandleCfgBlackboardReadEcIdArrayCountAndStorage', 'FightHandleBlackboardReadEcIdArrayCountAndStorage',
            'FightHandleBlackboardReadListCountAndStorage', 'LogicFilter', 'EntityClassIdFilter',
            'SameFactionFilter', 'RangeFilter', 'FightHandleCfgForeachEntities', 'FightHandleForeachEntities',
            'FightHandleCfgTriggerSkillBySkill', 'FightHandleTriggerSkillBySkill',
            'FightHandleCfgGetMasterEntityAndStore', 'FightHandleGetMasterEntityAndStore',
            'FightHitFixedComponentInfo', 'FightHitRandomTargetComponentInfo', 'HitScopCfg',
            'FightStatusDamageFalloffComponentInfo')]
    paths = [ROOT / 'unity/Assets/Official/Combat' / name
             for name in ('skill-rules.json', 'battle-rules.json')]
    entities = {}
    for path in paths:
        for key, value in json.loads(path.read_text(encoding='utf-8-sig'))['entities'].items():
            entities.setdefault(key, {}).update(value)
    definitions = {key: entities[key] for key in ('13730', '13740', '13741', '13745', '13746', '13820', '13830', '13840', '50210',
                                                '24031', '24266', '24697', '24823', '54370', '54371', '54372', '54373', '54374', '54375', '54376', '9320', '13960', '13961', '10027', '13965', '13966', '13967', '24721', '24223', '25136', '24435', '12750', '10160', '50213', '50230', '50231', '50235', '50236', '50237', '64450', '64451', '64452', '64453', '24825', '24772', '24773', '13870', '13871', '13970', '13972', '13640', '13641', '13643', '13644', '13645')}
    definitions = deepcopy(definitions)
    empty_defaults = []
    for key in ('10027', '13967', '50237', '13645'):
        disperse = definitions[key]['FightDamageComponent']['DisperseStatus']
        assert disperse['BasePercent'] == 0 and disperse['Count'] == 0
        assert not disperse.get('ExcludeCharacterTypes')
        if 'ExcludeCharacterTypes' not in disperse:
            disperse['ExcludeCharacterTypes'] = []
            empty_defaults.append(dict(entity=key, field='FightDamageComponent.DisperseStatus.ExcludeCharacterTypes', value=[]))
    item_path = ROOT / 'unity/Assets/Official/Combat/rules.json'
    skill_items = [row for row in json.loads(item_path.read_text(encoding='utf-8-sig'))['skill']
                   if row['EcEntityId'] in (13640, 13730, 13740, 13820, 13830, 13840, 13870, 13960, 13970, 50210, 50230, 54370)]
    assert [(row['ClassId'], row['EcEntityId']) for row in skill_items] == [
        (10192, 13730), (10193, 13740), (10196, 13820), (10197, 13830), (10198, 13840),
        (10201, 13870), (10204, 13640), (10208, 13960), (10209, 13970),
        (16020, 50210), (16021, 50230), (16442, 54370)]
    pet_rows = json.loads(item_path.read_text(encoding='utf-8-sig'))['pet_evolution']
    pet_evolution = next(row for row in pet_rows
                         if row['ClassId'] == 1620 and row['EvolutionPhase'] == 2)
    assert pet_evolution['FightEntityClassId'] == 9320 and 16442 in pet_evolution['EntityActiveSkills']
    pet_support_bindings = [next(row for row in pet_rows
                                if row['ClassId'] == pet_id and row['EvolutionPhase'] == 1)
                            for pet_id in (1620, 1622)]
    for row, entity_id, support in zip(pet_support_bindings, (9220, 9222), (16020, 16021)):
        assert row['FightEntityClassId'] == entity_id and row['SupportSkills'] == [support]
        definition = entities[str(entity_id)]
        assert 'FightRolePetComponent' in definition
        assert definition['FightRoleCharacterComponent']['CharacterType'] == 5
        definitions[str(entity_id)] = deepcopy(definition)
    pet_selected_configuration = verify_pet_bindings([pet_evolution, *pet_support_bindings])
    logic_definitions = {key: value for key, value in entities.items()
                         if 'FightSkillLogicComponent' in value}
    assert len(logic_definitions) == 15, 'Review the complete skill-logic configuration scope.'
    selected_configuration = verify_selected_components(
        {**logic_definitions, **definitions}, skill_items, entity_paths={
            '13871': 'Assets/Config/EC/FightSkill_part194.g.bytes'})
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
    burn_graph = definitions['13730']['FightSkillLogicComponent']
    assert not any(burn_graph[name] for name in ('OnDoHit', 'OnSkillStart', 'OnHitFinish'))
    collect, read, scale = burn_graph['OnDoHitTransient']['HandleCfgs']
    assert collect['_type'] == 'FightHandleCfgCollectionEntitiesByRoleRoot' and collect['CharacterType'] == 6
    entity, faction, area = collect['FilterCondition']['SubFilters']
    assert collect['FilterCondition']['_type'] == 'LogicFilter' and collect['FilterCondition']['LogicType'] == 0
    assert entity['_type'] == 'EntityClassIdFilter' and entity['ClassId'] == 3051
    assert faction['_type'] == 'SameFactionFilter' and faction['CheckEntityKey']['Id'] == 3
    assert area['_type'] == 'RangeFilter' and area['CenterEntity']['Id'] == 2002
    assert len(area['Range']) == 13 and not area['UseWorldPosByCenterEntityTransform'] and area['ConsiderTargetBodySize']
    assert read['_type'] == 'FightHandleCfgBlackboardReadEcIdArrayCountAndStorage'
    assert read['ListBBKey']['Id'] == collect['ResultEntityArrayKey']['Id'] == 406339594
    assert read['StorageBBKey']['Id'] == scale['ScaleCountKey']['Id'] == 511879177
    assert scale['_type'] == 'FightHandleCfgDamageScaleByScale' and scale['ScaleValue'] == 500 and scale['StorageAsAbs']
    explosion = definitions['13740']['FightSkillLogicComponent']
    assert not any(explosion[name] for name in ('OnDoHitTransient', 'OnSkillStart', 'OnHitFinish'))
    collect, each = explosion['OnDoHit']['HandleCfgs']
    assert collect['_type'] == 'FightHandleCfgCollectionEntitiesByRoleRoot' and collect['CharacterType'] == 6
    assert each['_type'] == 'FightHandleCfgForeachEntities' and each['ForeachCount'] == -1
    trigger, = each['HandleCfgs']
    assert trigger['_type'] == 'FightHandleCfgTriggerSkillBySkill' and trigger['SkillEntityClassId'] == 13745 and trigger['Rate'] == 1
    assert trigger['SourceEntityKey']['Id'] == 3 and trigger['TargetEntityKey']['Id'] == 1
    assert len(collect['FilterCondition']['SubFilters'][2]['Range']) == 12
    assert definitions['13740']['FightSkillComponent']['CanTriggerChild']
    assert not definitions['13745']['FightSkillComponent']['CanTriggerChild']
    master = definitions['54370']['FightSkillLogicComponent']
    read, trigger = master['OnSkillStart']['HandleCfgs']
    assert read['_type'] == 'FightHandleCfgGetMasterEntityAndStore'
    assert read['EntityKey']['Id'] == 1 and read['StorageBBKey']['Id'] == 796681548
    assert trigger['_type'] == 'FightHandleCfgTriggerSkillBySkill' and trigger['SkillEntityClassId'] == 54373
    assert trigger['SourceEntityKey']['Id'] == 1 and trigger['TargetEntityKey']['Id'] == 796681548 and trigger['Rate'] == 1
    trigger, = definitions['54373']['FightSkillLogicComponent']['OnDoHit']['HandleCfgs']
    assert trigger['_type'] == 'FightHandleCfgTriggerSkillBySkill' and trigger['SkillEntityClassId'] == 54375
    assert trigger['SourceEntityKey']['Id'] == 1 and trigger['TargetEntityKey']['Id'] == 2003 and trigger['Rate'] == 1
    for key in ('54370', '54373', '54375'):
        assert definitions[key]['FightSkillComponent']['CanTriggerChild']
    for key in ('54371', '54376'):
        hit = definitions[key]['FightHitSummonComponent']
        assert hit['SummonCfg'] == {'SummonId': 3320, 'SummonStatus': [54372], 'ReferenceType': 1,
                                   'GridItemFindAdjacentIfBlocked': False, '_type': 'FightHitSummonGridItemCfg'}
        assert all(cell['Rate'] == .5 for cell in hit['SummonScope'])
    finish = definitions['13960']['FightSkillLogicComponent']
    reset, = finish['OnSkillStart']['HandleCfgs']
    trigger, = finish['OnHitFinish']['HandleCfgs']
    assert finish['OnSkillStart']['FilterCondition']['StatusClassIds'] == [24721]
    assert reset['_type'] == 'FightHandleCfgCounterReset' and reset['ResetValue'] == 1
    assert reset['CounterBBKey']['Id'] == 2081371075 and not reset['CounterBBKey']['IsPersistentBlackboard']
    condition = finish['OnHitFinish']['FilterCondition']
    assert condition['_type'] == 'CounterGreaterEqualFilter' and condition['CounterBBKey']['IsPersistentBlackboard']
    assert condition['CounterBBKey']['Id'] == 2081371075 and condition['Value'] == 1
    assert trigger['_type'] == 'FightHandleCfgTriggerHemophagiaSkill' and trigger['SkillId'] == 13965 and trigger['ScaleValue'] == 5000
    assert {name: trigger[name]['Id'] for name in ('SourceEntityKey', 'SkillEntityKey', 'ParentEntityKey', 'DamageKey')} == {
        'SourceEntityKey': 1, 'SkillEntityKey': 4, 'ParentEntityKey': 2001, 'DamageKey': 1026}
    assert not definitions['13965']['FightSkillComponent']['CanTriggerChild']
    assert definitions['13965']['FightSkillComponent']['CanTriggerStatusEntityIds'] == [25136]
    assert sorted(key for key, value in entities.items() if 'FightStatusCureReduceComponent' in value) == ['24223', '25136']
    ban = definitions['24223']['FightStatusCureReduceComponent']
    convert = definitions['25136']['FightStatusCureReduceComponent']
    assert ban['ExcuteOrder'] == 0 and ban['ReduceCurePer'] == ban['Rate'] == 1 and not ban['IsCheckSkillTrigger'] and not ban['StatusCfgs']
    assert convert['ExcuteOrder'] == 1001 and convert['ReduceCurePer'] == convert['Rate'] == 1 and convert['IsCheckSkillTrigger']
    assert len(convert['StatusCfgs']) == 1 and convert['StatusCfgs'][0]['StatusId'] == 24435 and convert['StatusCfgs'][0]['ApplyTarget'] == 2
    query = convert['FilterCondition']['Query']
    assert query['Length'] == 1 and query['Desc']['V0']['Type'] == 2
    assert query['Desc']['V0']['Tags'] == dict(V0=256, V1=0, V2=0, V3=0)
    for key in ('50213', '50230', '64450'):
        assert definitions[key]['FightSkillComponent']['CanTriggerChild'] is False
    start = definitions['50213']['FightSkillLogicComponent']['OnSkillStart']
    apply, = start['HandleCfgs']
    assert apply['_type'] == 'FightHandleCfgApplyStatussBySkill'
    assert apply['TargetEntityKey']['Id'] == 2003
    assert apply['TriggerStatusCfgs'][0]['StatusId'] == 24825
    recovery = definitions['50230']['FightSkillLogicComponent']['OnDoHit']
    master, trigger = recovery['HandleCfgs']
    assert master['_type'] == 'FightHandleCfgGetMasterEntityAndStore'
    assert master['StorageBBKey']['Id'] == trigger['TargetEntityKey']['Id'] == 796681548
    assert trigger['_type'] == 'FightHandleCfgTriggerSkillBySkill'
    assert trigger['SkillEntityClassId'] == 50235 and trigger['Rate'] == 1
    transient = definitions['64450']['FightSkillLogicComponent']['OnDoHitTransient']
    assert transient['FilterCondition']['_type'] == 'LogicNotFilter'
    condition = transient['FilterCondition']['TargetFilter']
    assert condition['_type'] == 'EntityStatusStackCountByEntityKeyFilter'
    assert condition['StatusId'] == 24773 and condition['StackCount'] == condition['Relation'] == 1
    apply, = transient['HandleCfgs']
    status, = apply['TriggerStatusCfgs']
    assert status['StatusId'] == 24772 and status['BasePercent'] == .5 and not status['AffectedByProp']
    for key in ('13870', '13970'):
        graph = definitions[key]['FightSkillLogicComponent']
        assert not any(graph[name] for name in ('OnSkillStart', 'OnDoHit', 'OnHitFinish'))
        flow = graph['OnDoHitTransient']
        read, scale = flow['HandleCfgs']
        assert read['_type'] == 'FightHandleCfgNewIntInBB' and read['Value'] == 1
        assert read['BBKey']['Id'] == scale['ScaleCountKey']['Id'] == 563722184
        assert not read['BBKey']['IsPersistentBlackboard'] and not scale['ScaleCountKey']['IsPersistentBlackboard']
        assert scale['_type'] == 'FightHandleCfgDamageScaleByScale'
        assert scale['ScaleValue'] == 5000 and scale['StorageAsAbs']
        assert scale['DamageKey']['Id'] == scale['StorageDamageKey']['Id'] == 2004
        assert scale['BaseScaleKey']['Id'] == 774082833 and scale['DeltaDamageKey']['Id'] == 122229964
    hp = definitions['13870']['FightSkillLogicComponent']['OnDoHitTransient']['FilterCondition']
    assert hp['_type'] == 'EntityHpPercentByEntityKeyFilter' and hp['EntityKey']['Id'] == 2002
    assert hp['Relation'] == 4 and hp['HpPercent'] == 0.30000001192092896
    status = definitions['13970']['FightSkillLogicComponent']['OnDoHitTransient']['FilterCondition']
    assert status['_type'] == 'EntityHasAnyStatusByEntityKeyFilter'
    assert status['EntityKey']['Id'] == 1 and status['StatusClassIds'] == [24721]
    collect, sort, each = definitions['13640']['FightSkillLogicComponent']['OnDoHit']['HandleCfgs']
    assert collect['_type'] == 'FightHandleCfgCollectionEntitiesByFaction2Battle'
    assert sort['_type'] == 'FightHandleCfgSortEntitiesByHpPercent' and sort['IsAscendingOrder']
    assert each['_type'] == 'FightHandleCfgForeachEntities' and each['ForeachCount'] == 2
    trigger, = each['HandleCfgs']
    assert trigger['SkillEntityClassId'] == 13643 and trigger['Rate'] == 1
    area, eligible = collect['FilterCondition']['SubFilters']
    assert area['_type'] == 'RangeFilter' and area['ConsiderTargetBodySize'] and len(area['Range']) == 113
    assert eligible['_type'] == 'IsSkillTargetFilter'
    output = ROOT / 'assets/reference/battle-rules/skill-logic-stack-source-manifest.json'
    proof = dict(manifest=version, code_receipt=identity(receipt), assemblies=assemblies,
        official_sources=[identity(current / 'official-code' / name) for name in source_names],
        configuration_inputs=[identity(path) for path in paths + [item_path]],
        definitions=definitions, skill_items=skill_items, pet_evolution=pet_evolution,
        pet_support_bindings=pet_support_bindings, pet_selected_configuration=pet_selected_configuration,
        selected_configuration=selected_configuration,
        historical_payload_empty_defaults=empty_defaults,
        configuration_only_skill_logic_ids=sorted(
            (int(key) for key in logic_definitions if key not in ('13730', '13740', '13820', '13830', '13840', '50210', '54370', '54373', '13960', '50213', '50230', '64450', '13870', '13970', '13640'))),
        generator=identity(Path(__file__)),
        operation='Verify complete components for all 15 skill-logic entities against the selected version. '
            'Preserve the two transient stack graphs, the 13820 charge start/scale graph and status configuration. '
            'The APK exposes empty client/handler classes; execution uses the confirmed per-target, pre-HP, '
            'miss/immune/shield and child-status rules. Existing stack lifecycle is shared; '
            'The confirmed 13820 cast snapshot consumes valid charge instances at start and scales each damage '
            'segment by 1 + 0.07 * snapshot, truncating toward zero before shield/HP settlement; '
            'the confirmed 50210 graph rereads all valid owner 24823 stacks for each segment without consuming '
            'them and scales damage and AbsoluteDamage by 1 + 0.2 * current stacks, independently of CanTriggerChild; '
            'the confirmed 13730 graph rereads live same-faction 3051 grid entities within the original 13 cells '
            'around each hit target and scales each segment by 1 + 0.05 * entity count without consumption or an extra cap; '
            'the confirmed 13740 OnDoHit graph snapshots matching same-faction 3051 tiles in the original 12 cells '
            'once per 13741 execution, triggering one inherited 13745 per tile without consumption or extra action; '
            'the confirmed area attack rule settles each occupied body cell separately, preserving original '
            'cell delays and explicit random selection constraints and applying existing falloff per occurrence; '
            '54370 follows the official MasterId lookup contract and triggers 54373 at the explicit master; '
            '54373 triggers 54375 once per original hit execution at SkillTargetEntity, retaining child policy, '
            'caster identity, inherited cultivation and original grid-spawn configuration; '
            '13960 captures effective 24721 at successful cast start and sums only original-node actual HP loss; '
            'it triggers one inherited 13965 per completed node with a truncated 50% custom cure base, '
            'excluding shields, overkill and other child damage. Cure interceptors run after original cure modifiers '
            'and before HP clipping in stable original ExcuteOrder, passing actual reduced cure as CustomParam '
            'to original statuses; 13965 follows its explicit 25136 whitelist and that Without-256 filter treats '
            'absent tags as empty. Both missing-server execution rules were confirmed by the user. '
            'Four reused historical damage components omit only the recorded optional empty exclusion lists; '
            'strict selected-884 verification checks those exact empty defaults without rewriting payloads or expected hashes. '
            'The three user-approved exceptions execute only their explicitly authored skill-body graphs: '
            '50213 applies original 24825 to the master target at start; 50230 triggers original 50235 '
            'once per original hit node at its master; 64450 checks the original 24773 condition and '
            '50 percent stream before applying 24772. Their original CanTriggerChild=false remains '
            'effective for automatic hit children and downstream status reactions. These applications '
            'do not claim that the dependent pet-transfer or status-stack-change graphs are implemented. '
            '13870 checks the original strictly-less HP float threshold on every body-cell occurrence; '
            '13970 checks current effective owner 24721. Both scale the settled pre-shield segment by 1.5. '
            '13640 collects original skill-eligible units within the original 113-cell body-aware range, '
            'snapshots exact HP ratios and stable battle order, and triggers original 13643 at the first two. '
            'The user approved full-HP eligibility after injured candidates; this does not claim to recover '
            'the missing server CheckSkillCureCondition. The collection capability also supplies cure '
            'availability so a full-HP anchor does not hide injured selected allies. '
            'Dependent status-change and pet-transfer graphs remain outside this implementation.')
    output.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Verified all 15 enabled skill-body graphs, their selected-version dependencies and confirmed offline rules; dependent status event graphs remain separate.')


if __name__ == '__main__':
    main()
