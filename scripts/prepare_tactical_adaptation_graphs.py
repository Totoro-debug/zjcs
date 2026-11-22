"""Convert the current original enemy-count passive and its two exclusive status graphs."""
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
                   1421486109: ('BattleGridItemEntities', 'MyFightStatusMoveRange'),
                   -550836577: ('BattleGridItemEntities', 'MyFightStatusProp'),
                   -194148604: ('StoryPlayables', 'MyPlayableApplyObj'),
                   1640720687: ('StorySounds', 'MyPlayableSound')})
    build(DEST=ROOT / 'unity/Assets/Official/Combat/TacticalAdaptationGraphs/Resources/Combat/TacticalAdaptationGraphs',
          DATA=ROOT / 'unity/Assets/Game/Resources/Combat/tactical-adaptation-current-graphs.json',
          RECEIPT=ROOT / 'assets/reference/battle-animation/tactical-adaptation-current-graphs-source-manifest.json',
          ROOTS={}, graph_roots={key: f'Prefabs/EcEntity/Fight/FightSkill/{key}/{key}.prefab'
                                 for key in (25118, 24393, 24394)},
          MAPPED=mapped, family='TacticalAdaptationGraphs', shader_prefix='TacticalAdaptationGraph_',
          reference_scripts={-210918009: dict(name='WwiseEventReference',
              native_asset='Wwise/ScriptableObjects/Event/D6F01B79-6E79-4B12-B37B-EBBB12221E47.asset',
              runtime='unity/Assets/Game/Runtime/Official/ProfessionUI/WwiseEventReference.cs',
              receipt='assets/reference/profession-ui/audio-source-manifest.json')},
          generator=Path(__file__), native_manifest=source.version,
          native_cache=ROOT / '.tools/source-packages/current' / source.version / 'conqueror-passives',
          native_export_name='native-export',
          limits=['The owning skill verifies enemy counts, cultivation, persistence and rendered Windows gameplay separately.'])


if __name__ == '__main__':
    main()
