"""Rebind unchanged jigsaw inputs to the selected resource manifest."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import sys
import zipfile

from official_apk import Apk, ROOT
from official_cloud_cache import CloudCache
from official_source import OfficialSource


PROVENANCE_KEYS = {
    'bundle', 'bundle_hash', 'path_id', 'origin', 'url', 'manifest', 'source_manifest',
    'apk_manifest', 'apk_sha256', 'apk_entry', 'raw_size', 'raw_sha256', 'bundle_size',
    'bundle_sha256', 'resource_version',
}


def encode(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode('utf-8')


def object_index(apk, rows):
    bundles = {}
    index = {}
    for row in rows:
        name = row['bundle']
        if name not in bundles:
            bundles[name] = {obj.path_id: obj for obj in apk.objects(name)}
        obj = bundles[name].get(int(row['path_id']))
        if obj is None:
            index[(name, str(row['path_id']))] = (None, None, False)
            continue
        raw = obj.get_raw_data()
        provenance = apk.provenance(obj)
        provenance['resource_version'] = apk.source.profile['resource_version']
        digest = hashlib.sha256(raw).hexdigest()
        expected = row.get('sha256', row.get('input_sha256'))
        index[(name, str(obj.path_id))] = (digest, provenance, digest == expected)
    return index


def rebind(row, provenance):
    for key in PROVENANCE_KEYS:
        row.pop(key, None)
    row.update(provenance)


def update_nested(value, index, updated):
    if isinstance(value, dict):
        if 'bundle' in value and 'path_id' in value:
            key = (value['bundle'], str(value['path_id']))
            verified = index.get(key)
            if verified and verified[2] and value.get('sha256', verified[0]) == verified[0]:
                rebind(value, verified[1])
                updated.add(key)
        for child in value.values():
            update_nested(child, index, updated)
    elif isinstance(value, list):
        for child in value:
            update_nested(child, index, updated)


def update_object_manifest(path, apk, table_projection, panel_fields, *, write, reconverted):
    document = json.loads(path.read_bytes())
    rows = document.get('inputs', [])
    references = list(rows)

    def collect(value):
        if isinstance(value, dict):
            if 'bundle' in value and 'path_id' in value and 'sha256' in value:
                references.append(value)
            for child in value.values():
                collect(child)
        elif isinstance(value, list):
            for child in value:
                collect(child)

    for key in ('particle_inputs', 'layout_inputs', 'picture_inputs'):
        collect(document.get(key))
    for resource in document.get('resources', {}).values():
        source = resource.get('source')
        if isinstance(source, dict) and 'bundle' in source and 'path_id' in source:
            references.append(dict(source, sha256=resource.get('input_sha256')))
    unique = {(row['bundle'], str(row['path_id'])): row for row in references}
    index = object_index(apk, list(unique.values()))
    index.update(reconverted)
    unchanged_count, changed, projected = 0, [], []
    table_path_ids = {'3644532119292667704', '-6894107096465166770'}
    panel_key = ('defaultpackage_assets_prefabs_ui_activity_nonhuman.bundle',
                 '3375435469937877950')
    for row in rows:
        key = (row['bundle'], str(row['path_id']))
        digest, provenance, same_object = index[key]
        if same_object or key == panel_key or (key[0] == 'defaultpackage_assets_config_luban.bundle'
                                               and key[1] in table_path_ids):
            row['sha256'] = digest
            rebind(row, provenance)
            unchanged_count += 1
            if not same_object:
                projected.append({'bundle': key[0], 'path_id': key[1],
                                  'current_871_sha256': digest})
        else:
            changed.append({'bundle': row['bundle'], 'path_id': row['path_id'],
                            'old_sha256': row['sha256'], 'current_871_sha256': digest,
                            'current_871': provenance,
                            'status': 'missing_in_871' if digest is None else 'changed'})
    nested_updated = set()
    for row in references:
        key = (row['bundle'], str(row['path_id']))
        digest, provenance, same_object = index[key]
        if same_object:
            row['sha256'] = digest
            rebind(row, provenance)
            nested_updated.add(key)
        elif key not in {(changed_row['bundle'], str(changed_row['path_id'])) for changed_row in changed}:
            changed.append({'bundle': key[0], 'path_id': key[1],
                            'old_sha256': row['sha256'], 'current_871_sha256': digest,
                            'current_871': provenance,
                            'status': 'missing_in_871' if digest is None else 'changed'})
    resource_updates, resource_pending = 0, []
    resource_objects = {}
    for resource in document.get('resources', {}).values():
        bundle = resource.get('bundle')
        if bundle not in resource_objects:
            resource_objects[bundle] = {obj.path_id: obj for obj in apk.objects(bundle)}
        obj = resource_objects[bundle].get(int(resource.get('path_id', 0)))
        if obj is None:
            resource_pending.append({'bundle': bundle, 'path_id': resource.get('path_id'),
                                     'object': resource.get('object')})
            continue
        provenance = apk.provenance(obj)
        if provenance['bundle_hash'] != resource.get('bundle_hash'):
            image = getattr(obj.read(), 'image', None)
            if image is None:
                resource_pending.append({'bundle': bundle, 'path_id': resource.get('path_id'),
                                         'object': resource.get('object'),
                                         'old_bundle_hash': resource.get('bundle_hash'),
                                         'current_bundle_hash': provenance['bundle_hash'],
                                         'status': 'bundle_changed_no_sprite_projection'})
                continue
            from io import BytesIO
            png = BytesIO()
            image.save(png, format='PNG')
            output_digest = hashlib.sha256(png.getvalue()).hexdigest()
            if output_digest != resource.get('sha256'):
                resource_pending.append({'bundle': bundle, 'path_id': resource.get('path_id'),
                                         'object': resource.get('object'),
                                         'old_bundle_hash': resource.get('bundle_hash'),
                                         'current_bundle_hash': provenance['bundle_hash'],
                                         'current_output_sha256': output_digest,
                                         'expected_output_sha256': resource.get('sha256'),
                                         'status': 'reconversion_output_changed'})
                continue
            resource['reconversion_output_sha256'] = output_digest
        if getattr(obj.read(), 'm_Name', None) != resource.get('object'):
            raise ValueError(f"Jigsaw resource object name changed: {bundle}:{resource['path_id']}")
        provenance['input_sha256'] = hashlib.sha256(obj.get_raw_data()).hexdigest()
        provenance['resource_version'] = apk.source.profile['resource_version']
        for key in PROVENANCE_KEYS:
            resource.pop(key, None)
        resource.update(provenance)
        resource_updates += 1
    document.setdefault('source_migrations', {})['jigsaw_871_object_recheck'] = {
        'operation': 'Rebind object-identical inputs and record the ParticleSystemRenderer reconverted from 871.',
        'resource_version': apk.source.profile['resource_version'],
        'package_manifest': apk.version,
        'reconciler_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        'inputs_checked': len(rows),
        'all_unique_references_checked': len(unique),
        'inputs_rebound': unchanged_count,
        'nested_sources_rebound': len(nested_updated),
        'resource_sources_rebound': resource_updates,
        'resource_sources_pending': resource_pending,
        'verified_changed_projections': projected,
        'jigsaw_table_projection': table_projection,
        'jigsaw_panel_fields': panel_fields if any(
            row['bundle'] == panel_key[0] and str(row['path_id']) == panel_key[1] for row in rows) else None,
        'pending_output_reconversion': changed,
    }
    if not changed:
        document['manifest'] = apk.version
    if write:
        path.write_bytes(encode(document))
    return {'path': path.relative_to(ROOT).as_posix(), 'checked': len(rows),
            'rebound': unchanged_count, 'nested_rebound': len(nested_updated),
            'resources_rebound': resource_updates, 'resource_pending': resource_pending,
            'pending': changed}


def update_catalog(apk, index, *, write):
    path = ROOT / 'assets/official/jigsaw-game/catalog.json'
    catalog = json.loads(path.read_bytes())
    rebound = 0

    def visit(value):
        nonlocal rebound
        if isinstance(value, dict):
            source = value.get('source')
            if isinstance(source, dict) and 'bundle' in source and 'path_id' in source:
                key = (source['bundle'], str(source['path_id']))
                verified = index.get(key)
                if verified and verified[2]:
                    rebind(source, verified[1])
                    source['input_sha256'] = verified[0]
                    rebound += 1
            for child in value.values():
                visit(child)
        elif isinstance(value, list):
            for child in value:
                visit(child)

    visit(catalog)
    renderer_path = '/NonHumanJigsawPanel/BgPanel1/Content/MiddleGo/GameContent/PanelGo/Content/UIParticle/Particle System'
    node = next((value for value in _walk_dicts(catalog)
                 if value.get('path') == renderer_path), None)
    if node is None:
        raise ValueError('Original jigsaw ParticleSystem output node is missing')
    bundle = 'defaultpackage_assets_prefabs_ui_activity_nonhuman.bundle'
    renderer = next((obj for obj in apk.objects(bundle)
                     if obj.path_id == 5265412201340117950), None)
    if renderer is None or renderer.type.name != 'ParticleSystemRenderer':
        raise ValueError('871 jigsaw ParticleSystemRenderer input is missing')
    fields = renderer.read_typetree()
    game_object = fields['m_GameObject']['m_PathID']
    owner = next(obj for obj in apk.objects(bundle)
                 if obj.path_id == game_object and obj.type.name == 'GameObject')
    if owner.read().m_Name != 'Particle System':
        raise ValueError('871 particle renderer owner changed')
    material_rows = []
    for pointer in renderer.read().m_Materials:
        material = pointer.deref()
        material_rows.append(dict(name=material.read().m_Name,
            source=dict(apk.provenance(material), input_sha256=hashlib.sha256(material.get_raw_data()).hexdigest())))
    node['renderer'] = {
        'source': dict(apk.provenance(renderer), input_sha256=hashlib.sha256(renderer.get_raw_data()).hexdigest()),
        'render_mode': fields['m_RenderMode'], 'mesh_distribution': fields['m_MeshDistribution'],
        'sort_mode': fields['m_SortMode'], 'min_particle_size': fields['m_MinParticleSize'],
        'max_particle_size': fields['m_MaxParticleSize'],
        'camera_velocity_scale': fields['m_CameraVelocityScale'],
        'velocity_scale': fields['m_VelocityScale'], 'length_scale': fields['m_LengthScale'],
        'sorting_fudge': fields['m_SortingFudge'], 'normal_direction': fields['m_NormalDirection'],
        'shadow_bias': fields['m_ShadowBias'], 'render_alignment': fields['m_RenderAlignment'],
        'pivot': fields['m_Pivot'], 'flip': fields['m_Flip'],
        'enable_gpu_instancing': fields['m_EnableGPUInstancing'],
        'use_custom_vertex_streams': fields['m_UseCustomVertexStreams'],
        'vertex_streams': fields['m_VertexStreams'],
        'use_custom_trail_vertex_streams': fields['m_UseCustomTrailVertexStreams'],
        'trail_vertex_streams': fields['m_TrailVertexStreams'], 'materials': material_rows,
    }
    if write:
        path.write_bytes(encode(catalog))
    renderer_digest = hashlib.sha256(renderer.get_raw_data()).hexdigest()
    renderer_provenance = dict(apk.provenance(renderer), input_sha256=renderer_digest)
    return {'path': path.relative_to(ROOT).as_posix(), 'unchanged_sources_rebound': rebound,
            'reconverted_particle_renderer': renderer_provenance,
            'renderer_index_key': (bundle, str(renderer.path_id)),
            'renderer_sha256': renderer_digest,
            'output_sha256': hashlib.sha256(encode(catalog)).hexdigest()}


def _walk_dicts(value):
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from _walk_dicts(child)
    elif isinstance(value, list):
        for child in value:
            yield from _walk_dicts(child)


def update_audio_packages(path, source, *, write):
    document = json.loads(path.read_bytes())
    cloud = CloudCache(source.cloud_path, source.version)
    current = {row['name']: row for row in source.manifest('WwiseBnk')['bundles']}
    with zipfile.ZipFile(source.apk) as apk_archive:
        apk_entries = set(apk_archive.namelist())
    checked, rebound, pending = 0, 0, []
    for name, package in document['packages'].items():
        record = current.get(name)
        if record is None:
            raise ValueError('871 Wwise package is absent: ' + name)
        entry = f"assets/yoo/WwiseBnk/{record['file']}"
        if entry in apk_entries:
            with zipfile.ZipFile(source.apk) as apk_archive:
                raw = apk_archive.read(entry)
            if len(raw) != record['size'] or hashlib.md5(raw).hexdigest() != record['hash']:
                raise ValueError('Packaged 871 Wwise resource failed manifest verification: ' + name)
            provenance = {'origin': 'apk_zip', 'apk_entry': entry, 'manifest': source.version,
                          'apk_manifest': source.apk_version, 'apk_sha256': source.apk_sha256,
                          'raw_size': len(raw), 'raw_sha256': hashlib.sha256(raw).hexdigest()}
        else:
            content = cloud.read('WwiseBnk', record)
            if content is None:
                raise ValueError('871 Wwise resource is absent from APK and verified CDN cache: ' + name)
            raw, provenance = content
        digest = hashlib.sha256(raw).hexdigest()
        checked += 1
        provenance['resource_version'] = source.profile['resource_version']
        if digest == package['sha256']:
            package['source'] = dict(bundle=record['name'], bundle_hash=record['hash'], **provenance)
            rebound += 1
        else:
            pending.append({'package': name, 'old_sha256': package['sha256'],
                            'current_871_sha256': digest, 'current_871': provenance})
    document['manifest'] = source.version if not pending else document['manifest']
    document['source_migrations'] = {
        'operation': 'Verify Wwise package bytes against the selected 871 manifest and retain identical outputs.',
        'resource_version': source.profile['resource_version'],
        'reconciler_sha256': hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        'packages_checked': checked, 'packages_rebound': rebound,
        'pending_output_reconversion': pending,
    }
    if write:
        path.write_bytes(encode(document))
    return {'path': path.relative_to(ROOT).as_posix(), 'checked': checked,
            'rebound': rebound, 'pending': pending}


def jigsaw_panel_fields(apk):
    """Decode serialized JigsawPanel fields with the selected code assembly."""
    sys.path.insert(0, str(ROOT / '.tools/python-packages'))
    from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator

    tools = json.loads((ROOT / 'unity-tools.json').read_bytes())
    editor = Path(tools['editorInstallPath'])
    profile = editor / 'Editor/Data/MonoBleedingEdge/lib/mono/4.7.1-api'
    modules = editor / 'Editor/Data/Managed/UnityEngine'
    assemblies = ROOT / '.tools/source-packages/current/871_156598/official-code/managed/decoded'
    ui_assembly = ROOT / 'unity/Library/ScriptAssemblies/UnityEngine.UI.dll'
    generator = TypeTreeGenerator('2022.3.57f1', generator='AssetsTools')
    seen = set()
    for folder in (profile, assemblies, modules):
        for path in folder.glob('*.dll'):
            if path.name.lower() in seen:
                continue
            seen.add(path.name.lower())
            try:
                generator.load_dll(path.read_bytes())
            except Exception:
                pass
    if ui_assembly.exists() and ui_assembly.name.lower() not in seen:
        generator.load_dll(ui_assembly.read_bytes())

    tree = generator.get_nodes_up('Assembly-CSharp.dll', 'JigsawPanel')
    tree.m_Children = [child for child in tree.m_Children if child.m_Name != 'references']
    bundle = 'defaultpackage_assets_prefabs_ui_activity_nonhuman.bundle'
    panel = next(obj for obj in apk.objects(bundle) if obj.path_id == 3375435469937877950)
    fields = panel.read_typetree(nodes=tree, check_read=False)
    if panel.byte_size != 1028 or set(fields) != {
            'm_GameObject', 'm_Enabled', 'm_Script', 'm_Name', 'mActivityInfoUI',
            'mFinishAnim', 'mFinishAndChangeAnim', 'mSelectSwitcher', 'mListContent',
            'mGameContent', 'mTabGroup', 'mTabItems', 'mCountDownTxt', 'mExitBtn',
            'mRuleKey', 'mRuleBtn', 'mAnimation', 'mBlocker'}:
        raise ValueError('871 JigsawPanel serialized field projection is incomplete')
    if panel.reader.Position != panel.byte_start + panel.byte_size:
        raise ValueError('871 JigsawPanel field projection did not consume its serialized object')
    return fields


def jigsaw_table_projection():
    """Prove changed global tables leave the jigsaw's selected rows unchanged."""
    old_path = ROOT / '.tools/source-packages/official-data/tables.json'
    new_path = ROOT / '.tools/source-packages/current/871_156598/official-data/tables.json'
    old = json.loads(old_path.read_text(encoding='utf-8-sig'))
    new = json.loads(new_path.read_text(encoding='utf-8-sig'))
    stages = json.loads((ROOT / 'scripts/fixtures/jigsaw-reference.json').read_bytes())['rows']
    award_ids = {award_id for stage in stages for award_id in stage['AwardIds']}

    def relevant(tables):
        rewards = {row['Id']: row for row in tables['reward_rule'] if row['Id'] in award_ids}
        if set(rewards) != award_ids:
            raise ValueError('A jigsaw award row is absent from the selected table set')
        item_ids = {item for row in rewards.values() for item in row['IdList']}
        items = {row['ClassId']: row for row in tables['item'] if row['ClassId'] in item_ids}
        if set(items) != item_ids:
            raise ValueError('A jigsaw reward item is absent from the selected table set')
        tips = [row for row in tables['rule_tip'] if row['RuleKey'] != 'ActivityMatch3GameSkill']
        return {'rewards': rewards, 'items': items, 'rule_tips': tips}

    old_projection, new_projection = relevant(old), relevant(new)
    hashes = {name: hashlib.sha256(encode(new_projection[name])).hexdigest()
              for name in new_projection}
    if old_projection != new_projection:
        raise ValueError('871 changes a jigsaw reward or existing rule-tip projection')
    return {'stages': len(stages), 'award_ids': len(award_ids),
            'item_ids': len(new_projection['items']),
            'existing_rule_tips': len(new_projection['rule_tips']),
            'projection_sha256': hashes}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--write', action='store_true', help='update verified source manifests')
    args = parser.parse_args()
    apk = Apk()
    source = apk.source
    manifests = [
        ROOT / 'assets/reference/jigsaw-game/source-manifest.json',
        ROOT / 'assets/reference/jigsaw-game/audio/bindings-source-manifest.json',
    ]
    panel_fields = jigsaw_panel_fields(apk)
    table_projection = jigsaw_table_projection()
    source_index = {}
    for path in manifests[:2]:
        document = json.loads(path.read_bytes())
        references = list(document.get('inputs', []))

        def collect(value):
            if isinstance(value, dict):
                if ('bundle' in value and 'path_id' in value
                        and value.get('sha256', value.get('input_sha256'))):
                    references.append(value)
                for child in value.values():
                    collect(child)
            elif isinstance(value, list):
                for child in value:
                    collect(child)

        for key in ('particle_inputs', 'layout_inputs', 'picture_inputs'):
            collect(document.get(key, {}))
        for resource in document.get('resources', {}).values():
            source_row = resource.get('source')
            if (isinstance(source_row, dict) and source_row.get('bundle')
                    and resource.get('input_sha256')):
                references.append(dict(source_row, sha256=resource['input_sha256']))
        unique = {(row['bundle'], str(row['path_id'])): row for row in references}
        source_index.update(object_index(apk, list(unique.values())))
    catalog = update_catalog(apk, source_index, write=args.write)
    reconverted = {catalog['renderer_index_key']: (
        catalog['renderer_sha256'], catalog['reconverted_particle_renderer'], True)}
    result = [update_object_manifest(path, apk, table_projection, panel_fields,
                                     write=args.write, reconverted=reconverted)
              for path in manifests]
    result.append({'path': catalog['path'], 'checked': len(source_index),
                   'rebound': catalog['unchanged_sources_rebound'],
                   'pending': []})
    if args.write:
        source_document = json.loads(manifests[0].read_bytes())
        source_document['outputs']['res://assets/official/jigsaw-game/catalog.json'] = catalog['output_sha256']
        source_document.setdefault('source_migrations', {})['jigsaw_871_catalog_sources'] = {
            'operation': 'Rebind object-identical UI sources to 871 and reconvert the changed celebration ParticleSystemRenderer from its 871 type tree.',
            'manifest': apk.version, 'catalog_sources_rebound': catalog['unchanged_sources_rebound'],
            'renderer': catalog['reconverted_particle_renderer'],
            'output_sha256': catalog['output_sha256'],
        }
        manifests[0].write_bytes(encode(source_document))
    result.append(update_audio_packages(ROOT / 'assets/reference/jigsaw-game/audio/source-manifest.json',
                                        source, write=args.write))
    summary = [{key: row[key] for key in ('path', 'checked', 'rebound', 'nested_rebound') if key in row}
               | {'pending': [{'bundle': item['bundle'], 'path_id': item['path_id'],
                               'old_sha256': item['old_sha256'],
                               'current_871_sha256': item['current_871_sha256']}
                              for item in row['pending']]}
               for row in result]
    print(json.dumps({'mode': 'write' if args.write else 'check', 'results': summary,
                      'jigsaw_panel_871': panel_fields,
                      'jigsaw_table_projection_871': table_projection},
                     ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
