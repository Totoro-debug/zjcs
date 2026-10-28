"""Verify the original profession Wwise object scope and prepare native field carriers."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import struct

from official_apk import ROOT
from official_native_yaml_verification import documents
from prepare_unity_profession_native_inputs import CACHE
from prepare_unity_profession_native_bindings import build as bindings, guid
from prepare_unity_profession_photographer import record
from read_official_native import NativeMethods
from validation_workspace import require_temporary_unity_project


def build(project=None):
    closure = bindings('main-dynamic')
    exported = CACHE / 'typed/export/ExportedProject/Assets'
    original_script = exported / 'Scripts/AK.Wwise.Unity.MonoBehaviour/AkGameObj.cs'
    original_guid = guid(original_script)
    runtime = ROOT / 'unity/Assets/Game/Runtime/Official/ProfessionUI/AkGameObj.cs'
    native = NativeMethods(ROOT / '.tools/source-packages/commerce-native/libil2cpp-restored.bin',
        ROOT / '.tools/source-packages/official-code/global-metadata.dat')
    methods = []
    for name in ('AkGameObj.Register', 'AkGameObj.SetPosition', 'AkGameObj.Awake', 'AkGameObj.OnEnable',
                 'AkGameObj.OnDestroy', 'AkGameObj.Update', 'AkGameObj.GetPosition', 'AkGameObj.GetForward',
                 'AkGameObj.GetUpward', 'AkGameObj..ctor', 'AkGameObjListenerList.Init'):
        address, index = native.by_name[name]
        end = min(value for value in native.by_address if value > address)
        methods.append(dict(method=name, metadata_index=index, address=hex(address), byte_count=end-address,
            sha256=hashlib.sha256(native.image[address:end]).hexdigest()))
    epsilon = struct.unpack_from('<f', native.image, 0x3982ee8)[0]
    if epsilon != 9.999999439624929e-11:
        raise ValueError('Original Wwise position comparison threshold changed')
    rows, carriers = [], []
    for relative, asset in closure['assets'].items():
        path = ROOT / asset['path']
        if path.suffix != '.prefab':
            continue
        text = path.read_text(encoding='utf-8')
        if original_guid not in text:
            continue
        docs = documents(text)
        for file_id, (kind, fields) in docs.items():
            if kind != 'MonoBehaviour' or fields.get('m_Script', {}).get('guid') != original_guid:
                continue
            listeners = fields['m_listeners']
            offset = fields['m_positionOffsetData']
            if (listeners != dict(initialListenerList=[], useDefaultListeners=1) or not fields['isEnvironmentAware']
                    or fields['isStaticObject'] or offset != dict(KeepMe=0, positionOffset=dict(x=0, y=0, z=0))
                    or fields['m_posOffsetData']['fileID'] or fields['listenerMask'] != 1):
                raise ValueError('Inspect newly encountered original Wwise object configuration: ' + relative)
            go_id = fields['m_GameObject']['fileID']
            components = [entry['component']['fileID'] for entry in docs[go_id][1]['m_Component']]
            if any('Collider' in docs[item][0] for item in components):
                raise ValueError('Profession sound object requires its original collider/environment branch')
            transform_id = next(item for item in components if docs[item][0] in ('Transform', 'RectTransform'))
            rows.append(dict(native_relative_path=relative, file_id=file_id, source=asset,
                fields=fields, game_object_name=docs[go_id][1]['m_Name'], sibling_types=[docs[item][0] for item in components]))
            headers = list(re.finditer(r'(?m)^--- !u!\d+ &(-?\d+)\n', text))
            spans = {int(match[1]): (match.start(), headers[n+1].start() if n+1<len(headers) else len(text)) for n, match in enumerate(headers)}
            blocks = {item: text[spans[item][0]:spans[item][1]] for item in (go_id, transform_id, file_id)}
            blocks[go_id] = re.sub(r'(?ms)^  m_Component:.*?(?=  m_Layer:)',
                '  m_Component:\n  - component: {fileID: ' + str(transform_id) + '}\n  - component: {fileID: ' + str(file_id) + '}\n', blocks[go_id])
            blocks[transform_id] = re.sub(r'(?ms)^  m_Children:.*?(?=  m_Father:)', '  m_Children: []\n', blocks[transform_id])
            blocks[transform_id] = re.sub(r'(?m)^  m_Father:.*$', '  m_Father: {fileID: 0}', blocks[transform_id])
            carriers.append('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n' + ''.join(blocks[item] for item in (go_id, transform_id, file_id)).replace(original_guid, guid(runtime)))
    if len(rows) != 3:
        raise ValueError('Inspect original profession sound-object closure: ' + str(len(rows)))
    result = dict(format=1, manifest=closure['manifest'], native_inputs=[record(ROOT / '.tools/source-packages/commerce-native/libil2cpp-restored.bin'), record(ROOT / '.tools/source-packages/official-code/global-metadata.dat')],
        methods=methods, position_threshold=dict(address='0x3982ee8', value=epsilon,
            sha256=hashlib.sha256(native.image[0x3982ee8:0x3982eec]).hexdigest()), source_objects=rows,
        runtime=[record(runtime), record(runtime.with_name(runtime.name + '.meta')),
            record(ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/OfflineProfessionSoundHost.cs')],
        generator=record(Path(__file__)),
        validation=record(ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionSoundObjectValidation.cs'),
        source_audio=record(ROOT / 'assets/reference/profession-ui/windows-audio-source-manifest.json'),
        operation='Recover the original APK native registration, position/rotation-offset/cache threshold, Awake/OnEnable/Update/OnDestroy order and original registration-failure flag. Bind the three verified collider-free, default-listener UI objects to the existing local five-event PCM host. Serialized prefab fields and source transform are preserved; standalone probe carriers isolate the original three documents only, not the game prefab.',
        limitations=['Only the three verified profession UI object configurations are supported. Non-default listeners, legacy offsets, collider/environment routing and destroy-trigger callbacks fail explicitly and remain outside this port.',
            'The local host uses the original missing-initialization-settings scaling fallback of 1. Spatial audio, general Wwise runtime and game-wide settings mixer are not implemented here.',
            'No full profession prefab or Windows Player validation has been performed.'],
        validated_scope='Pending original field import and Play Mode object lifecycle/audio validation.')
    target = ROOT / 'assets/reference/profession-ui/sound-objects-source-manifest.json'
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    if project:
        project = require_temporary_unity_project(project.resolve())
        dest = project / 'Assets/ProfessionSoundObjectProbe'
        if dest.exists():
            raise ValueError('An existing sound-object probe will not be overwritten')
        dest.mkdir()
        for i, text in enumerate(carriers):
            path = dest / (str(i) + '.prefab')
            path.write_text(text, encoding='utf-8', newline='\n')
            path.with_name(path.name + '.meta').write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs-profession-sound-object-probe-' + str(i)).encode()).hexdigest() + '\nPrefabImporter:\n  externalObjects: {}\n', encoding='utf-8', newline='\n')
    print('Verified original sound-object configurations:', len(rows))
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path)
    build(parser.parse_args().project)
