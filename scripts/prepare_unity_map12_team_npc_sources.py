"""Recover every official NPC model used by the original team invitation selector."""
from __future__ import annotations

import argparse
import json
import hashlib
from collections import Counter

from official_apk import Apk, ROOT
from map13_native_schemas import NativeSchemas
from map12_team_native_schemas import install
from prepare_unity_bundle_inputs import collect, read_source
from prepare_unity_map12_sources import encode, sha, source_path
from UnityPy.classes.PPtr import PPtr

OUTPUT = ROOT / 'unity/Assets/Official/World/Map/12/Source/team-npc-prefabs.json'
RECEIPT = ROOT / 'assets/reference/official-map/map12-team-npc-prefabs-source-manifest.json'
SELECTOR = ROOT / 'assets/reference/official-map/map12-stele-npcs-source-manifest.json'


def build(write):
    apk = Apk.current()
    current = ROOT / '.tools/source-packages/current' / apk.version
    tables_path = current / 'official-data/tables.json'
    tables, selector = json.loads(tables_path.read_bytes()), json.loads(SELECTOR.read_bytes())
    if apk.version != '871_156598' or selector['manifest'] != apk.version:
        raise ValueError('Original invitation NPC version differs')
    selected = set(selector['candidateNpcClassIds'])
    npcs = [row for row in tables['npc'] if row['Id'] in selected]
    if len(npcs) != 95 or len(selected) != 95:
        raise ValueError('Original invitation NPC selector closure differs')
    proof_path = current / 'table-proof-source-manifest.json'
    table_proof = json.loads(proof_path.read_bytes())
    if table_proof['manifest'] != apk.version or table_proof['table_sha256'] != sha(tables_path.read_bytes()):
        raise ValueError('Original NPC table constructor proof differs')
    bundles, unavailable = {}, []
    for npc in npcs:
        path = npc['FigureModelUI']
        prefix = path.rsplit('/', 1)[0].lower().replace('/', '_')
        name = 'defaultpackage_assets_prefabs_' + prefix + '.bundle'
        if name not in apk.index:
            if npc['Id'] not in range(11015, 11019):
                raise ValueError('Resolve original FigureModelUI bundle: ' + path)
            unavailable.append(dict(npcClassId=npc['Id'], figureModelUI=path, bundle=name,
                reason='Absent from the selected original asset manifest; confirmed offline consumer exclusion.'))
            continue
        bundles[npc['Id']] = name
    if [row['npcClassId'] for row in unavailable] != list(range(11015, 11019)) or len(bundles) != 91:
        raise ValueError('Confirmed original NPC resource boundary differs')
    _, missing = collect(apk, sorted(set(bundles.values())))
    if missing:
        raise ValueError('Resolve original NPC model CAB dependencies: ' + str(missing))
    schemas = NativeSchemas(current / 'official-code')
    schema_proof = install(schemas, current / 'official-code')
    objects, references, descriptions, models = {}, {}, {}, []

    def identity(obj):
        return obj.assets_file.name + ':' + str(obj.path_id)

    def reference(obj):
        return dict(identity=identity(obj), type=obj.type.name,
                    source=apk.provenance(obj), payload_sha256=sha(obj.get_raw_data()))

    def pointers(value, owner):
        if isinstance(value, dict):
            if set(value) == {'m_FileID', 'm_PathID'} and value['m_PathID']:
                obj = PPtr(**value, assetsfile=owner.assets_file).deref()
                references.setdefault(identity(obj), reference(obj))
            else:
                for item in value.values():
                    pointers(item, owner)
        elif isinstance(value, list):
            for item in value:
                pointers(item, owner)

    def decode(obj):
        key = identity(obj)
        if key in objects:
            return key
        row = reference(obj)
        if obj.type.name == 'MonoBehaviour':
            script = obj.read(check_read=False).m_Script.read()
            fields, tree = schemas.read(obj)
            name = (script.m_Namespace + '.' if script.m_Namespace else '') + script.m_ClassName
            row.update(assembly=script.m_AssemblyName, scriptType=name)
            description = [dict(level=n.m_Level, type=n.m_Type, name=n.m_Name, flags=n.m_MetaFlag or 0)
                           for n in tree.traverse()]
            schema_key = script.m_AssemblyName + ':' + name
            if schema_key in descriptions and descriptions[schema_key] != description:
                raise ValueError('Original NPC schema changes within the prefab set: ' + name)
            descriptions[schema_key] = description
        else:
            fields = obj.read_typetree(check_read=True)
        row['fields'] = fields
        objects[key] = row
        pointers(fields, obj)
        return key

    for npc in npcs:
        if npc['Id'] not in bundles:
            continue
        wanted = ('Assets/Prefabs/' + npc['FigureModelUI'] + '.prefab').casefold()
        roots = {identity(ptr.deref()): (path, ptr) for path, ptr in apk.container(bundles[npc['Id']])
                 if path.casefold() == wanted}
        if len(roots) != 1:
            raise ValueError('Original FigureModelUI root is absent or ambiguous: ' + wanted)
        path, root = next(iter(roots.values()))
        nodes = []

        def visit(pointer, parent):
            obj = pointer.deref()
            go = obj.read()
            components = [entry.component.deref() for entry in go.m_Component]
            index = len(nodes)
            nodes.append(dict(gameObject=decode(obj), parentIndex=parent,
                              components=[decode(component) for component in components]))
            transform, = [component for component in components
                          if component.type.name in ('Transform', 'RectTransform')]
            for child in transform.read().m_Children:
                visit(child.read().m_GameObject, index)

        visit(root, -1)
        models.append(dict(npcClassId=npc['Id'], figureModelUI=npc['FigureModelUI'], path=path,
                           root=identity(root.deref()), nodes=nodes))
        print('Original invitation NPC model', npc['Id'], len(nodes), 'nodes', flush=True)
    _, missing = collect(apk, sorted(set(bundles.values())))
    if missing:
        raise ValueError('Resolve original NPC serialized-pointer CAB dependencies: ' + str(missing))
    payload = encode(dict(manifest=apk.version, mapClassId=12, models=models,
        unavailable=unavailable, candidateNpcClassIds=selector['candidateNpcClassIds'],
        objects=[objects[key] for key in sorted(objects)],
        references=[references[key] for key in sorted(references)], schemas=descriptions))
    meta_path = OUTPUT.with_name(OUTPUT.name + '.meta')
    guid = hashlib.md5(('zjcs:map12:team-npc-source:' + source_path(OUTPUT)).encode()).hexdigest()
    metadata = ('fileFormatVersion: 2\nguid: ' + guid + '\nTextScriptImporter:\n  externalObjects: {}\n').encode()

    def record(path):
        return dict(path=source_path(path), sha256=sha(path.read_bytes()))

    inputs = []
    for name in sorted(apk.loaded):
        raw, item = read_source(apk, name)
        inputs.append(dict(item, decoded_sha256=sha(raw)))
    scripts = Counter(row['scriptType'] for row in objects.values() if row['type'] == 'MonoBehaviour')
    receipt = encode(dict(format=1, manifest=apk.version, mapClassId=12,
        selector=record(SELECTOR), tables=record(tables_path), tableConstructorProof=record(proof_path),
        inputs=inputs, schemaProof=schema_proof,
        adapters=[record(ROOT / name) for name in (
            'scripts/prepare_unity_map12_team_npc_sources.py', 'scripts/map12_team_native_schemas.py',
            'scripts/map13_native_schemas.py', 'scripts/map13_entity_native_schemas.py',
            'scripts/official_unity_schema.py', 'scripts/prepare_unity_bundle_inputs.py')],
        originalCode=[record(current / 'official-code/client-source' / name) for name in
                      ('PlayerInfoUI.cs', 'TeamInviteNpc.cs', 'NpcUtils.cs')],
        counts=dict(candidates=95, models=len(models), unavailableModels=len(unavailable),
                    nodes=sum(len(row['nodes']) for row in models),
                    objects=len(objects), referencedObjects=len(references), componentTypes=dict(sorted(scripts.items()))),
        outputs=[dict(path=source_path(OUTPUT), sha256=sha(payload)),
                 dict(path=source_path(meta_path), sha256=sha(metadata))],
        operation='Resolve all 95 original TeamInviteNpc candidates through the exact FigureModelUI table path; '
                  'record the four absent model bundles and retain all 91 available original prefabs, '
                  'including the four existing models in the separately confirmed 11015-11022 consumer exclusion. '
                  'Preserve every active and inactive node, component order and complete serialized field '
                  'with signed Int64 CAB pointers and full payload consumption. Verify all actual APK-first '
                  'and selected-cloud transitive CAB bytes. Preserve all 95 table candidates in source '
                  'metadata; do not change the original selector or invent missing models.',
        limitations=['Source extraction only; native UI graphic, model resource bindings and actual NPC '
                     'invitation, party save and battle validation remain pending.']))
    for path, raw in ((OUTPUT, payload), (meta_path, metadata), (RECEIPT, receipt)):
        if write:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
        elif not path.is_file() or path.read_bytes() != raw:
            raise ValueError('Original invitation NPC source drift: ' + source_path(path))
    print('Original invitation NPC prefabs verified:', len(models), 'models', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true')
    build(parser.parse_args().write)
