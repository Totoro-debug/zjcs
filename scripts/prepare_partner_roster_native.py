"""Bind the exact current NpcPanel.mNpcListGo subtree; stage before installing."""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import re
import shutil

from official_apk import ROOT
from official_manifest import read_asset_manifest
from npc_travel_language import language
from prepare_partner_card_native import guid, sha, script_id
from prepare_partner_gift_native import PartnerGiftBinding
from prepare_current_partner_native import documents, identity
from prepare_partner_ui_helpers import CLIENT, meta
from validation_workspace import validation_directory


class PartnerRosterBinding(PartnerGiftBinding):
    def learn(self, source, native, owner):
        if isinstance(source, dict) and 'm_FileID' in source and source.get('m_PathID') and isinstance(native, dict) and native.get('guid'):
            from prepare_current_partner_native import pointer
            linked = pointer(source, owner)
            if linked.type.name == 'MonoScript':
                script = linked.read(check_read=False)
                full_name = '.'.join(filter(None, [script.m_Namespace, script.m_ClassName]))
                if native['fileID'] != script_id(script.m_Namespace, script.m_ClassName):
                    raise ValueError('Original animation script fileID differs: ' + full_name)
                target = self.scripts[full_name]
                key = (native['guid'], native['fileID'])
                if key in self.animation_scripts and self.animation_scripts[key]['pointer'] != target:
                    raise ValueError('One animation MonoScript identity maps to different runtime types')
                self.animation_scripts[key] = dict(original=identity(linked), sha256=sha(linked.get_raw_data()),
                    native=native, pointer=target, type=full_name)
                return
            previous = self.objects.get(native['guid'])
            if previous is not None and identity(previous) != identity(linked):
                raise ValueError('Native GUID identity conflict: ' + repr((native['guid'], identity(owner),
                    identity(previous), previous.type.name, sha(previous.get_raw_data()),
                    identity(linked), linked.type.name, sha(linked.get_raw_data()))))
        return super().learn(source, native, owner)

    def register_scripts(self):
        super().register_scripts()
        self.animation_scripts = {}
        self.animation_root_paths = []
        runtime = ROOT / 'unity/Assets/Game/Runtime/Official'
        paths = {name: runtime / 'PartnersUI' / (name + '.cs') for name in CLIENT}
        paths.update({
            'NpcPanelNpcList': runtime / 'PartnersUI/Roster/NpcPanelNpcList.cs',
            'UnityEngine.UI.LoopVerticalScrollRect': runtime / 'WorldUI/LoopScroll/LoopVerticalScrollRect.cs',
            'LoopScrollGridLayoutGroup': runtime / 'WorldUI/LoopScroll/LoopScrollGridLayoutGroup.cs',
            'UnityEngine.UI.Ext.TabGroup': runtime / 'WorldUI/WorldTabGroup.cs',
            'UnityEngine.UI.Ext.TabItem': runtime / 'WorldUI/WorldTabItem.cs',
            'UnityEngine.UI.Ext.TabObjectTransition': runtime / 'PartnersUI/TabObjectTransition.cs',
            'UIToggle': runtime / 'SkillsUI/UIToggle.cs',
            'UIClickSound': runtime / 'ProfessionUI/UIClickSound.cs',
            'LayoutElementClamper': runtime / 'WorldUI/WorldLayoutElementClamper.cs',
        })
        for full_name, path in paths.items():
            namespace, _, name = full_name.rpartition('.')
            metadata = Path(str(path) + '.meta')
            reference = dict(fileID=11500000, guid=guid(metadata), type=3)
            self.scripts[full_name] = reference
            self.script_sources[full_name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=reference,
                sha256=sha(path.read_bytes()), meta_sha256=sha(metadata.read_bytes()))
            self.class_ids[script_id(namespace, name)] = reference
            self.script_guids.add(reference['guid'])

    def gradient_evidence(self):
        return []

    def replace(self, value):
        if isinstance(value, dict) and 'guid' in value and (value['guid'], value.get('fileID')) in self.animation_scripts:
            return self.animation_scripts[(value['guid'], value['fileID'])]['pointer']
        if isinstance(value, dict) and value == dict(**{'class': 'StateAnimatorConfig', 'ns': '', 'asm': 'Framework'}):
            return dict(**{'class': 'StateAnimatorConfig', 'ns': 'Zjcs.Official.PartnersUI', 'asm': 'Zjcs.Official.PartnersUI'})
        return super().replace(value)

    def load(self, source_guid):
        if source_guid in self.redirects:
            return self.redirects[source_guid]
        obj = self.objects.get(source_guid)
        if source_guid not in self.redirects and obj is not None and obj.type.name == 'GameObject':
            self.recovery.recover('NpcPanelNpcItem', self.work / 'partner-native')
            native, _, data, record = self.recovery.outputs[-1]
            original_root, = [key for key, value in data['objectMap'].items() if value == data['root']]
            if original_root != identity(obj) or guid(Path(str(native) + '.meta')) != source_guid:
                raise ValueError('The referenced prefab differs from the recovered official card')
            receipt_path = ROOT / 'assets/reference/partner-card-binding-source-manifest.json'
            receipt = json.loads(receipt_path.read_bytes())
            installed = ROOT / receipt['installed_root'] / 'NpcPanelNpcItem.prefab'
            if receipt['manifest'] != self.recovery.apk.version or receipt['root']['fileID'] != data['root']:
                raise ValueError('Installed partner card version/root differs')
            for path in (installed, Path(str(installed) + '.meta')):
                if sha(path.read_bytes()) != receipt['outputs'][path.name]:
                    raise ValueError('Installed partner card differs from its receipt')
                self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
            self.inputs[receipt_path.relative_to(ROOT).as_posix()] = sha(receipt_path.read_bytes())
            self.inputs[record['nativeSource']] = sha(native.read_bytes())
            self.redirects[source_guid] = receipt['root']
            self.shared_card = dict(original=original_root, original_sha256=sha(obj.get_raw_data()),
                reference=receipt['root'], receipt=receipt_path.relative_to(ROOT).as_posix())
        if source_guid not in self.redirects and obj is not None and obj.type.name == 'AnimatorController':
            return self.controller(source_guid, obj)
        try:
            result = super().load(source_guid)
            if obj.type.name == 'AnimationClip':
                import zlib
                _, _, docs = next(row for row in self.queue if row[1] == result['guid'])
                clip = docs[result['fileID']]['AnimationClip']
                source = self.recovery.fields(obj)['m_ClipBindingConstant']['genericBindings']
                native = clip['m_ClipBindingConstant']['genericBindings']
                if len(source) != len(native):
                    raise ValueError('Original animation binding count differs')
                for field in ('m_FloatCurves', 'm_EditorCurves'):
                    for index, curve in enumerate(clip.get(field, [])):
                        if curve.get('path', '') is not None:
                            continue
                        matches = [(number, original) for number, (original, converted) in enumerate(zip(source, native))
                            if original['path'] == converted['path'] == 0 and original['typeID'] == curve['classID']
                            and original['attribute'] == zlib.crc32(curve['attribute'].encode('utf8'))
                            and converted['script'] == curve['script']]
                        if len(matches) != 1:
                            raise ValueError('Null curve path is not one original compiled root binding')
                        number, original = matches[0]
                        curve['path'] = ''
                        self.animation_root_paths.append(dict(original=identity(obj), field=field, curve_index=index,
                            original_binding_index=number, original_binding=original, converted=None, restored=''))
            return result
        except (ValueError, KeyError) as error:
            detail = 'unresolved' if obj is None else obj.type.name + ' ' + identity(obj) + ' ' + str(getattr(obj.read(check_read=False), 'm_Name', ''))
            raise ValueError('Roster dependency ' + source_guid + ' ' + detail + ': ' + str(error)) from error

    def controller(self, source_guid, obj):
        native = self.native_path(source_guid, obj)
        raw = native.read_bytes()
        receipt_key = native.relative_to(self.cache / 'native-export-all').as_posix()
        metadata = Path(str(native) + '.meta')
        if sha(raw) != self.checked[receipt_key] or sha(metadata.read_bytes()) != self.checked[receipt_key + '.meta']:
            raise ValueError('Native roster animator differs from selected cache')
        self.inputs[native.relative_to(ROOT).as_posix()] = sha(raw)
        self.inputs[metadata.relative_to(ROOT).as_posix()] = sha(metadata.read_bytes())
        docs = documents(raw.decode('utf8'))
        root, = [key for key, value in docs.items() if 'AnimatorController' in value]
        from partner_roster_animator_binding import verify_controller
        visited = verify_controller(self, obj, docs, root)
        target = Path('NativeControllers') / (sha(identity(obj).encode())[:16] + '.controller')
        target_guid = hashlib.md5(('zjcs:partner-original:' + identity(obj)).encode()).hexdigest()
        reference = dict(fileID=root, guid=target_guid, type=2)
        self.redirects[source_guid] = reference
        self.queue.append((target, target_guid, docs))
        self.proof[identity(obj)] = dict(source=self.recovery.apk.provenance(obj), original_sha256=sha(obj.get_raw_data()),
            output=target.as_posix(), guid=target_guid, verified_runtime_subassets=visited)
        return reference

    def friendship_templates(self):
        self.recovery.recover('NpcPanel', self.work / 'partner-native')
        self.recovery.finalize()
        native, _, panel, record = self.recovery.outputs[-1]
        consumer, = [row for row in panel['scripts'] if row['type'] == 'NpcPanel']
        root = panel['documents'][consumer['nativeFileID']]['MonoBehaviour']['mNpcListGo']['fileID']
        selected = set()

        def visit(go_id):
            selected.add(go_id)
            components = panel['documents'][go_id]['GameObject']['m_Component']
            selected.update(row['component']['fileID'] for row in components)
            transform = next(iter(panel['documents'][components[0]['component']['fileID']].values()))
            for child in transform['m_Children']:
                visit(next(iter(panel['documents'][child['fileID']].values()))['m_GameObject']['fileID'])

        visit(root)
        result = copy.deepcopy(panel)
        result['root'] = root
        result['documents'] = {key: value for key, value in result['documents'].items() if key in selected}
        result['scripts'] = [row for row in result['scripts'] if row['nativeFileID'] in selected]
        result['objectMap'] = {key: value for key, value in result['objectMap'].items() if value in selected}
        transform_id = result['documents'][root]['GameObject']['m_Component'][0]['component']['fileID']
        transform = next(iter(result['documents'][transform_id].values()))
        old_parent = transform['m_Father']
        transform['m_Father'] = dict(fileID=0)

        def check(value):
            if isinstance(value, dict):
                if set(value) == {'fileID'} and value['fileID'] and value['fileID'] not in selected:
                    raise ValueError('Roster subtree references outside its original hierarchy: ' + repr(value))
                for item in value.values():
                    check(item)
            elif isinstance(value, list):
                for item in value:
                    check(item)

        check(result['documents'])
        source_root, = [key for key, value in result['objectMap'].items() if value == root]
        self.template_selection = dict(consumer=consumer['original'], field='mNpcListGo', source_root=source_root,
            native_root=root, mapped_objects=len(selected), source_name=result['documents'][root]['GameObject']['m_Name'],
            original_parent=old_parent, detached_parent=dict(fileID=0),
            adaptation='Exact original referenced roster subtree; detach root parent only. Complete hierarchy/geometry/fields retained.')
        for path in (native, Path(str(native) + '.meta')):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        cache = self.recovery.proof['component_cache']
        self.inputs[cache['path']] = cache['sha256']
        for key, reference in result['external'].items():
            self.objects[reference['guid']] = self.original(key)
        for key, file_id in result['objectMap'].items():
            owner = self.original(key)
            self.learn(self.recovery.fields(owner), next(iter(result['documents'][file_id].values())), owner)
        self.replace(result['documents'])
        records = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        self.font = self.chinese_font(records)
        return [('NpcRosterContent', result)]

    def build(self):
        super().build()
        receipt_path = self.work / 'partner-gift-native-source-manifest.json'
        receipt = json.loads(receipt_path.read_bytes())
        for key in ('gradient_aot_methods', 'gradient_observations'):
            receipt.pop(key)
        folder = self.work / 'gift-assets'
        prefab = folder / 'NpcRosterContent.prefab'
        keys = set(re.findall(r'(?m)^  myKey: ([^\n]+)$', prefab.read_text('utf8')))
        keys = {key for key in keys if key.startswith(('ui_', 'CollectBookPanel.'))}
        enum_path = ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI/CollectSortType.cs'
        enum_text = enum_path.read_text('utf-8-sig')
        enum_names = re.findall(r'^\s*([A-Za-z]\w*)[,]?$', enum_text, re.M)
        configured = {prefab['documents'][row['nativeFileID']]['MonoBehaviour']['SortType']
            for _, _, prefab, _ in self.recovery.outputs for row in prefab['scripts'] if row['type'] == 'CollectSortGroupItem'
            and prefab['documents'][prefab['documents'][row['nativeFileID']]['MonoBehaviour']['m_GameObject']['fileID']]['GameObject']['m_IsActive']}
        keys.update('CollectSortType.' + enum_names[index] for index in configured)
        receipt['inputs'][enum_path.relative_to(ROOT).as_posix()] = sha(enum_path.read_bytes())
        texts, sources = language(keys)
        if set(texts) != keys:
            raise ValueError('Original roster language is unavailable: ' + repr(keys - texts.keys()))
        for relative, expected in sources.items():
            if sha((ROOT / relative).read_bytes()) != expected:
                raise ValueError('Original roster language input changed: ' + relative)
            receipt['inputs'][relative] = expected
        text_path = folder / 'partner-gift-texts.json'
        payload = json.loads(text_path.read_bytes())
        merged = {row['key']: row['text'] for row in payload['texts']}
        merged.update(texts)
        payload['texts'] = [dict(key=key, text=value) for key, value in sorted(merged.items())]
        text_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        target = folder / 'partner-roster-texts.json'
        target.write_bytes(text_path.read_bytes())
        metadata = Path(str(target) + '.meta')
        metadata.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:partner-roster-texts').hexdigest()
            + '\nTextScriptImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
        for old in (text_path, Path(str(text_path) + '.meta')):
            receipt['outputs'].pop(old.name)
            old.unlink()
        for path in (target, metadata):
            receipt['outputs'][path.name] = sha(path.read_bytes())
        used = set()
        for relative in receipt['outputs']:
            if Path(relative).suffix in ('.prefab', '.asset', '.mat'):
                used.update(match.decode() for match in re.findall(rb'm_Script: \{fileID: 11500000, guid: ([0-9a-f]{32})',
                    (folder / relative).read_bytes()))
        receipt['script_bindings'] = {name: row for name, row in receipt['script_bindings'].items() if row['pointer']['guid'] in used}
        receipt['font'] = self.font
        receipt['shared_card'] = self.shared_card
        receipt['animation_script_bindings'] = list(self.animation_scripts.values())
        receipt['runtime_code'] = self.runtime_code
        receipt['inputs'].update(self.runtime_inputs)
        for name in ('prepare_partner_gift_native.py', 'partner_gift_animator_binding.py', 'partner_roster_animator_binding.py'):
            path = ROOT / 'scripts' / name
            receipt['inputs'][path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        receipt['scope'] = 'Exact original roster subtree and static dependencies; complete NpcPanel host/dialogue/star-god pages are not installed.'
        receipt['animation_root_path_repairs'] = self.animation_root_paths
        receipt['adaptations'] = [
            'Detach only the exact NpcPanel.mNpcListGo root parent; retain every original object, component, field and geometry.',
            'Bind original MonoScript references by assembly GUID plus script fileID, including animation curve bindings.',
            'Restore empty animation root curve paths only where original compiled path hash zero, attribute CRC, component type and script identity match; converted YAML null must not become a literal hierarchy path.',
            'Verify original flat animator states, motions, transitions, parameter types/names/defaults against compiled player constants.',
            'Reuse the separately verified current original card prefab by its CAB/PathID, native GUID and installed receipt.',
            'TabObjectTransition and StateAnimatorConfig/Data keep original branches, order and serialized field names; namespace/interface aliases and decompiler enum-arithmetic cast only.',
            'Retain inactive generic sort/filter options; include the original active Friendship/ActivateTime language entries without inventing missing generic keys.'
        ]
        receipt['generator_sha256'] = sha(Path(__file__).read_bytes())
        (self.work / 'partner-roster-native-source-manifest.json').write_text(
            json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        receipt_path.unlink()


def install_staged(work):
    receipt = json.loads((work / 'partner-roster-native-source-manifest.json').read_bytes())
    if receipt['generator_sha256'] != sha(Path(__file__).read_bytes()):
        raise ValueError('Roster generator changed after staging')
    for relative, expected in dict(receipt['inputs'], **receipt['runtime_code']).items():
        if sha((ROOT / relative).read_bytes()) != expected:
            raise ValueError('Roster input/runtime code changed: ' + relative)
    for record in receipt['script_bindings'].values():
        path = ROOT / record['path']
        if sha(path.read_bytes()) != record['sha256'] or sha(Path(str(path) + '.meta').read_bytes()) != record['meta_sha256']:
            raise ValueError('Roster component binding changed: ' + record['path'])
    existing = {}
    partners = ROOT / 'unity/Assets/Official/Partners'
    for metadata in partners.rglob('*.meta'):
        asset = Path(str(metadata)[:-5])
        if asset.is_file():
            key = guid(metadata)
            if key in existing:
                raise ValueError('Existing partner asset GUID collision: ' + key)
            existing[key] = asset
    destination = partners / 'UI/Roster'
    staged = work / 'gift-assets'
    planned, reused = [], []
    for relative, expected in receipt['outputs'].items():
        source = staged / relative
        if sha(source.read_bytes()) != expected:
            raise ValueError('Staged roster output changed: ' + relative)
        if relative.endswith('.meta'):
            continue
        metadata = Path(str(source) + '.meta')
        key = guid(metadata)
        target = existing.get(key, destination / relative)
        if target.exists():
            if target.read_bytes() != source.read_bytes() or guid(Path(str(target) + '.meta')) != key:
                raise ValueError('Preserve different existing partner resource: ' + str(target))
            reused.append(dict(staged=relative, installed=target.relative_to(ROOT).as_posix(), guid=key,
                sha256=sha(target.read_bytes()), metadata_sha256=sha(Path(str(target) + '.meta').read_bytes())))
        else:
            planned.append((source, target))
        existing[key] = target
    folders = set()
    for target in existing.values():
        if not target.is_relative_to(destination):
            continue
        folder = target.parent
        while folder.is_relative_to(destination):
            folders.add(folder)
            if folder == destination:
                break
            folder = folder.parent
    folder_metadata = {}
    for folder in sorted(folders, key=lambda path: len(path.parts)):
        folder.mkdir(parents=True, exist_ok=True)
        metadata = Path(str(folder) + '.meta')
        if not metadata.exists():
            meta(folder, folder=True)
            metadata.write_bytes(b'\n'.join(line.rstrip() for line in metadata.read_bytes().splitlines()) + b'\n')
        folder_metadata[metadata.relative_to(ROOT).as_posix()] = sha(metadata.read_bytes())
    for source, target in planned:
        shutil.copy2(source, target)
        shutil.copy2(Path(str(source) + '.meta'), Path(str(target) + '.meta'))
    installed_outputs = dict(folder_metadata)
    for relative in receipt['outputs']:
        if relative.endswith('.meta'):
            continue
        target = existing[guid(Path(str(staged / relative) + '.meta'))]
        for path in (target, Path(str(target) + '.meta')):
            installed_outputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    receipt['installed_root'] = destination.relative_to(ROOT).as_posix()
    receipt['installed_outputs'] = installed_outputs
    receipt['reused_assets'] = reused
    receipt['validation_source_sha256'] = {relative: sha((ROOT / relative).read_bytes()) for relative in (
        'scripts/prepare_partner_roster_native_validation.py', 'scripts/fixtures/partner_roster_native_validation.cs')}
    (ROOT / 'assets/reference/partner-roster-native-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed roster payloads:', len(planned), '; reused original assets:', len(reused))


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--install-staged', action='store_true')
    args = parser.parse_args()
    if args.install_staged:
        install_staged(validation_directory())
        return
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    runtime_code, runtime_inputs = {}, {}
    for relative in ('client-source/UnityEngine.UI.Ext/TabObjectTransition.cs',
                     'framework-source/StateAnimatorConfig.cs', 'framework-source/StateAnimatorData.cs'):
        source = ROOT / '.tools/source-packages/official-code' / relative
        selected = ROOT / '.tools/source-packages/current' / version / 'official-code' / relative
        raw = source.read_bytes()
        if raw != selected.read_bytes():
            raise ValueError('Selected roster helper source differs: ' + relative)
        runtime_inputs[source.relative_to(ROOT).as_posix()] = sha(raw)
        text = raw.decode('utf-8-sig').replace('\r\n', '\n')
        if source.stem == 'TabObjectTransition':
            if not text.startswith('namespace UnityEngine.UI.Ext;\n'):
                raise ValueError('Original transition namespace changed')
            text = text.replace('namespace UnityEngine.UI.Ext;\n', '').replace(': TabTransition', ': Zjcs.Official.WorldUI.WorldTabTransition')
        else:
            text = re.sub(r'^\s*//IL_[^\n]*\n', '', text, flags=re.M)
            text = text.replace('IStateConfig', 'IWorldStateConfig').replace('StateConfigValue', 'WorldStateConfigValue')
            text = text.replace('switch (parameterType - 1)', 'switch ((int)parameterType - 1)')
        output = ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI' / source.name
        migrated = ('// Selected original roster helper; see partner-roster-native-source-manifest.json.\n'
            'using UnityEngine;\nusing Zjcs.Official.WorldUI;\nnamespace Zjcs.Official.PartnersUI\n{\n' + text + '\n}\n').encode('utf8')
        # Only overwrite a previously generated revision of this new helper.
        previous_tab = ('// Selected original tab object transition; see partner-roster-native-source-manifest.json.\n'
            'using UnityEngine;\nnamespace Zjcs.Official.PartnersUI\n{\n' + text + '\n}\n').encode('utf8')
        if output.exists() and output.read_bytes() not in (migrated, previous_tab):
            raise ValueError('Preserve an existing roster helper edit: ' + str(output))
        if not output.exists() or output.read_bytes() != migrated:
            output.write_bytes(migrated)
        meta(output)
        metadata = Path(str(output) + '.meta')
        normalized = b'\n'.join(line.rstrip() for line in metadata.read_bytes().splitlines()) + b'\n'
        if metadata.read_bytes() != normalized:
            metadata.write_bytes(normalized)
        runtime_code.update({path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in (output, metadata)})
    binding = PartnerRosterBinding(validation_directory())
    binding.runtime_code = runtime_code
    binding.runtime_inputs = runtime_inputs
    binding.build()


if __name__ == '__main__':
    main()
