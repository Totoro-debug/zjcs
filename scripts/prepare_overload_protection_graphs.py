"""Convert the current original persistent shield graph for Overload Protection."""
from pathlib import Path

from battle_native_script_bindings import shared_bindings
from official_apk import ROOT
from official_source import OfficialSource
from prepare_battle_status_orbit_assets import MAPPED, build


def main():
    source = OfficialSource()
    mapped = dict(MAPPED)
    shared = shared_bindings()
    mapped.update({1200213464: ('EntityLifecycle', 'MyEntity'),
                   -109006141: ('EntityActions', 'MyAction'),
                   -1481699058: ('BattleStatusPlayables', 'MyFightStatus'),
                   1598787400: ('BattleGridItemEntities', 'MyFightStatusShieldPersistent'),
                   1640720687: shared['MAPPED'][1640720687]})
    event_binding = dict(shared['reference_scripts'][-210918009])
    event_binding['native_asset'] = 'Wwise/ScriptableObjects/Event/0096C861-4CFE-4F4A-A553-4CB27EBDD981.asset'
    build(
        DEST=ROOT / 'unity/Assets/Official/Combat/OverloadProtectionGraphs/Resources/Combat/OverloadProtectionGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/overload-protection-current-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/overload-protection-current-graphs-source-manifest.json',
        ROOTS={}, graph_roots={24240: 'Prefabs/EcEntity/Fight/FightSkill/24240/24240.prefab'},
        MAPPED=mapped, reference_scripts={-210918009: event_binding},
        family='OverloadProtectionGraphs', shader_prefix='OverloadProtectionGraph_',
        generator=Path(__file__), native_manifest=source.version,
        native_cache=ROOT / '.tools/source-packages/current' / source.version / 'destroyer-overload',
        native_export_name='native-export',
        limits=['Cultivated shield amount, battle report counts, restore and rendered Windows gameplay are verified separately.'],
    )


if __name__ == '__main__':
    main()
