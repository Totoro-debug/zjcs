"""Recover the selected original home bottom bar and its complete bindings."""
import argparse
import hashlib
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_current_partner_native import documents, identity, pointer
from prepare_home_repair_native import HomeRepairBinding
from prepare_partner_card_native import guid, script_id, sha
from prepare_partner_gift_native import PartnerGiftBinding
from npc_travel_language import language
from official_manifest import read_asset_manifest
from validation_workspace import validation_directory


class HomeBottomBarBinding(HomeRepairBinding):
    def load(self, source_guid):
        result = super().load(source_guid)
        obj = self.objects.get(source_guid)
        if obj is not None and obj.type.name == 'AnimatorOverrideController':
            # Native controller references use SerializedFile's native-asset
            # pointer kind (2), like AnimatorController/AnimationClip.
            result['type'] = 2
        return result

    def original_asset(self, obj):
        if obj.type.name == 'MonoBehaviour':
            script = obj.read(check_read=False).m_Script.read()
            if script.m_ClassName.startswith('TipDotGraph') or script.m_ClassName == 'TipDotNodeGraph':
                return self.tip_address(obj, script.m_ClassName)
        return super().original_asset(obj)

    def tip_address(self, obj, original_type):
        key = identity(obj)
        if key in self.original_refs:
            return self.original_refs[key]
        source = self.recovery.fields(obj)
        is_graph = original_type == 'TipDotNodeGraph'
        name = 'HomeTipDotGraphAddress' if is_graph else 'HomeTipDotNodeAddress'
        target_guid = hashlib.md5(('zjcs:home-ui-tip-address:' + key).encode()).hexdigest()
        result = dict(fileID=11400000, guid=target_guid, type=2)
        self.original_refs[key] = result
        self.script_guids.add(target_guid)
        payload_guid = hashlib.md5(b'zjcs:home-bottom-tip-source-objects').hexdigest()
        self.script_guids.add(payload_guid)
        fields = dict(m_ObjectHideFlags=0, m_Name=obj.read(check_read=False).m_Name)
        fields.update(m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0),
            m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0), m_Enabled=1,
            m_EditorHideFlags=0, m_Script=self.scripts[name], m_EditorClassIdentifier='',
            sourceGraphData=dict(fileID=4900000, guid=payload_guid, type=3), originalIdentity=key)
        if is_graph:
            fields.update(Type=source['Type'], Scope=source['Scope'])
        else:
            fields.update(TipDotNodeId=source['TipDotNodeId'],
                graph=self.tip_address(pointer(source['graph'], obj), 'TipDotNodeGraph'))
        target = Path('TipAddresses') / (sha(key.encode())[:16] + '.asset')
        self.queue.append((target, target_guid, {11400000: dict(MonoBehaviour=fields)}))
        if not hasattr(self, 'tip_source_objects'):
            self.tip_source_objects = {}
        self.collect_tip_source(obj)
        self.proof[key] = dict(source=self.recovery.apk.provenance(obj), type=original_type,
            original_sha256=sha(obj.get_raw_data()), output=target.as_posix(), guid=target_guid,
            usage='Native selector graph/name/node-ID address; complete original objects retained separately, not evaluated here.')
        return result

    def collect_tip_source(self, obj):
        key = identity(obj)
        if key in self.tip_source_objects:
            return
        fields = self.recovery.fields(obj)
        script = obj.read(check_read=False).m_Script.read()
        self.tip_source_objects[key] = dict(type=script.m_ClassName, fields=fields,
            original_sha256=sha(obj.get_raw_data()), source=self.recovery.apk.provenance(obj))
        def visit(value):
            if isinstance(value, dict):
                if 'm_FileID' in value and value.get('m_PathID'):
                    linked = pointer(value, obj)
                    if linked.type.name == 'MonoBehaviour':
                        name = linked.read(check_read=False).m_Script.read().m_ClassName
                        if name.startswith('TipDot'):
                            self.collect_tip_source(linked)
                else:
                    for child in value.values():
                        visit(child)
            elif isinstance(value, list):
                for child in value:
                    visit(child)
        visit(fields)

    def replace(self, value):
        if isinstance(value, dict) and set(value) == {'class', 'ns', 'asm'}:
            existing = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI' / (value['class'] + '.cs')
            if value['class'].startswith('TipDot') and value['ns'] == '' and existing.is_file():
                self.inputs[existing.relative_to(ROOT).as_posix()] = sha(existing.read_bytes())
                return dict(value, ns='Zjcs.Official.ProfessionUI', asm='Zjcs.Official.ProfessionUI')
        if isinstance(value, dict) and value == dict(**{'class': 'StateSpriteAssetProxyConfig', 'ns': '', 'asm': 'Framework'}):
            return dict(value, ns='Zjcs.Official.HomeHudUI', asm='Zjcs.Official.HomeHudUI')
        if isinstance(value, dict) and value.get('mAssetGuid') and 'ContainerSource' in value:
            self.bind_dynamic_sprite(value['mAssetGuid'])
        return super().replace(value)

    def bind_dynamic_sprite(self, asset_guid):
        if not hasattr(self, 'dynamic_sprites_by_guid'):
            self.dynamic_sprites_by_guid = {}
            self.asset_manifest = read_asset_manifest(
                self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        if asset_guid in self.dynamic_sprites_by_guid:
            return
        record, = [row for row in self.asset_manifest if row['guid'] == asset_guid]
        obj, = [pointer.deref() for path, pointer in self.recovery.apk.container(record['bundle'])
            if path.casefold() == record['path'].casefold() and pointer.deref().type.name == 'Sprite']
        source_key, source_id = self.recovery.fields(obj)['m_RenderDataKey']
        render_guid = ''.join(format(source_key['data[' + str(i) + ']'], '08x')[::-1] for i in range(4))
        if render_guid != asset_guid:
            raise ValueError('Dynamic bottom-bar sprite differs from original GUID')
        name = self.name_key(obj.read(check_read=False).m_Name)
        matches = []
        for key in [name] + [name + str(i) for i in range(100)]:
            for candidate in self.by_name.get(key, []):
                if candidate.suffix != '.asset':
                    continue
                docs = documents(candidate.read_text('utf8'))
                if len(docs) == 1 and next(iter(docs.values())).get('Sprite', {}).get('m_RenderDataKey') == {render_guid: source_id}:
                    matches.append(candidate)
        candidate, = matches
        source_guid = guid(Path(str(candidate) + '.meta'))
        previous = self.objects.get(source_guid)
        if previous is not None and identity(previous) != identity(obj):
            raise ValueError('Dynamic bottom-bar sprite native identity collides')
        self.objects[source_guid] = obj
        pointer = self.load(source_guid)
        self.dynamic_sprites_by_guid[asset_guid] = dict(pointer=pointer, manifest_asset=record,
            original=identity(obj), original_sha256=sha(obj.get_raw_data()), source=self.recovery.apk.provenance(obj))

    def controller(self, source_guid, obj):
        try:
            source = self.recovery.fields(obj)
            states = [state['data'] for machine in source['m_Controller']['m_StateMachineArray']
                for state in machine['data']['m_StateConstantArray']]
            if states and any(not state['m_BlendTreeConstantArray'] for state in states):
                from home_bottom_bar_animator_binding import verify_controller
                native = self.native_path(source_guid, obj)
                raw = native.read_bytes()
                meta = Path(str(native) + '.meta')
                key = native.relative_to(self.cache / 'native-export-all').as_posix()
                if sha(raw) != self.checked[key] or sha(meta.read_bytes()) != self.checked[key + '.meta']:
                    raise ValueError('Selected original bottom-bar controller cache changed')
                docs = documents(raw.decode('utf8'))
                root, = [key for key, value in docs.items() if 'AnimatorController' in value]
                visited = verify_controller(self, obj, docs, root)
                for item in (native, meta):
                    self.inputs[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
                target = Path('NativeControllers') / (sha(identity(obj).encode())[:16] + '.controller')
                target_guid = hashlib.md5(('zjcs:home-original:' + identity(obj)).encode()).hexdigest()
                result = dict(fileID=root, guid=target_guid, type=2)
                self.redirects[source_guid] = result
                self.queue.append((target, target_guid, docs))
                self.proof[identity(obj)] = dict(source=self.recovery.apk.provenance(obj),
                    original_sha256=sha(obj.get_raw_data()), output=target.as_posix(), guid=target_guid,
                    verified_runtime_subassets=visited)
                return result
            return super().controller(source_guid, obj)
        except ValueError as error:
            source = self.recovery.fields(obj)
            (self.work / 'bottom-bar-controller-review.json').write_text(
                json.dumps(dict(identity=identity(obj), source=source,
                    native=self.native_path(source_guid, obj).relative_to(ROOT).as_posix()),
                    ensure_ascii=False, indent=2), 'utf8', newline='\n')
            raise ValueError('Original bottom-bar controller ' + source['m_Name'] + ': ' + str(error)) from error

    def register_scripts(self):
        super().register_scripts()
        folder = ROOT / 'unity/Assets/Game/Runtime/Official/HomeHudUI'
        paths = {name: folder / (name + '.cs') for name in ('PublicBottomBar', 'PublicBottomBarCharacterBtn', 'EquipPosListConfig',
                     'PetEvolutionRedDotItem', 'RedDotMultiItem', 'TravelNoteBtn', 'LegionBtn',
                     'SwitchInStageGameBtn', 'LegionEntryControllerWithBossGameEnd', 'LegionMapDataListener')}
        paths['UnityEngine.UI.Ext.TabAnimatorTransition'] = (
            ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/WorldTabAnimatorTransition.cs')
        for name in ('HomeTipDotNodeAddress', 'HomeTipDotGraphAddress'):
            paths[name] = folder / (name + '.cs')
        for full_name, path in paths.items():
            namespace, _, name = full_name.rpartition('.')
            meta = Path(str(path) + '.meta')
            pointer = dict(fileID=11500000, guid=guid(meta), type=3)
            self.scripts[full_name] = pointer
            self.script_sources[full_name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))
            self.class_ids[script_id(namespace, name)] = pointer
            self.script_guids.add(pointer['guid'])

    def friendship_templates(self):
        self.recovery.recover('PublicBottomBar', self.work / 'bottom-bar-native',
            bundle='defaultpackage_assets_prefabs_ui_public.bundle',
            native_relative='Prefabs/UI/Public/PublicBottomBar.prefab')
        self.recovery.finalize()
        native, _, panel, record = self.recovery.outputs[0]
        self.empty_strings = []

        def restore(source, converted, owner, address=''):
            if isinstance(source, dict) and isinstance(converted, dict):
                for key in source.keys() & converted.keys():
                    if converted[key] is None and (source[key] == '' or source[key] == [] or source[key] == {}):
                        converted[key] = source[key]
                        self.empty_strings.append(dict(original=owner, field=address + '/' + key, value=source[key]))
                    else:
                        restore(source[key], converted[key], owner, address + '/' + key)
            elif isinstance(source, list) and isinstance(converted, list) and len(source) == len(converted):
                for index, (left, right) in enumerate(zip(source, converted)):
                    restore(left, right, owner, address + '/' + str(index))

        template_guid = hashlib.md5(b'zjcs:partner-current:PublicBottomBar.prefab').hexdigest()
        self.home_template_pointers = {key: dict(fileID=file_id, guid=template_guid, type=3)
            for key, file_id in panel['objectMap'].items()}
        self.script_guids.add(template_guid)
        for item in (native, Path(str(native) + '.meta')):
            self.inputs[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
        for key, pointer in panel['external'].items():
            self.objects[pointer['guid']] = self.original(key)
        for key, file_id in panel['objectMap'].items():
            owner = self.original(key)
            self.learn(self.recovery.fields(owner), next(iter(panel['documents'][file_id].values())), owner)
        for script in panel['scripts']:
            owner = self.original(script['original'])
            restore(self.recovery.fields(owner), panel['documents'][script['nativeFileID']]['MonoBehaviour'], script['original'])
            if script['type'] == 'UIClickSound':
                # Keep the original event reference serialized, and separately
                # recover its local PCM for the AK.Wwise.Event.Post boundary.
                self.sound(self.recovery.fields(owner)['AkEvent'], owner)
        cache = self.recovery.proof['component_cache']
        self.inputs[cache['path']] = cache['sha256']
        self.template_selection = dict(scope='Complete selected original PublicBottomBar hierarchy and fields.',
            source=record['originalRoot'], mapped_objects=len(panel['objectMap']))
        missing = sorted({'.'.join(filter(None, [row['namespace'], row['type']]))
            for row in panel['scripts']} - self.scripts.keys())
        if missing:
            raise ValueError('Migrate these original bottom-bar scripts before binding: ' + repr(missing))
        return [('PublicBottomBar', panel)]

    def build(self):
        PartnerGiftBinding.build(self)
        path = self.work / 'partner-gift-native-source-manifest.json'
        receipt = json.loads(path.read_bytes())
        folder = self.work / 'gift-assets'
        tip_data = folder / 'home-bottom-tip-source-objects.json'
        tip_data.write_text(json.dumps(dict(manifest=self.recovery.apk.version,
            scope='Complete original dependency payload; condition evaluation is outside the selector-address adapter.',
            objects=self.tip_source_objects), ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        tip_meta = Path(str(tip_data) + '.meta')
        tip_meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:home-bottom-tip-source-objects').hexdigest()
            + '\nTextScriptImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
        for item in (tip_data, tip_meta):
            receipt['outputs'][item.name] = sha(item.read_bytes())
        receipt['tip_address_scope'] = 'Exact original graph name, type, scope and node ID for each native UI selector; original graph runtime is not claimed by this binding.'
        receipt['tip_source_object_count'] = len(self.tip_source_objects)
        used = set()
        for relative in receipt['outputs']:
            item = folder / relative
            if item.suffix != '.meta' and item.read_bytes().startswith(b'%YAML'):
                used.update(re.findall(r'm_Script: \{[^\n}]*guid: ([0-9a-f]{32})', item.read_text('utf8')))
        receipt['script_bindings'] = {name: row for name, row in receipt['script_bindings'].items()
            if row['pointer']['guid'] in used}
        keys = {doc['MonoBehaviour']['myKey'] for doc in
            documents((folder / 'PublicBottomBar.prefab').read_text('utf8')).values()
            if 'MonoBehaviour' in doc and doc['MonoBehaviour'].get('myKey')}
        keys.update(('TravelNote.FunctionLimit', 'Legion.FunctionLimit'))
        texts, sources = language(keys)
        missing = keys - texts.keys()
        if missing - {'{0}'}:
            raise ValueError('Selected bottom-bar language unavailable: ' + repr(missing))
        receipt['inputs'].update(sources)
        old = folder / 'partner-gift-texts.json'
        for item in (old, Path(str(old) + '.meta')):
            receipt['outputs'].pop(item.name)
            item.unlink()
        target = folder / 'home-bottom-bar-texts.json'
        target.write_text(json.dumps(dict(manifest=self.recovery.apk.version,
            texts=[dict(key=key, text=text) for key, text in sorted(texts.items())]),
            ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        meta = Path(str(target) + '.meta')
        meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:home-bottom-bar-texts').hexdigest()
            + '\nTextScriptImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
        for item in (target, meta):
            receipt['outputs'][item.name] = sha(item.read_bytes())
        receipt['generator_sha256'] = sha(Path(__file__).read_bytes())
        receipt['scope'] = 'Native bottom-bar staging; local state binding and runtime verification are required before installation.'
        receipt['missing_language_keys'] = sorted(missing)
        receipt['original_empty_value_restorations'] = self.empty_strings
        receipt['dynamic_sprites'] = self.dynamic_sprites_by_guid
        for name in ('prepare_home_repair_native.py', 'prepare_partner_gift_native.py',
                     'prepare_partner_confirm_dialog.py', 'prepare_partner_input_dialog.py',
                     'home_bottom_bar_animator_binding.py', 'partner_roster_animator_binding.py',
                     'prepare_home_bottom_bar_validation.py', 'fixtures/home_bottom_bar_native_validation.cs'):
            source = ROOT / 'scripts' / name
            receipt['inputs'][source.relative_to(ROOT).as_posix()] = sha(source.read_bytes())
        original = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code'
        source_files = [('client-source', name) for name in ('PublicBottomBar', 'PublicBottomBarCharacterBtn',
            'EquipPosListConfig', 'PetEvolutionRedDotItem', 'PetRedDotItemBase', 'RedDotItemBase', 'RedDotMultiItem',
            'PlayerUIBase', 'TravelNoteBtn', 'LegionBtn', 'SwitchInGameBtnBase', 'SwitchInStageGameBtn',
            'LegionEntryControllerWithBossGameEnd', 'LegionMapDataListener', 'UnityEngine.UI.Ext/TabAnimatorTransition')]
        source_files += [('common-source/Common', name) for name in
            ('Sex', 'EquipPos', 'FunctionType', 'StageType', 'MapWorldActiveableEntityType', 'MapRegionStatus')]
        source_files += [('framework-source', name) for name in
            ('StateSpriteAssetProxyConfig', 'StateSpriteAssetProxyData', 'ResourceAssetProxy')]
        source_files += [('framework-firstpass-source', 'AssetProxyBase')]
        for folder_name, name in source_files:
            source = original / folder_name / (name + '.cs')
            receipt['inputs'][source.relative_to(ROOT).as_posix()] = sha(source.read_bytes())
        runtime = ROOT / 'unity/Assets/Game/Runtime/Official/HomeHudUI'
        receipt['runtime_code'] = {item.relative_to(ROOT).as_posix(): sha(item.read_bytes())
            for item in runtime.iterdir() if item.is_file()}
        receipt.pop('gradient_aot_methods')
        receipt.pop('gradient_observations')
        (self.work / 'home-bottom-bar-native-source-manifest.json').write_text(
            json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        path.unlink()


def install(work):
    from prepare_home_bottom_bar_validation import equivalent, catalog_bytes
    from prepare_partner_input_dialog import metadata
    receipt_path = ROOT / 'assets/reference/home-bottom-bar-native-source-manifest.json'
    destination = ROOT / 'unity/Assets/Official/Home/UI/BottomBar'
    receipt = json.loads((work / receipt_path.name).read_bytes())
    if receipt['generator_sha256'] != sha(Path(__file__).read_bytes()):
        raise ValueError('Rebuild the changed original bottom-bar generator')
    for relative, expected in {**receipt['inputs'], **receipt['runtime_code']}.items():
        if sha((ROOT / relative).read_bytes()) != expected:
            raise ValueError('Original bottom-bar source changed: ' + relative)
    for row in receipt['script_bindings'].values():
        source = ROOT / row['path']
        if sha(source.read_bytes()) != row['sha256'] or sha(Path(str(source)+'.meta').read_bytes()) != row['meta_sha256']:
            raise ValueError('Original bottom-bar script binding changed: ' + row['path'])
    existing = {}
    for meta in (ROOT / 'unity/Assets').rglob('*.meta'):
        source = Path(str(meta)[:-5])
        if not source.is_file():
            continue
        key = guid(meta)
        if key in existing:
            raise ValueError('Existing Unity GUID collision: ' + key)
        existing[key] = source
    plan, installed, reused = [], {}, []
    for relative, expected in receipt['outputs'].items():
        source = work / 'gift-assets' / relative
        if sha(source.read_bytes()) != expected:
            raise ValueError('Staged original bottom-bar resource changed: ' + relative)
        if relative.endswith('.meta'):
            continue
        key = guid(Path(str(source) + '.meta'))
        target = existing.get(key, destination / relative)
        if target.exists():
            if not equivalent(source.read_bytes(), target.read_bytes()):
                raise ValueError('Preserve different existing original resource: ' + str(target))
            reused.append(dict(path=target.relative_to(ROOT).as_posix(), sha256=sha(target.read_bytes()),
                guid=key, staged_sha256=expected))
        else:
            plan.extend(((source.read_bytes(), target), (Path(str(source)+'.meta').read_bytes(), Path(str(target)+'.meta'))))
        if target.is_relative_to(destination):
            installed[target.relative_to(ROOT).as_posix()] = sha(target.read_bytes()) if target.exists() else expected
            installed[Path(str(target)+'.meta').relative_to(ROOT).as_posix()] = (
                sha(Path(str(target)+'.meta').read_bytes()) if target.exists() else receipt['outputs'][relative+'.meta'])
        existing[key] = target
    catalog = ROOT / 'unity/Assets/Game/Resources/Home/OfficialHomeBottomBarAssetCatalog.asset'
    raw = catalog_bytes(receipt)
    if catalog.exists() and catalog.read_bytes() != raw:
        raise ValueError('Preserve different existing bottom-bar catalog')
    plan.append((raw, catalog))
    for raw, target in plan:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(raw)
    for folder in (destination, *[item for item in destination.rglob('*') if item.is_dir()]):
        meta = metadata(folder, 'DefaultImporter', True)
        installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
    meta = Path(str(catalog)+'.meta')
    meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:home-bottom-bar-catalog').hexdigest()
        + '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n', 'utf8', newline='\n')
    for item in (catalog, meta):
        installed[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
    receipt.update(installed_output_sha256=installed, reused=reused,
        installed_root=destination.relative_to(ROOT).as_posix(),
        scope='Complete original native bottom-bar hierarchy and explicit resources; original UI selector identities retained independently of full condition-graph evaluation. Local production home state integration remains required.')
    receipt_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2)+'\n', 'utf8', newline='\n')
    print('Installed original bottom bar:', len(installed), 'files; reused', len(reused), 'native resources', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--install', action='store_true')
    options = parser.parse_args()
    install(validation_directory()) if options.install else HomeBottomBarBinding(validation_directory()).build()
