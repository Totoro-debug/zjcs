"""Restore Ice Guardian's original protection and thaw-healing child skills."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs


if __name__ == '__main__':
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/IceGuardianSkillGraphs/Resources/Combat/IceGuardianSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/ice-guardian-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/ice-guardian-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [12160, 12190]),
            'hits': ('hit_entity_id', 'MyFightHitFixed', [12161, 12191]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', [12162, 12192]),
        },
        family='IceGuardianSkillGraphs', cache_name='ice-guardian', generator=Path(__file__),
        restore_current_views=True)
