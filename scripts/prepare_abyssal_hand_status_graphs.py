"""Restore the original slow-one status and its native property view."""
from pathlib import Path
from current_battle_native_status_graphs import build_current_status_graphs
from current_battle_native_skill_graphs import verified_noise_mask_binding
from official_source import OfficialSource


if __name__ == '__main__':
    build_current_status_graphs(family='AbyssalHandStatusGraphs',
        catalog_name='abyssal-hand-current-status-graphs', cache_name='abyssal-hand',
        entities=[24150], source_wrappers=[('BattleStatusPlayables', 'MyFightStatus', False),
            ('BattleGridItemEntities', 'MyFightStatusProp', False)], generator=Path(__file__),
        external_assets=verified_noise_mask_binding(OfficialSource().prepared_path / 'abyssal-hand',
            reference_receipt='assets/reference/battle-animation/meteor-fire-current-skill-graphs-source-manifest.json'))
