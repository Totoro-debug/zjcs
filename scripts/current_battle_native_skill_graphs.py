"""Convert verified current native skill graphs and recover their typed component bindings."""
import copy
import json
import re
from pathlib import Path

import yaml
from official_apk import ROOT
from official_source import OfficialSource
from battle_native_script_bindings import shared_bindings
from prepare_battle_status_orbit_assets import build
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed


def verified_noise_mask_binding(cache):
    """Share the migrated Noise_Mask_Pro shader when the official object bytes match."""
    proof = ROOT / 'assets/reference/battle-animation/fire-core-current-skill-graphs-source-manifest.json'
    previous = json.loads(proof.read_bytes())
    inventory = json.loads((cache / 'all-assets/inventory.json').read_bytes())
    current, = (row for row in inventory['shaders'] if row['name'] == 'Effect/Noise_Mask_Pro')
    converted, = previous['shaders']
    assert current['object_sha256'] == converted['object_sha256']
    target = 'unity/Assets/Game/Runtime/Presentation/Combat/Shaders/FireCoreSkillGraph_Noise_Mask_Pro.shader'
    assert identity(ROOT / target) in previous['outputs']
    assert identity(ROOT / (target + '.meta')) in previous['outputs']
    source = 'Shaders/Effect/Noise_Mask_Pro.asset'
    source_meta = cache / 'native-export/ExportedProject/Assets' / (source + '.meta')
    guid = re.search(r'(?m)^guid: ([a-f0-9]{32})$', source_meta.read_text('utf-8-sig'))[1]
    return {guid: dict(source=source, target=target)}


def build_current_skill_graphs(*, DEST, DATA, RECEIPT, GROUPS, family, cache_name, generator,
                               external_assets=None):
    source = OfficialSource()
    cache = ROOT / '.tools/source-packages/current' / source.version / cache_name
    legacy_path = ROOT / 'unity/Assets/Game/Resources/Combat/skill-native-graphs.json'
    legacy_receipt = ROOT / 'assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json'
    proof = json.loads(legacy_receipt.read_bytes())
    assert identity(legacy_path) in proof['outputs'], 'Historical native graph catalog changed'
    legacy = json.loads(legacy_path.read_bytes())
    selected, roots = {}, {}
    for group, (id_key, _, wanted) in GROUPS.items():
        selected[group] = [copy.deepcopy(row) for row in legacy[group] if row[id_key] in wanted]
        assert {row[id_key] for row in selected[group]} == set(wanted)
        for row in selected[group]:
            assert row['entity_resource'].startswith('Combat/SkillGraphs/')
            relative = row['entity_resource'].removeprefix('Combat/SkillGraphs/') + '.prefab'
            roots[f'{group}:{row[id_key]}'] = relative
    bindings = shared_bindings()
    exported = cache / 'native-export/ExportedProject/Assets'
    native_guids = {}
    for path in exported.rglob('*.prefab.meta'):
        match = re.search(r'(?m)^guid: ([a-f0-9]{32})$', path.read_text('utf-8-sig'))
        if match:
            native_guids[match[1]] = path.with_suffix('').relative_to(exported).as_posix()
    # Each direct playable binding is also an original root. This records the
    # current shared prefab bundles which the freshly converted entities use,
    # rather than claiming to reuse their historical playable copies.
    for relative in list(roots.values()):
        text = (exported / relative).read_text('utf-8-sig')
        for guid in re.findall(r'guid: ([a-f0-9]{32})', text):
            if guid in native_guids:
                path = native_guids[guid]
                roots['binding:' + path] = path
    missing_examples = {key: value for key, value in bindings['reference_scripts'].items()
                        if not (exported / value['native_asset']).is_file()}
    examples = {}
    if missing_examples:
        for path in sorted(exported.rglob('*.prefab')):
            text = path.read_text('utf-8-sig')
            for key in missing_examples:
                if key not in examples and re.search(r'm_Script: \{fileID: ' + str(key) + r',', text):
                    examples[key] = path.relative_to(exported).as_posix()
        for key, descriptor in missing_examples.items():
            if key in examples:
                descriptor['native_asset'] = examples[key]
            else:
                # The canonical registry includes unrelated example prefabs.
                # A class absent from this complete export cannot be used by
                # the selected graph closure; unknown scripts still fail build.
                del bindings['reference_scripts'][key]
    build(DEST=DEST, DATA=DATA, RECEIPT=RECEIPT, ROOTS={}, graph_roots=roots,
          **bindings, family=family, shader_prefix=family.removesuffix('s') + '_',
          generator=generator, native_manifest=source.version, native_cache=cache,
          native_export_name='native-export', external_assets=external_assets,
          limits=['Each owning skill separately verifies current rules, cultivation, persistence and rendered Windows gameplay.'])

    # Read the freshly converted component fields, including current pointer
    # bindings and display flags. Historical rows provide only their typed shape.
    guid_paths = {}
    for path in DEST.rglob('*.meta'):
        match = re.search(r'(?m)^guid: ([a-f0-9]{32})$', path.read_text('utf-8-sig'))
        if match:
            guid_paths[match[1]] = path.with_suffix('')
    binding_inputs = []
    views_path = ROOT / 'unity/Assets/Game/Resources/Combat/skill-entity-views.json'
    view_proof = ROOT / 'assets/reference/battle-animation/skill-entity-views-source-manifest.json'
    assert identity(views_path) in json.loads(view_proof.read_bytes())['outputs']
    views = {row['skillEntityId']: row for row in json.loads(views_path.read_bytes())['skills']}
    visibility_path = ROOT / 'unity/Assets/Game/Resources/Combat/damage-visibility.json'
    visibility_proof = ROOT / 'assets/reference/battle-hud/damage-visibility-source-manifest.json'
    assert identity(visibility_path)['sha256'] == json.loads(visibility_proof.read_bytes())['output_sha256']
    visibility = {row['damage_entity_id']: row for row in json.loads(visibility_path.read_bytes())['damage']}
    reused_view_fields = []
    catalog = dict(manifest=source.version)
    for group, (id_key, cls, _) in GROUPS.items():
        catalog[group] = []
        for row in selected[group]:
            client_type = cls if cls is not None else row['client_type']
            script = ROOT / 'unity/Assets/Game/Runtime/Official/BattleSkillPlayables' / (client_type + '.cs.meta')
            script_guid = re.search(r'(?m)^guid: ([a-f0-9]{32})$', script.read_text('utf-8-sig'))[1]
            relative = roots[f'{group}:{row[id_key]}']
            path = DEST / relative
            text = path.read_text('utf-8-sig')
            components = [(int(match[1]), yaml.safe_load('MonoBehaviour:\n' + match[2])['MonoBehaviour'])
                          for match in re.finditer(r'(?ms)^--- !u!114 &(-?\d+)\nMonoBehaviour:\n(.*?)(?=^--- !u!|\Z)', text)
                          if re.search(r'm_Script: \{fileID: 11500000, guid: ' + script_guid, match[2])]
            assert len(components) == 1, ('Missing current native component', path, client_type)
            component_id, fields = components[0]
            if group == 'skills':
                actual = dict(skillEntityId=row[id_key], preferredPosToPlayable=bool(fields['PreferredPosToPlayable']),
                              showSkillReplay=bool(fields['ShowSkillReplay']))
                assert actual == views[row[id_key]], 'Current native skill view requires a scoped field migration'
                reused_view_fields.append(actual)
            if group == 'damages':
                actual = dict(damage_entity_id=row[id_key], hide=bool(fields['myHideDamage']),
                              show_zero=bool(fields['myShowZeroDamage']))
                assert actual == visibility[row[id_key]], 'Current native damage visibility requires a scoped field migration'
                reused_view_fields.append(actual)
            row['entity_resource'] = f'Combat/{family}/' + Path(relative).with_suffix('').as_posix()
            if 'entity_prefab_sha256' in row:
                row['entity_prefab_sha256'] = identity(exported / relative)['sha256']
            row['playables'] = []
            for field, pointer in fields.items():
                if not isinstance(pointer, dict) or pointer.get('guid') not in guid_paths:
                    continue
                target = guid_paths[pointer['guid']]
                if target.suffix != '.prefab':
                    continue
                row['playables'].append(dict(field=field, resource=f'Combat/{family}/'
                    + target.relative_to(DEST).with_suffix('').as_posix()))
            if 'component_file_id' in row:
                row['component_file_id'] = component_id
                row['game_object_file_id'] = fields['m_GameObject']['fileID']
                row['component_enabled'] = bool(fields['m_Enabled'])
            if 'flags' in row:
                row['flags'] = {key: bool(fields[key]) for key in row['flags']}
            if 'source_to_me_anchor' in row:
                row['source_to_me_anchor'] = fields['SourceToMeAnchor']
            catalog[group].append(row)
            binding_inputs.append(dict(entity_id=row[id_key], converted=identity(path), component_file_id=component_id))
    write_changed(DATA, json.dumps(catalog, ensure_ascii=False, indent=2) + '\n')
    metadata = DATA.with_name(DATA.stem + '-source-manifest.json')
    write_changed(metadata, json.dumps(dict(manifest=source.version, output_sha256=identity(DATA)['sha256']), indent=2) + '\n')
    meta(metadata)
    current = json.loads(RECEIPT.read_bytes())
    current['outputs'] = [identity(DATA) if row['path'] == DATA.relative_to(ROOT).as_posix() else row for row in current['outputs']]
    current['outputs'].extend([identity(metadata), identity(Path(str(metadata) + '.meta'))])
    current['current_native_bindings'] = binding_inputs
    current['verified_reused_view_fields'] = reused_view_fields
    current['reused_view_sources'] = [identity(path) for path in (views_path, view_proof, visibility_path, visibility_proof)]
    current['binding_registry'] = [identity(ROOT / 'scripts' / name) for name in
        ('current_battle_native_skill_graphs.py', 'battle_native_script_bindings.py', 'prepare_battle_skill_graph_assets.py', 'battle_skill_entity_prefabs.py')]
    current['historical_catalog_shape'] = [identity(legacy_path), identity(legacy_receipt)]
    current['selected_reference_examples'] = examples
    write_changed(RECEIPT, json.dumps(current, ensure_ascii=False, indent=2) + '\n')
