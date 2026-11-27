"""Restore Soul Guardian's original self-targeted cost and protection cast."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs, verified_noise_mask_binding
from official_source import OfficialSource


if __name__ == '__main__':
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/SoulGuardianSkillGraphs/Resources/Combat/SoulGuardianSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/soul-guardian-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/soul-guardian-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [11870]),
            'hits': ('hit_entity_id', 'MyFightHitFixed', [11871]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', [11872]),
        },
        family='SoulGuardianSkillGraphs', cache_name='soul-guardian', generator=Path(__file__),
        restore_current_views=True,
        external_assets=verified_noise_mask_binding(OfficialSource().prepared_path / 'soul-guardian',
            reference_receipt='assets/reference/battle-animation/meteor-fire-current-skill-graphs-source-manifest.json'))
