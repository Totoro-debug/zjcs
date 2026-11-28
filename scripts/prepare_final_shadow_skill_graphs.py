"""Install the fixed-source Final Shadow skill, hit and private damage views."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs


def main():
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/FinalShadowSkillGraphs/Resources/Combat/FinalShadowSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/final-shadow-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/final-shadow-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [11750]),
            'hits': ('hit_entity_id', None, [11751]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', [11752]),
        },
        family='FinalShadowSkillGraphs', cache_name='final-shadow',
        generator=Path(__file__), restore_current_views=True)


if __name__ == '__main__':
    main()
