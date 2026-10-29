"""Verify native invitation state graphs against original compiled Mecanim data."""
import zlib
import argparse
import json
import hashlib
import copy
from pathlib import Path
from official_apk import Apk, ROOT
import UnityPy
from UnityPy.classes.PPtr import PPtr
from prepare_unity_map12_matching_animation import equal
from prepare_unity_bundle_inputs import serialized_files
from prepare_unity_map12_sources import encode, sha, source_path


def verify_controller(obj, docs, guid_identities):
    original = obj.read_typetree()
    controller, = [fields for kind, fields in docs.values() if kind == 'AnimatorController']
    constants = original['m_Controller']
    names = dict(original['m_TOS'])
    name = lambda key: names[key] if key else ''
    eq = lambda a, b, label: equal(a, b, obj.peek_name() + '/' + label)
    eq(len(constants['m_LayerArray']), len(controller['m_AnimatorLayers']), 'layers')
    eq([], original['m_StateMachineBehaviours'], 'behaviours')
    parameters = constants['m_Values']['data']['m_ValueArray']
    eq(len(parameters), len(controller['m_AnimatorParameters']), 'parameters')
    defaults = constants['m_DefaultValues']['data']
    for source, target in zip(parameters, controller['m_AnimatorParameters']):
        eq(name(source['m_ID']), target['m_Name'], 'parameter name')
        eq(source['m_Type'], target['m_Type'], 'parameter type')
        value_type = {1: ('m_FloatValues', 'm_DefaultFloat'), 3: ('m_IntValues', 'm_DefaultInt'),
                      4: ('m_BoolValues', 'm_DefaultBool'), 9: ('m_BoolValues', 'm_DefaultBool')}
        if source['m_Type'] not in value_type:
            raise ValueError('Inspect original invitation parameter storage')
        storage, field = value_type[source['m_Type']]
        eq(defaults[storage][source['m_Index']], target[field], 'parameter default')
    counts = dict(layers=0, parameters=len(parameters), states=0, transitions=0, conditions=0, entryTransitions=0,
                  originalCompiledLoopFlags={})
    for layer_pointer, out_layer in zip(constants['m_LayerArray'], controller['m_AnimatorLayers']):
        layer = layer_pointer['data']
        eq(name(layer['m_Binding']), out_layer['m_Name'], 'layer name')
        for source_key, target_key in (('(int&)m_LayerBlendingMode', 'm_BlendingMode'),
                ('m_DefaultWeight', 'm_DefaultWeight'), ('m_IKPass', 'm_IKPass'),
                ('m_SyncedLayerAffectsTiming', 'm_SyncedLayerAffectsTiming')):
            eq(layer[source_key], out_layer[target_key], 'layer/' + source_key)
        eq(-1, out_layer['m_SyncedLayerIndex'], 'unsynchronized layer')
        machine = constants['m_StateMachineArray'][layer['m_StateMachineIndex']]['data']
        native_machine = docs[out_layer['m_StateMachine']['fileID']][1]
        states = [entry['data'] for entry in machine['m_StateConstantArray']]
        ids = [entry['m_State']['fileID'] for entry in native_machine['m_ChildStates']]
        eq(len(states), len(ids), 'states')
        eq(ids[machine['m_DefaultState']] if ids else 0, native_machine['m_DefaultState']['fileID'], 'default state')
        eq([], native_machine['m_ChildStateMachines'], 'nested machines')
        selectors = [entry['data'] for entry in machine['m_SelectorStateConstantArray']]
        entries = [entry for entry in selectors if entry['m_IsEntry']]
        if entries:
            entry, = entries
            eq(zlib.crc32(out_layer['m_Name'].encode()), entry['m_FullPathID'], 'entry path')
            transitions_with_default = entry['m_TransitionConstantArray']
            fallback = transitions_with_default[-1]['data']
            eq([], fallback['m_ConditionConstantArray'], 'default entry conditions')
            eq(machine['m_DefaultState'], fallback['m_Destination'], 'default entry destination')
            explicit = transitions_with_default[:-1]
        else:
            explicit = []
        eq(len(explicit), len(native_machine['m_EntryTransitions']), 'entry transitions')
        for entry, pointer in zip(explicit, native_machine['m_EntryTransitions']):
            source, target = entry['data'], docs[pointer['fileID']][1]
            eq(ids[source['m_Destination']], target['m_DstState']['fileID'], 'entry destination')
            eq(False, bool(target['m_IsExit']), 'entry exit')
            eq(len(source['m_ConditionConstantArray']), len(target['m_Conditions']), 'entry conditions')
            for condition, value in zip(source['m_ConditionConstantArray'], target['m_Conditions']):
                condition = condition['data']
                eq(condition['m_ConditionMode'], value['m_ConditionMode'], 'entry condition mode')
                eq(name(condition['m_EventID']), value['m_ConditionEvent'], 'entry condition parameter')
                eq(condition['m_EventThreshold'], value['m_EventTreshold'], 'entry condition threshold')
                counts['conditions'] += 1
            counts['entryTransitions'] += 1

        def transitions(source, pointers):
            eq(len(source), len(pointers), 'transitions')
            for entry, pointer in zip(source, pointers):
                a, b = entry['data'], docs[pointer['fileID']][1]
                for suffix in ('Duration', 'Offset', 'ExitTime', 'HasExitTime', 'HasFixedDuration',
                               'InterruptionSource', 'OrderedInterruption', 'CanTransitionToSelf'):
                    key = 'm_Transition' + suffix if suffix in ('Duration', 'Offset') else 'm_' + suffix
                    eq(a[key], b[key], 'transition/' + suffix)
                destination = a['m_DestinationState']
                eq(destination >= 30000, bool(b['m_IsExit']), 'transition exit')
                eq(ids[destination] if destination < 30000 else 0, b['m_DstState']['fileID'], 'destination')
                conditions = a['m_ConditionConstantArray']
                eq(len(conditions), len(b['m_Conditions']), 'conditions')
                for condition, target in zip(conditions, b['m_Conditions']):
                    value = condition['data']
                    eq(value['m_ConditionMode'], target['m_ConditionMode'], 'condition mode')
                    eq(name(value['m_EventID']), target['m_ConditionEvent'], 'condition parameter')
                    eq(value['m_EventThreshold'], target['m_EventTreshold'], 'condition threshold')
                    counts['conditions'] += 1
                counts['transitions'] += 1

        transitions(machine['m_AnyStateTransitionConstantArray'], native_machine['m_AnyStateTransitions'])
        for source, state_id in zip(states, ids):
            target = docs[state_id][1]
            eq(name(source['m_NameID']), target['m_Name'], 'state name')
            eq(zlib.crc32((out_layer['m_Name'] + '.' + target['m_Name']).encode()), source['m_FullPathID'], 'state path')
            for key in ('m_Speed', 'm_CycleOffset', 'm_IKOnFeet', 'm_WriteDefaultValues', 'm_Mirror'):
                eq(source[key], target[key], 'state/' + key)
            eq(name(source['m_TagID']), target['m_Tag'] or '', 'state tag')
            for prefix in ('Speed', 'Mirror', 'CycleOffset', 'Time'):
                parameter = source['m_' + prefix + 'ParamID']
                eq(name(parameter), target['m_' + prefix + 'Parameter'] or '', 'state parameter')
                eq(bool(parameter), bool(target['m_' + prefix + 'ParameterActive']), 'state parameter active')
            trees = source['m_BlendTreeConstantArray']
            if not trees:
                eq(0, target['m_Motion']['fileID'], 'empty motion')
            else:
                eq(1, len(trees), 'motion trees')
                nodes = trees[0]['data']['m_NodeArray']
                eq(1, len(nodes), 'motion leaves')
                leaf = nodes[0]['data']
                eq([], leaf['m_ChildIndices'], 'leaf children')
                clip = PPtr(**original['m_AnimationClips'][leaf['m_ClipID']], assetsfile=obj.assets_file).deref()
                identity = clip.assets_file.name + ':' + str(clip.path_id)
                eq(identity, guid_identities[target['m_Motion']['guid']], 'motion identity')
                # A compiled state loop flag is not the clip's loop-time flag.
                # Preserve the original for the Windows compiled-state check.
                counts['originalCompiledLoopFlags'][target['m_Name']] = source['m_Loop']
            transitions(source['m_TransitionConstantArray'], target['m_Transitions'])
            counts['states'] += 1
        counts['layers'] += 1
    return counts


def verify_windows(path):
    source = json.loads((ROOT / 'unity/Assets/Official/World/Map/12/Source/team-invite-animation-native.json').read_bytes())
    env = UnityPy.load(str(path))
    actual = {}
    for obj in env.objects:
        if obj.type.name == 'AnimatorController':
            if int(obj.assets_file.target_platform) != 19:
                raise ValueError('Invitation animation bundle is not native Windows')
            actual[obj.peek_name()] = obj
    apk = Apk.current()
    for row in source['files']:
        apk.load(row['source']['bundle'])
    checked = 0
    for row in source['files']:
        if not row['path'].endswith('.controller'):
            continue
        bundle = apk.load(row['source']['bundle'])
        original, = [obj for file in serialized_files(bundle) for obj in file.objects.values()
                     if file.name + ':' + str(obj.path_id) == row['identity']]
        a, b = original.read_typetree(), actual[original.peek_name()].read_typetree()
        original_clips = [PPtr(**pointer, assetsfile=original.assets_file).read().m_Name for pointer in a['m_AnimationClips']]
        windows_clips = [PPtr(**pointer, assetsfile=actual[original.peek_name()].assets_file).read().m_Name for pointer in b['m_AnimationClips']]
        if len(original_clips) != len(set(original_clips)) or sorted(original_clips) != sorted(windows_clips):
            raise ValueError('Windows controller original clip identities differ')
        clip_indices = {index: original_clips.index(name) for index, name in enumerate(windows_clips)}
        def original_clip_order(value):
            if isinstance(value, dict):
                return {key: clip_indices[item] if key == 'm_ClipID' else original_clip_order(item)
                        for key, item in value.items()}
            if isinstance(value, list):
                return [original_clip_order(item) for item in value]
            return value
        equal(a['m_Controller'], original_clip_order(b['m_Controller']), original.peek_name() + '/Windows compiled controller')
        equal({str(key): value for key, value in a['m_TOS']},
              {str(key): value for key, value in b['m_TOS']}, original.peek_name() + '/Windows compiled names')
        checked += 1
    if checked != 3 or len(actual) != 3:
        raise ValueError('Windows invitation controller coverage differs')
    print('Windows invitation compiled controller fields verified:', checked, flush=True)


def pin_windows(path):
    from validation_workspace import validation_directory
    if not path.resolve().is_relative_to(validation_directory()):
        raise ValueError('Use this session\'s real Unity Windows animation build')
    source_path_ = ROOT / 'unity/Assets/Official/World/Map/12/Source/team-invite-animation-native.json'
    source = json.loads(source_path_.read_bytes())
    raw = path.read_bytes()
    env = UnityPy.load(raw)
    bundle, = env.files.values()
    if any(int(file.target_platform) != 19 for file in serialized_files(bundle)):
        raise ValueError('The original animator carrier must be Unity-built Windows data')
    targets = {(obj.type.name, obj.peek_name()): obj for obj in env.objects
               if obj.type.name in ('AnimationClip', 'AnimatorController', 'AnimatorOverrideController')}
    if len(targets) != len(source['files']) or len(targets) != 18:
        raise ValueError('Windows animation source closure differs')
    windows_scripts = {}
    for obj in env.objects:
        if obj.type.name == 'MonoScript':
            script = obj.read()
            windows_scripts[(script.m_AssemblyName.removesuffix('.dll'), script.m_Namespace, script.m_ClassName)] = obj
    apk = Apk.current()
    for row in source['files']:
        apk.load(row['source']['bundle'])
    originals = {}
    for row in source['files']:
        original, = [obj for file in serialized_files(apk.loaded[row['source']['bundle']]) for obj in file.objects.values()
                     if file.name + ':' + str(obj.path_id) == row['identity']]
        if sha(original.get_raw_data()) != row['payloadSha256']:
            raise ValueError('Original animation payload changed')
        originals[row['identity']] = original
    target_by_identity = {key: targets[(obj.type.name, obj.peek_name())] for key, obj in originals.items()}
    records, pointers = [], []

    def relocate(value, owner):
        if isinstance(value, dict):
            if set(value) == {'m_FileID', 'm_PathID'}:
                if not value['m_PathID']:
                    return dict(m_FileID=0, m_PathID=0)
                pointer = PPtr(**value, assetsfile=owner.assets_file)
                original = pointer.deref()
                identity = original.assets_file.name + ':' + str(original.path_id)
                if original.type.name == 'MonoScript':
                    script = original.read()
                    script_key = (script.m_AssemblyName.removesuffix('.dll'), script.m_Namespace, script.m_ClassName)
                    target = windows_scripts[script_key]
                else:
                    target = target_by_identity[identity]
                pointers.append(dict(original=identity, kind=original.type.name, windowsPathId=str(target.path_id)))
                return dict(m_FileID=0, m_PathID=target.path_id)
            return {key: relocate(item, owner) for key, item in value.items()}
        if isinstance(value, list):
            return [relocate(item, owner) for item in value]
        return value

    for identity, original in originals.items():
        original_payload = original.get_raw_data()
        fields = original.read_typetree(check_read=True)
        if original.save_typetree(fields) != original_payload:
            raise ValueError('Original animation serialization round trip changed source bytes')
        converted = relocate(copy.deepcopy(fields), original)
        payload = original.save_typetree(converted)
        target = target_by_identity[identity]
        target.set_raw_data(payload)
        records.append(dict(identity=identity, type=original.type.name, name=original.peek_name(),
            originalPayloadSha256=sha(original_payload), windowsPathId=str(target.path_id),
            windowsPayloadSha256=sha(payload), originalExportPath=next(row['path'] for row in source['files'] if row['identity'] == identity)))
    output = bundle.save(packer='lz4')
    reloaded = UnityPy.load(output)
    restored = {str(obj.path_id): obj for obj in reloaded.objects}
    for record in records:
        obj = restored[record['windowsPathId']]
        if sha(obj.get_raw_data()) != record['windowsPayloadSha256']:
            raise ValueError('Native Windows animation payload changed after packing')
        obj.read_typetree(check_read=True)
    root = ROOT / 'unity/Assets/Official/World/Map/12/NativeTeamAnimations'
    destination = root / 'Resources/Map12/TeamAnimations/native-windows.bytes'
    outputs = {destination: output}
    for file in (destination, destination.parent, destination.parent.parent, destination.parent.parent.parent, root):
        identity = source_path(file)
        guid = hashlib.md5(('zjcs:map12:invite-windows:' + identity).encode()).hexdigest()
        outputs[file.with_name(file.name + '.meta')] = ('fileFormatVersion: 2\nguid: ' + guid +
            ('\nfolderAsset: yes\nDefaultImporter:\n' if file != destination else '\nTextScriptImporter:\n') +
            '  externalObjects: {}\n').encode()
    catalog = destination.with_name('catalog.json')
    outputs[catalog] = encode(dict(manifest=apk.version, bundleSha256=sha(output), assets=records))
    outputs[catalog.with_name(catalog.name + '.meta')] = ('fileFormatVersion: 2\nguid: ' +
        hashlib.md5(b'zjcs:map12:invite-windows-catalog').hexdigest() + '\nTextScriptImporter:\n  externalObjects: {}\n').encode()
    for file, payload in outputs.items():
        file.parent.mkdir(parents=True, exist_ok=True)
        file.write_bytes(payload)
    receipt = ROOT / 'assets/reference/official-map/map12-team-invite-windows-animation-source-manifest.json'
    receipt.write_bytes(encode(dict(format=1, manifest=apk.version, mapClassId=12,
        source=dict(path=source_path(source_path_), sha256=sha(source_path_.read_bytes())),
        unityWindowsBuildSha256=sha(raw), platform=19, originalObjects=records, relocatedPointers=pointers,
        generator=dict(path=source_path(Path(__file__).resolve()), sha256=sha(Path(__file__).read_bytes())),
        unityBuildEntry=dict(path='unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.InviteAnimations.cs',
            sha256=sha((ROOT / 'unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.InviteAnimations.cs').read_bytes())),
        outputs=[dict(path=source_path(file), sha256=sha(payload)) for file, payload in outputs.items()],
        operation='Use the real Unity StandaloneWindows64 container, native type metadata and MonoScripts. '
                  'Restore every original AnimationClip/controller/override payload with only explicit local '
                  'AnimationClip, AnimatorController and Unity Image/TMP MonoScript pointer relocation. '
                  'Preserve original compressed channels and compiled state-loop flags instead of accepting '
                  'Unity authoring recompile changes. Verify original byte serialization, output hashes and '
                  'complete consumption with the actual Windows type metadata.',
        limitations=['Native Windows load and 14-clip/320-value Animator binding verification passed in DX12 Play Mode; existing TeamPanel two-size simulated-click regression passed. Formal invitation prefab, NPC appearance and local party integration remain pending.'])))
    verify_windows(destination)
    print('Pinned original invitation animation payloads into native Windows carrier:', len(records), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--windows-bundle', type=Path)
    parser.add_argument('--pin-windows-bundle', type=Path)
    args = parser.parse_args()
    if args.pin_windows_bundle:
        pin_windows(args.pin_windows_bundle)
    elif args.windows_bundle:
        verify_windows(args.windows_bundle)
    else:
        parser.error('Specify a Windows bundle to verify or pin')
