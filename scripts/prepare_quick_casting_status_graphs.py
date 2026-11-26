"""Restore Quick Casting's complete current status entities and managed views."""
from pathlib import Path
from current_battle_native_status_graphs import build_current_status_graphs


if __name__ == '__main__':
    build_current_status_graphs(family='QuickCastingStatusGraphs',
        catalog_name='quick-casting-current-status-graphs', cache_name='quick-casting-statuses',
        entities=(25113, 24389), source_wrappers=(('BattleStatusPlayables', 'MyFightStatus', False),
            ('BattleGridItemEntities', 'MyFightStatusRoundStart', False),
            ('BattleGridItemEntities', 'MyFightStatusProp', True),
            ('BattleGridItemEntities', 'MyFightStatusGlobalRoundUpdate', True)), generator=Path(__file__))
