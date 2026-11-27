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
        name = path.relative_to(ROOT).as_posix() if path.is_relative_to(ROOT) else path.as_posix()
        return dict(path=name, bytes=len(raw), sha256=hashlib.sha256(raw).hexdigest())
    code = json.loads((base/'code-manifest.json').read_bytes())
    inputs = json.loads((base/'native-conversion/source-manifest.json').read_bytes())
    selection_path = base/'native-conversion/changed-inputs-source-manifest.json'
    selection = json.loads(selection_path.read_bytes()) if selection_path.exists() else None
    if any(d['manifest'] != source.version for d in (code, inputs) + ((selection,) if selection else ())):
        raise ValueError('Preparation cache versions differ')
    if selection and selection['input_manifest_sha256'] != file(base/'native-conversion/source-manifest.json')['sha256']:
        raise ValueError('Changed selection uses a different source manifest')
    config = base/'official-data'
    failures = json.loads((config/'table-failures.json').read_bytes())
    if failures:
        raise ValueError('Unresolved official tables: ' + repr(failures))
    result = dict(format=1, manifest=source.version, apk_manifest=source.apk_version,
                  apk_sha256=source.apk_sha256, resource_version=source.profile['resource_version'],
                  generator=file(Path(__file__)), code_manifest=file(base/'code-manifest.json'),
                  decoded_assemblies=[file(base/'official-code/managed/decoded'/name) for name in sorted(code['assemblies'])],
                  table_payload=file(config/'tables.json'),
                  luban_inputs=[file(path) for path in sorted(config.glob('*.bytes'))],
                  fully_decoded_tables=len(json.loads((config/'tables.json').read_bytes())),
                  unresolved_tables=failures,
                  converter_inputs=file(base/'native-conversion/source-manifest.json'),
                  converter_input_files=len(inputs['inputs']),
                  selected_bundle_and_builtin_inputs=len(selection['inputs'] if selection else inputs['inputs']),
                  catalog_entries=sum(len(c['merged']) for c in inputs['catalogs'].values()),
                  retained_apk_only_asset_paths=sum(len({(a['path'], a['address']) for a in c['apk']}-{(a['path'], a['address']) for a in c['current']}) for c in inputs['catalogs'].values()),
                  unavailable_inputs=inputs['unavailable_original_bundles'])
    if selection:
        result.update(changed_selection=file(selection_path),
                      referenced_unchanged_bundles=len(selection['unchanged_bundles']),
                      referenced_changed_bundles=len(selection['changed_bundles']),
                      referenced_without_byte_proof=len(selection.get('unverified_referenced_bundles', [])),
                      unmatched_retained=selection['unmatched_retained'])
    result['supporting_scripts'] = [file(ROOT/'scripts'/name) for name in (
        'prepare_official_sources.py', 'recover_official_native.py', 'prepare_current_aot_metadata.py',
        'prepare_current_resource_inputs.py', 'select_current_resource_changes.py',
        'convert_current_resource_project.py', 'restore_current_empty_atlases.py',
        'convert_current_raw_resources.py', 'recover_current_component_fields.py',
        'current_component_schemas.py', 'official_unity_schema.py', 'official_managed_reference_conversion.py')]
    result['supporting_scripts'].extend(file(ROOT/'scripts'/name) for name in (
        'verify_official_apk_extraction.py', 'archive_official_apk_sources.py',
        'prepare_official_apk_tables.py', 'official_tables.py',
        'prepare_current_asset_inventory.py', 'convert_current_windows_shaders.py', 'convert_current_texture_mips.py',
        'prepare_current_windows_validation.py'))
    apk_code = base/'apk-code-manifest.json'
    if apk_code.exists():
        receipt = json.loads(apk_code.read_bytes())
        if receipt['manifest'] != source.version or receipt['apk_sha256'] != source.apk_sha256:
            raise ValueError('Embedded APK code belongs to a different source')
        result['apk_code'] = dict(source_manifest=file(apk_code),
                                 source_version=receipt['source_manifest'], assemblies=len(receipt['assemblies']),
                                 csharp_files=sum(row['csharp_files'] for row in receipt['projects']))
    for name, key in (('apk-tables-source-manifest.json', 'apk_tables'),
                      ('all-tables-source-manifest.json', 'all_online_tables')):
        path = base/name
        if path.exists():
            receipt = json.loads(path.read_bytes())
            if receipt['manifest'] != source.version or receipt['apk_sha256'] != source.apk_sha256:
                raise ValueError('Table archive belongs to a different source')
            result[key] = dict(source_manifest=file(path), completed=receipt['completed'],
                               tables=receipt['tables'], bundles=len(receipt['bundles']),
                               unresolved=receipt['unresolved'],
                               code_tables_without_payload=receipt['code_tables_without_payload'])
    api = Path(importlib.util.find_spec('TypeTreeGeneratorAPI').origin).parent
    result['type_tree_generator'] = dict(version=importlib.metadata.version('TypeTreeGeneratorAPI'),
                                       binaries=[file(p) for p in sorted(api.rglob('*')) if p.suffix in ('.dll', '.pyd')])
    aot = base/'official-code/aot-metadata/source-manifest.json'
    inventory = base/'native-conversion/all-assets/inventory.json'
    if inventory.exists():
        receipt = json.loads(inventory.read_bytes())
        if receipt['manifest'] != source.version:
            raise ValueError('Object inventory belongs to a different source')
        result['asset_inventory'] = dict(source_manifest=file(inventory), completed=receipt['completed'],
                                        types=receipt['types'], textures=len(receipt['textures']),
                                        shaders=len(receipt['shaders']))
    extraction = base/'apk-extraction-source-manifest.json'
    if extraction.exists():
        receipt = json.loads(extraction.read_bytes())
        if receipt['manifest'] != source.version or receipt['apk_sha256'] != source.apk_sha256:
            raise ValueError('APK extraction receipt has a different source')
        result['apk_extraction'] = dict(source_manifest=file(extraction), completed=receipt['completed'],
                                        files=len(receipt['files']))
    shaders = base/'native-conversion/all-assets/windows-shaders/source-manifest.json'
    if shaders.exists():
        receipt = json.loads(shaders.read_bytes())
        if receipt['manifest'] != source.version:
            raise ValueError('Windows shaders belong to a different source')
        result['windows_shaders'] = dict(source_manifest=file(shaders), completed=receipt['completed'],
                                        shaders=len(receipt['shaders']), engine_bindings=len(receipt['native_engine_bindings']),
                                        programs=sum(len(row['programs']) for row in receipt['shaders']),
                                        unresolved=len(receipt['unresolved']))
    textures = base/'native-conversion/all-assets/windows-textures/source-manifest.json'
    if textures.exists():
        receipt = json.loads(textures.read_bytes())
        if receipt['manifest'] != source.version:
            raise ValueError('Windows textures belong to a different source')
        result['windows_textures'] = dict(source_manifest=file(textures), completed=receipt['completed'],
                                         counts=receipt['counts'], unresolved=len(receipt['unresolved']))
    if aot.exists():
        receipt = json.loads(aot.read_bytes())
        result['aot_metadata'] = dict(source_manifest=file(aot), types=receipt['recovered_types'], images=receipt['metadata_images'])
    native = base/'native-conversion/native-export-complete-source-manifest.json'
    if not native.exists():
        native = base/'native-conversion/native-export-source-manifest.json'
    if native.exists():
        receipt = json.loads(native.read_bytes())
        from collections import Counter
        result['native_export'] = dict(source_manifest=file(native), completed=receipt.get('completed', False),
                                     files=len(receipt['outputs']),
                                     files_by_extension=dict(Counter(Path(row['path']).suffix for row in receipt['outputs'])),
                                     limitations=receipt.get('limitations', []),
                                     conversion_issue_counts=dict(Counter(issue.split(' : ', 1)[0] for issue in receipt.get('conversion_issues', []))),
                                     conversion_errors=dict(Counter(issue for issue in receipt.get('conversion_issues', []) if '[Error]' in issue)))
    raw = base/'native-conversion/raw-resources-source-manifest.json'
    if raw.exists():
        receipt = json.loads(raw.read_bytes())
        result['raw_resources'] = dict(source_manifest=file(raw), completed=receipt['completed'],
                                      packages=len(receipt['packages']), files=len(receipt['outputs']),
                                      pcm_audio=len(receipt['audio']),
                                      audio_with_loop_metadata=sum(bool(row['metadata'].get('loopingInfo')) for row in receipt['audio'].values()),
                                      limitations=receipt['limitations'])
    components = base/'native-conversion/component-fields/source-manifest.json'
    if not components.exists():
        components = base/'native-conversion/component-fields-all/source-manifest.json'
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
