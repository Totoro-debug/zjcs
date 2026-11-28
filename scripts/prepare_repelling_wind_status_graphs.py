"""Restore the current native Repelling Wind range listener."""
from pathlib import Path
from current_battle_native_status_graphs import build_current_status_graphs


if __name__ == '__main__':
    build_current_status_graphs(
        family='RepellingWindStatusGraphs', catalog_name='repelling-wind-current-status-graphs',
        cache_name='repelling-wind', entities=[25032],
        source_wrappers=[('BattleStatusPlayables', 'MyFightStatus', False),
                         ('BattleGridItemEntities', 'MyFightStatusMoveNear', True)],
        generator=Path(__file__))
