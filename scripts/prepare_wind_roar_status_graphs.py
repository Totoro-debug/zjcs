"""Restore the original launch and recovery phases used by Wind Roar."""
from pathlib import Path
import json
from current_battle_native_status_graphs import build_current_status_graphs
from official_source import ROOT
from prepare_battle_scene_appearance_runtime import identity, write_changed


if __name__ == '__main__':
    build_current_status_graphs(family='WindRoarStatusGraphs',
        catalog_name='wind-roar-current-status-graphs', cache_name='wind-roar',
        entities=(24269, 24270), source_wrappers=(('BattleStatusPlayables', 'MyFightStatus', False),
            ('BattleGridItemEntities', 'MyFightStatusDamageSkill', True),
            ('BattleGridItemEntities', 'MyFightStatusDamageRemove', True),
            ('BattleGridItemEntities', 'MyStatusAutoRemove', True)), generator=Path(__file__))
    receipt = ROOT / 'assets/reference/battle-animation/wind-roar-current-status-graphs-source-manifest.json'
    proof = json.loads(receipt.read_bytes())
    proof['current_event_view_runtime'] = identity(
        ROOT / 'assets/reference/battle-animation/current-battle-event-views-source-manifest.json')
    write_changed(receipt, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
