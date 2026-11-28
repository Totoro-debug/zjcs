"""Restore the original extra-healing listener view without adding an invented effect."""
from pathlib import Path
from current_battle_native_status_graphs import build_current_status_graphs


if __name__ == '__main__':
    build_current_status_graphs(family='ExtraHealingStatusGraphs',
        catalog_name='extra-healing-current-status-graphs', cache_name='extra-healing',
        entities=[25039], source_wrappers=[('BattleStatusPlayables', 'MyFightStatus', False),
            ('BattleGridItemEntities', 'MyFightStatusHitSkill', True)], generator=Path(__file__))
