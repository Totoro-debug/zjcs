"""Decode complete original grid/status prefab trees, including managed-reference views.

These retained inputs are for native prefab restoration. They are not a runtime
substitute for the original component scripts or their lifecycle bindings.
"""
import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed

DEST = ROOT / 'unity/Assets/Official/Combat/GridItemSources'
RECEIPT = ROOT / 'assets/reference/battle-animation/grid-item-views-source-manifest.json'
ROOTS = {
    3051: 'fightgriditem_3051.bundle',
    3054: 'fightgriditem_3054.bundle',
    13052: 'fightskill_13050.bundle',
    13482: 'fightskill_13480.bundle',
}


def main():
    DEST.mkdir(parents=True, exist_ok=True)
    with Apk.current() as apk:
        if apk.version != '884_156692':
            raise ValueError('Review original grid view inputs for the selected version')
        code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
        code_manifest = code.parent / 'code-manifest.json'
        metadata = json.loads(code_manifest.read_bytes())
        if metadata['manifest'] != apk.version:
            raise ValueError('Grid views and official assemblies have different versions')
        schemas = ScriptSchemas(code_root=code)
        inputs, outputs, sources, assemblies = [], [], set(), {}

        def record(obj):
            fields = schemas.read(obj) if obj.type.name == 'MonoBehaviour' else obj.read_typetree()
            row = dict(type=obj.type.name, source=apk.provenance(obj),
                payload_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(), fields=fields)
            if obj.type.name == 'MonoBehaviour':
                script = obj.read(check_read=False).m_Script.read()
                assembly_name = script.m_AssemblyName.removesuffix('.dll') + '.dll'
                if assembly_name not in assemblies:
                    assembly = code / 'managed/decoded' / assembly_name
                    assembly_identity = identity(assembly)
                    if assembly_identity['sha256'] != metadata['assemblies'][assembly_name]['decoded_sha256']:
                        raise ValueError('Original grid-view assembly identity differs: ' + assembly_name)
                    assemblies[assembly_name] = assembly_identity
                row['script'] = dict(assembly=script.m_AssemblyName,
                    namespace=script.m_Namespace, name=script.m_ClassName)
                original = code / 'client-source' / (script.m_ClassName + '.cs')
                if original.is_file():
                    sources.add(original)
                for reference in fields.get('references', {}).get('RefIds', []):
                    original = code / 'client-source' / (reference['type']['class'] + '.cs')
                    if original.is_file():
                        sources.add(original)
            return row

        for key, suffix in ROOTS.items():
            bundle, = [name for name in apk.index if name.endswith(suffix)]
            roots = [obj for obj in apk.objects(bundle) if obj.type.name == 'GameObject'
                and obj.read(check_read=False).m_Name == str(key)]
            root, = roots
            nodes, seen = [], set()

            def visit(obj):
                if obj.path_id in seen:
                    raise ValueError('Original prefab hierarchy is cyclic')
                seen.add(obj.path_id)
                go = obj.read(check_read=False)
                components = [component.component.deref() for component in go.m_Component]
                transform, = [component for component in components
                    if component.type.name in ('Transform', 'RectTransform')]
                nodes.append(dict(game_object=record(obj), components=[record(component) for component in components]))
                for child in transform.read(check_read=False).m_Children:
                    visit(child.deref().read(check_read=False).m_GameObject.deref())

            visit(root)
            target = DEST / (str(key) + '-source.json')
            write_changed(target, json.dumps(dict(manifest=apk.version, root=key, nodes=nodes),
                ensure_ascii=False, indent=2) + '\n')
            meta(target)
            outputs.extend([target, Path(str(target) + '.meta')])
            inputs.extend([row for node in nodes for row in [node['game_object'], *node['components']]])
            print('Original grid/status prefab:', key, 'nodes:', len(nodes), 'components:',
                sum(len(node['components']) for node in nodes), flush=True)

        meta(DEST, folder=True)
        outputs.append(Path(str(DEST) + '.meta'))
        proof = dict(manifest=apk.version, generator=identity(Path(__file__)),
            decoder=identity(ROOT / 'scripts/official_unity_schema.py'),
            code_manifest=identity(code_manifest), assemblies=list(assemblies.values()),
            original_code=[identity(path) for path in sorted(sources)], inputs=inputs,
            schemas=schemas.records, outputs=[identity(path) for path in outputs],
            operation='APK-first verified bundles; traverse every original GameObject, component and child in order. '
                'Decode MonoBehaviour fields with the selected official assemblies, including ECViewDriver '
                'SerializeReference registries that the native YAML exporter omits. Retain original pointers '
                'and raw object hashes without replacing or filtering components.',
            limits=['Decoded prefab inputs only; native prefab conversion, component migration and production '
                'grid/status lifecycle are separate. These JSON inputs are not visible runtime substitutes.'])
        write_changed(RECEIPT, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')


if __name__ == '__main__':
    main()
