"""Restore Soul Guardian's original permanent listener and protection status."""
from pathlib import Path
from current_battle_native_status_graphs import build_current_status_graphs
from current_battle_native_skill_graphs import verified_noise_mask_binding
from official_source import OfficialSource


if __name__ == '__main__':
    build_current_status_graphs(family='SoulGuardianStatusGraphs',
        catalog_name='soul-guardian-current-status-graphs', cache_name='soul-guardian',
        entities=[25093, 24372], source_wrappers=[('BattleStatusPlayables', 'MyFightStatus', False),
            ('BattleGridItemEntities', 'MyFightStatusProp', True),
            ('BattleGridItemEntities', 'MyFightStatusRoundStart', False)], generator=Path(__file__),
        external_assets=verified_noise_mask_binding(OfficialSource().prepared_path / 'soul-guardian',
            reference_receipt='assets/reference/battle-animation/meteor-fire-current-skill-graphs-source-manifest.json'))
