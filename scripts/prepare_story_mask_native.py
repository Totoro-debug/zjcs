"""Recover native original story masks and bind migrated scripts for Windows."""
import hashlib
import json
from pathlib import Path
from official_apk import ROOT
from official_manifest import read_asset_manifest
from prepare_partner_card_native import guid, sha, script_id
from prepare_partner_roster_native import PartnerRosterBinding
from prepare_activity_battle_selector_native import encode
from validation_workspace import validation_directory

class StoryMaskBinding(PartnerRosterBinding):
    def register_scripts(self):
        super().register_scripts()
        paths = {name: 'StoryMasks/' + name + '.cs' for name in
                 ('StoryMask', 'StoryMaskGraphic', 'ProcessGuideHint', 'AttachToObjectLocalUI', 'UIBloomBuiltInGroupRegister')}
        paths.update(AttachToObjUI='StoryBubbles/AttachToObjUI.cs', UIBloomGroup='StoryBubbles/UIBloomGroup.cs')
        for name, relative in paths.items():
            path = ROOT / 'unity/Assets/Game/Runtime/Official' / relative
            pointer = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
            self.scripts[name] = pointer
            self.class_ids[script_id('', name)] = pointer
            self.script_guids.add(pointer['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + '.meta').read_bytes()))


def main():
    work = validation_directory()
    binding = StoryMaskBinding(work)
    records_proof = []
    prefabs = []
    for name in ('StoryMask', 'Mask'):
        binding.recovery.recover(name, work / 'native', bundle='defaultpackage_assets_prefabs_ui_story.bundle',
            native_relative='Prefabs/UI/Story/' + name + '.prefab')
        binding.recovery.finalize()
        native, _, prefab, record = binding.recovery.outputs[-1]
        records_proof.append(record)
        for path in (native, Path(str(native) + '.meta')):
            key = path.relative_to(binding.cache / 'native-export-all').as_posix()
            if sha(path.read_bytes()) != binding.checked[key]: raise ValueError('Native story mask cache differs')
            binding.inputs[path.relative_to(ROOT).as_posix()] = binding.checked[key]
        for key, pointer in prefab['external'].items(): binding.objects[pointer['guid']] = binding.original(key)
        for key, file_id in prefab['objectMap'].items():
            owner = binding.original(key)
            binding.learn(binding.recovery.fields(owner), next(iter(prefab['documents'][file_id].values())), owner)
        for row in prefab['scripts']:
            fields = prefab['documents'][row['nativeFileID']]['MonoBehaviour']
            fields['m_Script'] = binding.scripts['.'.join(filter(None, [row['namespace'], row['type']]))]
        prefabs.append((name, binding.replace(prefab['documents'])))
    for name, data in prefabs:
        target_guid = hashlib.md5(('zjcs:story-mask:' + name + ':prefab').encode()).hexdigest()
        binding.script_guids.add(target_guid)
        binding.queue.append((Path('Resources/StoryUI/Mask') / (name + '.prefab'), target_guid, data))
    binding.script_guids.update(pointer['guid'] for pointer in binding.redirects.values() if pointer.get('guid'))
    for _, _, data in binding.queue:
        if isinstance(data, dict):
            result = binding.replace(data)
            data.clear(); data.update(result)
    records = read_asset_manifest(binding.recovery.apk.source.manifest_bytes('DefaultPackage', binding.recovery.apk.archive))
    font = binding.chinese_font(records)
    # FontAssetFontService.SetupFont binds this exact source TTF at runtime in the
    # original client. Bind it explicitly for the offline native asset instead.
    # Own the dynamic font, material and atlas so other modules keep their caches.
    font_data, = [data for _, value, data in binding.queue if value == font['font_asset']['guid']]
    font_fields = font_data[font['font_asset']['fileID']]['MonoBehaviour']
    owned = {font['font_asset']['guid'], font_fields['material']['guid']}
    owned.update(value['guid'] for value in font_fields['m_AtlasTextures'] if value.get('guid'))
    redirects = {value: hashlib.md5(('zjcs:story-mask-font:' + value).encode()).hexdigest() for value in owned}
    def bind_font(value):
        if isinstance(value, dict):
            return {key: redirects.get(child, child) if key == 'guid' else bind_font(child)
                    for key, child in value.items()}
        if isinstance(value, list): return [bind_font(child) for child in value]
        return value
    font_fields['m_SourceFontFile'] = font['source_font']
    font_fields['m_SourceFontFileGUID'] = font['source_font']['guid']
    font_fields['m_SourceFontFile_EditorRef'] = font['source_font']
    binding.queue = [(path, redirects.get(value, value), bind_font(data)) for path, value, data in binding.queue]
    destination = ROOT / 'unity/Assets/Official/StoryUI/Mask'
    target = ROOT / 'assets/reference/official-map/story-mask-native-source-manifest.json'
    previous = json.loads(target.read_text('utf8'))['installed'] if target.exists() else {}
    existing = {}
    for folder in (ROOT / 'unity/Assets/Official/Partners', ROOT / 'unity/Assets/Official/DailyChallenges/UI/Selector', destination):
        if not folder.exists(): continue
        for meta in folder.rglob('*.meta'):
            path = Path(str(meta)[:-5])
            if path.is_file():
                value = guid(meta)
                if value in existing: raise ValueError('Existing native GUID collision')
                existing[value] = path
    planned = []
    for relative, target_guid, data in binding.queue:
        raw = encode(data) if isinstance(data, dict) else data
        path = existing.get(target_guid, destination / relative)
        if path.is_file() and path.read_bytes().replace(b'\r\n', b'\n') != raw.replace(b'\r\n', b'\n'):
            if not path.is_relative_to(destination) or sha(path.read_bytes()) != previous.get(path.relative_to(ROOT).as_posix()):
                raise ValueError('Refusing changed native resource overwrite: ' + str(path))
        planned.append((path, target_guid, raw, data))
    installed, reused, shared = {}, [], {}
    for path, target_guid, raw, data in planned:
        if path.is_file() and path.read_bytes() == raw: reused.append(path.relative_to(ROOT).as_posix())
        else:
            path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(raw)
        meta = Path(str(path) + '.meta')
        if not meta.exists():
            if path.suffix == '.prefab': importer = 'PrefabImporter:\n  externalObjects: {}\n'
            elif isinstance(data, dict):
                importer = 'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: ' + str(next(iter(data))) + '\n'
            else: importer = ''
            meta.write_text('fileFormatVersion: 2\nguid: ' + target_guid + '\n' + importer, 'utf8', newline='\n')
        if path.is_relative_to(destination):
            for entry in (path, meta): installed[entry.relative_to(ROOT).as_posix()] = sha(entry.read_bytes())
        else:
            # A shared importer's unrelated settings may be maintained by another
            # module. This binding depends on its exact payload and stable GUID.
            shared[path.relative_to(ROOT).as_posix()] = dict(sha256=sha(path.read_bytes()), guid=guid(meta))
        parent = path.parent
        while parent.is_relative_to(destination):
            meta = Path(str(parent) + '.meta')
            if not meta.exists():
                meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs:story-mask-folder:' +
                    parent.relative_to(ROOT).as_posix()).encode()).hexdigest() +
                    '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
            installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes()); parent = parent.parent
    for folder in (destination.parent, destination.parent.parent):
        meta = Path(str(folder) + '.meta')
        if not meta.exists():
            meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs:story-mask-folder:' +
                folder.relative_to(ROOT).as_posix()).encode()).hexdigest() +
                '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
        installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
    used_scripts = set()
    def scripts(value):
        if isinstance(value, dict):
            if isinstance(value.get('m_Script'), dict): used_scripts.add(value['m_Script'].get('guid'))
            for child in value.values(): scripts(child)
        elif isinstance(value, list):
            for child in value: scripts(child)
    for _, _, data in binding.queue: scripts(data)
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    adapters = [runtime / 'Presentation/World' / name for name in
                ('NativeStoryMaskHost.cs', 'NativeStoryWindowMetadata.cs', 'NativeStoryUiBindings.cs', 'OfficialDungeonPopupMask.cs')]
    adapters.extend(runtime / 'Offline/StoryUI' / name for name in ('StoryUiModule.cs', 'Zjcs.Offline.StoryUI.asmdef'))
    adapters.extend(runtime / 'Official/StoryMasks' / name for name in ('StoryMaskHost.cs', 'Zjcs.Official.StoryMasks.asmdef'))
    binding.inputs.update({(ROOT / 'scripts' / name).relative_to(ROOT).as_posix(): sha((ROOT / 'scripts' / name).read_bytes())
        for name in ('prepare_partner_card_native.py', 'prepare_partner_roster_native.py',
                     'prepare_activity_battle_selector_native.py', 'prepare_story_mask_runtime.py', 'prepare_story_ui_registrations.py')})
    receipt = dict(manifest=binding.recovery.apk.version, prefabs=records_proof, inputs=binding.inputs,
        scripts={name: row for name, row in binding.script_sources.items() if row['pointer']['guid'] in used_scripts},
        resources=binding.proof, installed=installed, reused=reused, shared_resources=shared,
        font=font, font_instance_guids=redirects,
        
        audio=binding.audio_proof,
        runtime_adapters={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in adapters},
        generator_sha256=sha(Path(__file__).read_bytes()),
        scope='Original StoryMask and Mask prefabs with their native hierarchy, fields and dependencies. Bloom composition depends on the map host.')
    target.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed original story mask:', len(installed), 'files;', len(reused), 'assets reused.', flush=True)


if __name__ == "__main__":
    main()
