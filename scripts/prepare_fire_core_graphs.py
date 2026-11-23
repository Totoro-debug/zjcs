"""Convert the current original fire core burst skill and hit graphs."""
from pathlib import Path

from current_battle_native_skill_graphs import build_current_skill_graphs
from official_apk import ROOT


def main():
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/FireCoreSkillGraphs/Resources/Combat/FireCoreSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/fire-core-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/fire-core-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [10540]),
            'hits': ('hit_entity_id', 'MyFightHitFixed', [10541]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', []),
        },
        family='FireCoreSkillGraphs', cache_name='destroyer-fire-core', generator=Path(__file__),
    )


if __name__ == '__main__':
    main()
