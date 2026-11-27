"""Restore Swift Wind Dance's original movement and speed buffs."""
from pathlib import Path
from current_battle_native_status_graphs import build_current_status_graphs
from current_battle_native_skill_graphs import verified_noise_mask_binding
from official_source import OfficialSource

if __name__ == '__main__':
    build_current_status_graphs(family='SwiftWindDanceStatusGraphs',
        catalog_name='swift-wind-dance-current-status-graphs',cache_name='swift-wind-dance-full',
        entities=[24373,24374],source_wrappers=[('BattleStatusPlayables','MyFightStatus',False),
            ('BattleGridItemEntities','MyFightStatusProp',True)],generator=Path(__file__),
        external_assets=verified_noise_mask_binding(OfficialSource().prepared_path / 'swift-wind-dance-full',
            reference_receipt='assets/reference/battle-animation/meteor-fire-current-skill-graphs-source-manifest.json'))
