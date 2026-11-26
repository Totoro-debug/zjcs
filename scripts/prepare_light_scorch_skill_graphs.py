"""Restore the current original Light Scorch child skill and its damage view."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs


if __name__ == '__main__':
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/LightScorchSkillGraphs/Resources/Combat/LightScorchSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/light-scorch-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/light-scorch-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [11680]),
            'hits': ('hit_entity_id', 'MyFightHitFixed', [11681]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', [11682]),
        },
        family='LightScorchSkillGraphs', cache_name='light-scorch', generator=Path(__file__),
        restore_current_views=True)
