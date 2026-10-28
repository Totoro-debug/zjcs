"""Recover the original prefab hierarchy around all 57 Map13 prop renderers.

Native fields are read from the selected original bundle, never inferred from
the converted geometry. Managed fields retain the verified source registry.
"""
from __future__ import annotations
import hashlib
import json
from pathlib import Path
import uuid

from official_apk import Apk, ROOT

SOURCE = ROOT / 'unity/Assets/Official/World/Map/13/Source'
CATALOG = ROOT / 'unity/Assets/Official/World/Map/13/NativeSkeletonModels/catalog.json'
DEST = SOURCE / 'native-prop-hierarchies.json'
RECEIPT = ROOT / 'assets/reference/official-map/map13-native-prop-hierarchies-source-manifest.json'
SEPARATORS = SOURCE / 'native-prop-separators.json'


def sha(raw): return hashlib.sha256(raw).hexdigest()
def key(cab, pid): return cab.lower() + ':' + str(pid)
def encode(value): return (json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + '\n').encode()


def write(path, raw):
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists() or path.read_bytes() != raw:
        temp = path.with_name(path.name + '.map13.tmp')
        temp.write_bytes(raw)
        temp.replace(path)


def build():
    apk = Apk.current()
    graph_path = SOURCE / 'native-entity-object-graph.json'
    components_path = SOURCE / 'native-entity-components.json'
    graph, components, catalog = [json.loads(p.read_bytes()) for p in (graph_path, components_path, CATALOG)]
    for receipt_name, source_path in (
        ('map13-native-entity-object-graph-source-manifest.json', graph_path),
        ('map13-native-skeleton-models-source-manifest.json', CATALOG)):
        receipt = json.loads((RECEIPT.parent / receipt_name).read_bytes())
        expected, = [r for r in receipt['outputs'] if r['path'] == source_path.relative_to(ROOT).as_posix()]
        if sha(source_path.read_bytes()) != expected['sha256']:
            raise ValueError('Verified original dependency changed: ' + str(source_path))
    if any(d['manifest'] != apk.version for d in (graph, components, catalog)):
        raise ValueError('Original prefab hierarchy must use the selected Map13 version')
    indexed = {key(r['cab'], r['path_id']): r for r in graph['objects']}
    managed = {key(r['cab'], r['path_id']): r for r in components['components']}
    native, fields, bundles, records = {}, {}, {}, {}

    def record(identity):
        if identity in records:
            return records[identity]
        original = indexed[identity]
        if original['input'] not in bundles:
            objects = apk.objects(original['input'])
            bundles[original['input']] = objects
            native.update({key(o.assets_file.name, o.path_id): o for o in objects})
        obj = native[identity]
        raw = obj.get_raw_data()
        expected = original.get('original_payload_sha256', original['payload_sha256'])
        if sha(raw) != expected:
            raise ValueError('Original prefab object changed: ' + identity)
        if obj.type.name == 'MonoBehaviour':
            body = managed[identity]['fields']
            kind = managed[identity]['type']
        else:
            body = obj.read_typetree(check_read=True)
            kind = obj.type.name
        fields[identity] = body
        result = dict(key=identity, cab=original['cab'], path_id=original['path_id'],
                      type=kind, source=apk.provenance(obj), payload_sha256=sha(raw),
                      references=original['references'], fields=body)
        records[identity] = result
        return result

    def pointer(owner, value):
        if not value['m_PathID']:
            return None
        obj = native[owner]
        cab = obj.assets_file.name if value['m_FileID'] == 0 else Path(
            obj.assets_file.externals[value['m_FileID'] - 1].path).name
        return key(cab, value['m_PathID'])

    def transform_for(go):
        body = record(go)['fields']
        candidates = [pointer(go, c['component']) for c in body['m_Component']]
        transforms = [c for c in candidates if indexed[c]['type'] in ('Transform', 'RectTransform')]
        if len(transforms) != 1:
            raise ValueError('Original GameObject must have one transform: ' + go)
        return transforms[0]

    profiles, roots = [], {}
    for renderer in catalog['renderers']:
        renderer_key = key(renderer['cab'], renderer['path_id'])
        body = record(renderer_key)['fields']
        go = pointer(renderer_key, body['m_GameObject'])
        node = transform_for(go)
        while True:
            parent = pointer(node, record(node)['fields']['m_Father'])
            if not parent:
                break
            node = parent
        root = pointer(node, fields[node]['m_GameObject'])
        profiles.append(dict(renderer=renderer_key, model=renderer['model'], gameObject=go, root=root))
        if root in roots:
            continue
        visited, gos = set(), []

        def visit(transform):
            if transform in visited:
                raise ValueError('Original prefab hierarchy contains a cycle')
            visited.add(transform)
            transform_body = record(transform)['fields']
            game_object = pointer(transform, transform_body['m_GameObject'])
            go_body = record(game_object)['fields']
            gos.append(game_object)
            for item in go_body['m_Component']:
                component = pointer(game_object, item['component'])
                if component:
                    record(component)
            for child in transform_body['m_Children']:
                visit(pointer(transform, child))

        visit(node)
        roots[root] = dict(root=root, gameObjects=gos,
            paths=[r['path'] for r in graph['roots'] if key(r['cab'], r['path_id']) == root])
    outputs = dict(manifest=apk.version, mapClassId=13, profiles=profiles,
                   roots=list(roots.values()), objects=[records[k] for k in sorted(records)])
    write(DEST, encode(outputs))
    material_path = SOURCE / 'native-entity-material-bindings.json'
    materials = {key(r['cab'], r['path_id']): r['output']['path'] for r in json.loads(material_path.read_bytes())['entries']}
    separator_records = []
    for separator in list(records.values()):
        if separator['type'] != 'Spine.Unity.SkeletonRenderSeparator':
            continue
        identity = separator['key']
        renderer_key = pointer(identity, separator['fields']['skeletonRenderer'])
        profile = next(p for p in profiles if p['renderer'] == renderer_key)
        renderer = records[renderer_key]
        tree_go = pointer(renderer_key, renderer['fields']['m_GameObject'])
        tree_transform = transform_for(tree_go)
        transforms = [tree_transform]
        for transform in transforms:
            transforms.extend(pointer(transform, c) for c in fields[transform]['m_Children'])
        nodes = []
        for transform in transforms:
            game_object = pointer(transform, fields[transform]['m_GameObject'])
            go = fields[game_object]
            node = dict(key=game_object, transform_key=transform, name=go['m_Name'], layer=go['m_Layer'],
                        active=go['m_IsActive'], transform=fields[transform],
                        parent=pointer(transform, fields[transform]['m_Father']))
            components_here = [pointer(game_object, c['component']) for c in go['m_Component']]
            for component in components_here:
                native_record = records[component]
                body = native_record['fields']
                if native_record['type'] == 'MeshRenderer':
                    node['mesh'] = body
                    node['materials'] = [materials[key(r['cab'], r['path_id'])] for r in native_record['references'] if r['field'].startswith('.m_Materials[')]
                elif native_record['type'] == 'Animator':
                    ref, = [r for r in native_record['references'] if r['field'] == '.m_Controller']
                    node['animator'] = body
                    node['controller'] = sha(key(ref['cab'], ref['path_id']).encode())[:24]
            nodes.append(node)
        parts = [pointer(identity, part) for part in separator['fields']['partsRenderers']]
        separator_records.append(dict(renderer=renderer_key, model=profile['model'], nodes=nodes,
            separatorSlots=renderer['fields']['separatorSlotNames'], source=separator,
            parts=[pointer(part, fields[part]['m_GameObject']) for part in parts]))
    write(SEPARATORS, encode(dict(manifest=apk.version, mapClassId=13, separators=separator_records)))
    for output in (DEST, SEPARATORS):
        meta = Path(str(output) + '.meta')
        if not meta.exists():
            guid = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs:map13-native-prop-hierarchies:' + output.name).hex
            write(meta, ('fileFormatVersion: 2\nguid: ' + guid + '\nTextScriptImporter:\n  externalObjects: {}\n').encode())
    dependencies = [graph_path, components_path, CATALOG, material_path, Path(__file__).resolve()]
    metadata_path = ROOT / '.tools/source-packages/official-code/global-metadata.dat'
    raw_metadata = metadata_path.read_bytes()
    if sha(raw_metadata) != '58884b94b56ef35665f44d3f186fce8b8c854cf3bab0a74e4733a18b6a9175b4':
        raise ValueError('Original serialized enum metadata changed')
    from official_metadata import Metadata
    metadata = Metadata(raw_metadata)
    enums = {}
    for name in ('UpdateTiming', 'UpdateMode'):
        typ, = [t for t in metadata.types if t['namespace'] == 'Spine.Unity' and t['name'] == name and t['enum']]
        enums[name] = {f['name']: metadata.default_int(f['index']) for f in typ['fields'] if f['index'] in metadata.defaults}
    if enums['UpdateTiming'].get('InUpdate') != 1 or enums['UpdateMode'].get('Nothing') != 0:
        raise ValueError('Original prop lifecycle enum values differ')
    dependencies.append(metadata_path)
    proof = dict(manifest=apk.version, mapClassId=13,
        method='Original native GameObject/Transform/component fields and verified managed fields; no hierarchy synthesis.',
        serialized_enum_constants=enums,
        counts=dict(profiles=len(profiles), roots=len(roots), gameObjects=sum(len(r['gameObjects']) for r in roots.values()),
                    objects=len(records), bundles=len(bundles), separators=len(separator_records)),
        inputs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p.read_bytes())) for p in dependencies],
        outputs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p.read_bytes()))
                 for p in (DEST, Path(str(DEST) + '.meta'), SEPARATORS, Path(str(SEPARATORS) + '.meta'))])
    write(RECEIPT, encode(proof))
    print(json.dumps(proof['counts']))


if __name__ == '__main__':
    build()
