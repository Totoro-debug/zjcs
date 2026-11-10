"""Restore the selected official GameRoundScene without rebuilding its hierarchy."""
import copy
import hashlib
import json
import re
import struct
from pathlib import Path

from official_apk import ROOT
from prepare_activity_battle_selector_native import SelectorBinding, encode
from prepare_partner_card_native import guid, script_id, sha
from validation_workspace import validation_directory
from prepare_current_partner_native import documents as native_documents

DESTINATION = ROOT / 'unity/Assets/Official/Combat/RoundUI'
RECEIPT = ROOT / 'assets/reference/battle-hud/round-scene-native-source-manifest.json'
NAMES = ('GameRoundScene', 'SpriteRendererCanvas', 'GameSceneObjectOrderLayer')


class RoundSceneBinding(SelectorBinding):
    def register_scripts(self):
        super().register_scripts()
        for name in NAMES:
            path = ROOT / 'unity/Assets/Game/Runtime/Official/BattleRoundUI' / (name + '.cs')
            meta = Path(str(path) + '.meta')
            pointer = dict(fileID=11500000, guid=guid(meta), type=3)
            self.scripts[name] = self.class_ids[script_id('', name)] = pointer
            self.script_guids.add(pointer['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(),
                sha256=sha(path.read_bytes()), meta_sha256=sha(meta.read_bytes()), pointer=pointer)

    def extract(self):
        self.recovery.recover('GameRoundScene', self.work / 'round-scene-native',
            bundle='defaultpackage_assets_prefabs_ui_fight_gameroundscene.bundle',
            native_relative='Prefabs/UI/Fight/GameRoundScene.prefab')
        self.recovery.recover('GameRoundCharacterItem_Scene', self.work / 'round-scene-native',
            bundle='defaultpackage_assets_prefabs_ui_fight_gameroundcharacteritem_scene.bundle',
            native_relative='Prefabs/UI/Fight/GameRoundCharacterItem_Scene.prefab')
        bundle = 'defaultpackage_assets_prefabs_ui_fight_fightmainpanel_fightmainpanel.bundle'
        root, = [obj for obj in self.recovery.apk.objects(bundle) if obj.type.name == 'GameObject'
            and obj.read().m_Name == 'FightMainPanel'
            and obj.read().m_Component[0].component.read().m_Father.path_id == 0]
        for name in ('Content', 'Top', 'FixedContent', 'RoundUI'):
            root, = [child.read().m_GameObject.deref()
                for child in root.read().m_Component[0].component.read().m_Children
                if child.read().m_GameObject.read().m_Name == name]
        self.recovery.recover('RoundUI', self.work / 'round-scene-native', bundle=bundle,
            native_relative='Prefabs/UI/Fight/FightMainPanel/FightMainPanel.prefab',
            source_root=root, native_root_id=1256069497284392)
        self.recovery.finalize()
        native, _, panel, record = self.recovery.outputs[0]
        for path in (native, Path(str(native) + '.meta')):
            key = path.relative_to(self.cache / 'native-export-all').as_posix()
            if sha(path.read_bytes()) != self.checked[key]:
                raise ValueError('Changed native round-scene source: ' + str(path))
            self.inputs[path.relative_to(ROOT).as_posix()] = self.checked[key]
        if panel['unresolved']:
            raise ValueError('Unresolved official round-scene fields: ' + repr(panel['unresolved']))
        documents = copy.deepcopy(panel['documents'])
        for key, pointer in panel['external'].items(): self.objects[pointer['guid']] = self.original(key)
        for key, file_id in panel['objectMap'].items():
            owner = self.original(key)
            self.learn(self.recovery.fields(owner), next(iter(documents[file_id].values())), owner)
        for row in panel['scripts']:
            if row['namespace'] or row['type'] not in NAMES:
                raise ValueError('Unmigrated round-scene script: ' + repr(row))
            documents[row['nativeFileID']]['MonoBehaviour']['m_Script'] = self.scripts[row['type']]
        value = hashlib.md5(b'zjcs:battle-round-native:GameRoundScene.prefab').hexdigest()
        self.script_guids.add(value)
        self.queue.append((Path('Resources/Combat/RoundUI/GameRoundScene.prefab'), value, self.replace(documents)))
        native, _, card, card_record = self.recovery.outputs[1]
        for path in (native, Path(str(native) + '.meta')):
            key = path.relative_to(self.cache / 'native-export-all').as_posix()
            if sha(path.read_bytes()) != self.checked[key]:
                raise ValueError('Changed original timeline card source: ' + str(path))
            self.inputs[path.relative_to(ROOT).as_posix()] = self.checked[key]
        if card['unresolved']: raise ValueError('Unresolved original timeline card fields')
        retained_path = ROOT / 'unity/Assets/Official/Combat/Timeline/Native/round_character_scene.prefab'
        retained_source = ROOT / 'assets/reference/battle-hud/timeline-source-manifest.json'
        retained = native_documents(retained_path.read_text(encoding='utf-8'))
        available = {}
        for meta in retained_path.parent.parent.rglob('*.meta'):
            path = Path(str(meta)[:-5])
            if path.is_file(): available[guid(meta)] = path
        circle = ROOT / 'unity/Assets/Game/Runtime/Official/WorldUI/WorldCircleSpriteRenderer.cs'
        available[guid(Path(str(circle) + '.meta'))] = circle
        for meta in (ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/Shaders').glob('*.meta'):
            path = Path(str(meta)[:-5])
            if path.is_file(): available[guid(meta)] = path
        pending = list(set(re.findall(rb'guid: ([0-9a-f]{32})', retained_path.read_bytes())))
        self.retained_resources = {}
        while pending:
            value = pending.pop().decode()
            if value in self.script_guids: continue
            if value.startswith('0000000000000000'):
                self.script_guids.add(value); continue
            path = available.get(value)
            if path is None: raise ValueError('Unresolved retained timeline dependency: ' + value)
            meta = Path(str(path) + '.meta')
            self.retained_resources[path.relative_to(ROOT).as_posix()] = dict(sha256=sha(path.read_bytes()),
                meta_sha256=sha(meta.read_bytes()), guid=value)
            self.script_guids.add(value)
            pending.extend(set(re.findall(rb'guid: ([0-9a-f]{32})', path.read_bytes())))
        root_transform, = [doc['Transform'] for doc in retained.values()
            if 'Transform' in doc and doc['Transform']['m_Father']['fileID'] == 0]
        root = root_transform['m_GameObject']['fileID']
        order, = [row for row in card['scripts'] if row['type'] == 'GameSceneObjectOrderLayer']
        order_id = order['nativeFileID']
        order_doc = copy.deepcopy(card['documents'][order_id])
        group_id = order_doc['MonoBehaviour']['mSortingGroup']['fileID']
        group_doc = copy.deepcopy(card['documents'][group_id])
        assert 'SortingGroup' in group_doc and order_doc['MonoBehaviour']['m_GameObject']['fileID'] == card['root']
        assert group_doc['SortingGroup']['m_GameObject']['fileID'] == card['root']
        assert group_id not in retained and order_id not in retained
        order_doc['MonoBehaviour']['m_GameObject'] = dict(fileID=root)
        order_doc['MonoBehaviour']['m_Script'] = self.scripts['GameSceneObjectOrderLayer']
        group_doc['SortingGroup']['m_GameObject'] = dict(fileID=root)
        retained[group_id] = group_doc; retained[order_id] = order_doc
        retained[root]['GameObject']['m_Component'].extend(dict(component=dict(fileID=file_id))
            for file_id in (group_id, order_id))
        value = hashlib.md5(b'zjcs:battle-round-native:retained-card-sorting').hexdigest()
        self.script_guids.add(value)
        self.queue.append((Path('Resources/Combat/RoundUI/GameRoundCharacterItem_Scene.prefab'), value, retained))
        for path in (retained_path, Path(str(retained_path) + '.meta'), retained_source):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        self.card_sorting = dict(original=card_record, component=order['original'],
            native_order_file_id=order_id, native_sorting_group_file_id=group_id,
            retained_card=dict(manifest='854_156383', prefab=retained_path.relative_to(ROOT).as_posix(),
                source_manifest=retained_source.relative_to(ROOT).as_posix()),
            operation='Retain the existing source-derived card unchanged; append the two original root sorting components and rebind their owner/script only. Full native card script migration is pending.')
        native, _, round_ui, anchor_record = self.recovery.outputs[2]
        for path in (native, Path(str(native) + '.meta')):
            key = path.relative_to(self.cache / 'native-export-all').as_posix()
            if sha(path.read_bytes()) != self.checked[key]:
                raise ValueError('Changed original HUD anchor source: ' + str(path))
            self.inputs[path.relative_to(ROOT).as_posix()] = self.checked[key]
        if round_ui['unresolved']: raise ValueError('Unresolved original RoundUI fields')
        hud_path = ROOT / 'unity/Assets/Official/Combat/HUD/panels-runtime.json'
        timeline_path = ROOT / 'unity/Assets/Official/Combat/Timeline/Resources/Combat/OfficialBattleTimelineAssets.asset'
        hud = next(row for row in json.loads(hud_path.read_bytes())['prefabs'] if row['key'] == 'main')
        anchor = re.search(r'^  Anchor: (.+)$', timeline_path.read_text(encoding='utf-8'), re.M).group(1)
        if anchor != 'Content/Top/FixedContent/RoundUI/Linear':
            raise ValueError('Review a changed retained timeline anchor')
        checked_nodes = []
        for name in ('RoundUI', 'Linear'):
            transform, = [doc['RectTransform'] for doc in round_ui['documents'].values()
                if 'RectTransform' in doc and round_ui['documents'][doc['RectTransform']['m_GameObject']['fileID']]['GameObject']['m_Name'] == name]
            path = '/FightMainPanel/Content/Top/FixedContent/RoundUI' + ('/Linear' if name == 'Linear' else '')
            retained, = [node for node in hud['nodes'] if node['path'] == path]
            fields = {}
            for key, source in (('anchor_min', 'm_AnchorMin'), ('anchor_max', 'm_AnchorMax'),
                ('anchored_position', 'm_AnchoredPosition'), ('size_delta', 'm_SizeDelta'),
                ('pivot', 'm_Pivot'), ('scale', 'm_LocalScale')):
                original = [transform[source][axis] for axis in ('x', 'y')]
                if [struct.pack('<f', value) for value in original] != [struct.pack('<f', value) for value in retained[key]]:
                    raise ValueError('Retained HUD anchor geometry differs: ' + path + '/' + key)
                fields[key] = dict(original=original, retained=retained[key])
            if transform['m_LocalScale']['z'] != 1 or transform['m_LocalRotation'] != dict(x=0, y=0, z=0, w=1) or retained['rotation'] != 0:
                raise ValueError('Retained HUD anchor rotation/scale differs: ' + path)
            checked_nodes.append(dict(path=path, fields=fields, rotation=transform['m_LocalRotation']))
        for path in (hud_path, timeline_path):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        self.retained_hud_anchor = dict(original=anchor_record, retained_manifest='854_156383',
            anchor=anchor, nodes=checked_nodes, comparison='Original serialized float32 geometry; no UI field changes.')
        code = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code/client-source'
        for name in NAMES:
            path = code / (name + '.cs')
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        return record


def main():
    binding = RoundSceneBinding(validation_directory())
    record = binding.extract()
    binding.script_guids.update(pointer['guid'] for pointer in binding.redirects.values() if pointer.get('guid'))
    for _, _, data in binding.queue:
        if isinstance(data, dict):
            resolved = binding.replace(data); data.clear(); data.update(resolved)
    existing = {}
    for folder in (ROOT / 'unity/Assets/Official/Partners', ROOT / 'unity/Assets/Official/DailyChallenges/UI',
                   ROOT / 'unity/Assets/Official/Combat/StageStatusUI'):
        for meta in folder.rglob('*.meta'):
            path = Path(str(meta)[:-5])
            if path.is_file():
                value = guid(meta)
                if value in existing: raise ValueError('Shared native GUID collision: ' + value)
                existing[value] = path
    aliases = {}
    def own_references(data):
        if isinstance(data, dict):
            return {key: aliases.get(child, child) if isinstance(child, str)
                and (key == 'guid' or key.endswith('GUID')) else own_references(child)
                for key, child in data.items()}
        if isinstance(data, list): return [own_references(child) for child in data]
        return data
    while True:
        changed = False
        for _, value, data in binding.queue:
            if value in aliases or value not in existing: continue
            resolved = own_references(data)
            raw = encode(resolved) if isinstance(resolved, dict) else resolved
            if existing[value].read_bytes().replace(b'\r\n', b'\n') != raw.replace(b'\r\n', b'\n'):
                aliases[value] = hashlib.md5(('zjcs:round-scene-variant:' + value).encode()).hexdigest()
                changed = True
        if not changed: break
    previous = json.loads(RECEIPT.read_bytes())['installed'] if RECEIPT.exists() else {}
    planned = []
    for relative, value, data in binding.queue:
        value, data = aliases.get(value, value), own_references(data)
        raw = encode(data) if isinstance(data, dict) else data
        path = existing.get(value, DESTINATION / relative)
        if path.exists() and path.read_bytes().replace(b'\r\n', b'\n') != raw.replace(b'\r\n', b'\n'):
            if not path.is_relative_to(DESTINATION) or sha(path.read_bytes()) != previous.get(path.relative_to(ROOT).as_posix()):
                raise ValueError('Refusing changed round-scene asset overwrite: ' + str(path))
        planned.append((path, value, raw, data))
    installed, shared = {}, dict(binding.retained_resources)
    for path, value, raw, data in planned:
        if not path.exists() or path.is_relative_to(DESTINATION) and path.read_bytes() != raw:
            path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(raw)
        meta = Path(str(path) + '.meta')
        if not meta.exists():
            importer = 'PrefabImporter:\n  externalObjects: {}\n' if path.suffix == '.prefab' else (
                'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: ' + str(next(iter(data))) + '\n'
                if isinstance(data, dict) else '')
            meta.write_bytes(('fileFormatVersion: 2\nguid: ' + value + '\n' + importer).encode())
        if path.is_relative_to(DESTINATION):
            for entry in (path, meta): installed[entry.relative_to(ROOT).as_posix()] = sha(entry.read_bytes())
        else:
            shared[path.relative_to(ROOT).as_posix()] = dict(sha256=sha(path.read_bytes()),
                meta_sha256=sha(meta.read_bytes()), guid=guid(meta))
        parent = path.parent
        while parent.is_relative_to(DESTINATION):
            meta = Path(str(parent) + '.meta')
            if not meta.exists():
                value = hashlib.md5(('zjcs:round-scene-folder:' + parent.relative_to(ROOT).as_posix()).encode()).hexdigest()
                meta.write_bytes(('fileFormatVersion: 2\nguid: ' + value + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n').encode())
            installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes()); parent = parent.parent
    runtime = ROOT / 'unity/Assets/Game/Runtime/Official/BattleRoundUI'
    ports = list(runtime.glob('*')) + [ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat' / name for name in (
        'OfficialBattleSceneTimelineView.cs', 'OfficialBattleSceneTimelineView.cs.meta', 'Zjcs.Presentation.Combat.asmdef')]
    RECEIPT.write_bytes((json.dumps(dict(manifest=binding.recovery.apk.version, prefab=record,
        inputs=binding.inputs, native_recovery=binding.recovery.proof, resources=binding.proof,
        scripts={name: binding.script_sources[name] for name in NAMES}, installed=installed,
        shared_resources=shared, variant_guids=aliases, generator_sha256=sha(Path(__file__).read_bytes()),
        card_sorting=binding.card_sorting,
        retained_hud_anchor=binding.retained_hud_anchor,
        runtime_ports={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in ports if path.is_file()}),
        ensure_ascii=False, indent=2) + '\n').encode())
    print('Installed original GameRoundScene:', len(installed), 'files;', len(shared), 'shared assets.', flush=True)


if __name__ == '__main__':
    main()
