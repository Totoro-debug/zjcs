"""Restore serialized profession UI component schemas before native export."""
import hashlib
import json
from pathlib import Path
import sys
import subprocess
ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / '.tools/unitypy-work'))
from official_apk import Apk
from map13_native_schemas import NativeSchemas, fields_tree, node, verify_fields
from official_managed_reference_conversion import clone
from map13_entity_native_schemas import install
from prepare_unity_bundle_inputs import collect, read_source, serialized_files
from prepare_unity_map13_water_entity_inputs import hydrate

CACHE = ROOT / '.tools/profession-native-ui/871_156598'
BUNDLE = 'defaultpackage_assets_prefabs_ui_profession.bundle'
RESTORE = {BUNDLE, 'defaultpackage_assets_prefabs_ui_rule.bundle'}
def digest(raw):
    return hashlib.sha256(raw).hexdigest()
def build():
    apk = Apk.current()
    if apk.version != '871_156598' or apk.index[BUNDLE] != '66728446397356c9e2febcd2ebf9f057':
        raise ValueError('Profession prefab version/bundle identity differs from the verified official source.')
    code = ROOT / '.tools/source-packages/current/871_156598/official-code'
    code_manifest = code.parent / 'code-manifest.json'
    code_sources = json.loads(code_manifest.read_bytes())
    if code_sources['manifest'] != apk.version:
        raise ValueError('Official profession code version differs from its assets.')
    for name, row in code_sources['assemblies'].items():
        if digest((code / 'managed/decoded' / name).read_bytes()) != row['decoded_sha256']:
            raise ValueError('Official profession schema assembly changed: ' + name)
    assets, missing = collect(apk, [BUNDLE])
    if missing:
        raise ValueError('Incomplete profession CAB closure: ' + str(missing))
    schemas = NativeSchemas(ROOT / '.tools/source-packages/current/871_156598/official-code')
    original_generator = schemas.default.generator
    schemas.default.tree('Unity.TextMeshPro', 'TMPro.TMPEffect')
    schemas.default.tree('Unity.TextMeshPro', 'TMPColorGradient')
    for module in ('AK.Wwise.Unity.API.WwiseTypes', 'spine-unity'):
        schemas.native.generator.load_dll((ROOT / '.tools/unity-schema' / (module + '.dll')).read_bytes())
    dotnet = Path(subprocess.check_output(['where.exe', 'dotnet'], text=True).splitlines()[0]).parent
    runtime = sorted((dotnet / 'shared/Microsoft.NETCore.App').iterdir(), key=lambda p: tuple(map(int, p.name.split('.'))))[-1]
    for name in ('System.Runtime', 'System.Private.CoreLib', 'System.Collections'):
        schemas.native.generator.load_dll((runtime / (name + '.dll')).read_bytes())
    schemas.default.generator = schemas.native.generator
    schemas.default.cache.update(schemas.native.cache)
    schema_proof = install(schemas, code)
    original_tree = schemas.default.tree
    def profession_tree(assembly, name):
        if name != 'AkEvent':
            try:
                return original_tree(assembly, name)
            except ValueError:
                current_generator = schemas.default.generator
                try:
                    schemas.default.generator = original_generator
                    return original_tree(assembly, name)
                finally:
                    schemas.default.generator = current_generator
        verify_fields('AkTriggerHandler', ('triggerList', 'useOtherObject'))
        verify_fields('AkEvent', ('actionOnEventType', 'curveInterpolation', 'enableActionOnEvent',
            'data', 'useCallbacks', 'Callbacks', 'playingId', 'soundEmitterObject', 'transitionDuration',
            'eventIdInternal', 'valueGuidInternal', 'm_callbackDataInternal'))
        verify_fields('CallbackData', ('Flags', 'FunctionName', 'GameObject'))
        fields = [('triggerList', 'int[]'), ('useOtherObject', 'bool'),
                  ('actionOnEventType', 'int'), ('curveInterpolation', 'int'), ('enableActionOnEvent', 'bool'),
                  ('data', 'Event'), ('useCallbacks', 'bool'), ('Callbacks', 'CallbackData[]'),
                  ('playingId', 'UInt32'), ('soundEmitterObject', 'PPtr<GameObject>'),
                  ('transitionDuration', 'float'), ('eventIdInternal', 'int'),
                  ('valueGuidInternal', 'UInt8[]'), ('m_callbackDataInternal', 'PPtr<MonoBehaviour>')]
        result = fields_tree(schemas.default, fields)
        for child in result.m_Children:
            if child.m_Name == 'data':
                source = original_tree('AK.Wwise.Unity.API.WwiseTypes', 'AK.Wwise.Event')
                child.m_Children = [clone(value) for value in source.m_Children if value.m_Name not in
                                    ('m_GameObject', 'm_Enabled', 'm_Script', 'm_Name')]
                for field in child.m_Children:
                    for nested in field.traverse(): nested.m_Level += 1
            if child.m_Name == 'Callbacks':
                data = child.m_Children[0].m_Children[1]
                data.m_Children = [node('int', 'Flags', 4), node('string', 'FunctionName', 4), node('PPtr<GameObject>', 'GameObject', 4)]
        schemas.default.cache[assembly + ':' + name] = [dict(level=v.m_Level, type=v.m_Type, name=v.m_Name, flags=v.m_MetaFlag or 0) for v in result.traverse()]
        return result
    schemas.default.tree = profession_tree
    dest = CACHE / 'typed/inputs'
    dest.mkdir(parents=True, exist_ok=True)
    inputs, fields, changes, unions = [], [], [], []
    for name in sorted(apk.loaded):
        raw, source = read_source(apk, name)
        if name in RESTORE:
            converted, records, altered, typed, checked = hydrate(apk.loaded[name], schemas)
            fields.extend(records)
            changes.extend(altered)
            unions.extend(typed)
            print('Restored', name, len(records), 'components', flush=True)
        else:
            converted = raw
        filename = Path(name).name
        (dest / filename).write_bytes(converted)
        inputs.append(dict(source, output=filename, converter_input_sha256=digest(converted), converter_input_bytes=len(converted)))
    raw = apk.archive.read('assets/bin/Data/unity default resources')
    (dest / 'unity default resources').write_bytes(raw)
    tools = [Path(__file__), ROOT / 'scripts/map13_native_schemas.py',
             ROOT / 'scripts/map13_entity_native_schemas.py',
             ROOT / 'scripts/official_managed_reference_conversion.py',
             ROOT / 'scripts/prepare_unity_map13_water_entity_inputs.py']
    tools.extend(sorted((ROOT / '.tools/unity-schema').glob('*.dll')))
    tools.extend(sorted((ROOT / '.tools/source-packages/current/871_156598/official-code/managed/decoded').glob('*.dll')))
    tools.extend(runtime / (name + '.dll') for name in ('System.Runtime', 'System.Private.CoreLib', 'System.Collections'))
    tools.extend(schemas.reference_inputs)
    tools.extend(ROOT / record['path'] for record in schema_proof['client_sources'])
    tools.extend(ROOT / schema_proof[key]['path'] for key in ('image', 'metadata'))
    tools.append(code_manifest)
    result = dict(format=1, manifest=apk.version, root=BUNDLE, inputs=inputs, components=fields,
                  converted_objects=changes, managed_reference_unions=unions,
                  tools=[dict(path=str(path.resolve()), sha256=digest(path.read_bytes())) for path in sorted(set(tools))],
                  outputs=[], schema_proof=schema_proof,
                  operation='Strict original schema read; restore complete serialized fields and reversible managed-reference registries. Encode, reopen, compare every original field and preserve all other object payload bytes.',
                  limitations=['Converter inputs only; original runtime types, Windows asset import, interaction and publication remain required.'])
    (dest / 'source-manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Prepared typed profession UI inputs:', len(inputs), 'bundles', len(fields), 'components', flush=True)
if __name__ == '__main__':
    build()
