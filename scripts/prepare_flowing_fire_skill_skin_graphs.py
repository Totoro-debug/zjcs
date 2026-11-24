"""Convert the current official flowing fire skill skin and its original extra casts."""
from pathlib import Path

from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs, verified_noise_mask_binding
from official_source import OfficialSource


DEST = ROOT / 'unity/Assets/Official/Combat/FlowingFireSkillSkinGraphs/Resources/Combat/FlowingFireSkillSkinGraphs'
DATA = ROOT / 'unity/Assets/Game/Resources/Combat/flowing-fire-skill-skin-current-skill-graphs.json'
RECEIPT = ROOT / 'assets/reference/battle-animation/flowing-fire-skill-skin-current-skill-graphs-source-manifest.json'
GROUPS = {
    'skills': ('skill_entity_id', 'MyFightSkill', [13590, 13593, 13594]),
    'hits': ('hit_entity_id', 'MyFightHitFixed', [13591]),
    'moves': ('action_entity_id', 'MyFightActionSkillMove', []),
    'damages': ('damage_entity_id', 'MyFightDamage', [13592]),
}


def main():
    cache = ROOT / '.tools/source-packages/current' / OfficialSource().version / 'conqueror-actives'
    build_current_skill_graphs(DEST=DEST, DATA=DATA, RECEIPT=RECEIPT, GROUPS=GROUPS,
        family='FlowingFireSkillSkinGraphs', cache_name='conqueror-actives', generator=Path(__file__),
        external_assets=verified_noise_mask_binding(cache))


if __name__ == '__main__':
    main()
