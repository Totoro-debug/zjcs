"""Convert the current official explosion skill and generic damage hierarchy."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs


def main():
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/WarlockExplosionSkillGraphs/Resources/Combat/WarlockExplosionSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/warlock-explosion-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/warlock-explosion-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [12070]),
            'hits': ('hit_entity_id', 'MyFightHitFixed', [12071]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', [10004]),
        },
        family='WarlockExplosionSkillGraphs', cache_name='warlock-explosion', generator=Path(__file__),
        restore_current_views=True)


if __name__ == '__main__':
    main()
