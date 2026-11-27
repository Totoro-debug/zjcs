"""Reuse an already migrated native entity only after checking its original identity."""
import copy
import json
from pathlib import Path

from official_apk import ROOT
from prepare_battle_scene_appearance_runtime import identity, write_changed


def reuse_verified_native_entities(data_path, receipt_path, reference_data, reference_receipt, selections):
    data, proof = json.loads(data_path.read_bytes()), json.loads(receipt_path.read_bytes())
    reference = json.loads(reference_data.read_bytes())
    source = json.loads(reference_receipt.read_bytes())
    for path, receipt in ((data_path, proof), (reference_data, source)):
        assert identity(path) in receipt['outputs'], 'Native reuse catalog differs from its conversion receipt'
    assert data['manifest'] == proof['manifest'] == reference['manifest'] == source['manifest']
    reused = []
    # Validate the complete request before changing either catalog or receipt.
    for group, id_key, class_id in selections:
        left = [row for row in data[group] if row[id_key] == class_id]
        right = [row for row in reference[group] if row[id_key] == class_id]
        assert len(left) == len(right) == 1, 'Native reuse must resolve one entity in each catalog'
        def shape(row):
            row = copy.deepcopy(row)
            row.pop('entity_prefab_sha256', None)  # Exporter GUIDs differ between isolated conversions.
            row['entity_resource'] = row['entity_resource'].split('/', 2)[2]
            for playable in row['playables']:
                playable['resource'] = playable['resource'].split('/', 2)[2]
            return row
        assert shape(left[0]) == shape(right[0]), 'Native reuse changed converted fields or playable bindings'
        def original(receipt):
            rows = [row for row in receipt['originals'] if row['id'] == f'{group}:{class_id}']
            assert len(rows) == 1
            return {key: rows[0][key] for key in ('source', 'original_game_object_sha256')}
        assert original(proof) == original(source), 'Native reuse changed the original entity'
        def pointers(receipt):
            return [dict(source=row['source'], object_sha256=row['object_sha256'])
                    for row in receipt['current_managed_views']['pointers'] if row['entity_id'] == class_id]
        assert pointers(proof) == pointers(source), 'Native reuse changed original playable objects'
        binding = [row for row in source['current_native_bindings'] if row['entity_id'] == class_id]
        assert len(binding) == 1 and binding[0]['converted'] in source['outputs']
        assert identity(ROOT / binding[0]['converted']['path']) == binding[0]['converted']
        reused.append(dict(group=group, entity_id=class_id, resource=right[0]['entity_resource'],
                           original=original(source), playable_objects=pointers(source)))
    for group, id_key, class_id in selections:
        data[group] = [row for row in data[group] if row[id_key] != class_id]
    write_changed(data_path, json.dumps(data, ensure_ascii=False, indent=2) + '\n')
    metadata = data_path.with_name(data_path.stem + '-source-manifest.json')
    write_changed(metadata, json.dumps(dict(manifest=data['manifest'], output_sha256=identity(data_path)['sha256']), indent=2) + '\n')
    changed = {path.relative_to(ROOT).as_posix(): identity(path) for path in (data_path, metadata)}
    proof['outputs'] = [changed.get(row['path'], row) for row in proof['outputs']]
    proof['verified_shared_entities'] = dict(inputs=[identity(reference_data), identity(reference_receipt)],
                                             entities=reused, verifier=identity(Path(__file__)))
    write_changed(receipt_path, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
