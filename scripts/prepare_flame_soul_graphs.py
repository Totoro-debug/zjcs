"""Convert the current original flame-soul critical-rate stack status graph."""
from pathlib import Path
from official_apk import ROOT
from official_source import OfficialSource
from battle_native_script_bindings import shared_bindings
from prepare_battle_status_orbit_assets import MAPPED, build


def main():
    source = OfficialSource()
    mapped = dict(MAPPED)
    shared = shared_bindings()
    mapped.update({1200213464: ('EntityLifecycle', 'MyEntity'),
                   -109006141: ('EntityActions', 'MyAction'),
                   -1481699058: ('BattleStatusPlayables', 'MyFightStatus'),
                   -550836577: ('BattleGridItemEntities', 'MyFightStatusProp'),
                   1640720687: shared['MAPPED'][1640720687]})
    event_binding = dict(shared['reference_scripts'][-210918009])
    event_binding['native_asset'] = 'Wwise/ScriptableObjects/Event/7975C507-8570-477B-AC0D-FDDB079B11C9.asset'
    build(DEST=ROOT / 'unity/Assets/Official/Combat/FlameSoulGraphs/Resources/Combat/FlameSoulGraphs',
          DATA=ROOT / 'unity/Assets/Game/Resources/Combat/flame-soul-current-graphs.json',
          RECEIPT=ROOT / 'assets/reference/battle-animation/flame-soul-current-graphs-source-manifest.json',
          ROOTS={}, graph_roots={24386: 'Prefabs/EcEntity/Fight/FightSkill/24386/24386.prefab'},
          MAPPED=mapped, reference_scripts={-210918009: event_binding},
          family='FlameSoulGraphs', shader_prefix='FlameSoulGraph_',
          generator=Path(__file__), native_manifest=source.version,
          native_cache=ROOT / '.tools/source-packages/current' / source.version / 'destroyer-flame-soul',
          native_export_name='native-export',
          limits=['Cultivated fire-cast conditions, stack cap, expiry, persistence and Windows gameplay are verified separately.'])


if __name__ == '__main__':
    main()
