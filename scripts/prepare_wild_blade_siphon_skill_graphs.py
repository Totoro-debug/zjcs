"""Restore Restore Wild Blade Siphon's original self-healing skill graph."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs, verified_noise_mask_binding
from official_source import OfficialSource


if __name__ == '__main__':
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/WildBladeSiphonSkillGraphs/Resources/Combat/WildBladeSiphonSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/wild-blade-siphon-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/wild-blade-siphon-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [10960]),
            'hits': ('hit_entity_id', 'MyFightHitFixed', [10961]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', [10028]),
        },
        family='WildBladeSiphonSkillGraphs', cache_name='wild-blade-siphon-full', generator=Path(__file__),
        restore_current_views=True,
        external_assets=verified_noise_mask_binding(OfficialSource().prepared_path / 'wild-blade-siphon-full',
            reference_receipt='assets/reference/battle-animation/meteor-fire-current-skill-graphs-source-manifest.json'))
