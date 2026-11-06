"""Extract the original result action module, keeping its ancestor geometry."""
import copy
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from prepare_activity_battle_selector_native import SelectorBinding, encode
from prepare_partner_card_native import guid, script_id, sha
from validation_workspace import validation_directory


class ResultActionBinding(SelectorBinding):
    def register_scripts(self):
        super().register_scripts()
        for name, relative in (
            ('GameEndPanelBase', 'BattleResultUI/GameEndPanelBase.cs'),
            ('EndPanelAnimEventReceiver', 'BattleResultUI/EndPanelAnimEventReceiver.cs'),
            ('SafeArea', 'WorldUI/WorldSafeArea.cs')):
            path = ROOT / 'unity/Assets/Game/Runtime/Official' / relative
            pointer = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
            self.scripts[name] = self.class_ids[script_id('', name)] = pointer
            self.script_guids.add(pointer['guid'])
            self.script_sources[name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
                sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + '.meta').read_bytes()))


def extract(binding, work, name):
    binding.recovery.recover(name, work / 'result-actions-native',
        bundle='defaultpackage_assets_prefabs_ui_fight_' + name.lower() + '.bundle',
        native_relative='Prefabs/UI/Fight/' + name + '.prefab')
    binding.recovery.finalize()
    native, _, panel, source = binding.recovery.outputs[-1]
    for path in (native, Path(str(native) + '.meta')):
        key = path.relative_to(binding.cache / 'native-export-all').as_posix()
        if sha(path.read_bytes()) != binding.checked[key]:
            raise ValueError('Changed original result source: ' + str(path))
        binding.inputs[path.relative_to(ROOT).as_posix()] = binding.checked[key]
    original = panel['documents']
    def fields(file_id): return next(iter(original[file_id].values()))
    base, = [row for row in panel['scripts'] if row['type'] == 'GameEndPanelBase']
    base_fields = fields(base['nativeFileID'])
    button_go = fields(base_fields['mSkillItemReportBtn']['fileID'])['m_GameObject']['fileID']
    button_transform = fields(fields(button_go)['m_Component'][0]['component']['fileID'])
    group = fields(button_transform['m_Father']['fileID'])['m_GameObject']['fileID']
    selected, ancestors = set(), set()
    def visit(go):
        selected.add(go)
        components = fields(go)['m_Component']
        selected.update(row['component']['fileID'] for row in components)
        for child in fields(components[0]['component']['fileID'])['m_Children']:
            visit(fields(child['fileID'])['m_GameObject']['fileID'])
    visit(group)
    parent = fields(fields(group)['m_Component'][0]['component']['fileID'])['m_Father']['fileID']
    while parent:
        ancestor = fields(parent)['m_GameObject']['fileID']
        ancestors.add(ancestor)
        selected.update((parent, ancestor))
        # SafeArea is part of the source geometry, retained verbatim.
        for component in fields(ancestor)['m_Component'][1:]:
            component_id = component['component']['fileID']
            if any(row['nativeFileID'] == component_id and row['type'] == 'SafeArea' for row in panel['scripts']):
                selected.add(component_id)
            if ancestor == panel['root'] and ('Canvas' in original[component_id] or any(
                    row['nativeFileID'] == component_id and row['type'] == 'GraphicRaycaster' for row in panel['scripts'])):
                selected.add(component_id)
        parent = fields(parent)['m_Father']['fileID']
    selected.add(base['nativeFileID'])
    documents = copy.deepcopy({key: value for key, value in original.items() if key in selected})
    for ancestor in ancestors:
        go = documents[ancestor]['GameObject']
        go['m_Component'] = [row for row in go['m_Component'] if row['component']['fileID'] in selected]
        transform = documents[go['m_Component'][0]['component']['fileID']]['RectTransform']
        transform['m_Children'] = [row for row in transform['m_Children'] if row['fileID'] in selected]
    def check(value):
        if isinstance(value, dict):
            if set(value) == {'fileID'} and value['fileID'] and value['fileID'] not in selected:
                raise ValueError('Action module has an outside local reference: ' + repr(value))
            for child in value.values(): check(child)
        elif isinstance(value, list):
            for child in value: check(child)
    check(documents)
    for key, pointer in panel['external'].items(): binding.objects[pointer['guid']] = binding.original(key)
    for key, file_id in panel['objectMap'].items():
        if file_id in selected:
            owner = binding.original(key)
            binding.learn(binding.recovery.fields(owner), next(iter(documents[file_id].values())), owner)
    for row in panel['scripts']:
        if row['nativeFileID'] not in selected: continue
        value = documents[row['nativeFileID']]['MonoBehaviour']
        value['m_Script'] = binding.scripts['.'.join(filter(None, [row['namespace'], row['type']]))]
        if row['type'] == 'ButtonSound':
            owner = binding.original(row['original'])
            value['ClickEvent'] = binding.sound(binding.recovery.fields(owner)['ClickEvent'], owner)
    target_guid = hashlib.md5(('zjcs:result-actions:' + name).encode()).hexdigest()
    binding.queue.append((Path('Resources/Combat/ResultActions/' + name + '.prefab'), target_guid,
        binding.replace(documents)))
    return dict(prefab=source, root=panel['root'], action_group=group, objects=len(selected),
        base=base['original'], retained_ancestors=sorted(ancestors),
        adaptation='Exact BtnGroup subtree, ancestor GameObjects/RectTransforms, SafeArea, Canvas and GraphicRaycaster; original GameEndPanelBase. Other result content is supplied by its host.')


def main():
    work = validation_directory()
    binding = ResultActionBinding(work)
    sources = [extract(binding, work, name) for name in
        ('GameEndPanelActivityDomainBoss', 'GameEndPanelActivityFunnyBoss')]
    binding.queue.extend((path, value, raw) for path, (value, raw) in binding.audio_outputs.items())
    # Resources loaded while binding the prefab (notably packed Sprites) enqueue
    # their own native documents. Resolve that entire dependency closure too.
    binding.script_guids.update(pointer['guid'] for pointer in binding.redirects.values() if pointer.get('guid'))
    for _, _, data in binding.queue:
        if isinstance(data, dict):
            resolved = binding.replace(data)
            data.clear()
            data.update(resolved)
    destination = ROOT / 'unity/Assets/Official/DailyChallenges/UI/ResultActions'
    manifest = ROOT / 'assets/reference/daily-challenges/battle-result-actions-native-source-manifest.json'
    previous = json.loads(manifest.read_text('utf8'))['installed'] if manifest.exists() else {}
    existing = {}
    for folder in (ROOT / 'unity/Assets/Official/Partners', ROOT / 'unity/Assets/Official/DailyChallenges/UI'):
        for meta in folder.rglob('*.meta'):
            path = Path(str(meta)[:-5])
            if path.is_file():
                value = guid(meta)
                if value in existing: raise ValueError('Duplicate native GUID: ' + value)
                existing[value] = path
    installed, shared, planned = {}, {}, []
    for relative, value, data in binding.queue:
        raw = encode(data) if isinstance(data, dict) else data
        path = existing.get(value, destination / relative)
        if path.exists() and path.read_bytes().replace(b'\r\n', b'\n') != raw.replace(b'\r\n', b'\n'):
            if not path.is_relative_to(destination) or sha(path.read_bytes()) != previous.get(path.relative_to(ROOT).as_posix()):
                raise ValueError('Refusing to overwrite changed native asset: ' + str(path))
        planned.append((path, value, raw, data))
    for path, value, raw, data in planned:
        if not path.exists() or path.read_bytes() != raw:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
        meta = Path(str(path) + '.meta')
        if not meta.exists():
            importer = 'PrefabImporter:\n  externalObjects: {}\n' if path.suffix == '.prefab' else (
                'NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: ' + str(next(iter(data))) + '\n'
                if isinstance(data, dict) else '')
            meta.write_text('fileFormatVersion: 2\nguid: ' + value + '\n' + importer, 'utf8', newline='\n')
        if path.is_relative_to(destination):
            for entry in (path, meta): installed[entry.relative_to(ROOT).as_posix()] = sha(entry.read_bytes())
        else: shared[path.relative_to(ROOT).as_posix()] = dict(sha256=sha(path.read_bytes()), guid=guid(meta))
        parent = path.parent
        while parent.is_relative_to(destination):
            meta = Path(str(parent) + '.meta')
            if not meta.exists():
                meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs:result-actions-folder:' +
                    parent.relative_to(ROOT).as_posix()).encode()).hexdigest() +
                    '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
            installed[meta.relative_to(ROOT).as_posix()] = sha(meta.read_bytes())
            parent = parent.parent
    used = set()
    def find_scripts(value):
        if isinstance(value, dict):
            if isinstance(value.get('m_Script'), dict): used.add(value['m_Script'].get('guid'))
            for child in value.values(): find_scripts(child)
        elif isinstance(value, list):
            for child in value: find_scripts(child)
    for _, _, data in binding.queue: find_scripts(data)
    receipt = dict(manifest=binding.recovery.apk.version, prefabs=sources, inputs=binding.inputs,
        scripts={name: row for name, row in binding.script_sources.items() if row['pointer']['guid'] in used},
        resources=binding.proof, audio=binding.audio_proof, installed=installed, shared_resources=shared,
        generator_sha256=sha(Path(__file__).read_bytes()))
    manifest.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed original result actions:', len(installed), 'files;', len(shared), 'shared assets.', flush=True)


if __name__ == '__main__':
    main()
