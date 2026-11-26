"""Convert the current official quick-casting opener hierarchy and bindings."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs


def main():
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/QuickCastingSkillGraphs/Resources/Combat/QuickCastingSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/quick-casting-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/quick-casting-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [12150]),
            'hits': ('hit_entity_id', 'MyFightHitFixed', [12151]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', [12152]),
        },
        family='QuickCastingSkillGraphs', cache_name='quick-casting', generator=Path(__file__),
        restore_current_views=True)


if __name__ == '__main__':
    main()
