"""Convert current conqueror fire skill, hit, movement and damage prefab graphs."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs


DEST = ROOT / 'unity/Assets/Official/Combat/ConquerorSkillGraphs/Resources/Combat/ConquerorSkillGraphs'
DATA = ROOT / 'unity/Assets/Game/Resources/Combat/conqueror-current-skill-graphs.json'
RECEIPT = ROOT / 'assets/reference/battle-animation/conqueror-current-skill-graphs-source-manifest.json'
GROUPS = {
    'skills': ('skill_entity_id', 'MyFightSkill', [12090, 12093, 12094, 12100]),
    'hits': ('hit_entity_id', 'MyFightHitFixed', [12091, 12102]),
    'moves': ('action_entity_id', 'MyFightActionSkillMove', [12103]),
    'damages': ('damage_entity_id', 'MyFightDamage', [12092, 12104]),
}



def main():
    build_current_skill_graphs(DEST=DEST, DATA=DATA, RECEIPT=RECEIPT, GROUPS=GROUPS,
        family='ConquerorSkillGraphs', cache_name='conqueror-actives', generator=Path(__file__))


if __name__ == '__main__':
    main()
