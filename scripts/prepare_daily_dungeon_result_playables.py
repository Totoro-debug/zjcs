"""Bind the selected daily result's original chest/item performance graphs."""
import hashlib
import json
from pathlib import Path
import yaml
from UnityPy.enums import ClassIDType

from official_apk import ROOT
from official_manifest import read_asset_manifest
from prepare_current_partner_native import identity
from prepare_partner_card_native import PartnerCardBinding, guid, sha, script_id, Dumper
from prepare_daily_dungeon_result_ui import write_folder_tree
from prepare_partner_input_dialog import metadata
from validation_workspace import validation_directory


class ResultPlayables(PartnerCardBinding):
    def replace(self, value):
        if isinstance(value, dict) and isinstance(value.get('m_StreamData'), dict):
            stream = value['m_StreamData']
            if stream.get('path') is None:
                # An empty Unity string must stay empty. YAML `null` becomes a
                # literal path and makes Unity discard the original vertex data.
                value = dict(value, m_StreamData=dict(stream, path=''))
        return super().replace(value)

    def register_scripts(self):
        super().register_scripts()
        self.runtime = ROOT / 'unity/Assets/Game/Runtime'
        self.runtime_types = {}
        for folder in ('Official',):
            for path in (self.runtime / folder).rglob('*.cs'):
                self.runtime_types.setdefault(path.stem, []).append(path)

    def register(self, namespace, name):
        full = '.'.join(filter(None, (namespace, name)))
        if full in self.scripts: return
        paths = self.runtime_types.get(name, [])
        if len(paths) != 1: raise ValueError('Migrate original result playable: ' + full)
        path, = paths
        value = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
        self.scripts[full] = value
        self.script_guids.add(value['guid'])
        self.class_ids[script_id(namespace, name)] = value
        self.script_sources[full] = dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path.read_bytes()), pointer=value)

    def load(self, source_guid):
        if source_guid in self.redirects: return self.redirects[source_guid]
        obj = self.objects[source_guid]
        if obj.type.name in ('MonoScript', 'MonoBehaviour'):
            script = obj.read(check_read=False) if obj.type.name == 'MonoScript' else obj.read(check_read=False).m_Script.read()
            self.register(script.m_Namespace, script.m_ClassName)
        if obj.type.name != 'GameObject':
            result = super().load(source_guid)
            if result.get('guid'): self.script_guids.add(result['guid'])
            return result
        return self.prefab(obj, source_guid)

    def prefab(self, obj, source_guid=None):
        name = obj.read().m_Name
        key = identity(obj)
        target_guid = hashlib.md5(('zjcs:daily-result-playable:' + key).encode()).hexdigest()
        prior = next((row for row in self.queue if row[1] == target_guid), None)
        if prior: return dict(fileID=self.prefab_roots[key], guid=target_guid, type=3)
        entries = [row for row in self.table if Path(row['path']).stem == name and row['path'].endswith('.prefab')]
        matched = [row for row in entries if identity(self.recovery.apk.asset(row['bundle'], row['path'])) == key]
        bundle = matched[0]['bundle'] if matched else self.recovery.apk.provenance(obj)['bundle']
        native, = [path for path in self.by_name[self.name_key(name)] if path.suffix == '.prefab' and path.stem == name]
        self.recovery.recover(name, self.work / 'daily-result-playable-inputs', bundle=bundle,
            native_relative=native.relative_to(self.native).as_posix())
        _, _, template, evidence = self.recovery.outputs[-1]
        self.prefab_roots[key] = template['root']
        ref = dict(fileID=template['root'], guid=target_guid, type=3)
        if source_guid: self.redirects[source_guid] = ref
        self.script_guids.add(target_guid)
        for original, pointer in template['external'].items(): self.objects[pointer['guid']] = self.original(original)
        for original, file_id in template['objectMap'].items():
            owner = self.original(original)
            self.learn(self.recovery.fields(owner), next(iter(template['documents'][file_id].values())), owner)
        for script in template['scripts']:
            self.register(script['namespace'], script['type'])
            template['documents'][script['nativeFileID']]['MonoBehaviour']['m_Script'] = self.scripts[
                '.'.join(filter(None, (script['namespace'], script['type'])))]
            if script['type'] == 'MyPlayableSound':
                owner = self.original(script['original'])
                fields = self.recovery.fields(owner)
                for field in ('SoundEvent', 'StopSoundEvent'):
                    payload = fields[field]
                    if payload['WwiseObjectReference']['m_PathID']: self.sound(payload, owner)
        target = Path('Prefabs') / (name + '.prefab')
        self.queue.append((target, target_guid, template['documents']))
        resolved = self.replace(template['documents'])
        template['documents'].clear(); template['documents'].update(resolved)
        return ref

    def build(self):
        from prepare_current_partner_native import pointer
        self.table = read_asset_manifest(self.recovery.apk.source.manifest_bytes('DefaultPackage', self.recovery.apk.archive))
        self.prefab_roots = {}
        previous = ROOT / 'assets/reference/dungeon-ui/884_156692/daily-dungeon-result-playables-source-manifest.json'
        if previous.is_file():
            (self.work / 'partner-card-audio.json').write_text(json.dumps(
                json.loads(previous.read_text('utf8')).get('audio', {})), 'utf8')
        entry, = [row for row in self.table if row['path'].endswith('/DailyDungeonPlayableSettings.asset')]
        settings_obj = self.recovery.apk.asset(entry['bundle'], entry['path'])
        settings = self.recovery.fields(settings_obj)
        def prefab(ref):
            if not ref['m_PathID']: return None
            obj = pointer(ref, settings_obj)
            self.prefab(obj)
            return 'Dungeons/UI/884_156692/Native/Playables/Prefabs/' + obj.read().m_Name
        catalog = dict(manifest=self.recovery.apk.version,
            grades=[dict(chestId=row['ChestId'], effect=prefab(row['ChestEffectObj'])) for row in settings['GradeDatas']],
            openChest=prefab(settings['PlayableData']['MyOpenChestPlayablePrefab']),
            qualities=[dict(quality=row['Quality'], open=prefab(row['MyOpenEffectGo']),
                loopEquip=prefab(row['LoopEquipEffectGo']), loopPiece=prefab(row['LoopPieceEffectGo']))
                for row in settings['PlayableData']['ChestDatas']])
        for _, _, data in self.queue:
            if isinstance(data, dict):
                resolved = self.replace(data); data.clear(); data.update(resolved)
        self.recovery.finalize()
        destination = ROOT / 'unity/Assets/Game/Resources/Dungeons/UI/884_156692/Native/Playables'
        outputs = []
        audio_rows = []
        for relative, (audio_guid, raw) in self.audio_outputs.items():
            audio_guid = hashlib.md5(('zjcs:daily-result-audio:' + audio_guid).encode()).hexdigest()
            path = destination / relative
            write_folder_tree(path.parent)
            path.write_bytes(raw)
            meta = Path(str(path) + '.meta')
            meta.write_text('fileFormatVersion: 2\nguid: ' + audio_guid + '\nAudioImporter:\n  externalObjects: {}\n'
                '  serializedVersion: 7\n  defaultSettings:\n    loadType: 0\n    sampleRateSetting: 0\n'
                '    compressionFormat: 0\n    quality: 1\n  normalize: 0\n  preloadAudioData: 1\n  3D: 0\n', 'utf8', newline='\n')
            outputs.extend((path, meta))
        for proof in self.audio_proof.values():
            audio_rows.append(dict(EventId=proof['event']['id'], Clip=dict(proof['pointer'],
                guid=hashlib.md5(('zjcs:daily-result-audio:' + proof['pointer']['guid']).encode()).hexdigest()),
                TargetIds=[], StopTargetIds=[], StopSeconds=[], StopCurves=[], FadeInSeconds=0, FadeInCurve=0))
        if audio_rows:
            script = self.runtime / 'Offline/ProfessionUI/OfflineProfessionAudioCatalog.cs.meta'
            self.queue.append((Path('audio-catalog.asset'), hashlib.md5(b'zjcs:daily-result-audio').hexdigest(),
                {11400000: dict(MonoBehaviour=dict(m_ObjectHideFlags=0,
                    m_Script=dict(fileID=11500000, guid=guid(script), type=3), m_Name='Daily dungeon result audio', Events=audio_rows))}))
        redirects = {value: hashlib.md5(('zjcs:daily-result-dependency:' + value).encode()).hexdigest()
            for _, value, _ in self.queue}
        def unique(value):
            if isinstance(value, dict):
                return {key: redirects.get(item, item) if key == 'guid' else unique(item) for key, item in value.items()}
            if isinstance(value, list): return [unique(item) for item in value]
            return value
        for relative, target_guid, data in self.queue:
            target_guid = redirects[target_guid]
            data = unique(data)
            path = destination / relative
            write_folder_tree(path.parent)
            if isinstance(data, dict):
                raw = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
                for file_id, content in data.items():
                    kind, = content
                    raw += '--- !u!' + str(int(ClassIDType[kind])) + ' &' + str(file_id) + '\n'
                    raw += yaml.dump(content, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
                raw = raw.encode('utf8')
            else: raw = data
            path.write_bytes(raw)
            importer = 'PrefabImporter' if path.suffix == '.prefab' else 'ShaderImporter' if path.suffix == '.shader' else 'NativeFormatImporter'
            meta = Path(str(path) + '.meta')
            meta.write_text('fileFormatVersion: 2\nguid: ' + target_guid + '\n' + importer + ':\n  externalObjects: {}\n', 'utf8', newline='\n')
            outputs.extend((path, meta))
        path = destination / 'catalog.json'
        path.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        outputs.extend((path, metadata(path)))
        receipt = dict(manifest=self.recovery.apk.version, inputs=self.inputs, objects=self.proof,
            source=self.recovery.apk.provenance(settings_obj), source_sha256=sha(settings_obj.get_raw_data()),
            native_recovery=self.recovery.proof, scripts=self.script_sources, audio=self.audio_proof,
            generator=dict(path=Path(__file__).relative_to(ROOT).as_posix(), sha256=sha(Path(__file__).read_bytes())),
            outputs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p.read_bytes())) for p in outputs])
        path = ROOT / 'assets/reference/dungeon-ui/884_156692/daily-dungeon-result-playables-source-manifest.json'
        path.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Restored original daily result playable graphs:', len(self.prefab_roots))


if __name__ == '__main__':
    binding = ResultPlayables(validation_directory())
    try: binding.build()
    finally: binding.recovery.apk.close()
