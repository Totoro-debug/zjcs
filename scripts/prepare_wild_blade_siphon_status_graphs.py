"""Restore the current critical-hit self-healing listener."""
from pathlib import Path
from current_battle_native_status_graphs import build_current_status_graphs


if __name__ == '__main__':
    build_current_status_graphs(family='WildBladeSiphonStatusGraphs',
        catalog_name='wild-blade-siphon-current-status-graphs', cache_name='wild-blade-siphon-full',
        entities=(25009,), source_wrappers=(('BattleStatusPlayables', 'MyFightStatus', False),
            ('BattleGridItemEntities', 'MyFightStatusHitSkill', False)), generator=Path(__file__))
