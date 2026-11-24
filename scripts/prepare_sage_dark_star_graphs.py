"""Convert the original Dark Star Arrival listener and its three random gains."""
from pathlib import Path

from current_battle_native_skill_graphs import verified_noise_mask_binding
from official_apk import ROOT
from official_source import OfficialSource
from prepare_battle_status_orbit_assets import MAPPED, build


def main():
    source = OfficialSource()
    cache = ROOT / '.tools/source-packages/current' / source.version / 'sage-dark-star'
    mapped = {
        **MAPPED,
        1200213464: ('EntityLifecycle', 'MyEntity'),
        -109006141: ('EntityActions', 'MyAction'),
        -1481699058: ('BattleStatusPlayables', 'MyFightStatus'),
        -550836577: ('BattleGridItemEntities', 'MyFightStatusProp'),
        -2102852653: ('BattleGridItemEntities', 'MyFightStatusRoundStart'),
        1640720687: ('StorySounds', 'MyPlayableSound'),
    }
    build(
        DEST=ROOT / 'unity/Assets/Official/Combat/SageDarkStarGraphs/Resources/Combat/SageDarkStarGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/sage-dark-star-current-status-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/sage-dark-star-current-status-graphs-source-manifest.json',
        ROOTS={},
        graph_roots={key: f'Prefabs/EcEntity/Fight/FightSkill/{key}/{key}.prefab'
                     for key in (24396, 24398, 24399, 25120)},
        MAPPED=mapped, family='SageDarkStarGraphs', shader_prefix='SageDarkStarGraph_',
        reference_scripts={-210918009: dict(name='WwiseEventReference',
            native_asset='Wwise/ScriptableObjects/Event/75A8AF4D-DA05-48D9-B2EA-3C8C82A6EF1C.asset',
            runtime='unity/Assets/Game/Runtime/Official/ProfessionUI/WwiseEventReference.cs',
            receipt='assets/reference/profession-ui/audio-source-manifest.json')},
        generator=Path(__file__), native_manifest=source.version, native_cache=cache,
        native_export_name='native-export', external_assets=verified_noise_mask_binding(cache),
        limits=['The owning secret separately verifies random gains, cultivation, personal-round lifetimes and rendered Windows gameplay.'],
    )


if __name__ == '__main__':
    main()
