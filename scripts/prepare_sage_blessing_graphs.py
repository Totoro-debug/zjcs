"""Convert the original True Word Blessing listener and damage gain."""
from pathlib import Path

from current_battle_native_skill_graphs import verified_noise_mask_binding
from official_apk import ROOT
from official_source import OfficialSource
from prepare_battle_status_orbit_assets import MAPPED, build


def main():
    source = OfficialSource()
    cache = ROOT / '.tools/source-packages/current' / source.version / 'sage-blessing'
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
        DEST=ROOT / 'unity/Assets/Official/Combat/SageBlessingGraphs/Resources/Combat/SageBlessingGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/sage-blessing-current-status-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/sage-blessing-current-status-graphs-source-manifest.json',
        ROOTS={},
        graph_roots={key: f'Prefabs/EcEntity/Fight/FightSkill/{key}/{key}.prefab'
                     for key in (24395, 25119)},
        MAPPED=mapped, family='SageBlessingGraphs', shader_prefix='SageBlessingGraph_',
        reference_scripts={-210918009: dict(name='WwiseEventReference',
            native_asset='Wwise/ScriptableObjects/Event/8471403F-A6B2-4BA9-8A2E-3FC090FC32F7.asset',
            runtime='unity/Assets/Game/Runtime/Official/ProfessionUI/WwiseEventReference.cs',
            receipt='assets/reference/profession-ui/audio-source-manifest.json')},
        generator=Path(__file__), native_manifest=source.version, native_cache=cache,
        native_export_name='native-export', external_assets=verified_noise_mask_binding(cache),
        limits=['The owning secret separately verifies allied role selection, cultivated damage gain, personal-round lifetime and rendered Windows gameplay.'],
    )


if __name__ == '__main__':
    main()
