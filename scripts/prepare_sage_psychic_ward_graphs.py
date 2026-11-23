"""Convert the current original Psychic Ward cast, impact and persistent effect."""
from pathlib import Path

from current_battle_native_skill_graphs import build_current_skill_graphs, verified_noise_mask_binding
from official_apk import ROOT
from official_source import OfficialSource
from prepare_battle_status_orbit_assets import MAPPED, build


def main():
    source = OfficialSource()
    cache_name = 'sage-psychic-ward'
    cache = ROOT / '.tools/source-packages/current' / source.version / cache_name
    assets = ROOT / 'unity/Assets/Official/Combat'
    data = ROOT / 'unity/Assets/Game/Resources/Combat'
    receipts = ROOT / 'assets/reference/battle-animation'
    shader = verified_noise_mask_binding(cache)
    build_current_skill_graphs(
        DEST=assets / 'SagePsychicWardGraphs/Resources/Combat/SagePsychicWardGraphs',
        DATA=data / 'sage-psychic-ward-current-skill-graphs.json',
        RECEIPT=receipts / 'sage-psychic-ward-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [11760]),
            'hits': ('hit_entity_id', 'MyFightHitFixed', [11761]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', [11762]),
        },
        family='SagePsychicWardGraphs', cache_name=cache_name, generator=Path(__file__),
        external_assets=shader,
    )
    mapped = {
        **MAPPED,
        1200213464: ('EntityLifecycle', 'MyEntity'),
        -1481699058: ('BattleStatusPlayables', 'MyFightStatus'),
        -109006141: ('EntityActions', 'MyAction'),
        1504869675: ('BattleGridItemEntities', 'MyFightStatusBattleFormulaDamageCustomProps'),
    }
    build(
        DEST=assets / 'SagePsychicWardStatusGraphs/Resources/Combat/SagePsychicWardStatusGraphs',
        DATA=data / 'sage-psychic-ward-current-status-graphs.json',
        RECEIPT=receipts / 'sage-psychic-ward-current-status-graphs-source-manifest.json',
        ROOTS={}, graph_roots={24181: 'Prefabs/EcEntity/Fight/FightSkill/24181/24181.prefab'},
        MAPPED=mapped, family='SagePsychicWardStatusGraphs', shader_prefix='SagePsychicWardStatusGraph_',
        generator=Path(__file__), native_manifest=source.version, native_cache=cache,
        native_export_name='native-export', external_assets=shader,
        limits=['The owning skill separately verifies mark targeting, consumption and rendered Windows gameplay.'],
    )


if __name__ == '__main__':
    main()
