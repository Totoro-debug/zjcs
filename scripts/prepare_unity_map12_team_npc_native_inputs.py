"""Prepare verified original team NPC resources for native Windows conversion.

All temporary bundles, complete field records and converter logs stay in the
calling owned validation workspace. Original APK/cloud payloads remain intact.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from official_apk import Apk, ROOT
from map13_native_schemas import NativeSchemas
from map12_team_native_schemas import install
from prepare_unity_bundle_inputs import collect, read_source, serialized_files
from prepare_unity_map13_water_entity_inputs import hydrate
from prepare_unity_map12_sources import encode, sha, source_path
from prepare_unity_map12_team_npc_sources import OUTPUT as SOURCE, RECEIPT as SOURCE_RECEIPT
from validation_workspace import validation_directory


RECEIPT = ROOT / 'assets/reference/official-map/map12-team-npc-native-input-source-manifest.json'
TOOLS = ('scripts/prepare_unity_map12_team_npc_native_inputs.py', 'scripts/map12_team_native_schemas.py',
         'scripts/map13_native_schemas.py', 'scripts/map13_entity_native_schemas.py',
         'scripts/map13_ui_native_schemas.py', 'scripts/official_unity_schema.py',
         'scripts/official_managed_reference_conversion.py', 'scripts/prepare_unity_bundle_inputs.py',
         'scripts/prepare_unity_map13_water_entity_inputs.py')


def build(stage_dir, record=False):
    workspace = validation_directory()
    stage_dir = stage_dir.resolve()
    if workspace == ROOT / 'output/validation' or not stage_dir.is_relative_to(workspace):
        raise ValueError('Use a stage inside the calling owned validation workspace')
    if stage_dir.exists():
        raise ValueError('Use a new team conversion stage; do not overwrite another run')
    tools = [dict(path=name, sha256=sha((ROOT / name).read_bytes())) for name in TOOLS]
    source_receipt = json.loads(SOURCE_RECEIPT.read_bytes())
    source_raw = SOURCE.read_bytes()
    source_record, = [r for r in source_receipt['outputs'] if r['path'] == source_path(SOURCE)]
    if sha(source_raw) != source_record['sha256']:
        raise ValueError('Original team NPC hierarchy differs from its source receipt')
    source = json.loads(source_raw)
    apk = Apk.current()
    if apk.version != '871_156598' or source['manifest'] != apk.version:
        raise ValueError('Original team NPC source version differs')
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    code_manifest_path = code.parent / 'code-manifest.json'
    code_raw = code_manifest_path.read_bytes()
    code_manifest = json.loads(code_raw)
    if code_manifest['manifest'] != apk.version or code_manifest['apk_sha256'] != apk.source.apk_sha256:
        raise ValueError('Original team NPC assemblies and APK selection differ')
    assemblies = []
    for name, expected in code_manifest['assemblies'].items():
        path = code / 'managed/decoded' / name
        if sha(path.read_bytes()) != expected['decoded_sha256']:
            raise ValueError('Original team NPC schema assembly differs: ' + name)
        assemblies.append(dict(path=source_path(path), sha256=expected['decoded_sha256']))
    roots = sorted({'defaultpackage_assets_prefabs_' + row['figureModelUI'].rsplit('/', 1)[0].lower().replace('/', '_') + '.bundle' for row in source['models']})
    assets, missing = collect(apk, roots)
    if missing:
        raise ValueError('Resolve original team NPC CAB closure: ' + str(missing))
    schemas = NativeSchemas(code)
    schema_proof = install(schemas, code)
    if schema_proof['metadata']['sha256'] != code_manifest['metadata_sha256']:
        raise ValueError('Original team NPC metadata and managed source differ')
    selected = {row['identity']: row for row in source['objects'] if row['type'] == 'MonoBehaviour'}
    if len(source['models']) != 91 or len(selected) != 605:
        raise ValueError('Original team NPC selected component count changed')
    verified = set()
    for bundle in apk.loaded.values():
        for file in serialized_files(bundle):
            for obj in file.objects.values():
                key = file.name + ':' + str(obj.path_id)
                if key not in selected:
                    continue
                value, _ = schemas.read(obj)
                if value != selected[key]['fields'] or sha(obj.get_raw_data()) != selected[key]['payload_sha256']:
                    raise ValueError('Converter descriptions changed original selected team fields: ' + key)
                verified.add(key)
    if verified != set(selected):
        raise ValueError('Original selected team component closure is incomplete')
    destination = stage_dir / 'inputs'
    destination.mkdir(parents=True)
    inputs, components, changes, unions = [], [], [], []
    checked_objects = 0
    for name, bundle in sorted(apk.loaded.items()):
        original, record_ = read_source(apk, name)
        if name.startswith('assets/bin/Data/'):
            converted, fields, altered, typed = original, [], [], []
            checked = sum(len(file.objects) for file in serialized_files(bundle))
        else:
            converted, fields, altered, typed, checked = hydrate(bundle, schemas, include_source_identity=True)
        for row in fields:
            if row['type'] == 'AkEvent' and row['fields']['Callbacks']:
                raise ValueError('Inspect nonempty original AkEvent callback serialization')
        path = destination / Path(name).name
        path.write_bytes(converted)
        record_.update(output=path.name, converter_input_sha256=sha(converted), converter_input_bytes=len(converted),
                       restored_components=len(fields), changed_payloads=len(altered), checked_objects=checked)
        inputs.append(record_)
        components.extend(fields)
        changes.extend(altered)
        unions.extend(typed)
        checked_objects += checked
        print('Original team NPC conversion input', name, len(fields), 'components;', len(altered), 'registries', flush=True)
    component_raw = encode(dict(manifest=apk.version, mapClassId=12, components=components))
    component_path = stage_dir / 'native-team-npc-components.json'
    component_path.write_bytes(component_raw)
    references = sorted(set(schemas.reference_inputs))
    reference_records = [dict(path=str(p.resolve()), sha256=sha(p.read_bytes())) for p in references]
    receipt = dict(format=1, manifest=apk.version, mapClassId=12, roots=roots,
        source=dict(path=source_path(SOURCE), sha256=sha(source_raw)),
        source_receipt=dict(path=source_path(SOURCE_RECEIPT), sha256=sha(SOURCE_RECEIPT.read_bytes())),
        code_manifest=dict(path=source_path(code_manifest_path), sha256=sha(code_raw)), assemblies=assemblies,
        inputs=inputs, serialized_files=assets, tools=tools, schema_proof=schema_proof,
        reference_inputs=reference_records, converted_objects=changes, managed_reference_unions=unions,
        field_record=dict(file=component_path.name, sha256=sha(component_raw), bytes=len(component_raw),
                          scope='Reproducible converter data in the owned validation session; not a runtime payload.'),
        counts=dict(bundles=len(inputs), selected_components=len(verified), components=len(components),
                    checked_objects=checked_objects, converted_registries=len(changes)),
        operation='Strictly decode every original component in the CAB closure; compare all 605 selected NPC components '
                  'with the original hierarchy extraction. Restore type trees and reversibly expose managed references, '
                  'reopen every generated bundle, compare all fields and preserve every other object payload byte. '
                  'Reference-only metadata carriers and converter inputs never enter Unity Assets or the Player.',
        limitations=['Native prefab/resource export and original managed-reference restoration remain required.',
                     'Runtime script/resource bindings, team services, actual input/render/save verification and Windows publication remain pending.',
                     'Original AkEvent callback arrays in this scope are empty; nonempty callback data is not inferred.'])
    if (SOURCE.read_bytes() != source_raw or code_manifest_path.read_bytes() != code_raw
            or any(sha((ROOT / r['path']).read_bytes()) != r['sha256'] for r in tools)):
        raise ValueError('A team conversion source changed during preparation; rerun on stable inputs')
    raw = encode(receipt)
    (destination / 'source-manifest.json').write_bytes(raw)
    if record:
        RECEIPT.write_bytes(raw)
    print('Original team NPC converter inputs verified:', receipt['counts'], flush=True)
    return receipt


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--stage-dir', type=Path, required=True)
    parser.add_argument('--record', action='store_true', help='Record the verified official input receipt')
    args = parser.parse_args()
    build(args.stage_dir, args.record)
