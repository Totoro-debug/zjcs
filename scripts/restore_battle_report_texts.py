"""Restore all original report text keys and their verified historical APK Chinese strings."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import struct
import sys

from restore_battle_report_dropdown import ROOT, WORLD, sha, write, guid, metadata

RECEIPT = ROOT / 'assets/reference/battle-hud/report-text-source-manifest.json'
SERVICE = WORLD + 'WorldBattleReportTextService.cs'


def build(source_root):
    sys.path.insert(0, str(source_root / 'scripts'))
    from official_apk import Apk
    from official_unity_schema import ScriptSchemas
    from refresh_current_ui_graphics import object_path
    from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
    import xxhash
    import yaml
    apk = Apk(manifest='854_156383', reuse_current_cache=True)
    schema = ScriptSchemas()
    schema.generator = TypeTreeGenerator('2022.3.57f1', generator='AssetsTools')
    editor = Path('D:/Tool/Unity/Editor/Data')
    schema.generator.load_local_dll_folder(str(editor / 'Managed/UnityEngine'))
    assemblies = []
    for path in sorted((source_root / '.tools/source-packages/official-code/managed/decoded').glob('*.dll')):
        if not path.stem.startswith('UnityEngine') and path.stem != 'Unity.TextMeshPro':
            schema.generator.load_dll(path.read_bytes())
            assemblies.append(dict(path=path.relative_to(source_root).as_posix(), sha256=sha(path.read_bytes())))
    for path in [ROOT / 'unity/Library/ScriptAssemblies/UnityEngine.UI.dll',
                 ROOT / 'unity/Library/ScriptAssemblies/Unity.TextMeshPro.dll',
                 editor / 'NetStandard/ref/2.1.0/netstandard.dll', editor / 'MonoBleedingEdge/lib/mono/4.5/mscorlib.dll']:
        schema.generator.load_dll(path.read_bytes())
    class Dumper(yaml.SafeDumper):
        pass
    Dumper.add_representer(dict, lambda dumper, value: dumper.represent_mapping(
        'tag:yaml.org,2002:map', value.items(), flow_style='fileID' in value))
    receipts = [json.loads(path.read_bytes()) for path in (ROOT / 'assets/reference/battle-hud').glob('report-*-source-manifest.json')]
    base = json.loads((ROOT / 'assets/reference/battle-hud/report-graphics-source-manifest.json').read_bytes())
    bindings, outputs, keys, results = [], [], set(), []
    for panel in base['outputs']:
        path = ROOT / panel['path']
        raw = path.read_bytes().replace(b'\r\n', b'\n')
        expected = {entry['sha256'] for receipt in receipts for entry in receipt.get('outputs', []) if entry['path'] == panel['path']}
        if sha(raw) not in expected:
            raise ValueError('Unrecognized report text input: ' + panel['path'])
        blocks = re.split(r'(?m)^--- ', raw.decode())
        docs = {}
        for index, block in enumerate(blocks[1:], 1):
            match = re.match(r'!u!(\d+) &(-?\d+)\n', block)
            docs[int(match[2])] = (index, int(match[1]), next(iter(yaml.safe_load(block[match.end():]).values())))
        transforms = {value['m_GameObject']['fileID']: value for _, kind, value in docs.values() if kind == 224}
        def hierarchy(go):
            data = docs[go][2]
            parent = transforms[go]['m_Father']['fileID']
            repeated = 0
            if parent:
                for child in docs[parent][2]['m_Children']:
                    child_go = docs[child['fileID']][2]['m_GameObject']['fileID']
                    if child_go == go: break
                    repeated += docs[child_go][2]['m_Name'] == data['m_Name']
            return (hierarchy(docs[parent][2]['m_GameObject']['fileID']) if parent else '') + '/' + data['m_Name'] + (f' ({repeated})' if repeated else '')
        native = {hierarchy(go): go for go, (_, kind, _) in docs.items() if kind == 1}
        bundle = 'defaultpackage_assets_prefabs_ui_fight_fightendreportpanel_' + path.stem.lower() + '.bundle'
        for obj in apk.objects(bundle):
            if obj.type.name != 'MonoBehaviour': continue
            basic = obj.read(check_read=False)
            if basic.m_Script.read().m_ClassName != 'MultiLanguageText': continue
            original_go = basic.m_GameObject.deref()
            original_components = original_go.read().m_Component
            go_path = object_path(original_go)[0]
            target_go = docs[native[go_path]][2]
            if len(original_components) != len(target_go['m_Component']):
                raise ValueError('Original text component order changed: ' + go_path)
            slot = next(i for i, component in enumerate(original_components) if component.component.path_id == obj.path_id)
            file_id = target_go['m_Component'][slot]['component']['fileID']
            index, kind, value = docs[file_id]
            if kind != 114: raise ValueError('Original text component type changed.')
            data = schema.read(obj)
            fields = {key: item for key, item in data.items() if key not in ['m_GameObject', 'm_Enabled', 'm_Script', 'm_Name']}
            if set(fields) != {'myKey'}: raise ValueError('Unexpected original MultiLanguageText fields: ' + str(fields.keys()))
            value['m_Script'] = dict(fileID=11500000, guid=guid(ROOT / WORLD / 'WorldMultiLanguageText.cs'), type=3)
            value.update(fields)
            blocks[index] = '!u!114 &' + str(file_id) + '\n' + yaml.dump({'MonoBehaviour': value}, Dumper=Dumper, allow_unicode=True, sort_keys=False, width=10000)
            keys.add(fields['myKey'])
            bindings.append(dict(prefab=panel['path'], path=go_path, file_id=str(file_id), fields=fields,
                                 original_path_id=str(obj.path_id), object_sha256=sha(obj.get_raw_data()), source=apk.provenance(obj)))
        result = '--- '.join(blocks).encode()
        results.append((path, result))
        outputs.append(dict(path=panel['path'], sha256=sha(result), bytes=len(result)))
    # Inspect the APK first. A retained rawfile is accepted only against this same manifest's size/MD5.
    language_values, inputs = {}, []
    manifest = apk.source.manifest('WwiseBnk', apk.archive)
    for suffix in ['launch_config_binary_language_zh_cn_text_g_bytes.rawfile', 'config_binary_language_zh_cn_text_g_bytes.rawfile']:
        name = 'wwisebnk_assets_' + suffix
        bundle = next(row for row in manifest['bundles'] if row['name'] == name)
        entry = 'assets/yoo/WwiseBnk/' + bundle['file']
        if entry in apk.available:
            raw = apk.archive.read(entry)
            source = dict(origin='apk_zip', apk_entry=entry, apk_sha256=apk.source.apk_sha256)
        else:
            cached = apk.cloud.read('WwiseBnk', bundle)
            if cached is None: raise FileNotFoundError('Original report language rawfile: ' + name)
            raw, source = cached
        if len(raw) != bundle['size'] or hashlib.md5(raw).hexdigest() != bundle['hash']:
            raise ValueError('Original report language size/MD5 mismatch: ' + name)
        count = struct.unpack_from('<i', raw)[0]
        start = 4 + count * 12
        for i in range(count):
            identity, offset = struct.unpack_from('<Qi', raw, 4 + i * 12)
            position = start + offset
            if offset < 0 or position + 2 > len(raw): raise ValueError('Original language entry offset is invalid.')
            size = struct.unpack_from('<H', raw, position)[0]
            if position + 2 + size > len(raw): raise ValueError('Original language entry is truncated.')
            language_values[identity] = raw[position + 2:position + 2 + size].decode('utf8')
        inputs.append(dict(name=name, sha256=sha(raw), md5=bundle['hash'], bytes=len(raw), entries=count, source=source))
    texts = []
    for key in sorted(keys):
        if not key or key.isdigit() or key == '{0}': continue
        identity = xxhash.xxh64_intdigest(key.encode('utf8'))
        if identity not in language_values: raise ValueError('Original report language key missing: ' + key)
        texts.append(dict(key=key, text=language_values[identity]))
    payload = ROOT / 'unity/Assets/Game/Resources/Combat/BattleReport/report-texts.json'
    metadata(payload, 'TextScriptImporter')
    metadata(ROOT / SERVICE, 'MonoImporter')
    write(payload, (json.dumps(dict(manifest=apk.version, language='zh_CN', texts=texts), ensure_ascii=False, indent=2) + '\n').encode())
    for path, result in results: write(path, result)
    code = {name: sha((source_root / '.tools/source-packages/official-code' / name).read_bytes()) for name in
            ['framework-firstpass-source/MultiLanguageText.cs', 'framework-firstpass-source/MultiLanguageUI.cs',
             'framework-firstpass-source/Framework/LocalizationTextService.cs']}
    receipt = dict(format=1, manifest=apk.version, generator_sha256=sha(Path(__file__).read_bytes()),
                   schema_inputs=assemblies, official_code=code, bindings=bindings, outputs=outputs,
                   language_inputs=inputs, text_payload=dict(path=payload.relative_to(ROOT).as_posix(), sha256=sha(payload.read_bytes()), keys=len(texts)),
                   adaptations=['Keep original text keys, hierarchy, component order and strict complete fields; bind the existing original key/format processor port.',
                                'Use verified local zh_CN entries in place of injected providers; preserve original lookup fallback and ProcessText.',
                                'Font/language switching, text replacement providers and typography effects remain separate pending services.'])
    write(RECEIPT, (json.dumps(receipt, ensure_ascii=False, indent=2) + '\n').encode())
    record_runtime()
    print('Restored original report text components:', len(bindings), 'translated keys:', len(texts))


def record_runtime():
    data = json.loads(RECEIPT.read_bytes())
    for entry in data['outputs']:
        allowed = {entry['sha256']}
        row_receipt = ROOT / 'assets/reference/battle-hud/report-skill-row-source-manifest.json'
        if row_receipt.exists():
            allowed.update(row['sha256'] for row in json.loads(row_receipt.read_bytes())['outputs'] if row['path'] == entry['path'])
        if sha((ROOT / entry['path']).read_bytes().replace(b'\r\n', b'\n')) not in allowed:
            raise ValueError('Report text prefab changed: ' + entry['path'])
    data['generator_sha256'] = sha(Path(__file__).read_bytes())
    paths = [SERVICE, SERVICE + '.meta', WORLD + 'WorldMultiLanguageText.cs', WORLD + 'WorldMultiLanguageText.cs.meta',
             'unity/Assets/Game/Editor/Combat/OfficialBattleReportTextAcceptance.cs',
             'unity/Assets/Game/Editor/Combat/OfficialBattleReportTextAcceptance.cs.meta',
             'unity/Assets/Game/Editor/OfficialMap10BattlePlayModeAcceptance.cs']
    data['runtime'] = {path: sha((ROOT / path).read_bytes()) for path in paths if (ROOT / path).exists()}
    write(RECEIPT, (json.dumps(data, ensure_ascii=False, indent=2) + '\n').encode())


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-root', type=Path, default=ROOT)
    parser.add_argument('--record-runtime', action='store_true')
    args = parser.parse_args()
    record_runtime() if args.record_runtime else build(args.source_root)
