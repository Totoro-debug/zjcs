"""Bind the complete original shared MarkableConfirmDialog and its closure."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from home_native_assets import install_staged_assets, write_staged_assets
from home_native_cache_verification import verify_native_cache
from prepare_current_partner_native import PartnerNativeRecovery, documents
from prepare_pet_gacha_controls_native import PetGachaControlsBinding
from prepare_map16_story_source import decode_localization
from prepare_partner_card_native import ROOT, guid, script_id, sha
from official_source import OfficialSource
from validation_workspace import validation_directory


BUNDLE = 'defaultpackage_assets_prefabs_ui_confirmdialog.bundle'
PREFAB = 'Prefabs/UI/ConfirmDialog/MarkableConfirmDialog.prefab'
DEST = ROOT / 'unity/Assets/Official/Home/PetGacha/MarkableConfirm'
CATALOG = ROOT / 'unity/Assets/Game/Resources/PetGacha/OfficialMarkableConfirmAssetCatalog.asset'
RECEIPT = ROOT / 'assets/reference/markable-confirm-native-source-manifest.json'


class OfficialPopupBinding(PetGachaControlsBinding):
    """Full native popup closure; subclasses select source, scripts and identity."""
    def register_scripts(self):
        super().register_scripts()
        for name, relative in self.supplementary_scripts.items():
            path = ROOT / 'unity/Assets/Game/Runtime/Official' / relative
            meta = Path(str(path) + '.meta')
            pointer = dict(fileID=11500000, guid=guid(meta), type=3)
            self.scripts[name] = pointer
            self.class_ids[script_id('', name)] = pointer
            self.script_guids.add(pointer['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))

    def friendship_templates(self):
        self.recovery.recover(self.panel_name, self.work / 'original',
                              bundle=self.bundle, native_relative=self.prefab)
        self.recovery.finalize()
        native, _, panel, record = self.recovery.outputs[0]
        self.empty_restorations = []
        self.prepare_popup_template(native, panel, record)
        self.template_selection = dict(source=record['originalRoot'], mapped_objects=len(panel['objectMap']),
            scope='Complete original ' + self.panel_name + ' hierarchy and serialized fields.')
        return [(self.panel_name, panel)]

    def prepare_popup_template(self, native, panel, record):
        for item in (native, Path(str(native) + '.meta')):
            self.inputs[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
        cache = self.recovery.proof['component_cache']
        self.inputs[cache['path']] = cache['sha256']

        def restore(source, target, address):
            if isinstance(source, dict) and isinstance(target, dict):
                for key in source.keys() & target.keys():
                    if target[key] is None and source[key] in ('', [], {}):
                        target[key] = source[key]
                        self.empty_restorations.append(address + '/' + key)
                    else:
                        restore(source[key], target[key], address + '/' + key)
            elif isinstance(source, list) and isinstance(target, list) and len(source) == len(target):
                for index, (left, right) in enumerate(zip(source, target)):
                    restore(left, right, address + '/' + str(index))

        for key, pointer in panel['external'].items():
            self.objects[pointer['guid']] = self.original(key)
        for key, file_id in panel['objectMap'].items():
            owner = self.original(key)
            self.learn(self.recovery.fields(owner), next(iter(panel['documents'][file_id].values())), owner)
        for row in panel['scripts']:
            owner = self.original(row['original'])
            original = self.recovery.fields(owner)
            fields = panel['documents'][row['nativeFileID']]['MonoBehaviour']
            restore(original, fields, row['original'])
            if row['type'] == 'PopupWindowSound':
                for field in ('ShowSound', 'HideSound'):
                    fields[field] = self.sound(original[field], owner)
            elif row['type'] == 'UIClickSound':
                self.sound(original['AkEvent'], owner)
        missing = {'.'.join(filter(None, (row['namespace'], row['type']))) for row in panel['scripts']} - self.scripts.keys()
        if missing:
            raise ValueError('Migrate original popup scripts: ' + repr(missing))

    def build(self):
        roots = {}
        prefabs = []
        for name, panel in self.friendship_templates():
            for row in panel['scripts']:
                fields = panel['documents'][row['nativeFileID']]['MonoBehaviour']
                fields['m_Script'] = self.scripts['.'.join(filter(None, (row['namespace'], row['type'])))]
                if row['type'] == 'ButtonSound':
                    owner = self.original(row['original'])
                    fields['ClickEvent'] = self.sound(self.recovery.fields(owner)['ClickEvent'], owner)
            token = hashlib.md5(('zjcs:' + self.identity + ':' + name).encode()).hexdigest()
            self.script_guids.add(token)
            roots[name] = dict(fileID=panel['root'], guid=token, type=3)
            prefabs.append((Path(name + '.prefab'), token, self.replace(panel['documents'])))
        for _, _, data in self.queue:
            if isinstance(data, dict):
                resolved = self.replace(data)
                data.clear()
                data.update(resolved)
        self.queue.extend(prefabs)
        folder = self.work / 'gift-assets'
        outputs = write_staged_assets(self.queue, folder)
        for path, (token, raw) in self.audio_outputs.items():
            target = folder / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(raw)
            meta = Path(str(target) + '.meta')
            meta.write_text('fileFormatVersion: 2\nguid: ' + token + '\nAudioImporter:\n  externalObjects: {}\n'
                '  serializedVersion: 7\n  defaultSettings:\n    loadType: 0\n    sampleRateSetting: 0\n'
                '    sampleRateOverride: 0\n    compressionFormat: 0\n    quality: 1\n    conversionMode: 0\n'
                '  forceToMono: 0\n  normalize: 0\n  preloadAudioData: 1\n  loadInBackground: 0\n'
                '  ambisonic: 0\n  3D: 0\n', 'utf8', newline='\n')
            outputs[path.as_posix()] = sha(raw)
            outputs[path.as_posix() + '.meta'] = sha(meta.read_bytes())
        receipt = dict(manifest=self.recovery.apk.version, inputs=self.inputs, roots=roots, outputs=outputs,
            native_recovery=self.recovery.proof, objects=self.proof, audio=self.audio_proof,
            script_bindings=self.script_sources, template_selection=self.template_selection)
        keys = {doc['MonoBehaviour']['myKey'] for name in roots for doc in documents(
            (folder / (name + '.prefab')).read_text('utf8')).values()
            if doc.get('MonoBehaviour', {}).get('myKey')}
        keys.update(self.required_keys)
        texts, localization = decode_localization(self.recovery.apk, keys, keys - set(self.literal_keys))
        token = hashlib.md5(('zjcs:' + self.identity + ':texts').encode()).hexdigest()
        outputs = write_staged_assets([(Path(self.identity + '-texts.json'), token,
            (json.dumps(dict(texts=[dict(key=k, text=v) for k, v in sorted(texts.items())]),
                ensure_ascii=False, indent=2) + '\n').encode())], folder)
        receipt['outputs'].update(outputs)
        receipt['texts'] = dict(fileID=4900000, guid=token, type=3)
        tokens = {guid(folder / path) for path in receipt['outputs'] if path.endswith('.meta')}
        collisions = tokens & {row['pointer']['guid'] for row in self.script_sources.values()}
        if collisions:
            raise ValueError('Native popup output identity overlaps a runtime script: ' + repr(collisions))
        rewrites = {key: hashlib.md5(('zjcs:' + self.identity + ':asset:' + key).encode()).hexdigest() for key in tokens}

        def remap(value):
            if isinstance(value, dict):
                return {key: rewrites.get(item, item) if key == 'guid' and isinstance(item, str) else remap(item)
                        for key, item in value.items()}
            if isinstance(value, list):
                return [remap(item) for item in value]
            return value

        for relative in receipt['outputs']:
            path = folder / relative
            raw = path.read_bytes()
            if relative.endswith('.meta') or raw.startswith(b'%YAML'):
                raw = re.sub(rb'guid: ([0-9a-f]{32})',
                    lambda match: b'guid: ' + rewrites.get(match[1].decode(), match[1].decode()).encode(), raw)
                path.write_bytes(raw)
            receipt['outputs'][relative] = sha(raw)
        receipt = remap(receipt)
        used = set()
        for relative in receipt['outputs']:
            raw = (folder / relative).read_bytes()
            if raw.startswith(b'%YAML'):
                used.update(match.decode() for match in re.findall(rb'm_Script: \{[^\n}]*guid: ([0-9a-f]{32})', raw))
        receipt['script_bindings'] = {name: row for name, row in receipt['script_bindings'].items()
                                     if row['pointer']['guid'] in used}
        code = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code'
        paths = [Path(__file__), ROOT / 'scripts/home_native_assets.py', ROOT / 'scripts/prepare_home_currency_windows_native.py',
                 ROOT / 'scripts/prepare_pet_gacha_controls_native.py']
        paths += [code / path for path in self.source_paths]
        receipt['inputs'].update({path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in paths})
        runtime = ROOT / 'unity/Assets/Game/Runtime'
        paths = [runtime / path for path in self.runtime_paths]
        receipt['runtime_bindings'] = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in paths}
        receipt.update(generator_sha256=sha(Path(__file__).read_bytes()), localization=localization,
            animation_root_path_repairs=self.animation_root_paths, original_empty_value_restorations=self.empty_restorations,
            scope='Complete original ' + self.panel_name + ' native resource closure; runtime integration recorded separately.')
        return folder, receipt


class MarkableConfirmBinding(OfficialPopupBinding):
    panel_name, bundle, prefab, identity = 'MarkableConfirmDialog', BUNDLE, PREFAB, 'markable-confirm'
    supplementary_scripts = {'MarkableConfirmDialog': 'GachaUI/MarkableConfirmDialog.cs', 'UIToggle': 'SkillsUI/UIToggle.cs'}
    required_keys = ('confirmation', 'confirm', 'cancel', 'ui_pet_gacha_is_times_hundred', 'ui_no_prompts_today')
    literal_keys = ('{0}',)
    source_paths = ('client-source/MarkableConfirmDialog.cs', 'client-source/ConfirmDialog.cs',
        'client-source/GachaPanelUtils.cs', 'client-source/GachaBaseSetting.cs',
        'common-source/Common/TimeUtils.cs', 'framework-firstpass-source/UIToggle.cs')
    runtime_paths = ('Official/GachaUI/MarkableConfirmDialog.cs', 'Official/GachaUI/MarkableConfirmDialog.cs.meta',
        'Official/GachaUI/ScoreRewardServices.cs', 'Official/GachaUI/Zjcs.Official.GachaUI.asmdef',
        'Official/GachaUI/Zjcs.Official.GachaUI.asmdef.meta', 'Offline/GachaUI/OfficialMarkableConfirmAssetCatalog.cs',
        'Offline/GachaUI/OfficialMarkableConfirmAssetCatalog.cs.meta')


def install(work, folder, receipt):
    previous = json.loads(RECEIPT.read_bytes()).get('outputs', {}) if RECEIPT.is_file() else {}
    receipt['outputs'] = install_staged_assets(work, folder, receipt['outputs'], DEST,
        'markable-confirm', previous_outputs=previous)
    root = receipt['roots']['MarkableConfirmDialog']
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/GachaUI/OfficialMarkableConfirmAssetCatalog.cs'
    data = {11400000: {'MonoBehaviour': dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0),
        m_PrefabInstance=dict(fileID=0), m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0),
        m_Enabled=1, m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000, guid=guid(Path(str(script) + '.meta')), type=3),
        m_Name=CATALOG.stem, m_EditorClassIdentifier='', prefab=root, texts=receipt['texts'])}}
    staged = work / 'catalog-assets'
    token = hashlib.md5(b'zjcs:markable-confirm:catalog').hexdigest()
    outputs = write_staged_assets([(Path(CATALOG.name), token, data)], staged)
    receipt['outputs'].update(install_staged_assets(work, staged, outputs, CATALOG.parent,
        'markable-confirm-catalog', previous_outputs=previous))
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


def check():
    receipt = json.loads(RECEIPT.read_bytes())
    if receipt['generator_sha256'] != sha(Path(__file__).read_bytes()):
        raise ValueError('Rebuild changed markable confirmation generator')
    for group in ('inputs', 'outputs', 'runtime_bindings'):
        for relative, expected in receipt[group].items():
            if sha((ROOT / relative).read_bytes()) != expected:
                raise ValueError('Markable confirmation source differs: ' + relative)
    for row in receipt['script_bindings'].values():
        path = ROOT / row['path']
        if guid(Path(str(path) + '.meta')) != row['pointer']['guid']:
            raise ValueError('Markable confirmation script identity differs: ' + row['path'])
        if sha(path.read_bytes()) != row['sha256'] or sha(Path(str(path) + '.meta').read_bytes()) != row['meta_sha256']:
            raise ValueError('Markable confirmation script binding differs: ' + row['path'])
    print('Original markable confirmation source and resource closure verified')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    if args.check:
        check()
    else:
        cache = ROOT / '.tools/source-packages/current' / OfficialSource().version / 'home-markable-confirm-native'
        verify_native_cache(cache, [PREFAB], cache.parent.name)
        recovery = PartnerNativeRecovery(native_cache=cache, export_name='native-export', bundle=BUNDLE)
        try:
            folder, receipt = MarkableConfirmBinding(validation_directory(), recovery=recovery).build()
            if args.install:
                install(validation_directory(), folder, receipt)
            (validation_directory() / RECEIPT.name).write_text(
                json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        finally:
            recovery.apk.reader.close()
