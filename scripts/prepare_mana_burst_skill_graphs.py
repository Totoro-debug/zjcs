"""Restore Mana Burst's native area impact and periodic erosion child."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs, verified_noise_mask_binding
from official_source import OfficialSource
from battle_native_graph_scopes import scope_verified_native_entities


if __name__ == '__main__':
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/ManaBurstSkillGraphs/Resources/Combat/ManaBurstSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/mana-burst-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/mana-burst-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [11390, 12560]),
            'hits': ('hit_entity_id', 'MyFightHitFixed', [11391, 12561]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', [11392, 10027]),
        },
        family='ManaBurstSkillGraphs', cache_name='mana-burst', generator=Path(__file__),
        restore_current_views=True,
        external_assets=verified_noise_mask_binding(OfficialSource().prepared_path / 'mana-burst',
            reference_receipt='assets/reference/battle-animation/meteor-fire-current-skill-graphs-source-manifest.json'))
    scope_verified_native_entities(
        ROOT / 'unity/Assets/Game/Resources/Combat/mana-burst-current-skill-graphs.json',
        ROOT / 'assets/reference/battle-animation/mana-burst-current-skill-graphs-source-manifest.json',
        [('damages', 'damage_entity_id', 10027, [10064])])
