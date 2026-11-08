"""Recover the complete selected official Arena result, including animations."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET

from npc_travel_language import language
from official_apk import ROOT
from official_manifest import read_asset_manifest
from prepare_demon_result_native import DemonResultBinding
from prepare_partner_card_native import guid, script_id, sha
from prepare_current_partner_native import identity, pointer as source_pointer
from validation_workspace import validation_directory

NAME = 'arena-result-native-source-manifest.json'
DESTINATION = ROOT / 'unity/Assets/Official/Combat/ArenaResult'


class ArenaResultBinding(DemonResultBinding):
    result_name = 'arena-result'
    generator_path = Path(__file__)

    def load(self, source_guid):
        if source_guid in self.redirects: return self.redirects[source_guid]
        obj = self.objects.get(source_guid)
        if obj is not None:
            key = identity(obj)
            proof_path = ROOT / 'assets/reference/partner-detail-actor-native-source-manifest.json'
            if not hasattr(self, 'shared_actors'):
                self.shared_actors = json.loads(proof_path.read_bytes())
                self.shared_actor_paths = {}
            proof = self.shared_actors
            actor_key = key
            if actor_key not in proof['native_actor_assets']:
                actor_key = next((row['source'] for row in proof['actor_data']['actors']
                    if row.get('splitSource') == key), key)
            if actor_key in proof['native_actor_assets']:
                pointer = proof['native_actor_assets'][actor_key]
                original = proof['actor_data']['sources'][key]
                if sha(obj.get_raw_data()) != original['payload_sha256']:
                    raise ValueError('Shared original NPC skeleton identity differs')
                if actor_key not in self.shared_actor_paths:
                    matches = []
                    for relative, expected in proof['installed_output_sha256'].items():
                        if not relative.endswith('.meta'): continue
                        path = ROOT / relative
                        if path.is_file() and guid(path) == pointer['guid']:
                            if sha(path.read_bytes()) != expected: raise ValueError('Shared skeleton metadata changed')
                            matches.append(Path(str(path)[:-5]))
                    path, = matches
                    expected = proof['installed_output_sha256'][path.relative_to(ROOT).as_posix()]
                    if sha(path.read_bytes()) != expected: raise ValueError('Shared native skeleton changed')
                    self.shared_actor_paths[actor_key] = path
                    self.inputs[path.relative_to(ROOT).as_posix()] = expected
                    self.inputs[path.relative_to(ROOT).as_posix() + '.meta'] = sha(Path(str(path) + '.meta').read_bytes())
                    self.inputs[proof_path.relative_to(ROOT).as_posix()] = sha(proof_path.read_bytes())
                self.redirects[source_guid] = pointer
                self.script_guids.add(pointer['guid'])
                return pointer
        return super().load(source_guid)

    def restore(self, panel):
        super().restore(panel)
        adapters = []
        for row in panel['scripts']:
            if row['type'] != 'SkeletonGraphic': continue
            fields = panel['documents'][row['nativeFileID']]['MonoBehaviour']
            if not fields['skeletonDataAsset']['fileID']: continue
            go = fields['m_GameObject']['fileID']
            root = panel['documents'][go]['GameObject']
            render_root = root['m_Component'][0]['component']['fileID']
            added = max(panel['documents']) + 1
            root['m_Component'].insert(1, dict(component=dict(fileID=added)))
            script = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/PartnerNativeUiActor.cs'
            pointer = dict(fileID=11500000, guid=guid(Path(str(script) + '.meta')), type=3)
            self.script_guids.add(pointer['guid'])
            self.inputs[script.relative_to(ROOT).as_posix()] = sha(script.read_bytes())
            panel['documents'][added] = dict(MonoBehaviour=dict(m_ObjectHideFlags=0,
                m_CorrespondingSourceObject=dict(fileID=0), m_PrefabInstance=dict(fileID=0), m_PrefabAsset=dict(fileID=0),
                m_GameObject=dict(fileID=go), m_Enabled=1, m_EditorHideFlags=0, m_Script=pointer,
                m_Name='', m_EditorClassIdentifier='', graphic=dict(fileID=row['nativeFileID']),
                renderRoot=dict(fileID=render_root)))
            adapters.append(dict(graphic=row['nativeFileID'], adapter=added, render_root=render_root))
        self.dynamic_proof['native_actor_adapters'] = adapters

    def sound(self, payload, owner):
        if payload.get('idInternal') or payload.get('valueGuidInternal'):
            raise ValueError('Inspect the inline arena Wwise event')
        if not payload['WwiseObjectReference']['m_PathID']: return dict(fileID=0)
        obj = source_pointer(payload['WwiseObjectReference'], owner)
        key, event = identity(obj), self.recovery.fields(obj)
        if key in self.audio_proof: return self.audio_proof[key]['pointer']
        media = json.loads((self.cache / 'raw-resources-source-manifest.json').read_bytes())
        if media['manifest'] != self.recovery.apk.version or not media['completed']:
            raise ValueError('Arena sound source version differs')
        package, = [p for p in media['packages'] if str(event['id']) in p.get('control_objects', {})]
        control = package['control_objects'][str(event['id'])]
        bank, = [b for b in package['banks'] if b['id'] == control['bank_id'] and b['language'] == 0]
        raw = self.cache / 'raw-resources' / bank['path']
        if sha(raw.read_bytes()) != bank['sha256']: raise ValueError('Changed arena Wwise bank')
        wwiser = ROOT / '.tools/wwiser/wwiser.py'
        self.inputs[raw.relative_to(ROOT).as_posix()] = bank['sha256']
        self.inputs[wwiser.relative_to(ROOT).as_posix()] = sha(wwiser.read_bytes())
        if not hasattr(self, 'bank_xml'): self.bank_xml = {}
        if bank['id'] not in self.bank_xml:
            stage = self.work / ('arena-audio-bank-' + str(bank['id'])); stage.mkdir(exist_ok=True)
            subprocess.run([sys.executable, str(wwiser), str(raw), '-d', 'xml', '-dn', 'bank'],
                cwd=stage, check=True, capture_output=True)
            tree = ET.parse(stage / 'bank.xml')
            self.bank_xml[bank['id']] = {o.find('./field[@name="ulID"]').get('value'): o
                for o in tree.iter('object') if o.find('./field[@name="ulID"]') is not None}
        objects = self.bank_xml[bank['id']]
        source = objects[str(event['id'])]
        def field(node, name, default=None):
            values = [f.get('value') for f in node.iter('field') if f.get('name') == name]
            if not values: return default
            value, = values
            return int(value)
        actions, targets = [], []
        for ref in source.iter('field'):
            if ref.get('name') != 'ulActionID': continue
            action = objects[ref.get('value')]
            kind = field(action, 'ulActionType')
            if kind not in (258, 1027) or field(action, 'bIsBus', 0) != 0:
                raise ValueError('Inspect original arena action scope/type: ' + ET.tostring(action).decode())
            if field(action, 'ulExceptionListSize', 0):
                raise ValueError('Inspect original arena action exceptions/properties')
            seconds = 0
            properties = [p for p in action.iter('object') if p.get('name') == 'AkPropBundle']
            if len(properties) > 1: raise ValueError('Inspect original arena action property combination')
            for prop in properties:
                if field(prop, 'pID') != 16: raise ValueError('Inspect original arena action property')
                seconds = field(prop, 'pValue') / 1000
            target = field(action, 'idExt')
            actions.append(dict(kind=kind, target=target, seconds=seconds, curve=field(action, 'eFadeCurve', 1)))
            if kind == 1027:
                while target:
                    if target in targets: raise ValueError('Cyclic or layered arena audio graph')
                    targets.append(target); target = field(objects[str(target)], 'DirectParentID', 0)
        plays = [a for a in actions if a['kind'] == 1027]
        if len(plays) > 1 or not actions: raise ValueError('Inspect the original arena layered/empty event')
        if plays:
            result = super().sound(payload, owner)
        else:
            result = dict(fileID=0)
            self.audio_proof[key] = dict(event=event, pointer=result, original_sha256=sha(obj.get_raw_data()), bank=bank)
        self.audio_proof[key].update(source_actions=actions, target_ids=targets)
        return result

    def register_scripts(self):
        super().register_scripts()
        for name in ('GameEndPanelArena', 'ArenaShowFightResult', 'ArenaOpponentIcon', 'CombatRatingPromoteWayItem'):
            path = ROOT / 'unity/Assets/Game/Runtime/Official/BattleResultUI' / (name + '.cs')
            pointer = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
            self.scripts[name] = self.class_ids[script_id('', name)] = pointer
            self.script_guids.add(pointer['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + '.meta').read_bytes()))
        for name in ('AkTriggerDisable', 'UIBloomPlayerElement'):
            path = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI' / (name + '.cs')
            pointer = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
            self.scripts[name] = self.class_ids[script_id('', name)] = pointer
            self.script_guids.add(pointer['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + '.meta').read_bytes()))
        for full, relative in (
                ('AppearanceSystem.NpcAppearance', 'ActivityRankUI/NpcAppearance.cs'),
                ('AppearanceSystem.SkeletonGraphicProxy', 'PartnersUI/Detail/SkeletonGraphicProxy.cs'),
                ('AppearanceCombineSkin', 'PartnersUI/Detail/AppearanceCombineSkin.cs'),
                ('SpineLazyAnimLoad', 'PartnersUI/Detail/SpineLazyAnimLoad.cs'),
                ('UIBloomLiteSpineElement', 'PartnersUI/Detail/UIBloomLiteSpineElement.cs'),
                ('AppearanceSystem.AppearanceUILite', 'ProfessionUI/AppearanceUILite.cs')):
            path = ROOT / 'unity/Assets/Game/Runtime/Official' / relative
            pointer = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
            namespace, _, leaf = full.rpartition('.')
            self.scripts[full] = self.class_ids[script_id(namespace, leaf)] = pointer
            self.script_guids.add(pointer['guid'])
            self.script_sources[full] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + '.meta').read_bytes()))

    def prepare(self):
        self.selections, self.roots, self.dynamic_proof = {}, {}, {}
        aot = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code/aot-metadata'
        image = aot / 'analysis-elf.so'
        with image.open('rb') as stream:
            stream.seek(0x79ef480); method = stream.read(0x20)
        if method.hex() != '081040f9c80000b4030d40f9002140f9021540f9e1031faa60001fd6c0035fd6':
            raise ValueError('Original AkTriggerDisable.OnDisable differs')
        for path in (image, aot / 'dump.cs', aot / 'source-manifest.json'):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        self.dynamic_proof['disable_trigger'] = dict(rva='0x79ef480', bytes=method.hex(),
            behavior='Load triggerDelegate at +0x20; if non-null invoke with null; return.')
        with image.open('rb') as stream:
            stream.seek(0x79e0598); register = stream.read(0x438)
        self.dynamic_proof['trigger_registration'] = dict(rva='0x79e0598', length=len(register),
            sha256=sha(register), behavior='Resolve configured trigger type; GetComponent; '
            'when absent get gameObject and AddComponent(type); combine triggerDelegate at +0x20.')
        code = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code/client-source'
        for name in ('GameEndPanelArena', 'ArenaShowFightResult', 'ArenaOpponentIcon',
                     'ArenaGuardIconInfoProvider', 'PlayerInfoData', 'GameEndUIHandler',
                     'CombatRatingPromoteWayItem', 'CombatRatingPromoteWayType',
                     'CombatRatingPromoteWaySkill', 'CombatRatingPromoteWayEquip'):
            path = code / (name + '.cs')
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        for script in (Path(__file__), ROOT / 'scripts/prepare_demon_result_native.py'):
            self.inputs[script.relative_to(ROOT).as_posix()] = sha(script.read_bytes())
        records = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        selected, = [row for row in records if row['path'].endswith('/GameEndPanelArena.prefab')]
        self.prefab('GameEndPanelArena', selected['bundle'], selected['path'].removeprefix('Assets/'))
        tables_path = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-data/tables.json'
        tables = json.loads(tables_path.read_bytes())
        self.inputs[tables_path.relative_to(ROOT).as_posix()] = sha(tables_path.read_bytes())
        self.dynamic_proof['result_timing'] = dict(normal=tables['game_settings']['GameEndDelayTime'],
            round_limit=tables['game_settings']['GameEndOverRoundDelayTime'])
        self.bind_language_font(records)

    def catalog(self):
        keys = {'ui_arena_win_text', 'ui_arena_lose_text', 'ui_game_end_promote_tip_Skill',
                'ui_game_end_promote_tip_Equip'}
        arena_path = ROOT / 'unity/Assets/Game/Resources/Dungeons/arena-rules.json'
        arena = json.loads(arena_path.read_bytes())
        self.inputs[arena_path.relative_to(ROOT).as_posix()] = sha(arena_path.read_bytes())
        keys.update(guard['nameKey'] for guard in arena['runtime']['guards'])
        def collect(value):
            if isinstance(value, dict):
                if value.get('myKey') and not value['myKey'].startswith('{'): keys.add(value['myKey'])
                for child in value.values(): collect(child)
            elif isinstance(value, list):
                for child in value: collect(child)
        for _, _, data in self.queue: collect(data)
        texts, inputs = language(keys)
        if set(texts) != keys: raise ValueError('Original arena language is absent: ' + repr(keys - texts.keys()))
        self.inputs.update(inputs)
        payload = dict(schema=1, manifest=self.recovery.apk.version,
            texts=[dict(key=k, text=v) for k,v in sorted(texts.items())],
            end_delays=self.dynamic_proof['result_timing'])
        bindings_guid = hashlib.md5(b'zjcs:arena-result:bindings').hexdigest()
        self.queue.append((Path('bindings.json'), bindings_guid,
            (json.dumps(payload, ensure_ascii=False, indent=2) + '\n').encode('utf8')))
        script = ROOT / 'unity/Assets/Game/Runtime/Presentation/Home/OfficialArenaResultAssets.cs'
        audio_script = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionAudioCatalog.cs'
        for path in (script, Path(str(script) + '.meta'), audio_script, Path(str(audio_script) + '.meta')):
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        audio_guid = hashlib.md5(b'zjcs:arena-result:audio').hexdigest()
        def fields(path, name):
            return dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0),
                m_PrefabInstance=dict(fileID=0), m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0),
                m_Enabled=1, m_EditorHideFlags=0,
                m_Script=dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3),
                m_Name=name, m_EditorClassIdentifier='')
        audio = fields(audio_script, 'Original arena result audio')
        audio['Events'] = []
        for row in sorted(self.audio_proof.values(), key=lambda row: row['event']['id']):
            stops = [a for a in row['source_actions'] if a['kind'] == 258]
            play = next((a for a in row['source_actions'] if a['kind'] == 1027), None)
            audio['Events'].append(dict(EventId=row['event']['id'], Clip=row['pointer'],
                TargetIds=row['target_ids'], StopTargetIds=[a['target'] for a in stops],
                StopSeconds=[a['seconds'] for a in stops], StopCurves=[a['curve'] for a in stops],
                FadeInSeconds=play['seconds'] if play else 0, FadeInCurve=play['curve'] if play else 1))
        self.queue.append((Path('Resources/Combat/ArenaResult/audio.asset'), audio_guid, {11400000: dict(MonoBehaviour=audio)}))
        asset = fields(script, 'Official arena result assets')
        asset.update(Panel=self.roots['GameEndPanelArena'], Font=self.dynamic_proof['font']['font_asset'],
            Audio=dict(fileID=11400000, guid=audio_guid, type=2),
            Bindings=dict(fileID=4900000, guid=bindings_guid, type=3))
        self.queue.append((Path('Resources/Combat/ArenaResult/catalog.asset'),
            hashlib.md5(b'zjcs:arena-result:catalog').hexdigest(), {11400000: dict(MonoBehaviour=asset)}))


def install(work):
    receipt = json.loads((work / NAME).read_bytes())
    if receipt['generator_sha256'] != sha(Path(__file__).read_bytes()):
        raise ValueError('Rebuild arena result after generator changes')
    for relative, expected in receipt['inputs'].items():
        if sha((ROOT / relative).read_bytes()) != expected: raise ValueError('Changed arena input: ' + relative)
    staged = work / 'arena-result-assets'
    prior_path = ROOT / 'assets/reference' / NAME
    prior = json.loads(prior_path.read_bytes()) if prior_path.exists() else {}
    plan = []
    for relative, expected in receipt['outputs'].items():
        source, target = staged / relative, DESTINATION / relative
        if sha(source.read_bytes()) != expected: raise ValueError('Changed staged arena output: ' + relative)
        if target.exists() and target.read_bytes() != source.read_bytes():
            if sha(target.read_bytes()) != prior.get('installed_output_sha256', {}).get(target.relative_to(ROOT).as_posix()):
                raise ValueError('Refusing to overwrite changed arena asset: ' + str(target))
        plan.append((source, target))
    installed, folders = {}, {}
    for source, target in plan:
        if not target.exists() or target.read_bytes() != source.read_bytes():
            target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(source.read_bytes())
        installed[target.relative_to(ROOT).as_posix()] = sha(target.read_bytes())
        parent = target.parent
        while parent.is_relative_to(DESTINATION):
            meta = Path(str(parent) + '.meta')
            if not meta.exists():
                meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs:arena-result-folder:' +
                    parent.relative_to(ROOT).as_posix()).encode()).hexdigest() +
                    '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
            folders[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes()); parent = parent.parent
    receipt.update(installed_output_sha256=installed, folder_meta_sha256=folders)
    prior_path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed original Arena result:', len(installed), 'files', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    if parser.parse_args().install: install(validation_directory())
    else: ArenaResultBinding(validation_directory()).build()
