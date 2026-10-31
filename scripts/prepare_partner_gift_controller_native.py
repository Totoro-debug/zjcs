"""Restore the exact gift subtree of the current official NPC detail panel."""
import copy
import argparse
import hashlib
import json
import re
from pathlib import Path

from official_apk import ROOT
from official_manifest import read_asset_manifest
from prepare_partner_card_native import guid, identity, language, script_id, sha
from prepare_current_partner_native import documents, pointer
from prepare_partner_gift_preferences_native import PartnerGiftPreferencesBinding
from validation_workspace import validation_directory


class PartnerGiftControllerBinding(PartnerGiftPreferencesBinding):
    def load(self, source_guid):
        try:
            obj = self.objects.get(source_guid)
            if source_guid not in self.redirects and obj is not None and obj.type.name == 'AnimatorController':
                return self.controller(source_guid, obj)
            return super().load(source_guid)
        except ValueError as error:
            obj = self.objects.get(source_guid)
            detail = '' if obj is None else obj.type.name + ' ' + identity(obj)
            raise ValueError('Gift dependency ' + source_guid + ' ' + detail + ': ' + str(error)) from error

    def controller(self, source_guid, obj):
        native = self.native_path(source_guid, obj)
        raw = native.read_bytes()
        receipt_key = native.relative_to(self.cache / 'native-export-all').as_posix()
        if sha(raw) != self.checked[receipt_key] or sha(Path(str(native) + '.meta').read_bytes()) != self.checked[receipt_key + '.meta']:
            raise ValueError('Native animator payload differs from selected cache')
        self.inputs[native.relative_to(ROOT).as_posix()] = sha(raw)
        self.inputs[native.relative_to(ROOT).as_posix() + '.meta'] = sha(Path(str(native) + '.meta').read_bytes())
        docs = documents(raw.decode('utf8'))
        root, = [key for key, value in docs.items() if 'AnimatorController' in value]
        # Player controllers store compiled state/transition constants. The
        # conversion cache restores authoring subassets; verify those constants
        # explicitly instead of treating the runtime data as editor PPtrs.
        from partner_gift_animator_binding import verify_controller
        visited = verify_controller(self, obj, docs, root)
        target = Path('NativeControllers') / (sha(identity(obj).encode())[:16] + '.controller')
        target_guid = hashlib.md5(('zjcs:partner-original:' + identity(obj)).encode()).hexdigest()
        reference = dict(fileID=root, guid=target_guid, type=2)
        self.redirects[source_guid] = reference
        self.queue.append((target, target_guid, docs))
        self.proof[identity(obj)] = dict(source=self.recovery.apk.provenance(obj), original_sha256=sha(obj.get_raw_data()),
            output=target.as_posix(), guid=target_guid, verified_runtime_subassets=visited)
        return reference

    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT / 'unity/Assets/Game/Runtime/Official'
        paths = {name: self.work / 'gift-controller-code' / (name + '.cs') for name in
                 ('NpcDetailPanelGiftContent', 'NpcGiftBlueDotItem', 'NpcFriendshipLevelContent')}
        paths.update({
            'UnityEngine.UI.LoopVerticalScrollRect': runtime / 'WorldUI/LoopScroll/LoopVerticalScrollRect.cs',
            'LoopScrollGridLayoutGroup': runtime / 'WorldUI/LoopScroll/LoopScrollGridLayoutGroup.cs',
            'SlicedFilledImage': runtime / 'WorldUI/SlicedFilledImage.cs',
            'UIBloomGraphicElement': runtime / 'ProfessionUI/UIBloomGraphicElement.cs',
            'CountRedDotImage': runtime / 'World/Map13MiniMapWindows/CountRedDotImage.cs',
            'PreviewTool': runtime / 'World/Map13MiniMapWindows/PreviewTool.cs',
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

    def friendship_templates(self):
        templates = super().friendship_templates()
        self.recovery.recover('NpcDetailPanel', self.work / 'partner-native')
        detail = self.recovery.outputs[-1][2]
        self.recovery.recover('NpcFriendshipPropItemGroup', self.work / 'partner-native')
        group = self.recovery.outputs[-1][2]
        records = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        record, = [row for row in records if row['guid'] == '18dffd1b841a0884bb2131048f3c26a1']
        upgrade, = [ref.deref() for name, ref in self.recovery.apk.container(record['bundle']) if name.casefold() == record['path'].casefold()]
        upgrade_name = upgrade.read().m_Name
        self.recovery.recover(upgrade_name, self.work / 'partner-native', bundle=record['bundle'],
                              native_relative=record['path'].removeprefix('Assets/'))
        upgrade_data = self.recovery.outputs[-1][2]
        self.recovery.finalize()
        # Cross-prefab references keep the source prefab identity, while the
        # restored payload uses an explicit generated GUID for that same root.
        for native, _, data, record in self.recovery.outputs:
            if record['name'] != 'NpcDetailPanel':
                self.redirects[guid(Path(str(native) + '.meta'))] = dict(fileID=data['root'],
                    guid=hashlib.md5(('zjcs:partner-current:' + record['name'] + '.prefab').encode()).hexdigest(), type=3)
        consumer, = [row for row in detail['scripts'] if row['type'] == 'NpcDetailPanelGiftContent']
        root = detail['documents'][consumer['nativeFileID']]['MonoBehaviour']['m_GameObject']['fileID']
        selected = set()

        def visit(go_id):
            selected.add(go_id)
            components = detail['documents'][go_id]['GameObject']['m_Component']
            selected.update(row['component']['fileID'] for row in components)
            transform = next(iter(detail['documents'][components[0]['component']['fileID']].values()))
            for child in transform['m_Children']:
                visit(next(iter(detail['documents'][child['fileID']].values()))['m_GameObject']['fileID'])

        visit(root)
        panel = copy.deepcopy(detail)
        panel['root'] = root
        panel['documents'] = {key: value for key, value in panel['documents'].items() if key in selected}
        panel['scripts'] = [row for row in panel['scripts'] if row['nativeFileID'] in selected]
        panel['objectMap'] = {key: value for key, value in panel['objectMap'].items() if value in selected}
        transform_id = panel['documents'][root]['GameObject']['m_Component'][0]['component']['fileID']
        transform = next(iter(panel['documents'][transform_id].values()))
        old_parent = transform['m_Father']
        transform['m_Father'] = dict(fileID=0)

        def check(value):
            if isinstance(value, dict):
                if set(value) == {'fileID'} and value['fileID'] and value['fileID'] not in selected:
                    raise ValueError('Gift subtree reference leaves its original hierarchy: ' + repr(value))
                for item in value.values():
                    check(item)
            elif isinstance(value, list):
                for item in value:
                    check(item)

        check(panel['documents'])
        source_root, = [key for key, value in panel['objectMap'].items() if value == root]
        self.template_selection['gift_panel'] = dict(consumer=consumer['original'], source_root=source_root,
            source_name=panel['documents'][root]['GameObject']['m_Name'], native_root=root,
            mapped_objects=len(selected), original_parent=old_parent, detached_parent=dict(fileID=0),
            adaptation='Exact gift subtree; detach root parent only. All geometry, children, components and internal fields retained.')
        self.template_selection['attachments'].append(dict(type=14, name=upgrade_name, asset=record))
        for native, _, data, record in self.recovery.outputs:
            self.inputs[record['nativeSource']] = sha(native.read_bytes())
            self.inputs[record['nativeSource'] + '.meta'] = sha(Path(str(native) + '.meta').read_bytes())
            if data is group:
                self.redirects[guid(Path(str(native) + '.meta'))] = dict(fileID=group['root'],
                    guid=hashlib.md5(b'zjcs:partner-current:NpcFriendshipPropItemGroup.prefab').hexdigest(), type=3)
        return templates + [('NpcGiftContent', panel), ('NpcFriendshipPropItemGroup', group), (upgrade_name, upgrade_data)]

    def build(self):
        super().build()
        finish_staged(self.work)


def finish_staged(work):
    path = work / 'partner-gift-preferences-native-source-manifest.json'
    if not path.is_file():
        path = work / 'partner-gift-controller-native-source-manifest.json'
    receipt = json.loads(path.read_bytes())
    # Earlier exports round-tripped Unity's blank myKey through YAML null.
    # Verify every repaired field against the original native document before
    # writing the empty string; retain the previous bytes for guarded installs.
    repairs = receipt.setdefault('empty_language_key_repairs', {})
    for name, original in (('ItemIconDefault', 'ItemIconDefault'), ('ItemIconNpcGift', 'ItemIconNpcGift'),
                           ('NpcFriendshipPropItemGroup', 'NpcFriendshipPropItemGroup'), ('NpcGiftContent', 'NpcDetailPanel')):
        asset = work / 'gift-assets' / (name + '.prefab')
        raw = asset.read_bytes()
        if b'  myKey: null\n' not in raw:
            continue
        candidates = [ROOT / relative for relative in receipt['inputs'] if relative.endswith('/' + original + '.prefab')]
        source, = candidates
        if sha(source.read_bytes()) != receipt['inputs'][source.relative_to(ROOT).as_posix()]:
            raise ValueError('Original language key source changed')
        native = documents(source.read_text('utf8'))
        blocks = re.findall(rb'^--- !u!114 &(-?\d+)\n(.*?)(?=^--- !u!|\Z)', raw, re.M | re.S)
        fields = [int(file_id) for file_id, body in blocks if b'  myKey: null\n' in body]
        if not fields or any(native[file_id]['MonoBehaviour']['myKey'] != '' for file_id in fields):
            raise ValueError('Only restore source-backed empty language keys: ' + name)
        repaired = raw.replace(b'  myKey: null\n', b"  myKey: ''\n")
        asset.write_bytes(repaired)
        repairs[asset.name] = dict(previous_sha256=sha(raw), sha256=sha(repaired), source=source.relative_to(ROOT).as_posix(), fields=fields)
        receipt['outputs'][asset.name] = sha(repaired)
    receipt['generator_sha256'] = sha(Path(__file__).read_bytes())
    for name in ('partner_gift_animator_binding.py', 'prepare_partner_gift_preferences_native.py', 'prepare_current_partner_native.py'):
        helper = ROOT / 'scripts' / name
        receipt['inputs'][helper.relative_to(ROOT).as_posix()] = sha(helper.read_bytes())
    keys = set()
    for name in ('NpcGiftContent', 'NpcFriendshipPropItemGroup'):
        keys.update(re.findall(r'(?m)^  myKey: (ui_\S+)$', (work / 'gift-assets' / (name + '.prefab')).read_text('utf8')))
    keys.update(('ui_npc_change_tag', 'ui_npc_input_tag', 'ui_npc_change_tag_success',
        'ui_npc_detail_give_gift_max_level_tip', 'ui_npc_detail_select_gift_level_limit_tip',
        'ui_npc_detail_select_gift_overflow_tip', 'ui_npc_detail_gift_unselected_tip',
        'ui_npc_detail_give_gift_unselected_tip', 'ui_npc_detail_give_gift_overflow_tip'))
    texts, sources = language(keys)
    if set(texts) != keys:
        raise ValueError('Original gift page language missing: ' + repr(keys - texts.keys()))
    for relative, expected in sources.items():
        if sha((ROOT / relative).read_bytes()) != expected:
            raise ValueError('Gift page language input changed: ' + relative)
        receipt['inputs'][relative] = expected
    original_text = work / 'gift-assets/partner-gift-texts.json'
    target = work / 'gift-assets/partner-gift-controller-texts.json'
    original_text = original_text if original_text.is_file() else target
    payload = json.loads(original_text.read_bytes())
    merged = {row['key']: row['text'] for row in payload['texts']}
    merged.update(texts)
    payload['texts'] = [dict(key=key, text=value) for key, value in sorted(merged.items())]
    target.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    metadata = Path(str(target) + '.meta')
    metadata.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:partner-gift-controller-texts').hexdigest()
        + '\nTextScriptImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
    receipt['outputs'][target.name] = sha(target.read_bytes())
    receipt['outputs'][metadata.name] = sha(metadata.read_bytes())
    if original_text != target:
        for old in (original_text, Path(str(original_text) + '.meta')):
            receipt['outputs'].pop(old.name)
            old.unlink()
    receipt['scope'] = 'Complete original gift subtree, gift icons, property template and upgrade/reduce/hint attachments staged; production host pending.'
    final_path = work / 'partner-gift-controller-native-source-manifest.json'
    final_path.write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    if path != final_path:
        path.unlink()


def install_staged(work):
    from prepare_partner_ui_helpers import meta
    from prepare_partner_gift_controller_ui import DEST
    receipt = json.loads((work / 'partner-gift-controller-native-source-manifest.json').read_bytes())
    code = json.loads((work / 'partner-gift-controller-ui-source-manifest.json').read_bytes())
    if receipt['generator_sha256'] != sha(Path(__file__).read_bytes()):
        raise ValueError('Native gift generator changed after staging')
    if code['generator_sha256'] != sha((ROOT / 'scripts/prepare_partner_gift_controller_ui.py').read_bytes()):
        raise ValueError('Gift controller generator changed after staging')
    for relative, expected in dict(receipt['inputs'], **code['source_sha256']).items():
        if sha((ROOT / relative).read_bytes()) != expected:
            raise ValueError('Gift controller input changed: ' + relative)
    for relative, expected in code['output_sha256'].items():
        if sha((work / 'gift-controller-code' / Path(relative).name).read_bytes()) != expected:
            raise ValueError('Gift controller code bytes changed: ' + relative)
    partners = ROOT / 'unity/Assets/Official/Partners'
    existing = {}
    for metadata in partners.rglob('*.meta'):
        asset = Path(str(metadata)[:-5])
        if asset.is_file():
            identity = guid(metadata)
            if identity in existing:
                raise ValueError('Existing partner GUID collision: ' + identity)
            existing[identity] = asset
    destination = partners / 'UI/GiftController'
    plan, reused = [], []
    repaired_targets = {}
    for relative, expected in receipt['outputs'].items():
        path = work / 'gift-assets' / relative
        if sha(path.read_bytes()) != expected:
            raise ValueError('Native gift bytes changed: ' + relative)
        if relative.endswith('.meta'):
            continue
        target = existing.get(guid(Path(str(path) + '.meta')), destination / relative)
        if target.is_file():
            if target.read_bytes() != path.read_bytes():
                repair = receipt.get('empty_language_key_repairs', {}).get(relative)
                if not repair or sha(target.read_bytes()) != repair['previous_sha256'] or target.read_bytes().replace(b'  myKey: null\n', b"  myKey: ''\n") != path.read_bytes():
                    raise ValueError('Existing gift asset identity has different bytes: ' + str(target))
                repaired_targets[target] = repair['previous_sha256']
            else:
                reused.append(target.relative_to(ROOT).as_posix())
        source_meta, target_meta = Path(str(path) + '.meta'), Path(str(target) + '.meta')
        if target_meta.is_file() and target_meta.read_bytes() != source_meta.read_bytes():
            if target_meta.read_bytes().replace(b'\r\n', b'\n') != source_meta.read_bytes().replace(b'\r\n', b'\n'):
                raise ValueError('Existing gift metadata differs beyond line endings: ' + str(target_meta))
            source_meta = target_meta
        plan.extend(((path, target), (source_meta, target_meta)))
    for row in receipt['script_bindings'].values():
        path = ROOT / row['path']
        if sha(path.read_bytes()) != row['sha256'] or sha(Path(str(path) + '.meta').read_bytes()) != row['meta_sha256']:
            raise ValueError('Native gift script changed: ' + row['path'])
        if path.parent == work / 'gift-controller-code':
            row['path'] = (DEST / path.name).as_posix()
    for relative in code['output_sha256']:
        plan.append((work / 'gift-controller-code' / Path(relative).name, ROOT / relative))
    installed, folders = {}, {}
    for source, target in plan:
        if target.is_file() and target.read_bytes() != source.read_bytes() and sha(target.read_bytes()) != repaired_targets.get(target):
            raise ValueError('Do not overwrite another session asset: ' + str(target))
    for source, target in plan:
        if not target.is_file() or target in repaired_targets:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(source.read_bytes())
        installed[target.relative_to(ROOT).as_posix()] = sha(target.read_bytes())
        parent = target.parent
        while parent == destination or parent.is_relative_to(destination) or parent == ROOT / DEST:
            metadata = Path(str(parent) + '.meta')
            if not metadata.is_file():
                meta(parent, folder=True)
                metadata.write_bytes(metadata.read_bytes().replace(b'\r\n', b'\n'))
            folders[metadata.relative_to(ROOT).as_posix()] = sha(metadata.read_bytes())
            parent = parent.parent
    receipt.update(installed_output_sha256=installed, installed_folder_sha256=folders, reused=reused,
        installed_root=destination.relative_to(ROOT).as_posix(), controller_source=code,
        scope='Original gift panel subtree/controller/level content/blue dot installed; production provider, complete detail host and Player verification remain pending.')
    (ROOT / 'assets/reference/partner-gift-controller-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed native gift controller:', len(installed), 'files; reused', len(reused), 'assets')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--install-staged', action='store_true')
    args = parser.parse_args()
    if args.install_staged:
        install_staged(validation_directory())
    else:
        PartnerGiftControllerBinding(validation_directory()).build()
