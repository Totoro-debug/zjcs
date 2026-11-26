"""Restore the original Divine Thunder per-target damage falloff status."""
from pathlib import Path
from current_battle_native_status_graphs import build_current_status_graphs


if __name__ == '__main__':
    build_current_status_graphs(family='DivineThunderStatusGraphs',
        catalog_name='divine-thunder-current-status-graphs', cache_name='divine-thunder',
        entities=(24100,), source_wrappers=(('BattleStatusPlayables', 'MyFightStatus', False),
            ('BattleGridItemEntities', 'MyFightStatusDamageFalloff', True)), generator=Path(__file__))
