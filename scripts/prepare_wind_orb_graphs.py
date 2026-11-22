"""Convert current original wind-orb statuses, shared stack display and release playables."""
from pathlib import Path

from official_apk import ROOT
from official_source import OfficialSource
from prepare_battle_status_orbit_assets import MAPPED, build
from prepare_battle_scene_appearance_runtime import meta


def main():
    source = OfficialSource()
    cache = ROOT / '.tools/source-packages/current' / source.version / 'conqueror-passive-children'
    mapped = dict(MAPPED)
    mapped.update({1200213464: ('EntityLifecycle', 'MyEntity'),
                   -109006141: ('EntityActions', 'MyAction'),
                   -1481699058: ('BattleStatusPlayables', 'MyFightStatus'),
                   -1931582627: ('BattleGridItemEntities', 'MyFightStatusHitSkill'),
                   1131240570: ('BattleGridItemEntities', 'MyFightStatusRoundBeforeEnd'),
                   1096565154: ('BattleGridItemEntities', 'MyFightStatusShareStack'),
                   1562105360: ('BattleGridItemEntities', 'MyFightGameplayTag'),
                   -533254470: ('BattleGridItemEntities', 'MyFightStatusStackChange'),
                   -265745592: ('BattleGridItemEntities', 'MyFightStatusHitDmgAddPer'),
                   1640720687: ('StorySounds', 'MyPlayableSound')})
    for name in ('MyFightStatusRoundBeforeEnd', 'MyFightStatusShareStack'):
        meta(ROOT / 'unity/Assets/Game/Runtime/Official/BattleGridItemEntities' / (name + '.cs'))
    exported = cache / 'native-export/ExportedProject/Assets'
    event = next(iter(sorted((exported / 'Wwise/ScriptableObjects/Event').glob('*.asset'))))
    build(DEST=ROOT / 'unity/Assets/Official/Combat/WindOrbGraphs/Resources/Combat/WindOrbGraphs',
          DATA=ROOT / 'unity/Assets/Game/Resources/Combat/wind-orb-current-graphs.json',
          RECEIPT=ROOT / 'assets/reference/battle-animation/wind-orb-current-graphs-source-manifest.json',
          ROOTS={}, graph_roots={key: f'Prefabs/EcEntity/Fight/FightSkill/{key}/{key}.prefab'
                                 for key in (25096, 24385, 24069, 24065)},
          MAPPED=mapped, family='WindOrbGraphs', shader_prefix='WindOrbGraph_',
          reference_scripts={-210918009: dict(name='WwiseEventReference',
              native_asset=event.relative_to(exported).as_posix(),
              runtime='unity/Assets/Game/Runtime/Official/ProfessionUI/WwiseEventReference.cs',
              receipt='assets/reference/profession-ui/audio-source-manifest.json')},
          generator=Path(__file__), native_manifest=source.version, native_cache=cache,
          native_export_name='native-export',
          limits=['The owning skill verifies chance, successful before-end casts, cultivation, persistence and rendered Windows gameplay separately.'])


if __name__ == '__main__':
    main()
