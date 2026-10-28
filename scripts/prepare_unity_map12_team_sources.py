"""Decode original stele team UI hierarchy and every serialized component field."""
from __future__ import annotations

import argparse
import hashlib
import json

from official_apk import Apk, ROOT
from map13_native_schemas import NativeSchemas
from map13_entity_native_schemas import install
from prepare_unity_bundle_inputs import collect, read_source
from prepare_unity_map12_sources import encode, sha, source_path
from UnityPy.classes.PPtr import PPtr

BUNDLE = 'defaultpackage_assets_prefabs_ui_team.bundle'
KEYS = ('TeamPanel', 'TeamPanelItem', 'TeamPanelDialogButtonGroup', 'TeamMemberIcon',
        'TeamPanelTarget', 'TeamPanelTargetItem', 'TeamPanelTargetSubItem',
        'TeamInvitePanel', 'TeamInvitePanelPlayerItem', 'TeamInvitePanelTabGroupItem_11')
OUTPUT = ROOT / 'unity/Assets/Official/World/Map/12/Source/team-ui-prefabs.json'
RECEIPT = ROOT / 'assets/reference/official-map/map12-team-ui-source-manifest.json'
TOOLS = ('scripts/prepare_unity_map12_team_sources.py', 'scripts/map13_native_schemas.py',
         'scripts/map13_entity_native_schemas.py', 'scripts/official_unity_schema.py',
         'scripts/prepare_unity_bundle_inputs.py')


def build(write):
    tool_records = [dict(path=name, sha256=sha((ROOT / name).read_bytes())) for name in TOOLS]
    apk = Apk.current()
    if apk.version != '871_156598':
        raise ValueError('Map12 team source selection changed')
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    code_manifest_path = code.parent / 'code-manifest.json'
    code_manifest = json.loads(code_manifest_path.read_bytes())
    if code_manifest['manifest'] != apk.version or code_manifest['apk_sha256'] != apk.source.apk_sha256:
        raise ValueError('Original team code and APK selection differ')
    assemblies = []
    for name, item in code_manifest['assemblies'].items():
        path = code / 'managed/decoded' / name
        if sha(path.read_bytes()) != item['decoded_sha256']:
            raise ValueError('Original team schema assembly differs: ' + name)
        assemblies.append(dict(path=source_path(path), sha256=item['decoded_sha256']))
    assets, missing = collect(apk, [BUNDLE])
    if missing:
        raise ValueError('Resolve original team CAB dependencies: ' + str(missing))
    schemas = NativeSchemas(code)
    proof = install(schemas, code)
    if proof['metadata']['sha256'] != code_manifest['metadata_sha256']:
        raise ValueError('Original team native metadata and selected managed code differ')
    roots = {path.rsplit('/', 1)[-1][:-7]: (path, pointer)
             for path, pointer in apk.container(BUNDLE) if path.endswith('.prefab')}
    objects, descriptions, references, panels = {}, {}, {}, []
    script_count = 0

    def identity(obj):
        return obj.assets_file.name + ':' + str(obj.path_id)

    def reference_record(obj):
        return dict(identity=identity(obj), type=obj.type.name,
                    source=apk.provenance(obj), payload_sha256=sha(obj.get_raw_data()))

    def resolve_references(value, owner):
        if isinstance(value, dict):
            if 'm_FileID' in value and 'm_PathID' in value and value['m_PathID']:
                obj = PPtr(**value, assetsfile=owner.assets_file).deref()
                key = identity(obj)
                if key not in references:
                    references[key] = reference_record(obj)
            for item in value.values():
                resolve_references(item, owner)
        elif isinstance(value, list):
            for item in value:
                resolve_references(item, owner)

    def decode(obj):
        nonlocal script_count
        key = identity(obj)
        if key in objects:
            return key
        row = reference_record(obj)
        if obj.type.name == 'MonoBehaviour':
            script = obj.read(check_read=False).m_Script.read()
            fields, tree = schemas.read(obj)
            name = (script.m_Namespace + '.' if script.m_Namespace else '') + script.m_ClassName
            row.update(assembly=script.m_AssemblyName, scriptType=name)
            schema = [dict(level=node.m_Level, type=node.m_Type, name=node.m_Name,
                           flags=node.m_MetaFlag or 0) for node in tree.traverse()]
            schema_key = script.m_AssemblyName + ':' + name
            if schema_key in descriptions and descriptions[schema_key] != schema:
                raise ValueError('Component schema changed within the original prefab set: ' + schema_key)
            descriptions[schema_key] = schema
            script_count += 1
        else:
            fields = obj.read_typetree()
        row['fields'] = fields
        objects[key] = row
        resolve_references(fields, obj)
        return key

    for name in KEYS:
        path, pointer = roots[name]
        nodes = []

        def visit(go_pointer, parent):
            go_object = go_pointer.deref()
            go = go_object.read()
            index = len(nodes)
            components = [entry.component.deref() for entry in go.m_Component]
            nodes.append(dict(gameObject=decode(go_object), parentIndex=parent,
                              components=[decode(obj) for obj in components]))
            transform, = [obj for obj in components if obj.type.name in ('Transform', 'RectTransform')]
            for child in transform.read().m_Children:
                visit(child.read().m_GameObject, index)

        visit(pointer, -1)
        panels.append(dict(name=name, path=path, root=identity(pointer.deref()), nodes=nodes))
        print('Original team prefab', name, len(nodes), 'nodes', flush=True)
    if sum(len(row['nodes']) for row in panels) != 645 or script_count != 1105:
        raise ValueError('Original team hierarchy/component closure changed')
    assets, missing = collect(apk, [BUNDLE])
    if missing:
        raise ValueError('Resolve original team pointer CAB dependencies: ' + str(missing))
    payload = encode(dict(manifest=apk.version, mapClassId=12, panels=panels,
                          objects=[objects[key] for key in sorted(objects)],
                          references=[references[key] for key in sorted(references)], schemas=descriptions))
    meta_path = OUTPUT.with_name(OUTPUT.name + '.meta')
    guid = hashlib.md5(('zjcs-official:' + source_path(OUTPUT).removeprefix('unity/')).encode()).hexdigest()
    metadata = ('fileFormatVersion: 2\nguid: ' + guid + '\nTextScriptImporter:\n'
                '  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()

    def record(path):
        return dict(path=source_path(path), sha256=sha(path.read_bytes()))

    inputs = []
    for name in sorted(apk.loaded):
        raw, item = read_source(apk, name)
        inputs.append(dict(item, decoded_sha256=sha(raw)))
    originals = ['client-source/' + name + '.cs' for name in KEYS if (code / 'client-source' / (name + '.cs')).is_file()]
    originals += ['client-source/' + name + '.cs' for name in (
        'TeamPanelHelper', 'TeamInviteNpc', 'TeamInvitePlayerPanel', 'TeamInviter', 'InviterBase',
        'AllowMemberInviteToggle', 'TeamInvitePlayerComparer', 'TeamInviteComparer')]
    receipt = encode(dict(format=1, manifest=apk.version, mapClassId=12, inputs=inputs,
        codeManifest=record(code_manifest_path), assemblies=assemblies, originalCode=[record(code / p) for p in originals],
        schemaProof=proof, adapters=tool_records, serializedFiles=assets,
        outputs=[dict(path=source_path(OUTPUT), sha256=sha(payload), bytes=len(payload)),
                 dict(path=source_path(meta_path), sha256=sha(metadata), bytes=len(metadata))],
        counts=dict(prefabs=len(panels), nodes=645, monoBehaviours=script_count,
                    serializedObjects=len(objects), referencedObjects=len(references), componentSchemas=len(descriptions)),
        operation='Decode original native GameObject/component order, exact transform/RectTransform fields and '
                  'all MonoBehaviour fields with full-byte consumption. Preserve signed Int64 pointers and source object hashes. '
                  'Resolve every selected serialized pointer and verify the actual complete transitive CAB inputs.',
        scope='Source extraction only. The original stele uses TeamPanelHelper -> Player invite page -> NPC tab; '
              'this path does not initialize TeamInviteRobotPanel with a RobotInvitePlayType.',
        limitations=['Windows native prefab/resource conversion and binding remain pending.',
                     'Original team/NPC services, roster, battle formation and actual input/render/save checks remain pending.',
                     'Appearance serialization is retained; no Spine runtime or alternative UI is generated.']))
    if any(sha((ROOT / row['path']).read_bytes()) != row['sha256'] for row in tool_records):
        raise ValueError('A conversion source changed during extraction; rerun against stable sources')
    for path, content in ((OUTPUT, payload), (meta_path, metadata), (RECEIPT, receipt)):
        if write:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(content)
        elif not path.is_file() or path.read_bytes() != content:
            raise ValueError('Original team source/receipt drift: ' + str(path))
    print('Map12 original team source verified: 10 prefabs, 645 nodes, 1105 complete script payloads.', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true')
    build(parser.parse_args().write)
