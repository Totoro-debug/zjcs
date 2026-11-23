"""Convert the current original Dark Blast cast and hit graphs."""
from pathlib import Path

from current_battle_native_skill_graphs import build_current_skill_graphs, verified_noise_mask_binding
from official_apk import ROOT
from official_source import OfficialSource


def main():
    cache_name = 'sage-dark-blast'
    cache = ROOT / '.tools/source-packages/current' / OfficialSource().version / cache_name
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/SageDarkBlastGraphs/Resources/Combat/SageDarkBlastGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/sage-dark-blast-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/sage-dark-blast-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [12120]),
            'hits': ('hit_entity_id', 'MyFightHitFixed', [12121]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', []),
        },
        family='SageDarkBlastGraphs', cache_name=cache_name, generator=Path(__file__),
        external_assets=verified_noise_mask_binding(cache),
    )


if __name__ == '__main__':
    main()
