"""Bind the official report's horizontal clamp/header synchronization methods."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import sys
import uuid

ROOT = Path(__file__).resolve().parents[1]
RECEIPT = ROOT / 'assets/reference/battle-hud/report-scroll-source-manifest.json'
CODE = 'unity/Assets/Game/Runtime/Official/WorldUI/WorldBattleReportSkillScrollSync.cs'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def write(path, raw):
    temporary = path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp')
    temporary.write_bytes(raw)
    temporary.replace(path)


def build(source_root):
    sys.path.insert(0, str(source_root / 'scripts'))
    from official_apk import Apk
    from official_unity_schema import ScriptSchemas
    from refresh_current_ui_graphics import object_path
    from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
    import yaml
    class Dumper(yaml.SafeDumper):
        pass
    def mapping(dumper, value):
        return dumper.represent_mapping('tag:yaml.org,2002:map', value.items(),
                                        flow_style='fileID' in value)
    Dumper.add_representer(dict, mapping)
    apk = Apk(manifest='854_156383', reuse_current_cache=True)
    schemas = ScriptSchemas()
    schemas.generator = TypeTreeGenerator('2022.3.57f1', generator='AssetsTools')
    editor = Path('D:/Tool/Unity/Editor/Data')
    schemas.generator.load_local_dll_folder(str(editor / 'Managed/UnityEngine'))
    for path in sorted((source_root / '.tools/source-packages/official-code/managed/decoded').glob('*.dll')):
        if not path.stem.startswith('UnityEngine') and path.stem != 'Unity.TextMeshPro':
            schemas.generator.load_dll(path.read_bytes())
    for path in [ROOT / ('unity/Library/ScriptAssemblies/' + name + '.dll') for name in ['UnityEngine.UI', 'Unity.TextMeshPro']] + [
            editor / 'NetStandard/ref/2.1.0/netstandard.dll', editor / 'MonoBleedingEdge/lib/mono/4.5/mscorlib.dll']:
        schemas.generator.load_dll(path.read_bytes())
    code = ROOT / CODE
    meta = code.with_name(code.name + '.meta')
    if not meta.exists():
        key = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs/official-world-ui/' + code.name).hex
        write(meta, ('fileFormatVersion: 2\nguid: ' + key + '\nMonoImporter:\n  externalObjects: {}\n'
                     '  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n'
                     '  icon: {instanceID: 0}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode())
    script_guid = re.search(r'(?m)^guid: (\w+)', meta.read_text())[1]
    foundation = json.loads((ROOT / 'assets/reference/battle-hud/report-foundation-source-manifest.json').read_bytes())
    prior = json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else None
    bindings, outputs, inputs = [], [], []
    for entry in foundation['outputs']:
        path = ROOT / entry['path']
        raw = path.read_bytes().replace(b'\r\n', b'\n')
        allowed = {entry['sha256']}
        if prior:
            allowed.update(row['sha256'] for row in prior['outputs'] if row['path'] == entry['path'])
        if sha(raw) not in allowed:
            raise ValueError('Unexpected prefab modification: ' + str(path))
        blocks = re.split(r'(?m)^--- ', raw.decode())
        docs = {}
        for index, block in enumerate(blocks[1:], 1):
            match = re.match(r'!u!(\d+) &(-?\d+)\n', block)
            docs[int(match[2])] = (index, int(match[1]), next(iter(yaml.safe_load(block[match.end():]).values())))
        bundle = 'defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_' + path.stem.lower() + '.bundle'
        objects = list(apk.objects(bundle))
        # Existing strict foundation bindings identify the original local ScrollRect/LoopScroll objects.
        local = {int(row['original_path_id']): int(row['file_id']) for row in foundation['bindings'] if row['prefab'] == entry['path']}
        for obj in objects:
            if obj.type.name != 'MonoBehaviour':
                continue
            basic = obj.read(check_read=False)
            if basic.m_Script.read().m_ClassName != 'FightEndReportSkillTabView':
                continue
            data = schemas.read(obj)
            hierarchy = object_path(basic.m_GameObject.deref())[0]
            # Find its native component by the unchanged component slot on the native owner GameObject.
            owner = basic.m_GameObject.read()
            slot = next(index for index, item in enumerate(owner.m_Component) if item.component.path_id == obj.path_id)
            # Both owners are located by their complete, duplicate-aware hierarchy paths.
            parents = {value['m_GameObject']['fileID']: value['m_Father']['fileID'] for _, kind, value in docs.values() if kind == 224}
            def native_path(go):
                value = docs[go][2]
                parent = parents[go]
                repeated = 0
                if parent:
                    for child in docs[parent][2]['m_Children']:
                        child_go = docs[child['fileID']][2]['m_GameObject']['fileID']
                        if child_go == go:
                            break
                        repeated += docs[child_go][2]['m_Name'] == value['m_Name']
                return (native_path(docs[parent][2]['m_GameObject']['fileID']) if parent else '') + '/' + value['m_Name'] + (f' ({repeated})' if repeated else '')
            native_go = next(go for go, (_, kind, _) in docs.items() if kind == 1 and native_path(go) == hierarchy)
            components = docs[native_go][2]['m_Component']
            if len(components) != len(owner.m_Component):
                raise ValueError('Original report component order changed: ' + hierarchy)
            file_id = components[slot]['component']['fileID']
            index, kind, native = docs[file_id]
            assert kind == 114
            fields = {key: {'fileID': local[data[key]['m_PathID']]} for key in ['mTitleScrollRect', 'mItemScrollRect']}
            native['m_Script'] = dict(fileID=11500000, guid=script_guid, type=3)
            native.update(fields)
            blocks[index] = '!u!114 &' + str(file_id) + '\n' + yaml.dump({'MonoBehaviour': native}, Dumper=Dumper, allow_unicode=True, sort_keys=False, width=10000)
            bindings.append(dict(prefab=entry['path'], path=hierarchy, file_id=str(file_id), original_path_id=str(obj.path_id),
                                 object_sha256=sha(obj.get_raw_data()), fields=fields))
            inputs.append(apk.provenance(obj))
        result = '--- '.join(blocks).encode()
        if result != raw:
            write(path, result)
        outputs.append(dict(path=entry['path'], sha256=sha(result), bytes=len(result)))
    original = source_root / '.tools/source-packages/official-code/client-source/FightEndReportSkillTabView.cs'
    receipt = dict(format=1, manifest=apk.version, generator_sha256=sha(Path(__file__).read_bytes()), inputs=inputs,
                   original=dict(path=original.relative_to(source_root).as_posix(), sha256=sha(original.read_bytes())),
                   bindings=bindings, outputs=outputs,
                   methods=['OnItemScrollRectValueChanged', 'ClampItemHorizontalAnchorX', 'SetTitleHorizontalNormalizedPosition', 'SyncTitleByItemAnchoredX'],
                   adaptations=['The offline Initialize/OnDisable adapter replaces the UIBase initialization gate for the extracted scrolling methods only.',
                                'RefreshLayout preserves the original forced canvas/layout rebuild order. Report providers and data initialization are not included.'])
    write(RECEIPT, (json.dumps(receipt, ensure_ascii=False, indent=2) + '\n').encode())
    record_runtime()
    print('Bound original report scroll synchronization:', len(bindings))


def record_runtime():
    data = json.loads(RECEIPT.read_bytes())
    dropdown_path = ROOT / 'assets/reference/battle-hud/report-dropdown-source-manifest.json'
    dropdown = json.loads(dropdown_path.read_bytes()) if dropdown_path.exists() else None
    for entry in data['outputs']:
        allowed = {entry['sha256']}
        if dropdown:
            allowed.update(row['sha256'] for row in dropdown['outputs'] if row['path'] == entry['path'])
        if sha((ROOT / entry['path']).read_bytes().replace(b'\r\n', b'\n')) not in allowed:
            raise ValueError('Report scroll prefab changed: ' + entry['path'])
    data['generator_sha256'] = sha(Path(__file__).read_bytes())
    data['runtime'] = {path: sha((ROOT / path).read_bytes()) for path in [CODE, CODE + '.meta',
        'unity/Assets/Game/Editor/Combat/OfficialBattleReportFoundationAcceptance.cs',
        'unity/Assets/Game/Editor/OfficialMap10BattlePlayModeAcceptance.cs']}
    write(RECEIPT, (json.dumps(data, ensure_ascii=False, indent=2) + '\n').encode())


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, default=ROOT)
    parser.add_argument('--record-runtime', action='store_true')
    args = parser.parse_args()
    record_runtime() if args.record_runtime else build(args.source_root)
