"""Restore the original Doom Link application listener and additional erosion view."""
from pathlib import Path
from current_battle_native_status_graphs import build_current_status_graphs
from current_battle_native_skill_graphs import verified_noise_mask_binding
from official_source import OfficialSource


if __name__ == '__main__':
    build_current_status_graphs(family='DoomLinkStatusGraphs',
        catalog_name='doom-link-current-status-graphs', cache_name='doom-link',
        entities=[25073, 24472], source_wrappers=[('BattleStatusPlayables', 'MyFightStatus', False),
            ('BattleGridItemEntities', 'MyStatusApplyTargetHandle', True),
            ('BattleGridItemEntities', 'MyFightStatusRoundStart', False)], generator=Path(__file__),
        external_assets=verified_noise_mask_binding(OfficialSource().prepared_path / 'doom-link',
            reference_receipt='assets/reference/battle-animation/meteor-fire-current-skill-graphs-source-manifest.json'))
