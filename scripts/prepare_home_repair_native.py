"""Recover the selected official home repair window and its icon prefabs."""
from pathlib import Path
import argparse
import hashlib
import json
import re

from prepare_partner_confirm_dialog import PartnerConfirmBinding
from prepare_partner_gift_native import PartnerGiftBinding
from prepare_partner_input_dialog import metadata
from prepare_partner_card_native import ROOT, guid, script_id, sha
from prepare_current_partner_native import identity, documents
from official_manifest import read_asset_manifest
from official_tables import Constructors
from npc_travel_language import language
from validation_workspace import validation_directory

RECEIPT = ROOT / 'assets/reference/home-repair-native-source-manifest.json'
DESTINATION = ROOT / 'unity/Assets/Official/Home/UI/Repair'
CATALOG = ROOT / 'unity/Assets/Game/Resources/Home/OfficialHomeRepairAssetCatalog.asset'


class HomeRepairBinding(PartnerConfirmBinding):
    def original_asset(self, obj):
        result = getattr(self, 'home_template_pointers', {}).get(identity(obj))
        return result if result is not None else super().original_asset(obj)

    def controller(self, source_guid, obj):
        source = self.recovery.fields(obj)
        constants = source['m_Controller']
        machines = constants['m_StateMachineArray']
        if len(machines) != 1 or machines[0]['data']['m_StateConstantArray']:
            result = super().controller(source_guid, obj)
            old = result['guid']
            new = hashlib.md5(('zjcs:home-original:' + identity(obj)).encode()).hexdigest()
            result['guid'] = new
            self.queue = [(target, new if key == old else key, data) for target, key, data in self.queue]
            self.proof[identity(obj)]['guid'] = new
            return result
        native = self.native_path(source_guid, obj)
        raw = native.read_bytes()
        meta = Path(str(native) + '.meta')
        key = native.relative_to(self.cache / 'native-export-all').as_posix()
        if sha(raw) != self.checked[key] or sha(meta.read_bytes()) != self.checked[key + '.meta']:
            raise ValueError('Empty original controller cache changed')
        docs = documents(raw.decode('utf8'))
        root, = [key for key, value in docs.items() if 'AnimatorController' in value]
        converted = docs[root]['AnimatorController']
        layer, = converted['m_AnimatorLayers']
        machine_id = layer['m_StateMachine']['fileID']
        machine = docs[machine_id]['AnimatorStateMachine']
        original = machines[0]['data']
        original_layer, = constants['m_LayerArray']
        original_layer = original_layer['data']
        names = dict(source['m_TOS'])
        if (len(docs) != 2 or source['m_AnimationClips'] or source['m_StateMachineBehaviours']
            or constants['m_Values']['data']['m_ValueArray'] or converted['m_AnimatorParameters']
            or source['m_Name'] != converted['m_Name'] or original['m_DefaultState'] != 0
            or machine['m_DefaultState'] != dict(fileID=0) or original['m_AnyStateTransitionConstantArray']
            or any(machine[name] for name in ('m_ChildStates', 'm_ChildStateMachines', 'm_AnyStateTransitions',
                'm_EntryTransitions', 'm_StateMachineTransitions', 'm_StateMachineBehaviours'))
            or layer['m_Name'] != names[original_layer['m_Binding']]
            or layer['m_DefaultWeight'] != original_layer['m_DefaultWeight']
            or layer['m_BlendingMode'] != original_layer['(int&)m_LayerBlendingMode']
            or bool(layer['m_IKPass']) != original_layer['m_IKPass']
            or bool(layer['m_SyncedLayerAffectsTiming']) != original_layer['m_SyncedLayerAffectsTiming']):
            raise ValueError('Review empty original controller conversion')
        selectors = original['m_SelectorStateConstantArray']
        expected = [dict(data=dict(m_TransitionConstantArray=[dict(data=dict(m_Destination=destination,
            m_ConditionConstantArray=[]))], m_FullPathID=original_layer['m_Binding'], m_IsEntry=is_entry))
            for destination, is_entry in ((4294967295, True), (30000, False))]
        if selectors != expected:
            raise ValueError('Review empty original controller selectors')
        self.inputs[native.relative_to(ROOT).as_posix()] = sha(raw)
        self.inputs[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
        target = Path('NativeControllers') / (sha(identity(obj).encode())[:16] + '.controller')
        target_guid = hashlib.md5(('zjcs:home-original:' + identity(obj)).encode()).hexdigest()
        result = dict(fileID=root, guid=target_guid, type=2)
        self.redirects[source_guid] = result
        self.queue.append((target, target_guid, docs))
        self.proof[identity(obj)] = dict(source=self.recovery.apk.provenance(obj), original_sha256=sha(obj.get_raw_data()),
            output=target.as_posix(), guid=target_guid,
            verified_runtime_subassets={root: 'empty-controller', machine_id: 'empty-state-machine'})
        return result

    def load(self, source_guid):
        obj = self.objects.get(source_guid)
        if source_guid in self.redirects or obj is None or obj.type.name != 'TextAsset':
            return super().load(source_guid)
        path = self.native_path(source_guid, obj)
        raw = path.read_bytes()
        if raw.startswith(b'%YAML'):
            return super().load(source_guid)
        receipt_key = path.relative_to(self.cache / 'native-export-all').as_posix()
        original = obj.read(check_read=False).m_Script
        if isinstance(original, str):
            original = original.encode('utf8', 'surrogateescape')
        if raw != bytes(original) or sha(raw) != self.checked[receipt_key]:
            raise ValueError('Native TextAsset bytes differ from selected original: ' + str(path))
        meta = Path(str(path) + '.meta')
        if sha(meta.read_bytes()) != self.checked[receipt_key + '.meta'] or guid(meta) != source_guid:
            raise ValueError('Native TextAsset identity differs: ' + str(path))
        self.inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
        self.inputs[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
        target = path.relative_to(self.native)
        target_guid = hashlib.md5(('zjcs:partner-current:' + source_guid).encode()).hexdigest()
        result = dict(fileID=4900000, guid=target_guid, type=3)
        self.redirects[source_guid] = result
        self.queue.append((target, target_guid, raw))
        self.proof[identity(obj)] = dict(source=self.recovery.apk.provenance(obj), type='TextAsset',
            original_sha256=sha(obj.get_raw_data()), native_path=path.relative_to(ROOT).as_posix(),
            output=target.as_posix(), guid=target_guid, payload_sha256=sha(raw))
        return result

    def register_scripts(self):
        super().register_scripts()
        paths = {name: ROOT / 'unity/Assets/Game/Runtime/Official/HomeUI' / (name + '.cs')
            for name in ('FurnitureRepairDialog', 'FurnitureIconWidget', 'FurnitureRepairedNotifyDialog')}
        paths.update({
            'Spine.Unity.SkeletonGraphic': ROOT / 'unity/Assets/Spine/Runtime/spine-unity/Components/SkeletonGraphic.cs',
            'Spine.Unity.SkeletonDataAsset': ROOT / 'unity/Assets/Spine/Runtime/spine-unity/Asset Types/SkeletonDataAsset.cs',
            'Spine.Unity.SpineAtlasAsset': ROOT / 'unity/Assets/Spine/Runtime/spine-unity/Asset Types/SpineAtlasAsset.cs',
            'CloseWindowBtn': ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI/CloseWindowBtn.cs',
            'PropUpgradeEffectAgent': ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI/PropUpgradeEffectAgent.cs',
            'SortingOrderSetter': ROOT / 'unity/Assets/Game/Runtime/Official/TeamUI/SortingOrderSetter.cs',
            'UIMesh': ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/UIMesh.cs',
        })
        for name, path in paths.items():
            pointer = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
            self.scripts[name] = pointer
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + '.meta').read_bytes()))
            namespace, _, short_name = name.rpartition('.')
            self.class_ids[script_id(namespace, short_name)] = pointer
            self.script_guids.add(pointer['guid'])

    def friendship_templates(self):
        self.recovery.recover('FurnitureRepairDialog', self.work / 'home-native',
            bundle='defaultpackage_assets_prefabs_ui_furniture.bundle',
            native_relative='Prefabs/UI/Furniture/FurnitureRepairDialog.prefab')
        self.recovery.recover('FurnitureRepairedNotifyDialog', self.work / 'home-native',
            bundle='defaultpackage_assets_prefabs_ui_commonnotify.bundle',
            native_relative='Prefabs/UI/CommonNotify/FurnitureRepairedNotifyDialog.prefab')
        self.recovery.recover('PlayerPropItemVariant', self.work / 'home-native',
            bundle='defaultpackage_assets_prefabs_ui_public.bundle',
            native_relative='Prefabs/UI/Public/PlayerPropItemVariant.prefab')
        table = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        self.icons = []
        for row in table:
            if not row['path'].startswith('Assets/Prefabs/UI/Home/Furniture/') or Path(row['path']).stem == '0_template':
                continue
            name = Path(row['path']).stem
            self.recovery.recover(name, self.work / 'home-native', bundle=row['bundle'],
                native_relative=row['path'].removeprefix('Assets/'))
            self.icons.append(name)
        self.recovery.finalize()
        settings, = [obj for obj in self.recovery.apk.objects('defaultpackage_assets_scriptableconfig_modules_home.bundle')
            if obj.type.name == 'MonoBehaviour' and self.recovery.fields(obj).get('m_Name') == 'HomeSettings']
        self.settings = dict(source=self.recovery.apk.provenance(settings), identity=identity(settings),
            sha256=sha(settings.get_raw_data()), fields=self.recovery.fields(settings))
        code = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code'
        constructors = Constructors(code)
        mapping = {name.rsplit('/', 1)[-1]: kind for kind, name in
            re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
        table, = [obj for obj in self.recovery.apk.objects('defaultpackage_assets_config_luban.bundle')
            if obj.type.name == 'TextAsset' and obj.read().m_Name.rsplit('/', 1)[-1] == 'furniture.g']
        raw = table.read().m_Script.encode('utf8', 'surrogateescape')
        self.furniture_rows = constructors.table(raw, mapping['furniture'])
        self.furniture_source = dict(source=self.recovery.apk.provenance(table), object_sha256=sha(table.get_raw_data()),
            payload_sha256=sha(raw), constructor=mapping['furniture'], rows=self.furniture_rows)
        self.hang_up_source = None
        for obj in self.recovery.apk.objects('defaultpackage_assets_config_luban.bundle'):
            if obj.type.name == 'TextAsset' and obj.read().m_Name.rsplit('/', 1)[-1] == 'hang_up.g':
                raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
                self.hang_up_rows = constructors.table(raw, mapping['hang_up'])
                self.hang_up_source = dict(source=self.recovery.apk.provenance(obj), object_sha256=sha(obj.get_raw_data()),
                    payload_sha256=sha(raw), constructor=mapping['hang_up'], rows=self.hang_up_rows)
                break
        if self.hang_up_source is None:
            raise ValueError('Selected original hang_up table is unavailable')
        for name in constructors.sources:
            item = code / 'common-source/Common' / (name + '.cs')
            if not item.is_file():
                item = code / 'common-source/Common.LubanGen' / (name + '.cs')
            self.inputs[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
        item = code / 'global-metadata.dat'
        self.inputs[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
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

        result = []
        self.home_template_pointers = {}
        for native, _, panel, record in self.recovery.outputs:
            template_guid = hashlib.md5(('zjcs:partner-current:' + native.stem + '.prefab').encode()).hexdigest()
            for key, file_id in panel['objectMap'].items():
                self.home_template_pointers[key] = dict(fileID=file_id, guid=template_guid, type=3)
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
                fields = panel['documents'][script['nativeFileID']]['MonoBehaviour']
                original = self.recovery.fields(owner)
                restore(original, fields, script['original'])
                if script['type'] == 'PopupWindowSound':
                    for field in ('ShowSound', 'HideSound'):
                        fields[field] = self.sound(original[field], owner)
            result.append((native.stem, panel))
        cache = self.recovery.proof['component_cache']
        self.inputs[cache['path']] = cache['sha256']
        self.template_selection = dict(scope='Complete original FurnitureRepairDialog and configured icon hierarchies.',
            settings=self.settings, furniture=self.furniture_source, hang_up=self.hang_up_source, icons=self.icons)
        return result

    def build(self):
        PartnerGiftBinding.build(self)
        receipt_path = self.work / 'partner-gift-native-source-manifest.json'
        receipt = json.loads(receipt_path.read_bytes())
        folder = self.work / 'gift-assets'
        for relative in list(receipt['outputs']):
            if Path(relative).suffix in ('.bytes', '.txt'):
                meta = folder / (relative + '.meta')
                meta.write_text('fileFormatVersion: 2\nguid: ' + guid(meta)
                    + '\nTextScriptImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
                receipt['outputs'][relative + '.meta'] = sha(meta.read_bytes())
        keys = {doc['MonoBehaviour']['myKey'] for filename in ('FurnitureRepairDialog.prefab', 'FurnitureRepairedNotifyDialog.prefab')
            for doc in documents((folder / filename).read_text('utf8')).values()
            if 'MonoBehaviour' in doc and doc['MonoBehaviour'].get('myKey')}
        keys.update(('ui_build_text', 'ui_fix_text', 'ui_item_not_enough'))
        keys.update('item_' + str(row['ClassId']) + '_name' for row in self.furniture_rows)
        keys.update(group['TypeNameKey'] for group in self.settings['fields']['mFurnitureClassifications'])
        for name in self.icons:
            keys.update(('item_unrepair_' + name, 'item_unrepair_desc_' + name))
        prop_type = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code/common-source/Common/PropType.cs'
        names = {int(value): name for name, value in re.findall(r'^\s*(\w+) = (\d+),?$', prop_type.read_text('utf-8-sig'), re.M)}
        keys.update('PropType.' + names[row['EffectFixedPropType']] for row in self.hang_up_rows if row['NeedRecordData'])
        receipt['inputs'][prop_type.relative_to(ROOT).as_posix()] = sha(prop_type.read_bytes())
        texts, sources = language(keys)
        missing = keys - texts.keys()
        # Literal format keys use the original missing-key fallback.
        if missing - {'{0}'}:
            raise ValueError('Official home repair localization missing: ' + repr(missing))
        receipt['missing_language_keys'] = sorted(missing)
        receipt['inputs'].update(sources)
        target = folder / 'home-repair-texts.json'
        target.write_text(json.dumps(dict(manifest=self.recovery.apk.version,
            texts=[dict(key=key, text=text) for key, text in sorted(texts.items())],
            classifications=self.settings['fields']['mFurnitureClassifications'],
            furniture=[dict(type=row['FurnitureType'], itemId=row['ClassId']) for row in self.furniture_rows],
            recordedHangUps=[dict(id=row['Id'], furniture=row['BindFurniture']) for row in self.hang_up_rows if row['NeedRecordData']]), ensure_ascii=False, indent=2) + '\n',
            'utf8', newline='\n')
        Path(str(target) + '.meta').write_text('fileFormatVersion: 2\nguid: '
            + hashlib.md5(b'zjcs:home-repair-texts').hexdigest() + '\nTextScriptImporter:\n  externalObjects: {}\n',
            'utf8', newline='\n')
        for item in (folder / 'partner-gift-texts.json', folder / 'partner-gift-texts.json.meta'):
            receipt['outputs'].pop(item.name)
            item.unlink()
        for item in (target, Path(str(target) + '.meta')):
            receipt['outputs'][item.name] = sha(item.read_bytes())
        used = set()
        for relative in receipt['outputs']:
            item = folder / relative
            if item.suffix != '.meta' and item.read_bytes().startswith(b'%YAML'):
                used.update(re.findall(r'm_Script: \{[^\n}]*guid: ([0-9a-f]{32})', item.read_text('utf8')))
        receipt['script_bindings'] = {name: row for name, row in receipt['script_bindings'].items()
            if row['pointer']['guid'] in used}
        for name in ('prepare_home_repair_native.py', 'prepare_partner_roster_native.py', 'prepare_partner_input_dialog.py',
                     'prepare_partner_confirm_dialog.py', 'prepare_partner_gift_native.py', 'partner_roster_animator_binding.py',
                     'official_tables.py', 'official_metadata.py'):
            item = ROOT / 'scripts' / name
            receipt['inputs'][item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
        source = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code'
        for relative in ('client-source/FurnitureRepairDialog.cs', 'client-source/FurnitureIconWidget.cs',
                         'client-source/FurnitureRepairedNotifyDialog.cs',
                         'common-source/Common/PlayerHangUpDataExt.cs', 'common-source/Common/HangUpInfoParser.cs',
                         'common-source/Common/EFurniture.cs', 'common-source/HomeSettings.cs', 'common-source/Common/LanguageUtils.cs'):
            item = source / relative
            receipt['inputs'][item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
        receipt['scope'] = 'Original repair and repaired-notify windows, property row template, native popup animations, selected HomeSettings classifications and furniture icon closure.'
        receipt['generator_sha256'] = sha(Path(__file__).read_bytes())
        receipt['animation_root_path_repairs'] = self.animation_root_paths
        receipt['original_empty_value_restorations'] = self.empty_strings
        receipt.pop('gradient_aot_methods')
        receipt.pop('gradient_observations')
        (self.work / RECEIPT.name).write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n',
            'utf8', newline='\n')
        receipt_path.unlink()


def install(work):
    receipt = json.loads((work / RECEIPT.name).read_bytes())
    if receipt['generator_sha256'] != sha(Path(__file__).read_bytes()):
        raise ValueError('Rebuild changed home repair generator')
    for relative, expected in receipt['inputs'].items():
        if sha((ROOT / relative).read_bytes()) != expected:
            raise ValueError('Home repair source changed: ' + relative)
    for row in receipt['script_bindings'].values():
        path = ROOT / row['path']
        if sha(path.read_bytes()) != row['sha256'] or sha(Path(str(path) + '.meta').read_bytes()) != row['meta_sha256']:
            raise ValueError('Home repair script binding changed: ' + row['path'])
    previous = json.loads(RECEIPT.read_bytes()).get('installed_output_sha256', {}) if RECEIPT.is_file() else {}
    existing = {}
    # Reuse shared native resources by GUID, including resources installed by
    # other feature modules. Never create a second asset with the same GUID.
    for meta in (ROOT / 'unity/Assets').rglob('*.meta'):
        path = Path(str(meta)[:-5])
        if path.is_file():
            key = guid(meta)
            if key in existing:
                raise ValueError('Existing Unity GUID collision: ' + key)
            existing[key] = path
    def same_native_names(left, right):
        if not left.startswith(b'%YAML') or not right.startswith(b'%YAML'):
            return False
        def normalize(value):
            if isinstance(value, dict):
                return {key: '' if key == 'm_Name' and child is None else normalize(child) for key, child in value.items()}
            if isinstance(value, list):
                return [normalize(child) for child in value]
            return value
        return normalize(documents(left.decode('utf8'))) == normalize(documents(right.decode('utf8')))

    plan, installed, reused = [], {}, []
    for relative, expected in receipt['outputs'].items():
        source = work / 'gift-assets' / relative
        if sha(source.read_bytes()) != expected:
            raise ValueError('Staged home repair output changed: ' + relative)
        if relative.endswith('.meta'):
            continue
        key = guid(Path(str(source) + '.meta'))
        target = existing.get(key, DESTINATION / relative)
        equivalent = target.exists() and same_native_names(target.read_bytes(), source.read_bytes())
        if target.exists() and target.read_bytes() != source.read_bytes() and not equivalent:
            if not target.is_relative_to(DESTINATION) or sha(target.read_bytes()) != previous.get(target.relative_to(ROOT).as_posix()):
                raise ValueError('Preserve different existing native resource: ' + str(target))
        if target.exists() and (target.read_bytes() == source.read_bytes() or equivalent):
            reused.append(dict(path=target.relative_to(ROOT).as_posix(), sha256=sha(target.read_bytes()), guid=key,
                staged_sha256=expected, comparison='Exact bytes, or parsed native YAML equality with null/empty object names normalized only.'))
        else:
            plan.extend(((source, target), (Path(str(source) + '.meta'), Path(str(target) + '.meta'))))
        if target.is_relative_to(DESTINATION):
            installed[target.relative_to(ROOT).as_posix()] = sha(target.read_bytes()) if target.exists() and equivalent else expected
            installed[Path(str(target) + '.meta').relative_to(ROOT).as_posix()] = sha(Path(str(source) + '.meta').read_bytes())
        existing[key] = target
    for source, target in plan:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(source.read_bytes())
    for folder in [DESTINATION.parent, DESTINATION, *[p for p in DESTINATION.rglob('*') if p.is_dir()]]:
        meta = metadata(folder, 'DefaultImporter', True)
        installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
    script = ROOT / 'unity/Assets/Game/Runtime/Official/HomeUI/OfficialHomeRepairAssetCatalog.cs'
    raw = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'
    raw += '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n'
    raw += '  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
    raw += '  m_Script: {fileID: 11500000, guid: ' + guid(Path(str(script) + '.meta')) + ', type: 3}\n'
    raw += "  m_Name: OfficialHomeRepairAssetCatalog\n  m_EditorClassIdentifier: ''\n"
    for field, name in (('repairPrefab', 'FurnitureRepairDialog'), ('repairedNotifyPrefab', 'FurnitureRepairedNotifyDialog')):
        root = receipt['roots'][name]
        raw += '  ' + field + ': {fileID: ' + str(root['fileID']) + ', guid: ' + root['guid'] + ', type: 3}\n'
    text = existing[hashlib.md5(b'zjcs:home-repair-texts').hexdigest()]
    raw += '  texts: {fileID: 4900000, guid: ' + guid(Path(str(text) + '.meta')) + ', type: 3}\n  icons:\n'
    enum_path = ROOT / 'unity/Assets/Game/Runtime/Official/HomeUI/EFurniture.cs'
    enum = {name: int(value) for name, value in re.findall(r'(\w+)\s*=\s*(\d+)', enum_path.read_text('utf8'))}
    for name in receipt['template_selection']['icons']:
        root = receipt['roots'][name]
        raw += '  - furniture: ' + str(enum[name]) + '\n    prefab: {fileID: ' + str(root['fileID']) + ', guid: ' + root['guid'] + ', type: 3}\n'
    CATALOG.parent.mkdir(parents=True, exist_ok=True)
    metadata(CATALOG.parent, 'DefaultImporter', True)
    if CATALOG.exists() and sha(CATALOG.read_bytes()) != previous.get(CATALOG.relative_to(ROOT).as_posix()) and CATALOG.read_text('utf8') != raw:
        raise ValueError('Preserve changed home repair catalog')
    CATALOG.write_text(raw, 'utf8', newline='\n')
    metadata(CATALOG, 'NativeFormatImporter')
    for path in (CATALOG, Path(str(CATALOG) + '.meta')):
        installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    receipt.update(installed_output_sha256=installed, reused=reused)
    receipt['runtime_code'] = {item.relative_to(ROOT).as_posix(): sha(item.read_bytes())
        for item in script.parent.iterdir() if item.is_file()}
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed original home repair resources:', len(installed), 'files; reused', len(reused), 'assets', flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    work = validation_directory()
    if args.install:
        install(work)
    else:
        HomeRepairBinding(work).build()


if __name__ == '__main__':
    main()
