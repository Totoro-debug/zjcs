"""Convert the current original flame-momentum listener; child graphs are shared."""
from pathlib import Path

from official_apk import ROOT
from official_source import OfficialSource
from prepare_battle_status_orbit_assets import MAPPED, build


def main():
    source = OfficialSource()
    cache = ROOT / '.tools/source-packages/current' / source.version / 'conqueror-passive-children'
    mapped = dict(MAPPED)
    mapped.update({1200213464: ('EntityLifecycle', 'MyEntity'),
                   -109006141: ('EntityActions', 'MyAction'),
                   -1481699058: ('BattleStatusPlayables', 'MyFightStatus'),
                   -812331650: ('BattleGridItemEntities', 'MyFightStatusLogic')})
    build(DEST=ROOT / 'unity/Assets/Official/Combat/FlameMomentumGraphs/Resources/Combat/FlameMomentumGraphs',
          DATA=ROOT / 'unity/Assets/Game/Resources/Combat/flame-momentum-current-graphs.json',
          RECEIPT=ROOT / 'assets/reference/battle-animation/flame-momentum-current-graphs-source-manifest.json',
          ROOTS={}, graph_roots={25010: 'Prefabs/EcEntity/Fight/FightSkill/25010/25010.prefab'},
          MAPPED=mapped, family='FlameMomentumGraphs', shader_prefix='FlameMomentumGraph_',
          generator=Path(__file__), native_manifest=source.version, native_cache=cache,
          native_export_name='native-export',
          limits=['The owning skill verifies critical damage, position-centred children, cultivation, persistence and rendered Windows gameplay separately.'])


if __name__ == '__main__':
    main()
