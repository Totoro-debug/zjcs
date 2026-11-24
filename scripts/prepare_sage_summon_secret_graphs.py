"""Convert current original summon-related Sage secret prefabs and healing."""
import argparse
from pathlib import Path

from current_battle_native_skill_graphs import build_current_skill_graphs, verified_noise_mask_binding
from official_apk import ROOT
from official_source import OfficialSource
from prepare_battle_status_orbit_assets import MAPPED, build


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('item', type=int, choices=(10591, 10600))
    item = parser.parse_args().item
    source = OfficialSource()
    cache_name = 'sage-summon-companions'
    cache = ROOT / '.tools/source-packages/current' / source.version / cache_name
    assets = ROOT / 'unity/Assets/Official/Combat'
    data = ROOT / 'unity/Assets/Game/Resources/Combat'
    receipts = ROOT / 'assets/reference/battle-animation'
    shader = verified_noise_mask_binding(cache)
    if item == 10591:
        family = 'SageSummonResonance'
        stem = 'sage-summon-resonance'
        status_ids = (25104, 24133)
        status_scripts = {
            -353109949: ('BattleGridItemEntities', 'MyFightStatusRoleSummonCount'),
            -550836577: ('BattleGridItemEntities', 'MyFightStatusProp'),
            -1931582627: ('BattleGridItemEntities', 'MyFightStatusHitSkill'),
        }
    else:
        family = 'SageSummonRestoration'
        stem = 'sage-summon-restoration'
        status_ids = (25105,)
        status_scripts = {219313581: ('BattleGridItemEntities', 'MyFightStatusRoleDie')}
        skill_graphs = family + 'SkillGraphs'
        build_current_skill_graphs(
            DEST=assets / skill_graphs / 'Resources/Combat' / skill_graphs,
            DATA=data / (stem + '-current-skill-graphs.json'),
            RECEIPT=receipts / (stem + '-current-skill-graphs-source-manifest.json'),
            GROUPS={
                'skills': ('skill_entity_id', 'MyFightSkill', [12050]),
                'hits': ('hit_entity_id', 'MyFightHitFixed', [12051]),
                'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
                'damages': ('damage_entity_id', 'MyFightDamage', [12052]),
            },
            family=skill_graphs, cache_name=cache_name, generator=Path(__file__), external_assets=shader,
        )
    status_graphs = family + 'StatusGraphs'
    build(
        DEST=assets / status_graphs / 'Resources/Combat' / status_graphs,
        DATA=data / (stem + '-current-status-graphs.json'),
        RECEIPT=receipts / (stem + '-current-status-graphs-source-manifest.json'),
        ROOTS={}, graph_roots={entity: f'Prefabs/EcEntity/Fight/FightSkill/{entity}/{entity}.prefab' for entity in status_ids},
        MAPPED={**MAPPED, **status_scripts, 1200213464: ('EntityLifecycle', 'MyEntity'),
                -1481699058: ('BattleStatusPlayables', 'MyFightStatus'), -109006141: ('EntityActions', 'MyAction')},
        family=status_graphs, shader_prefix=status_graphs + '_',
        generator=Path(__file__), native_manifest=source.version, native_cache=cache,
        native_export_name='native-export', external_assets=shader,
    )


if __name__ == '__main__':
    main()
