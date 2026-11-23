"""Convert the current original skill, hit and cure entities for Fire Life."""
from pathlib import Path

from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs


DEST = ROOT / 'unity/Assets/Official/Combat/FireLifeGraphs/Resources/Combat/FireLifeGraphs'
DATA = ROOT / 'unity/Assets/Game/Resources/Combat/fire-life-current-skill-graphs.json'
RECEIPT = ROOT / 'assets/reference/battle-animation/fire-life-current-skill-graphs-source-manifest.json'
GROUPS = {
    'skills': ('skill_entity_id', 'MyFightSkill', [12040]),
    'hits': ('hit_entity_id', 'MyFightHitFixed', [12041]),
    'damages': ('damage_entity_id', 'MyFightDamage', [12042]),
}


def main():
    build_current_skill_graphs(DEST=DEST, DATA=DATA, RECEIPT=RECEIPT, GROUPS=GROUPS,
        family='FireLifeGraphs', cache_name='destroyer-fire-life', generator=Path(__file__))


if __name__ == '__main__':
    main()
