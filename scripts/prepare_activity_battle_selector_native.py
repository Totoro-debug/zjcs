"""Bind the original activity difficulty cell to migrated scripts and Windows assets."""
import hashlib
import json
import copy
from pathlib import Path

import yaml
from UnityPy.enums import ClassIDType
from official_manifest import read_asset_manifest

from official_apk import ROOT
from prepare_partner_card_native import Dumper, guid, sha, script_id
from prepare_partner_roster_native import PartnerRosterBinding
from prepare_partner_panel_native import PartnerPanelBinding
from validation_workspace import validation_directory
from npc_travel_language import language


class SelectorBinding(PartnerRosterBinding):
    def register_scripts(self):
        super().register_scripts()
        for namespace, name, relative in (('', 'ActivityFestivalBossNumCell', 'ActivityBattleUI/ActivityFestivalBossNumCell.cs'),
                ('', 'ActivityFestivalBossNumScrollView', 'ActivityBattleUI/ActivityFestivalBossNumScrollView.cs'),
                ('FancyScrollView', 'Scroller', 'SkillsUI/Scroller.cs'), ('', 'MirrorImage', 'WorldUI/MirrorImage.cs'),
                ('', 'NoDrawingRayCast', 'WorldUI/NoDrawingRayCast.cs'), ('', 'PreviewTool', 'WorldUI/WorldPreviewTool.cs')):
            path = ROOT / 'unity/Assets/Game/Runtime/Official' / relative
            pointer = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
            self.scripts['.'.join(filter(None, [namespace, name]))] = pointer
            self.class_ids[script_id(namespace, name)] = pointer
            self.script_guids.add(pointer['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + '.meta').read_bytes()))

    def load(self, source_guid):
        obj = self.objects.get(source_guid)
        if source_guid not in self.redirects and obj is not None and obj.type.name == 'AnimatorController':
            return PartnerPanelBinding.controller(self, source_guid, obj)
        return super().load(source_guid)


def encode(data):
    text = '%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'
    for file_id, content in data.items():
        kind, = content
        text += '--- !u!' + str(int(ClassIDType[kind])) + ' &' + str(file_id) + '\n'
        text += yaml.dump(content, Dumper=Dumper, sort_keys=False, allow_unicode=True, width=1000000)
    return text.encode('utf8')


def scroll_view(binding, work, cell_guid, cell_root):
    binding.recovery.recover('ActivityDomainBossPanel', work / 'activity-selector-native',
        bundle='defaultpackage_assets_prefabs_ui_activitydomainboss.bundle',
        native_relative='Prefabs/UI/ActivityDomainBoss/ActivityDomainBossPanel.prefab')
    binding.recovery.finalize()
    native, _, panel, record = binding.recovery.outputs[-1]
    for path in (native, Path(str(native) + '.meta')):
        key = path.relative_to(binding.cache / 'native-export-all').as_posix()
        if sha(path.read_bytes()) != binding.checked[key]: raise ValueError('Native selector source changed')
        binding.inputs[path.relative_to(ROOT).as_posix()] = binding.checked[key]
    consumer, = [row for row in panel['scripts'] if row['type'] == 'ActivityDomainBossPanelDetailContent']
    component = panel['documents'][consumer['nativeFileID']]['MonoBehaviour']['mNumScrollView']['fileID']
    root = panel['documents'][component]['MonoBehaviour']['m_GameObject']['fileID']
    selected = set()
    def visit(go):
        selected.add(go)
        components = panel['documents'][go]['GameObject']['m_Component']
        selected.update(row['component']['fileID'] for row in components)
        transform = next(iter(panel['documents'][components[0]['component']['fileID']].values()))
        for child in transform['m_Children']:
            visit(next(iter(panel['documents'][child['fileID']].values()))['m_GameObject']['fileID'])
    visit(root)
    documents = copy.deepcopy({key: value for key, value in panel['documents'].items() if key in selected})
    transform = next(iter(documents[documents[root]['GameObject']['m_Component'][0]['component']['fileID']].values()))
    old_parent = transform['m_Father']
    transform['m_Father'] = dict(fileID=0)
    def check(value):
        if isinstance(value, dict):
            if set(value) == {'fileID'} and value['fileID'] and value['fileID'] not in selected:
                raise ValueError('Original selector references outside its subtree: ' + repr(value))
            for child in value.values(): check(child)
        elif isinstance(value, list):
            for child in value: check(child)
    check(documents)
    for key, pointer in panel['external'].items(): binding.objects[pointer['guid']] = binding.original(key)
    for key, file_id in panel['objectMap'].items():
        if file_id in selected:
            owner = binding.original(key)
            binding.learn(binding.recovery.fields(owner), next(iter(documents[file_id].values())), owner)
    fields = documents[component]['MonoBehaviour']
    binding.redirects[fields['cellPrefab']['guid']] = dict(fileID=cell_root, guid=cell_guid, type=3)
    for row in panel['scripts']:
        if row['nativeFileID'] not in selected: continue
        fields = documents[row['nativeFileID']]['MonoBehaviour']
        fields['m_Script'] = binding.scripts['.'.join(filter(None, [row['namespace'], row['type']]))]
        if row['type'] == 'ActivityFestivalBossNumScrollView':
            owner = binding.original(row['original'])
            fields['mPreSelectedEvent'] = binding.sound(binding.recovery.fields(owner)['mPreSelectedEvent'], owner)
    resolved = binding.replace(documents)
    binding.queue.append((Path('Resources/DailyChallenges/Selector/ActivityDomainBossNumScrollView.prefab'),
        hashlib.md5(b'zjcs:activity-selector:ActivityDomainBossNumScrollView').hexdigest(), resolved))
    return dict(prefab=record, consumer=consumer['original'], field='mNumScrollView', native_root=root,
        objects=len(selected), original_parent=old_parent,
        adaptation='Exact original referenced subtree; detach root parent only. Bind migrated cell and local original audio.')


def main():
    work = validation_directory()
    binding = SelectorBinding(work)
    binding.recovery.recover('ActivityDomainBossNumCell', work / 'activity-selector-native',
        bundle='defaultpackage_assets_prefabs_ui_activitydomainboss.bundle',
        native_relative='Prefabs/UI/ActivityDomainBoss/ActivityDomainBossNumCell.prefab')
    binding.recovery.finalize()
    native, _, cell, record = binding.recovery.outputs[-1]
    key = native.relative_to(binding.cache / 'native-export-all').as_posix()
    for path, expected in ((native, binding.checked[key]),
                           (Path(str(native) + '.meta'), binding.checked[key + '.meta'])):
        if sha(path.read_bytes()) != expected: raise ValueError('Native cell source changed')
        binding.inputs[path.relative_to(ROOT).as_posix()] = expected
    for key, pointer in cell['external'].items(): binding.objects[pointer['guid']] = binding.original(key)
    for key, file_id in cell['objectMap'].items():
        owner = binding.original(key)
        binding.learn(binding.recovery.fields(owner), next(iter(cell['documents'][file_id].values())), owner)
    for row in cell['scripts']:
        fields = cell['documents'][row['nativeFileID']]['MonoBehaviour']
        name = '.'.join(filter(None, [row['namespace'], row['type']]))
        fields['m_Script'] = binding.scripts[name]
        if row['type'] == 'ButtonSound':
            owner = binding.original(row['original'])
            fields['ClickEvent'] = binding.sound(binding.recovery.fields(owner)['ClickEvent'], owner)
    resolved = binding.replace(cell['documents'])
    text_keys = {cell['documents'][row['nativeFileID']]['MonoBehaviour']['myKey']
                 for row in cell['scripts'] if row['type'] == 'MultiLanguageText'}
    texts, sources = language(text_keys)
    if set(texts) != text_keys: raise ValueError('Original difficulty text missing')
    binding.inputs.update(sources)
    binding.queue.append((Path('Resources/DailyChallenges/Selector/texts.json'),
        hashlib.md5(b'zjcs:activity-selector:texts').hexdigest(),
        (json.dumps(dict(texts=[dict(key=key, text=texts[key]) for key in sorted(texts)]),
                    ensure_ascii=False, indent=2) + '\n').encode('utf8')))
    prefab_guid = hashlib.md5(b'zjcs:activity-selector:ActivityDomainBossNumCell').hexdigest()
    binding.script_guids.add(prefab_guid)
    scroll = scroll_view(binding, work, prefab_guid, cell['root'])
    binding.queue.extend((path, value, raw) for path, (value, raw) in binding.audio_outputs.items())
    binding.script_guids.update(pointer['guid'] for pointer in binding.redirects.values() if pointer.get('guid'))
    for target, target_guid, data in binding.queue:
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
    redirects = {value: hashlib.md5(('zjcs:activity-selector-font:' + value).encode()).hexdigest() for value in owned}
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
    resolved = bind_font(resolved)
    binding.queue.append((Path('Resources/DailyChallenges/Selector/ActivityDomainBossNumCell.prefab'), prefab_guid, resolved))
    destination = ROOT / 'unity/Assets/Official/DailyChallenges/UI/Selector'
    target = ROOT / 'assets/reference/daily-challenges/activity-selector-native-source-manifest.json'
    previous = json.loads(target.read_text('utf8'))['installed'] if target.exists() else {}
    existing = {}
    for folder in (ROOT / 'unity/Assets/Official/Partners', destination):
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
                meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs:activity-selector-folder:' +
                    parent.relative_to(ROOT).as_posix()).encode()).hexdigest() +
                    '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
            installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes()); parent = parent.parent
    for folder in (destination.parent, destination.parent.parent):
        meta = Path(str(folder) + '.meta')
        if not meta.exists():
            meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs:activity-selector-folder:' +
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
    receipt = dict(manifest=binding.recovery.apk.version, prefab=record, inputs=binding.inputs,
        scripts={name: row for name, row in binding.script_sources.items() if row['pointer']['guid'] in used_scripts},
        resources=binding.proof, installed=installed, reused=reused, shared_resources=shared,
        font=font, font_instance_guids=redirects,
        scroll_view=scroll,
        audio=binding.audio_proof,
        generator_sha256=sha(Path(__file__).read_bytes()),
        scope='Original difficulty cell and referenced scroll-view subtree; the full activity panel host is still pending.')
    target.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed original activity difficulty cell:', len(installed), 'files;', len(reused), 'assets reused.', flush=True)


if __name__ == '__main__':
    main()
