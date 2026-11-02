"""Restore the original dialogue entry subtree, retaining all serialized objects."""
import copy
import argparse
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_current_partner_native import identity, pointer as source_pointer
from prepare_partner_card_native import guid, sha, script_id
from prepare_partner_input_dialog import PartnerInputBinding, metadata
from prepare_partner_gift_native import PartnerGiftBinding
from validation_workspace import validation_directory


class PartnerDialogueEntryBinding(PartnerInputBinding):
    def register_scripts(self):
        super().register_scripts()
        for name in ('NpcPanelDialogueList', 'NpcPanelDialogueListNpcItem', 'NpcPanelDialogueListDialogueItem'):
            path = ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI/Roster' / (name + '.cs')
            meta = Path(str(path) + '.meta')
            pointer = dict(fileID=11500000, guid=guid(meta), type=3)
            self.scripts[name] = pointer
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))
            self.class_ids[script_id('', name)] = pointer
            self.script_guids.add(pointer['guid'])

    def replace(self, value):
        if isinstance(value, dict) and set(value) == {'originalCAB', 'originalPathID'}:
            key = value['originalCAB'].lower() + ':' + str(value['originalPathID'])
            found = [dict(guid=ref[0], fileID=ref[1], type=2) for ref, obj in self.original_pointers.items()
                     if identity(obj) == key]
            unique = {(row['guid'], row['fileID']) for row in found}
            if len(unique) != 1:
                raise ValueError('Recover exact original dialogue dependency: ' + key)
            return super().replace(found[0])
        return super().replace(value)

    def friendship_templates(self):
        self.recovery.recover('NpcPanel', self.work / 'partner-native')
        self.recovery.finalize()
        native, _, panel, record = self.recovery.outputs[-1]
        host, = [row for row in panel['scripts'] if row['type'] == 'NpcPanel']
        list_root = panel['documents'][host['nativeFileID']]['MonoBehaviour']['mNpcDialogueListGo']['fileID']

        def component(go, name):
            items = {row['component']['fileID'] for row in panel['documents'][go]['GameObject']['m_Component']}
            result, = [row for row in panel['scripts'] if row['nativeFileID'] in items and row['type'] == name]
            return result, panel['documents'][result['nativeFileID']]['MonoBehaviour']

        _, list_fields = component(list_root, 'NpcPanelDialogueList')
        list_controller, _ = component(list_root, 'NpcPanelDialogueList')
        owner = self.original(list_controller['original'])
        original_prefab = source_pointer(self.recovery.fields(owner)['mItemPrefab'], owner)
        self.recovery.recover(original_prefab.read().m_Name, self.work / 'partner-native')
        self.recovery.finalize()
        native, _, panel, record = self.recovery.outputs[-1]
        _, npc_fields = component(panel['root'], 'NpcPanelDialogueListNpcItem')
        root = npc_fields['mDialogueItemPrefab']['fileID']
        controller, _ = component(root, 'NpcPanelDialogueListDialogueItem')
        selected = set()

        def visit(go):
            selected.add(go)
            components = panel['documents'][go]['GameObject']['m_Component']
            selected.update(row['component']['fileID'] for row in components)
            transform = next(iter(panel['documents'][components[0]['component']['fileID']].values()))
            for child in transform['m_Children']:
                visit(next(iter(panel['documents'][child['fileID']].values()))['m_GameObject']['fileID'])

        visit(root)
        data = copy.deepcopy(panel)
        data['root'] = root
        data['documents'] = {key: value for key, value in data['documents'].items() if key in selected}
        data['scripts'] = [row for row in data['scripts'] if row['nativeFileID'] in selected]
        data['objectMap'] = {key: value for key, value in data['objectMap'].items() if value in selected}
        transform_id = data['documents'][root]['GameObject']['m_Component'][0]['component']['fileID']
        transform = next(iter(data['documents'][transform_id].values()))
        parent = transform['m_Father']
        transform['m_Father'] = dict(fileID=0)

        def restore(source, target):
            if isinstance(source, dict) and isinstance(target, dict):
                for key in source.keys() & target.keys():
                    if target[key] is None and source[key] in ('', [], {}): target[key] = source[key]
                    else: restore(source[key], target[key])
            elif isinstance(source, list) and isinstance(target, list) and len(source) == len(target):
                for left, right in zip(source, target): restore(left, right)

        for key, file_id in data['objectMap'].items():
            owner = self.original(key)
            fields = next(iter(data['documents'][file_id].values()))
            original = self.recovery.fields(owner)
            restore(original, fields)
            self.learn(original, fields, owner)
        for key, ref in data['external'].items():
            self.objects[ref['guid']] = self.original(key)
        self.template_selection = dict(consumer=controller['original'], original_root=root,
            mapped_objects=len(selected), original_parent=parent,
            adaptation='Exact dialogue entry subtree; detach root parent only. Complete original fields and geometry retained.')
        for path in (native, Path(str(native) + '.meta')):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        cache = self.recovery.proof['component_cache']
        self.inputs[cache['path']] = cache['sha256']
        return [('NpcPanelDialogueEntry', data)]

    def build(self):
        PartnerGiftBinding.build(self)
        path = self.work / 'partner-gift-native-source-manifest.json'
        receipt = json.loads(path.read_bytes())
        receipt['generator_sha256'] = sha(Path(__file__).read_bytes())
        for name in ('prepare_partner_input_dialog.py', 'prepare_partner_roster_native.py',
                     'prepare_partner_gift_native.py', 'partner_roster_animator_binding.py'):
            source = ROOT / 'scripts' / name
            receipt['inputs'][source.relative_to(ROOT).as_posix()] = sha(source.read_bytes())
        used = set()
        for name in receipt['outputs']:
            source = self.work / 'gift-assets' / name
            if source.suffix != '.meta' and source.read_bytes().startswith(b'%YAML'):
                used.update(re.findall(r'm_Script: \{[^\n}]*guid: ([0-9a-f]{32})', source.read_text('utf8')))
        receipt['script_bindings'] = {name: value for name, value in receipt['script_bindings'].items()
            if value['pointer']['guid'] in used}
        (self.work / 'partner-dialogue-entry-native-source-manifest.json').write_text(
            json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


def install(work):
    receipt = json.loads((work / 'partner-dialogue-entry-native-source-manifest.json').read_bytes())
    if receipt['generator_sha256'] != sha(Path(__file__).read_bytes()):
        raise ValueError('Rebuild original entry after generator changes')
    for path, expected in receipt['inputs'].items():
        if sha((ROOT / path).read_bytes()) != expected: raise ValueError('Input changed: ' + path)
    for name, row in receipt['script_bindings'].items():
        path = ROOT / row['path']
        if sha(path.read_bytes()) != row['sha256'] or sha(Path(str(path) + '.meta').read_bytes()) != row['meta_sha256']:
            raise ValueError('Runtime binding changed: ' + name)
    base = ROOT / 'unity/Assets/Official/Partners'
    existing = {}
    for meta in base.rglob('*.meta'):
        path = Path(str(meta)[:-5])
        if path.is_file():
            key = guid(meta)
            if key in existing: raise ValueError('Duplicate partner GUID: ' + key)
            existing[key] = path
    destination = base / 'UI/DialogueEntry'
    planned, installed, reused = [], {}, []
    for name, expected in receipt['outputs'].items():
        source = work / 'gift-assets' / name
        if sha(source.read_bytes()) != expected: raise ValueError('Staged original entry changed: ' + name)
        if name.endswith('.meta'): continue
        key = guid(Path(str(source) + '.meta'))
        target = existing.get(key, destination / name)
        if target.exists():
            if source.read_bytes() != target.read_bytes(): raise ValueError('Preserve differing original asset: ' + str(target))
            reused.append(target.relative_to(ROOT).as_posix())
        else:
            planned.extend(((source, target), (Path(str(source) + '.meta'), Path(str(target) + '.meta'))))
        for left, right in ((source, target), (Path(str(source) + '.meta'), Path(str(target) + '.meta'))):
            installed[right.relative_to(ROOT).as_posix()] = sha((right if right.exists() else left).read_bytes())
    for source, target in planned:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(source.read_bytes())
    for folder in [destination, *[path for path in destination.rglob('*') if path.is_dir()]]:
        meta = metadata(folder, 'DefaultImporter', True)
        installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfficialPartnerDialogueAssetCatalog.cs'
    script_meta = metadata(script)
    catalog = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerDialogueAssetCatalog.asset'
    pointer = receipt['roots']['NpcPanelDialogueEntry']
    raw = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'
    raw += '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n'
    raw += '  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
    raw += '  m_Script: {fileID: 11500000, guid: ' + guid(script_meta) + ', type: 3}\n'
    raw += "  m_Name: OfficialPartnerDialogueAssetCatalog\n  m_EditorClassIdentifier: ''\n"
    raw += '  entryPrefab: {fileID: ' + str(pointer['fileID']) + ', guid: ' + pointer['guid'] + ', type: 3}\n'
    catalog.write_text(raw, 'utf8', newline='\n')
    for path in (catalog, metadata(catalog, 'NativeFormatImporter'), script, script_meta):
        installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    receipt.update(installed_output_sha256=installed, reused_assets=reused)
    (ROOT / 'assets/reference/partner-dialogue-entry-native-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed original dialogue entry:', len(installed), 'files; reused', len(reused), 'assets', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    work = validation_directory()
    if parser.parse_args().install: install(work)
    else: PartnerDialogueEntryBinding(work).build()
