"""Install the fixed-source Frenzy Totem summon and original child skill views."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs


def main():
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/FrenzyTotemSkillGraphs/Resources/Combat/FrenzyTotemSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/frenzy-totem-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/frenzy-totem-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [12060, 56790]),
            'hits': ('hit_entity_id', None, [12063, 12061, 56791, 56792]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', [56793]),
        },
        family='FrenzyTotemSkillGraphs', cache_name='frenzy-totem',
        generator=Path(__file__), restore_current_views=True)


if __name__ == '__main__':
    main()
