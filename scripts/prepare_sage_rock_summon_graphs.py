"""Convert the current original Rock Emperor cast, attacks and damage graphs."""
from pathlib import Path

from battle_native_script_bindings import shared_bindings
from current_battle_native_skill_graphs import build_current_skill_graphs, verified_noise_mask_binding
from official_apk import ROOT
from official_source import OfficialSource
from prepare_battle_status_orbit_assets import MAPPED, build


def main():
    cache_name = 'sage-rock-summon-closure'
    source = OfficialSource()
    cache = ROOT / '.tools/source-packages/current' / source.version / cache_name
    shader = verified_noise_mask_binding(cache)
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/SageRockSummonGraphs/Resources/Combat/SageRockSummonGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/sage-rock-summon-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/sage-rock-summon-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [12140, 56800, 56810]),
            'hits': ('hit_entity_id', None, [12141, 56801, 56811]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', [56802, 56812]),
        },
        family='SageRockSummonGraphs', cache_name=cache_name, generator=Path(__file__),
        external_assets=shader,
    )
    statuses = {12142: '12140', 24216: '24216', 25110: '25110', 25121: '25121'}
    shared = shared_bindings()
    presentation = (-1211289337, 666328883, 1391534298, 1513771027, 1640720687)
    event = dict(shared['reference_scripts'][-210918009])
    event['native_asset'] = 'Wwise/ScriptableObjects/Event/F7EABFCF-9C49-4F80-9FD8-4D50CAFAFD7A.asset'
    build(
        DEST=ROOT / 'unity/Assets/Official/Combat/SageRockSummonStatuses/Resources/Combat/SageRockSummonStatuses',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/sage-rock-summon-current-status-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/sage-rock-summon-current-status-graphs-source-manifest.json',
        ROOTS={}, graph_roots={entity: f'Prefabs/EcEntity/Fight/FightSkill/{family}/{entity}.prefab'
                              for entity, family in statuses.items()},
        MAPPED={**MAPPED,
                **{script: shared['MAPPED'][script] for script in presentation},
                1200213464: ('EntityLifecycle', 'MyEntity'),
                -1481699058: ('BattleStatusPlayables', 'MyFightStatus'),
                -109006141: ('EntityActions', 'MyAction'),
                932710152: ('BattleGridItemEntities', 'MyStatusEnd'),
                1562105360: ('BattleGridItemEntities', 'MyFightGameplayTag'),
                -550836577: ('BattleGridItemEntities', 'MyFightStatusProp'),
                -310466705: ('BattleGridItemEntities', 'MyStatusAutoRemove'),
                -2102852653: ('BattleGridItemEntities', 'MyFightStatusRoundStart')},
        family='SageRockSummonStatuses', shader_prefix='SageRockSummonStatus_',
        generator=Path(__file__), native_manifest=source.version, native_cache=cache,
        native_export_name='native-export', external_assets=shader, reference_scripts={-210918009: event},
    )


if __name__ == '__main__':
    main()
