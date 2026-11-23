"""Convert the current original Heavenly Rain cast, impact and healing graphs."""
from pathlib import Path

from current_battle_native_skill_graphs import build_current_skill_graphs, verified_noise_mask_binding
from official_apk import ROOT
from official_source import OfficialSource


def main():
    cache_name = 'sage-heavenly-rain'
    cache = ROOT / '.tools/source-packages/current' / OfficialSource().version / cache_name
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/HeavenlyRainGraphs/Resources/Combat/HeavenlyRainGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/heavenly-rain-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/heavenly-rain-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [12020]),
            'hits': ('hit_entity_id', 'MyFightHitFixed', [12021]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', [12022]),
        },
        family='HeavenlyRainGraphs', cache_name=cache_name, generator=Path(__file__),
        external_assets=verified_noise_mask_binding(cache),
    )


if __name__ == '__main__':
    main()
