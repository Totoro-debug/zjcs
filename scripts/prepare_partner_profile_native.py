"""Recover the exact selected NPC profile subtree and its native dependencies."""
import argparse
import copy
import hashlib
import json
from pathlib import Path

from prepare_partner_card_native import ROOT, guid, sha, script_id
from prepare_partner_roster_native import PartnerRosterBinding
from prepare_partner_gift_native import PartnerGiftBinding
from prepare_partner_panel_native import PartnerPanelBinding
from prepare_current_partner_native import documents
from official_manifest import read_asset_manifest
from validation_workspace import validation_directory


class PartnerProfileBinding(PartnerRosterBinding):
    def __init__(self, work, code):
        self.code = code
        super().__init__(work)

    def build(self):
        return PartnerGiftBinding.build(self)

    def load(self, source_guid):
        obj = self.objects.get(source_guid)
        if source_guid not in self.redirects and obj is not None and obj.type.name == 'AnimatorController':
            return PartnerPanelBinding.controller(self, source_guid, obj)
        return super().load(source_guid)

    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT / 'unity/Assets/Game/Runtime/Official'
        paths = {name: self.code / (name + '.cs') for name in (
            'NpcDetailPanelProfileContent', 'NpcDetailPanelProfileContentTabItem')}
        paths.update({
            'ItemIconAttachmentCell': runtime / 'ProfessionUI/ItemIconAttachmentCell.cs',
            'UnityEngine.UI.Ext.TabGroup': runtime / 'WorldUI/WorldTabGroup.cs',
            'UnityEngine.UI.Ext.TabItem': runtime / 'WorldUI/WorldTabItem.cs',
            'UnityEngine.UI.Ext.TabObjectTransition': runtime / 'PartnersUI/TabObjectTransition.cs',
            'ClampingScrollRect': runtime / 'WorldUI/WorldClampingScrollRect.cs',
            'UIBloomGraphicElement': runtime / 'ProfessionUI/UIBloomGraphicElement.cs',
            'PreviewTool': runtime / 'World/Map13MiniMapWindows/PreviewTool.cs',
            'CountRedDotImage': runtime / 'World/Map13MiniMapWindows/CountRedDotImage.cs',
            'UnityEngine.UI.Ext.TabAnimatorTransition': runtime / 'WorldUI/WorldTabAnimatorTransition.cs',
        })
        for full_name, path in paths.items():
            namespace, _, name = full_name.rpartition('.')
            meta = Path(str(path) + '.meta')
            ref = dict(fileID=11500000, guid=guid(meta), type=3)
            self.scripts[full_name] = ref
            self.script_sources[full_name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=ref,
                sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))
            self.class_ids[script_id(namespace, name)] = ref
            self.script_guids.add(ref['guid'])

    def friendship_templates(self):
        self.recovery.recover('NpcDetailPanel', self.work / 'partner-native')
        records = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        attachment_record, = [row for row in records if row['guid'] == '9717aac89f12f8c4bb521b6b3f66a83a']
        attachment, = [ref.deref() for name, ref in self.recovery.apk.container(attachment_record['bundle'])
            if name.casefold() == attachment_record['path'].casefold()]
        attachment_name = attachment.read().m_Name
        self.recovery.recover(attachment_name, self.work / 'partner-native', bundle=attachment_record['bundle'],
            native_relative=attachment_record['path'].removeprefix('Assets/'))
        self.recovery.finalize()
        native, _, detail, record = self.recovery.outputs[0]
        consumer, = [row for row in detail['scripts'] if row['type'] == 'NpcDetailPanelProfileContent']
        root = detail['documents'][consumer['nativeFileID']]['MonoBehaviour']['m_GameObject']['fileID']
        selected = set()

        def visit(go):
            selected.add(go)
            components = detail['documents'][go]['GameObject']['m_Component']
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
                    raise ValueError('Profile reference leaves original subtree: ' + repr(value))
                for entry in value.values(): check(entry)
            elif isinstance(value, list):
                for entry in value: check(entry)

        check(panel['documents'])
        self.template_selection = dict(consumer=consumer['original'], original_parent=old_parent,
            root=root, mapped_objects=len(selected), attachment=dict(type=2, name=attachment_name, asset=attachment_record),
            adaptation='Detach original profile subtree root parent only; recover the configured original GotTip attachment.')
        self.inputs[record['nativeSource']] = sha(native.read_bytes())
        self.inputs[record['nativeSource'] + '.meta'] = sha(Path(str(native) + '.meta').read_bytes())
        cache = self.recovery.proof['component_cache']
        self.inputs[cache['path']] = cache['sha256']
        attachment_native, _, attachment_data, attachment_receipt = self.recovery.outputs[1]
        self.inputs[attachment_receipt['nativeSource']] = sha(attachment_native.read_bytes())
        self.inputs[attachment_receipt['nativeSource'] + '.meta'] = sha(Path(str(attachment_native) + '.meta').read_bytes())
        return [('NpcDetailProfileContent', panel), (attachment_name, attachment_data)]


def finish(work):
    proof = json.loads((work / 'partner-gift-native-source-manifest.json').read_bytes())
    path = work / 'gift-assets/NpcDetailProfileContent.prefab'
    raw = path.read_bytes()
    native_path, = [ROOT / relative for relative in proof['inputs'] if relative.endswith('/NpcDetailPanel.prefab')]
    native = documents(native_path.read_text('utf8'))
    import re
    fields = [int(file_id) for file_id, body in re.findall(rb'^--- !u!114 &(-?\d+)\n(.*?)(?=^--- !u!|\Z)', raw, re.M | re.S)
        if b'  myKey: null\n' in body]
    if any(native[file_id]['MonoBehaviour']['myKey'] != '' for file_id in fields):
        raise ValueError('Only restore original empty language keys')
    restored = raw.replace(b'  myKey: null\n', b"  myKey: ''\n")
    path.write_bytes(restored)
    proof['outputs'][path.name] = sha(restored)
    proof['empty_language_key_restoration'] = fields
    proof['generator_sha256'] = sha(Path(__file__).read_bytes())
    proof['inputs']['scripts/prepare_partner_gift_native.py'] = sha((ROOT / 'scripts/prepare_partner_gift_native.py').read_bytes())
    for name in ('prepare_partner_roster_native.py', 'prepare_partner_panel_native.py', 'partner_panel_animator_binding.py'):
        proof['inputs']['scripts/' + name] = sha((ROOT / 'scripts' / name).read_bytes())
    (work / 'partner-profile-native-source-manifest.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


def install(work):
    proof = json.loads((work / 'partner-profile-native-source-manifest.json').read_bytes())
    code = json.loads((work / 'partner-profile-ui-source-manifest.json').read_bytes())
    for relative, expected in dict(proof['inputs'], **code['inputs']).items():
        if sha((ROOT / relative).read_bytes()) != expected: raise ValueError('Profile input changed: ' + relative)
    if proof['generator_sha256'] != sha(Path(__file__).read_bytes()): raise ValueError('Rebuild profile native generator')
    if code['generator_sha256'] != sha((ROOT / 'scripts/prepare_partner_profile_ui.py').read_bytes()): raise ValueError('Rebuild profile code generator')
    previous_path = ROOT / 'assets/reference/partner-profile-native-source-manifest.json'
    previous = json.loads(previous_path.read_bytes()).get('installed_output_sha256', {}) if previous_path.is_file() else {}
    existing = {}
    for meta in (ROOT / 'unity/Assets/Official/Partners').rglob('*.meta'):
        path = Path(str(meta)[:-5])
        if path.is_file():
            key = guid(meta)
            if key in existing: raise ValueError('Partner GUID collision')
            existing[key] = path
    destination = ROOT / 'unity/Assets/Official/Partners/UI/Profile'
    code_destination = ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI/Roster/Profile'
    plan, installed, reused = [], {}, []
    for relative, expected in proof['outputs'].items():
        source = work / 'gift-assets' / relative
        if sha(source.read_bytes()) != expected: raise ValueError('Profile staged native changed: ' + relative)
        if relative.endswith('.meta') or relative == 'partner-gift-texts.json': continue
        target = existing.get(guid(Path(str(source) + '.meta')), destination / relative)
        if target.is_file(): reused.append(target.relative_to(ROOT).as_posix())
        plan.extend(((source, target), (Path(str(source) + '.meta'), Path(str(target) + '.meta'))))
    for relative, expected in code['outputs'].items():
        source = work / 'profile-code' / Path(relative).name
        if sha(source.read_bytes()) != expected: raise ValueError('Profile staged code changed: ' + relative)
        plan.append((source, ROOT / relative))
    for source, target in plan:
        if target.is_file() and target.read_bytes().replace(b'\r\n', b'\n') != source.read_bytes().replace(b'\r\n', b'\n'):
            if sha(target.read_bytes()) != previous.get(target.relative_to(ROOT).as_posix()):
                raise ValueError('Refusing unrecorded profile overwrite: ' + str(target))
    for source, target in plan:
        if not target.is_file() or sha(target.read_bytes()) != sha(source.read_bytes()):
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(source.read_bytes())
        installed[target.relative_to(ROOT).as_posix()] = sha(target.read_bytes())
    for folder in (destination, code_destination):
        meta = Path(str(folder) + '.meta')
        if not meta.is_file(): meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs-profile-folder:' + folder.relative_to(ROOT).as_posix()).encode()).hexdigest()
            + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
        installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
    for binding in proof['script_bindings'].values():
        source = ROOT / binding['path']
        if source.parent == work / 'profile-code':
            target, = [relative for relative in code['outputs'] if Path(relative).name == source.name]
            binding['path'] = target
        source = ROOT / binding['path']
        if sha(source.read_bytes()) != binding['sha256'] or sha(Path(str(source) + '.meta').read_bytes()) != binding['meta_sha256']:
            raise ValueError('Installed profile script binding differs: ' + binding['path'])
    proof.update(controller_source=code, installed_output_sha256=installed, reused=reused,
        scope='Exact original NPC profile region with explicit native dependencies; formal host and Windows Player pending.')
    previous_path.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed original profile region:', len(installed), 'files; reused', len(reused), 'assets')


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    work = validation_directory()
    if args.install: install(work)
    else:
        PartnerProfileBinding(work, work / 'profile-code').build()
        finish(work)


if __name__ == '__main__': main()
