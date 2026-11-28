"""Restore Soul Splash's original death-position custom-damage graph."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs, verified_noise_mask_binding
from official_source import OfficialSource
from battle_native_graph_scopes import scope_verified_native_entities


if __name__ == '__main__':
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/SoulSplashSkillGraphs/Resources/Combat/SoulSplashSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/soul-splash-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/soul-splash-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [11280]),
            'hits': ('hit_entity_id', 'MyFightHitFixed', [11281]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', [10023]),
        },
        family='SoulSplashSkillGraphs', cache_name='soul-splash', generator=Path(__file__),
        restore_current_views=True,
        external_assets=verified_noise_mask_binding(OfficialSource().prepared_path / 'soul-splash',
            reference_receipt='assets/reference/battle-animation/meteor-fire-current-skill-graphs-source-manifest.json'))
    scope_verified_native_entities(
        ROOT / 'unity/Assets/Game/Resources/Combat/soul-splash-current-skill-graphs.json',
        ROOT / 'assets/reference/battle-animation/soul-splash-current-skill-graphs-source-manifest.json',
        [('damages', 'damage_entity_id', 10023, [10545])])
