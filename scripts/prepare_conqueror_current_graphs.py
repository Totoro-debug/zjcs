"""Convert verified current native status graphs for the conqueror profession."""
from pathlib import Path

from official_apk import ROOT
from official_source import OfficialSource
from prepare_battle_status_orbit_assets import MAPPED, build


def main():
    source = OfficialSource()
    mapped = dict(MAPPED)
    mapped.update({1200213464: ('EntityLifecycle', 'MyEntity'),
                   -109006141: ('EntityActions', 'MyAction'),
                   -1481699058: ('BattleStatusPlayables', 'MyFightStatus'),
                   -2102852653: ('BattleGridItemEntities', 'MyFightStatusRoundStart')})
    build(DEST=ROOT / 'unity/Assets/Official/Combat/ConquerorGraphs/Resources/Combat/ConquerorGraphs',
          DATA=ROOT / 'unity/Assets/Game/Resources/Combat/conqueror-current-graphs.json',
          RECEIPT=ROOT / 'assets/reference/battle-animation/conqueror-current-graphs-source-manifest.json',
          ROOTS={}, graph_roots={24235: 'Prefabs/EcEntity/Fight/FightSkill/24235/24235.prefab'},
          MAPPED=mapped, family='ConquerorGraphs', shader_prefix='ConquerorGraph_',
          generator=Path(__file__), native_manifest=source.version,
          native_cache=ROOT / '.tools/source-packages/current' / source.version / 'conqueror-actives',
          native_export_name='native-export',
          limits=['Each owning skill separately verifies rules, original UI, cultivation, persistence and rendered Windows gameplay.'])


if __name__ == '__main__':
    main()
