"""Restore the current original hit listener and next-skill dazzle status."""
from pathlib import Path
from current_battle_native_status_graphs import build_current_status_graphs


if __name__ == '__main__':
    build_current_status_graphs(family='LightScorchStatusGraphs',
        catalog_name='light-scorch-current-status-graphs', cache_name='light-scorch',
        entities=(25046, 24234), source_wrappers=(('BattleStatusPlayables', 'MyFightStatus', False),
            ('BattleGridItemEntities', 'MyFightStatusHitSkill', False),
            ('BattleGridItemEntities', 'MyFightStatusHitDmgAddPer', False)), generator=Path(__file__))
