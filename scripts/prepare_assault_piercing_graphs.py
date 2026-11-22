"""Convert the current original buff-scaled defence-ignore passive entity."""
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
                   -265745592: ('BattleGridItemEntities', 'MyFightStatusHitDmgAddPer')})
    build(DEST=ROOT / 'unity/Assets/Official/Combat/AssaultPiercingGraphs/Resources/Combat/AssaultPiercingGraphs',
          DATA=ROOT / 'unity/Assets/Game/Resources/Combat/assault-piercing-current-graphs.json',
          RECEIPT=ROOT / 'assets/reference/battle-animation/assault-piercing-current-graphs-source-manifest.json',
          ROOTS={}, graph_roots={25081: 'Prefabs/EcEntity/Fight/FightSkill/25081/25081.prefab'},
          MAPPED=mapped, family='AssaultPiercingGraphs', shader_prefix='AssaultPiercingGraph_',
          generator=Path(__file__), native_manifest=source.version,
          native_cache=ROOT / '.tools/source-packages/current' / source.version / 'conqueror-passives',
          native_export_name='native-export',
          limits=['The owning skill verifies damage boundaries, cultivation, persistence and Windows gameplay separately.'])


if __name__ == '__main__':
    main()
