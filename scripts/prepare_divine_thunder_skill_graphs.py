"""Convert the current original Divine Thunder entities and all authored waves."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs


def main():
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/DivineThunderSkillGraphs/Resources/Combat/DivineThunderSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/divine-thunder-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/divine-thunder-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [10920]),
            'hits': ('hit_entity_id', None, [10921, 10922, 10923, 10924, 10925]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', [10926]),
        },
        family='DivineThunderSkillGraphs', cache_name='divine-thunder', generator=Path(__file__),
        restore_current_views=True)


if __name__ == '__main__':
    main()
