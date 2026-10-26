"""Restore original report layout, scale, scrolling and standard control fields."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import sys
import uuid

ROOT = Path(__file__).resolve().parents[1]
RECEIPT = ROOT / 'assets/reference/battle-hud/report-foundation-source-manifest.json'
WORLD = 'unity/Assets/Game/Runtime/Official/WorldUI/'
CLASSES = ['LayoutElementClamper', 'MaintainGlobalScaleUI', 'GridLayoutGroupJSZX',
           'HorizontalLayoutGroupJSZX', 'ScrollRect', 'LoopVerticalScrollRect', 'Button', 'Slider', 'Mask']


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def write(path, raw):
    temporary = path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp')
    try:
        temporary.write_bytes(raw)
        temporary.replace(path)
    finally:
        temporary.unlink(missing_ok=True)


def guid(path):
    return re.search(r'(?m)^guid: (\w+)', path.with_name(path.name + '.meta').read_text())[1]


def build(source_root):
    sys.path.insert(0, str(source_root / 'scripts'))
    from official_apk import Apk
    from official_unity_schema import ScriptSchemas
    from refresh_current_ui_graphics import object_path
    from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
    from UnityPy.classes.PPtr import PPtr
    import yaml

    class Dumper(yaml.SafeDumper):
        pass

    def mapping(dumper, value):
        return dumper.represent_mapping('tag:yaml.org,2002:map', value.items(),
                                        flow_style=bool(value) and not any(isinstance(x, (list, dict)) for x in value.values()))
    Dumper.add_representer(dict, mapping)
    apk = Apk(manifest='854_156383', reuse_current_cache=True)
    schemas = ScriptSchemas()
    schemas.generator = TypeTreeGenerator('2022.3.57f1', generator='AssetsTools')
    unity_data = Path('D:/Tool/Unity/Editor/Data')
    schemas.generator.load_local_dll_folder(str(unity_data / 'Managed/UnityEngine'))
    decoded = source_root / '.tools/source-packages/official-code/managed/decoded'
    schema_inputs = []
    for path in sorted(decoded.glob('*.dll')):
        if path.stem.startswith('UnityEngine') or path.stem == 'Unity.TextMeshPro':
            continue
        schemas.generator.load_dll(path.read_bytes())
        schema_inputs.append(dict(path=path.relative_to(source_root).as_posix(), sha256=sha(path.read_bytes())))
    for path in [ROOT / ('unity/Library/ScriptAssemblies/' + name + '.dll') for name in ['UnityEngine.UI', 'Unity.TextMeshPro']]:
        schemas.generator.load_dll(path.read_bytes())
    schemas.generator.load_dll((unity_data / 'MonoBleedingEdge/lib/mono/4.5/mscorlib.dll').read_bytes())
    schemas.generator.load_dll((unity_data / 'NetStandard/ref/2.1.0/netstandard.dll').read_bytes())
    script_paths = {name: ROOT / WORLD / (name + '.cs') for name in ['GridLayoutGroupJSZX', 'HorizontalLayoutGroupJSZX']}
    script_paths.update({name: ROOT / WORLD / ('World' + name + '.cs') for name in ['LayoutElementClamper', 'MaintainGlobalScaleUI']})
    script_paths['LoopVerticalScrollRect'] = ROOT / WORLD / 'LoopScroll/LoopVerticalScrollRect.cs'
    script_paths.update({name: ROOT / 'unity/Library/PackageCache/com.unity.ugui@1.0.0/Runtime/UI/Core' / (name + '.cs')
                         for name in ['ScrollRect', 'Button', 'Slider', 'Mask']})
    for name in ['LayoutElementClamper', 'MaintainGlobalScale', 'MaintainGlobalScaleUI']:
        path = ROOT / WORLD / ('World' + name + '.cs')
        meta = path.with_name(path.name + '.meta')
        if not meta.exists():
            key = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs/official-world-ui/' + path.name).hex
            write(meta, ('fileFormatVersion: 2\nguid: ' + key + '\nMonoImporter:\n'
                         '  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n'
                         '  executionOrder: 0\n  icon: {instanceID: 0}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n').encode())

    graphics = json.loads((ROOT / 'assets/reference/battle-hud/report-graphics-source-manifest.json').read_bytes())
    prior = json.loads(RECEIPT.read_bytes()) if RECEIPT.exists() else None
    panels = []
    # Build every local object map before resolving the original row-prefab dependencies.
    for entry in graphics['outputs']:
        path = ROOT / entry['path']
        raw = path.read_bytes().replace(b'\r\n', b'\n')
        expected = {entry['sha256']}
        if prior:
            expected.update(x['sha256'] for x in prior['outputs'] if x['path'] == entry['path'])
        if sha(raw) not in expected:
            raise ValueError('Unexpected report prefab modification: ' + str(path))
        blocks = re.split(r'(?m)^--- ', raw.decode())
        docs = {}
        for index, block in enumerate(blocks[1:], 1):
            match = re.match(r'!u!(\d+) &(-?\d+)\n', block)
            docs[int(match[2])] = (index, int(match[1]), next(iter(yaml.safe_load(block[match.end():]).values())))
        parents = {data['m_GameObject']['fileID']: data['m_Father']['fileID'] for _, kind, data in docs.values() if kind == 224}
        paths = {}

        def native_path(go):
            if go in paths:
                return paths[go]
            data = docs[go][2]
            parent = parents[go]
            name = data['m_Name']
            repeated = 0
            if parent:
                for child in docs[parent][2]['m_Children']:
                    child_go = docs[child['fileID']][2]['m_GameObject']['fileID']
                    if child_go == go:
                        break
                    if docs[child_go][2]['m_Name'] == name:
                        repeated += 1
            paths[go] = (native_path(docs[parent][2]['m_GameObject']['fileID']) if parent else '') + '/' + name + (f' ({repeated})' if repeated else '')
            return paths[go]

        go_by_path = {native_path(go): go for go, (_, kind, _) in docs.items() if kind == 1}
        bundle = 'defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_' + path.stem.lower() + '.bundle'
        objects = apk.objects(bundle)
        local = {}
        for obj in objects:
            if obj.type.name != 'GameObject':
                continue
            key = object_path(obj)[0]
            go = go_by_path[key]
            original = obj.read()
            components = docs[go][2]['m_Component']
            if len(original.m_Component) != len(components):
                raise ValueError('Original component order changed: ' + key)
            local[obj.path_id] = go
            for old, new in zip(original.m_Component, components):
                file_id = new['component']['fileID']
                if old.component.type.value != docs[file_id][1]:
                    raise ValueError('Original component type changed: ' + key)
                local[old.component.path_id] = file_id
        panels.append(dict(path=path, blocks=blocks, docs=docs, local=local, objects=objects, input_sha256=sha(raw)))

    inputs, bindings, outputs, deferred = [], [], [], []
    for panel in panels:
        def convert(value, owner):
            if isinstance(value, list):
                return [convert(x, owner) for x in value]
            if isinstance(value, dict):
                if set(value) == {'m_FileID', 'm_PathID'}:
                    if not value['m_PathID']:
                        return dict(fileID=0)
                    target = PPtr(**value, assetsfile=owner.assets_file).deref()
                    if value['m_FileID'] == 0:
                        return dict(fileID=panel['local'][target.path_id])
                    for other in panels:
                        if any(obj.assets_file.name == target.assets_file.name for obj in other['objects']) and target.path_id in other['local']:
                            return dict(fileID=other['local'][target.path_id], guid=guid(other['path']), type=3)
                    raise ValueError('Unconverted original dependency: ' + str(target.type.name)
                                     + ':' + str(target.path_id) + ':' + target.assets_file.name
                                     + '; matching panels=' + str([(x['path'].name, [o.assets_file.name for o in x['objects'][:1]])
                                                                  for x in panels if target.path_id in x['local']]))
                return {k: convert(v, owner) for k, v in value.items()}
            return value
        for obj in panel['objects']:
            if obj.type.name != 'MonoBehaviour':
                continue
            basic = obj.read(check_read=False)
            name = basic.m_Script.read().m_ClassName
            if name not in CLASSES:
                continue
            data = schemas.read(obj)
            if name == 'LoopVerticalScrollRect':
                target = PPtr(**data['DefaultPrefabSource']['Prefab'], assetsfile=obj.assets_file).deref()
                if target.read().m_Name == 'DropdownItem':
                    deferred.append(dict(prefab=panel['path'].relative_to(ROOT).as_posix(), original_path_id=str(obj.path_id),
                                         object_sha256=sha(obj.get_raw_data()), fields=data,
                                         dependency=apk.provenance(target), reason='The common DropdownItem prefab and UIDropdown provider must be migrated together.'))
                    continue
            fields = {k: convert(v, obj) for k, v in data.items() if k not in ['m_GameObject', 'm_Enabled', 'm_Script', 'm_Name']}
            file_id = panel['local'][obj.path_id]
            index, _, native = panel['docs'][file_id]
            native['m_Script'] = dict(fileID=11500000, guid=guid(script_paths[name]), type=3)
            native = {k: v for k, v in native.items() if k not in fields}
            native.update(fields)
            panel['blocks'][index] = '!u!114 &' + str(file_id) + '\n' + yaml.dump({'MonoBehaviour': native}, Dumper=Dumper, allow_unicode=True, sort_keys=False, width=10000)
            checks = []
            def check(value, key):
                if isinstance(value, dict):
                    if 'fileID' in value:
                        checks.append(dict(path=key, kind='reference', reference=value))
                    else:
                        for k, v in value.items():
                            check(v, key + '.' + k)
                elif isinstance(value, list):
                    checks.append(dict(path=key, kind='array', count=len(value)))
                    for i, v in enumerate(value):
                        check(v, key + '.Array.data[' + str(i) + ']')
                elif isinstance(value, str):
                    checks.append(dict(path=key, kind='string', text=value))
                else:
                    checks.append(dict(path=key, kind='number', number=value))
            for key, value in fields.items():
                check(value, key)
            bindings.append(dict(prefab=panel['path'].relative_to(ROOT).as_posix(), path=object_path(basic.m_GameObject.deref())[0],
                                 kind=name, file_id=str(file_id), original_path_id=str(obj.path_id), object_sha256=sha(obj.get_raw_data()), fields=fields, checks=checks))
            inputs.append(apk.provenance(obj))
        result = '--- '.join(panel['blocks']).encode()
        outputs.append(dict(path=panel['path'].relative_to(ROOT).as_posix(), sha256=sha(result), previous_sha256=panel['input_sha256'], bytes=len(result)))
        panel['result'] = result
    # All strict reads and dependencies must resolve before any prefab is replaced.
    for panel in panels:
        write(panel['path'], panel['result'])
    code = []
    for name in ['LayoutElementClamper', 'MaintainGlobalScale', 'MaintainGlobalScaleUI']:
        original = source_root / '.tools/source-packages/official-code/client-source' / (name + '.cs')
        migrated = ROOT / WORLD / ('World' + name + '.cs')
        code.append(dict(original=original.relative_to(source_root).as_posix(), original_sha256=sha(original.read_bytes()),
                         path=migrated.relative_to(ROOT).as_posix(), sha256=sha(migrated.read_bytes())))
    reused = []
    for path in sorted((ROOT / WORLD / 'LoopScroll').glob('*.cs')) + [ROOT / WORLD / (name + '.cs') for name in
            ['GridLayoutGroupJSZX', 'GridLayoutSettings', 'GridLayoutSetting', 'HorizontalLayoutGroupJSZX', 'HorizontalOrVerticalLayoutGroupJSZX']]:
        reused.append(dict(path=path.relative_to(ROOT).as_posix(), canonical_lf_sha256=sha(path.read_bytes().replace(b'\r\n', b'\n'))))
    schema_environment = {path.name: sha(path.read_bytes()) for path in [
        unity_data / 'Managed/UnityEngine/UnityEngine.CoreModule.dll',
        unity_data / 'NetStandard/ref/2.1.0/netstandard.dll',
        unity_data / 'MonoBleedingEdge/lib/mono/4.5/mscorlib.dll',
        ROOT / 'unity/Library/ScriptAssemblies/UnityEngine.UI.dll',
        ROOT / 'unity/Library/ScriptAssemblies/Unity.TextMeshPro.dll']}
    receipt = dict(format=1, manifest=apk.version, generator_sha256=sha(Path(__file__).read_bytes()),
                   inputs=inputs, schema_inputs=schema_inputs, schema_environment=schema_environment, code=code, reused=reused, bindings=bindings, outputs=outputs, deferred=deferred,
                   adaptations=['Use actual installed Unity/UGUI types with original client, Framework and LoopScroll assemblies for strict complete field reads.',
                                'Resolve local references by unchanged hierarchy/component order and external row prefabs by original asset identity.',
                                'Only decompiler syntax, namespaces and diagnostic logging are adapted in the three ported layout/scale classes.'],
                   pending=['Report row/provider logic, localization refresh, remaining graphic/character components and production entry remain unfinished.'])
    write(RECEIPT, (json.dumps(receipt, ensure_ascii=False, indent=2) + '\n').encode())
    print(json.dumps(dict(prefabs=len(outputs), components=len(bindings))))


def record_runtime():
    receipt = json.loads(RECEIPT.read_bytes())
    scroll_path = ROOT / 'assets/reference/battle-hud/report-scroll-source-manifest.json'
    scroll = json.loads(scroll_path.read_bytes()) if scroll_path.exists() else None
    for entry in receipt['outputs']:
        allowed = {entry['sha256']}
        if scroll:
            allowed.update(row['sha256'] for row in scroll['outputs'] if row['path'] == entry['path'])
        if sha((ROOT / entry['path']).read_bytes().replace(b'\r\n', b'\n')) not in allowed:
            raise ValueError('Report foundation prefab changed: ' + entry['path'])
    receipt['generator_sha256'] = sha(Path(__file__).read_bytes())
    unity_data = Path('D:/Tool/Unity/Editor/Data')
    receipt['schema_environment'] = {path.name: sha(path.read_bytes()) for path in [
        unity_data / 'Managed/UnityEngine/UnityEngine.CoreModule.dll',
        unity_data / 'NetStandard/ref/2.1.0/netstandard.dll',
        unity_data / 'MonoBleedingEdge/lib/mono/4.5/mscorlib.dll',
        ROOT / 'unity/Library/ScriptAssemblies/UnityEngine.UI.dll',
        ROOT / 'unity/Library/ScriptAssemblies/Unity.TextMeshPro.dll']}
    for entry in receipt['code']:
        entry['sha256'] = sha((ROOT / entry['path']).read_bytes())
        entry['meta_sha256'] = sha((ROOT / (entry['path'] + '.meta')).read_bytes())
    receipt['runtime'] = {path: sha((ROOT / path).read_bytes()) for path in [
        'unity/Assets/Game/Editor/Combat/OfficialBattleReportFoundationAcceptance.cs',
        'unity/Assets/Game/Editor/OfficialMap10BattlePlayModeAcceptance.cs']}
    write(RECEIPT, (json.dumps(receipt, ensure_ascii=False, indent=2) + '\n').encode())
    print('Recorded verified report foundation runtime.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, default=ROOT)
    parser.add_argument('--record-runtime', action='store_true')
    args = parser.parse_args()
    if args.record_runtime:
        record_runtime()
    else:
        build(args.source_root)
