"""Convert the original shared movement skill and its movement action graph."""
from pathlib import Path

from current_battle_native_skill_graphs import build_current_skill_graphs
from official_apk import ROOT


def main():
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/CommonMovementSkillGraphs/Resources/Combat/CommonMovementSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/common-movement-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/common-movement-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [19810]),
            'hits': ('hit_entity_id', None, []),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', [19811]),
            'damages': ('damage_entity_id', 'MyFightDamage', []),
        },
        family='CommonMovementSkillGraphs', cache_name='summon-entry',
        generator=Path(__file__), restore_current_views=True)


if __name__ == '__main__':
    main()
