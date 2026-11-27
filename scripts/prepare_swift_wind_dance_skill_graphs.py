"""Restore Swift Wind Dance's original friendly area enhancement graph."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs, verified_noise_mask_binding
from official_source import OfficialSource


if __name__ == '__main__':
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/SwiftWindDanceSkillGraphs/Resources/Combat/SwiftWindDanceSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/swift-wind-dance-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/swift-wind-dance-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [11930]),
            'hits': ('hit_entity_id', 'MyFightHitFixed', [11931]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', [11932]),
        },
        family='SwiftWindDanceSkillGraphs', cache_name='swift-wind-dance-full', generator=Path(__file__),
        restore_current_views=True,
        external_assets=verified_noise_mask_binding(OfficialSource().prepared_path / 'swift-wind-dance-full',
            reference_receipt='assets/reference/battle-animation/meteor-fire-current-skill-graphs-source-manifest.json'))
