"""Separate a verified mixed native graph closure into its character-status catalog."""
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed


def build():
    directory = ROOT / 'unity/Assets/Game/Resources/Combat'
    closure_path = directory / 'passive-revival-statuses.json'
    graph_path = directory / 'passive-revival-current-skill-graphs.json'
    closure_proof = ROOT / 'assets/reference/battle-animation/passive-revival-assets-source-manifest.json'
    graph_proof = ROOT / 'assets/reference/battle-animation/passive-revival-current-skill-graphs-source-manifest.json'
    inputs = [identity(path) for path in (closure_path, graph_path, closure_proof, graph_proof)]
    for path, proof in ((closure_path, closure_proof), (graph_path, graph_proof)):
        assert identity(path) in json.loads(proof.read_bytes())['outputs']
    closure, graphs = json.loads(closure_path.read_bytes()), json.loads(graph_path.read_bytes())
    assert closure['manifest'] == graphs['manifest']
    entities = {row[key] for group, key in (('skills', 'skill_entity_id'), ('hits', 'hit_entity_id'),
        ('damages', 'damage_entity_id'), ('moves', 'action_entity_id')) for row in graphs[group]}
    script = ROOT / 'unity/Assets/Game/Runtime/Official/BattleStatusPlayables/MyFightStatus.cs.meta'
    guid = re.search(r'(?m)^guid: ([a-f0-9]{32})$', script.read_text('utf-8-sig'))[1]
    inputs.append(identity(script))
    verified = {row['path']: row for row in json.loads(closure_proof.read_bytes())['outputs']}
    rows = []
    for row in closure['graphs']:
        candidates = list((ROOT / 'unity/Assets/Official/Combat/PassiveRevival/Resources').glob(row['resource'] + '.prefab'))
        prefab, = candidates
        assert identity(prefab) == verified[prefab.relative_to(ROOT).as_posix()]
        inputs.append(identity(prefab))
        is_status = bool(re.search(r'm_Script: \{fileID: 11500000, guid: ' + guid, prefab.read_text('utf-8-sig')))
        assert is_status != (row['id'] in entities), ('Native graph has no unique catalog role', row['id'])
        if is_status:
            rows.append(row)
    assert rows
    target = directory / 'passive-revival-current-status-graphs.json'
    write_changed(target, json.dumps(dict(manifest=closure['manifest'], graphs=rows), indent=2) + '\n')
    meta(target)
    receipt = ROOT / 'assets/reference/battle-animation/passive-revival-current-status-graphs-source-manifest.json'
    write_changed(receipt, json.dumps(dict(inputs=inputs, outputs=[identity(target), identity(Path(str(target) + '.meta'))],
        generator=identity(Path(__file__)),
        operation='Retain every converted native object; classify character-status roots by the actual migrated status component. '
                  'Verify all remaining roots against their existing typed skill, hit, damage and movement catalogs.'), indent=2) + '\n')
    print('Verified character-status catalog:', len(rows), 'statuses;', len(entities), 'other native entity roots.')


if __name__ == '__main__':
    build()
