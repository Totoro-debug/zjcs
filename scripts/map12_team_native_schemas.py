"""Recover missing team-converter descriptions from the pinned APK metadata.

The two temporary DLLs identify native object reference types to the metadata
reader only. They contain no fields or behavior, never enter Assets, and do not
implement UMI, SimpleAnimation, Spine, or Wwise at runtime.
"""
from pathlib import Path
import json
import struct
import subprocess

from official_apk import ROOT
from official_metadata import Metadata
from read_official_native import NativeMethods
from map13_entity_native_schemas import install as install_shared
from map13_ui_native_schemas import install_input_field
from map13_native_schemas import node, fields_tree
from official_managed_reference_conversion import clone
from prepare_unity_map12_sources import sha, source_path
from validation_workspace import validation_directory


CARRIERS = {
    'UMI.Runtime': 'namespace UMI { public abstract class MobileInputReceiver : UnityEngine.MonoBehaviour {} '
                   'public class MobileInputField : MobileInputReceiver {} }\n',
    'SimpleAnimationComponent': 'public class SimpleAnimation : UnityEngine.MonoBehaviour {}\n',
}


def install(schemas, code):
    directory = validation_directory()
    if directory == ROOT / 'output/validation':
        raise ValueError('Use an owned validation session for team converter metadata')
    proof = install_shared(schemas, code)
    native = NativeMethods(ROOT / proof['image']['path'], ROOT / proof['metadata']['path'])
    model = Metadata(native.metadata)
    registration = struct.unpack_from('<16Q', native.image, proof['metadata_registration'])
    if registration[7] != proof['type_table'] or registration[10] != len(model.types):
        raise ValueError('Original team native registration differs')
    images = [struct.unpack_from('<10I', native.metadata, at)
              for at in range(native.header[42], native.header[42] + native.header[43], 40)]

    def type_shape(pointer):
        data, bits = struct.unpack_from('<QI', native.image, pointer)
        kind = bits >> 16 & 255
        result = dict(kind=kind, data=data, bits=bits)
        if kind in (17, 18):
            typ = model.types[data]
            result.update(name=[typ['namespace'], typ['name']], enum=typ['enum'])
        elif kind == 29:
            result['element'] = type_shape(data)
        elif kind == 21:
            generic = struct.unpack_from('<4Q', native.image, data)
            definition = type_shape(generic[0])
            count = struct.unpack_from('<I', native.image, generic[1])[0]
            if count > 16:
                raise ValueError('Inspect original team generic argument count')
            arguments = struct.unpack_from('<Q', native.image, generic[1] + 8)[0]
            result.update(definition=definition, arguments=[type_shape(struct.unpack_from('<Q', native.image, arguments + i * 8)[0]) for i in range(count)])
        return result

    def describe(namespace, name, expected, parent=None, delegates=()):
        typ, = [t for t in model.types if (t['namespace'], t['name']) == (namespace, name)]
        base, = [t for t in model.types if t['byval'] == typ['parent']]
        if parent and (base['namespace'], base['name']) != parent:
            raise ValueError('Original team base class differs: ' + name)
        image, = [i for i in images if i[2] <= typ['index'] < i[2] + i[3]]
        attributes = dict(struct.unpack_from('<2I', native.metadata, native.header[52] + i * 8)
                          for i in range(image[8], image[8] + image[9]))
        rows, selected = [], []
        for field in typ['fields']:
            pointer = struct.unpack_from('<Q', native.image, registration[7] + field['type_index'] * 8)[0]
            data, bits = struct.unpack_from('<QI', native.image, pointer)
            constructors = []
            if field['token'] in attributes:
                at = native.header[50] + attributes[field['token']]
                count = native.metadata[at]
                if count >= 128:
                    raise ValueError('Inspect extended original team field attributes')
                for index in range(count):
                    method_index = struct.unpack_from('<I', native.metadata, at + 1 + index * 4)[0]
                    method = native.methods[method_index]
                    owner = native.types[method[1]]
                    constructors.append(native.string(owner[1]) + '.' + native.string(owner[0]) + '.' + native.string(method[0]))
            flags, kind = bits & 65535, bits >> 16 & 255
            serial = not flags & (16 | 32 | 64 | 128) and ((flags & 7) == 6 or 'UnityEngine.SerializeField..ctor' in constructors)
            detail = dict(field, native_type_pointer=pointer, native_type_data=data,
                          native_type_bits=bits, attribute_constructors=constructors, type_shape=type_shape(pointer))
            if kind in (17, 18):
                target = model.types[data]
                detail['field_type'] = [target['namespace'], target['name']]
            elif kind == 29:
                item_data, item_bits = struct.unpack_from('<QI', native.image, data)
                detail['element_type_bits'] = item_bits
                if item_bits >> 16 & 255 in (17, 18):
                    item = model.types[item_data]
                    detail['element_type'] = [item['namespace'], item['name']]
            if field['name'] in delegates:
                if kind == 18:
                    if detail['field_type'] != ['System', 'Action']:
                        raise ValueError('Original UMI delegate differs')
                elif kind == 21:
                    generic = struct.unpack_from('<4Q', native.image, data)
                    detail['generic_class'] = generic
                    argument, argument_bits = struct.unpack_from('<QI', native.image, generic[0])
                    target = model.types[argument]
                    if argument_bits >> 16 & 255 not in (17, 18) or (target['namespace'], target['name']) != ('System', 'Action`1'):
                        raise ValueError('Original UMI generic delegate differs')
                    if [a['kind'] for a in detail['type_shape']['arguments']] != [2]:
                        raise ValueError('Original UMI focus delegate argument differs')
                else:
                    raise ValueError('Original UMI delegate kind differs')
                serial = False
                detail['excluded_delegate'] = True
            detail['serialized'] = bool(serial)
            if serial:
                selected.append(field['name'])
            rows.append(detail)
        if selected != list(expected):
            raise ValueError('Original team serialized field order differs: ' + name + ' ' + str(selected))
        record = dict(type=name, namespace=namespace, type_index=typ['index'],
                      parent=[base['namespace'], base['name']], fields=rows, serialized=selected)
        proof['types'].append(record)
        return record

    def verify(record, expected):
        for name, shape in expected.items():
            field, = [f for f in record['fields'] if f['name'] == name]
            actual = field['type_shape']
            for key, value in shape.items():
                if actual.get(key) != value:
                    raise ValueError('Original team field type differs: ' + record['type'] + '.' + name)

    describe('UMI', 'MobileInputReceiver', (), ('UnityEngine', 'MonoBehaviour'))
    mobile = [('CustomFont', 'string'), ('KeyboardLanguage', 'string'), ('BackgroundColor', 'ColorRGBA'),
              ('IsManualHideControl', 'bool'), ('IsWithDoneButton', 'bool'), ('IsWithClearButton', 'bool'),
              ('ReturnKey', 'int'), ('OnReturnPressedEvent', 'UnityEvent')]
    mobile_proof = describe('UMI', 'MobileInputField', [n for n, _ in mobile], ('UMI', 'MobileInputReceiver'),
                            ('OnReturnPressed', 'OnFocusChanged'))
    verify(mobile_proof, dict(CustomFont=dict(kind=14), KeyboardLanguage=dict(kind=14),
        BackgroundColor=dict(kind=17, name=['UnityEngine', 'Color']),
        IsManualHideControl=dict(kind=2), IsWithDoneButton=dict(kind=2), IsWithClearButton=dict(kind=2),
        ReturnKey=dict(kind=17, name=['', 'ReturnKeyType'], enum=True),
        OnReturnPressedEvent=dict(kind=18, name=['UnityEngine.Events', 'UnityEvent'])))
    animation = [('m_PlayAutomatically', 'bool'), ('m_WrapMode', 'int'), ('m_Clip', 'PPtr<AnimationClip>'), ('m_States', 'EditorState[]')]
    animation_proof = describe('', 'SimpleAnimation', [n for n, _ in animation], ('UnityEngine', 'MonoBehaviour'))
    verify(animation_proof, dict(m_PlayAutomatically=dict(kind=2), m_WrapMode=dict(kind=17, name=['UnityEngine', 'WrapMode'], enum=True),
        m_Clip=dict(kind=18, name=['UnityEngine', 'AnimationClip']), m_States=dict(kind=29)))
    state_array, = [f['type_shape']['element'] for f in animation_proof['fields'] if f['name'] == 'm_States']
    if (state_array.get('kind'), state_array.get('name')) != (18, ['', 'EditorState']):
        raise ValueError('Original SimpleAnimation state element differs')
    state_proof = describe('', 'EditorState', ('clip', 'name'))
    verify(state_proof, dict(clip=dict(kind=18, name=['UnityEngine', 'AnimationClip']), name=dict(kind=14)))
    describe('Spine.Unity', 'SkeletonSubmeshGraphic', (), ('UnityEngine.UI', 'MaskableGraphic'))
    trigger_proof = describe('', 'AkTriggerHandler', ('triggerList', 'useOtherObject'), ('UnityEngine', 'MonoBehaviour'))
    verify(trigger_proof, dict(triggerList=dict(kind=21), useOtherObject=dict(kind=2)))
    describe('', 'AkDragDropTriggerHandler', (), ('', 'AkTriggerHandler'))
    event_fields = [('triggerList', 'int[]'), ('useOtherObject', 'bool'),
                    ('actionOnEventType', 'int'), ('curveInterpolation', 'int'), ('enableActionOnEvent', 'bool'),
                    ('data', 'Event'), ('useCallbacks', 'bool'), ('Callbacks', 'CallbackData[]'), ('playingId', 'UInt32'),
                    ('soundEmitterObject', 'PPtr<GameObject>'), ('transitionDuration', 'float'), ('eventIdInternal', 'int'),
                    ('valueGuidInternal', 'UInt8[]'), ('m_callbackDataInternal', 'PPtr<MonoBehaviour>')]
    event_proof = describe('', 'AkEvent', [n for n, _ in event_fields[2:]], ('', 'AkDragDropTriggerHandler'))
    verify(event_proof, dict(actionOnEventType=dict(kind=17, name=['', 'AkActionOnEventType'], enum=True),
        curveInterpolation=dict(kind=17, name=['', 'AkCurveInterpolation'], enum=True),
        enableActionOnEvent=dict(kind=2), data=dict(kind=18, name=['AK.Wwise', 'Event']),
        useCallbacks=dict(kind=2), Callbacks=dict(kind=21), playingId=dict(kind=9),
        soundEmitterObject=dict(kind=18, name=['UnityEngine', 'GameObject']), transitionDuration=dict(kind=12),
        eventIdInternal=dict(kind=8), valueGuidInternal=dict(kind=29), m_callbackDataInternal=dict(kind=18, name=['', 'AkEventCallbackData'])))
    for record_, label, argument in ((trigger_proof, 'triggerList', dict(kind=8)),
                                     (event_proof, 'Callbacks', dict(kind=18, name=['', 'CallbackData']))):
        shape, = [f['type_shape'] for f in record_['fields'] if f['name'] == label]
        if shape['definition'].get('name') != ['System.Collections.Generic', 'List`1'] or len(shape['arguments']) != 1:
            raise ValueError('Original team list type differs: ' + label)
        if any(shape['arguments'][0].get(k) != v for k, v in argument.items()):
            raise ValueError('Original team list element differs: ' + label)
    guid_array, = [f['type_shape']['element'] for f in event_proof['fields'] if f['name'] == 'valueGuidInternal']
    if guid_array['kind'] != 5:
        raise ValueError('Original Wwise GUID is not a byte array')

    # Keep the original schema generator for classes whose object-reference
    # dependencies are supplied by the existing metadata-only schema assemblies.
    original_generator = schemas.default.generator
    schemas.default.tree('Unity.TextMeshPro', 'TMPro.TMPEffect')
    schemas.default.tree('Unity.TextMeshPro', 'TMPColorGradient')
    for module in ('AK.Wwise.Unity.API.WwiseTypes', 'spine-unity'):
        path = ROOT / '.tools/unity-schema' / (module + '.dll')
        schemas.native.generator.load_dll(path.read_bytes())
        schemas.reference_inputs.append(path)
    dotnet = Path(subprocess.check_output(['where.exe', 'dotnet'], text=True).splitlines()[0]).parent
    runtime = max((dotnet / 'shared/Microsoft.NETCore.App').iterdir(), key=lambda p: tuple(map(int, p.name.split('.'))))
    for module in ('System.Runtime', 'System.Private.CoreLib', 'System.Collections'):
        path = runtime / (module + '.dll')
        schemas.native.generator.load_dll(path.read_bytes())
        schemas.reference_inputs.append(path)
    editor = Path(json.loads((ROOT / 'unity-tools.json').read_bytes())['editorInstallPath']) / 'Editor'
    compiler, host = editor / 'Data/DotNetSdkRoslyn/csc.dll', editor / 'Data/NetCoreRuntime/dotnet.exe'
    carrier_dir = directory / 'team-reference-types'
    carrier_dir.mkdir(exist_ok=True)
    proof['reference_type_carriers'] = []
    for module, source in CARRIERS.items():
        source_path_ = carrier_dir / (module + '.cs')
        target = carrier_dir / (module + '.dll')
        response = carrier_dir / (module + '.rsp')
        source_path_.write_bytes(source.encode())
        refs = sorted((editor / 'Data/NetStandard/ref/2.1.0').glob('*.dll'))
        refs += [editor / 'Data/Managed/UnityEngine/UnityEngine.CoreModule.dll']
        lines = ['/nologo', '/target:library', '/nostdlib+', '/deterministic+',
                 '/pathmap:' + str(carrier_dir) + '=/zjcs/team-reference-types', '/out:"' + str(target) + '"']
        lines += ['/r:"' + str(p) + '"' for p in refs]
        lines += ['"' + str(source_path_) + '"']
        response.write_bytes(('\n'.join(lines) + '\n').encode())
        result = subprocess.run([str(host), str(compiler), '@' + str(response)], capture_output=True)
        (carrier_dir / (module + '.log')).write_bytes(result.stdout + result.stderr)
        if result.returncode:
            raise ValueError('Team reference-only metadata compile failed: ' + (result.stdout + result.stderr).decode(errors='replace'))
        schemas.native.generator.load_dll(target.read_bytes())
        proof['reference_type_carriers'].append(dict(module=module, source=source,
            sha256=sha(target.read_bytes()), compiler_sha256=sha(compiler.read_bytes()),
            references=[dict(path=str(p.resolve()), sha256=sha(p.read_bytes())) for p in refs]))

    schemas.default.generator = schemas.native.generator
    schemas.default.cache.update(schemas.native.cache)
    input_proof = install_input_field(schemas.default)
    proof['team_input_field'] = input_proof
    previous = schemas.default.tree

    def cache(assembly, name, result):
        rows = [dict(level=n.m_Level, type=n.m_Type, name=n.m_Name, flags=n.m_MetaFlag or 0) for n in result.traverse()]
        schemas.default.cache[assembly + ':' + name] = rows
        schemas.default.records[assembly + ':' + name] = rows
        return result

    def tree(assembly, name):
        if name == 'Spine.Unity.SkeletonSubmeshGraphic':
            return cache(assembly, name, clone(schemas.native.tree('UnityEngine.UI', 'UnityEngine.UI.MaskableGraphic')))
        if name == 'SimpleAnimation':
            result = fields_tree(schemas.default, animation)
            data = result.m_Children[-1].m_Children[0].m_Children[1]
            data.m_Children = [node('PPtr<AnimationClip>', 'clip', 4), node('string', 'name', 4)]
            return cache(assembly, name, result)
        if name == 'UMI.MobileInputField':
            result = fields_tree(schemas.default, mobile)
            button = schemas.native.tree('UnityEngine.UI', 'UnityEngine.UI.Button')
            event = clone(next(c for c in button.m_Children if c.m_Name == 'm_OnClick'))
            event.m_Name, event.m_Type = 'OnReturnPressedEvent', 'UnityEvent'
            result.m_Children[-1] = event
            return cache(assembly, name, result)
        if name == 'AkEvent':
            result = fields_tree(schemas.default, event_fields)
            event = previous('AK.Wwise.Unity.API.WwiseTypes', 'AK.Wwise.Event')
            data = next(c for c in result.m_Children if c.m_Name == 'data')
            data.m_Children = [clone(c) for c in event.m_Children if c.m_Name not in ('m_GameObject', 'm_Enabled', 'm_Script', 'm_Name')]
            for child in data.m_Children:
                for nested in child.traverse():
                    nested.m_Level += 1
            # This CAB closure contains no callback elements; callers must verify
            # this before conversion instead of inferring CallbackFlags encoding.
            return cache(assembly, name, result)
        try:
            return previous(assembly, name)
        except ValueError:
            active = schemas.default.generator
            try:
                schemas.default.generator = original_generator
                return previous(assembly, name)
            finally:
                schemas.default.generator = active

    schemas.default.tree = tree
    proof['team_limitations'] = ['Metadata descriptions only; native scripts/resources and runtime behavior remain required.',
                               'AkEvent callback arrays must be empty; nonempty callback serialization is not inferred.']
    proof['team_tool'] = dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes()))
    return proof
