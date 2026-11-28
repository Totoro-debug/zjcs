"""Restore the original extra-healing skill, cross-shaped hit and private cure view."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs, verified_noise_mask_binding
from official_source import OfficialSource


if __name__ == '__main__':
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/ExtraHealingSkillGraphs/Resources/Combat/ExtraHealingSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/extra-healing-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/extra-healing-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [11430]),
            'hits': ('hit_entity_id', 'MyFightHitFixed', [11431]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', [11432]),
        },
        family='ExtraHealingSkillGraphs', cache_name='extra-healing', generator=Path(__file__),
        restore_current_views=True,
        external_assets=verified_noise_mask_binding(OfficialSource().prepared_path / 'extra-healing',
            reference_receipt='assets/reference/battle-animation/meteor-fire-current-skill-graphs-source-manifest.json'))
