"""Verify each migrated third-rank skill against the selected APK/CDN inputs.

Historical runtime payloads retain their own identities. Reuse is permitted only
after comparing the current item, EC fields, property inputs and native bundles.
"""
import argparse
import copy
import hashlib
import json
import re
import struct
from pathlib import Path
import xxhash

from official_apk import Apk, ROOT, canonical_bundle_name
from official_ec_codec import Formats
from official_map_codec import unpack
from official_source import OfficialSource
from official_tables import Constructors
from prepare_unity_character_catalog import decode_level_props


SKILLS = {
    10017: dict(entities=[10690, 10691, 10692, 10970, 10971, 10972, 10973, 24266],
                code=['FightSkillComponentInfo', 'FightHitFixedComponentInfo', 'FightDamageComponentInfo',
                      'FightSKillMoveCfg', 'FightStatusComponentInfo', 'BattleFormulaHandler']),
    10032: dict(entities=[11170, 11171, 11172, 24180],
                code=['FightSkillComponentInfo', 'FightHitFixedComponentInfo', 'FightDamageComponentInfo',
                      'FightStatusComponentInfo', 'FightStatusShieldPersistentComponentInfo',
                      'BattleFormulaHandler', 'AiPriorityType', 'SkillAiCfg']),
    10039: dict(entities=[10940, 10941, 10015],
                code=['FightSkillComponentInfo', 'FightHitFixedComponentInfo', 'FightDamageComponentInfo',
                      'BattleFormulaHandler', 'AiPriorityType', 'SkillAiCfg']),
    10073: dict(entities=[11730, 11731, 11732, 11733, 11734],
                code=['FightSkillComponentInfo', 'FightHitFixedComponentInfo', 'FightDamageComponentInfo',
                      'FightActionSkillMoveComponentInfo', 'FightSKillMoveCfg', 'BattleFormulaHandler']),
    10079: dict(entities=[11860, 11861, 11862],
                code=['FightSkillComponentInfo', 'FightHitFixedComponentInfo', 'FightDamageComponentInfo',
                      'BattleFormulaHandler']),
}
COMBAT = ROOT / 'unity/Assets/Official/Combat'


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def build(item_id, spec=None, profession_ids=(3, 7, 12, 16)):
    spec = SKILLS[item_id] if spec is None else spec
    rules = json.loads((COMBAT / 'rules.json').read_bytes())
    skill_rules = json.loads((COMBAT / 'skill-rules.json').read_bytes())
    installed = next(row for row in rules['skill'] if row['ClassId'] == item_id)
    passive_pack = None
    if spec.get('runtime_passive_property_pack'):
        from prepare_passive_property_skill_source import verify as verify_passive_properties
        path = ROOT / spec['runtime_passive_property_pack']
        pack = json.loads(path.read_bytes())
        receipt, encoded = verify_passive_properties(item_id, pack['manifest'], include_graph_properties=True)
        assert path.read_bytes() == encoded, 'Passive cultivation payload differs from its official source'
        proof_path = ROOT / f'assets/reference/battle-rules/passive-property-{item_id}-source-manifest.json'
        assert json.loads(proof_path.read_bytes()) == receipt, 'Passive cultivation source receipt changed'
        passive_pack = dict(payload=identity(path), receipt=identity(proof_path), manifest=pack['manifest'])
    entities, locations = {}, {}
    for name in ('skill-rules', 'battle-rules'):
        source = json.loads((COMBAT / (name + '-source-manifest.json')).read_bytes())
        for key, row in source.get('selected', source.get('entities', {}).get('selected', {})).items():
            locations[key] = row['path']
        for key, row in json.loads((COMBAT / (name + '.json')).read_bytes())['entities'].items():
            entities.setdefault(key, {}).update(row)
    from profession_skill_rule_baselines import read_verified_rule_pack, apply_rule_pack_baseline
    rule_packs, baseline_packs, summon_inputs = [], [], []
    table_keys = {
        'entity_prop': ('EntityId',),
        'entity_prop_group_level': ('GroupId', 'SubRank'),
        'summon_monster_fix_prop': ('EntityId',),
        'summon_monster_add_prop': ('ClassId',),
        'summon_rank_additive_factor': ('RankGroup', 'Rank'),
    }
    selected_summons = set(spec.get('summon_inheritance_entities', ()))
    verified_summons = set()
    dependencies = list(spec.get('baseline_rule_packs', ()))
    for relative in dependencies + list(spec.get('runtime_rule_packs', ())):
        path, proof_path, pack, proof = read_verified_rule_pack(ROOT, relative)
        apply_rule_pack_baseline(entities, pack, relative)
        locations.update(proof['entity_locations'])
        for name, rows in pack['tables'].items():
            assert name in table_keys, 'Unsupported skill rule pack table: ' + name
            keys = table_keys[name]
            by_key = {tuple(row[key] for key in keys): row for row in skill_rules['tables'][name]}
            by_key.update({tuple(row[key] for key in keys): row for row in rows})
            skill_rules['tables'][name] = list(by_key.values())
        pack_summons = {row['EntityId'] for row in pack['tables'].get('summon_monster_fix_prop', ())}
        if pack_summons:
            assert relative in dependencies or pack_summons <= selected_summons, 'Undeclared summon inheritance entities'
            from verify_current_summon_properties import verify as verify_summon_properties
            summon_inputs.append(verify_summon_properties(sorted(pack_summons), relative))
            verified_summons.update(pack_summons & selected_summons)
        skill_rules['props'].update(pack['props'])
        selected_packs = baseline_packs if relative in dependencies else rule_packs
        selected_packs.append(dict(payload=identity(path), receipt=identity(proof_path), manifest=pack['manifest']))
    assert verified_summons == selected_summons, 'Missing scoped summon inheritance rule pack'
    definitions = {str(key): copy.deepcopy(entities[str(key)]) for key in spec['entities']}
    defaults = []
    for key, components in definitions.items():
        disperse = components.get('FightDamageComponent', {}).get('DisperseStatus')
        if disperse is not None and 'ExcludeCharacterTypes' not in disperse:
            disperse['ExcludeCharacterTypes'] = []
            defaults.append(key + '/FightDamageComponent/DisperseStatus/ExcludeCharacterTypes')

    with Apk.current() as apk:
        assert all(pack['manifest'] == apk.version for pack in rule_packs), 'Skill rule pack source differs'
        assert passive_pack is None or passive_pack['manifest'] == apk.version, 'Passive cultivation source differs'
        code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
        constructors, formats = Constructors(code), Formats(code)
        table_proofs = []

        def table(name, cls):
            obj = apk.asset('config_luban', '/' + name + '.g.bytes')
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            table_proofs.append(dict(name=name, source=apk.provenance(obj),
                                     payload_sha256=hashlib.sha256(raw).hexdigest()))
            return json.loads(json.dumps(constructors.table(raw, cls)))

        current = next(row for row in table('skill', 'TbSkillInfo') if row['ClassId'] == item_id)
        assert current == installed, ('Current skill item changed', item_id, installed, current)
        owner = next(row for row in table('profession_base', 'TbProfessionBaseInfo')
                     if row['Profession'] in profession_ids and item_id in row['Skills'] + row.get('FightUsedSkills', []))
        assert owner == next(row for row in rules['profession_base'] if row['Profession'] == owner['Profession'])
        mappings = {name: cls for cls, name in re.findall(
            r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
        skin_bindings = []
        if spec.get('base_item'):
            skin_bindings = [row for row in table('skill_skin', mappings['skill_skin'])
                             if row['EffectSkillId'] == item_id]
            assert len(skin_bindings) == 1 and skin_bindings[0]['SkillId'] == spec['base_item']
            assert skin_bindings == [row for row in rules['skill_skin'] if row['EffectSkillId'] == item_id], 'Current skill skin mapping changed'
            assert spec['base_item'] in owner['Skills'], 'Built-in variant has no owned profession base skill'
        properties = [row for row in table('entity_prop', mappings['entity_prop']) if row['EntityId'] in spec['entities']]
        expected_properties = [row for row in skill_rules['tables']['entity_prop'] if row['EntityId'] in spec['entities']]
        property_overrides = spec.get('entity_prop_overrides', {})
        expected_by_id = {row['EntityId']: copy.deepcopy(row) for row in expected_properties}
        current_by_id = {row['EntityId']: row for row in properties}
        baseline_changes = []
        for entity_id_text, overrides in property_overrides.items():
            entity_id = int(entity_id_text)
            if entity_id not in expected_by_id or entity_id not in current_by_id:
                raise AssertionError('Entity property override references an unavailable entity: '
                                     + entity_id_text)
            previous = expected_by_id[entity_id]
            current_row = current_by_id[entity_id]
            baseline_row = next(row for row in expected_properties if row['EntityId'] == entity_id)
            changes = {}
            for field, expected_value in overrides.items():
                if field not in current_row or current_row[field] != expected_value:
                    raise AssertionError('Declared entity property override differs from current official table: '
                                         + f'{entity_id}/{field}')
                if previous[field] != expected_value:
                    changes[field] = dict(previous=previous[field], current=expected_value)
                    previous[field] = expected_value
            if set(current_row) != set(baseline_row):
                raise AssertionError('Current entity property row shape changed: ' + entity_id_text)
            actual_changes = {field for field in current_row
                              if current_row[field] != baseline_row[field]}
            changed_override_fields = {field for field, value in overrides.items()
                                       if baseline_row[field] != value}
            if actual_changes != changed_override_fields:
                raise AssertionError('Current entity property row has undeclared changes: '
                                     + f'{entity_id}: {sorted(actual_changes - changed_override_fields)}')
            if changes:
                baseline_changes.append(dict(EntityId=entity_id, fields=changes))
        expected_properties = [expected_by_id[row['EntityId']] for row in expected_properties]
        assert sorted(properties, key=lambda row: row['EntityId']) == sorted(expected_properties, key=lambda row: row['EntityId']), 'Current entity cultivation properties changed'
        group_ids = {row['GroupLevelPropId'] for row in properties}
        # Status-only secrets obtain cultivation from their EC entities. An empty
        # PassivePropFactors map does not read the item's passive numeric group.
        if passive_pack is None and current['PassivePropFactors']:
            group_ids.add(current['PassiveGroupLevelPropId'])
        groups = [row for row in table('entity_prop_group_level', mappings['entity_prop_group_level']) if row['GroupId'] in group_ids]
        expected_groups = [row for row in skill_rules['tables']['entity_prop_group_level'] if row['GroupId'] in group_ids]
        assert sorted(groups, key=lambda row: (row['GroupId'],row['SubRank'])) == sorted(expected_groups, key=lambda row: (row['GroupId'],row['SubRank'])), 'Current cultivation groups changed'
        prop_ids = {row['LevelPropId'] for row in groups}
        prop_ids.update(row[key] for row in properties for key in ('RankPropId','SubRankPropId'))
        if passive_pack is None and current['PassivePropFactors']:
            prop_ids.update(current[key] for key in ('PassiveRankPropId','PassiveRankFactorId'))
        prop_ids.discard(0)
        verified_props = apk.reader.read('WwiseBnk', 'wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile')
        prop_rows = decode_level_props(verified_props.data, prop_ids, include_zero=bool(spec.get('scope_numeric')))
        if spec.get('scope_numeric'):
            for row in table('level_prop_default_value', mappings['level_prop_default_value']):
                if row['ClassId'] not in prop_ids:
                    continue
                for props_row in prop_rows[str(row['ClassId'])]['rows'].values():
                    for property_id in row['PropTypes']:
                        props_row.setdefault(str(property_id), row['DefaultValue'])
        assert all(skill_rules['props'][key] == value for key,value in prop_rows.items()), 'Current level/rank numeric rows changed'
        numeric = dict(entities=properties, groups=groups, props=prop_rows,
                       source=dict(verified_props.provenance),
                       sha256=hashlib.sha256(verified_props.data).hexdigest())
        language = apk.reader.read('WwiseBnk', 'wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile')
        raw = language.data
        count, = struct.unpack_from('<i', raw)
        start = 4 + count * 12
        keys = {xxhash.xxh64_intdigest(key.encode()): key for key in
                (f'item_{item_id}_name', f'item_{item_id}_func_desc')}
        texts = {}
        for index in range(count):
            text_id, offset = struct.unpack_from('<Qi', raw, 4 + index * 12)
            if text_id in keys:
                size, = struct.unpack_from('<H', raw, start + offset)
                texts[keys[text_id]] = raw[start+offset+2:start+offset+2+size].decode('utf8')
        fidelity = json.loads((ROOT / 'unity/Assets/Official/Skills/fidelity-texts.json').read_bytes())
        assert len(texts) == len(keys) and all(fidelity[key] == value for key,value in texts.items()), 'Current original skill wording changed'
        wording = dict(source=dict(language.provenance), sha256=hashlib.sha256(raw).hexdigest(), texts=texts)
        fragments, groups = [], {}
        for key in definitions:
            groups.setdefault(locations[key], set()).add(key)
        for path, wanted in sorted(groups.items()):
            obj = apk.asset('config_ec', '/' + Path(path).name)
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            try:
                rows, prefix = unpack(raw)[0], False
            except Exception:
                rows, prefix = unpack(raw[2:])[0], True
            matched = {}
            for parent, entity_id, components in rows:
                key = str(entity_id)
                if key not in wanted:
                    continue
                assert key not in matched, ('Duplicate current EC entity', key)
                original = {}
                for component, payload in components:
                    short = component.removeprefix('Common.')
                    if short in definitions[key]:
                        original[short] = formats.component(component, payload[2:] if prefix and payload else payload)
                original = json.loads(json.dumps(original))
                assert original == definitions[key], ('Current EC component changed', key, original, definitions[key])
                matched[key] = dict(parent=parent, components=original)
            assert set(matched) == wanted, ('Missing current EC entities', path, wanted - set(matched))
            fragments.append(dict(path=path, source=apk.provenance(obj), legacy_prefix=prefix,
                                  payload_sha256=hashlib.sha256(raw).hexdigest(), entities=matched))

        # Manifest dependency IDs are opaque. Follow serialized CAB externals;
        # unchanged prefab bytes alone do not prove identical referenced assets.
        graph_path = ROOT / 'assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json'
        graph = json.loads(graph_path.read_bytes())
        historical = OfficialSource(graph['manifest']).manifest('DefaultPackage')['bundles']
        current_bundles = apk.source.manifest('DefaultPackage')['bundles']
        old_by_name = {row['name']: row for row in historical}
        new_by_name = {row['name']: row for row in current_bundles}
        roots = {row['shared_dependency_bundle'] for row in graph['originals']
                 if row['id'].split(':')[1].isdigit() and int(row['id'].split(':')[1]) in spec['entities']}
        additional_conversions = []
        for entry in spec.get('native_receipts', ()):
            relative = entry['path'] if isinstance(entry, dict) else entry
            path = ROOT / relative
            conversion = json.loads(path.read_bytes())
            assert conversion['manifest'] == graph['manifest'], 'Conversion sources differ'
            originals = conversion['originals']
            if isinstance(entry, dict):
                originals = [row for row in originals if row['id'] in entry['ids']]
                assert {row['id'] for row in originals} == set(entry['ids']), 'Missing selected native conversion roots'
            roots.update(row['shared_dependency_bundle'] for row in originals)
            additional_conversions.append(identity(path))
        legacy_roots = set(roots)
        current_roots = set()
        current_conversions, current_native = [], {}
        for relative in spec.get('current_native_receipts', ()):
            path = ROOT / relative
            conversion = json.loads(path.read_bytes())
            assert conversion['manifest'] == apk.version, 'Current native conversion source differs'
            for output in conversion['outputs']:
                assert identity(ROOT / output['path']) == output, ('Current converted native output changed', output['path'])
            export_record = conversion['native_export_receipt']
            export_path = ROOT / export_record['path']
            assert identity(export_path) == export_record, 'Current native exporter receipt changed'
            exported = json.loads(export_path.read_bytes())
            input_path = export_path.parent / 'source-manifest.json'
            assert exported['completed'] and exported['manifest'] == apk.version
            assert hashlib.sha256(input_path.read_bytes()).hexdigest() == exported['input_manifest_sha256']
            inputs = json.loads(input_path.read_bytes())
            assert inputs['manifest'] == apk.version and inputs['apk_sha256'] == apk.source.apk_sha256
            for row in inputs['inputs']:
                if row.get('package') != 'DefaultPackage':
                    continue
                digest = row['raw_sha256']
                assert row['name'] not in current_native or current_native[row['name']] == digest
                current_native[row['name']] = digest
            current_roots.update(row['shared_dependency_bundle'] for row in conversion['originals'])
            current_conversions.append(identity(path))
        roots.update(current_roots)

        def dependencies(loaded):
            bundles, builtin = set(), set()
            for asset in loaded.files.values():
                for external in getattr(asset, 'externals', []):
                    cab = external.path.rsplit('/', 1)[-1].lower()
                    if cab in ('unity_builtin_extra', 'unity default resources'):
                        builtin.add(cab)
                    else:
                        assert cab in apk.cabs, ('Unresolved native CAB dependency', cab)
                        bundles.add(canonical_bundle_name(apk.cabs[cab], apk.index))
            return bundles, builtin

        def closure(selected_roots):
            visited, pending = set(), list(selected_roots)
            while pending:
                name = pending.pop()
                if name in visited:
                    continue
                visited.add(name)
                next_bundles, _ = dependencies(apk.load(name))
                pending.extend(next_bundles - visited)
            return visited

        # Reusing a shared dependency still requires equivalence, even when
        # another newly converted graph uses the current copy of that bundle.
        legacy_bundles = closure(legacy_roots - current_roots)
        fresh_bundles = closure(current_roots)
        assert fresh_bundles <= current_native.keys(), 'Current conversion omitted a native dependency'
        assert roots, 'No original native graph roots'
        pending, visited, native = list(sorted(roots)), set(), []
        shader_bundle_changed = False
        changed_event_bundle = None
        shared_objects = []
        materials = []
        while pending:
            name = pending.pop()
            if name in visited:
                continue
            visited.add(name)
            new = new_by_name[name]
            old = old_by_name.get(name)
            fresh, reused = name in fresh_bundles, name in legacy_bundles
            verified = apk.reader.read('DefaultPackage', name)
            if fresh:
                assert hashlib.sha256(verified.data).hexdigest() == current_native[name], ('Current native conversion input changed', name)
            if reused and name == 'defaultpackage_unityshaders.bundle' and old['hash'] != new['hash']:
                shader_bundle_changed = True
                continue
            if reused and name == 'defaultpackage_assets_wwise_scriptableobjects_event.bundle' and old['hash'] != new['hash']:
                changed_event_bundle = name
                continue
            loaded = apk.load(name)
            if reused and (old['hash'], old['size']) != (new['hash'], new['size']):
                with Apk(graph['manifest']) as prior:
                    previous = prior.load(name)
                    current_objects = {obj.path_id: obj for asset in loaded.files.values()
                                       for obj in getattr(asset, 'objects', {}).values()}
                    for asset in previous.files.values():
                        for obj in getattr(asset, 'objects', {}).values():
                            if obj.type.name == 'AssetBundle':
                                continue  # Container inventory changes when unrelated objects are added.
                            selected = current_objects[obj.path_id]
                            old_raw, new_raw = obj.get_raw_data(), selected.get_raw_data()
                            metadata = []
                            if old_raw != new_raw and obj.type.name == 'Texture2D':
                                old_tree, new_tree = obj.read_typetree(), selected.read_typetree()
                                # Unity's import usage marker is not a sampler,
                                # color-space, texture-format or pixel change.
                                for key in ('m_LightmapFormat',):
                                    if old_tree[key] != new_tree[key]:
                                        metadata.append(dict(field=key, historical=old_tree.pop(key), current=new_tree.pop(key)))
                                assert old_tree == new_tree and obj.read().get_image_data() == selected.read().get_image_data(), ('Current native texture changed', name, obj.path_id)
                            else:
                                assert old_raw == new_raw, ('Current native object changed', name, obj.path_id, obj.type.name)
                            shared_objects.append(dict(bundle=name, path_id=str(obj.path_id), type=obj.type.name,
                                                       historical_sha256=hashlib.sha256(old_raw).hexdigest(),
                                                       current_sha256=hashlib.sha256(new_raw).hexdigest(),
                                                       import_metadata_differences=metadata))
            new_deps, builtin = dependencies(loaded)
            if reused:
                for asset in loaded.files.values():
                    materials.extend(obj for obj in getattr(asset, 'objects', {}).values() if obj.type.name == 'Material')
            native.append(dict(bundle=name, source=dict(verified.provenance),
                               sha256=hashlib.sha256(verified.data).hexdigest(), dependencies=sorted(new_deps),
                               engine_dependencies=sorted(builtin)))
            pending.extend(sorted(new_deps - visited))
        shader_objects = []
        reused_events = None
        if changed_event_bundle:
            name = changed_event_bundle
            verified = apk.reader.read('DefaultPackage', name)
            loaded = apk.load(name)
            current_objects = {obj.path_id: obj for asset in loaded.files.values()
                               for obj in getattr(asset, 'objects', {}).values()}
            with Apk(graph['manifest']) as prior:
                previous = prior.load(name)
                checked = []
                for asset in previous.files.values():
                    for obj in getattr(asset, 'objects', {}).values():
                        if obj.type.name == 'AssetBundle':
                            continue
                        raw = obj.get_raw_data()
                        assert obj.path_id in current_objects and raw == current_objects[obj.path_id].get_raw_data(), ('Current original sound object changed', obj.path_id)
                        checked.append((str(obj.path_id), hashlib.sha256(raw).hexdigest()))
                reused_events = dict(bundle=name, source=dict(verified.provenance),
                                     original_object_count=len(checked),
                                     object_identities_sha256=hashlib.sha256(json.dumps(sorted(checked)).encode()).hexdigest())
        if shader_bundle_changed:
            # A shared shader bundle may change for unrelated materials. Prove
            # each actually referenced shader object, preserving both bundles.
            with Apk(graph['manifest']) as prior:
                previous = prior.load('defaultpackage_unityshaders.bundle')
                previous_objects = {obj.path_id: obj for asset in previous.files.values()
                                    for obj in getattr(asset, 'objects', {}).values()}
                seen_shaders = set()
                for obj in materials:
                    shader = obj.read().m_Shader.deref()
                    if shader.path_id in seen_shaders:
                        continue
                    seen_shaders.add(shader.path_id)
                    old_shader = previous_objects[shader.path_id]
                    raw, old_raw = shader.get_raw_data(), old_shader.get_raw_data()
                    assert raw == old_raw, ('Current referenced shader object changed', shader.path_id)
                    shader_objects.append(dict(path_id=str(shader.path_id), sha256=hashlib.sha256(raw).hexdigest()))
                assert shader_objects, 'Changed shader bundle has no verified referenced shaders'
        inputs = [identity(code / 'common-source/Common' / (name + '.cs')) for name in spec['code']]
        inputs += [identity(code / 'client-source' / (name + '.cs')) for name in
                   ('MyFightSkill', 'MyFightHitFixed', 'MyFightActionSkillMove')]
        inputs += [identity(code / 'client-source' / (name + '.cs'))
                   for name in spec.get('client_code', ())]
        inputs += [identity(code / 'game-framework-source/GameFramework' / (name + '.cs'))
                   for name in spec.get('framework_code', ())]
        reused_client = []
        for name in spec.get('reused_client_code', ()):
            prior = ROOT / '.tools/source-packages/current' / graph['manifest'] / 'official-code/client-source' / (name + '.cs')
            selected = code / 'client-source' / (name + '.cs')
            assert prior.read_bytes() == selected.read_bytes(), ('Current reused client implementation changed', name)
            reused_client.append(dict(historical=identity(prior), current=identity(selected)))
        sound_proof_path = ROOT / 'assets/reference/battle-animation/native-sound-sources-source-manifest.json'
        sound_proof = json.loads(sound_proof_path.read_bytes())
        sound_events = [row for row in sound_proof['events'] if row['name'] == f'Skill_{item_id}']
        sound_banks = []
        prior_banks = {row['name']: row for row in OfficialSource(graph['manifest']).manifest('WwiseBnk')['bundles']}
        current_banks = {row['name']: row for row in apk.source.manifest('WwiseBnk')['bundles']}
        for bank in sorted({name for event in sound_events for name in event['banks']}):
            name = 'wwisebnk_assets_wwisedata_android_' + bank.lower() + '_pck.rawfile'
            old, new = prior_banks[name], current_banks[name]
            assert (old['hash'],old['size']) == (new['hash'],new['size']), ('Current skill sound bank changed',name)
            verified = apk.reader.read('WwiseBnk',name)
            sound_banks.append(dict(name=name,source=dict(verified.provenance),sha256=hashlib.sha256(verified.data).hexdigest()))
        result = dict(manifest=apk.version, item_id=item_id, profession=owner['Profession'],
                      skill_item=current, table_inputs=table_proofs, fragments=fragments,
                      numeric_inputs=numeric,
                      entity_prop_baseline_changes=baseline_changes,
                      wording=wording, sound_events=sound_events, sound_banks=sound_banks,
                      omitted_empty_defaults=defaults, official_code=inputs,
                      native_bundles=sorted(native, key=lambda row: row['bundle']),
                      reused_shader_objects=sorted(shader_objects, key=lambda row: row['path_id']),
                      reused_sound_objects=reused_events,
                      reused_shared_objects=shared_objects,
                      reused_native_conversion=identity(graph_path),
                      formatters=[identity(path) for path in sorted(set(formats.sources.values()))],
                      constructors=[identity(code / 'common-source' / ('Common' if (code / 'common-source/Common' / (name + '.cs')).is_file() else 'Common.LubanGen') / (name + '.cs')) for name in sorted(constructors.sources)],
                      generator=identity(Path(__file__)))
        if additional_conversions:
            result['additional_native_conversions'] = additional_conversions
        if current_conversions:
            result['current_native_conversions'] = current_conversions
        if reused_client:
            result['reused_client_implementations'] = reused_client
        if skin_bindings:
            result['skill_skin_bindings'] = skin_bindings
        if rule_packs:
            result['current_rule_packs'] = rule_packs
        if baseline_packs:
            result['baseline_rule_packs'] = baseline_packs
        if rule_packs or baseline_packs:
            result['rule_pack_reader'] = identity(ROOT / 'scripts/profession_skill_rule_baselines.py')
        if passive_pack is not None:
            result['current_passive_property_pack'] = passive_pack
        if summon_inputs:
            result['current_summon_inheritance'] = summon_inputs
    return result


def compact_receipt(result):
    def digest(value):
        return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(',', ':'),
                                         ensure_ascii=False).encode('utf8')).hexdigest()

    # The comparison above re-decodes and checks these inputs. Their values
    # already live in the runtime payloads and native receipts.
    return {
        **result,
        'numeric_inputs': {
            'source': result['numeric_inputs']['source'],
            'sha256': result['numeric_inputs']['sha256'],
            'verified_rows_sha256': digest({key: result['numeric_inputs'][key]
                                            for key in ('entities', 'groups', 'props')}),
        },
        'reused_shared_objects': {
            'count': len(result['reused_shared_objects']),
            'sha256': digest(result['reused_shared_objects']),
        },
    }


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--item', type=int, required=True)
    parser.add_argument('--write', action='store_true')
    args = parser.parse_args()
    spec_path = ROOT / f'scripts/profession_skill_sources/third/{args.item}.json'
    spec = SKILLS.get(args.item)
    if spec is None:
        if not spec_path.is_file():
            raise FileNotFoundError(f'Third-profession source spec is missing: {spec_path}')
        spec = json.loads(spec_path.read_bytes())
    result = build(args.item, spec)
    if args.item != 10017:
        result = compact_receipt(result)
    target = ROOT / f'assets/reference/battle-rules/third-profession-{args.item}-source-manifest.json'
    if args.write:
        target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8')
    else:
        assert json.loads(target.read_bytes()) == result, 'Third-profession source receipt changed'
    print(f'Current official source verified: {args.item}, {len(result["fragments"])} EC fragments, {len(result["native_bundles"])} native dependency bundles')


if __name__ == '__main__':
    main()
