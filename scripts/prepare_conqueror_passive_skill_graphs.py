"""Convert current conqueror passive child skill, hit and damage prefab graphs."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs


DEST = ROOT / 'unity/Assets/Official/Combat/ConquerorPassiveSkillGraphs/Resources/Combat/ConquerorPassiveSkillGraphs'
DATA = ROOT / 'unity/Assets/Game/Resources/Combat/conqueror-passive-current-skill-graphs.json'
RECEIPT = ROOT / 'assets/reference/battle-animation/conqueror-passive-current-skill-graphs-source-manifest.json'
GROUPS = {
    'skills': ('skill_entity_id', 'MyFightSkill', [11150, 12030, 13170]),
    'hits': ('hit_entity_id', 'MyFightHitFixed', [11151, 12031, 13171]),
    'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
    'damages': ('damage_entity_id', 'MyFightDamage', [10023, 12032, 13172]),
}



def main():
    build_current_skill_graphs(DEST=DEST, DATA=DATA, RECEIPT=RECEIPT, GROUPS=GROUPS,
        family='ConquerorPassiveSkillGraphs', cache_name='conqueror-passive-children', generator=Path(__file__))


if __name__ == '__main__':
    main()
