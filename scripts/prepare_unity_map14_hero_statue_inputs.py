"""Recover the eight original hero-statue entity/display roots for conversion.

Temporary hydrated bundles and converter exports belong to the caller's isolated
validation workspace. All source and hydrated hashes must match the previously
verified full Map14 entity closure; this does not replace runnable prefabs.
"""
import argparse
import json
import os
from pathlib import Path

from official_apk import Apk, ROOT
from map13_native_schemas import NativeSchemas
from map14_entity_native_schemas import install
from prepare_unity_bundle_inputs import collect, read_source, serialized_files
from prepare_unity_map13_water_entity_inputs import hydrate
from prepare_unity_map13_water_entity_export import convert, build as verify_export
import prepare_unity_map13_water_entity_export as export_verification
from prepare_unity_map14_sources import encode, sha, source_path, write


def workspace():
    value = os.environ.get('ZJCS_VALIDATION_DIR')
    if not value or Path(os.environ.get('ZJCS_REPOSITORY_ROOT', '')).resolve() != ROOT.resolve():
        raise ValueError('Set this session validation directory and repository root first')
    path = Path(value).resolve()
    if path.parent != (ROOT / 'output/validation').resolve() or not (path / '.zjcs-validation.json').is_file():
        raise ValueError('Use a repository validation workspace')
    return path / 'hero-statue-native'


def build(run_converter=False, verify_existing=False):
    cache = workspace()
    graph_path = ROOT / 'unity/Assets/Official/World/Map/14/Source/native-entity-object-graph.json'
    original_receipt = ROOT / 'assets/reference/official-map/map14-native-entity-input-source-manifest.json'
    graph = json.loads(graph_path.read_bytes())
    previous = json.loads(original_receipt.read_bytes())
    apk = Apk.current()
    if graph['manifest'] != apk.version or previous['manifest'] != apk.version or graph['mapClassId'] != 14:
        raise ValueError('Original hero-statue source versions differ')
    selected = [root for root in graph['roots'] if any(
        use.get('class_id') in (6570, 6571, 6572, 6573) or
        (use.get('kind') == 'entity_display' and use['selector']['EntityClassId'] in (6570, 6571, 6572, 6573)
         and use['selector']['MapClassId'] == 14) for use in root['uses'])]
    if len(selected) != 8:
        raise ValueError('Expected all four original roots and all four selected Map14 displays')
    objects = {(row['cab'], row['path_id']): row for row in graph['objects']}
    roots = sorted({objects[row['cab'], row['path_id']]['input'] for row in selected})
    assets, missing = collect(apk, roots)
    if missing:
        raise ValueError('Resolve original statue CAB dependencies: ' + str(missing))
    # Every root must still be an actual GameObject at the exact official
    # container address and have the original inventory payload hash.
    for row in selected:
        original = objects[row['cab'], row['path_id']]
        matches = [obj for name, ptr in apk.container(original['input'])
                   if name.casefold() == row['path'].casefold()
                   for obj in [ptr.deref()] if obj.type.name == 'GameObject']
        if len(matches) != 1 or sha(matches[0].get_raw_data()) != original['payload_sha256']:
            raise ValueError('Original statue root identity/payload differs: ' + row['path'])
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    manifest_path = code.parent / 'code-manifest.json'
    manifest = json.loads(manifest_path.read_bytes())
    assemblies = []
    for name, info in manifest['assemblies'].items():
        path = code / 'managed/decoded' / name
        if sha(path.read_bytes()) != info['decoded_sha256']:
            raise ValueError('Original statue metadata assembly differs: ' + name)
        assemblies.append(dict(path=source_path(path), sha256=sha(path.read_bytes())))
    schemas = NativeSchemas(code)
    schema_proof = install(schemas, code)
    known = {row['name']: row for row in previous['inputs']}
    inputs, components, changes, unions = [], [], [], []
    count = 0
    for name in sorted(apk.loaded):
        raw, record = read_source(apk, name)
        if name not in known:
            raise ValueError('Statue closure is outside the verified full entity closure: ' + name)
        for field in ('raw_sha256', 'decoded_sha256'):
            if record[field] != known[name][field]:
                raise ValueError('Original verified statue input changed: ' + name + '/' + field)
        if name.startswith('assets/bin/Data/'):
            converted, fields, changed, typed = raw, [], [], []
            checked = sum(len(file.objects) for file in serialized_files(apk.loaded[name]))
        else:
            converted, fields, changed, typed, checked = hydrate(apk.loaded[name], schemas, include_source_identity=True)
        if sha(converted) != known[name]['converter_input_sha256']:
            raise ValueError('Rehydrated statue input differs from full verified closure: ' + name)
        record.update(output=Path(name).name, converter_input_sha256=sha(converted),
                      converter_input_bytes=len(converted), checked_objects=checked)
        write(cache / 'inputs' / record['output'], converted)
        inputs.append(record); components.extend(fields); changes.extend(changed); unions.extend(typed); count += checked
        print('Verified statue converter input:', name, len(fields), 'components', flush=True)
    output = cache / 'components.json'
    write(output, encode(dict(manifest=apk.version, mapClassId=14, components=components)))
    tools = [Path(__file__).resolve()] + [ROOT / 'scripts' / name for name in
             ('prepare_unity_map13_water_entity_inputs.py', 'prepare_unity_map13_water_entity_export.py',
              'official_managed_reference_conversion.py', 'map13_native_schemas.py', 'map14_entity_native_schemas.py')]
    receipt = cache / 'input-source-manifest.json'
    write(receipt, encode(dict(manifest=apk.version, mapClassId=14,
        selection=dict(roots=roots, prefabs=selected), inputs=inputs,
        serialized_files=list(assets.values()), assemblies=assemblies,
        tools=[dict(path=source_path(path), sha256=sha(path.read_bytes())) for path in tools],
        outputs=[dict(path=source_path(output), sha256=sha(output.read_bytes()))],
        original_receipt=dict(path=source_path(original_receipt), sha256=sha(original_receipt.read_bytes())),
        source_graph=dict(path=source_path(graph_path), sha256=sha(graph_path.read_bytes())),
        native_schema=schema_proof, converted_objects=changes, managed_reference_unions=unions,
        counts=dict(roots=8, bundles=len(inputs), components=len(components), checked_objects=count),
        operation='Resolve and verify the exact four original hero-statue entity roots and four selected Map14 display GameObjects, close official CAB dependencies, and rehydrate native component schemas/managed-reference registries. Require each raw, decoded and hydrated bundle hash to equal the earlier full Map14 entity closure. No original bundle is modified or shipped.')))
    print('Original hero-statue inputs prepared:', len(inputs), 'bundles;', count, 'objects', flush=True)
    if run_converter:
        convert(cache)
    if run_converter or verify_existing:
        # AssetRipper/.NET creates long paths successfully. Python on Windows
        # still needs the extended prefix for those exported package names.
        # Keep receipt paths repository-relative, with no platform prefix.
        long_cache = Path('\\\\?\\' + str(cache)) if os.name == 'nt' else cache
        previous_source_path = export_verification.source_path
        def normalized_source_path(path):
            return previous_source_path(Path(str(path).removeprefix('\\\\?\\')))
        export_verification.source_path = normalized_source_path
        try:
            verify_export(receipt_path=receipt, component_path=output, roots=roots, cache_root=long_cache,
                          proof_path=long_cache / 'export-source-manifest.json', expected_counts=None,
                          entry_path=Path(__file__).resolve(), map_class_id=14, subject='hero statue')
        finally:
            export_verification.source_path = previous_source_path


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--convert', action='store_true')
    parser.add_argument('--verify-export', action='store_true', help='Revalidate inputs and reuse the existing completed export')
    args = parser.parse_args()
    build(args.convert, args.verify_export)
