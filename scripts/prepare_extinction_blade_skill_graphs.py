"""Restore Restore Extinction Blade's original jump, dark impact and native movement graph."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs, verified_noise_mask_binding
from official_source import OfficialSource


if __name__ == '__main__':
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/ExtinctionBladeSkillGraphs/Resources/Combat/ExtinctionBladeSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/extinction-blade-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/extinction-blade-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [11530]),
            'hits': ('hit_entity_id', 'MyFightHitFixed', [11531]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', [11532]),
            'damages': ('damage_entity_id', 'MyFightDamage', [11534]),
        },
        family='ExtinctionBladeSkillGraphs', cache_name='extinction-blade', generator=Path(__file__),
        restore_current_views=True,
        external_assets=verified_noise_mask_binding(OfficialSource().prepared_path / 'extinction-blade',
            reference_receipt='assets/reference/battle-animation/meteor-fire-current-skill-graphs-source-manifest.json'))
