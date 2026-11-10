"""Verify flat home UI animators, including original motionless states."""
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
        if not trees:
            if actual['m_Motion'] != dict(fileID=0):
                raise ValueError('Original motionless UI state gained a motion')
        else:
            if len(trees) != 1 or len(trees[0]['data']['m_NodeArray']) != 1:
                raise ValueError('Review UI controller blend tree conversion')
            node = trees[0]['data']['m_NodeArray'][0]['data']
            clip = pointer(source['m_AnimationClips'][node['m_ClipID']], owner)
            motion = actual['m_Motion']
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
    selectors = [entry['data'] for entry in machine_source['m_SelectorStateConstantArray']]
    entry_selector, = [entry for entry in selectors if entry['m_IsEntry']]
    entry_transitions = [entry['data'] for entry in entry_selector['m_TransitionConstantArray']]
    # Unity stores the unconditional default fallback after explicit entry edges.
    fallback = entry_transitions[-1]
    if fallback['m_Destination'] != machine_source['m_DefaultState'] or fallback['m_ConditionConstantArray']:
        raise ValueError('Original UI entry fallback differs')
    if len(entry_transitions) != len(machine['m_EntryTransitions']) + 1:
        raise ValueError('Original UI entry transition count differs')
    for expected, found in zip(entry_transitions[:-1], machine['m_EntryTransitions']):
        file_id = found['fileID']
        actual = docs[file_id]['AnimatorTransition']
        if (actual['m_DstState'] != dict(fileID=native_ids[expected['m_Destination']])
                or actual['m_DstStateMachine'] != dict(fileID=0)
                or any(actual[key] for key in ('m_Solo', 'm_Mute', 'm_IsExit'))):
            raise ValueError('Original UI entry destination differs')
        conditions = [entry['data'] for entry in expected['m_ConditionConstantArray']]
        if len(conditions) != len(actual['m_Conditions']):
            raise ValueError('Original UI entry condition count differs')
        for expected_condition, actual_condition in zip(conditions, actual['m_Conditions']):
            if not (equal(expected_condition['m_ConditionMode'], actual_condition['m_ConditionMode'])
                    and equal(names[expected_condition['m_EventID']], actual_condition['m_ConditionEvent'])
                    and equal(expected_condition['m_EventThreshold'], actual_condition['m_EventTreshold'])):
                raise ValueError('Original UI entry condition differs')
        visited[file_id] = 'entry-transition'
    exit_selectors = [entry for entry in selectors if not entry['m_IsEntry']]
    if machine['m_StateMachineTransitions'] or len(exit_selectors) != 1 or exit_selectors[0]['m_TransitionConstantArray'] != [
            dict(data=dict(m_Destination=30000, m_ConditionConstantArray=[]))]:
        raise ValueError('Review original UI exit selector')
    parameters = constants['m_Values']['data']['m_ValueArray']
    if len(parameters) != len(controller['m_AnimatorParameters']):
        raise ValueError('Original UI trigger count differs')
    for expected, found in zip(parameters, controller['m_AnimatorParameters']):
        if expected['m_Type'] != found['m_Type'] or found['m_Name'] != names[expected['m_ID']]:
            raise ValueError('Original UI parameter type/name differs')
        values = constants['m_DefaultValues']['data']
        fields = {1: ('m_FloatValues', 'm_DefaultFloat'), 3: ('m_IntValues', 'm_DefaultInt'),
                  4: ('m_BoolValues', 'm_DefaultBool'), 9: ('m_BoolValues', 'm_DefaultBool')}
        if expected['m_Type'] not in fields:
            raise ValueError('Review original roster parameter type: ' + repr(expected))
        source_field, native_field = fields[expected['m_Type']]
        if not equal(values[source_field][expected['m_Index']], found[native_field]):
            raise ValueError('Original UI parameter default differs')
    if set(docs) - visited.keys() - {100100000}:
        raise ValueError('Unverified original UI animator subassets')
    return visited
