"""Restore Eclipse Slashes' current six physical hits and final strike graph."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs, verified_noise_mask_binding
from official_source import OfficialSource


if __name__ == '__main__':
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/EclipseSlashesSkillGraphs/Resources/Combat/EclipseSlashesSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/eclipse-slashes-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/eclipse-slashes-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [11440]),
            'hits': ('hit_entity_id', 'MyFightHitFixed', [11441, 11442]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
            'damages': ('damage_entity_id', 'MyFightDamage', [10020, 11443]),
        },
        family='EclipseSlashesSkillGraphs', cache_name='eclipse-slashes', generator=Path(__file__),
        restore_current_views=True,
        external_assets=verified_noise_mask_binding(OfficialSource().prepared_path / 'eclipse-slashes',
            reference_receipt='assets/reference/battle-animation/meteor-fire-current-skill-graphs-source-manifest.json'))
