"""Record source identities and cache hashes for the selected preparation."""
import hashlib
import json
from pathlib import Path
import importlib.util
import importlib.metadata

from official_apk import ROOT
from official_source import OfficialSource


def record():
    source = OfficialSource()
    base = ROOT/'.tools/source-packages/current'/source.version
    def file(path):
        raw = path.read_bytes()
        return dict(path=path.relative_to(ROOT).as_posix(), bytes=len(raw), sha256=hashlib.sha256(raw).hexdigest())
    code = json.loads((base/'code-manifest.json').read_bytes())
    inputs = json.loads((base/'native-conversion/source-manifest.json').read_bytes())
    selection = json.loads((base/'native-conversion/changed-inputs-source-manifest.json').read_bytes())
    if any(d['manifest'] != source.version for d in (code, inputs, selection)):
        raise ValueError('Preparation cache versions differ')
    if selection['input_manifest_sha256'] != file(base/'native-conversion/source-manifest.json')['sha256']:
        raise ValueError('Changed selection uses a different source manifest')
    config = base/'official-data'
    failures = json.loads((config/'table-failures.json').read_bytes())
    if failures:
        raise ValueError('Unresolved official tables: ' + repr(failures))
    result = dict(format=1, manifest=source.version, apk_manifest=source.apk_version,
                  apk_sha256=source.apk_sha256, resource_version=source.profile['resource_version'],
                  generator=file(Path(__file__)), code_manifest=file(base/'code-manifest.json'),
                  decoded_assemblies=[file(base/'official-code/managed/decoded'/name) for name in sorted(code['assemblies'])],
                  merged_cache_manifest=file(ROOT/'.tools/source-packages/prepared-code-source-manifest.json'),
                  table_payload=file(config/'tables.json'),
                  luban_inputs=[file(path) for path in sorted(config.glob('*.bytes'))],
                  fully_decoded_tables=len(json.loads((config/'tables.json').read_bytes())),
                  unresolved_tables=failures,
                  converter_inputs=file(base/'native-conversion/source-manifest.json'),
                  converter_input_files=len(inputs['inputs']),
                  changed_selection=file(base/'native-conversion/changed-inputs-source-manifest.json'),
                  referenced_unchanged_bundles=len(selection['unchanged_bundles']),
                  referenced_changed_bundles=len(selection['changed_bundles']),
                  referenced_without_byte_proof=len(selection.get('unverified_referenced_bundles', [])),
                  selected_bundle_and_builtin_inputs=len(selection['inputs']),
                  catalog_entries=sum(len(c['merged']) for c in inputs['catalogs'].values()),
                  retained_apk_only_asset_paths=sum(len({(a['path'], a['address']) for a in c['apk']}-{(a['path'], a['address']) for a in c['current']}) for c in inputs['catalogs'].values()),
                  unmatched_retained=selection['unmatched_retained'])
    result['supporting_scripts'] = [file(ROOT/'scripts'/name) for name in (
        'prepare_official_sources.py', 'recover_official_native.py', 'prepare_current_aot_metadata.py',
        'prepare_current_resource_inputs.py', 'select_current_resource_changes.py',
        'convert_current_resource_project.py', 'restore_current_empty_atlases.py',
        'convert_current_raw_resources.py', 'recover_current_component_fields.py',
        'current_component_schemas.py', 'official_unity_schema.py', 'official_managed_reference_conversion.py')]
    api = Path(importlib.util.find_spec('TypeTreeGeneratorAPI').origin).parent
    result['type_tree_generator'] = dict(version=importlib.metadata.version('TypeTreeGeneratorAPI'),
                                       binaries=[file(p) for p in sorted(api.rglob('*')) if p.suffix in ('.dll', '.pyd')])
    aot = base/'official-code/aot-metadata/source-manifest.json'
    if aot.exists():
        receipt = json.loads(aot.read_bytes())
        result['aot_metadata'] = dict(source_manifest=file(aot), types=receipt['recovered_types'], images=receipt['metadata_images'])
    native = base/'native-conversion/native-export-complete-source-manifest.json'
    if native.exists():
        receipt = json.loads(native.read_bytes())
        from collections import Counter
        result['native_export'] = dict(source_manifest=file(native), completed=receipt.get('completed', False),
                                     files=len(receipt['outputs']),
                                     files_by_extension=dict(Counter(Path(row['path']).suffix for row in receipt['outputs'])),
                                     limitations=receipt.get('limitations', []))
    raw = base/'native-conversion/raw-resources-source-manifest.json'
    if raw.exists():
        receipt = json.loads(raw.read_bytes())
        result['raw_resources'] = dict(source_manifest=file(raw), completed=receipt['completed'],
                                      packages=len(receipt['packages']), files=len(receipt['outputs']),
                                      pcm_audio=len(receipt['audio']),
                                      audio_with_loop_metadata=sum(bool(row['metadata'].get('loopingInfo')) for row in receipt['audio'].values()),
                                      limitations=receipt['limitations'])
    components = base/'native-conversion/component-fields/source-manifest.json'
    if components.exists():
        receipt = json.loads(components.read_bytes())
        result['component_fields'] = dict(source_manifest=file(components), completed=receipt['completed'],
                                         scope=receipt['scope'], objects=len(receipt['objects']),
                                         unresolved=len(receipt['unresolved']),
                                         managed_references=sum(row.get('managed_references', 0) for row in receipt['objects']),
                                         limitations=receipt['limitations'])
    path = ROOT/'assets/reference/official-map/current-resource-preparation-source-manifest.json'
    path.write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', 'utf8')
    return result


if __name__ == '__main__':
    record()
    print('Recorded current preparation source identities')
