"""Bind the complete current official friendship dialog, retaining its native hierarchy."""
import json
import hashlib
import re
from pathlib import Path

from official_apk import ROOT
from prepare_partner_card_native import guid, script_id, sha, language
from prepare_partner_gift_native import PartnerGiftBinding
from validation_workspace import validation_directory


class PartnerFriendshipDialogBinding(PartnerGiftBinding):
    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT / 'unity/Assets/Game/Runtime/Official'
        paths = {
            'BgDialogElement': runtime / 'WorldUI/WorldBgDialogElement.cs',
            'LayoutElementClamper': runtime / 'WorldUI/WorldLayoutElementClamper.cs',
            'MirrorImage': runtime / 'WorldUI/MirrorImage.cs',
            'UIBloomGroup': runtime / 'StoryBubbles/UIBloomGroup.cs',
            'UGUIJszx.VerticalLayoutGroupJSZX': runtime / 'WorldUI/VerticalLayoutGroupJSZX.cs',
            'NpcFriendshipDialog': self.work / 'dialog-code/NpcFriendshipDialog.cs',
        }
        for name in ('PopupWindow', 'PopupWindowSound', 'PreviewTool'):
            paths[name] = runtime / 'World/Map13MiniMapWindows' / (name + '.cs')
        for full_name, path in paths.items():
            namespace, _, name = full_name.rpartition('.')
            metadata = Path(str(path) + '.meta')
            pointer = dict(fileID=11500000, guid=guid(metadata), type=3)
            self.scripts[full_name] = pointer
            self.script_sources[full_name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(metadata.read_bytes()))
            self.class_ids[script_id(namespace, name)] = pointer
            self.script_guids.add(pointer['guid'])

    def friendship_templates(self):
        self.recovery.recover('NpcFriendshipDialog', self.work / 'partner-native')
        self.recovery.finalize()
        for record in self.recovery.proof['prefabs']:
            path = ROOT / record['nativeSource']
            self.inputs[record['nativeSource']] = sha(path.read_bytes())
            self.inputs[record['nativeSource'] + '.meta'] = sha(Path(str(path) + '.meta').read_bytes())
        cache = self.recovery.proof['component_cache']
        self.inputs[cache['path']] = cache['sha256']
        self.template_selection = dict(scope='Complete original dialog; no hierarchy extraction or geometry changes.')
        return [('NpcFriendshipDialog', self.recovery.outputs[0][2])]

    def build(self):
        super().build()
        receipt_path = self.work / 'partner-gift-native-source-manifest.json'
        receipt = json.loads(receipt_path.read_bytes())
        receipt['generator_sha256'] = sha(Path(__file__).read_bytes())
        keys = set(re.findall(r'(?m)^  myKey: (ui_\S+)$', (self.work / 'gift-assets/NpcFriendshipDialog.prefab').read_text('utf8')))
        keys.update(('ui_rule_npc_star_god_title', 'ui_rule_npc_normal_title'))
        texts, sources = language(keys)
        if set(texts) != keys:
            raise ValueError('Original dialog language entries missing: ' + repr(keys - texts.keys()))
        records = {row['name']: row for row in self.recovery.apk.source.manifest('WwiseBnk', self.recovery.apk.archive)['bundles']}
        for relative, expected in sources.items():
            raw = (ROOT / relative).read_bytes()
            row = records[Path(relative).name]
            if len(raw) != row['size'] or hashlib.md5(raw).hexdigest() != row['hash'] or sha(raw) != expected:
                raise ValueError('Selected dialog language input changed: ' + relative)
            receipt['inputs'][relative] = expected
        path = self.work / 'gift-assets/partner-gift-texts.json'
        payload = json.loads(path.read_bytes())
        combined = {row['key']: row['text'] for row in payload['texts']}
        combined.update(texts)
        payload['texts'] = [dict(key=key, text=value) for key, value in sorted(combined.items())]
        target = path.with_name('partner-friendship-dialog-texts.json')
        target.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        metadata = Path(str(target) + '.meta')
        metadata.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:partner-friendship-dialog-texts').hexdigest()
                            + '\nTextScriptImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
        for old in (path, Path(str(path) + '.meta')):
            receipt['outputs'].pop(old.name)
            old.unlink()
        for entry in (target, metadata):
            receipt['outputs'][entry.name] = sha(entry.read_bytes())
        base = ROOT / 'scripts/prepare_partner_gift_native.py'
        receipt['inputs'][base.relative_to(ROOT).as_posix()] = sha(base.read_bytes())
        used_scripts = set()
        for relative in receipt['outputs']:
            path = self.work / 'gift-assets' / relative
            if path.suffix != '.meta' and path.read_bytes().startswith(b'%YAML'):
                used_scripts.update(re.findall(r'm_Script: \{[^\n}]*guid: ([0-9a-f]{32})', path.read_text('utf8')))
        receipt['script_bindings'] = {name: row for name, row in receipt['script_bindings'].items()
                                      if row['pointer']['guid'] in used_scripts}
        for relative in ('client-source/BgDialogElement.cs', 'client-source/LayoutElementClamper.cs',
                         'client-source/UIBloomGroup.cs', 'client-source/UIBloomGroupBase.cs',
                         'framework-source/PreviewTool.cs', 'framework-firstpass-source/PopupWindow.cs',
                         'framework-firstpass-source/PopupWindowSound.cs', 'framework-firstpass-source/MirrorImage.cs',
                         'framework-source/UGUIJszx/VerticalLayoutGroupJSZX.cs',
                         'framework-source/UGUIJszx/HorizontalOrVerticalLayoutGroupJSZX.cs'):
            path = ROOT / '.tools/source-packages/official-code' / relative
            raw = path.read_bytes()
            selected = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code' / relative
            if raw != selected.read_bytes():
                raise ValueError('Selected shared dialog source differs: ' + relative)
            receipt['inputs'][path.relative_to(ROOT).as_posix()] = sha(raw)
        receipt['scope'] = 'Complete original friendship dialog staged; local provider/window host pending.'
        (self.work / 'partner-friendship-dialog-native-source-manifest.json').write_text(
            json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        receipt_path.unlink()


def install_staged(work):
    receipt = json.loads((work / 'partner-friendship-dialog-native-source-manifest.json').read_bytes())
    controller = json.loads((work / 'partner-friendship-dialog-ui-source-manifest.json').read_bytes())
    for relative, expected in {**receipt['inputs'], **controller['source_sha256']}.items():
        if sha((ROOT / relative).read_bytes()) != expected:
            raise ValueError('Dialog input changed: ' + relative)
    if receipt['generator_sha256'] != sha(Path(__file__).read_bytes()):
        raise ValueError('Rebuild native dialog after generator changes')
    code_generator = ROOT / 'scripts/prepare_partner_friendship_dialog_ui.py'
    if controller['generator_sha256'] != sha(code_generator.read_bytes()):
        raise ValueError('Rebuild dialog controller after generator changes')
    for binding in receipt['script_bindings'].values():
        path = ROOT / binding['path']
        if sha(path.read_bytes()) != binding['sha256'] or sha(Path(str(path) + '.meta').read_bytes()) != binding['meta_sha256']:
            raise ValueError('Dialog script binding changed: ' + str(path))
    partners = ROOT / 'unity/Assets/Official/Partners'
    existing = {}
    for metadata in partners.rglob('*.meta'):
        asset = Path(str(metadata)[:-5])
        if asset.is_file():
            identity = guid(metadata)
            if identity in existing:
                raise ValueError('Existing partner GUID collision: ' + identity)
            existing[identity] = asset
    destination = partners / 'UI/FriendshipDialog'
    plan, reused = [], []
    for relative, expected in receipt['outputs'].items():
        original = work / 'gift-assets' / relative
        if sha(original.read_bytes()) != expected:
            raise ValueError('Staged dialog resource changed: ' + relative)
        if relative.endswith('.meta'):
            continue
        target = existing.get(guid(Path(str(original) + '.meta')), destination / relative)
        if target.is_file():
            if target.read_bytes() != original.read_bytes():
                raise ValueError('Existing dialog asset bytes differ: ' + str(target))
            reused.append(target.relative_to(ROOT).as_posix())
        plan.extend(((original, target), (Path(str(original) + '.meta'), Path(str(target) + '.meta'))))
    for relative, expected in controller['output_sha256'].items():
        original = work / 'dialog-code' / Path(relative).name
        if sha(original.read_bytes()) != expected:
            raise ValueError('Staged dialog controller changed: ' + relative)
        target = ROOT / relative
        if target.is_file() and target.read_bytes() != original.read_bytes():
            raise ValueError('Existing dialog controller bytes differ: ' + relative)
        plan.append((original, target))
    installed, folders = {}, {}
    for original, target in plan:
        if not target.is_file():
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(original.read_bytes())
        installed[target.relative_to(ROOT).as_posix()] = sha(target.read_bytes())
        parent = target.parent
        while parent == destination or parent.is_relative_to(destination) or parent.name == 'FriendshipDialog':
            metadata = Path(str(parent) + '.meta')
            if not metadata.is_file():
                metadata.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs:partner-friendship-dialog-folder:' + parent.relative_to(ROOT).as_posix()).encode()).hexdigest()
                    + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n', 'utf8', newline='\n')
            folders[metadata.relative_to(ROOT).as_posix()] = sha(metadata.read_bytes())
            parent = parent.parent
    staged_script = receipt['script_bindings']['NpcFriendshipDialog']
    staged_script['path'] = 'unity/Assets/Game/Runtime/Official/PartnersUI/FriendshipDialog/NpcFriendshipDialog.cs'
    receipt.update(controller=controller, installed_output_sha256=installed, folder_meta_sha256=folders, reused_assets=reused,
        scope='Complete native friendship dialog/controller; production local provider, populated gift icons and audio playback remain pending.')
    (ROOT / 'assets/reference/partner-friendship-dialog-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed native dialog files:', len(installed), 'reused assets:', len(reused))


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    if parser.parse_args().install:
        install_staged(validation_directory())
    else:
        PartnerFriendshipDialogBinding(validation_directory()).build()
