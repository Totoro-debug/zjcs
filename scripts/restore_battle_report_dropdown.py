"""Recover the original report UIDropdown and its common DropdownItem dependency."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import struct
import sys
import uuid

ROOT = Path(__file__).resolve().parents[1]
WORLD = 'unity/Assets/Game/Runtime/Official/WorldUI/'
RECEIPT = ROOT / 'assets/reference/battle-hud/report-dropdown-source-manifest.json'
RESOURCE = 'unity/Assets/Game/Resources/Combat/BattleReport/'
NAMES = ['WorldStateSwitcher', 'WorldMultiLanguageText', 'WorldDropdownItem', 'WorldUIDropdown']


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def write(path, raw):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp')
    temporary.write_bytes(raw)
    temporary.replace(path)


def guid(path):
    return re.search(r'(?m)^guid: (\w+)', path.with_name(path.name + '.meta').read_text())[1]


def metadata(path, importer):
    meta = path.with_name(path.name + '.meta')
    if meta.exists():
        return
    key = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs/report-dropdown/' + path.relative_to(ROOT).as_posix()).hex
    fields = '  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n' if importer == 'MonoImporter' else ''
    write(meta, ('fileFormatVersion: 2\nguid: ' + key + '\n' + importer + ':\n  externalObjects: {}\n' + fields
                 + '  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode())


def build(source_root):
    sys.path.insert(0, str(source_root / 'scripts'))
    from official_apk import Apk
    from official_unity_schema import ScriptSchemas
    from refresh_current_ui_graphics import object_path
    from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
    from UnityPy.classes.PPtr import PPtr
    from prepare_unity_battle_report_assets import SCRIPT_GUID_MAPPINGS
    import yaml
    class Dumper(yaml.SafeDumper):
        pass
    def mapping(dumper, value):
        return dumper.represent_mapping('tag:yaml.org,2002:map', value.items(), flow_style='fileID' in value)
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
    for name in NAMES:
        metadata(ROOT / WORLD / (name + '.cs'), 'MonoImporter')
    scripts = {name[5:]: ROOT / WORLD / (name + '.cs') for name in NAMES[1:]}
    scripts.update({name: ROOT / WORLD / ('World' + name + '.cs') for name in ['LayoutElementClamper']})
    scripts['NoDrawingRayCast'] = ROOT / WORLD / 'NoDrawingRayCast.cs'
    scripts['LoopVerticalScrollRect'] = ROOT / WORLD / 'LoopScroll/LoopVerticalScrollRect.cs'
    core = ROOT / 'unity/Library/PackageCache/com.unity.ugui@1.0.0/Runtime/UI/Core'
    scripts.update({name: core / (name + '.cs') for name in ['Button', 'Image']})
    scripts.update({name: core / 'Layout' / (name + '.cs') for name in ['LayoutElement', 'HorizontalLayoutGroup']})
    tmp_scripts = list((ROOT / 'unity/Library/PackageCache').glob('com.unity.textmeshpro@*/Scripts/Runtime/TextMeshProUGUI.cs'))
    if len(tmp_scripts) != 1:
        raise ValueError('Expected exactly one installed TMP package.')
    scripts['TextMeshProUGUI'] = tmp_scripts[0]
    export = source_root / '.tools/assetripper-1.3.14/temp/battle-report-ui-ripper/ExportedProject/Assets'
    source = export / 'Prefabs/UI/Common/Dropdown/DropdownItem.prefab'
    dependency = ROOT / RESOURCE / 'DropdownItem.prefab'
    dependency_raw = source.read_bytes().replace(b'\r\n', b'\n')
    for old, new in SCRIPT_GUID_MAPPINGS.items():
        dependency_raw = dependency_raw.replace(('guid: ' + old + ', type: 3').encode(), ('guid: ' + new + ', type: 3').encode())
    metadata(dependency, 'PrefabImporter')
    scroll = json.loads((ROOT / 'assets/reference/battle-hud/report-scroll-source-manifest.json').read_bytes())
    prior = json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else None
    panels = []
    records = [dict(path=RESOURCE + 'FightEndAllPlayerReportPanel.prefab', bundle='defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_fightendallplayerreportpanel.bundle'),
               dict(path=RESOURCE + 'DropdownItem.prefab', bundle='defaultpackage_assets_prefabs_ui_common.bundle', root_id=3171787996812696172)]
    for entry in records:
        path = ROOT / entry['path']
        raw = path.read_bytes().replace(b'\r\n', b'\n') if path.exists() else dependency_raw
        expected = {sha(dependency_raw)} if entry.get('root_id') else {row['sha256'] for row in scroll['outputs'] if row['path'] == entry['path']}
        if prior:
            expected.update(row['sha256'] for row in prior['outputs'] if row['path'] == entry['path'])
        if sha(raw) not in expected:
            raise ValueError('Unexpected dropdown prefab modification: ' + entry['path'])
        blocks = re.split(r'(?m)^--- ', raw.decode())
        docs = {}
        for index, block in enumerate(blocks[1:], 1):
            match = re.match(r'!u!(\d+) &(-?\d+)\n', block)
            docs[int(match[2])] = (index, int(match[1]), next(iter(yaml.safe_load(block[match.end():]).values())))
        objects = list(apk.objects(entry['bundle']))
        by_id = {obj.path_id: obj for obj in objects}
        root = by_id[entry['root_id']] if entry.get('root_id') else next(obj for obj in objects if obj.type.name == 'GameObject' and obj.read().m_Name == 'FightEndAllPlayerReportPanel')
        originals = []
        def walk(go):
            originals.append(go)
            for item in go.read().m_Component:
                component = item.component.deref()
                originals.append(component)
                if component.type.name == 'RectTransform':
                    for child in component.read().m_Children:
                        walk(child.read().m_GameObject.deref())
        walk(root)
        parents = {value['m_GameObject']['fileID']: value['m_Father']['fileID'] for _, kind, value in docs.values() if kind == 224}
        def native_path(go):
            data = docs[go][2]
            parent = parents[go]
            repeated = 0
            if parent:
                for child in docs[parent][2]['m_Children']:
                    child_go = docs[child['fileID']][2]['m_GameObject']['fileID']
                    if child_go == go:
                        break
                    repeated += docs[child_go][2]['m_Name'] == data['m_Name']
            return (native_path(docs[parent][2]['m_GameObject']['fileID']) if parent else '') + '/' + data['m_Name'] + (f' ({repeated})' if repeated else '')
        native_go = {native_path(go): go for go, (_, kind, _) in docs.items() if kind == 1}
        local = {}
        for obj in originals:
            if obj.type.name != 'GameObject':
                continue
            go = native_go[object_path(obj)[0]]
            old = obj.read().m_Component
            new = docs[go][2]['m_Component']
            if len(old) != len(new):
                raise ValueError('Original dropdown component order changed.')
            local[obj.path_id] = go
            for source_component, native_component in zip(old, new):
                target_id = native_component['component']['fileID']
                if source_component.component.type.value != docs[target_id][1]:
                    raise ValueError('Original dropdown component type changed.')
                local[source_component.component.path_id] = target_id
        panels.append(dict(path=path, entry=entry, raw=raw, blocks=blocks, docs=docs, local=local, objects=originals))
    inputs, bindings, pending, outputs, graphics, geometry, animation_sources = [], [], [], [], {}, [], {}
    for panel in panels:
        def convert(value, owner):
            if isinstance(value, list):
                return [convert(item, owner) for item in value]
            if isinstance(value, dict):
                if set(value) == {'m_FileID', 'm_PathID'}:
                    if not value['m_PathID']:
                        return {'fileID': 0}
                    target = PPtr(**value, assetsfile=owner.assets_file).deref()
                    if value['m_FileID'] == 0:
                        return dict(fileID=panel['local'][target.path_id])
                    for other in panels:
                        if target.path_id in other['local'] and target.assets_file.name == other['objects'][0].assets_file.name:
                            return dict(fileID=other['local'][target.path_id], guid=guid(other['path']), type=3)
                    kind = target.type.name
                    if kind == 'MonoBehaviour':
                        kind = 'Font' if target.read(check_read=False).m_Script.read().m_ClassName == 'TMP_FontAsset' else kind
                    stem = {'Sprite': 'Sprite', 'Material': 'Material', 'Font': 'Font'}.get(kind)
                    if stem is None:
                        raise ValueError('Unconverted dropdown dependency: ' + target.type.name)
                    suffix = '.mat' if kind == 'Material' else '.asset'
                    filename = stem + '_' + sha(target.get_raw_data())[:16] + suffix
                    candidates = [ROOT / 'unity/Assets/Official/Combat/BattleReport/Graphics' / filename,
                                  ROOT / 'unity/Assets/Official/Combat/ElementFeedback/Resources/Combat/ElementFeedback' / filename]
                    asset = next((path for path in candidates if path.exists()), None)
                    if asset is None:
                        raise ValueError('Original dropdown graphic not converted: ' + filename + ' (' + target.read().m_Name + ')')
                    graphics[asset.relative_to(ROOT).as_posix()] = dict(sha256=sha(asset.read_bytes()), meta_sha256=sha(asset.with_name(asset.name + '.meta').read_bytes()),
                                                                       original_object_sha256=sha(target.get_raw_data()), source=apk.provenance(target))
                    return dict(fileID={'Sprite': 21300000, 'Material': 2100000, 'Font': 11400000}[kind], guid=guid(asset), type=2)
                return {key: convert(item, owner) for key, item in value.items()}
            return value
        for obj in panel['objects']:
            if obj.type.name == 'RectTransform' and panel['entry'].get('root_id'):
                keys = ['m_LocalRotation', 'm_LocalPosition', 'm_LocalScale', 'm_Children', 'm_Father',
                        'm_AnchorMin', 'm_AnchorMax', 'm_AnchoredPosition', 'm_SizeDelta', 'm_Pivot']
                original = obj.read_typetree()
                fields = {key: convert(original[key], obj) for key in keys}
                native = panel['docs'][panel['local'][obj.path_id]][2]
                def equal(expected, actual):
                    if isinstance(expected, dict):
                        return all(key in actual and equal(value, actual[key]) for key, value in expected.items())
                    if isinstance(expected, list):
                        return len(expected) == len(actual) and all(equal(a, b) for a, b in zip(expected, actual))
                    if isinstance(expected, float):
                        return struct.pack('<f', expected) == struct.pack('<f', actual)
                    return expected == actual
                if not all(equal(value, native[key]) for key, value in fields.items()):
                    raise ValueError('Original common DropdownItem transform differs from its native export: ' + str(obj.path_id))
                geometry.append(dict(prefab=panel['entry']['path'], file_id=str(panel['local'][obj.path_id]), original_path_id=str(obj.path_id),
                                     object_sha256=sha(obj.get_raw_data()), fields=fields, source=apk.provenance(obj)))
            if obj.type.name != 'MonoBehaviour':
                continue
            basic = obj.read(check_read=False)
            name = basic.m_Script.read().m_ClassName
            hierarchy = object_path(basic.m_GameObject.deref())[0]
            selected = panel['entry'].get('root_id') or '/SelectPlayer/' in hierarchy + '/' and name in ['UIDropdown', 'DropdownItem', 'MultiLanguageText', 'LoopVerticalScrollRect']
            if not selected:
                continue
            if name not in scripts:
                pending.append(dict(prefab=panel['entry']['path'], path=hierarchy, kind=name, original_path_id=str(obj.path_id),
                                    object_sha256=sha(obj.get_raw_data()), source=apk.provenance(obj)))
                continue
            data = schemas.read(obj)
            if name == 'UIDropdown':
                animator = PPtr(**data['Animator'], assetsfile=obj.assets_file).deref().read()
                controller = animator.m_Controller.deref()
                for target in [controller] + [clip.deref() for clip in controller.read().m_AnimationClips]:
                    key = target.assets_file.name + ':' + str(target.path_id)
                    animation_sources[key] = dict(source=apk.provenance(target), object_sha256=sha(target.get_raw_data()),
                                                  kind=target.type.name, name=target.read().m_Name)
            fields = {key: convert(value, obj) for key, value in data.items() if key not in ['m_GameObject', 'm_Enabled', 'm_Script', 'm_Name']}
            if 'references' in fields:
                for reference in fields['references']['RefIds']:
                    old = reference['type']
                    if old['class'] not in ['StateGameObjectConfig', 'StateUIColorConfig']:
                        raise ValueError('Unported managed state configuration: ' + old['class'])
                    reference['type'] = dict(old, **{'class': 'World' + old['class'], 'ns': 'Zjcs.Official.WorldUI', 'asm': 'Zjcs.Official.WorldUI'})
            file_id = panel['local'][obj.path_id]
            index, _, native = panel['docs'][file_id]
            native['m_Script'] = dict(fileID=11500000, guid=guid(scripts[name]), type=3)
            native = {key: value for key, value in native.items() if key not in fields}
            native.update(fields)
            panel['blocks'][index] = '!u!114 &' + str(file_id) + '\n' + yaml.dump({'MonoBehaviour': native}, Dumper=Dumper, allow_unicode=True, sort_keys=False, width=10000)
            references = {row['rid']: row for row in fields.get('references', {}).get('RefIds', [])}
            checks = []
            def check(value, key):
                if isinstance(value, dict):
                    if 'fileID' in value:
                        checks.append(dict(path=key, kind='reference', reference=value))
                    elif set(value) == {'rid'}:
                        reference = references[value['rid']]
                        type_info = reference['type']
                        checks.append(dict(path=key, kind='managed', text=type_info['asm'] + ' ' + type_info['ns'] + '.' + type_info['class']))
                        check(reference['data'], key)
                    else:
                        for child, item in value.items():
                            check(item, key + '.' + child)
                elif isinstance(value, list):
                    checks.append(dict(path=key, kind='array', count=len(value)))
                    for index, item in enumerate(value):
                        check(item, key + '.Array.data[' + str(index) + ']')
                elif isinstance(value, str):
                    checks.append(dict(path=key, kind='string', text=value))
                else:
                    checks.append(dict(path=key, kind='number', number=value))
            for key, value in fields.items():
                if key != 'references':
                    check(value, key)
            bindings.append(dict(prefab=panel['entry']['path'], path=hierarchy, kind=name, file_id=str(file_id),
                                 original_path_id=str(obj.path_id), object_sha256=sha(obj.get_raw_data()), fields=fields, checks=checks))
            inputs.append(apk.provenance(obj))
        panel['result'] = '--- '.join(panel['blocks']).encode()
        outputs.append(dict(path=panel['entry']['path'], sha256=sha(panel['result']), bytes=len(panel['result'])))
    for panel in panels:
        write(panel['path'], panel['result'])
    code = {}
    source_paths = ['client-source/' + name + '.cs' for name in ['UIDropdown', 'DropdownItem']]
    source_paths += ['framework-source/' + name + '.cs' for name in ['StateSwitcher', 'StateConfigValue', 'StateGameObjectConfig', 'StateGameObjectData', 'StateUIColorConfig', 'StateUIColorData', 'StateKVMatch', 'IStateConfig']]
    source_paths += ['framework-firstpass-source/' + name + '.cs' for name in ['MultiLanguageText', 'MultiLanguageUI', 'MonoKeyValuePair', 'Common/ITextProcessor', 'Framework/LocalizationTextService', 'MultiLanguageFontService']]
    for name in source_paths:
        path = source_root / '.tools/source-packages/official-code' / name
        code[path.relative_to(source_root).as_posix()] = sha(path.read_bytes())
    baseline = json.loads((ROOT / 'assets/reference/battle-hud/report-panels-source-manifest.json').read_bytes())
    reused_animations = []
    for entry in baseline['outputs']:
        if Path(entry['path']).stem in ['UIDropDownController', 'UIDropDown_Hide', 'UIDropDown_Idle', 'UIDropDown_Show']:
            path = ROOT / entry['path']
            if sha(path.read_bytes().replace(b'\r\n', b'\n')) != entry['sha256']:
                raise ValueError('Previously migrated original dropdown animation changed: ' + entry['path'])
            reused_animations.append(dict(entry, comparison='canonical LF, matching the committed native export'))
    if len(reused_animations) != 4 or len(animation_sources) != 4:
        raise ValueError('Original dropdown animation dependency set changed.')
    receipt = dict(format=1, manifest=apk.version, generator_sha256=sha(Path(__file__).read_bytes()), inputs=inputs, official_code=code,
                   prefab_export=dict(path=source.relative_to(source_root).as_posix(), sha256=sha(source.read_bytes()), canonical_lf_sha256=sha(source.read_bytes().replace(b'\r\n', b'\n'))),
                   bindings=bindings, outputs=outputs, graphics=graphics, geometry=geometry,
                   animation_sources=animation_sources, reused_animations=reused_animations, pending_components=pending,
                   adaptations=['Port namespaces and diagnostic logging; retain dropdown lifecycle, item callbacks, serialized fields, state configuration order and original common prefab hierarchy.',
                                'Translate managed-reference type names to the local assembly while preserving IDs, field data and object references.',
                                'The offline input host supplies global pointer events. Localization/font delegates replace injection; the full language/effect services and production report host remain pending.'])
    write(RECEIPT, (json.dumps(receipt, ensure_ascii=False, indent=2) + '\n').encode())
    record_runtime()
    print(json.dumps(dict(bindings=len(bindings), graphics=len(graphics), pending=len(pending))))


def record_runtime():
    from battle_report_font_receipt import font_payload_record
    data = json.loads(RECEIPT.read_bytes())
    for entry in data['outputs']:
        if sha((ROOT / entry['path']).read_bytes().replace(b'\r\n', b'\n')) != entry['sha256']:
            raise ValueError('Dropdown prefab changed: ' + entry['path'])
        entry['meta_sha256'] = sha((ROOT / (entry['path'] + '.meta')).read_bytes())
    data['generator_sha256'] = sha(Path(__file__).read_bytes())
    for path, entry in data['graphics'].items():
        font = font_payload_record(ROOT / path, ROOT)
        if font:
            entry.update(font)
    runtime = [path for name in NAMES for path in [WORLD + name + '.cs', WORLD + name + '.cs.meta']]
    runtime += ['unity/Assets/Game/Editor/Combat/OfficialBattleReportDropdownAcceptance.cs',
                'unity/Assets/Game/Editor/Combat/OfficialBattleReportDropdownAcceptance.cs.meta',
                'unity/Assets/Game/Editor/Combat/OfficialBattleReportFoundationAcceptance.cs',
                'unity/Assets/Game/Editor/OfficialMap10BattlePlayModeAcceptance.cs']
    runtime.append('scripts/battle_report_font_receipt.py')
    data['runtime'] = {path: sha((ROOT / path).read_bytes()) for path in runtime if (ROOT / path).exists()}
    write(RECEIPT, (json.dumps(data, ensure_ascii=False, indent=2) + '\n').encode())


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, default=ROOT)
    parser.add_argument('--record-runtime', action='store_true')
    args = parser.parse_args()
    record_runtime() if args.record_runtime else build(args.source_root)
