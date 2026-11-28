"""Recover current battle HUD prefabs and their ordered common-module assembly.

This prepares verified original inputs for binding. It does not install Android
assets, unbound scripts, or an incomplete HUD into the working Unity project.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import struct

from official_apk import ROOT
from official_manifest import read_asset_manifest
from official_source import OfficialSource
from prepare_current_partner_native import PartnerNativeRecovery, identity as object_id, documents, script_id, pointer
from validation_workspace import validation_directory


MAIN_BUNDLE = 'defaultpackage_assets_prefabs_ui_fight_fightmainpanel_fightmainpanel.bundle'
CATALOG = ROOT / 'unity/Assets/Official/Combat/HUD/panels.json'
RECEIPT = ROOT / 'assets/reference/battle-hud/current-panel-source-manifest.json'


def identity(path):
    with path.open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=digest)


def same_fields(left, right):
    if isinstance(left, dict) and isinstance(right, dict):
        return left.keys() == right.keys() and all(same_fields(left[key], right[key]) for key in left)
    if isinstance(left, (tuple, list)) and isinstance(right, (tuple, list)):
        return len(left) == len(right) and all(same_fields(a, b) for a, b in zip(left, right))
    if isinstance(left, (int, float)) and isinstance(right, (int, float)) and (isinstance(left, float) or isinstance(right, float)):
        return struct.pack('<f', left) == struct.pack('<f', right)
    return left == right


def verify_cache(cache):
    source = OfficialSource()
    if not cache.is_relative_to(source.prepared_path.resolve()):
        raise ValueError('Use a selected current native source cache')
    inputs_path, export_path = cache/'source-manifest.json', cache/'native-export-source-manifest.json'
    inputs, export = json.loads(inputs_path.read_bytes()), json.loads(export_path.read_bytes())
    code_path = source.prepared_path/'code-manifest.json'
    code = json.loads(code_path.read_bytes())
    if (inputs['manifest'] != source.version or export['manifest'] != source.version or not export['completed']
            or export['input_manifest_sha256'] != identity(inputs_path)['sha256']
            or export['code_manifest_sha256'] != identity(code_path)['sha256']):
        raise ValueError('Selected original input/export identity differs')
    records = [identity(code_path)]
    for row in inputs['inputs']:
        path = cache/row['output']
        if identity(path)['sha256'] != row['decoded_sha256']:
            raise ValueError('A selected original bundle changed: ' + row['output'])
    for name, row in code['assemblies'].items():
        path = cache/'inputs/Managed'/name
        if identity(path)['sha256'] != row['decoded_sha256']:
            raise ValueError('A selected application descriptor changed: ' + name)
        records.append(identity(path))
    for row in export['type_dependencies']:
        path = cache/row['output']
        if identity(path)['sha256'] != row['sha256']:
            raise ValueError('A selected AOT/engine descriptor changed: ' + row['output'])
        records.append(identity(path))
    return records


def windows_references(cache):
    """Verify shared conversions against exactly the same original bundle bytes."""
    inventory_path = cache/'all-assets/inventory.json'
    if not inventory_path.is_file():
        from prepare_current_asset_inventory import build
        build(cache)
    inventory = json.loads(inventory_path.read_bytes())
    source_path = cache/'source-manifest.json'
    source = json.loads(source_path.read_bytes())
    if inventory['input_manifest_sha256'] != identity(source_path)['sha256'] or not inventory['completed']:
        raise ValueError('The selected Windows inventory differs from the original source inputs')
    shared = OfficialSource().prepared_path/'native-conversion'
    original_path = shared/'source-manifest.json'
    original = json.loads(original_path.read_bytes())
    by_input = {row['output']:row for row in original['inputs']}
    for row in source['inputs']:
        if row['decoded_sha256'] != by_input[row['output']]['decoded_sha256']:
            raise ValueError('The shared Windows conversion uses another original input: ' + row['output'])
    shared_inventory = shared/'all-assets/inventory.json'
    shared_data = json.loads(shared_inventory.read_bytes())
    if shared_data['input_manifest_sha256'] != identity(original_path)['sha256']:
        raise ValueError('The shared Windows inventory source changed')
    result = dict(inventory=identity(inventory_path), original_inputs=identity(original_path), shared_inventory=identity(shared_inventory), catalogs=[], files=[])
    for kind in ('textures','shaders'):
        base = shared/'all-assets'/('windows-'+kind)
        path = base/'source-manifest.json'
        proof = json.loads(path.read_bytes())
        if proof['manifest'] != source['manifest'] or not proof['completed'] or proof['inventory_sha256'] != identity(shared_inventory)['sha256']:
            raise ValueError('A shared Windows conversion source differs: ' + kind)
        known = {(row['source']['cab'].lower(),row['source']['path_id']):row for row in proof[kind]+proof.get('native_engine_bindings',[])}
        for row in inventory[kind]:
            native = known[(row['cab'].lower(),row['path_id'])]
            if native['source']['object_sha256'] != row['object_sha256']:
                raise ValueError('A converted Windows object differs from the original source: ' + row['name'])
            outputs = native.get('outputs',[]) if kind == 'textures' else ([dict(path=native['path'],sha256=native['sha256']),dict(path=native['path']+'.meta',sha256=native['meta_sha256'])] if 'path' in native else [])
            for output in outputs:
                actual = identity(base/output['path'])
                if actual['sha256'] != output['sha256']:
                    raise ValueError('A converted Windows payload changed: ' + actual['path'])
                result['files'].append(actual)
        result['catalogs'].append(dict(kind=kind, source=identity(path), objects=len(inventory[kind])))
    print('Verified shared current Windows resource inputs and',len(result['files']),'payload files.',flush=True)
    return result


def work_directory(path):
    path = path.resolve()
    marker = json.loads((path / '.zjcs-validation.json').read_text('utf-8-sig'))
    if path.parent != (ROOT / 'output/validation').resolve() or Path(marker['repository']).resolve() != ROOT.resolve():
        raise ValueError('Use an owned battle HUD validation workspace')
    return path


def game_path(go):
    parts = []
    while go is not None:
        parts.insert(0, go.read().m_Name)
        transform = go.read().m_Component[0].component.read()
        go = transform.m_Father.read().m_GameObject.deref() if transform.m_Father.path_id else None
    return '/' + '/'.join(parts)


class CurrentBattleHudSources:
    def __init__(self, cache):
        self.managed_inputs = verify_cache(cache)
        self.recovery = PartnerNativeRecovery(native_cache=cache, export_name='native-export', bundle=MAIN_BUNDLE)
        self.apk = self.recovery.apk
        self.assets = read_asset_manifest(self.apk.source.manifest_bytes('DefaultPackage', self.apk.archive))
        self.by_guid = {row['guid']: row for row in self.assets if row['guid']}
        self.code = self.apk.source.prepared_path / 'official-code/client-source'
        self.interface_sources, self.module_types = {}, {}

    def module_type(self, name):
        """Resolve application interface inheritance without loading a game DLL."""
        if name in self.module_types:
            return self.module_types[name]
        if name == 'IFightPanelModule':
            return True
        self.module_types[name] = False
        if not re.fullmatch(r'[A-Za-z_][A-Za-z0-9_]*', name):
            return False
        path = self.code / (name + '.cs')
        if not path.is_file():
            return False
        text = path.read_text('utf-8-sig')
        match = re.search(r'\b(?:class|interface)\s+' + re.escape(name) + r'\s*(?:<[^{}]+>)?\s*:\s*([^\n{]+)', text)
        if match:
            bases = [entry.strip().split('<', 1)[0] for entry in match[1].split(',')]
            self.module_types[name] = any(self.module_type(base) for base in bases)
        self.interface_sources[path.as_posix()] = identity(path)
        return self.module_types[name]

    def modules(self, root):
        result = []
        for component in root.read().m_Component:
            obj = component.component.deref()
            if obj.type.name != 'MonoBehaviour':
                continue
            script = obj.read(check_read=False).m_Script.read()
            if script.m_Namespace:
                continue  # These common-module interfaces belong to the global application namespace.
            if self.module_type(script.m_ClassName):
                result.append(dict(type=script.m_ClassName, component=object_id(obj)))
        return result

    def root(self, row):
        matches = [ptr.deref() for path, ptr in self.apk.container(row['bundle'])
                   if path.casefold() == row['path'].casefold() and ptr.deref().type.name == 'GameObject']
        result, = matches
        if result.type.name != 'GameObject':
            raise ValueError('A common-module asset is not a prefab: ' + row['path'])
        return result

    def original(self, key):
        cab, path_id = key.rsplit(':', 1)
        file = self.apk.env.get_cab(cab)
        if file is None:
            file = self.apk.env.find_file(cab)
        obj = file.objects[int(path_id)]
        if object_id(obj) != key:
            raise ValueError('A recovered source object changed identity: ' + key)
        return obj

    def restore_layout(self, data):
        restored = []
        for key, target in data['objectMap'].items():
            obj = self.original(key)
            if obj.type.name not in ('RectTransform', 'Transform', 'CanvasGroup'):
                continue
            source = self.recovery.fields(obj)
            native = next(iter(data['documents'][target].values()))
            names = ('m_LocalPosition', 'm_LocalRotation', 'm_LocalScale') if obj.type.name != 'CanvasGroup' else ('m_Alpha', 'm_Interactable', 'm_BlocksRaycasts', 'm_IgnoreParentGroups', 'm_Enabled')
            if obj.type.name == 'RectTransform':
                names += ('m_AnchorMin', 'm_AnchorMax', 'm_AnchoredPosition', 'm_SizeDelta', 'm_Pivot')
            for name in names:
                if name not in source or name not in native:
                    raise ValueError('An original native layout field is absent: ' + key + '/' + name)
                before = native[name]
                native[name] = source[name]
                if not same_fields(before, native[name]):
                    restored.append(dict(original=key, field=name, previous=before, restored=source[name]))
        return restored

    def validate_layout(self, data):
        checked = 0
        for key, target in data['objectMap'].items():
            obj = self.original(key)
            native = next(iter(data['documents'][target].values()))
            if obj.type.name == 'GameObject':
                go = obj.read()
                if go.m_Name != native['m_Name'] or go.m_IsActive != native['m_IsActive'] or go.m_Layer != native['m_Layer']:
                    raise ValueError('The original GameObject fields differ: ' + key)
                expected = [data['objectMap'][object_id(row.component.deref())] for row in go.m_Component]
                if expected != [row['component']['fileID'] for row in native['m_Component']]:
                    raise ValueError('The original component order differs: ' + key)
                checked += 4
            elif obj.type.name in ('RectTransform', 'Transform', 'CanvasGroup'):
                source = self.recovery.fields(obj)
                names = ('m_LocalPosition', 'm_LocalRotation', 'm_LocalScale') if obj.type.name != 'CanvasGroup' else ('m_Alpha', 'm_Interactable', 'm_BlocksRaycasts', 'm_IgnoreParentGroups', 'm_Enabled')
                if obj.type.name == 'RectTransform':
                    names += ('m_AnchorMin', 'm_AnchorMax', 'm_AnchoredPosition', 'm_SizeDelta', 'm_Pivot')
                for name in names:
                    if name not in source or name not in native or not same_fields(source[name], native[name]):
                        raise ValueError('An original native layout field differs: ' + key + '/' + name)
                    checked += 1
                if obj.type.name != 'CanvasGroup':
                    expected = [data['objectMap'][object_id(child.deref())] for child in obj.read().m_Children]
                    if expected != [row['fileID'] for row in native['m_Children']]:
                        raise ValueError('Original transform child order differs: ' + key)
                    checked += 1
            elif obj.type.name == 'MonoBehaviour':
                if obj.read(check_read=False).m_Enabled != native['m_Enabled']:
                    raise ValueError('An original MonoBehaviour enabled flag differs: ' + key)
                checked += 1
        return checked

    def resolve_references(self, work):
        native_pointers, pending = {}, {}
        checked = self.recovery.native_verified

        def native_guid(path):
            meta = Path(str(path) + '.meta')
            for item in (path, meta):
                row = item.relative_to(self.recovery.cache/self.recovery.export_name).as_posix()
                if identity(item)['sha256'] != checked[row]:
                    raise ValueError('An original native dependency changed: ' + str(item))
            return re.search(r'(?m)^guid: ([a-f0-9]{32})$', meta.read_text('utf-8-sig'))[1]

        for native, output, data, record in self.recovery.outputs:
            guid = native_guid(native)
            for key, file_id in data['objectMap'].items():
                value = dict(fileID=file_id, guid=guid, type=3)
                if key in native_pointers and native_pointers[key] != value:
                    raise ValueError('One original object belongs to two recovered prefab roots: ' + key)
                native_pointers[key] = value
            pending.update(data['unresolved'])
        assets, asset_paths = [], set()
        native_assets = None
        for key in sorted(set(pending) - native_pointers.keys()):
            obj = self.original(key)
            if obj.type.name != 'MonoBehaviour' or self.recovery.fields(obj)['m_GameObject']['m_PathID']:
                raise ValueError('Recover the missing original prefab/component before binding: ' + key)
            bundle = self.apk.provenance(obj)['bundle']
            entries = [(name, ptr.deref()) for name, ptr in self.apk.container(bundle)]
            paths = {name for name, item in entries if object_id(item) == key}
            path, = paths
            if not path.endswith('.asset'):
                raise ValueError('Resolve the original external asset container: ' + path)
            if path in asset_paths:
                continue
            asset_paths.add(path)
            native = self.recovery.native/path.removeprefix('Assets/')
            guid = native_guid(native)
            docs = documents(native.read_text('utf-8-sig'))
            if len(docs) != 1 or 'MonoBehaviour' not in next(iter(docs.values())):
                raise ValueError('Resolve the original native asset main object: ' + path)
            file_id, fields = next(iter(docs.items())); fields = fields['MonoBehaviour']
            members = {object_id(item): item for name, item in entries if name == path and item.type.name == 'MonoBehaviour'}
            roots = [item for item in members.values() if item.read(check_read=False).m_Name == fields['m_Name']
                     and script_id(item.read(check_read=False).m_Script.read()) == fields['m_Script']['fileID']]
            root, = roots
            native_pointers[object_id(root)] = dict(fileID=file_id, guid=guid, type=2)

            def learn(source, native_value, owner):
                if isinstance(source, dict) and 'm_FileID' in source and source.get('m_PathID'):
                    if isinstance(native_value, dict) and native_value.get('guid') and native_value.get('fileID'):
                        linked = pointer(source, owner)
                        actual = object_id(linked)
                        if actual in native_pointers and native_pointers[actual] != native_value:
                            raise ValueError('An original asset pointer has conflicting native identities: ' + actual)
                        native_pointers[actual] = native_value
                elif isinstance(source, dict) and isinstance(native_value, dict):
                    for name in source.keys() & native_value.keys():
                        if name != 'm_Script': learn(source[name], native_value[name], owner)
                elif isinstance(source, list) and isinstance(native_value, list) and len(source) == len(native_value):
                    for a, b in zip(source, native_value): learn(a, b, owner)

            learn(self.recovery.fields(root), fields, root)
            if native_assets is None:
                native_assets = {}
                for relative in checked:
                    if relative.startswith('ExportedProject/Assets/') and relative.endswith('.asset.meta'):
                        metadata = self.recovery.cache/self.recovery.export_name/relative
                        raw = metadata.read_bytes()
                        if hashlib.sha256(raw).hexdigest() != checked[relative]:
                            raise ValueError('An original native asset GUID changed: ' + relative)
                        value = re.search(rb'(?m)^guid: ([a-f0-9]{32})\r?$', raw)[1].decode()
                        if value in native_assets:
                            raise ValueError('Duplicate original native asset GUID: ' + value)
                        native_assets[value] = Path(str(metadata)[:-5])
            for original, member in sorted(members.items()):
                value = native_pointers.get(original)
                if value is None or value['guid'] not in native_assets:
                    raise ValueError('The original asset member has no exact native pointer: ' + original)
                source_path = native_assets[value['guid']]
                native_guid(source_path)
                member_docs = documents(source_path.read_text('utf-8-sig'))
                target = member_docs[value['fileID']]['MonoBehaviour']
                if script_id(member.read(check_read=False).m_Script.read()) != target['m_Script']['fileID']:
                    raise ValueError('Original asset-member script identity differs: ' + original)
                source = self.recovery.fields(member)
                learn(source, target, member)

                def convert(item):
                    if isinstance(item, dict) and 'm_FileID' in item and 'm_PathID' in item:
                        if not item['m_PathID']: return dict(fileID=0)
                        linked = object_id(pointer(item, member))
                        resolved = native_pointers.get(linked)
                        if resolved is None: raise ValueError('Recover an additional original asset dependency: ' + linked)
                        return dict(fileID=resolved['fileID']) if resolved['guid'] == value['guid'] else resolved
                    if isinstance(item, dict): return {name: convert(child) for name, child in item.items()}
                    if isinstance(item, list): return [convert(child) for child in item]
                    return item

                for name, item in source.items():
                    if name != 'm_Script': target[name] = convert(item)
                output = work/'recovered-assets'/(value['guid']+'.json'); output.parent.mkdir(exist_ok=True)
                output.write_bytes((json.dumps(dict(documents=member_docs, root=value['fileID'], objectMap={original:value['fileID']}), ensure_ascii=False, indent=2, sort_keys=True)+'\n').encode())
                assets.append(dict(original=original, source=self.apk.provenance(member), object_sha256=hashlib.sha256(member.get_raw_data()).hexdigest(),
                                   native_source=identity(source_path), native_meta=identity(Path(str(source_path)+'.meta')), output=identity(output), pointer=value))
        resolved_count = 0
        for native, output, data, record in self.recovery.outputs:
            def bind(value):
                nonlocal resolved_count
                if isinstance(value, dict):
                    if set(value) == {'originalCAB', 'originalPathID'}:
                        key = value['originalCAB'].lower()+':'+str(value['originalPathID'])
                        if key not in native_pointers: raise ValueError('An original HUD dependency remains unresolved: ' + key)
                        resolved_count += 1
                        data['external'][key] = native_pointers[key]
                        return native_pointers[key]
                    return {name: bind(child) for name, child in value.items()}
                if isinstance(value, list): return [bind(child) for child in value]
                return value
            data['documents'] = bind(data['documents'])
            for key in list(data['unresolved']):
                if key not in native_pointers: raise ValueError('An original HUD dependency remains unresolved: ' + key)
                data['external'][key] = native_pointers[key]; data['unresolved'].pop(key)
            record['unresolved'] = 0
            record['pendingBindings'] = {}
        return assets, resolved_count

    def selection(self):
        previous = json.loads(CATALOG.read_bytes())
        roots, uses = {}, {}
        for key, panel in previous['prefabs'].items():
            source = panel['source']
            matches = [row for row in self.assets if row['bundle'] == source['bundle'] and row['path'].endswith(source['prefab'])]
            row, = matches
            obj = self.root(row)
            roots[object_id(obj)] = (row, obj)
            uses.setdefault(object_id(obj), []).append(dict(kind='existing_hud', key=key))
        main, = [obj for row, obj in roots.values() if row['bundle'] == MAIN_BUNDLE and obj.read().m_Name == 'FightMainPanel']
        component, = [ptr.component.deref() for ptr in main.read().m_Component
                      if ptr.component.deref().type.name == 'MonoBehaviour'
                      and ptr.component.deref().read(check_read=False).m_Script.read().m_ClassName == 'FightMainPanel']
        fields = self.recovery.fields(component)
        assembly, registered = [], []
        for order, proxy in enumerate(fields['mCommonModuleSlotProxies']):
            slot = pointer(proxy['mSlot'], component) if proxy['mSlot']['m_PathID'] else None
            guid = proxy['mAssetGuidProxy']['mAssetGuid']
            record = dict(order=order, asset_guid=guid, slot=object_id(slot) if slot is not None else None,
                          slot_path=game_path(slot.read().m_GameObject.deref()) if slot is not None else None)
            if slot is None:
                record['operation'] = 'skip_missing_slot'
                assembly.append(record)
                continue
            slot_go = slot.read().m_GameObject.deref()
            if guid:
                row = self.by_guid[guid]
                prefab = self.root(row)
                roots[object_id(prefab)] = (row, prefab)
                uses.setdefault(object_id(prefab), []).append(dict(kind='common_module', order=order))
                prefab_modules = self.modules(prefab)
                if not prefab_modules:
                    raise ValueError('The original common-module prefab has no module: ' + row['path'])
                module_type = prefab_modules[0]['type']
                # FightMainPanel.GetCommonModuleFromSlot checks the slot itself,
                # then direct children, using the first component implementing
                # IFightPanelModule and exact runtime type equality.
                existing = None
                for candidate in [slot_go, *(child.read().m_GameObject.deref() for child in slot.read().m_Children)]:
                    candidate_modules = self.modules(candidate)
                    if candidate_modules and candidate_modules[0]['type'] == module_type:
                        existing = (candidate, candidate_modules[0])
                        break
                selected = existing[1] if existing else prefab_modules[0]
                record.update(asset=row, prefab=object_id(prefab), module_type=module_type,
                              operation='reuse_existing' if existing else 'instantiate',
                              component=selected['component'], existing_game_object=object_id(existing[0]) if existing else None)
                if selected['component'] not in registered:
                    registered.append(selected['component'])
            else:
                found = self.modules(slot_go)
                record.update(operation='register_slot_components', modules=found)
                for module in found:
                    if module['component'] not in registered:
                        registered.append(module['component'])
            assembly.append(record)
        return roots, uses, assembly, registered, dict(source=self.apk.provenance(component), object_sha256=hashlib.sha256(component.get_raw_data()).hexdigest())

    def recover(self, work):
        roots, uses, assembly, registered, main_component = self.selection()
        names = set()
        for key, (row, obj) in roots.items():
            name = obj.read().m_Name
            if name in names:
                raise ValueError('Give ambiguous prefab names distinct intermediate identities: ' + name)
            names.add(name)
            self.recovery.recover(name, work / 'recovered', bundle=row['bundle'], native_relative=row['path'].removeprefix('Assets/'))
        self.recovery.finalize()
        assets, resolved_references = self.resolve_references(work)
        proof = dict(manifest=self.apk.version, apk_sha256=self.apk.source.apk_sha256,
                     selection_basis=identity(CATALOG), input_manifest=identity(self.recovery.cache/'source-manifest.json'),
                     native_export=identity(self.recovery.cache/'native-export-source-manifest.json'),
                     main_component=main_component, assembly=assembly, registered_components=registered,
                     managed_inputs=self.managed_inputs,
                     recovered_assets=assets, resolved_references=resolved_references,
                     interface_sources=list(self.interface_sources.values()), prefabs=[], outputs=[],
                     generator=identity(Path(__file__).resolve()),
                     recovery=identity(ROOT/'scripts/prepare_current_partner_native.py'),
                     decoders=[identity(ROOT/'scripts'/name) for name in ('current_component_schemas.py', 'official_unity_schema.py', 'official_metadata.py', 'official_manifest.py')],
                     scope='Original current HUD hierarchies, ordered components, recovered fields and common-module load/reuse plan. Runtime binding, resource conversion and replacement remain separate operations.')
        for native, output, data, record in self.recovery.outputs:
            key = record['originalRoot']
            row, obj = roots[key]
            restored = self.restore_layout(data)
            proof['prefabs'].append(dict(record, asset=row, uses=uses[key], restored_layout=restored, original_layout_fields=self.validate_layout(data)))
            # Canonical object-map key ordering makes extraction reproducible;
            # the original hierarchy, component and registry arrays retain order.
            output.write_bytes((json.dumps(data, ensure_ascii=False, indent=2, sort_keys=True)+'\n').encode('utf-8'))
            record['output_sha256'] = hashlib.sha256(output.read_bytes()).hexdigest()
            proof['prefabs'][-1]['output_sha256'] = record['output_sha256']
            proof['outputs'].append(identity(output))
            # Source recovery must preserve every object and component in order;
            # unresolved external identities remain explicit for the binding step.
            if len(data['objectMap']) != record['mapped_objects'] or len(data['scripts']) != record['recovered_components']:
                raise ValueError('Recovered source object coverage differs')
        (work/'current-hud-source-manifest.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
        print('Recovered current battle HUD:', len(roots), 'prefabs;', len(assembly), 'ordered slots;', len(registered), 'registered modules.', flush=True)
        return proof


def verify_proof(proof):
    if proof['manifest'] != OfficialSource().version:
        raise ValueError('The recovered battle HUD source differs')
    assets = [item for row in proof['recovered_assets'] for item in (row['native_source'], row['native_meta'], row['output'])]
    for row in [proof['selection_basis'], proof['input_manifest'], proof['native_export'], proof['generator'], proof['recovery'], *proof['decoders'], *proof['managed_inputs'], *proof['interface_sources'], *proof['outputs'], *assets]:
        if identity(ROOT/row['path']) != row:
            raise ValueError('A recovered battle HUD input or output changed: ' + row['path'])
    if 'windows' in proof:
        windows = proof['windows']
        for row in [windows['inventory'],windows['original_inputs'],windows['shared_inventory'],*[item['source'] for item in windows['catalogs']],*windows['files']]:
            if identity(ROOT/row['path']) != row:
                raise ValueError('A verified Windows source dependency changed: ' + row['path'])
    print('Current battle HUD source recovery verified:', len(proof['prefabs']), 'prefabs.', flush=True)
    return proof


def verify(work):
    work = work_directory(work)
    return verify_proof(json.loads((work/'current-hud-source-manifest.json').read_bytes()))


def record(work):
    proof = verify(work)
    cache = (ROOT/proof['input_manifest']['path']).parent
    destination = cache/'recovered-hud'
    destination.mkdir(exist_ok=True)
    def retain(row, label):
        origin = ROOT/row['path']
        target = destination/(label+'-'+row['sha256']+'.json')
        raw = origin.read_bytes()
        if target.is_file() and target.read_bytes() != raw:
            raise ValueError('Preserve a changed recovered source snapshot: ' + str(target))
        if not target.is_file(): target.write_bytes(raw)
        return identity(target)
    outputs = []
    for row, prefab in zip(proof['outputs'], proof['prefabs']):
        installed = retain(row, prefab['name'])
        outputs.append(installed)
        prefab['output'] = installed['path']
    for row in proof['recovered_assets']:
        row['output'] = retain(row['output'], row['pointer']['guid'])
    proof['outputs'] = outputs
    raw = (json.dumps(proof, ensure_ascii=False, indent=2)+'\n').replace('\n', '\r\n').encode('utf-8')
    if RECEIPT.is_file() and RECEIPT.read_bytes() != raw:
        from conversion_history import preserve
        preserve(RECEIPT)
    RECEIPT.write_bytes(raw)
    print('Recorded verified current HUD source inputs; runtime installation remains a separate step.', flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--work', type=Path)
    parser.add_argument('--cache', type=Path)
    phase = parser.add_mutually_exclusive_group()
    phase.add_argument('--verify', action='store_true')
    phase.add_argument('--verify-recorded', action='store_true')
    phase.add_argument('--record', action='store_true')
    phase.add_argument('--windows', action='store_true', help='Verify and record the existing shared Windows resource conversions')
    args = parser.parse_args()
    if args.verify_recorded:
        verify_proof(json.loads(RECEIPT.read_bytes()))
        return
    work = work_directory(args.work or validation_directory())
    if args.windows:
        verify(work)
        proof_path = work/'current-hud-source-manifest.json'
        proof = json.loads(proof_path.read_bytes())
        proof['windows'] = windows_references((ROOT/proof['input_manifest']['path']).parent)
        proof_path.write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
        verify(work)
    elif args.record:
        record(work)
    elif args.verify:
        verify(work)
    else:
        cache = args.cache or OfficialSource().prepared_path/'battle-hud-native'
        sources = CurrentBattleHudSources(cache.resolve())
        try:
            sources.recover(work)
        finally:
            sources.apk.close()


if __name__ == '__main__':
    main()
