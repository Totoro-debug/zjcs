"""Keep a current shared native entity local to its owning skill migration."""
import json
from pathlib import Path

from official_apk import ROOT
from prepare_battle_scene_appearance_runtime import identity, write_changed


def scope_verified_native_entities(data_path, receipt_path, selections):
    data = json.loads(data_path.read_bytes())
    proof = json.loads(receipt_path.read_bytes())
    assert data['manifest'] == proof['manifest']
    assert identity(data_path) in proof['outputs']
    scoped, updates, selected = [], [], set()
    for group, id_key, class_id, owners in selections:
        assert (group, class_id) not in selected
        selected.add((group, class_id))
        assert owners and all(type(owner) is int and owner > 0 for owner in owners)
        assert len(owners) == len(set(owners))
        row, = (row for row in data[group] if row[id_key] == class_id)
        assert 'skill_item_ids' not in row
        binding, = (row for row in proof['current_native_bindings'] if row['entity_id'] == class_id)
        assert binding['converted'] in proof['outputs']
        assert identity(ROOT / binding['converted']['path']) == binding['converted']
        updates.append((row, list(owners)))
        scoped.append(dict(group=group, entity_id=class_id, skill_item_ids=owners,
                           resource=row['entity_resource'], native_binding=binding))
    for row, owners in updates:
        row['skill_item_ids'] = owners
    write_changed(data_path, json.dumps(data, ensure_ascii=False, indent=2) + '\n')
    metadata = data_path.with_name(data_path.stem + '-source-manifest.json')
    write_changed(metadata, json.dumps(dict(manifest=data['manifest'],
        output_sha256=identity(data_path)['sha256']), indent=2) + '\n')
    changed = {path.relative_to(ROOT).as_posix(): identity(path) for path in (data_path, metadata)}
    proof['outputs'] = [changed.get(row['path'], row) for row in proof['outputs']]
    proof['scoped_entities'] = dict(entities=scoped, verifier=identity(Path(__file__)))
    write_changed(receipt_path, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
