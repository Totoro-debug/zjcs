"""Restore the current native burning-ground status owned by Meteor Fire."""
from pathlib import Path
from current_battle_native_status_graphs import build_current_status_graphs


if __name__ == '__main__':
    build_current_status_graphs(
        family='MeteorFireStatusGraphs', catalog_name='meteor-fire-current-status-graphs',
        cache_name='meteor-fire', entities=[11542],
        entity_paths={11542: 'Prefabs/EcEntity/Fight/FightSkill/11540/11542.prefab'},
        source_wrappers=[('BattleGridItemEntities', 'MyFightStatusMoveNear', True),
                         ('BattleStatusPlayables', 'MyFightStatus', False),
                         ('BattleGridItemEntities', 'MyStatusEnd', True),
                         ('BattleGridItemEntities', 'MyStatusAutoRemove', True)],
        script_bindings={-638476292: ('BattleGridItemPlayables', 'AutoStopOnSourceDie')},
        generator=Path(__file__))
