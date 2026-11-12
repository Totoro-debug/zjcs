"""Bind all thirteen original world damage prefabs to complete local behaviours."""
import copy
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from prepare_battle_round_scene_native import RoundSceneBinding
from prepare_activity_battle_selector_native import encode
from prepare_partner_card_native import guid, script_id, sha
from prepare_current_partner_native import documents
from validation_workspace import validation_directory

DEST = ROOT / 'unity/Assets/Official/Combat/DamageUI'
RECEIPT = ROOT / 'assets/reference/battle-hud/damage-ui-native-source-manifest.json'


class DamageBinding(RoundSceneBinding):
    def damage_root(self):
        # Original FightRoot.DamageFloatsRoot is a world-space subtree, not a
        # HUD canvas. Isolate that exact empty GameObject/Transform pair.
        path = self.native / 'Prefabs/EcEntity/Fight/FightStage/1000/game_1000.prefab'
        key = path.relative_to(self.cache / 'native-export-all').as_posix()
        if sha(path.read_bytes()) != self.checked[key]: raise ValueError('Changed original FightRoot prefab')
        self.inputs[path.relative_to(ROOT).as_posix()] = self.checked[key]
        docs = documents(path.read_text(encoding='utf-8-sig'))
        root, = [d['MonoBehaviour']['DamageFloatsRoot']['fileID'] for d in docs.values()
            if 'MonoBehaviour' in d and 'DamageFloatsRoot' in d['MonoBehaviour']]
        transform = copy.deepcopy(docs[root])
        obj = transform['Transform']['m_GameObject']['fileID']
        game_object = copy.deepcopy(docs[obj])
        if transform['Transform']['m_Children'] or len(game_object['GameObject']['m_Component']) != 1:
            raise ValueError('Review original damage root subtree closure')
        transform['Transform']['m_Father'] = dict(fileID=0)
        self.queue.append((Path('Resources/Combat/DamageUIRoot.prefab'),
            hashlib.md5(b'zjcs:damage-ui:original-world-root').hexdigest(), {obj: game_object, root: transform}))
        self.damage_root_source = dict(path=path.relative_to(ROOT).as_posix(),
            transform_file_id=root, original_transform=docs[root],
            operation='Isolate original FightRoot.DamageFloatsRoot subtree; detach only its external parent.')

    def register_scripts(self):
        super().register_scripts()
        for name in ('CharacterDamageUI', 'DamageNumberUI', 'DamageFloatSize',
                     'DamageCustomActionUI', 'ElementConfrontationUI', 'DamageAutoSizeTextBack', 'FightDamageSettings'):
            path = ROOT / 'unity/Assets/Game/Runtime/Official/BattleDamageUI' / (name + '.cs')
            pointer = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
            self.scripts[name] = self.class_ids[script_id('', name)] = pointer
            self.script_guids.add(pointer['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + '.meta').read_bytes()))

    def load(self, source_guid):
        obj = self.objects.get(source_guid)
        if source_guid not in self.redirects and obj is not None and obj.type.name == 'Font' and not obj.read().m_FontData:
            # Bitmap Font is a native YAML Font, not an imported TTF. Retain its
            # original glyph rectangles, metrics and material references.
            path = self.native_path(source_guid, obj)
            for item in (path, Path(str(path) + '.meta')):
                expected = self.checked[item.relative_to(self.cache / 'native-export-all').as_posix()]
                if sha(item.read_bytes()) != expected: raise ValueError('Changed original bitmap Font')
                self.inputs[item.relative_to(ROOT).as_posix()] = expected
            data = documents(path.read_text(encoding='utf-8'))
            file_id, = data
            self.learn(self.recovery.fields(obj), data[file_id]['Font'], obj)
            value = hashlib.md5(('zjcs:damage-ui-font:' + source_guid).encode()).hexdigest()
            pointer = dict(fileID=file_id, guid=value, type=2)
            self.redirects[source_guid] = pointer
            self.queue.append((Path('Fonts') / path.name, value, data))
            from prepare_current_partner_native import identity
            self.proof[identity(obj)] = dict(source=self.recovery.apk.provenance(obj), type='Font',
                original_sha256=sha(obj.get_raw_data()), output=str(Path('Fonts') / path.name), guid=value,
                operation='Original native bitmap Font; no glyph or metric changes.')
            return pointer
        return super().load(source_guid)

    def extract(self):
        prefabs = []
        text_keys = {'EStatusCustomAction.' + name for name in ('None', 'Immunity', 'Unflinching', 'CDModify')}
        for path in sorted((self.native / 'Prefabs/UI/Fight/DamageScene').glob('*.prefab')):
            self.recovery.recover(path.stem, self.work / 'damage-ui-native',
                bundle='defaultpackage_assets_prefabs_ui_fight_damagescene_' + path.stem.lower() + '.bundle',
                native_relative=path.relative_to(self.native))
        self.recovery.finalize()
        for path, _, data, record in self.recovery.outputs:
            if data['unresolved']: raise ValueError('Unresolved original damage prefab')
            for item in (path, Path(str(path) + '.meta')):
                expected = self.checked[item.relative_to(self.cache / 'native-export-all').as_posix()]
                if sha(item.read_bytes()) != expected: raise ValueError('Changed original damage prefab')
                self.inputs[item.relative_to(ROOT).as_posix()] = expected
            docs = copy.deepcopy(data['documents'])
            for key, pointer in data['external'].items(): self.objects[pointer['guid']] = self.original(key)
            for key, file_id in data['objectMap'].items():
                owner = self.original(key)
                self.learn(self.recovery.fields(owner), next(iter(docs[file_id].values())), owner)
            for row in data['scripts']:
                name = '.'.join(filter(None, (row['namespace'], row['type'])))
                docs[row['nativeFileID']]['MonoBehaviour']['m_Script'] = self.scripts[name]
                fields = docs[row['nativeFileID']]['MonoBehaviour']
                if row['type'] == 'MultiLanguageText' and fields.get('myKey'): text_keys.add(fields['myKey'])
                if row['type'] == 'ElementConfrontationUI':
                    text_keys.update(fields[key] for key in ('mElementDefenseKey', 'mElementConfrontWeak',
                        'mElementConfrontStrength', 'mElementConfrontImmunity') if fields.get(key))
            value = hashlib.md5(('zjcs:damage-ui:' + path.stem).encode()).hexdigest()
            self.script_guids.add(value)
            self.queue.append((Path('Resources/Combat/DamageUI') / path.name, value, self.replace(docs)))
            ui, = [docs[row['nativeFileID']]['MonoBehaviour'] for row in data['scripts'] if row['type'] == 'CharacterDamageUI']
            prefabs.append(dict(name=path.stem, type=ui['mType'], root=data['root'], original=record,
                scripts=data['scripts'], guid=value))
        if sorted(row['type'] for row in prefabs) != list(range(13)):
            raise ValueError('Original effect UI set changed')
        from npc_travel_language import language
        texts, sources = language(text_keys)
        # Enum None has no user-facing label in the source language table.
        required = text_keys - {'EStatusCustomAction.None'}
        if required - texts.keys(): raise ValueError('Original damage UI language missing: ' + repr(required - texts.keys()))
        self.inputs.update(sources)
        from official_manifest import read_asset_manifest
        records = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        self.configuration(records, prefabs)
        self.language_font = self.chinese_font(records)
        pointer = self.language_font['font_asset']
        for index, (relative, value, data) in enumerate(self.queue):
            if value != pointer['guid']: continue
            fields = data[pointer['fileID']]['MonoBehaviour']
            fields['m_SourceFontFile'] = self.language_font['source_font']
            fields['m_SourceFontFileGUID'] = self.language_font['source_font']['guid']
            fields['m_SourceFontFile_EditorRef'] = self.language_font['source_font']
            self.queue[index] = (Path('Resources/Combat/DamageUI/LanguageFont.asset'), value, data)
            break
        else: raise ValueError('Original damage UI language Font not queued')
        self.queue.append((Path('Resources/Combat/DamageUI/texts.json'),
            hashlib.md5(b'zjcs:damage-ui:texts').hexdigest(),
            (json.dumps(dict(texts=[dict(key=key, text=value) for key, value in sorted(texts.items())]),
                        ensure_ascii=False, indent=2) + '\n').encode()))
        return prefabs

    def configuration(self, records, prefabs):
        from prepare_current_partner_native import identity
        configurations = {}
        for name in ('FightDamageSettings', 'FightSettings'):
            relative = 'ScriptableConfig/Modules/Fight/' + name + '.asset'
            row, = [r for r in records if r['path'] == 'Assets/' + relative]
            obj, = [ptr.deref() for address, ptr in self.recovery.apk.container(row['bundle'])
                if address.casefold() == row['path'].casefold() and ptr.deref().type.name == 'MonoBehaviour']
            path = self.native / relative
            for item in (path, Path(str(path) + '.meta')):
                expected = self.checked[item.relative_to(self.cache / 'native-export-all').as_posix()]
                if sha(item.read_bytes()) != expected: raise ValueError('Changed original damage UI configuration: ' + name)
                self.inputs[item.relative_to(ROOT).as_posix()] = expected
            docs = documents(path.read_text(encoding='utf-8-sig'))
            original = self.recovery.fields(obj)
            configurations[name] = (docs, original, obj)
            self.proof[identity(obj)] = dict(source=self.recovery.apk.provenance(obj),
                original_sha256=sha(obj.get_raw_data()), original_fields=original,
                operation='Original native damage configuration or selected unmodified warmup/element fields.')
        docs, original, obj = configurations['FightDamageSettings']
        fields = docs[11400000]['MonoBehaviour']
        self.learn(original, fields, obj)
        fields['m_Script'] = self.scripts['FightDamageSettings']
        value = hashlib.md5(b'zjcs:damage-ui:FightDamageSettings').hexdigest()
        self.script_guids.add(value)
        self.queue.append((Path('Resources/Combat/DamageUI/FightDamageSettings.asset'), value, docs))
        resources = []
        for ui_type, present in enumerate(original['EffectUIPoolPresents']):
            source_guid = present['EffectUIPrefabProxy']['mAssetGuid']
            record, = [r for r in records if r['guid'] == source_guid]
            prefab, = [p for p in prefabs if p['type'] == ui_type and record['path'].endswith('/' + p['name'] + '.prefab')]
            resources.append(dict(guid=source_guid, resource='Combat/DamageUI/' + prefab['name']))
        settings = configurations['FightSettings'][1]
        for name, data in [('prefab-bindings', dict(prefabs=resources)), ('warmup-settings',
                {key: settings[key] for key in ('ElementConfrontThreshold', 'FightWarmupSettings')})]:
            value = hashlib.md5(('zjcs:damage-ui:' + name).encode()).hexdigest()
            self.queue.append((Path('Resources/Combat/DamageUI/' + name + '.json'), value,
                (json.dumps(data, ensure_ascii=False, indent=2) + '\n').encode()))


def main():
    binding = DamageBinding(validation_directory())
    prefabs = binding.extract()
    binding.damage_root()
    binding.script_guids.update(p['guid'] for p in binding.redirects.values() if p.get('guid'))
    for _, _, data in binding.queue:
        if isinstance(data, dict):
            resolved = binding.replace(data); data.clear(); data.update(resolved)
    # Own this closure: earlier UI modules can keep their material/font variants.
    aliases = {value: hashlib.md5(('zjcs:damage-ui-closure:' + value).encode()).hexdigest()
               for _, value, _ in binding.queue}
    def remap(value):
        if isinstance(value, dict):
            return {key: aliases.get(child, child) if isinstance(key, str) and (key == 'guid' or key.endswith('GUID')) else remap(child)
                    for key, child in value.items()}
        if isinstance(value, list): return [remap(child) for child in value]
        return value
    previous = json.loads(RECEIPT.read_bytes())['installed'] if RECEIPT.exists() else {}
    planned = []
    for relative, value, data in binding.queue:
        data = remap(data)
        raw = encode(data) if isinstance(data, dict) else data
        path = DEST / relative
        if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get(path.relative_to(ROOT).as_posix()):
            raise ValueError('Refusing changed damage UI asset overwrite: ' + str(path))
        planned.append((path, aliases[value], raw, data))
    planned_paths = {item for path, _, _, _ in planned for item in (path, Path(str(path) + '.meta'))}
    for relative, expected in previous.items():
        path = ROOT / relative
        if path in planned_paths or not path.is_file(): continue
        if path.suffix == '.meta' and Path(str(path)[:-5]).is_dir(): continue
        if not path.resolve().is_relative_to(DEST.resolve()) or sha(path.read_bytes()) != expected:
            raise ValueError('Refusing changed obsolete damage UI resource: ' + relative)
        path.unlink()
    installed = {}
    referenced_guids = set()
    def collect_references(value):
        if isinstance(value, dict):
            if value.get('guid'): referenced_guids.add(value['guid'])
            for child in value.values(): collect_references(child)
        elif isinstance(value, list):
            for child in value: collect_references(child)
    for path, value, raw, data in planned:
        collect_references(data)
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists() or path.read_bytes() != raw: path.write_bytes(raw)
        meta = Path(str(path) + '.meta')
        if not meta.exists():
            importer = 'PrefabImporter:\n  externalObjects: {}\n' if path.suffix == '.prefab' else (
                'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: ' + str(next(iter(data))) + '\n'
                if isinstance(data, dict) else '')
            meta.write_text('fileFormatVersion: 2\nguid: ' + value + '\n' + importer, encoding='utf-8', newline='\n')
        for item in (path, meta): installed[item.relative_to(ROOT).as_posix()] = sha(item.read_bytes())
        parent = path.parent
        while parent.is_relative_to(DEST):
            meta = Path(str(parent) + '.meta')
            if not meta.exists():
                value = hashlib.md5(('zjcs:damage-ui-folder:' + parent.relative_to(ROOT).as_posix()).encode()).hexdigest()
                meta.write_text('fileFormatVersion: 2\nguid: ' + value + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n', encoding='utf-8', newline='\n')
            installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes()); parent = parent.parent
    RECEIPT.write_text(json.dumps(dict(manifest=binding.recovery.apk.version, inputs=binding.inputs,
        prefabs=prefabs, resources=binding.proof,
        scripts={name: source for name, source in binding.script_sources.items()
                 if source['pointer']['guid'] in referenced_guids}, installed=installed,
        language_font=binding.language_font,
        damage_floats_root=binding.damage_root_source,
        closure_guids=aliases, generator_sha256=sha(Path(__file__).read_bytes()),
        scope='Complete original world-space damage prefab hierarchy, animation, bitmap fonts and materials.'),
        ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Installed damage UI:', len(prefabs), 'prefabs;', len(installed), 'files', flush=True)


if __name__ == '__main__':
    main()
