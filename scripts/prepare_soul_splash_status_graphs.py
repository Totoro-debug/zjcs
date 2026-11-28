"""Restore Soul Splash's original kill listener and single-hit damage modifier."""
from pathlib import Path
from current_battle_native_status_graphs import build_current_status_graphs
from current_battle_native_skill_graphs import verified_noise_mask_binding
from official_source import OfficialSource


if __name__ == '__main__':
    build_current_status_graphs(family='SoulSplashStatusGraphs',
        catalog_name='soul-splash-current-status-graphs', cache_name='soul-splash',
        entities=[25024, 24471], source_wrappers=[('BattleStatusPlayables', 'MyFightStatus', False),
            ('BattleGridItemEntities', 'MyFightStatusKillSkill', False),
            ('BattleGridItemEntities', 'MyFightStatusStart', False),
            ('BattleGridItemEntities', 'MyFightStatusHitDmgAddPer', False)], generator=Path(__file__),
        external_assets=verified_noise_mask_binding(OfficialSource().prepared_path / 'soul-splash',
            reference_receipt='assets/reference/battle-animation/meteor-fire-current-skill-graphs-source-manifest.json'))
