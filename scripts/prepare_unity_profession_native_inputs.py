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
ITEM_BUNDLE = 'defaultpackage_assets_prefabs_ui_item.bundle'
RULE_DIALOG_BUNDLE = 'defaultpackage_assets_prefabs_ui_ruledialog.bundle'
ROOT_BUNDLES = [BUNDLE, ITEM_BUNDLE, RULE_DIALOG_BUNDLE,
    'defaultpackage_assets_image_itemicon_materials_public_noatlas.bundle',
    'defaultpackage_assets_image_itemicon_other_common_noatlas.bundle']
def digest(raw):
    return hashlib.sha256(raw).hexdigest()
def build(unity_project=None):
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
    assets, missing = collect(apk, ROOT_BUNDLES)
    if missing:
        raise ValueError('Incomplete profession CAB closure: ' + str(missing))
    # Follow the actual service-loaded reward prefab and record its component scope.
    reward_root = apk.asset(ITEM_BUNDLE, '/ItemIconDefault.prefab')
    reward_script_types = set()
    reward_components = []
    def visit_reward(obj):
        for reference in obj.read().m_Component:
            component = reference.component.deref()
            if component.type.name == 'MonoBehaviour':
                reward_script_types.add((component.assets_file.name, component.serialized_type.script_type_index))
                reward_components.append(dict(cab=component.assets_file.name, path_id=str(component.path_id)))
            elif component.type.name in ('RectTransform', 'Transform'):
                for child in component.read().m_Children:
                    visit_reward(child.read().m_GameObject.deref())
    visit_reward(reward_root)
    dynamic_seeds = [dict(path='Prefabs/UI/Item/ItemIconDefault.prefab', source=apk.provenance(reward_root),
        payload_sha256=digest(reward_root.get_raw_data()), component_scope=reward_components)]
    for relative in ('/RuleDialog.prefab',):
        root = apk.asset(RULE_DIALOG_BUNDLE, relative)
        dynamic_seeds.append(dict(path='Prefabs/UI/RuleDialog' + relative, source=apk.provenance(root),
            payload_sha256=digest(root.get_raw_data())))
    schemas = NativeSchemas(ROOT / '.tools/source-packages/current/871_156598/official-code', unity_project=unity_project)
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
        if name == 'GachaOneRewardGo':
            # The managed tree generator cannot describe the native Wwise Event
            # nested after this class's generic state switcher. Its client source
            # fixes the exact serialized order. T is never a serialized field of
            # StateSwitcher<T>, so reuse its verified native member layout.
            fields = [('ItemIconSlot', 'PPtr<Transform>'), ('SkillTagGo', 'PPtr<MonoBehaviour>'),
                ('ItemNameText', 'PPtr<MonoBehaviour>'), ('ItemDescText', 'PPtr<MonoBehaviour>'),
                ('ItemTypeText', 'PPtr<MonoBehaviour>'), ('mHyperLinkClickListener', 'PPtr<MonoBehaviour>'),
                ('ProfIcon', 'PPtr<Image>'), ('mTransisionEffect', 'PPtr<MonoBehaviour>'),
                ('mNotExistEffect', 'PPtr<MonoBehaviour>'), ('IsSkillSwitcher', 'StateSwitcher<bool>'),
                ('mItemIconPrefab', 'PPtr<GameObject>'), ('mTransVoice', 'Event')]
            client = code / 'client-source/GachaOneRewardGo.cs'
            text = client.read_text(encoding='utf-8').split('private AwardData mAwardData;')[0]
            import re
            names = re.findall(r'(?:public|private) [\w<>]+ (\w+);', text)
            if names != [field for field, _ in fields]:
                raise ValueError('Original GachaOneRewardGo serialized field order changed')
            state_source = code / 'framework-source/StateSwitcher.cs'
            if not state_source.exists():
                state_source = ROOT / '.tools/source-packages/official-code/framework-source/StateSwitcher.cs'
            state_text = state_source.read_text(encoding='utf-8')
            if '[SerializeReference]\n\tprivate List<IStateConfig> mStateConfigs;' not in state_text or '[SerializeField]\n\tprivate GameObject mOwner;' not in state_text:
                raise ValueError('Original StateSwitcher generic serialization changed')
            overview = original_tree('Assembly-CSharp', 'ProfessionOverviewPanel')
            state = next(child for child in overview.m_Children if child.m_Name == 'mContentStateSwitcher')
            result = fields_tree(schemas.default, fields)
            event = original_tree('AK.Wwise.Unity.API.WwiseTypes', 'AK.Wwise.Event')
            for index, child in enumerate(result.m_Children):
                if child.m_Name == 'IsSkillSwitcher':
                    replacement = clone(state)
                    replacement.m_Name, replacement.m_Type = child.m_Name, child.m_Type
                    result.m_Children[index] = replacement
                elif child.m_Name == 'mTransVoice':
                    child.m_Children = [clone(value) for value in event.m_Children if value.m_Name not in
                        ('m_GameObject', 'm_Enabled', 'm_Script', 'm_Name')]
                    for field in child.m_Children:
                        for nested in field.traverse(): nested.m_Level += 1
            result.m_Children.append(clone(next(child for child in overview.m_Children if child.m_Name == 'references')))
            schemas.default.cache[assembly + ':' + name] = [dict(level=v.m_Level, type=v.m_Type, name=v.m_Name, flags=v.m_MetaFlag or 0) for v in result.traverse()]
            for path in (client, state_source):
                row = dict(path=path.relative_to(ROOT).as_posix(), sha256=digest(path.read_bytes()))
                if row not in schema_proof['client_sources']: schema_proof['client_sources'].append(row)
            return result
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
    restored_counts = {}
    for name in sorted(apk.loaded):
        raw, source = read_source(apk, name)
        if not name.startswith('assets/bin/Data/') and ('_prefabs_' not in name or name in (BUNDLE, ITEM_BUNDLE, RULE_DIALOG_BUNDLE, 'defaultpackage_assets_prefabs_ui_rule.bundle')):
            converted, records, altered, typed, checked = hydrate(apk.loaded[name], schemas, include_source_identity=True)
            restored_counts[name] = len(records)
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
    tools.extend(schemas.reference_inputs)
    result = dict(format=1, manifest=apk.version, root=BUNDLE, roots=ROOT_BUNDLES, dynamic_prefab_seeds=dynamic_seeds,
                  selected_reward_script_types=[dict(cab=cab, script_type_index=index) for cab,index in sorted(reward_script_types)], inputs=inputs, components=fields,
                  converted_objects=changes, managed_reference_unions=unions,
                  tools=[dict(path=str(path.resolve()), sha256=digest(path.read_bytes())) for path in sorted(set(tools))],
                  outputs=[], schema_proof=schema_proof, restored_component_counts=restored_counts,
                  operation='Strict original schema read; restore complete serialized fields and reversible managed-reference registries. Encode, reopen, compare every original field and preserve all other object payload bytes.',
                  limitations=['Converter inputs only; original runtime types, Windows asset import, interaction and publication remain required.'])
    (dest / 'source-manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('Prepared typed profession UI inputs:', len(inputs), 'bundles', len(fields), 'components', flush=True)
if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--unity-project', type=Path, help='Unity project providing actual compiled TMP/UGUI metadata')
    build(parser.parse_args().unity_project)
