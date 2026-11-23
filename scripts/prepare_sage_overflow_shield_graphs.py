"""Convert the current original Overflow Shield listener and shield entity graphs."""
from pathlib import Path

from battle_native_script_bindings import shared_bindings
from current_battle_native_skill_graphs import verified_noise_mask_binding
from official_apk import ROOT
from official_source import OfficialSource
from prepare_battle_status_orbit_assets import MAPPED, build


def main():
    source = OfficialSource()
    cache = ROOT / '.tools/source-packages/current' / source.version / 'sage-overflow-shield'
    roots = {entity: f'Prefabs/EcEntity/Fight/FightSkill/{entity}/{entity}.prefab'
             for entity in (25106, 24174)}
    mapped = {
        **MAPPED,
        1200213464: ('EntityLifecycle', 'MyEntity'),
        -1481699058: ('BattleStatusPlayables', 'MyFightStatus'),
        -109006141: ('EntityActions', 'MyAction'),
        -1931582627: ('BattleGridItemEntities', 'MyFightStatusHitSkill'),
        1598787400: ('BattleGridItemEntities', 'MyFightStatusShieldPersistent'),
        1640720687: ('StorySounds', 'MyPlayableSound'),
    }
    event = dict(shared_bindings()['reference_scripts'][-210918009])
    event['native_asset'] = 'Wwise/ScriptableObjects/Event/23AFCE49-6164-404D-97B1-4055AEC43FB1.asset'
    build(
        DEST=ROOT / 'unity/Assets/Official/Combat/SageOverflowShieldGraphs/Resources/Combat/SageOverflowShieldGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/sage-overflow-shield-current-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/sage-overflow-shield-current-graphs-source-manifest.json',
        ROOTS={}, graph_roots=roots, MAPPED=mapped,
        family='SageOverflowShieldGraphs', shader_prefix='SageOverflowShieldGraph_',
        reference_scripts={-210918009: event},
        generator=Path(__file__), native_manifest=source.version, native_cache=cache,
        native_export_name='native-export', external_assets=verified_noise_mask_binding(cache),
        limits=['The owning skill separately verifies overflow, shield lifetime and rendered Windows gameplay.'],
    )


if __name__ == '__main__':
    main()
