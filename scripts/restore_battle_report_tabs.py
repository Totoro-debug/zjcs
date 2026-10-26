"""Restore original report tabs, their standard layout/SafeArea dependencies and graphic hit-test flags."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import sys
import uuid


ROOT = Path(__file__).resolve().parents[1]
TAB_CLASSES = ('TabItem', 'TabGroup', 'TabAnimatorTransition')
LAYOUT_CLASSES = ('HorizontalLayoutGroup', 'VerticalLayoutGroup', 'ContentSizeFitter', 'LayoutElement', 'RectMask2D', 'GraphicRaycaster')
SCRIPT_PATH = 'unity/Assets/Game/Runtime/Official/WorldUI/'
RECEIPT = 'assets/reference/battle-hud/report-tabs-source-manifest.json'


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def write(path, raw):
    path.parent.mkdir(parents=True, exist_ok=True)
    temp = path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp')
    temp.write_bytes(raw)
    temp.replace(path)


def metadata(path):
    meta = path.with_name(path.name + '.meta')
    if not meta.exists():
        guid = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs/official-world-ui/' + path.name).hex
        write(meta, ('fileFormatVersion: 2\nguid: ' + guid + '\nMonoImporter:\n'
                    '  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n'
                    '  executionOrder: 0\n  icon: {instanceID: 0}\n  userData:\n'
                    '  assetBundleName:\n  assetBundleVariant:\n').encode())
    return re.search(r'(?m)^guid: (\w+)$', meta.read_text(encoding='utf8'))[1]


def build(source_root, ugui, netstandard):
    sys.path.insert(0, str(source_root / 'scripts'))
    from official_apk import Apk
    from official_unity_schema import ScriptSchemas
    from refresh_current_ui_graphics import object_path
    from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
    import yaml
    class UnityDumper(yaml.SafeDumper):
        pass
    def mapping(dumper, value):
        flow = 'fileID' in value and set(value) <= {'fileID', 'guid', 'type'}
        return dumper.represent_mapping('tag:yaml.org,2002:map', value.items(), flow_style=flow)
    UnityDumper.add_representer(dict, mapping)
    apk = Apk(manifest='854_156383', reuse_current_cache=True)
    schemas = ScriptSchemas(assembly_overrides={'UnityEngine.UI': ugui})
    schemas.generator.load_dll(netstandard.read_bytes())
    tmp = ScriptSchemas()
    tmp.generator = TypeTreeGenerator('2022.3.57f1', generator='AssetsTools')
    tmp.generator.load_local_dll_folder(str(netstandard.parents[3] / 'Managed/UnityEngine'))
    tmp.generator.load_local_dll_folder(str(ugui.parent))
    tmp.generator.load_dll(netstandard.read_bytes())
    tmp.generator.load_dll((netstandard.parents[3] / 'MonoBleedingEdge/lib/mono/4.5/mscorlib.dll').read_bytes())
    prior_path = ROOT / RECEIPT
    prior = json.loads(prior_path.read_bytes()) if prior_path.exists() else None
    baseline = json.loads((ROOT / 'assets/reference/battle-hud/report-panels-source-manifest.json').read_bytes())
    by_name = {Path(row['path']).name: row for row in baseline['runtime_prefabs']}
    guids = {name: metadata(ROOT / SCRIPT_PATH / ('World' + name + '.cs')) for name in TAB_CLASSES}
    guids['NoDrawingRayCast'] = re.search(r'(?m)^guid: (\w+)$', (ROOT / SCRIPT_PATH / 'NoDrawingRayCast.cs.meta').read_text(encoding='utf8'))[1]
    guids['SafeArea'] = re.search(r'(?m)^guid: (\w+)$', (ROOT / SCRIPT_PATH / 'WorldSafeArea.cs.meta').read_text(encoding='utf8'))[1]
    metadata(ROOT / SCRIPT_PATH / 'WorldTabTransition.cs')
    outputs, inputs, bindings = [], [], []
    for name, record in by_name.items():
        key = Path(name).stem.lower()
        bundle = 'defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_' + key + '.bundle'
        objects = list(apk.objects(bundle))
        game_objects = [obj for obj in objects if obj.type.name == 'GameObject']
        target = ROOT / record['path']
        raw = target.read_bytes()
        expected = next((r['sha256'] for r in prior['outputs'] if r['path'] == record['path']), None) if prior else None
        if digest(raw) not in {record['sha256'], expected} and digest(raw.replace(b'\r\n', b'\n')) not in {record['sha256'], expected}:
            raise ValueError('Changed runtime report prefab: ' + str(target))
        # Always rebuild from the unmodified native export, with the existing UGUI/TMP adaptation.
        export = source_root / '.tools/assetripper-1.3.14/temp/battle-report-ui-ripper/ExportedProject/Assets' / record['source']
        from prepare_unity_battle_report_assets import SCRIPT_GUID_MAPPINGS
        text = export.read_text(encoding='utf8')
        if digest(text.encode()) != record['source_sha256']:
            raise ValueError('Original report prefab hash differs: ' + str(export))
        for old, new in SCRIPT_GUID_MAPPINGS.items():
            text = text.replace('guid: ' + old + ', type: 3', 'guid: ' + new + ', type: 3')
        blocks = re.split(r'(?m)^--- ', text)
        documents = {}
        for index, block in enumerate(blocks[1:], 1):
            match = re.match(r'!u!(\d+) &(-?\d+)\n', block)
            documents[int(match[2])] = (index, int(match[1]), next(iter(yaml.safe_load(block[match.end():]).values())))
        parents = {}
        for _, (index, kind, data) in documents.items():
            if kind == 224:
                parents[data['m_GameObject']['fileID']] = data['m_Father']['fileID']
        paths = {}
        def native_path(go):
            if go in paths:
                return paths[go]
            data = documents[go][2]
            parent_transform = parents[go]
            parent_go = documents[parent_transform][2]['m_GameObject']['fileID'] if parent_transform else 0
            name = data['m_Name']
            sibling_index = 0
            if parent_transform:
                for child in documents[parent_transform][2]['m_Children']:
                    child_go = documents[child['fileID']][2]['m_GameObject']['fileID']
                    if child_go == go:
                        break
                    if documents[child_go][2]['m_Name'] == name:
                        sibling_index += 1
            paths[go] = (native_path(parent_go) if parent_go else '') + '/' + name + (f' ({sibling_index})' if sibling_index else '')
            return paths[go]
        go_by_path = {native_path(go): go for go, (_, kind, _) in documents.items() if kind == 1}
        mapping = {}
        for obj in game_objects:
            path = object_path(obj)[0]
            go = go_by_path.get(path)
            if go is None:
                raise ValueError('Original hierarchy path was lost: ' + path)
            data = obj.read()
            components = documents[go][2]['m_Component']
            if len(data.m_Component) != len(components):
                raise ValueError('Original component order differs: ' + path)
            mapping[obj.path_id] = go
            for original, component in zip(data.m_Component, components):
                local_id = component['component']['fileID']
                if original.component.type.value != documents[local_id][1]:
                    raise ValueError('Original component type differs: ' + path)
                mapping[original.component.path_id] = local_id
        def convert(value):
            if isinstance(value, dict):
                if set(value) == {'m_FileID', 'm_PathID'}:
                    if value['m_FileID']:
                        raise ValueError('External tab field requires explicit asset conversion')
                    return {'fileID': mapping[value['m_PathID']] if value['m_PathID'] else 0}
                return {k: convert(v) for k, v in value.items()}
            if isinstance(value, list):
                return [convert(v) for v in value]
            return value
        restored = 0
        for obj in objects:
            if obj.type.name != 'MonoBehaviour':
                continue
            basic = obj.read(check_read=False)
            script = basic.m_Script.read()
            name = script.m_ClassName
            if name not in (*TAB_CLASSES, *LAYOUT_CLASSES, 'SafeArea', 'NoDrawingRayCast', 'Image', 'MirrorImage', 'TextMeshProUGUI'):
                continue
            data = (tmp if name == 'TextMeshProUGUI' or name in LAYOUT_CLASSES else schemas).read(obj)
            local_id = mapping[obj.path_id]
            index, _, native = documents[local_id]
            if name in ('Image', 'MirrorImage', 'TextMeshProUGUI'):
                # Their native image/font/material restoration is a separate, unfinished step.
                # Restore only hit-test flags here so placeholder defaults cannot intercept tab input.
                fields = {k: data[k] for k in ('m_RaycastTarget', 'm_RaycastPadding', 'm_Maskable')}
            else:
                fields = {k: convert(v) for k, v in data.items() if k not in {'m_GameObject', 'm_Enabled', 'm_Script', 'm_Name'}}
                if name in guids:
                    native['m_Script'] = {'fileID': 11500000, 'guid': guids[name], 'type': 3}
            native.update(fields)
            blocks[index] = '!u!114 &' + str(local_id) + '\n' + yaml.dump({'MonoBehaviour': native}, Dumper=UnityDumper, allow_unicode=True, sort_keys=False, width=10000)
            bindings.append({'prefab': record['path'], 'path': object_path(basic.m_GameObject.deref())[0],
                             'class': name, 'original_path_id': str(obj.path_id),
                             'object_sha256': digest(obj.get_raw_data()), 'file_id': str(local_id), 'fields': fields})
            inputs.append(apk.provenance(obj))
            restored += 1
        if restored:
            result = '--- '.join(blocks).encode()
            write(target, result)
            outputs.append({'path': record['path'], 'sha256': digest(result), 'bytes': len(result),
                            'previous_sha256': record['sha256'], 'restored_components': restored})
    code = []
    for name in (*TAB_CLASSES, 'TabTransition'):
        original = source_root / '.tools/source-packages/official-code/client-source/UnityEngine.UI.Ext' / (name + '.cs')
        migrated = ROOT / SCRIPT_PATH / ('World' + name + '.cs')
        code.append({'original': original.relative_to(source_root).as_posix(), 'original_sha256': digest(original.read_bytes()),
                     'path': migrated.relative_to(ROOT).as_posix(), 'sha256': digest(migrated.read_bytes()),
                     'meta_sha256': digest(migrated.with_name(migrated.name + '.meta').read_bytes())})
    for name, migrated_name in [('NoDrawingRayCast', 'NoDrawingRayCast'), ('SafeArea', 'WorldSafeArea')]:
        original = source_root / '.tools/source-packages/official-code/framework-firstpass-source' / (name + '.cs')
        migrated = ROOT / SCRIPT_PATH / (migrated_name + '.cs')
        code.append({'original': original.relative_to(source_root).as_posix(), 'original_sha256': digest(original.read_bytes()),
                     'path': migrated.relative_to(ROOT).as_posix(), 'canonical_lf_sha256': digest(migrated.read_bytes().replace(b'\r\n', b'\n')),
                     'canonical_meta_lf_sha256': digest(migrated.with_name(migrated.name + '.meta').read_bytes().replace(b'\r\n', b'\n')), 'reused': True})
    receipt = {'format': 1, 'manifest': apk.version, 'operation': __doc__, 'inputs': inputs,
               'schema': {'ugui_sha256': digest(ugui.read_bytes()), 'netstandard_sha256': digest(netstandard.read_bytes())},
               'generator_sha256': digest(Path(__file__).read_bytes()), 'bindings': bindings, 'outputs': outputs, 'code': code,
               'runtime': {path: digest((ROOT / path).read_bytes()) for path in [
                   'unity/Assets/Game/Editor/Combat/OfficialBattleReportTabAcceptance.cs',
                   'unity/Assets/Game/Editor/OfficialMap10BattlePlayModeAcceptance.cs']},
               'extraction_tools': {('scripts/' + name): digest((source_root / 'scripts' / name).read_bytes()) for name in [
                   'official_apk.py', 'official_unity_schema.py', 'refresh_current_ui_graphics.py',
                   'prepare_unity_battle_report_assets.py']},
               'adaptations': ['Port namespace/class names to WorldTab*, keep original serialized fields and notification order.',
                               'Replace original structured logger in hook exceptions with Unity Debug.LogException.',
                               'Map original local PPtrs by unchanged hierarchy and component order; preserve native Animator controllers.'],
               'pending': ['Report Image/TMP/custom layout fields were also omitted by the old export. Image/TMP restoration here covers hit-test flags only; original visuals, custom components, data rows and production entry remain unfinished.']}
    write(prior_path, (json.dumps(receipt, ensure_ascii=False, indent=2) + '\n').encode())
    return receipt


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, default=ROOT)
    parser.add_argument('--ugui', type=Path, default=ROOT / 'unity/Library/ScriptAssemblies/UnityEngine.UI.dll')
    parser.add_argument('--netstandard', type=Path, default=Path('D:/Tool/Unity/Editor/Data/NetStandard/ref/2.1.0/netstandard.dll'))
    args = parser.parse_args()
    result = build(args.source_root, args.ugui, args.netstandard)
    print(json.dumps({'prefabs': len(result['outputs']), 'components': len(result['bindings'])}))
