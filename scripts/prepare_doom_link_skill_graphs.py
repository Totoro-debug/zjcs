"""Restore the original Doom Link periodic skill and its scoped dark damage view."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs, verified_noise_mask_binding
from official_source import OfficialSource
from battle_native_graph_scopes import scope_verified_native_entities


if __name__ == '__main__':
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/DoomLinkSkillGraphs/Resources/Combat/DoomLinkSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/doom-link-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/doom-link-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [12540]),
            'hits': ('hit_entity_id', 'MyFightHitFixed', [12541]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', [10027]),
        },
        family='DoomLinkSkillGraphs', cache_name='doom-link', generator=Path(__file__),
        restore_current_views=True,
        external_assets=verified_noise_mask_binding(OfficialSource().prepared_path / 'doom-link',
            reference_receipt='assets/reference/battle-animation/meteor-fire-current-skill-graphs-source-manifest.json'))
    scope_verified_native_entities(
        ROOT / 'unity/Assets/Game/Resources/Combat/doom-link-current-skill-graphs.json',
        ROOT / 'assets/reference/battle-animation/doom-link-current-skill-graphs-source-manifest.json',
        [('damages', 'damage_entity_id', 10027, [10585])])
