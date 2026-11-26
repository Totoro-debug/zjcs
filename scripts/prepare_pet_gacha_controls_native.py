"""Bind the complete original pet-gacha button and daily-count subtrees."""
import argparse
import copy
import hashlib
import json
import re
from pathlib import Path

from home_native_assets import install_staged_assets, write_staged_assets
from official_manifest import read_asset_manifest
from prepare_current_partner_native import PartnerNativeRecovery
from prepare_home_currency_windows_native import HomeCurrencyWindowsBinding
from prepare_home_pet_gacha_native import BUNDLE, PREFAB, prepare
from prepare_map16_story_source import decode_localization
from prepare_partner_card_native import PartnerCardBinding, ROOT, guid, script_id, sha
from prepare_selected_wwise_inputs import prepare as prepare_audio
from validation_workspace import validation_directory


DEST = ROOT / 'unity/Assets/Official/Home/PetGacha/Controls'
RECEIPT = ROOT / 'assets/reference/pet-gacha-controls-native-source-manifest.json'


class PetGachaControlsBinding(HomeCurrencyWindowsBinding):
    def __init__(self, work, *, recovery):
        # The older roster builder owns a fixed recovery scope. Initialize its
        # resource maps around this explicitly verified current panel instead.
        self.original_pointers = {}
        self.prefab_object_pointers = {}
        self.prefab_references = {}
        self.original_objects_by_guid = {}
        PartnerCardBinding.__init__(self, work, recovery=recovery)

    def register_scripts(self):
        super().register_scripts()
        folder = ROOT / 'unity/Assets/Game/Runtime/Official/GachaUI'
        for name in ('GachaBtnContent', 'PlayerOpenSkillChestBtn', 'GachaTodayCountLimitUI'):
            path = folder / (name + '.cs')
            meta = Path(str(path) + '.meta')
            ref = dict(fileID=11500000, guid=guid(meta), type=3)
            self.scripts[name] = ref
            self.class_ids[script_id('', name)] = ref
            self.script_guids.add(ref['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=ref,
                sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()))

    def replace(self, value):
        if value == dict(**{'class': 'StateAnimationConfig', 'ns': '', 'asm': 'Framework'}):
            for name in ('StateAnimationConfig', 'StateAnimationData'):
                path = ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI' / (name + '.cs')
                self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
            return dict(**{'class': 'StateAnimationConfig', 'ns': 'Zjcs.Official.PartnersUI', 'asm': 'Zjcs.Official.PartnersUI'})
        return super().replace(value)

    def sound(self, payload, owner):
        if not hasattr(self, 'control_audio_cache'):
            self.control_audio_cache = prepare_audio(self.cache.parent / 'pet-gacha-controls-audio',
                ['wwisebnk_assets_wwisedata_android_minimum_pck.rawfile'])
            receipt = self.control_audio_cache / 'raw-resources-source-manifest.json'
            self.inputs[receipt.relative_to(ROOT).as_posix()] = sha(receipt.read_bytes())
        original_cache = self.cache
        try:
            self.cache = self.control_audio_cache
            return super().sound(payload, owner)
        finally:
            self.cache = original_cache

    def build(self):
        self.recovery.recover('PetGachaPanel', self.work / 'controls-original', bundle=BUNDLE, native_relative=PREFAB)
        self.recovery.finalize()
        native, _, original, _ = self.recovery.outputs[0]
        self.selections = {}
        keys = {'ui_common_free', 'common_format_args1'}
        roots = {}
        prefabs = []
        for name, kind in [('PetGachaButtons', 'GachaBtnContent'), ('PetGachaDailyCount', 'GachaTodayCountLimitUI')]:
            panel = copy.deepcopy(original)
            docs = panel['documents']
            component, = [row for row in panel['scripts'] if row['type'] == kind]
            root = docs[component['nativeFileID']]['MonoBehaviour']['m_GameObject']['fileID']
            selected = set()

            def visit(go):
                selected.add(go)
                components = docs[go]['GameObject']['m_Component']
                selected.update(row['component']['fileID'] for row in components)
                transform = next(iter(docs[components[0]['component']['fileID']].values()))
                for child in transform['m_Children']:
                    visit(next(iter(docs[child['fileID']].values()))['m_GameObject']['fileID'])

            visit(root)
            panel['root'] = root
            panel['documents'] = {key: value for key, value in docs.items() if key in selected}
            panel['scripts'] = [row for row in panel['scripts'] if row['nativeFileID'] in selected]
            panel['objectMap'] = {key: value for key, value in panel['objectMap'].items() if value in selected}
            transform = next(iter(docs[docs[root]['GameObject']['m_Component'][0]['component']['fileID']].values()))
            previous_parent = transform['m_Father']
            transform['m_Father'] = dict(fileID=0)
            parents = []
            ancestor = previous_parent['fileID']
            while ancestor:
                rect = docs[ancestor]['RectTransform']
                parents.append(dict(name=docs[rect['m_GameObject']['fileID']]['GameObject']['m_Name'], transform=rect))
                ancestor = rect['m_Father']['fileID']

            def check(value):
                if isinstance(value, dict):
                    if set(value) == {'fileID'} and value['fileID'] and value['fileID'] not in selected:
                        raise ValueError('Bind the original control reference outside its subtree: ' + repr(value))
                    for child in value.values():
                        check(child)
                elif isinstance(value, list):
                    for child in value:
                        check(child)

            check(panel['documents'])
            missing = {'.'.join(filter(None, (row['namespace'], row['type']))) for row in panel['scripts']} - self.scripts.keys()
            if missing:
                raise ValueError('Migrate original gacha control scripts: ' + repr(missing))
            for key, ref in panel['external'].items():
                self.objects[ref['guid']] = self.original(key)
            for key, file_id in panel['objectMap'].items():
                owner = self.original(key)
                self.learn(self.recovery.fields(owner), next(iter(panel['documents'][file_id].values())), owner)

            def restore(source, target):
                if isinstance(source, dict) and isinstance(target, dict):
                    for key in source.keys() & target.keys():
                        if target[key] is None and source[key] in ('', [], {}):
                            target[key] = source[key]
                        else:
                            restore(source[key], target[key])
                elif isinstance(source, list) and isinstance(target, list) and len(source) == len(target):
                    for left, right in zip(source, target):
                        restore(left, right)

            for row in panel['scripts']:
                owner = self.original(row['original'])
                fields = panel['documents'][row['nativeFileID']]['MonoBehaviour']
                source = self.recovery.fields(owner)
                restore(source, fields)
                fields['m_Script'] = self.scripts['.'.join(filter(None, (row['namespace'], row['type'])))]
                if row['type'] == 'ButtonSound':
                    fields['ClickEvent'] = self.sound(source['ClickEvent'], owner)
                elif row['type'] == 'UIClickSound':
                    self.sound(source['AkEvent'], owner)
                if fields.get('myKey'):
                    keys.add(fields['myKey'])
            token = hashlib.md5(('zjcs:pet-gacha-controls:' + name).encode()).hexdigest()
            roots[name] = dict(fileID=root, guid=token, type=3)
            self.script_guids.add(token)
            prefabs.append((Path(name + '.prefab'), token, self.replace(panel['documents'])))
            self.selections[name] = dict(original_parent=previous_parent, detached_parent=dict(fileID=0),
                original_parent_chain=parents,
                original_root=next(key for key, value in panel['objectMap'].items() if value == root),
                objects=panel['objectMap'], scripts=panel['scripts'],
                adaptation='Complete original control subtree; detach its parent only.')

        runtime_rules = ROOT / 'unity/Assets/Game/Resources/PetGacha/pet-gacha-runtime.json'
        rules = json.loads(runtime_rules.read_bytes())
        cost_id = rules['Gacha']['CostItemId']
        item, = [row for row in rules['Items'] if row['ClassId'] == cost_id]
        records = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        address = 'Assets/Image/' + item['RawIcon']
        record, = [row for row in records if row.get('address', '').casefold() == address.casefold()
                    or row['path'].casefold() == (address + '.png').casefold()]
        self.bind_dynamic_sprite(record['guid'])
        cost_sprite = dict(item_id=cost_id, path=address, asset_guid=record['guid'],
                           pointer=self.dynamic_sprites_by_guid[record['guid']]['pointer'])
        self.inputs[runtime_rules.relative_to(ROOT).as_posix()] = sha(runtime_rules.read_bytes())
        for _, _, data in self.queue:
            if isinstance(data, dict):
                resolved = self.replace(data)
                data.clear()
                data.update(resolved)
        self.queue.extend(prefabs)
        texts, localization = decode_localization(self.recovery.apk, keys, keys - {'{0}'})
        text_token = hashlib.md5(b'zjcs:pet-gacha-controls:texts').hexdigest()
        text_ref = dict(fileID=4900000, guid=text_token, type=3)
        self.queue.append((Path('pet-gacha-controls-texts.json'), text_token,
            (json.dumps(dict(texts=texts), ensure_ascii=False, indent=2) + '\n').encode()))
        # Current native UI controllers can share an official GUID with an
        # earlier migrated input. Give this closure its own output identities.
        tokens = {token for _, token, _ in self.queue} | {token for token, _ in self.audio_outputs.values()}
        rewrites = {token: hashlib.md5(('zjcs:pet-gacha-controls:asset:' + token).encode()).hexdigest() for token in tokens}

        def remap(value):
            if isinstance(value, dict):
                return {key: rewrites.get(item, item) if key == 'guid' and isinstance(item, str) else remap(item)
                        for key, item in value.items()}
            if isinstance(value, list):
                return [remap(item) for item in value]
            return value

        self.queue = [(path, rewrites[token], remap(data)) for path, token, data in self.queue]
        roots = remap(roots)
        self.proof = remap(self.proof)
        self.dynamic_sprites_by_guid = remap(self.dynamic_sprites_by_guid)
        self.audio_proof = remap(self.audio_proof)
        self.audio_outputs = {path: (rewrites[token], raw) for path, (token, raw) in self.audio_outputs.items()}
        folder = self.work / 'control-assets'
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
        for path in (native, Path(str(native) + '.meta'), Path(__file__), ROOT / 'scripts/home_native_assets.py',
                     ROOT / 'scripts/prepare_partner_roster_native.py'):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        code = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code'
        for relative in ('client-source/GachaBtnContent.cs', 'client-source/PlayerOpenSkillChestBtn.cs',
                         'client-source/GachaTodayCountLimitUI.cs', 'common-source/Common/GachaType.cs',
                         'common-source/Common/GachaClassType.cs'):
            path = code / relative
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        used = set()
        for path in folder.rglob('*'):
            if path.is_file() and path.suffix != '.meta' and path.read_bytes().startswith(b'%YAML'):
                used.update(re.findall(r'm_Script: \{[^\n}]*guid: ([0-9a-f]{32})', path.read_text('utf8')))
        scripts = {name: row for name, row in self.script_sources.items() if row['pointer']['guid'] in used}
        runtime = ROOT / 'unity/Assets/Game/Runtime/Official/GachaUI'
        return folder, outputs, dict(manifest=self.recovery.apk.version, inputs=self.inputs, recovery=self.recovery.proof,
            selections=self.selections, localization=localization, resources=self.proof, roots=roots,
            script_bindings=scripts, audio=self.audio_proof,
            dynamic_sprites=self.dynamic_sprites_by_guid, animation_root_path_repairs=self.animation_root_paths,
            cost_sprite=remap(cost_sprite),
            texts=remap(text_ref), runtime_bindings={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in runtime.iterdir() if path.is_file()},
            scope='Original pet-gacha buttons and daily count. Full panel and result performer remain to be bound.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    work = validation_directory()
    recovery = PartnerNativeRecovery(native_cache=prepare(), export_name='native-export', bundle=BUNDLE)
    try:
        binding = PetGachaControlsBinding(work, recovery=recovery)
        folder, outputs, receipt = binding.build()
        if args.install:
            previous = json.loads(RECEIPT.read_bytes())['outputs'] if RECEIPT.is_file() else {}
            receipt['outputs'] = install_staged_assets(work, folder, outputs, DEST, 'pet-gacha-controls', previous_outputs=previous)
            parent_meta = Path(str(DEST.parent) + '.meta')
            if not parent_meta.is_file():
                parent_meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:pet-gacha-native-folder').hexdigest()
                    + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
            receipt['outputs'][parent_meta.relative_to(ROOT).as_posix()] = sha(parent_meta.read_bytes())
            RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        else:
            receipt['outputs'] = outputs
        (work / 'pet-gacha-controls-source-manifest.json').write_text(
            json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    finally:
        recovery.apk.reader.close()
