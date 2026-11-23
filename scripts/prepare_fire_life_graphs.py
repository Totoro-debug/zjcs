"""Convert the current original skill, hit and cure entities for Fire Life."""
import json
from pathlib import Path

from official_apk import ROOT
from current_battle_native_skill_graphs import build_current_skill_graphs
from prepare_battle_scene_appearance_runtime import identity, write_changed


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
    data = json.loads(DATA.read_bytes())
    if data.get('moves') != []:
        data['moves'] = []
        write_changed(DATA, json.dumps(data, ensure_ascii=False, indent=2) + '\n')
        receipt = json.loads(RECEIPT.read_bytes())
        metadata = DATA.with_name(DATA.stem + '-source-manifest.json')
        write_changed(metadata, json.dumps(dict(manifest=receipt['manifest'],
            output_sha256=identity(DATA)['sha256']), indent=2) + '\n')
        updated_paths = set()
        for output in receipt['outputs']:
            if output['path'] in (DATA.relative_to(ROOT).as_posix(), metadata.relative_to(ROOT).as_posix()):
                output.update(identity(ROOT / output['path']))
                updated_paths.add(output['path'])
        for path in (DATA.relative_to(ROOT).as_posix(), metadata.relative_to(ROOT).as_posix()):
            if path not in updated_paths:
                receipt['outputs'].append(identity(ROOT / path))
        write_changed(RECEIPT, json.dumps(receipt, ensure_ascii=False, indent=2) + '\n')


if __name__ == '__main__':
    main()
