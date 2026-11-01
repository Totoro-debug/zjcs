"""Bind the current official partner card and its native resource closure.

All staging is confined to the caller's validation workspace. Source object
identities are learned from original PPtrs paired with native exported PPtrs;
names never establish resource identity.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import struct
import subprocess
import sys
import wave

from Crypto.Hash import MD4
from UnityPy.classes.PPtr import PPtr
from UnityPy.enums import ClassIDType
import yaml

from official_apk import ROOT
from official_manifest import read_asset_manifest
from npc_travel_language import language
from prepare_current_partner_native import PartnerNativeRecovery, documents, identity
from validation_workspace import validation_directory
from prepare_unity_map13_native_sprite_yaml import Dumper as NativeDumper


class Dumper(NativeDumper):
    def ignore_aliases(self, data):
        # Unity's object references must be written at each field. YAML aliases
        # deserialize as unbound pointers in native node-port records.
        return True


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def guid(path):
    return re.search(rb'(?m)^guid: ([0-9a-f]{32})', path.read_bytes())[1].decode()


def script_id(namespace, name):
    return struct.unpack('<i', MD4.new(('s\0\0\0' + namespace + name).encode()).digest()[:4])[0]


class PartnerCardBinding:
    def __init__(self, work):
        if work.parent != ROOT / 'output/validation' or not (work / '.zjcs-validation.json').is_file():
            raise ValueError('Create an owned validation workspace before binding partner assets')
        self.work = work
        self.recovery = PartnerNativeRecovery()
        self.cache = self.recovery.cache
        self.native = self.recovery.native
        self.proof, self.redirects, self.queue = {}, {}, []
        self.objects, self.scripts, self.class_ids = {}, {}, {}
        self.audio_outputs = {}
        self.audio_proof = {}
        self.inputs = {}
        for name in ('prepare_current_partner_native.py', 'official_apk.py', 'official_manifest.py'):
            path = ROOT / 'scripts' / name
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        self.assets = {}
        self.script_sources = {}
        self.by_name = {}
        receipt_path = self.cache / 'native-export-all-source-manifest.json'
        receipt = json.loads(receipt_path.read_bytes())
        if receipt['manifest'] != self.recovery.apk.version or not receipt['completed']:
            raise ValueError('Native conversion receipt differs')
        self.inputs[receipt_path.relative_to(ROOT).as_posix()] = sha(receipt_path.read_bytes())
        self.checked = {r['path']: r['sha256'] for r in receipt['outputs']}
        prefix = 'ExportedProject/Assets/'
        for row in receipt['outputs']:
            if row['path'].startswith(prefix) and row['path'].endswith('.meta'):
                path = self.cache / 'native-export-all' / row['path'][:-5]
                self.by_name.setdefault(self.name_key(path.stem), []).append(path)
        print('Indexed native asset paths:', len(self.by_name), flush=True)
        for kind in ('textures', 'shaders'):
            folder = self.cache / 'all-assets' / ('windows-' + kind)
            manifest_path = folder / 'source-manifest.json'
            data = json.loads(manifest_path.read_bytes())
            if data['manifest'] != self.recovery.apk.version or not data['completed']:
                raise ValueError('Windows resource receipt differs: ' + kind)
            self.inputs[manifest_path.relative_to(ROOT).as_posix()] = sha(manifest_path.read_bytes())
            setattr(self, kind, {r['source']['cab'].lower() + ':' + r['source']['path_id']: r for r in data[kind]})
            if kind == 'shaders':
                self.engine = {r['source']['cab'].lower() + ':' + r['source']['path_id']: r for r in data['native_engine_bindings']}
        self.register_scripts()

    @staticmethod
    def name_key(name):
        return re.sub(r'[\W_]', '', name.casefold())

    def native_path(self, source_guid, obj):
        if source_guid in self.assets:
            return self.assets[source_guid]
        value = obj.read(check_read=False)
        name = getattr(value, 'm_Name', '')
        if not name and obj.type.name == 'Shader':
            name = value.m_ParsedForm.m_Name
        key = self.name_key(name.rsplit('/', 1)[-1])
        candidates = list(self.by_name.get(key, []))
        for suffix in range(100):
            candidates.extend(self.by_name.get(key + str(suffix), []))
        matches = [path for path in candidates if guid(Path(str(path) + '.meta')) == source_guid]
        if len(matches) != 1:
            raise ValueError('Locate exact native GUID (candidate names are not identity): ' + source_guid + ' / ' + obj.type.name + ' / ' + name)
        self.assets[source_guid] = matches[0]
        return matches[0]

    def register_scripts(self):
        runtime = ROOT / 'unity/Assets/Game/Runtime'
        paths = {
            'MultiLanguageText': runtime / 'Official/WorldUI/WorldMultiLanguageText.cs',
            'MaintainGlobalScaleUI': runtime / 'Official/WorldUI/WorldMaintainGlobalScaleUI.cs',
            'NoDrawingRayCast': runtime / 'Official/WorldUI/NoDrawingRayCast.cs',
            'ButtonSound': runtime / 'Official/WorldUI/WorldButtonSound.cs',
            'NpcPanelNpcItem': runtime / 'Official/PartnersUI/Roster/NpcPanelNpcItem.cs',
            'NpcFriendshipLevelShow': runtime / 'Official/PartnersUI/Roster/NpcFriendshipLevelShow.cs',
            'NpcMeetProgressItem': runtime / 'Official/PartnersUI/NpcMeetProgressItem.cs',
            'TipDotNodeLinker': runtime / 'Official/ProfessionUI/TipDotNodeLinker.cs',
            'TipDotShowImageController': runtime / 'Official/ProfessionUI/TipDotShowImageController.cs',
            'TMPro.TMPEffect': runtime / 'Official/World/NativeText/TMPEffect.cs',
            'Coffee.UIExtensions.UIParticle': runtime / 'Presentation/Opening/OfficialCoffeeUIParticle/UIParticle.cs',
        }
        for path in (runtime / 'Official/ProfessionUI').glob('TipDot*.cs'):
            paths.setdefault(path.stem, path)
        for path in (runtime / 'Official/PartnersUI/Roster/TipDots').glob('Tip*.cs'):
            paths.setdefault(path.stem, path)
        packages = ROOT / 'unity/Library/PackageCache'
        for package in ('com.unity.ugui@1.0.0', 'com.unity.textmeshpro@3.0.6'):
            for path in (packages / package / 'Scripts/Runtime').rglob('*.cs'):
                prefix = 'UnityEngine.UI.' if package.startswith('com.unity.ugui') else 'TMPro.'
                paths[prefix + path.stem] = path
        # uGUI places runtime sources under Runtime rather than Scripts/Runtime.
        for path in (packages / 'com.unity.ugui@1.0.0/Runtime').rglob('*.cs'):
            paths['UnityEngine.UI.' + path.stem] = path
        for full_name, path in paths.items():
            namespace, _, name = full_name.rpartition('.')
            if not path.is_file() or not Path(str(path) + '.meta').is_file():
                raise ValueError('Migrated native component is unavailable: ' + str(path))
            pointer = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
            self.scripts[full_name] = pointer
            self.script_sources[full_name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + '.meta').read_bytes()))
            self.class_ids[script_id(namespace, name)] = pointer
        self.script_guids = {pointer['guid'] for pointer in self.scripts.values()}
        self.original_refs = {}

    def original_asset(self, obj):
        key = identity(obj)
        if key in self.original_refs:
            return self.original_refs[key]
        if obj.type.name != 'MonoBehaviour':
            raise ValueError('Bind original non-native resource: ' + key)
        script = obj.read(check_read=False).m_Script.read()
        full_name = '.'.join(filter(None, [script.m_Namespace, script.m_ClassName]))
        script_pointer = self.scripts[full_name]
        target_guid = hashlib.md5(('zjcs:partner-original:' + key).encode()).hexdigest()
        result = dict(fileID=11400000, guid=target_guid, type=2)
        self.original_refs[key] = result
        self.script_guids.add(target_guid)
        fields = self.recovery.fields(obj)

        def convert(value):
            if isinstance(value, dict):
                if 'm_FileID' in value and 'm_PathID' in value:
                    if not value['m_PathID']:
                        return dict(fileID=0)
                    linked = PPtr(**value, assetsfile=obj.assets_file).deref()
                    return self.original_asset(linked)
                return {k: script_pointer if k == 'm_Script' else convert(v) for k, v in value.items()}
            if isinstance(value, list):
                return [convert(v) for v in value]
            return value
        target = Path('Graph') / (sha(key.encode())[:16] + '.asset')
        data = {11400000: dict(MonoBehaviour=convert(fields))}
        self.queue.append((target, target_guid, data))
        self.proof[key] = dict(source=self.recovery.apk.provenance(obj), type=full_name, name=fields.get('m_Name', ''),
            original_sha256=sha(obj.get_raw_data()), output=target.as_posix(), guid=target_guid)
        return result

    def original(self, key):
        cab, path_id = key.rsplit(':', 1)
        file = self.recovery.apk.env.get_cab(cab)
        if file is None:
            file = self.recovery.apk.env.find_file(cab)
        return file.objects[int(path_id)]

    def sound(self, payload, owner):
        if payload.get('idInternal') or payload.get('valueGuidInternal'):
            raise ValueError('Resolve the original inline Wwise event')
        pointer = payload['WwiseObjectReference']
        if not pointer['m_PathID']:
            return dict(fileID=0)
        obj = PPtr(**pointer, assetsfile=owner.assets_file).deref()
        event = self.recovery.fields(obj)
        key = identity(obj)
        if key in self.audio_proof:
            return self.audio_proof[key]['pointer']
        wwiser = ROOT / '.tools/wwiser/wwiser.py'
        decoder = ROOT / '.tools/vgmstream/vgmstream-cli.exe'
        cached_path = self.work / 'partner-card-audio.json'
        cached = json.loads(cached_path.read_bytes()).get(key) if cached_path.is_file() else None
        if cached and cached['original_sha256'] == sha(obj.get_raw_data()) and cached['event'] == event:
            wav = self.work / ('sound-' + str(event['id'])) / (event['objectName'] + '.wav')
            original_bank = self.cache / 'raw-resources' / cached['bank']['path']
            if (wav.is_file() and sha(wav.read_bytes()) == cached['output_sha256']
                and sha(original_bank.read_bytes()) == cached['bank']['sha256']
                and cached['tools'] == {str(p.relative_to(ROOT)): sha(p.read_bytes()) for p in (wwiser, decoder)}):
                self.audio_proof[key] = cached
                self.audio_outputs[Path(cached['output'])] = (cached['pointer']['guid'], wav.read_bytes())
                self.script_guids.add(cached['pointer']['guid'])
                return cached['pointer']
        receipt_path = self.cache / 'raw-resources-source-manifest.json'
        media = json.loads(receipt_path.read_bytes())
        if media['manifest'] != self.recovery.apk.version or not media['completed']:
            raise ValueError('Original audio cache version differs')
        packages = [p for p in media['packages'] if str(event['id']) in p.get('control_objects', {})]
        package, = packages
        event_record = package['control_objects'][str(event['id'])]
        if event_record['type'] != 4:
            raise ValueError('Original Wwise reference does not point to an event')
        bank, = [b for b in package['banks'] if b['id'] == event_record['bank_id'] and b['language'] == 0]
        raw = (self.cache / 'raw-resources' / bank['path']).read_bytes()
        if sha(raw) != bank['sha256']:
            raise ValueError('Original Wwise bank changed')
        stage = self.work / ('sound-' + str(event['id']))
        banks, txtps = stage / 'banks', stage / 'txtp'
        banks.mkdir(parents=True, exist_ok=True)
        txtps.mkdir(exist_ok=True)
        (banks / (str(bank['id']) + '.bnk')).write_bytes(raw)
        subprocess.run([sys.executable, str(wwiser), str(banks / (str(bank['id']) + '.bnk')),
            '-g', '-d', 'none', '-go', str(txtps), '-gw', '../banks/', '-gv', '0dB', '-gf', str(event['id'])],
            cwd=stage, check=True, capture_output=True, creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
        files = [p for p in txtps.glob('*.txtp') if re.search(r'CAkEvent\[\d+\] ' + str(event['id']) + r'\b', p.read_text('utf8'))]
        txtp, = files
        graph = txtp.read_text('utf8')
        leaves = [line.strip() for line in graph.splitlines() if line.strip() and not line.lstrip().startswith('#')]
        if len(leaves) != 1 or 'master volume: auto' in graph:
            raise ValueError('Preserve the original Wwise layered/random/gain graph: ' + graph)
        leaf = re.fullmatch(r'\.\./banks/(\d+)\.bnk #s(\d+) #i(?:  #v (-?[\d.]+)dB)?  ##(\d+)\.wem', leaves[0])
        if leaf is None or int(leaf[1]) != bank['id']:
            raise ValueError('Resolve the original Wwise playback leaf: ' + graph)
        wav = stage / (event['objectName'] + '.wav')
        subprocess.run([str(decoder), '-i', '-o', str(wav), str(txtp)], check=True,
            capture_output=True, creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0))
        data = wav.read_bytes()
        with wave.open(str(wav), 'rb') as sound:
            pcm = dict(channels=sound.getnchannels(), rate=sound.getframerate(), frames=sound.getnframes(), sample_width=sound.getsampwidth())
        target = Path('Audio') / (event['objectName'] + '.wav')
        target_guid = hashlib.md5(('zjcs:partner-current-sound:' + key).encode()).hexdigest()
        result = dict(fileID=8300000, guid=target_guid, type=3)
        self.audio_outputs[target] = (target_guid, data)
        self.script_guids.add(target_guid)  # Already bound local payload.
        self.audio_proof[key] = dict(event=event, original_sha256=sha(obj.get_raw_data()),
            source=self.recovery.apk.provenance(obj), bank=bank, package=package['name'],
            txtp=graph, gain_db=float(leaf[3] or 0), media_id=int(leaf[4]),
            pcm=pcm, output=target.as_posix(), output_sha256=sha(data), pointer=result,
            tools={str(p.relative_to(ROOT)): sha(p.read_bytes()) for p in (wwiser, decoder)})
        print('Resolved original partner sound:', event['objectName'], flush=True)
        return result

    def learn(self, source, native, owner):
        if isinstance(source, dict) and set(source) == {'first', 'second'} and isinstance(source['first'], str) and isinstance(native, dict) and source['first'] in native:
            self.learn(source['second'], native[source['first']], owner)
        elif isinstance(source, (list, tuple)) and len(source) == 2 and isinstance(source[0], str) and isinstance(native, dict) and source[0] in native:
            # Unity typetree maps are pairs; exported YAML maps use the key as
            # a property name (notably Material.m_SavedProperties.m_TexEnvs).
            self.learn(source[1], native[source[0]], owner)
        elif isinstance(source, dict) and 'm_FileID' in source and 'm_PathID' in source:
            if source['m_PathID'] and isinstance(native, dict) and native.get('guid') and native['guid'] != '0000000000000000e000000000000000':
                obj = PPtr(**source, assetsfile=owner.assets_file).deref()
                old = self.objects.get(native['guid'])
                if old is not None and identity(old) != identity(obj):
                    raise ValueError('One native GUID maps to two original objects')
                self.objects[native['guid']] = obj
        elif isinstance(source, dict) and isinstance(native, dict):
            for key in source.keys() & native.keys():
                if key != 'm_Script':
                    self.learn(source[key], native[key], owner)
        elif isinstance(source, list) and isinstance(native, dict):
            for pair in source:
                if isinstance(pair, (list, tuple)) and len(pair) == 2 and isinstance(pair[0], str) and pair[0] in native:
                    self.learn(pair[1], native[pair[0]], owner)
        elif isinstance(source, list) and isinstance(native, list) and len(source) == len(native):
            for left, right in zip(source, native):
                self.learn(left, right, owner)

    def load(self, source_guid):
        if source_guid in self.redirects:
            return self.redirects[source_guid]
        obj = self.objects.get(source_guid)
        if obj is None:
            raise ValueError('Original CAB/PathID not resolved for native GUID: ' + source_guid)
        if obj.type.name == 'MonoScript':
            script = obj.read(check_read=False)
            full_name = '.'.join(filter(None, [script.m_Namespace, script.m_ClassName]))
            self.redirects[source_guid] = self.scripts[full_name]
            return self.redirects[source_guid]
        path = None
        geometry = None
        key = identity(obj)
        if obj.type.name in ('Texture2D', 'Cubemap', 'Shader'):
            rows = self.textures if obj.type.name != 'Shader' else self.shaders
            if key not in rows:
                if obj.type.name == 'Shader' and key in self.engine:
                    self.redirects[source_guid] = self.engine[key]['unity_reference']
                    return self.redirects[source_guid]
                raise ValueError('Windows asset conversion missing: ' + key)
            row = rows[key]
            if sha(obj.get_raw_data()) != row['source']['object_sha256']:
                raise ValueError('Original Windows resource identity changed')
            folder = self.cache / 'all-assets' / ('windows-shaders' if obj.type.name == 'Shader' else 'windows-textures')
            record = row if obj.type.name == 'Shader' else row['outputs'][0]
            data = (folder / record['path']).read_bytes()
            if sha(data) != record['sha256']:
                raise ValueError('Windows resource payload changed')
            file_id = 4800000 if obj.type.name == 'Shader' else 8900000 if obj.type.name == 'Cubemap' else 2800000
            target = Path('Converted') / (sha(key.encode())[:16] + Path(record['path']).suffix)
        else:
            path = self.native_path(source_guid, obj)
            raw = path.read_bytes()
            receipt_key = path.relative_to(self.cache / 'native-export-all').as_posix()
            meta_raw = Path(str(path) + '.meta').read_bytes()
            if sha(meta_raw) != self.checked[receipt_key + '.meta'] or guid(Path(str(path) + '.meta')) != source_guid:
                raise ValueError('Native resource metadata changed')
            if sha(raw) != self.checked[receipt_key]:
                raise ValueError('Native payload changed: ' + str(path))
            self.inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
            if obj.type.name == 'Font':
                if raw != bytes(obj.read(check_read=False).m_FontData):
                    raise ValueError('Native font bytes differ from original Font data')
                file_id, data = 12800000, raw
            elif not raw.startswith(b'%YAML'):
                raise ValueError('Resolve original native binary resource: ' + str(path))
            else:
                docs = documents(raw.decode('utf8'))
                file_id, = [file_id for file_id in docs if file_id != 100100000]
                self.learn(self.recovery.fields(obj), next(iter(docs[file_id].values())), obj)
                data = docs
            if obj.type.name == 'Sprite':
                sprite = obj.read(check_read=False)
                source = self.recovery.fields(obj)
                original_indices = bytes(source['m_RD']['m_IndexBuffer'])
                native_sprite = docs[file_id]['Sprite']
                if bytes.fromhex(native_sprite['m_RD']['m_IndexBuffer']) != original_indices:
                    raise ValueError('Native sprite index bytes differ from original: ' + key)
                vertices = source['m_RD']['m_VertexData']['m_VertexCount']
                if native_sprite['m_RD']['m_VertexData']['m_VertexCount'] != vertices:
                    raise ValueError('Native sprite vertex count differs from original: ' + key)
                geometry = dict(vertices=vertices, index_bytes=len(original_indices), index_sha256=sha(original_indices))
                if sprite.m_SpriteAtlas.path_id:
                    atlas = sprite.m_SpriteAtlas.deref()
                    packed, = [value for atlas_key, value in self.recovery.fields(atlas)['m_RenderDataMap']
                               if atlas_key == source['m_RenderDataKey']]
                    self.learn(packed, docs[file_id]['Sprite']['m_RD'], atlas)
                    self.proof[identity(atlas)] = dict(source=self.recovery.apk.provenance(atlas),
                        type='SpriteAtlas', original_sha256=sha(atlas.get_raw_data()), usage='Packed sprite render data')
            target = path.relative_to(self.native)
        target_guid = hashlib.md5(('zjcs:partner-current:' + source_guid).encode()).hexdigest()
        pointer = dict(fileID=file_id, guid=target_guid, type=2 if target.suffix in ('.asset', '.mat', '.anim', '.controller') else 3)
        self.redirects[source_guid] = pointer
        self.proof[key] = dict(source=self.recovery.apk.provenance(obj), type=obj.type.name,
            original_sha256=sha(obj.get_raw_data()), native_path=path.relative_to(ROOT).as_posix() if path else None,
            output=target.as_posix(), guid=target_guid, geometry=geometry)
        self.queue.append((target, target_guid, data))
        return pointer

    def replace(self, value):
        if isinstance(value, dict):
            if set(value) == {'class', 'ns', 'asm'}:
                if value['class'] == 'StateGameObjectConfig' and value['asm'] == 'Framework':
                    return dict(value, **{'class': 'WorldStateGameObjectConfig', 'ns': 'Zjcs.Official.WorldUI', 'asm': 'Zjcs.Official.WorldUI'})
                if value['class'] == 'TipDotCommonNode':
                    return dict(value, ns='Zjcs.Official.ProfessionUI', asm='Zjcs.Official.ProfessionUI')
                if value['class'] in {path.stem for path in (ROOT / 'unity/Assets/Game/Runtime/Official/PartnersUI/Roster/TipDots').glob('Tip*.cs')}:
                    return dict(value, ns='Zjcs.Official.PartnersUI', asm='Zjcs.Official.PartnerRosterUI')
                if value['class']:
                    raise ValueError('Map original managed reference type explicitly: ' + repr(value))
                return value
            if 'guid' in value and value.get('fileID'):
                if value['guid'] in self.script_guids:
                    return value
                if value['guid'].startswith('0000000000000000'):
                    return value
                return self.load(value['guid'])
            if set(value) == {'originalCAB', 'originalPathID'}:
                return self.original_asset(self.original(value['originalCAB'].lower() + ':' + str(value['originalPathID'])))
            return {key: self.class_ids[item['fileID']] if key == 'm_Script' and item['guid'] not in self.script_guids
                else self.replace(item) for key, item in value.items()}
        if isinstance(value, list):
            return [self.replace(item) for item in value]
        return value

    def dynamic_sprites(self, npc_ids):
        rules = ROOT / 'unity/Assets/Official/Partners/partner-rules.json'
        data = json.loads(rules.read_bytes())
        if data['manifest'] != self.recovery.apk.version:
            raise ValueError('Dynamic partner image configuration differs from selected source')
        self.inputs[rules.relative_to(ROOT).as_posix()] = sha(rules.read_bytes())
        original = ROOT / '.tools/source-packages/official-code/framework-firstpass-source/ResourceLoaderExtensions.cs'
        selected = ROOT / '.tools/source-packages/current' / data['manifest'] / 'official-code/framework-firstpass-source/ResourceLoaderExtensions.cs'
        if original.read_bytes() != selected.read_bytes() or '"Assets/Image/" + spriteName' not in original.read_text('utf-8-sig'):
            raise ValueError('Original sprite address prefix changed')
        self.inputs[original.relative_to(ROOT).as_posix()] = sha(original.read_bytes())
        manifest = self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive)
        records = read_asset_manifest(manifest)
        requested = {}
        text_keys = {'ui_new_mark_text'}
        for npc_id in npc_ids:
            row, = [row for row in data['tables']['npc'] if row['Id'] == npc_id]
            text_keys.add(row['NameKey'])
            for field in ('FigureImage', 'SmallBgImage'):
                requested.setdefault(row[field], []).append(dict(npc_id=npc_id, field=field))
        bindings = []
        for address, consumers in sorted(requested.items()):
            full_address = 'Assets/Image/' + address
            record, = [row for row in records if row['address'] == full_address or row['path'] == full_address + '.png']
            sprites = [pointer.deref() for path, pointer in self.recovery.apk.container(record['bundle'])
                       if path.casefold() == record['path'].casefold() and pointer.deref().type.name == 'Sprite']
            obj, = sprites
            source_key, source_id = self.recovery.fields(obj)['m_RenderDataKey']
            render_guid = ''.join(format(source_key['data[' + str(i) + ']'], '08x')[::-1] for i in range(4))
            if render_guid != record['guid']:
                raise ValueError('Original sprite render key differs from asset manifest: ' + address)
            name = self.name_key(obj.read(check_read=False).m_Name)
            matches = []
            for key in [name] + [name + str(i) for i in range(100)]:
                for candidate in self.by_name.get(key, []):
                    if candidate.suffix != '.asset':
                        continue
                    docs = documents(candidate.read_text('utf8'))
                    if len(docs) == 1 and next(iter(docs.values())).get('Sprite', {}).get('m_RenderDataKey') == {render_guid: source_id}:
                        matches.append(candidate)
            candidate, = matches
            source_guid = guid(Path(str(candidate) + '.meta'))
            if source_guid in self.objects and identity(self.objects[source_guid]) != identity(obj):
                raise ValueError('Dynamic sprite identity collides with serialized dependency')
            self.objects[source_guid] = obj
            pointer = self.load(source_guid)
            bindings.append(dict(address=address, pointer=pointer, consumers=consumers, manifest_asset=record,
                                 original=identity(obj), original_sha256=sha(obj.get_raw_data()),
                                 source=self.recovery.apk.provenance(obj)))
            print('Bound original dynamic sprite:', address, flush=True)
        texts, text_sources = self.card_language(text_keys)
        language_manifest = self.recovery.apk.source.manifest('WwiseBnk', self.recovery.apk.archive)
        language_records = {row['name']: row for row in language_manifest['bundles']}
        for path, digest in text_sources.items():
            raw = (ROOT / path).read_bytes()
            record = language_records[Path(path).name]
            if len(raw) != record['size'] or hashlib.md5(raw).hexdigest() != record['hash']:
                raise ValueError('Language data differs from the selected WwiseBnk manifest: ' + path)
            self.inputs[path] = digest
        helper = ROOT / 'scripts/npc_travel_language.py'
        self.inputs[helper.relative_to(ROOT).as_posix()] = sha(helper.read_bytes())
        return dict(manifest=self.recovery.apk.version, asset_manifest_sha256=sha(manifest), sprites=bindings,
                    language_manifest_sha256=sha(self.recovery.apk.source.manifest_bytes('WwiseBnk', self.recovery.apk.archive)),
                    texts=[dict(key=key, text=value) for key, value in sorted(texts.items())],
                    font=self.chinese_font(records))

    def card_language(self, text_keys):
        texts, sources = language(text_keys)
        if set(texts) != text_keys:
            raise ValueError('Original partner card language entries are unavailable: ' + repr(text_keys - texts.keys()))
        return texts, sources

    def chinese_font(self, records):
        def asset(path, kind):
            row, = [row for row in records if row['path'] == path or row['address'] == path]
            obj, = [pointer.deref() for name, pointer in self.recovery.apk.container(row['bundle'])
                    if name.casefold() == row['path'].casefold() and pointer.deref().type.name == kind]
            return obj

        configs = []

        def config(path):
            obj = asset(path, 'TextAsset')
            configs.append(dict(source=self.recovery.apk.provenance(obj), original=identity(obj),
                                original_sha256=sha(obj.get_raw_data()), path=path))
            return json.loads(obj.read().m_Script)

        regional = config('Assets/TextMesh Pro/Assets/Fonts & Materials/MultiLanguageFontConfig.json')
        original_fonts = config('Assets/TextMesh Pro/RawFonts/FontAssetFontConfig.json')
        region = regional['RegionFontAssetDict']['CN']
        name = region['Default']
        if region['Fallbacks'] or regional['FontAssetDict']['zh_CN'] != name:
            raise ValueError('Original CN font selection needs its full fallback chain')
        obj = asset(name, 'MonoBehaviour')
        key = identity(obj)
        if key not in self.proof:
            raise ValueError('Original regional font is outside the card resource closure: ' + key)
        pointer, = [value for value in self.redirects.values() if value['guid'] == self.proof[key]['guid']]
        definition = original_fonts['FontAssetInfoDict'][name]
        font_obj = asset(definition['FontPath'], 'Font')
        raw = bytes(font_obj.read(check_read=False).m_FontData)
        if len(raw) != definition['Length'] or hashlib.md5(raw).hexdigest() != definition['Hash']:
            raise ValueError('Original source font differs from its official font configuration')
        font_guid = hashlib.md5(('zjcs:partner-source-font:' + identity(font_obj)).encode()).hexdigest()
        font_pointer = dict(fileID=12800000, guid=font_guid, type=3)
        self.script_guids.add(font_guid)
        target = Path('Fonts') / Path(definition['FontPath']).name
        self.queue.append((target, font_guid, raw))
        self.proof[identity(font_obj)] = dict(source=self.recovery.apk.provenance(font_obj), type='Font',
            original_sha256=sha(font_obj.get_raw_data()), font_data_sha256=sha(raw), output=target.as_posix(), guid=font_guid)
        for filename in ('MultiLanguageFontService.cs', 'FontAssetFontService.cs', 'MultiLanguageText.cs'):
            relative = Path('framework-firstpass-source') / filename
            original = ROOT / '.tools/source-packages/official-code' / relative
            selected = ROOT / '.tools/source-packages/current' / self.recovery.apk.version / 'official-code' / relative
            if original.read_bytes() != selected.read_bytes():
                raise ValueError('Original font source differs from selected version: ' + filename)
            self.inputs[original.relative_to(ROOT).as_posix()] = sha(original.read_bytes())
        return dict(region='CN', language='zh_CN', name=name, font_asset=pointer,
                    source_font=font_pointer, source_font_definition=definition, configs=configs)

    def build(self, npc_ids=()):
        self.recovery.recover('NpcPanelNpcItem', self.work / 'partner-native')
        self.recovery.finalize()
        _, _, card, _ = self.recovery.outputs[0]
        for key, pointer in card['external'].items():
            self.objects[pointer['guid']] = self.original(key)
        for key, file_id in card['objectMap'].items():
            owner = self.original(key)
            self.learn(self.recovery.fields(owner), next(iter(card['documents'][file_id].values())), owner)
        for script in card['scripts']:
            fields = card['documents'][script['nativeFileID']]['MonoBehaviour']
            full_name = '.'.join(filter(None, [script['namespace'], script['type']]))
            fields['m_Script'] = self.scripts[full_name]
            if script['type'] in ('NpcPanelNpcItem', 'ButtonSound'):
                field = 'MeetSound' if script['type'] == 'NpcPanelNpcItem' else 'ClickEvent'
                owner = self.original(script['original'])
                fields[field] = self.sound(self.recovery.fields(owner)[field], owner)
        (self.work / 'partner-card-audio.json').write_text(json.dumps(self.audio_proof, ensure_ascii=False, indent=2), 'utf8')
        graph_sources = {}

        def inspect_graph(obj):
            key = identity(obj)
            if key in graph_sources or obj.type.name != 'MonoBehaviour':
                return
            script = obj.read(check_read=False).m_Script.read()
            fields = self.recovery.fields(obj)
            graph_sources[key] = dict(type=script.m_ClassName, namespace=script.m_Namespace,
                sha256=sha(obj.get_raw_data()), source=self.recovery.apk.provenance(obj), fields=fields)

            def walk(value):
                if isinstance(value, dict):
                    if value.get('m_PathID') and 'm_FileID' in value:
                        inspect_graph(PPtr(**value, assetsfile=obj.assets_file).deref())
                    else:
                        for name, child in value.items():
                            if name != 'm_Script':
                                walk(child)
                elif isinstance(value, list):
                    for child in value:
                        walk(child)
            walk(fields)

        for key, pending in card['unresolved'].items():
            if pending['type'] == 'MonoBehaviour' and 'tip' in self.original(key).read(check_read=False).m_Script.read().m_ClassName.lower():
                inspect_graph(self.original(key))
        (self.work / 'partner-card-graph-source.json').write_text(json.dumps(graph_sources, ensure_ascii=False, indent=2), 'utf8')
        print('Original graph script types:', sorted({row['type'] for row in graph_sources.values()}), flush=True)
        print('Original graph managed types:', sorted({ref['type']['class'] for row in graph_sources.values()
            for ref in row['fields'].get('references', {}).get('RefIds', []) if ref['type']['class']}), flush=True)
        # Resolve the source-backed closure before any repository installation.
        card['documents'] = self.replace(card['documents'])
        dynamic = self.dynamic_sprites(npc_ids) if npc_ids else None
        for target, target_guid, data in self.queue:
            if isinstance(data, dict):
                print('Binding dependency:', target.as_posix(), flush=True)
                resolved = self.replace(data)
                data.clear()
                data.update(resolved)
        print('Resolved partner card resources:', len(self.queue))
        prefab_guid = hashlib.md5(b'zjcs:partner-current:NpcPanelNpcItem.prefab').hexdigest()
        self.queue.append((Path('NpcPanelNpcItem.prefab'), prefab_guid, card['documents']))
        folder = self.work / 'card-assets'
        outputs = {}
        for target, target_guid, data in self.queue:
            path = folder / target
            if isinstance(data, dict):
                raw = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
                for file_id, content in data.items():
                    kind, = content
                    raw += '--- !u!' + str(int(ClassIDType[kind])) + ' &' + str(file_id) + '\n'
                    raw += yaml.dump(content, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
                raw = raw.encode('utf8')
            else:
                raw = data
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
            extension = path.suffix.lower()
            importer = ('PrefabImporter' if extension == '.prefab' else 'ShaderImporter' if extension == '.shader'
                        else 'TrueTypeFontImporter' if extension in ('.ttf', '.otf') else 'NativeFormatImporter')
            Path(str(path) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + target_guid + '\n' + importer + ':\n  externalObjects: {}\n', 'utf8')
            outputs[target.as_posix()] = sha(raw)
            outputs[target.as_posix() + '.meta'] = sha(Path(str(path) + '.meta').read_bytes())
        if dynamic is not None:
            target = Path('partner-card-images.json')
            path = folder / target
            raw = (json.dumps(dynamic, ensure_ascii=False, indent=2) + '\n').encode('utf8')
            path.write_bytes(raw)
            Path(str(path) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:partner-card-images').hexdigest()
                + '\nTextScriptImporter:\n  externalObjects: {}\n', 'utf8')
            outputs[target.as_posix()] = sha(raw)
            outputs[target.as_posix() + '.meta'] = sha(Path(str(path) + '.meta').read_bytes())
        for target, (target_guid, raw) in self.audio_outputs.items():
            path = folder / target
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
            Path(str(path) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + target_guid +
                '\nAudioImporter:\n  externalObjects: {}\n  serializedVersion: 7\n  defaultSettings:\n'
                '    loadType: 0\n    sampleRateSetting: 0\n    sampleRateOverride: 0\n'
                '    compressionFormat: 0\n    quality: 1\n    conversionMode: 0\n'
                '  forceToMono: 0\n  normalize: 0\n  preloadAudioData: 1\n  loadInBackground: 0\n  ambisonic: 0\n  3D: 0\n', 'utf8')
            outputs[target.as_posix()] = sha(raw)
            outputs[target.as_posix() + '.meta'] = sha(Path(str(path) + '.meta').read_bytes())
        receipt = dict(manifest=self.recovery.apk.version, inputs=self.inputs, objects=self.proof,
            audio=self.audio_proof, outputs=outputs, root=dict(fileID=card['root'], guid=prefab_guid, type=3),
            script_bindings=self.script_sources,
            graph_bindings=[dict(guid=value['guid'], name=value['name'], type=value['type'])
                            for value in self.proof.values() if value.get('output', '').startswith('Graph/')],
            dynamic_images=dynamic,
            generator_sha256=sha(Path(__file__).read_bytes()))
        (self.work / 'partner-card-binding-source-manifest.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2), 'utf8')
        print('Staged source-backed native partner card:', folder, flush=True)


def install_staged(work):
    proof = ROOT / 'assets/reference/partner-card-binding-source-manifest.json'
    receipt_path = work / proof.name
    receipt = json.loads(receipt_path.read_bytes())
    previous = json.loads(proof.read_bytes()) if proof.is_file() else {}
    destination = ROOT / 'unity/Assets/Official/Partners/UI/Card'
    if previous.get('installed_root') and set(previous['outputs']) - receipt['outputs'].keys():
        raise ValueError('Refusing to forget installed payloads; retain the previous NPC image scope')
    for name, digest in receipt['outputs'].items():
        source = work / 'card-assets' / name
        target = destination / name
        if sha(source.read_bytes()) != digest:
            raise ValueError('Staged native card changed: ' + name)
        if target.is_file() and sha(target.read_bytes()) != previous.get('outputs', {}).get(name):
            raise ValueError('Installed native card differs from prior provenance: ' + name)
    folders = {destination, destination.parent}
    for name in receipt['outputs']:
        parent = (destination / name).parent
        while parent != destination:
            folders.add(parent)
            parent = parent.parent
    metadata = {}
    for folder in folders:
        identity = folder.relative_to(ROOT).as_posix()
        raw = ('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs:partner-card-folder:' + identity).encode()).hexdigest()
               + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()
        path = Path(str(folder) + '.meta')
        if path.exists() and path.read_bytes().replace(b'\r\n', b'\n') != raw:
            raise ValueError('Native card folder metadata differs: ' + identity)
        metadata[path] = raw
    for name in receipt['outputs']:
        target = destination / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(work / 'card-assets' / name, target)
    for path, raw in metadata.items():
        path.write_bytes(raw)
    receipt['installed_root'] = destination.relative_to(ROOT).as_posix()
    receipt['folder_metadata'] = {path.relative_to(ROOT).as_posix(): sha(raw) for path, raw in sorted(metadata.items())}
    raw = (json.dumps(receipt, ensure_ascii=False, indent=2) + '\n').encode('utf8')
    receipt_path.write_bytes(raw)
    proof.write_bytes(raw)
    print('Installed verified original partner card:', destination, flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--npc-ids', type=int, nargs='+', default=[], help='Also bind these original card figure/background addresses')
    parser.add_argument('--install', action='store_true', help='Install verified staged assets and update their source receipt')
    args = parser.parse_args()
    stage = validation_directory()
    PartnerCardBinding(stage).build(args.npc_ids)
    if args.install:
        install_staged(stage)
