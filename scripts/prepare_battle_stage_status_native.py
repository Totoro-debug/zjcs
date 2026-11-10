"""Recover the selected official stage-status dialog and its native loop row."""
import copy
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from npc_travel_language import language
from prepare_activity_battle_selector_native import SelectorBinding, encode
from prepare_partner_card_native import guid, script_id, sha
from validation_workspace import validation_directory
from prepare_current_partner_native import documents as native_documents

DESTINATION = ROOT / 'unity/Assets/Official/Combat/StageStatusUI'
RECEIPT = ROOT / 'assets/reference/battle-hud/stage-status-native-source-manifest.json'
NAMES = ('FightStageStatusDialog', 'FightStageStatusDialogItemUI')
SCRIPT_NAMES = (*NAMES, 'FightMainPanelStageStatus', 'FightStatusUIItem')


class StageStatusBinding(SelectorBinding):
    def register_scripts(self):
        super().register_scripts()
        runtime = ROOT / 'unity/Assets/Game/Runtime/Official'
        paths = {name: runtime / 'BattleStageStatusUI' / (name + '.cs') for name in SCRIPT_NAMES}
        paths.update({
            'PopupWindow': runtime / 'WorldUI/WorldPopupWindow.cs',
            'PopupWindowSound': runtime / 'WorldUI/WorldPopupWindowSound.cs',
            'BgDialogElement': runtime / 'WorldUI/WorldBgDialogElement.cs',
            'UIBloomGroup': runtime / 'WorldUI/WorldUIBloomGroup.cs',
            'UGUIJszx.HorizontalLayoutGroupJSZX': runtime / 'WorldUI/HorizontalLayoutGroupJSZX.cs',
            'UGUIJszx.VerticalLayoutGroupJSZX': runtime / 'WorldUI/VerticalLayoutGroupJSZX.cs',
            'HyperLinkClickListener': runtime / 'ProfessionUI/HyperLinkClickListener.cs',
            'UITouchPass': runtime / 'PartnersUI/DialoguePlayback/UITouchPass.cs',
            'ButtonSound': runtime / 'WorldUI/WorldButtonSound.cs',
            'NoDrawingRayCast': runtime / 'WorldUI/NoDrawingRayCast.cs',
        })
        for full_name, path in paths.items():
            namespace, _, name = full_name.rpartition('.')
            metadata = Path(str(path) + '.meta')
            pointer = dict(fileID=11500000, guid=guid(metadata), type=3)
            self.scripts[full_name] = self.class_ids[script_id(namespace, name)] = pointer
            self.script_guids.add(pointer['guid'])
            self.script_sources[full_name] = dict(path=path.relative_to(ROOT).as_posix(),
                sha256=sha(path.read_bytes()), meta_sha256=sha(metadata.read_bytes()), pointer=pointer)

    def extract(self):
        for name in NAMES:
            self.recovery.recover(name, self.work / 'stage-status-native',
                bundle='defaultpackage_assets_prefabs_ui_fight_stagestatus_' + name.lower() + '.bundle',
                native_relative='Prefabs/UI/Fight/StageStatus/' + name + '.prefab')
        bundle = 'defaultpackage_assets_prefabs_ui_fight_fightmainpanel_fightmainpanel.bundle'
        objects = self.recovery.apk.objects(bundle)
        source, = [obj for obj in objects if obj.type.name == 'GameObject'
            and obj.read().m_Name == 'FightMainPanel'
            and obj.read().m_Component[0].component.read().m_Father.path_id == 0]
        for name in ('Content', 'Top', 'FixedContent', 'StageStatus'):
            children = source.read().m_Component[0].component.read().m_Children
            source, = [child.read().m_GameObject.deref() for child in children
                if child.read().m_GameObject.read().m_Name == name]
        self.recovery.recover('StageStatus', self.work / 'stage-status-native', bundle=bundle,
            native_relative='Prefabs/UI/Fight/FightMainPanel/FightMainPanel.prefab',
            source_root=source, native_root_id=1025901758691103)
        # The retained HUD parent is an independently pinned source. Verify its
        # actual ancestor geometry before attaching this original subtree.
        parent_path = ROOT / 'unity/Assets/Official/Combat/HUD/panels-runtime.json'
        parent = next(row for row in json.loads(parent_path.read_bytes())['prefabs'] if row['key'] == 'main')
        self.inputs[parent_path.relative_to(ROOT).as_posix()] = sha(parent_path.read_bytes())
        native_docs = native_documents(self.recovery.outputs[-1][0].read_text(encoding='utf-8'))
        ancestor, = [doc['GameObject'] for doc in native_docs.values() if 'GameObject' in doc
            and doc['GameObject']['m_Name'] == 'FightMainPanel']
        geometry = []
        path = '/FightMainPanel'
        for name in ('Content', 'Top', 'FixedContent'):
            transform = next(iter(native_docs[ancestor['m_Component'][0]['component']['fileID']].values()))
            children = [next(iter(native_docs[row['fileID']].values())) for row in transform['m_Children']]
            ancestor, = [native_docs[row['m_GameObject']['fileID']]['GameObject'] for row in children
                if native_docs[row['m_GameObject']['fileID']]['GameObject']['m_Name'] == name]
            path += '/' + name
            rect = next(iter(native_docs[ancestor['m_Component'][0]['component']['fileID']].values()))
            retained, = [row for row in parent['nodes'] if row['path'] == path]
            fields = {}
            for source_key, target_key in (('m_AnchorMin', 'anchor_min'), ('m_AnchorMax', 'anchor_max'),
                    ('m_AnchoredPosition', 'anchored_position'), ('m_SizeDelta', 'size_delta'),
                    ('m_Pivot', 'pivot'), ('m_LocalScale', 'scale')):
                values = [rect[source_key][axis] for axis in ('x', 'y')]
                if any(abs(a-b) > 0.00001 for a, b in zip(values, retained[target_key])):
                    raise ValueError('Retained stage-status parent geometry differs: ' + path + '/' + source_key)
                fields[source_key] = values
            rotation = rect['m_LocalRotation']
            if any(abs(rotation[axis]) > 0.00001 for axis in ('x', 'y', 'z')) or abs(rotation['w']-1) > 0.00001 or retained['rotation'] != 0:
                raise ValueError('Retained stage-status parent rotation differs: ' + path)
            geometry.append(dict(path=path, fields=fields))
        self.strip_parent = dict(manifest='854_156383', source=parent_path.relative_to(ROOT).as_posix(),
            matched_original_ancestors=geometry)
        clip_path = self.native / 'Prefabs/UI/Fight/FightMainPanel/FightMainPanel_Show.anim'
        for asset in (clip_path, Path(str(clip_path) + '.meta')):
            key = asset.relative_to(self.cache / 'native-export-all').as_posix()
            if sha(asset.read_bytes()) != self.checked[key]:
                raise ValueError('Changed original stage-status show animation: ' + str(asset))
            self.inputs[asset.relative_to(ROOT).as_posix()] = self.checked[key]
        clip = next(iter(native_documents(clip_path.read_text(encoding='utf-8')).values()))['AnimationClip']
        show, = [animation for node in parent['nodes'] for animation in node.get('animations', [])
            if animation['name'] == 'FightMainPanel_Show']
        matched_tracks = []
        for track in clip['m_FloatCurves']:
            if track['path'] != 'Content/Top/FixedContent/StageStatus': continue
            retained, = [row for row in show['tracks'] if row['path'] == '/FightMainPanel/' + track['path']
                and row['property'] == track['attribute']]
            keys = track['curve']['m_Curve']
            if len(keys) != len(retained['keys']): raise ValueError('Changed stage-status show key count')
            for original, current in zip(keys, retained['keys']):
                for source_key, target_key in (('time', 'time'), ('value', 'value'),
                        ('inSlope', 'in_tangent'), ('outSlope', 'out_tangent')):
                    if abs(original[source_key] - current[target_key]) > 0.00001:
                        raise ValueError('Retained stage-status show curve differs: ' + track['attribute'])
                if original['weightedMode'] != 0:
                    raise ValueError('Original stage-status show curve uses unhandled weighted tangents')
            matched_tracks.append(dict(original=track, retained=retained))
        if len(matched_tracks) != 2: raise ValueError('Original stage-status slide/fade channels changed')
        self.strip_parent['matched_original_show_tracks'] = matched_tracks
        self.recovery.finalize()
        # Register both prefab identities before resolving their references.
        for native, _, panel, record in self.recovery.outputs:
            target_guid = hashlib.md5(('zjcs:battle-stage-status:' + record['name'] + '.prefab').encode()).hexdigest()
            source_guid = guid(Path(str(native) + '.meta'))
            self.prefab_references[source_guid] = target_guid
            self.redirects[source_guid] = dict(fileID=panel['root'], guid=target_guid, type=3)
            self.prefab_object_pointers.update({key: dict(fileID=file_id, guid=target_guid, type=3)
                for key, file_id in panel['objectMap'].items()})
            self.script_guids.add(target_guid)
            for path in (native, Path(str(native) + '.meta')):
                key = path.relative_to(self.cache / 'native-export-all').as_posix()
                if sha(path.read_bytes()) != self.checked[key]:
                    raise ValueError('Changed native stage-status source: ' + str(path))
                self.inputs[path.relative_to(ROOT).as_posix()] = self.checked[key]
        keys = set()
        records = []
        for native, _, panel, record in self.recovery.outputs:
            if panel['unresolved']:
                raise ValueError('Unresolved official stage-status fields: ' + repr(panel['unresolved']))
            for key, pointer in panel['external'].items(): self.objects[pointer['guid']] = self.original(key)
            documents = copy.deepcopy(panel['documents'])
            for key, file_id in panel['objectMap'].items():
                owner = self.original(key)
                self.learn(self.recovery.fields(owner), next(iter(documents[file_id].values())), owner)
            for row in panel['scripts']:
                fields = documents[row['nativeFileID']]['MonoBehaviour']
                full_name = '.'.join(filter(None, [row['namespace'], row['type']]))
                if full_name not in self.scripts:
                    raise ValueError('Unmigrated original stage-status script: ' + full_name)
                fields['m_Script'] = self.scripts[full_name]
                if row['type'] == 'MultiLanguageText': keys.add(fields['myKey'])
                if row['type'] == 'PopupWindowSound':
                    owner = self.original(row['original'])
                    for field in ('ShowSound', 'HideSound'):
                        fields[field] = self.sound(self.recovery.fields(owner)[field], owner)
                if row['type'] == 'ButtonSound':
                    owner = self.original(row['original'])
                    fields['ClickEvent'] = self.sound(self.recovery.fields(owner)['ClickEvent'], owner)
            target_guid = self.prefab_references[guid(Path(str(native) + '.meta'))]
            self.queue.append((Path('Resources/Combat/StageStatusUI') / (record['name'] + '.prefab'),
                target_guid, self.replace(documents)))
            records.append(record)
        keys.update(('fight_status_props_scale_format', 'fight_status_props_scale_not_exist',
            'fight_status_props_not_scale', 'fight_status_props_has_scale'))
        texts, inputs = language(keys - {'', '{0}'})
        if set(texts) != keys - {'', '{0}'}:
            raise ValueError('The selected original stage-status language keys are incomplete')
        self.inputs.update(inputs)
        self.queue.extend((path, value, raw) for path, (value, raw) in self.audio_outputs.items())
        # Retain the existing shared mask as its own pinned 854 source, exactly
        # as the shared battle report does; do not relabel it as a current asset.
        mask_path = ROOT / 'unity/Assets/Official/Skills/skill-ui.json'
        mask = json.loads(mask_path.read_bytes())['popup_masks']['3']
        self.inputs[mask_path.relative_to(ROOT).as_posix()] = sha(mask_path.read_bytes())
        for relative_path in ('unity/Assets/Game/Resources/Map11/UI/WorldCanvas.prefab',
                'unity/Assets/Game/Resources/World/Mainline/ScreenBlur.mat',
                'unity/Assets/Game/Runtime/Presentation/World/Shaders/ScreenBlur.shader'):
            path = ROOT / relative_path
            for asset in (path, Path(str(path) + '.meta')):
                self.inputs[asset.relative_to(ROOT).as_posix()] = sha(asset.read_bytes())
        prefix = '/WorldCanvas/' + mask['prefab_path']
        def relative(field):
            path = mask['fields'][field]
            if not path.startswith(prefix): raise ValueError('Original mask pointer left its root')
            return path[len(prefix):].lstrip('/')
        popup, = [panel['documents'][row['nativeFileID']]['MonoBehaviour']
            for _, _, panel, _ in self.recovery.outputs for row in panel['scripts'] if row['type'] == 'PopupWindow']
        if popup['mMaskType'] != 3: raise ValueError('Original stage-status mask type changed')
        settings_path = self.native / 'ScriptableConfig/Modules/Fight/FightSettings.asset'
        settings_key = settings_path.relative_to(self.cache / 'native-export-all').as_posix()
        if sha(settings_path.read_bytes()) != self.checked[settings_key]:
            raise ValueError('Changed original FightSettings source: ' + str(settings_path))
        settings = next(iter(native_documents(settings_path.read_text()).values()))['MonoBehaviour']
        scale_ids = [row['StatusId'] for row in settings['FightStageStatusDescTypes'] if row['Type'] == 1]
        self.inputs[settings_path.relative_to(ROOT).as_posix()] = sha(settings_path.read_bytes())
        raw = (json.dumps(dict(manifest=self.recovery.apk.version, scaleStatusIds=scale_ids,
            popup=dict(maskType=popup['mMaskType'], closeByMask=bool(popup['mCloseByClickMask']),
                mask=dict(prefabResource=mask['prefab_resource'], prefabPath=mask['prefab_path'],
                    blurMaterial=mask['blur_material'], buttonPath=relative('mBtn'),
                    imagePath=relative('mImage'), uvRect=mask['raw_image']['m_UVRect'])),
            texts=[dict(key=k, text=v) for k, v in sorted(texts.items())]),
            ensure_ascii=False, indent=2) + '\n').encode()
        self.queue.append((Path('Resources/Combat/StageStatusUI/texts.json'),
            hashlib.md5(b'zjcs:battle-stage-status:texts').hexdigest(), raw))
        code = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code'
        for name in (*SCRIPT_NAMES, 'FightStatusItemBase', 'FightStatusDescProvider',
                'FightStatusPropsScaleFactorDescProvider', 'IFightStatusDescUpdatable'):
            path = code / 'client-source' / (name + '.cs')
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        for name in ('MsgGameStartComplete', 'MsgGamePropsScaleUpdate', 'PlayerGame', 'Common/DoubleUtils'):
            path = code / 'common-source' / (name + '.cs')
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        return records


def main():
    binding = StageStatusBinding(validation_directory())
    records = binding.extract()
    binding.script_guids.update(pointer['guid'] for pointer in binding.redirects.values() if pointer.get('guid'))
    for _, _, data in binding.queue:
        if isinstance(data, dict):
            resolved = binding.replace(data); data.clear(); data.update(resolved)
    existing = {}
    # Reuse only exact native assets from the selected source migration. Never
    # overwrite a shared asset or reuse a matching name as asset identity.
    for folder in (ROOT / 'unity/Assets/Official/Partners', ROOT / 'unity/Assets/Official/DailyChallenges/UI'):
        for meta in folder.rglob('*.meta'):
            path = Path(str(meta)[:-5])
            if path.is_file():
                value = guid(meta)
                if value in existing: raise ValueError('Shared native GUID collision: ' + value)
                existing[value] = path
    previous = json.loads(RECEIPT.read_bytes())['installed'] if RECEIPT.exists() else {}
    aliases = {}
    def own_references(data):
        if isinstance(data, dict):
            return {key: aliases.get(child, child) if isinstance(child, str)
                and (key == 'guid' or key.endswith('GUID')) else own_references(child)
                for key, child in data.items()}
        if isinstance(data, list): return [own_references(child) for child in data]
        return data
    # A shared module can own a different converted variant of the same original
    # asset. Isolate that variant and all assets whose references must change;
    # never rewrite another module's payload to force sharing.
    while True:
        changed = False
        for relative, value, data in binding.queue:
            if value in aliases or value not in existing: continue
            resolved = own_references(data)
            raw = encode(resolved) if isinstance(resolved, dict) else resolved
            if existing[value].read_bytes().replace(b'\r\n', b'\n') != raw.replace(b'\r\n', b'\n'):
                aliases[value] = hashlib.md5(('zjcs:stage-status-variant:' + value).encode()).hexdigest()
                changed = True
        if not changed: break
    planned = []
    for relative, value, data in binding.queue:
        value, data = aliases.get(value, value), own_references(data)
        raw = encode(data) if isinstance(data, dict) else data
        path = existing.get(value, DESTINATION / relative)
        if path.exists() and path.read_bytes().replace(b'\r\n', b'\n') != raw.replace(b'\r\n', b'\n'):
            if not path.is_relative_to(DESTINATION) or sha(path.read_bytes()) != previous.get(path.relative_to(ROOT).as_posix()):
                raise ValueError('Refusing to replace changed native stage-status asset: ' + str(path))
        planned.append((path, value, raw, data))
    installed, shared = {}, {}
    for path, value, raw, data in planned:
        if not path.exists() or (path.is_relative_to(DESTINATION) and path.read_bytes() != raw):
            path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(raw)
        meta = Path(str(path) + '.meta')
        if not meta.exists():
            if path.suffix == '.prefab': importer = 'PrefabImporter:\n  externalObjects: {}\n'
            elif path.suffix == '.json': importer = 'TextScriptImporter:\n  externalObjects: {}\n'
            elif isinstance(data, dict): importer = 'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: ' + str(next(iter(data))) + '\n'
            else: importer = ''
            meta.write_bytes(('fileFormatVersion: 2\nguid: ' + value + '\n' + importer).encode())
        if path.is_relative_to(DESTINATION):
            for p in (path, meta): installed[p.relative_to(ROOT).as_posix()] = sha(p.read_bytes())
        else: shared[path.relative_to(ROOT).as_posix()] = dict(sha256=sha(path.read_bytes()),
            meta_sha256=sha(meta.read_bytes()), guid=guid(meta))
        parent = path.parent
        while parent.is_relative_to(DESTINATION):
            meta = Path(str(parent) + '.meta')
            if not meta.exists():
                value = hashlib.md5(('zjcs:stage-status-folder:' + parent.relative_to(ROOT).as_posix()).encode()).hexdigest()
                meta.write_bytes(('fileFormatVersion: 2\nguid: ' + value + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n').encode())
            installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes()); parent = parent.parent
    used = set()
    def find_scripts(data):
        if isinstance(data, dict):
            if isinstance(data.get('m_Script'), dict): used.add(data['m_Script'].get('guid'))
            for child in data.values(): find_scripts(child)
        elif isinstance(data, list):
            for child in data: find_scripts(child)
    for _, _, data in binding.queue: find_scripts(data)
    runtime_paths = list((ROOT / 'unity/Assets/Game/Runtime/Official/BattleStageStatusUI').glob('*'))
    runtime_paths.extend(ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat' / name for name in (
        'OfficialBattleStageStatusDialogView.cs', 'OfficialBattleStageStatusDialogView.cs.meta',
        'OfficialBattleStageStatusStripView.cs', 'OfficialBattleStageStatusStripView.cs.meta',
        'OfficialBattleHudAnimationPlayer.cs', 'OfficialBattleHudAnimationPlayer.cs.meta',
        'OfficialBattleHudView.cs', 'OfficialBattleHudView.Phases.cs', 'Zjcs.Presentation.Combat.asmdef'))
    runtime_ports = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes())
        for path in runtime_paths if path.is_file()}
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_bytes((json.dumps(dict(manifest=binding.recovery.apk.version, prefabs=records,
        inputs=binding.inputs, native_recovery=binding.recovery.proof, resources=binding.proof,
        scripts={name: row for name, row in binding.script_sources.items() if row['pointer']['guid'] in used},
        audio=binding.audio_proof, installed=installed, shared_resources=shared, variant_guids=aliases,
        retained_mask=dict(manifest='854_156383', source='unity/Assets/Official/Skills/skill-ui.json'),
        retained_stage_strip_parent=binding.strip_parent,
        generator_sha256=sha(Path(__file__).read_bytes()), runtime_ports=runtime_ports),
        ensure_ascii=False, indent=2) + '\n').encode())
    print('Installed original stage-status dialog:', len(installed), 'files;', len(shared), 'shared assets.', flush=True)


if __name__ == '__main__':
    main()
