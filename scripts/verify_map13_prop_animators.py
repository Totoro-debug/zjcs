"""Compare restored Mecanim YAML with original compiled Map13 state machines."""
import json
from pathlib import Path
import struct
import zlib
from collections import Counter

import official_apk
from official_native_yaml_verification import documents
from prepare_unity_map13_prop_animators import ROOT, RECEIPT, identity, sha
import UnityPy


def verify():
    receipt = json.loads(RECEIPT.read_bytes())
    original = json.loads((ROOT / receipt['source']['path']).read_bytes())
    assert sha((ROOT / receipt['source']['path']).read_bytes()) == receipt['source']['sha256']
    output = {r['key']: r for r in receipt['outputs']}
    objects = {(r['cab'].lower(), r['path_id']): r for r in original['objects']}
    counts = Counter()

    def eq(a, b, context):
        if isinstance(a, float):
            same = struct.pack('<f', a) == struct.pack('<f', b)
        else:
            same = a == b
        if not same:
            raise ValueError(context + ': ' + str((a, b)))
        counts['fields'] += 1

    for record in original['objects']:
        dest = output[record['key']]
        raw = (ROOT / dest['path']).read_bytes()
        eq(dest['sha256'], sha(raw), 'asset hash')
        eq(dest['meta_sha256'], sha(Path(str(ROOT / dest['path']) + '.meta').read_bytes()), 'meta hash')
        docs = documents(raw.decode())
        f = record['fields']
        data = next(v for k, v in docs.values() if k == record['kind'])
        eq(f['m_Name'], data['m_Name'], 'native name')
        if record['kind'] == 'AnimationClip':
            for key in ('m_Legacy', 'm_Compressed', 'm_UseHighQualityCurve', 'm_SampleRate',
                        'm_WrapMode', 'm_HasGenericRootTransform', 'm_HasMotionFloatCurves', 'm_Events'):
                eq(f[key], data[key], 'clip.' + key)
            muscle, settings = f['m_MuscleClip'], data['m_AnimationClipSettings']
            for key in ('m_StartTime', 'm_StopTime', 'm_OrientationOffsetY', 'm_Level', 'm_CycleOffset',
                        'm_LoopTime', 'm_LoopBlend', 'm_LoopBlendOrientation', 'm_LoopBlendPositionY',
                        'm_LoopBlendPositionXZ', 'm_KeepOriginalOrientation', 'm_KeepOriginalPositionY',
                        'm_KeepOriginalPositionXZ', 'm_HeightFromFeet', 'm_Mirror'):
                eq(muscle[key], settings[key], 'clip settings.' + key)
            expected = f['m_ClipBindingConstant']['genericBindings']
            actual = data['m_ClipBindingConstant']['genericBindings']
            eq(len(expected), len(actual), 'clip bindings count')
            for a, b in zip(expected, actual):
                for key in a:
                    if key == 'script':
                        eq(0, b[key]['fileID'], 'native null script')
                    else:
                        eq(a[key], b[key], 'native binding.' + key)
            if len(expected) == 1 and expected[0]['attribute'] == zlib.crc32(b'dummy'):
                curves = data['m_FloatCurves']
                eq(1, len(curves), 'dummy curve count')
                curve = curves[0]
                eq('dummy', curve['attribute'], 'dummy property')
                eq(None, curve['path'], 'dummy root')
                eq(1, curve['classID'], 'dummy GameObject')
                for key in curve['curve']['m_Curve']:
                    eq(0, key['value'], 'dummy constant value')
                counts['dummy_clips'] += 1
            counts['clips'] += 1
            continue
        c = f['m_Controller']
        tos = dict(f['m_TOS'])
        name = lambda key: tos[key] if key else ''
        eq(1, len(c['m_LayerArray']), 'original layer count')
        eq(1, len(c['m_StateMachineArray']), 'original machine count')
        eq([], f['m_StateMachineBehaviours'], 'original behaviours')
        layer = c['m_LayerArray'][0]['data']
        outlayer = data['m_AnimatorLayers'][0]
        eq(name(layer['m_Binding']), outlayer['m_Name'], 'layer name')
        for a, b in (('(int&)m_LayerBlendingMode', 'm_BlendingMode'), ('m_DefaultWeight', 'm_DefaultWeight'),
                     ('m_IKPass', 'm_IKPass'), ('m_SyncedLayerAffectsTiming', 'm_SyncedLayerAffectsTiming')):
            eq(layer[a], outlayer[b], 'layer.' + a)
        eq(-1, outlayer['m_SyncedLayerIndex'], 'self synchronized layer')
        params = c['m_Values']['data']['m_ValueArray']
        eq(len(params), len(data['m_AnimatorParameters']), 'parameter count')
        defaults = c['m_DefaultValues']['data']
        for a, b in zip(params, data['m_AnimatorParameters']):
            eq(name(a['m_ID']), b['m_Name'], 'parameter name')
            eq(a['m_Type'], b['m_Type'], 'parameter type')
            eq(False, b['m_DefaultBool'], 'default bool')
            if a['m_Type'] == 4:
                eq(defaults['m_BoolValues'][a['m_Index']], b['m_DefaultBool'], 'original bool')
            elif a['m_Type'] != 9:
                raise ValueError('Additional parameter type requires exact verification')
            counts['parameters'] += 1
        sm = c['m_StateMachineArray'][0]['data']
        outsm = docs[outlayer['m_StateMachine']['fileID']][1]
        states = [s['data'] for s in sm['m_StateConstantArray']]
        ids = [s['m_State']['fileID'] for s in outsm['m_ChildStates']]
        eq(len(states), len(ids), 'state count')
        eq(ids[sm['m_DefaultState']] if ids else 0, outsm['m_DefaultState']['fileID'], 'default state')
        eq([], outsm['m_ChildStateMachines'], 'nested machines')
        eq([], outsm['m_EntryTransitions'], 'entry transitions')

        def transitions(source, refs):
            eq(len(source), len(refs), 'transition count')
            for original_transition, ref in zip(source, refs):
                a, b = original_transition['data'], docs[ref['fileID']][1]
                for suffix in ('Duration', 'Offset', 'ExitTime', 'HasExitTime', 'HasFixedDuration',
                               'InterruptionSource', 'OrderedInterruption', 'CanTransitionToSelf'):
                    destkey = 'm_Transition' + suffix if suffix in ('Duration', 'Offset') else 'm_' + suffix
                    eq(a[destkey], b[destkey], 'transition.' + suffix)
                destination = a['m_DestinationState']
                eq(destination >= 30000, bool(b['m_IsExit']), 'transition exit')
                eq(ids[destination] if destination < 30000 else 0, b['m_DstState']['fileID'], 'destination')
                conditions = a['m_ConditionConstantArray']
                eq(len(conditions), len(b['m_Conditions']), 'condition count')
                for condition, target in zip(conditions, b['m_Conditions']):
                    v = condition['data']
                    eq(v['m_ConditionMode'], target['m_ConditionMode'], 'condition mode')
                    eq(name(v['m_EventID']), target['m_ConditionEvent'], 'condition parameter')
                    eq(v['m_EventThreshold'], target['m_EventTreshold'], 'condition threshold')
                    counts['conditions'] += 1
                counts['transitions'] += 1
                if a['m_TransitionDuration'] != 0: counts['nonzero_transitions'] += 1

        transitions(sm['m_AnyStateTransitionConstantArray'], outsm['m_AnyStateTransitions'])
        for a, stateid in zip(states, ids):
            b = docs[stateid][1]
            eq(name(a['m_NameID']), b['m_Name'], 'state name')
            eq(zlib.crc32((outlayer['m_Name'] + '.' + b['m_Name']).encode()), a['m_FullPathID'], 'full path hash')
            for key in ('m_Speed', 'm_CycleOffset', 'm_IKOnFeet', 'm_WriteDefaultValues', 'm_Mirror'):
                eq(a[key], b[key], 'state.' + key)
            eq(name(a['m_TagID']), b['m_Tag'] or '', 'state tag')
            for prefix in ('Speed', 'Mirror', 'CycleOffset', 'Time'):
                eq(0, a['m_' + prefix + 'ParamID'], 'original parameter inactive')
                eq(0, b['m_' + prefix + 'ParameterActive'], 'parameter inactive')
            trees = a['m_BlendTreeConstantArray']
            if not trees:
                eq(0, b['m_Motion']['fileID'], 'empty state motion')
            else:
                eq(1, len(trees), 'blend tree count')
                nodes = trees[0]['data']['m_NodeArray']
                eq(1, len(nodes), 'leaf count')
                leaf = nodes[0]['data']
                eq([], leaf['m_ChildIndices'], 'leaf children')
                ref = f['m_AnimationClips'][leaf['m_ClipID']]
                eq(0, ref['m_FileID'], 'clip CAB index')
                clip = objects[(record['cab'].lower(), str(ref['m_PathID']))]
                eq(output[clip['key']]['guid'], str(b['m_Motion']['guid']), 'clip original identity')
                eq(a['m_Loop'], clip['fields']['m_MuscleClip']['m_LoopTime'], 'state loop')
            transitions(a['m_TransitionConstantArray'], b['m_Transitions'])
            counts['states'] += 1
        counts['controllers'] += 1
    print('Original Map13 Mecanim verification:', dict(counts), flush=True)
    native = receipt.get('native_windows_bundle')
    if native:
        raw = (ROOT / native['path']).read_bytes()
        eq(native['sha256'], sha(raw), 'Windows bundle hash')
        env = UnityPy.load(raw)
        bundle = next(iter(env.files.values()))
        file = next(iter(bundle.files.values()))
        eq(19, int(file.target_platform), 'Windows native platform')
        objmap = {str(o.path_id): o for o in env.objects}
        records = {r['key']: r for r in original['objects']}
        relocated = {r['key']: int(r['windows_path_id']) for r in native['objects']}
        for entry in native['objects']:
            obj = objmap[entry['windows_path_id']]
            eq(entry['windows_payload_sha256'], sha(obj.get_raw_data()), 'Windows payload hash')
            record = records[entry['key']]
            if record['kind'] == 'AnimationClip':
                eq(record['payload_sha256'], sha(obj.get_raw_data()), 'exact original AnimationClip bytes')
                counts['byte_identical_windows_clips'] += 1
            else:
                data = obj.read_typetree(check_read=True)
                for source_ref, windows_ref in zip(record['fields']['m_AnimationClips'], data['m_AnimationClips']):
                    eq(relocated[identity(record['cab'], source_ref['m_PathID'])], windows_ref['m_PathID'], 'Windows clip relocation')
                data['m_AnimationClips'] = record['fields']['m_AnimationClips']
                eq(record['fields'], json.loads(json.dumps(data)), 'complete original controller fields')
                counts['exact_windows_controllers'] += 1
        print('Original Windows native payload verification:', dict(counts), flush=True)
    return dict(counts)


if __name__ == '__main__': verify()
