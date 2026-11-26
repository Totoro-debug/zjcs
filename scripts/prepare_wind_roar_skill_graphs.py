"""Restore Wind Roar's complete current native cast and five original wind hits."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs


if __name__ == '__main__':
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/WindRoarSkillGraphs/Resources/Combat/WindRoarSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/wind-roar-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/wind-roar-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [11130]),
            'hits': ('hit_entity_id', 'MyFightHitFixed', [11131]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', [11132]),
        },
        family='WindRoarSkillGraphs', cache_name='wind-roar', generator=Path(__file__),
        restore_current_views=True)
