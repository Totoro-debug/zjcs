"""Restore the current native Asura Hand next-skill status."""
from pathlib import Path
from official_source import OfficialSource
from current_battle_native_skill_graphs import verified_noise_mask_binding
from current_battle_native_status_graphs import build_current_status_graphs

if __name__ == '__main__':
    build_current_status_graphs(
        family='AsuraHandStatusGraphs', catalog_name='asura-hand-current-status-graphs',
        cache_name='asura-hand', entities=[24227],
        source_wrappers=[('BattleStatusPlayables', 'MyFightStatus', False),
                         ('BattleGridItemEntities', 'MyFightStatusHitDmgAddPer', True)],
        generator=Path(__file__),
        external_assets=verified_noise_mask_binding(OfficialSource().prepared_path / 'asura-hand',
            reference_receipt='assets/reference/battle-animation/meteor-fire-current-skill-graphs-source-manifest.json'))
