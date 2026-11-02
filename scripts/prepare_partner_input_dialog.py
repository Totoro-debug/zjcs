"""Bind the selected complete official InputDialog used by partner custom titles."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_partner_card_native import guid, script_id, sha, language, identity
from prepare_partner_gift_native import PartnerGiftBinding
from prepare_partner_roster_native import PartnerRosterBinding
from validation_workspace import validation_directory
from prepare_current_partner_native import pointer as source_pointer, documents

RECEIPT = ROOT / 'assets/reference/partner-input-dialog-source-manifest.json'
DESTINATION = ROOT / 'unity/Assets/Official/Partners/UI/Input'
CATALOG = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerPromptAssetCatalog.asset'
NEW_SCRIPTS = (
    'Official/WorldUI/WorldInputDialog.cs',
    'Official/WorldUI/WorldContentModerationClickMask.cs',
    'Official/WorldUI/WorldWindowMask.cs',
    'Official/WorldUI/WorldWindowMaskEffectBase.cs',
    'Offline/PartnersUI/OfficialPartnerPromptAssetCatalog.cs',
)


def metadata(path, importer='MonoImporter', folder=False):
    key = hashlib.md5(('zjcs:partner-input:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
    value = 'fileFormatVersion: 2\nguid: ' + key + '\n'
    if folder:
        value += 'folderAsset: yes\n'
    value += importer + ':\n  externalObjects: {}\n'
    target = Path(str(path) + '.meta')
    if not target.exists():
        target.write_text(value, 'utf8', newline='\n')
    return target


class PartnerInputBinding(PartnerRosterBinding):
    def load(self, source_guid):
        if source_guid in self.redirects:
            return self.redirects[source_guid]
        result = super().load(source_guid)
        obj = self.objects.get(source_guid)
        if obj is not None and obj.type.name == 'AnimationClip' and any(
                row['original'] == identity(obj) for row in self.animation_root_paths):
            # Earlier dialog imports retain the converter's null curve path.
            # Keep those assets intact and give this verified original root-path
            # restoration a distinct GUID; every new caller uses this pointer.
            key = hashlib.md5(('zjcs:partner-input-root-animation:' + identity(obj)).encode()).hexdigest()
            index, = [i for i, row in enumerate(self.queue) if row[1] == result['guid']]
            target, _, data = self.queue[index]
            self.queue[index] = (target, key, data)
            result = dict(result, guid=key)
            self.redirects[source_guid] = result
            self.proof[identity(obj)]['guid'] = key
        return result

    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI'
        paths = {name: runtime / ('World' + name + '.cs') for name in (
            'InputDialog', 'ContentModerationClickMask', 'BgDialogElement', 'CountRedDotImage',
            'PopupWindow', 'PopupWindowSound', 'UIBloomGroup')}
        paths['WindowMask'] = runtime / 'WorldWindowMask.cs'
        paths['MirrorImage'] = runtime / 'MirrorImage.cs'
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
        self.recovery.recover('InputDialog', self.work / 'partner-native',
            bundle='defaultpackage_assets_prefabs_ui_inputdialog.bundle',
            native_relative='Prefabs/UI/InputDialog/InputDialog.prefab')
        bundle = 'defaultpackage_assets_prefabs_ui_main.bundle'
        containers = []
        for obj in self.recovery.apk.objects(bundle):
            if obj.type.name != 'MonoBehaviour':
                continue
            script = obj.read(check_read=False).m_Script.read()
            if script.m_ClassName == 'WindowContainer' and self.recovery.fields(obj)['mContainerType'] == 0:
                containers.append((obj, script))
        container, script = containers[0]
        if len(containers) != 1:
            raise ValueError('Inspect original default window container')
        original = self.recovery.fields(container)
        masks = [(index, source_pointer(value, container)) for index, value in enumerate(original['MaskList'])]
        index, mask = next((index, obj) for index, obj in masks if self.recovery.fields(obj)['mMaskType'] == 1)
        native = self.native / 'Prefabs/UI/Main/MainCanvas.prefab'
        native_docs = documents(native.read_text('utf8'))
        native_container, = [doc['MonoBehaviour'] for doc in native_docs.values() if 'MonoBehaviour' in doc
            and doc['MonoBehaviour']['m_Script']['fileID'] == script_id(script.m_Namespace, script.m_ClassName)
            and doc['MonoBehaviour'].get('mContainerType') == 0]
        native_mask = native_docs[native_container['MaskList'][index]['fileID']]['MonoBehaviour']
        if native_mask['mMaskType'] != 1:
            raise ValueError('Original/native normal window mask differs')
        self.recovery.recover('PartnerInputMask', self.work / 'partner-native', bundle=bundle,
            native_relative='Prefabs/UI/Main/MainCanvas.prefab',
            source_root=source_pointer(self.recovery.fields(mask)['m_GameObject'], mask),
            native_root_id=native_mask['m_GameObject']['fileID'])
        self.mask_selection = dict(container=identity(container), source=identity(mask), list_index=index,
            original_container_fields=original, native_source=native.relative_to(ROOT).as_posix())
        self.recovery.finalize()
        native, _, panel, record = self.recovery.outputs[0]
        for native, _, _, _ in self.recovery.outputs:
            for path in (native, Path(str(native) + '.meta')):
                self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        cache = self.recovery.proof['component_cache']
        self.inputs[cache['path']] = cache['sha256']
        for key, pointer in panel['external'].items():
            self.objects[pointer['guid']] = self.original(key)
        for key, file_id in panel['objectMap'].items():
            owner = self.original(key)
            self.learn(self.recovery.fields(owner), next(iter(panel['documents'][file_id].values())), owner)
        for script in panel['scripts']:
            owner = self.original(script['original'])
            fields = panel['documents'][script['nativeFileID']]['MonoBehaviour']
            original = self.recovery.fields(owner)

            def restore_empty(source, converted, address):
                if isinstance(source, dict) and isinstance(converted, dict):
                    for key in source.keys() & converted.keys():
                        if source[key] == '' and converted[key] is None:
                            converted[key] = ''
                            self.empty_strings.append(dict(original=script['original'], field=address + '/' + key))
                        else:
                            restore_empty(source[key], converted[key], address + '/' + key)
                elif isinstance(source, list) and isinstance(converted, list) and len(source) == len(converted):
                    for index, (left, right) in enumerate(zip(source, converted)):
                        restore_empty(left, right, address + '/' + str(index))

            if not hasattr(self, 'empty_strings'):
                self.empty_strings = []
            restore_empty(original, fields, '')
            if script['type'] == 'PopupWindowSound':
                for field in ('ShowSound', 'HideSound'):
                    fields[field] = self.sound(original[field], owner)
        self.template_selection = dict(scope='Complete original InputDialog, with every original object/component/rectangle retained.',
            source=record['originalRoot'], mapped_objects=len(panel['objectMap']))
        return [('InputDialog', panel), ('PartnerInputMask', self.recovery.outputs[1][2])]

    def build(self):
        PartnerGiftBinding.build(self)
        path = self.work / 'partner-gift-native-source-manifest.json'
        receipt = json.loads(path.read_bytes())
        folder = self.work / 'gift-assets'
        prefab = folder / 'InputDialog.prefab'
        keys = set(re.findall(r"(?m)^  myKey: ([^\n]+)$", prefab.read_text('utf8')))
        keys = {key for key in keys if key and key not in ("''", 'null')}
        keys.update(('ui_npc_change_tag', 'ui_npc_input_tag', 'ui_npc_change_tag_success'))
        texts, sources = language(keys)
        missing = keys - texts.keys()
        if missing != {'ui_null'}:
            raise ValueError('Original input dialog language missing: ' + repr(keys - texts.keys()))
        # The original input-value language component uses this absent key.
        # LocalizationTextService.GetMissingKeyText returns the key itself.
        receipt['missing_language_keys'] = dict(ui_null='Original missing-key literal fallback; no invented translation.')
        receipt['inputs'].update(sources)
        old = folder / 'partner-gift-texts.json'
        target = folder / 'partner-input-texts.json'
        target.write_text(json.dumps(dict(manifest=self.recovery.apk.version,
            texts=[dict(key=key, text=text) for key, text in sorted(texts.items())]),
            ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        Path(str(target) + '.meta').write_text('fileFormatVersion: 2\nguid: '
            + hashlib.md5(b'zjcs:partner-input-texts').hexdigest()
            + '\nTextScriptImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
        for item in (old, Path(str(old) + '.meta')):
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
        for name in ('prepare_partner_roster_native.py', 'prepare_partner_gift_native.py', 'partner_roster_animator_binding.py'):
            item = ROOT / 'scripts' / name
            receipt['inputs'][item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
        for relative in ('client-source/InputDialog.cs', 'client-source/ContentModerationClickMask.cs',
                         'common-source/Common/ContentCustomType.cs',
                         'framework-firstpass-source/Framework/LocalizationTextService.cs',
                         'framework-firstpass-source/WindowMask.cs',
                         'framework-firstpass-source/WindowMaskBase.cs',
                         'framework-firstpass-source/WindowMaskEffectBase.cs',
                         'framework-firstpass-source/Framework.UI/WindowContainer.cs',
                         'framework-firstpass-source/PopupWindow.cs'):
            item = ROOT / '.tools/source-packages/official-code' / relative
            selected = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code' / relative
            if item.read_bytes() != selected.read_bytes():
                raise ValueError('Selected input dialog source differs: ' + relative)
            receipt['inputs'][item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
        receipt['generator_sha256'] = sha(Path(__file__).read_bytes())
        receipt['scope'] = 'Original InputDialog native Windows resource closure for partner custom titles.'
        receipt['adaptations'] = [
            'Bind MonoScripts to existing official ports and the consumed InputDialog/ContentModerationClickMask ports.',
            'Resolve original Wwise event objects to verified local PCM audio; preserve original clips and animation curves.',
            'Original compiled animation path hash zero restores an empty root path; use distinct GUIDs to preserve earlier converted-null assets.',
            'Project text-key arguments, window closing and server content-custom state through local interfaces.',
            'Confirmed offline NPC title policy: original glyph/length checks followed by local persistence; no server-disabled mask.',
        ]
        receipt['animation_root_path_repairs'] = self.animation_root_paths
        receipt['original_empty_string_restorations'] = self.empty_strings
        receipt['mask_selection'] = self.mask_selection
        for key in ('gradient_aot_methods', 'gradient_observations'):
            receipt.pop(key)
        (self.work / RECEIPT.name).write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        path.unlink()


def install(work):
    receipt = json.loads((work / RECEIPT.name).read_bytes())
    if receipt['generator_sha256'] != sha(Path(__file__).read_bytes()):
        raise ValueError('Rebuild changed input dialog generator')
    for relative, expected in receipt['inputs'].items():
        if sha((ROOT / relative).read_bytes()) != expected:
            raise ValueError('Input dialog source changed: ' + relative)
    for row in receipt['script_bindings'].values():
        path = ROOT / row['path']
        if sha(path.read_bytes()) != row['sha256'] or sha(Path(str(path) + '.meta').read_bytes()) != row['meta_sha256']:
            raise ValueError('Input dialog runtime binding changed: ' + row['path'])
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
            raise ValueError('Staged input dialog resource changed: ' + relative)
        if relative.endswith('.meta'):
            continue
        key = guid(Path(str(source) + '.meta'))
        target = existing.get(key, DESTINATION / relative)
        if target.exists():
            if target.read_bytes() != source.read_bytes():
                if not target.is_relative_to(DESTINATION) or sha(target.read_bytes()) != previous.get(target.relative_to(ROOT).as_posix()):
                    raise ValueError('Preserve different existing partner resource: ' + str(target))
                plan.extend(((source, target), (Path(str(source) + '.meta'), Path(str(target) + '.meta'))))
            else:
                reused.append(target.relative_to(ROOT).as_posix())
                if target.is_relative_to(DESTINATION):
                    for item in (target, Path(str(target) + '.meta')):
                        installed[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
        else:
            plan.extend(((source, target), (Path(str(source) + '.meta'), Path(str(target) + '.meta'))))
        existing[key] = target
    for source, target in plan:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(source.read_bytes())
        installed[target.relative_to(ROOT).as_posix()] = sha(target.read_bytes())
    for folder in [DESTINATION, *[p for p in DESTINATION.rglob('*') if p.is_dir()]]:
        meta = metadata(folder, 'DefaultImporter', True)
        installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfficialPartnerPromptAssetCatalog.cs'
    root = receipt['roots']['InputDialog']
    text = existing[hashlib.md5(b'zjcs:partner-input-texts').hexdigest()]
    raw = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'
    raw += '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n'
    raw += '  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
    raw += '  m_Script: {fileID: 11500000, guid: ' + guid(Path(str(script) + '.meta')) + ', type: 3}\n'
    raw += '  m_Name: OfficialPartnerPromptAssetCatalog\n  m_EditorClassIdentifier: \'\'\n'
    raw += '  inputPrefab: {fileID: ' + str(root['fileID']) + ', guid: ' + root['guid'] + ', type: 3}\n'
    mask = receipt['roots']['PartnerInputMask']
    raw += '  maskPrefab: {fileID: ' + str(mask['fileID']) + ', guid: ' + mask['guid'] + ', type: 3}\n'
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
            'unity/Assets/Game/Runtime/Official/WorldUI/WorldPopupWindow.cs',
            'unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.Partners.cs',
            'unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.cs',
            'unity/Assets/Game/Runtime/Presentation/Home/HomeAcceptanceDriver.cs',
            'unity/Assets/Game/Editor/Home/HomeAcceptanceLauncher.cs'):
        receipt.setdefault('host_code', {})[relative] = sha((ROOT / relative).read_bytes())
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed original partner input dialog:', len(installed), 'files; reused', len(reused), 'assets', flush=True)


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
        PartnerInputBinding(work).build()


if __name__ == '__main__':
    main()
