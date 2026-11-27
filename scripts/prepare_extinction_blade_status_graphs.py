"""Restore Extinction Blade's original armour reduction; launch/recovery reuse Wind Roar's verified shared graph."""
from pathlib import Path
from current_battle_native_status_graphs import build_current_status_graphs
from current_battle_native_skill_graphs import verified_noise_mask_binding
from official_source import OfficialSource

if __name__ == '__main__':
    build_current_status_graphs(family='ExtinctionBladeStatusGraphs',
        catalog_name='extinction-blade-current-status-graphs',cache_name='extinction-blade',
        entities=[24113],source_wrappers=[('BattleStatusPlayables','MyFightStatus',False),
            ('BattleGridItemEntities','MyFightStatusProp',True)],generator=Path(__file__),
        external_assets=verified_noise_mask_binding(OfficialSource().prepared_path / 'extinction-blade',
            reference_receipt='assets/reference/battle-animation/meteor-fire-current-skill-graphs-source-manifest.json'))
