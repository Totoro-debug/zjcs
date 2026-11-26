"""Restore Meteor Fire's current impact and burning-ground child skill graphs."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs


if __name__ == '__main__':
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/MeteorFireSkillGraphs/Resources/Combat/MeteorFireSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/meteor-fire-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/meteor-fire-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [11540, 11600]),
            'hits': ('hit_entity_id', None, [11541, 11601]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', [10007, 10021]),
        },
        family='MeteorFireSkillGraphs', cache_name='meteor-fire', generator=Path(__file__),
        restore_current_views=True)
