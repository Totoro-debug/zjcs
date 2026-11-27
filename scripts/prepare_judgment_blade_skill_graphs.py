"""Restore Judgment Blade's original physical threshold strike."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs, verified_noise_mask_binding
from official_source import OfficialSource
from battle_native_graph_reuse import reuse_verified_native_entities


if __name__ == '__main__':
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/JudgmentBladeSkillGraphs/Resources/Combat/JudgmentBladeSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/judgment-blade-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/judgment-blade-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [11810]),
            'hits': ('hit_entity_id', 'MyFightHitFixed', [11811]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', [10004]),
        },
        family='JudgmentBladeSkillGraphs', cache_name='judgment-blade', generator=Path(__file__),
        restore_current_views=True,
        external_assets=verified_noise_mask_binding(OfficialSource().prepared_path / 'judgment-blade',
            reference_receipt='assets/reference/battle-animation/meteor-fire-current-skill-graphs-source-manifest.json'))
    reuse_verified_native_entities(
        ROOT / 'unity/Assets/Game/Resources/Combat/judgment-blade-current-skill-graphs.json',
        ROOT / 'assets/reference/battle-animation/judgment-blade-current-skill-graphs-source-manifest.json',
        ROOT / 'unity/Assets/Game/Resources/Combat/warlock-explosion-current-skill-graphs.json',
        ROOT / 'assets/reference/battle-animation/warlock-explosion-current-skill-graphs-source-manifest.json',
        [('damages', 'damage_entity_id', 10004)])
