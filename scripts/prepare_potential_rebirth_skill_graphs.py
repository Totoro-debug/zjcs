"""Restore Potential Rebirth's current cure and dispersal graph."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs


if __name__ == '__main__':
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/PotentialRebirthSkillGraphs/Resources/Combat/PotentialRebirthSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/potential-rebirth-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/potential-rebirth-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [11770]),
            'hits': ('hit_entity_id', 'MyFightHitFixed', [11771]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', [11772]),
        },
        family='PotentialRebirthSkillGraphs', cache_name='potential-rebirth', generator=Path(__file__),
        restore_current_views=True)
