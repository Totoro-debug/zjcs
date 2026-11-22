"""Convert the current original target missing-health damage passive entity."""
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
                   2100482724: ('BattleGridItemEntities', 'MyFightStatusHitApplyDamage')})
    build(DEST=ROOT / 'unity/Assets/Official/Combat/SoulBreakerGraphs/Resources/Combat/SoulBreakerGraphs',
          DATA=ROOT / 'unity/Assets/Game/Resources/Combat/soul-breaker-current-graphs.json',
          RECEIPT=ROOT / 'assets/reference/battle-animation/soul-breaker-current-graphs-source-manifest.json',
          ROOTS={}, graph_roots={25095: 'Prefabs/EcEntity/Fight/FightSkill/25095/25095.prefab'},
          MAPPED=mapped, family='SoulBreakerGraphs', shader_prefix='SoulBreakerGraph_',
          generator=Path(__file__), native_manifest=source.version,
          native_cache=ROOT / '.tools/source-packages/current' / source.version / 'conqueror-passives',
          native_export_name='native-export',
          limits=['The owning skill verifies damage boundaries, cultivation, persistence and Windows gameplay separately.'])


if __name__ == '__main__':
    main()
