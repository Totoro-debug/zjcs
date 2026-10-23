"""Refresh converted Home layers against official hierarchy and sprite geometry."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

import numpy as np

from official_apk import Apk, ROOT
from refresh_current_character_meshes import preserve
from UnityPy.helpers.MeshHelper import MeshHandler


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def multiply(left, right):
    # These matrices have four rows. Avoid loading a process-wide BLAS DLL for
    # small hierarchy operations when invoked outside an activated Conda shell.
    return [[sum(left[i][k] * right[k][j] for k in range(len(right)))
             for j in range(len(right[0]))] for i in range(len(left))]


def local_matrix(trs):
    x, y, z, w = trs['rotation']
    scale = trs['scale']
    rotation = [[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)],
                [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)],
                [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]]
    result = [[rotation[i][j] * scale[j] for j in range(3)] + [trs['position'][i]] for i in range(3)]
    return result + [[0.0, 0.0, 0.0, 1.0]]


def refine_local_matrices(receipt, receipt_path):
    """Bring retained local matrices into line with the converted local TRS."""
    pending = []
    for record in receipt['outputs']:
        path = ROOT / record['path']
        raw = path.read_bytes()
        if sha(raw) != record['sha256']:
            raise ValueError('Converted Home payload changed after activation: ' + record['path'])
        if b'"local_matrices"' not in raw:
            continue
        value = json.loads(raw)
        changes = []
        mappings = value.get('local_matrices', {})
        for row in receipt.get('node_states', []):
            old, new = row['versions']
            key = old['path']
            if key not in mappings or old['local_trs'] == new['local_trs']:
                continue
            a, b = local_matrix(old['local_trs']), local_matrix(new['local_trs'])
            if np.allclose(mappings[key], a, atol=0.00001, rtol=0):
                mappings[key] = b
                changes.append(key)
        if changes:
            payload = json.dumps(value, ensure_ascii=False, separators=(',', ':')).encode('utf8')
            pending.append((record, path, payload, changes))
    if pending:
        previous_receipt = preserve(receipt_path)
        for record, path, payload, changes in pending:
            intermediate = preserve(path)
            path.write_bytes(payload)
            record.setdefault('intermediate_outputs', []).append(intermediate)
            record['sha256'] = sha(payload)
            record['bytes'] = len(payload)
            staged = ROOT / '.tools/source-packages/current' / receipt['manifest'] / 'home-layer-payloads' / (sha(payload) + '.json')
            staged.write_bytes(payload)
            record['staged'] = staged.relative_to(ROOT).as_posix()
            record['local_matrix_refinement'] = changes
        receipt.setdefault('refinements', []).append({'operation': 'Synchronize local matrices with converted local TRS.',
            'generator_sha256': sha(Path(__file__).read_bytes()), 'previous_receipt': previous_receipt})
        receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8')
    return receipt


def hierarchy(transform):
    q = transform.m_LocalRotation
    x, y, z, w = q.x, q.y, q.z, q.w
    rotation = np.array([[1-2*(y*y+z*z), 2*(x*y-z*w), 2*(x*z+y*w)],
                         [2*(x*y+z*w), 1-2*(x*x+z*z), 2*(y*z-x*w)],
                         [2*(x*z-y*w), 2*(y*z+x*w), 1-2*(x*x+y*y)]])
    scale = transform.m_LocalScale
    position = transform.m_LocalPosition
    matrix = np.eye(4)
    matrix[:3, :3] = rotation * np.array([scale.x, scale.y, scale.z])
    matrix[:3, 3] = [position.x, position.y, position.z]
    name = '/' + transform.m_GameObject.read().m_Name
    if transform.m_Father:
        parent, parent_matrix = hierarchy(transform.m_Father.read())
        return parent + name, np.array(multiply(parent_matrix.tolist(), matrix.tolist()))
    return name, matrix


def state(apk, obj):
    renderer = obj.read()
    go = renderer.m_GameObject.read()
    transform = next(pair.component.read() for pair in go.m_Component if pair.component.type.name == 'Transform')
    path, matrix = hierarchy(transform)
    sprite = renderer.m_Sprite.read() if renderer.m_Sprite else None
    bounds = None
    if sprite:
        mesh = MeshHandler(sprite.m_RD, sprite.object_reader.version)
        mesh.process()
        vertices = mesh.m_Vertices
        bounds = [min(p[0] for p in vertices), min(p[1] for p in vertices),
                  max(p[0] for p in vertices), max(p[1] for p in vertices)]
    color = renderer.m_Color
    materials = []
    for pointer in renderer.m_Materials:
        if not pointer:
            materials.append(None)
            continue
        material = pointer.read()
        shader = material.m_Shader.read()
        materials.append({'name': material.m_Name,
                          'shader': shader.m_ParsedForm.m_Name,
                          'colors': {name: [value.r, value.g, value.b, value.a]
                                     for name, value in material.m_SavedProperties.m_Colors},
                          'floats': dict(material.m_SavedProperties.m_Floats)})
    return {'path': path, 'matrix': matrix.tolist(), 'bounds': bounds,
            'sprite': sprite.m_Name if sprite else None,
            'color': [color.r, color.g, color.b, color.a],
            'sorting_layer': renderer.m_SortingLayer, 'sorting_order': renderer.m_SortingOrder,
            'flip_x': renderer.m_FlipX, 'flip_y': renderer.m_FlipY,
            'materials': materials,
            'source': apk.provenance(obj), 'raw_sha256': sha(obj.get_raw_data())}


def quad(record):
    x0, y0, x1, y1 = record['bounds']
    points = np.array([[x0, y0, 0, 1], [x1, y0, 0, 1],
                       [x1, y1, 0, 1], [x0, y1, 0, 1]], dtype=float)
    if record['flip_x']:
        points[:, 0] *= -1
    if record['flip_y']:
        points[:, 1] *= -1
    return np.array(multiply(record['matrix'], points.T.tolist())).T[:, :2]


def build(activate=False):
    apks = [Apk(), Apk.current()]
    cache = ROOT / '.tools/source-packages/current' / apks[1].version
    activated = ROOT / 'assets/reference/official-map/current-home-layers-source-manifest.json'
    if activated.exists():
        prior = json.loads(activated.read_bytes())
        if prior['manifest'] == apks[1].version:
            from current_conversion_state import verify_outputs
            verify_outputs(prior['outputs'], apks[1].version)
            if prior.get('refinements'):
                return prior
            return refine_local_matrices(prior, activated)
    proof_path = cache / 'home-layer-states-v2.json'
    node_proof_path = cache / 'home-node-states.json'
    if proof_path.exists():
        states = json.loads(proof_path.read_bytes())
    else:
        comparison = json.loads((cache / 'referenced-object-comparison.json').read_bytes())
        bundles = {row['bundle'] for row in comparison if not row.get('equal')
                   and '_home_' in row['bundle'] and row.get('type') in ('Transform', 'SpriteRenderer', 'GameObject')}
        states = []
        for bundle in sorted(bundles):
            versions = [{obj.path_id: obj for obj in apk.objects(bundle) if obj.type.name == 'SpriteRenderer'}
                        for apk in apks]
            for identity in versions[0].keys() & versions[1].keys():
                records = [state(apk, objects[identity]) for apk, objects in zip(apks, versions)]
                if any(records[0][key] != records[1][key] for key in
                       ('path', 'matrix', 'bounds', 'sprite', 'color', 'sorting_layer', 'sorting_order', 'flip_x', 'flip_y', 'materials')):
                    states.append({'bundle': bundle, 'path_id': str(identity), 'versions': records})
            print('Compared Home renderer hierarchy:', bundle, flush=True)
        proof_path.write_text(json.dumps(states, ensure_ascii=False, indent=2) + '\n', 'utf8')
    if node_proof_path.exists():
        nodes = json.loads(node_proof_path.read_bytes())
    else:
        nodes = []
        for bundle in sorted({row['bundle'] for row in states}):
            versions = [{obj.path_id: obj for obj in apk.objects(bundle) if obj.type.name == 'Transform'}
                        for apk in apks]
            for identity in versions[0].keys() & versions[1].keys():
                records = []
                for apk, objects in zip(apks, versions):
                    obj = objects[identity]
                    transform = obj.read()
                    path, world = hierarchy(transform)
                    p, q, s = transform.m_LocalPosition, transform.m_LocalRotation, transform.m_LocalScale
                    records.append({'path': path, 'matrix': world.tolist(),
                                    'local_trs': {'position': [p.x,p.y,p.z], 'rotation': [q.x,q.y,q.z,q.w],
                                                  'scale': [s.x,s.y,s.z]},
                                    'active': bool(transform.m_GameObject.read().m_IsActive),
                                    'source': apk.provenance(obj), 'raw_sha256': sha(obj.get_raw_data())})
                if any(records[0][key] != records[1][key] for key in ('path','matrix','local_trs','active')):
                    nodes.append({'bundle': bundle, 'path_id': str(identity), 'versions': records})
        node_proof_path.write_text(json.dumps(nodes, ensure_ascii=False, indent=2) + '\n', 'utf8')
    by_path = {}
    for row in states:
        before, after = row['versions']
        if before['path'] != after['path']:
            raise ValueError('Home layer changed hierarchy path: ' + before['path'])
        by_path.setdefault(before['path'], []).append(row)
    paths = set((ROOT / 'assets/official').glob('home*/**/*.json'))
    paths.add(ROOT / 'assets/official/home.json')
    paths.update((ROOT / 'unity/Assets/StreamingAssets/Home').glob('*.json'))
    output = cache / 'home-layer-payloads'
    output.mkdir(parents=True, exist_ok=True)
    converted = []
    for path in sorted(paths):
        raw = path.read_bytes()
        if b'"vertices"' not in raw:
            continue
        document = json.loads(raw)
        changes = []

        def walk(value, location='', matrices=None):
            if isinstance(value, list):
                for index, child in enumerate(value):
                    walk(child, location + '/' + str(index), matrices)
            elif isinstance(value, dict):
                matrices = value.get('object_matrices', matrices)
                matches = by_path.get(value.get('path', ''), [])
                if matches and isinstance(value.get('sprite'), dict) and 'vertices' in value:
                    matches = [row for row in matches if row['versions'][0]['sprite'] == value['sprite'].get('name')]
                    if len(matches) > 1 and matrices and value['path'] in matrices:
                        matches = [row for row in matches if np.allclose(
                            row['versions'][0]['matrix'], matrices[value['path']], atol=0.00001, rtol=0)]
                    if len(matches) > 1:
                        signatures = {json.dumps([{key: item for key, item in version.items()
                                                   if key not in ('source', 'raw_sha256')}
                                                  for version in row['versions']], sort_keys=True)
                                      for row in matches}
                        if len(signatures) == 1:
                            matches = matches[:1]
                        else:
                            raise ValueError('Ambiguous official Home layer: ' + str(path) + location
                                             + ':' + str([(row['bundle'], row['path_id']) for row in matches]))
                    if matches:
                        row = matches[0]
                        before, after = row['versions']
                        edits = []
                        for key in ('color', 'sorting_layer', 'sorting_order'):
                            if before[key] != after[key]:
                                if value.get(key) not in (before[key], after[key]):
                                    raise ValueError('Home layer property differs from source: ' + str(path) + location + '/' + key)
                                value[key] = after[key]
                                edits.append(key)
                        if before['materials'] != after['materials'] and 'materials' in value:
                            actual = value['materials']
                            if len(actual) != len(before['materials']) or len(actual) != len(after['materials']):
                                raise ValueError('Home material slot count differs: ' + str(path) + location)
                            for target, old_material, new_material in zip(actual, before['materials'], after['materials']):
                                for key in ('name', 'shader', 'colors', 'floats'):
                                    if key in target and old_material[key] != new_material[key]:
                                        if target[key] not in (old_material[key], new_material[key]):
                                            raise ValueError('Home material differs from official properties: ' + str(path) + location + '/' + key)
                                        target[key] = new_material[key]
                            edits.append('materials')
                        if before['matrix'] != after['matrix'] or before['bounds'] != after['bounds']:
                            if before['sprite'] != after['sprite']:
                                raise ValueError('Home layer sprite replacement requires binding conversion')
                            old_quad, new_quad = quad(before), quad(after)
                            flattened = bool(value['vertices'] and isinstance(value['vertices'][0], (int, float)))
                            vertices = np.array(value['vertices']).reshape((4, 2))
                            offset = (vertices - old_quad).mean(axis=0)
                            if not np.allclose(vertices, old_quad + offset, atol=0.0001, rtol=0):
                                source_matrix = (matrices or {}).get(before['path'])
                                if source_matrix is None or not np.allclose(source_matrix, before['matrix'], atol=0.00001, rtol=0):
                                    raise ValueError('Converted Home geometry differs from official bounds: ' + str(path) + location)
                                # Some old flattened weather variants carry stale
                                # quads while retaining the exact official object
                                # matrices. Rebuild these from the proven matrix.
                                offset = np.zeros(2)
                                edits.append('restore_official_bounds')
                            result = new_quad + offset
                            value['vertices'] = result.flatten().tolist() if flattened else result.tolist()
                            edits.append('vertices')
                        if edits:
                            changes.append({'pointer': location, 'bundle': row['bundle'],
                                            'path_id': row['path_id'], 'fields': edits})
                for key, child in list(value.items()):
                    walk(child, location + '/' + key, matrices)
                # Keep the explicit hierarchy used by future regeneration in
                # sync with the quads, rather than leaving old transforms behind.
                if not any(field in value for field in ('object_matrices', 'local_trs', 'original_active')):
                    return
                for row in nodes:
                    before, after = row['versions']
                    node_path = before['path']
                    if node_path != after['path']:
                        continue
                    for field, source_key in (('object_matrices','matrix'), ('local_trs','local_trs'),
                                              ('original_active','active')):
                        mapping = value.get(field)
                        if not isinstance(mapping, dict) or node_path not in mapping or before[source_key] == after[source_key]:
                            continue
                        actual = mapping[node_path]
                        if actual == after[source_key]:
                            continue
                        if isinstance(actual, list):
                            matches = np.allclose(actual, before[source_key], atol=0.00001, rtol=0)
                        else:
                            matches = actual == before[source_key]
                        if not matches:
                            continue
                        mapping[node_path] = after[source_key]
                        changes.append({'pointer': location + '/' + field + '/' + node_path,
                                        'bundle': row['bundle'], 'path_id': row['path_id'], 'fields': [field]})

        walk(document)
        if changes:
            payload = json.dumps(document, ensure_ascii=False, separators=(',', ':')).encode('utf8')
            staged = output / (sha(payload) + '.json')
            staged.write_bytes(payload)
            converted.append({'path': path.relative_to(ROOT).as_posix(), 'previous_sha256': sha(raw),
                              'sha256': sha(payload), 'bytes': len(payload), 'staged': staged.relative_to(ROOT).as_posix(),
                              'changes': changes})
            print(converted[-1]['path'], len(changes), flush=True)
    receipt = {'manifest': apks[1].version, 'base_manifest': apks[0].version,
               'operation': __doc__, 'renderer_states': states, 'node_states': nodes, 'outputs': converted,
               'generator_sha256': sha(Path(__file__).read_bytes())}
    (output / 'source-manifest.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8')
    if activate:
        for record in converted:
            if sha((ROOT / record['path']).read_bytes()) != record['previous_sha256']:
                raise ValueError('Home layer payload changed while staging: ' + record['path'])
        for record in converted:
            target = ROOT / record['path']
            record['previous'] = preserve(target)
            temporary = target.with_name(target.name + '.current.tmp')
            temporary.write_bytes((ROOT / record['staged']).read_bytes())
            temporary.replace(target)
        destination = ROOT / 'assets/reference/official-map/current-home-layers-source-manifest.json'
        if destination.exists():
            preserve(destination)
        destination.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8')
    return receipt


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--activate', action='store_true')
    print('Converted Home layer payloads:', len(build(parser.parse_args().activate)['outputs']))
