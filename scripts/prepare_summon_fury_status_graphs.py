"""Restore the original summon-creation listener and permanent attack status views."""
from pathlib import Path

from current_battle_native_status_graphs import build_current_status_graphs


if __name__ == '__main__':
    build_current_status_graphs(family='SummonFuryStatusGraphs',
        catalog_name='summon-fury-current-status-graphs', cache_name='summon-fury',
        entities=[25012, 24070], source_wrappers=[('BattleStatusPlayables', 'MyFightStatus', False),
            ('BattleGridItemEntities', 'MyFightStatusProp', True),
            ('BattleGridItemEntities', 'MyFightStatusHitSummon', True)],
        generator=Path(__file__))
