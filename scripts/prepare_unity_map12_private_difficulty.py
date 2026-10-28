"""Restore the private dungeon's original tab components and difficulty references."""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(ROOT / 'scripts'), str(ROOT / '.tools/character-art-python')]
import yaml
from official_apk import Apk
from official_unity_schema import ScriptSchemas
from official_native_yaml_verification import documents
from prepare_dungeon_ui_assets import convert_runtime_prefab
from prepare_dungeon_ui_bindings import CODE, VERSION, relative_transform_path

NATIVE = ROOT / 'unity/Assets/Game/Resources/Dungeons/UI/DailyDungeonDialogPrivateMap.prefab'
SOURCE = ROOT / 'unity/Assets/Official/Dungeons/Prefabs/UI/DailyDungeon/DailyDungeonDialogPrivateMap.prefab'
BINDINGS = ROOT / 'unity/Assets/StreamingAssets/Dungeons/UI/map12-private-difficulty.json'
RECEIPT = ROOT / 'assets/reference/official-map/map12-private-difficulty-source-manifest.json'
BASE_RECEIPT = ROOT / 'assets/reference/dungeon-ui/source-manifest.json'
PORTS = ('TabItem', 'TabGroup', 'TabAnimatorTransition')


def sha(raw): return hashlib.sha256(raw).hexdigest()
def rel(path): return path.relative_to(ROOT).as_posix()
def encode(value): return (json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + '\n').encode()


def write(path, raw, update):
    if path.exists() and path.read_bytes() == raw: return
    if path.exists() and not update: raise ValueError('Changed private difficulty output: ' + str(path))
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(raw)


class Dumper(yaml.SafeDumper): pass
def mapping(dumper, value):
    flow = 'fileID' in value and set(value) <= {'fileID', 'guid', 'type'}
    return dumper.represent_mapping('tag:yaml.org,2002:map', value.items(), flow_style=flow)
Dumper.add_representer(dict, mapping)
def plain(value):
    if isinstance(value, dict): return {key: plain(item) for key, item in value.items()}
    if isinstance(value, list): return [plain(item) for item in value]
    if isinstance(value, int): return int(value)
    if isinstance(value, float): return float(value)
    return value


def build(ugui, netstandard, update=False):
    base = json.loads(BASE_RECEIPT.read_bytes())
    if base['manifest'] != VERSION: raise ValueError('Wrong private difficulty native source version')
    source_record, = [row for row in base['outputs'] if row['path'] == rel(SOURCE)]
    native_record, = [row for row in base['runtime_prefabs'] if row['path'] == rel(NATIVE)]
    if sha(SOURCE.read_bytes()) != source_record['sha256']:
        raise ValueError('Original private difficulty native input changed')
    baseline = convert_runtime_prefab(SOURCE)
    if sha(baseline) != native_record['sha256']:
        raise ValueError('Original private difficulty UGUI conversion changed')
    prior = json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else None
    accepted = {native_record['sha256']}
    if prior:
        if prior['manifest'] != VERSION or prior['native_input']['sha256'] != native_record['sha256']:
            raise ValueError('Private difficulty successor has another native input')
        accepted.add(next(row['sha256'] for row in prior['outputs'] if row['path'] == rel(NATIVE)))
    if sha(NATIVE.read_bytes()) not in accepted:
        raise ValueError('Private dungeon runtime prefab has unrelated changes')
    native_guid = re.search(r'(?m)^guid: ([a-f0-9]{32})$', Path(str(NATIVE) + '.meta').read_text())[1]
    if native_guid != native_record['guid']: raise ValueError('Private dungeon native prefab GUID changed')
    apk = Apk.current()
    if apk.version != VERSION: raise ValueError('Wrong private difficulty official version')
    schemas = ScriptSchemas(code_root=CODE, assembly_overrides={'UnityEngine.UI': ugui})
    schemas.generator.load_dll(netstandard.read_bytes())
    prefab = apk.asset('prefabs_ui_dailydungeon', '/DailyDungeonDialogPrivateMap.prefab')
    nodes = list(relative_transform_path(prefab.read()))
    native_docs = documents(baseline.decode())
    roots = [row['m_GameObject']['fileID'] for kind, row in native_docs.values()
             if kind in ('Transform', 'RectTransform') and row['m_Father']['fileID'] == 0]
    native_root, = roots
    pointers, paths, records = {}, {}, []
    for go, indices, names, _ in nodes:
        go_id = native_root
        actual_names = [native_docs[go_id][1]['m_Name']]
        for index in indices:
            native_go = native_docs[go_id][1]
            transform, = [native_docs[pair['component']['fileID']][1] for pair in native_go['m_Component']
                          if native_docs[pair['component']['fileID']][0] in ('Transform', 'RectTransform')]
            child = native_docs[transform['m_Children'][index]['fileID']][1]
            go_id = child['m_GameObject']['fileID']; actual_names.append(native_docs[go_id][1]['m_Name'])
        if list(names) != actual_names: raise ValueError('Private difficulty native hierarchy changed')
        native_components = native_docs[go_id][1]['m_Component']
        if len(native_components) != len(go.m_Component): raise ValueError('Private difficulty component order changed')
        pointers[go.object_reader.path_id] = go_id
        paths[go.object_reader.path_id] = '/'.join(map(str, indices))
        for pair, native_pair in zip(go.m_Component, native_components):
            reader = pair.component.deref(); local = native_pair['component']['fileID']
            if reader.type.name != native_docs[local][0]: raise ValueError('Private difficulty component type changed')
            pointers[reader.path_id] = local
            if reader.type.name != 'MonoBehaviour': continue
            name = reader.read(check_read=False).m_Script.read().m_ClassName
            if name in PORTS or name == 'DailyDungeonDifficultyContent':
                records.append((name, reader, local, schemas.read(reader)))

    def convert(value, hierarchy=False):
        if isinstance(value, dict):
            if set(value) == {'m_FileID', 'm_PathID'}:
                if value['m_FileID']: raise ValueError('External private difficulty hierarchy reference')
                identity = value['m_PathID']
                if hierarchy:
                    if not identity: return None
                    obj = prefab.assets_file.objects[identity].read(check_read=False)
                    return paths[obj.m_GameObject.path_id if hasattr(obj, 'm_GameObject') else identity]
                return dict(fileID=pointers[identity] if identity else 0)
            return {key: convert(item, hierarchy) for key, item in value.items()}
        if isinstance(value, list): return [convert(item, hierarchy) for item in value]
        return value

    blocks = re.split(r'(?m)(?=^--- !u!)', baseline.decode())
    block_indices = {int(re.match(r'--- !u!\d+ &(-?\d+)', block)[1]): i
                     for i, block in enumerate(blocks) if block.startswith('--- !u!')}
    original_records, changed = [], {}
    for name, reader, local, fields in records:
        original_records.append(dict(type=name, path_id=reader.path_id, raw_sha256=sha(reader.get_raw_data())))
        if name not in PORTS: continue
        if name == 'TabItem' and fields['onValueChanged']['m_PersistentCalls']['m_Calls']:
            raise ValueError('Unbound original private difficulty tab callback')
        if name == 'TabGroup' and len(fields['mTabItems']) != 5:
            raise ValueError('Original private difficulty group size changed')
        script = ROOT / ('unity/Assets/Game/Runtime/Official/WorldUI/World' + name + '.cs')
        guid = re.search(r'(?m)^guid: ([a-f0-9]{32})$', Path(str(script) + '.meta').read_text())[1]
        native = dict(native_docs[local][1])
        for key, value in fields.items():
            if key not in ('m_GameObject', 'm_Script'): native[key] = convert(value)
        native['m_Script'] = dict(fileID=11500000, guid=guid, type=3)
        changed[local] = {key: convert(value) for key, value in fields.items() if key != 'm_Script'}
        blocks[block_indices[local]] = '--- !u!114 &' + str(local) + '\n' + yaml.dump(
            {'MonoBehaviour': plain(native)}, Dumper=Dumper, allow_unicode=True, sort_keys=False, width=10000)
    if len(changed) != 11: raise ValueError('Original private difficulty tab closure is incomplete')
    output = ''.join(blocks).encode()
    restored = documents(output.decode())
    # Preserve every unrelated object byte-for-byte, and check every migrated original field.
    for identity, expected in changed.items():
        actual = restored[identity][1]
        for key, value in expected.items():
            if actual[key] != value: raise ValueError('Private difficulty field recovery differs: ' + key)
    source, = [fields for name, _, _, fields in records if name == 'DailyDungeonDifficultyContent']
    refs = {row['rid']: row for row in source['references']['RefIds']}
    tabs = []
    for row in source['mDifficultyTabDatas']:
        config, = row['UnlockStateSwitcher']['mStateConfigs']
        ref = refs[config['rid']]
        if ref['type'] != dict(asm='Framework', ns='', **{'class': 'StateGameObjectConfig'}):
            raise ValueError('Unsupported private difficulty unlock-state config')
        states = [dict(state=entry['State']['mValue'], objects=convert(entry['GameObjects'], True))
                  for entry in ref['data']['mStateGameObjectData']['GoConfig']]
        tabs.append(dict(difficulty=row['Difficulty'], path=convert(row['TabItem'], True),
                         lockButton=convert(row['LockTipBtn'], True), unlockStates=states))
    template = ROOT / 'unity/Assets/Official/World/Map/12/Source/templates.json'
    stages = json.loads(template.read_bytes())['6103']['StageBehStageDailyDungeonComponent']['StageInfoList']
    dungeon_ids = {row['DungeonBattleId'] for row in stages}
    if dungeon_ids != {21}: raise ValueError('Original private difficulty display dungeon changed')
    binding = dict(manifest=VERSION, dungeonId=next(iter(dungeon_ids)),
                   group=convert(source['mDifficultyTabGroup'], True), tabs=tabs)
    binding_raw = encode(binding)
    write(NATIVE, output, update); write(BINDINGS, binding_raw, update)
    meta = Path(str(BINDINGS) + '.meta')
    guid = sha(b'zjcs-map12-private-difficulty-bindings:' + VERSION.encode())[:32]
    metadata = ('fileFormatVersion: 2\nguid: ' + guid + '\nTextScriptImporter:\n  externalObjects: {}\n'
                '  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode()
    if meta.exists() and meta.read_bytes() != metadata: raise ValueError('Private difficulty binding metadata changed')
    write(meta, metadata, update)
    code = ['client-source/DailyDungeonDifficultyContent.cs', 'client-source/DailyDungeonDialog.cs',
            'client-source/DailyDungeonUtils.cs', 'common-source/Common/DailyDungeonDifficulty.cs',
            'common-source/Common/LanguageUtils.cs'] + ['client-source/UnityEngine.UI.Ext/' + name + '.cs' for name in (*PORTS, 'TabTransition')]
    outputs = [dict(path=rel(path), sha256=sha(path.read_bytes()),
                    meta_sha256=sha(Path(str(path) + '.meta').read_bytes())) for path in (NATIVE, BINDINGS)]
    receipt = dict(manifest=VERSION, native_input=native_record, original_native_input=source_record,
        original_prefab=apk.provenance(prefab), original_components=original_records,
        code_sources=[dict(path=rel(CODE / path), sha256=sha((CODE / path).read_bytes())) for path in code],
        schema_adapters=[dict(path=str(path), sha256=sha(path.read_bytes())) for path in (ugui, netstandard)],
        reused_ports=[dict(path=rel(ROOT / ('unity/Assets/Game/Runtime/Official/WorldUI/World' + name + '.cs')),
                           canonical_lf_sha256=sha((ROOT / ('unity/Assets/Game/Runtime/Official/WorldUI/World' + name + '.cs')).read_text(encoding='utf8').encode())) for name in (*PORTS, 'TabTransition')],
        display_source=dict(path=rel(template), sha256=sha(template.read_bytes())),
        runtime_adapter=dict(path='unity/Assets/Game/Runtime/Presentation/World/OfficialDungeonPrivateDifficultyController.cs',
            canonical_lf_sha256=sha((ROOT / 'unity/Assets/Game/Runtime/Presentation/World/OfficialDungeonPrivateDifficultyController.cs').read_text(encoding='utf8').encode()),
            ported_methods=['InitContent (tab refresh/selection order)', 'RefreshTabItem', 'IsShowTabItem (selected display has no tab conditions)',
                            'OnDifficultyTabGroupValueChanged (detail callback)', 'OnClickLockTipBtn'],
            pending=['Original personal-best/ranking/red-dot refresh and reward preview are separate unfinished business controls.',
                     'Play Mode pointer, Animator, lock-tip rendering and unchanged-save checks have not run while automatic acceptance is paused.']),
        outputs=outputs, generator=dict(path=rel(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
        operation='Restore all original fields of five TabItems, their five TabAnimatorTransitions and the TabGroup; remap only script GUIDs and internal PPtrs by unchanged hierarchy/component order. Retain original native Animator controllers and clips. Export original difficulty/state/lock-button references for the local business adapter; runtime input/render acceptance remains pending.')
    write(RECEIPT, encode(receipt), update)
    print(json.dumps(dict(components=len(changed), tabs=len(tabs), outputs=outputs)))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--ugui', type=Path, default=ROOT / 'unity/Library/ScriptAssemblies/UnityEngine.UI.dll')
    parser.add_argument('--netstandard', type=Path, default=Path('D:/Tool/Unity/Editor/Data/NetStandard/ref/2.1.0/netstandard.dll'))
    parser.add_argument('--update', action='store_true')
    args = parser.parse_args(); build(args.ugui, args.netstandard, args.update)
