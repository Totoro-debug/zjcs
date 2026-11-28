"""Convert original Water Spirit summon, cure and dispel performance graphs."""
from pathlib import Path
from current_battle_native_skill_graphs import build_current_skill_graphs
from official_apk import ROOT


def main():
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/WaterSummonSkillGraphs/Resources/Combat/WaterSummonSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/water-summon-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/water-summon-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [10840, 16250, 16260]),
            'hits': ('hit_entity_id', None, [10841, 16251, 16261]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', [16252, 16262]),
        },
        family='WaterSummonSkillGraphs', cache_name='water-summon',
        generator=Path(__file__), restore_current_views=True)


if __name__ == '__main__':
    main()
