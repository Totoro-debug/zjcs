"""Restore the current official wet status and its original action-rate wrapper."""
from pathlib import Path
from current_battle_native_status_graphs import build_current_status_graphs


if __name__ == '__main__':
    build_current_status_graphs(family='WaterVortexStatusGraphs',
        catalog_name='water-vortex-current-status-graphs', cache_name='water-vortex',
        entities=(24012,), source_wrappers=(('BattleStatusPlayables', 'MyFightStatus', False),
            ('BattleGridItemEntities', 'MyFightStatusAgentRate', True)), generator=Path(__file__))
