"""Convert the current original fear status graph and its full native closure."""
from pathlib import Path

from official_apk import ROOT
from official_source import OfficialSource
from prepare_battle_status_orbit_assets import MAPPED, build


def main():
    source = OfficialSource()
    mapped = dict(MAPPED)
    mapped.update({1200213464: ('EntityLifecycle', 'MyEntity'),
                   -109006141: ('EntityActions', 'MyAction'),
                   -1481699058: ('BattleStatusPlayables', 'MyFightStatus')})
    build(DEST=ROOT / 'unity/Assets/Official/Combat/FearGraphs/Resources/Combat/FearGraphs',
          DATA=ROOT / 'unity/Assets/Game/Resources/Combat/fear-current-graphs.json',
          RECEIPT=ROOT / 'assets/reference/battle-animation/fear-current-graphs-source-manifest.json',
          ROOTS={}, graph_roots={24194: 'Prefabs/EcEntity/Fight/FightSkill/24194/24194.prefab'},
          MAPPED=mapped, family='FearGraphs', shader_prefix='FearGraph_',
          generator=Path(__file__), native_manifest=source.version,
          native_cache=ROOT / '.tools/source-packages/current' / source.version / 'conqueror-actives',
          native_export_name='native-export',
          limits=['Each owning skill separately verifies cultivation, targeting, persistence and rendered Windows gameplay.'])


if __name__ == '__main__':
    main()
