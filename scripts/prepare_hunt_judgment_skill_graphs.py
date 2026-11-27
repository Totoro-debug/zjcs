"""Restore Hunt Judgment's original grab, child pull, three strikes and move action."""
from pathlib import Path
from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs, verified_noise_mask_binding
from official_source import OfficialSource


if __name__ == '__main__':
    build_current_skill_graphs(
        DEST=ROOT / 'unity/Assets/Official/Combat/HuntJudgmentSkillGraphs/Resources/Combat/HuntJudgmentSkillGraphs',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/hunt-judgment-current-skill-graphs.json',
        RECEIPT=ROOT / 'assets/reference/battle-animation/hunt-judgment-current-skill-graphs-source-manifest.json',
        GROUPS={
            'skills': ('skill_entity_id', 'MyFightSkill', [11820, 11830]),
            'hits': ('hit_entity_id', 'MyFightHitFixed', [11821, 11823, 11831]),
            'moves': ('action_entity_id', 'MyFightActionSkillMove', [11833]),
            'damages': ('damage_entity_id', 'MyFightDamage', [11822, 11824, 11832]),
        },
        family='HuntJudgmentSkillGraphs', cache_name='hunt-judgment', generator=Path(__file__),
        restore_current_views=True,
        external_assets=verified_noise_mask_binding(OfficialSource().prepared_path / 'hunt-judgment',
            reference_receipt='assets/reference/battle-animation/meteor-fire-current-skill-graphs-source-manifest.json'))
