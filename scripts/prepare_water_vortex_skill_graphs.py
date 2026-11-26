"""Restore Water Vortex's original water impact and inward displacement graph."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs
import json
from prepare_battle_scene_appearance_runtime import identity, write_changed


if __name__ == '__main__':
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/WaterVortexSkillGraphs/Resources/Combat/WaterVortexSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/water-vortex-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/water-vortex-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [11340]),
            'hits': ('hit_entity_id', 'MyFightHitFixed', [11341]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', [11343]),
            'damages': ('damage_entity_id', 'MyFightDamage', [11342]),
        },
        family='WaterVortexSkillGraphs', cache_name='water-vortex', generator=Path(__file__),
        restore_current_views=True)
    receipt = ROOT / 'assets/reference/battle-animation/water-vortex-current-skill-graphs-source-manifest.json'
    proof = json.loads(receipt.read_bytes())
    proof['current_move_view_runtime'] = identity(
        ROOT / 'assets/reference/battle-animation/current-battle-move-views-source-manifest.json')
    write_changed(receipt, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
