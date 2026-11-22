"""Convert the current original turn-bound defensive passive and its status graph."""
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
                   -2102852653: ('BattleGridItemEntities', 'MyFightStatusRoundStart'),
                   -550836577: ('BattleGridItemEntities', 'MyFightStatusProp'),
                   1640720687: ('StorySounds', 'MyPlayableSound')})
    build(DEST=ROOT / 'unity/Assets/Official/Combat/StandfastSoulGraphs/Resources/Combat/StandfastSoulGraphs',
          DATA=ROOT / 'unity/Assets/Game/Resources/Combat/standfast-soul-current-graphs.json',
          RECEIPT=ROOT / 'assets/reference/battle-animation/standfast-soul-current-graphs-source-manifest.json',
          ROOTS={}, graph_roots={key: f'Prefabs/EcEntity/Fight/FightSkill/{key}/{key}.prefab'
                                 for key in (25117, 24392)},
          MAPPED=mapped, family='StandfastSoulGraphs', shader_prefix='StandfastSoulGraph_',
          reference_scripts={-210918009: dict(name='WwiseEventReference',
              native_asset='Wwise/ScriptableObjects/Event/D6F01B79-6E79-4B12-B37B-EBBB12221E47.asset',
              runtime='unity/Assets/Game/Runtime/Official/ProfessionUI/WwiseEventReference.cs',
              receipt='assets/reference/profession-ui/audio-source-manifest.json')},
          generator=Path(__file__), native_manifest=source.version,
          native_cache=ROOT / '.tools/source-packages/current' / source.version / 'conqueror-passives',
          native_export_name='native-export',
          limits=['The owning skill verifies turn timing, cultivation, persistence and rendered Windows gameplay separately.'])


if __name__ == '__main__':
    main()
