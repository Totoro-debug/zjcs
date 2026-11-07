"""Recover GameTimeoutUI and its original SafeArea/ancestor geometry from 884."""
import copy
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from npc_travel_language import language
from prepare_battle_result_actions_native import ResultActionBinding
from prepare_activity_battle_selector_native import encode
from prepare_partner_card_native import guid, script_id, sha
from validation_workspace import validation_directory

DESTINATION = ROOT / 'unity/Assets/Official/Combat/TimeoutUI'
MANIFEST = ROOT / 'assets/reference/battle-timeout-native-source-manifest.json'


class TimeoutBinding(ResultActionBinding):
    def replace(self, value):
        if value == dict(**{'class': 'WorldStateGameObjectConfig', 'ns': 'Zjcs.Official.WorldUI', 'asm': 'Zjcs.Official.WorldUI'}):
            return value
        return super().replace(value)

    def register_scripts(self):
        super().register_scripts()
        path = ROOT / 'unity/Assets/Game/Runtime/Official/BattleResultUI/GameTimeoutUI.cs'
        pointer = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
        self.scripts['GameTimeoutUI'] = self.class_ids[script_id('', 'GameTimeoutUI')] = pointer
        self.script_guids.add(pointer['guid'])
        self.script_sources['GameTimeoutUI'] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
            sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + '.meta').read_bytes()))

    def extract(self):
        self.recovery.recover('FightMainPanel', self.work / 'timeout-native',
            bundle='defaultpackage_assets_prefabs_ui_fight_fightmainpanel_fightmainpanel.bundle',
            native_relative='Prefabs/UI/Fight/FightMainPanel/FightMainPanel.prefab')
        self.recovery.finalize()
        native, _, panel, source = self.recovery.outputs[-1]
        for path in (native, Path(str(native) + '.meta')):
            key = path.relative_to(self.cache / 'native-export-all').as_posix()
            if sha(path.read_bytes()) != self.checked[key]:
                raise ValueError('Changed original timeout source: ' + str(path))
            self.inputs[path.relative_to(ROOT).as_posix()] = self.checked[key]
        original = panel['documents']
        def fields(i): return next(iter(original[i].values()))
        component, = [row for row in panel['scripts'] if row['type'] == 'GameTimeoutUI']
        root = fields(component['nativeFileID'])['m_GameObject']['fileID']
        selected, ancestors = set(), set()
        def visit(go):
            selected.add(go)
            components = fields(go)['m_Component']
            selected.update(row['component']['fileID'] for row in components)
            for child in fields(components[0]['component']['fileID'])['m_Children']:
                visit(fields(child['fileID'])['m_GameObject']['fileID'])
        visit(root)
        parent = fields(fields(root)['m_Component'][0]['component']['fileID'])['m_Father']['fileID']
        while parent:
            ancestor = fields(parent)['m_GameObject']['fileID']
            ancestors.add(ancestor); selected.update((parent, ancestor))
            for row in panel['scripts']:
                if row['type'] == 'SafeArea' and fields(row['nativeFileID'])['m_GameObject']['fileID'] == ancestor:
                    selected.add(row['nativeFileID'])
            parent = fields(parent)['m_Father']['fileID']
        documents = copy.deepcopy({i: row for i, row in original.items() if i in selected})
        for ancestor in ancestors:
            go = documents[ancestor]['GameObject']
            go['m_Component'] = [row for row in go['m_Component'] if row['component']['fileID'] in selected]
            rect = documents[go['m_Component'][0]['component']['fileID']]['RectTransform']
            rect['m_Children'] = [row for row in rect['m_Children'] if row['fileID'] in selected]
        def check(value):
            if isinstance(value, dict):
                if set(value) == {'fileID'} and value['fileID'] and value['fileID'] not in selected:
                    raise ValueError('Timeout branch has an outside local reference: ' + repr(value))
                for child in value.values(): check(child)
            elif isinstance(value, list):
                for child in value: check(child)
        check(documents)
        for key, pointer in panel['external'].items(): self.objects[pointer['guid']] = self.original(key)
        for key, i in panel['objectMap'].items():
            if i in selected:
                owner = self.original(key)
                self.learn(self.recovery.fields(owner), next(iter(documents[i].values())), owner)
        for row in panel['scripts']:
            if row['nativeFileID'] in selected:
                full = '.'.join(filter(None, [row['namespace'], row['type']]))
                documents[row['nativeFileID']]['MonoBehaviour']['m_Script'] = self.scripts[full]
        self.queue.append((Path('Resources/Combat/TimeoutUI/FightMainPanelTimeout.prefab'),
            hashlib.md5(b'zjcs:original-battle-timeout:FightMainPanel').hexdigest(), self.replace(documents)))
        code = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code'
        for relative in ('client-source/GameTimeoutUI.cs', 'client-source/GameEndUIHandler.cs',
                'framework-firstpass-source/SafeArea.cs', 'common-source/Common/GameBattleEndType.cs'):
            path = code / relative
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        texts, inputs = language({'game_end_roundlimit', 'game_end_timelimit', 'game_prepare_timeout'})
        if len(texts) != 3: raise ValueError('Original timeout text is missing')
        self.inputs.update(inputs)
        raw = (json.dumps(dict(texts=[dict(key=k, text=v) for k, v in sorted(texts.items())]),
            ensure_ascii=False, indent=2) + '\n').encode('utf8')
        self.queue.append((Path('Resources/Combat/TimeoutUI/bindings.json'),
            hashlib.md5(b'zjcs:original-battle-timeout:bindings').hexdigest(), raw))
        return dict(source=source, component=component, timeout_root=root, original_root=panel['root'],
            objects=len(selected), retained_ancestors=sorted(ancestors),
            adaptation='Exact TimeOutTipUI subtree and ancestor RectTransforms/Content.SafeArea; other HUD branches remain owned by the battle HUD. Runtime language font is supplied by the host.')


def main():
    binding = TimeoutBinding(validation_directory())
    selection = binding.extract()
    binding.script_guids.update(pointer['guid'] for pointer in binding.redirects.values() if pointer.get('guid'))
    for _, _, data in binding.queue:
        if isinstance(data, dict):
            resolved = binding.replace(data); data.clear(); data.update(resolved)
    existing = {}
    for folder in (ROOT / 'unity/Assets/Official/Partners',
                   ROOT / 'unity/Assets/Official/DailyChallenges/UI'):
        for meta in folder.rglob('*.meta'):
            path = Path(str(meta)[:-5])
            if path.is_file():
                value = guid(meta)
                if value in existing: raise ValueError('Duplicate shared native GUID: ' + value)
                existing[value] = path
    previous = json.loads(MANIFEST.read_bytes())['installed'] if MANIFEST.exists() else {}
    installed, shared, planned = {}, {}, []
    for relative, value, data in binding.queue:
        raw = encode(data) if isinstance(data, dict) else data
        path = existing.get(value, DESTINATION / relative)
        if path.exists() and path.read_bytes().replace(b'\r\n', b'\n') != raw.replace(b'\r\n', b'\n'):
            if not path.is_relative_to(DESTINATION) or sha(path.read_bytes()) != previous.get(path.relative_to(ROOT).as_posix()):
                raise ValueError('Refusing to overwrite changed native asset: ' + str(path))
        planned.append((path, value, raw, data))
    for path, value, raw, data in planned:
        if not path.exists() or (path.is_relative_to(DESTINATION) and path.read_bytes() != raw):
            path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(raw)
        meta = Path(str(path) + '.meta')
        if not meta.exists():
            importer = ('PrefabImporter' if path.suffix == '.prefab' else 'TextScriptImporter'
                if path.suffix == '.json' else 'NativeFormatImporter')
            meta.write_text('fileFormatVersion: 2\nguid: ' + value + '\n' + importer +
                ':\n  externalObjects: {}\n', 'utf8', newline='\n')
        if path.is_relative_to(DESTINATION):
            for entry in (path, meta): installed[entry.relative_to(ROOT).as_posix()] = sha(entry.read_bytes())
        else: shared[path.relative_to(ROOT).as_posix()] = dict(sha256=sha(path.read_bytes()), guid=guid(meta))
        parent = path.parent
        while parent.is_relative_to(DESTINATION):
            meta = Path(str(parent) + '.meta')
            if not meta.exists():
                meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs:timeout-folder:' +
                    parent.relative_to(ROOT).as_posix()).encode()).hexdigest() +
                    '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
            installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes()); parent = parent.parent
    used = set()
    def find_scripts(value):
        if isinstance(value, dict):
            if isinstance(value.get('m_Script'), dict): used.add(value['m_Script'].get('guid'))
            for child in value.values(): find_scripts(child)
        elif isinstance(value, list):
            for child in value: find_scripts(child)
    for _, _, data in binding.queue: find_scripts(data)
    MANIFEST.write_text(json.dumps(dict(manifest=binding.recovery.apk.version, selection=selection,
        inputs=binding.inputs, scripts={k:v for k,v in binding.script_sources.items() if v['pointer']['guid'] in used},
        resources=binding.proof, native_recovery=binding.recovery.proof, installed=installed,
        shared_resources=shared, generator_sha256=sha(Path(__file__).read_bytes())),
        ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed original battle timeout:', len(installed), 'files;', len(shared), 'shared assets.', flush=True)


if __name__ == '__main__':
    main()
