"""Restore Ice Guardian's original round-check listener and protection status."""
from pathlib import Path
from current_battle_native_status_graphs import build_current_status_graphs


if __name__ == '__main__':
    build_current_status_graphs(family='IceGuardianStatusGraphs',
        catalog_name='ice-guardian-current-status-graphs', cache_name='ice-guardian',
        entities=(25114, 24390), source_wrappers=(('BattleStatusPlayables', 'MyFightStatus', False),
            ('BattleGridItemEntities', 'MyFightStatusRoundCheckSkill', True),
            ('BattleGridItemEntities', 'MyFightStatusProp', True),
            ('BattleGridItemEntities', 'MyStatusEnd', False)), generator=Path(__file__))
