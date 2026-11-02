"""Restore the selected original ConfirmDialog for partner gift overflow."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_partner_card_native import guid, identity, language, script_id, sha
from prepare_partner_gift_native import PartnerGiftBinding
from prepare_partner_input_dialog import PartnerInputBinding, metadata
from prepare_current_partner_native import documents
from validation_workspace import validation_directory

RECEIPT = ROOT / 'assets/reference/partner-confirm-dialog-source-manifest.json'
DESTINATION = ROOT / 'unity/Assets/Official/Partners/UI/Confirm'
CATALOG = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerConfirmAssetCatalog.asset'
NEW_SCRIPTS = (
    'Official/WorldUI/WorldAutoAlignForSingleMultiLineTextContent.cs',
    'Offline/PartnersUI/OfficialPartnerConfirmAssetCatalog.cs',
)


class PartnerConfirmBinding(PartnerInputBinding):
    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT / 'unity/Assets/Game/Runtime/Official'
        paths = {name: runtime / 'WorldUI' / ('World' + name + '.cs') for name in (
            'ConfirmDialog', 'AutoAlignForSingleMultiLineText', 'AutoAlignForSingleMultiLineTextContent',
            'LockMaskWithTime', 'ShowRemainTimeInfo', 'PreviewTool', 'SafeArea')}
        paths.update({name: runtime / 'ProfessionUI' / (name + '.cs')
            for name in ('HyperLinkClickListener', 'TopBarHandler', 'StoryNodeRegister')})
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
        self.recovery.recover('ConfirmDialog', self.work / 'partner-native',
            bundle='defaultpackage_assets_prefabs_ui_confirmdialog.bundle',
            native_relative='Prefabs/UI/ConfirmDialog/ConfirmDialog.prefab')
        self.recovery.finalize()
        native, _, panel, record = self.recovery.outputs[0]
        for item in (native, Path(str(native) + '.meta')):
            self.inputs[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
        cache = self.recovery.proof['component_cache']
        self.inputs[cache['path']] = cache['sha256']
        self.empty_strings = []

        def restore_empty(source, converted, address, owner):
            if isinstance(source, dict) and isinstance(converted, dict):
                for key in source.keys() & converted.keys():
                    if converted[key] is None and (source[key] == '' or source[key] == [] or source[key] == {}):
                        converted[key] = source[key]
                        self.empty_strings.append(dict(original=owner, field=address + '/' + key, value=source[key]))
                    else:
                        restore_empty(source[key], converted[key], address + '/' + key, owner)
            elif isinstance(source, list) and isinstance(converted, list) and len(source) == len(converted):
                for index, (left, right) in enumerate(zip(source, converted)):
                    restore_empty(left, right, address + '/' + str(index), owner)

        for key, pointer in panel['external'].items():
            self.objects[pointer['guid']] = self.original(key)
        for key, file_id in panel['objectMap'].items():
            owner = self.original(key)
            self.learn(self.recovery.fields(owner), next(iter(panel['documents'][file_id].values())), owner)
        for script in panel['scripts']:
            owner = self.original(script['original'])
            fields = panel['documents'][script['nativeFileID']]['MonoBehaviour']
            original = self.recovery.fields(owner)
            restore_empty(original, fields, '', script['original'])
            if script['type'] == 'PopupWindowSound':
                for field in ('ShowSound', 'HideSound'):
                    fields[field] = self.sound(original[field], owner)
        self.template_selection = dict(scope='Complete original ConfirmDialog hierarchy and fields.',
            source=record['originalRoot'], mapped_objects=len(panel['objectMap']))
        return [('ConfirmDialog', panel)]

    def build(self):
        PartnerGiftBinding.build(self)
        old_receipt = self.work / 'partner-gift-native-source-manifest.json'
        receipt = json.loads(old_receipt.read_bytes())
        folder = self.work / 'gift-assets'
        keys = {doc['MonoBehaviour']['myKey'] for doc in documents(
            (folder / 'ConfirmDialog.prefab').read_text('utf8')).values()
            if 'MonoBehaviour' in doc and doc['MonoBehaviour'].get('myKey')}
        keys.update(('confirmation', 'confirm', 'cancel', 'ui_npc_detail_give_gift_overflow_tip'))
        texts, sources = language(keys)
        if keys - texts.keys() != {'{0}'}:
            raise ValueError('Original confirm dialog language missing: ' + repr(keys - texts.keys()))
        receipt['missing_language_keys'] = {'{0}': 'Original literal format-key fallback, retained without invented localization.'}
        receipt['inputs'].update(sources)
        target = folder / 'partner-confirm-texts.json'
        target.write_text(json.dumps(dict(manifest=self.recovery.apk.version,
            texts=[dict(key=key, text=text) for key, text in sorted(texts.items())]),
            ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        Path(str(target) + '.meta').write_text('fileFormatVersion: 2\nguid: '
            + hashlib.md5(b'zjcs:partner-confirm-texts').hexdigest()
            + '\nTextScriptImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
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
        for name in ('prepare_partner_roster_native.py', 'prepare_partner_input_dialog.py',
                     'prepare_partner_gift_native.py', 'partner_roster_animator_binding.py'):
            item = ROOT / 'scripts' / name
            receipt['inputs'][item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
        for relative in ('client-source/ConfirmDialog.cs', 'client-source/AutoAlignForSingleMultiLineTextContent.cs',
                         'client-source/AutoAlignForSingleMultiLineText.cs', 'client-source/LockMaskWithTime.cs'):
            item = ROOT / '.tools/source-packages/official-code' / relative
            selected = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code' / relative
            if item.read_bytes() != selected.read_bytes():
                raise ValueError('Selected confirm dialog source differs: ' + relative)
            receipt['inputs'][item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
        receipt['generator_sha256'] = sha(Path(__file__).read_bytes())
        receipt['scope'] = 'Original ConfirmDialog native Windows resource closure for partner gift overflow.'
        receipt['adaptations'] = [
            'Bind original MonoScripts to existing official ports and the consumed automatic text alignment content port.',
            'Resolve original Wwise events to verified local PCM audio; retain original clip data and compiled root paths.',
            'Restore empty strings from original serialized fields; host supplies original localization, window closing and local clock.',
        ]
        receipt['animation_root_path_repairs'] = self.animation_root_paths
        receipt['original_empty_value_restorations'] = self.empty_strings
        for key in ('gradient_aot_methods', 'gradient_observations'):
            receipt.pop(key)
        (self.work / RECEIPT.name).write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        old_receipt.unlink()


def install(work):
    receipt = json.loads((work / RECEIPT.name).read_bytes())
    if receipt['generator_sha256'] != sha(Path(__file__).read_bytes()):
        raise ValueError('Rebuild changed confirm dialog generator')
    for relative, expected in receipt['inputs'].items():
        if sha((ROOT / relative).read_bytes()) != expected:
            raise ValueError('Confirm dialog source changed: ' + relative)
    for row in receipt['script_bindings'].values():
        path = ROOT / row['path']
        if sha(path.read_bytes()) != row['sha256'] or sha(Path(str(path) + '.meta').read_bytes()) != row['meta_sha256']:
            raise ValueError('Confirm dialog runtime binding changed: ' + row['path'])
    previous = json.loads(RECEIPT.read_bytes()).get('installed_output_sha256', {}) if RECEIPT.is_file() else {}
    existing = {}
    for meta in (ROOT / 'unity/Assets/Official/Partners').rglob('*.meta'):
        path = Path(str(meta)[:-5])
        if path.is_file():
            key = guid(meta)
            if key in existing:
                raise ValueError('Existing partner GUID collision: ' + key)
            existing[key] = path
    plan, installed, reused = [], {}, []
    for relative, expected in receipt['outputs'].items():
        source = work / 'gift-assets' / relative
        if sha(source.read_bytes()) != expected:
            raise ValueError('Staged confirm resource changed: ' + relative)
        if relative.endswith('.meta'):
            continue
        key = guid(Path(str(source) + '.meta'))
        target = existing.get(key, DESTINATION / relative)
        if target.exists() and target.read_bytes() != source.read_bytes():
            if not target.is_relative_to(DESTINATION) or sha(target.read_bytes()) != previous.get(target.relative_to(ROOT).as_posix()):
                raise ValueError('Preserve different existing partner resource: ' + str(target))
        if target.exists() and target.read_bytes() == source.read_bytes():
            reused.append(target.relative_to(ROOT).as_posix())
        else:
            plan.extend(((source, target), (Path(str(source) + '.meta'), Path(str(target) + '.meta'))))
        if target.is_relative_to(DESTINATION):
            installed[target.relative_to(ROOT).as_posix()] = expected
            installed[Path(str(target) + '.meta').relative_to(ROOT).as_posix()] = sha(Path(str(source) + '.meta').read_bytes())
        existing[key] = target
    for source, target in plan:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(source.read_bytes())
    for folder in [DESTINATION, *[p for p in DESTINATION.rglob('*') if p.is_dir()]]:
        meta = metadata(folder, 'DefaultImporter', True)
        installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfficialPartnerConfirmAssetCatalog.cs'
    root = receipt['roots']['ConfirmDialog']
    text = existing[hashlib.md5(b'zjcs:partner-confirm-texts').hexdigest()]
    raw = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'
    raw += '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n'
    raw += '  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
    raw += '  m_Script: {fileID: 11500000, guid: ' + guid(Path(str(script) + '.meta')) + ', type: 3}\n'
    raw += "  m_Name: OfficialPartnerConfirmAssetCatalog\n  m_EditorClassIdentifier: ''\n"
    raw += '  prefab: {fileID: ' + str(root['fileID']) + ', guid: ' + root['guid'] + ', type: 3}\n'
    raw += '  texts: {fileID: 4900000, guid: ' + guid(Path(str(text) + '.meta')) + ', type: 3}\n'
    CATALOG.write_text(raw, 'utf8', newline='\n')
    metadata(CATALOG, 'NativeFormatImporter')
    for path in (CATALOG, Path(str(CATALOG) + '.meta')):
        installed[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    receipt.update(installed_output_sha256=installed, reused=reused)
    for relative in NEW_SCRIPTS:
        path = ROOT / 'unity/Assets/Game/Runtime' / relative
        for item in (path, Path(str(path) + '.meta')):
            receipt.setdefault('runtime_code', {})[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
    for relative in (
            'unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.Partners.cs',
            'unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.cs',
            'unity/Assets/Game/Runtime/Presentation/Home/HomeAcceptanceDriver.cs',
            'unity/Assets/Game/Editor/Home/HomeAcceptanceLauncher.cs',
            'unity/Assets/Game/Editor/Zjcs.Editor.asmdef'):
        receipt.setdefault('host_code', {})[relative] = sha((ROOT / relative).read_bytes())
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed original partner confirm dialog:', len(installed), 'files; reused', len(reused), 'assets', flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    for relative in NEW_SCRIPTS:
        metadata(ROOT / 'unity/Assets/Game/Runtime' / relative)
    work = validation_directory()
    if args.install:
        install(work)
    else:
        PartnerConfirmBinding(work).build()


if __name__ == '__main__':
    main()
