"""Verify restored flat UI animator authoring assets against player constants."""
import math

from prepare_current_partner_native import identity, pointer


def verify_controller(binding, owner, docs, root):
    source = binding.recovery.fields(owner)
    constants = source['m_Controller']
    names = dict(source['m_TOS'])
    controller = docs[root]['AnimatorController']
    if source['m_Name'] != controller['m_Name'] or len(constants['m_LayerArray']) != 1 or len(controller['m_AnimatorLayers']) != 1:
        raise ValueError('Review non-flat original UI controller')
    layer = controller['m_AnimatorLayers'][0]
    machine_id = layer['m_StateMachine']['fileID']
    machine = docs[machine_id]['AnimatorStateMachine']
    machine_source = constants['m_StateMachineArray'][0]['data']
    states = [entry['data'] for entry in machine_source['m_StateConstantArray']]
    native_ids = [entry['m_State']['fileID'] for entry in machine['m_ChildStates']]
    if len(states) != len(native_ids) or machine['m_ChildStateMachines'] or machine['m_StateMachineBehaviours'] or source['m_StateMachineBehaviours']:
        raise ValueError('Review UI controller state/behaviour conversion')
    visited = {root: 'controller', machine_id: 'state-machine'}

    def equal(left, right):
        if isinstance(left, (float, int)) and isinstance(right, (float, int)):
            return math.isclose(left, right, rel_tol=0, abs_tol=1e-6)
        return (left or '') == (right or '')

    def transition(original, file_id):
        actual = docs[file_id]['AnimatorStateTransition']
        if original['m_DestinationState'] >= len(native_ids) or actual['m_DstState']['fileID'] != native_ids[original['m_DestinationState']]:
            raise ValueError('Original UI transition destination differs')
        for key in ('m_TransitionDuration', 'm_TransitionOffset', 'm_ExitTime', 'm_HasExitTime',
                    'm_HasFixedDuration', 'm_InterruptionSource', 'm_OrderedInterruption', 'm_CanTransitionToSelf'):
            if not equal(original[key], actual[key]):
                raise ValueError('Original UI transition field differs: ' + key)
        conditions = [entry['data'] for entry in original['m_ConditionConstantArray']]
        if len(conditions) != len(actual['m_Conditions']):
            raise ValueError('UI transition condition count differs')
        for expected, found in zip(conditions, actual['m_Conditions']):
            if not (equal(expected['m_ConditionMode'], found['m_ConditionMode'])
                    and equal(names[expected['m_EventID']], found['m_ConditionEvent'])
                    and equal(expected['m_EventThreshold'], found['m_EventTreshold'])):
                raise ValueError('Original UI transition condition differs')
        visited[file_id] = 'transition:' + names[original['m_ID']]

    for original, file_id in zip(states, native_ids):
        actual = docs[file_id]['AnimatorState']
        if actual['m_Name'] != names[original['m_NameID']]:
            raise ValueError('Original UI state order/name differs')
        for key in ('m_Speed', 'm_CycleOffset', 'm_IKOnFeet', 'm_WriteDefaultValues', 'm_Mirror'):
            if not equal(original[key], actual[key]):
                raise ValueError('Original UI state field differs: ' + key)
        for original_key, actual_key in (('m_TagID', 'm_Tag'), ('m_SpeedParamID', 'm_SpeedParameter'),
                ('m_MirrorParamID', 'm_MirrorParameter'), ('m_CycleOffsetParamID', 'm_CycleOffsetParameter'), ('m_TimeParamID', 'm_TimeParameter')):
            if not equal(names[original[original_key]], actual[actual_key]):
                raise ValueError('Original UI state parameter differs')
        trees = original['m_BlendTreeConstantArray']
        motion = actual['m_Motion']
        if not trees:
            if motion != {'fileID': 0}:
                raise ValueError('Original empty UI state has an authoring motion')
        elif len(trees) != 1 or len(trees[0]['data']['m_NodeArray']) != 1:
            raise ValueError('Review UI controller blend tree conversion')
        else:
            node = trees[0]['data']['m_NodeArray'][0]['data']
            clip = pointer(source['m_AnimationClips'][node['m_ClipID']], owner)
            existing = binding.objects.get(motion['guid'])
            if existing is not None and identity(existing) != identity(clip):
                raise ValueError('Original UI motion GUID maps to multiple clips')
            binding.objects[motion['guid']] = clip
        transitions = [entry['data'] for entry in original['m_TransitionConstantArray']]
        if len(transitions) != len(actual['m_Transitions']):
            raise ValueError('UI state transition count differs')
        for expected, found in zip(transitions, actual['m_Transitions']):
            transition(expected, found['fileID'])
        visited[file_id] = 'state:' + actual['m_Name']
    any_transitions = [entry['data'] for entry in machine_source['m_AnyStateTransitionConstantArray']]
    if len(any_transitions) != len(machine['m_AnyStateTransitions']):
        raise ValueError('UI any-state transition count differs')
    for expected, found in zip(any_transitions, machine['m_AnyStateTransitions']):
        transition(expected, found['fileID'])
    if machine['m_DefaultState']['fileID'] != native_ids[machine_source['m_DefaultState']]:
        raise ValueError('Original UI default state differs')
    entries = machine['m_EntryTransitions']
    if entries:
        selectors = machine_source['m_SelectorStateConstantArray']
        entry, = [row['data'] for row in selectors if row['data']['m_IsEntry']]
        exit_selector, = [row['data'] for row in selectors if not row['data']['m_IsEntry']]
        binding_id = constants['m_LayerArray'][0]['data']['m_Binding']
        expected_exit = dict(m_TransitionConstantArray=[dict(data=dict(m_Destination=30000,m_ConditionConstantArray=[]))],
            m_FullPathID=binding_id,m_IsEntry=False)
        original_entries = [row['data'] for row in entry['m_TransitionConstantArray']]
        if (exit_selector != expected_exit or entry['m_FullPathID'] != binding_id
            or len(original_entries) != len(entries)+1
            or original_entries[0] != dict(m_Destination=machine_source['m_DefaultState'],m_ConditionConstantArray=[])):
            raise ValueError('Original UI entry/exit selector differs')
        for expected, found in zip(original_entries[1:],entries):
            file_id=found['fileID'];actual=docs[file_id]['AnimatorTransition']
            conditions=[row['data'] for row in expected['m_ConditionConstantArray']]
            if (actual['m_DstState'] != dict(fileID=native_ids[expected['m_Destination']])
                or actual['m_DstStateMachine'] != dict(fileID=0) or actual['m_Solo'] or actual['m_Mute'] or actual['m_IsExit']
                or len(conditions)!=len(actual['m_Conditions'])):
                raise ValueError('Original UI entry transition differs')
            for condition, converted in zip(conditions,actual['m_Conditions']):
                if (condition['m_ConditionMode']!=converted['m_ConditionMode']
                    or names[condition['m_EventID']]!=converted['m_ConditionEvent']
                    or not equal(condition['m_EventThreshold'],converted['m_EventTreshold'])):
                    raise ValueError('Original UI entry condition differs')
            visited[file_id]='entry-transition'
    parameters = constants['m_Values']['data']['m_ValueArray']
    if len(parameters) != len(controller['m_AnimatorParameters']):
        raise ValueError('Original UI parameter count differs')
    for expected, found in zip(parameters, controller['m_AnimatorParameters']):
        if expected['m_Type'] not in (4, 9) or found['m_Type'] != expected['m_Type'] or found['m_Name'] != names[expected['m_ID']]:
            raise ValueError('Original UI bool/trigger parameter differs')
        if expected['m_Type'] == 4:
            defaults = constants['m_DefaultValues']['data']['m_BoolValues']
            if (found['m_DefaultBool'] != defaults[expected['m_Index']]
                    or found['m_DefaultFloat'] != 0 or found['m_DefaultInt'] != 0):
                raise ValueError('Original UI bool parameter default differs')
    if set(docs) - visited.keys() - {100100000}:
        raise ValueError('Unverified original UI animator subassets: '+source['m_Name']+' '+str({key:value for key,value in docs.items() if key not in visited and key!=100100000}))
    return visited
