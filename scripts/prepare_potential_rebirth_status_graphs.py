"""Restore the current native Potential Rebirth fatal listener."""
from pathlib import Path
from current_battle_native_status_graphs import build_current_status_graphs


if __name__ == '__main__':
    build_current_status_graphs(
        family='PotentialRebirthStatusGraphs', catalog_name='potential-rebirth-current-status-graphs',
        cache_name='potential-rebirth', entities=[25064],
        source_wrappers=[('BattleStatusPlayables', 'MyFightStatus', False),
                         ('BattleGridItemEntities', 'MyFightStatusHpLimit', True)],
        generator=Path(__file__))
