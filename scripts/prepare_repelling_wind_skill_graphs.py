"""Restore Repelling Wind's current hit and outward displacement graph."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs


if __name__ == '__main__':
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/RepellingWindSkillGraphs/Resources/Combat/RepellingWindSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/repelling-wind-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/repelling-wind-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [11370]),
            'hits': ('hit_entity_id', 'MyFightHitFixed', [11371]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', [11373]),
            'damages': ('damage_entity_id', 'MyFightDamage', [11372]),
        },
        family='RepellingWindSkillGraphs', cache_name='repelling-wind', generator=Path(__file__),
        restore_current_views=True)
