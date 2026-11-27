"""Restore Judgment Blade's hit listener and ten-layer mark."""
from pathlib import Path
from current_battle_native_status_graphs import build_current_status_graphs
from current_battle_native_skill_graphs import verified_noise_mask_binding
from official_source import OfficialSource


if __name__ == '__main__':
    build_current_status_graphs(family='JudgmentBladeStatusGraphs',
        catalog_name='judgment-blade-current-status-graphs', cache_name='judgment-blade',
        entities=(25080,24316), source_wrappers=(('BattleStatusPlayables', 'MyFightStatus', False),
            ('BattleGridItemEntities', 'MyFightStatusHitSkill', False),
            ('BattleGridItemEntities', 'MyStatusStackCount', True)), generator=Path(__file__),
        external_assets=verified_noise_mask_binding(OfficialSource().prepared_path / 'judgment-blade',
            reference_receipt='assets/reference/battle-animation/meteor-fire-current-skill-graphs-source-manifest.json'))
