"""Shared exact prefab/resource binding for native home entities."""
import hashlib
from collections import Counter
from pathlib import Path

from home_native_assets import write_staged_assets
from official_apk import ROOT
from prepare_current_partner_native import documents, identity
from prepare_partner_card_native import guid, sha, script_id


def register_script(binding, full_name, path):
    namespace, _, name = full_name.rpartition('.')
    pointer = dict(fileID=11500000, guid=guid(Path(str(path) + '.meta')), type=3)
    binding.scripts[full_name] = pointer
    binding.class_ids[script_id(namespace, name)] = pointer
    binding.script_guids.add(pointer['guid'])
    binding.script_sources[full_name] = dict(path=path.relative_to(ROOT).as_posix(), pointer=pointer,
        sha256=sha(path.read_bytes()), meta_sha256=sha(Path(str(path) + '.meta').read_bytes()))


def namespace_loaded(binding, result, scope):
    if result['guid'].startswith('0000000000000000') or result['guid'] in binding.script_guids:
        return result
    old = result['guid']; token = hashlib.md5((scope + ':' + old).encode()).hexdigest()
    result['guid'] = token
    binding.queue = [(path, token if key == old else key, value) for path, key, value in binding.queue]
    for proof in binding.proof.values():
        if proof.get('guid') == old:
            proof['guid'] = token
    binding.script_guids.add(token)
    return result


def load_text(binding, source_guid, obj):
    value = obj.read(); raw = value.m_Script.encode('utf8', 'surrogateescape')
    name = value.m_Name
    if any(char in name for char in '/\\:*?"<>|') or not name:
        raise ValueError('Preserve the exact original TextAsset name with an explicit safe importer: ' + name)
    key = identity(obj)
    # .bytes strips only its own extension, retaining the source .skel/.atlas
    # name used by the actual Spine binary/text parser selection.
    target = Path('Text') / sha(key.encode())[:16] / (name + '.bytes')
    token = hashlib.md5(('zjcs:home-native-text:' + source_guid).encode()).hexdigest()
    pointer = dict(fileID=4900000, guid=token, type=3)
    binding.redirects[source_guid] = pointer; binding.queue.append((target, token, raw))
    binding.proof[key] = dict(source=binding.recovery.apk.provenance(obj), type='TextAsset',
        original_sha256=sha(obj.get_raw_data()), bytes_sha256=sha(raw), original_name=name,
        output=target.as_posix(), guid=token)
    return pointer


def load_mesh(binding, source_guid, obj):
    from native_spine_assets import NativeSpineBinding
    path, raw = NativeSpineBinding.checked_native(binding, source_guid, obj)
    data, = documents(raw.decode('utf8')).values(); native = data['Mesh']; source = binding.recovery.fields(obj)
    vertex = source['m_VertexData']; actual = native['m_VertexData']
    if (bytes.fromhex(native['m_IndexBuffer']) != bytes(source['m_IndexBuffer'])
        or actual['m_VertexCount'] != vertex['m_VertexCount'] or actual['m_Channels'] != vertex['m_Channels']
        or actual['m_DataSize'] != len(vertex['m_DataSize']) or bytes.fromhex(actual['_typelessdata']) != bytes(vertex['m_DataSize'])
        or native['m_IndexFormat'] != source['m_IndexFormat'] or native['m_MeshCompression'] != source['m_MeshCompression']):
        raise ValueError('Original native home mesh geometry differs')
    def external(value):
        if isinstance(value, dict):
            return bool(value.get('guid')) or any(external(item) for item in value.values())
        return isinstance(value, list) and any(external(item) for item in value)
    if external(data):
        raise ValueError('Bind native home mesh external references before preserving its payload')
    target = path.relative_to(binding.native)
    token = hashlib.md5(('zjcs:partner-current:' + source_guid).encode()).hexdigest()
    result = dict(fileID=4300000, guid=token, type=2)
    binding.redirects[source_guid] = result
    # The verified exporter preserves Unity's special hex and empty-buffer
    # scalars. A generic YAML round trip changes their native interpretation.
    binding.queue.append((target, token, raw))
    binding.proof[identity(obj)] = dict(source=binding.recovery.apk.provenance(obj), type='Mesh',
        original_sha256=sha(obj.get_raw_data()), native_path=path.relative_to(ROOT).as_posix(),
        output=target.as_posix(), guid=token, payload_sha256=sha(raw), geometry=dict(
            vertices=vertex['m_VertexCount'], vertex_bytes=len(vertex['m_DataSize']),
            vertex_sha256=sha(bytes(vertex['m_DataSize'])), index_sha256=sha(bytes(source['m_IndexBuffer']))))
    return result


def load_controller(binding, source_guid, obj):
    from partner_gift_animator_binding import verify_controller
    native = binding.native_path(source_guid, obj); raw = native.read_bytes()
    key = native.relative_to(binding.cache / binding.recovery.export_name).as_posix()
    if sha(raw) != binding.checked[key] or sha(Path(str(native) + '.meta').read_bytes()) != binding.checked[key + '.meta']:
        raise ValueError('Native home animator payload differs from selected conversion')
    binding.inputs[native.relative_to(ROOT).as_posix()] = sha(raw)
    binding.inputs[native.relative_to(ROOT).as_posix() + '.meta'] = sha(Path(str(native) + '.meta').read_bytes())
    docs = documents(raw.decode('utf8')); root, = [key for key, value in docs.items() if 'AnimatorController' in value]
    source = binding.recovery.fields(obj)
    machines = source['m_Controller']['m_StateMachineArray']
    if len(machines) == 1 and not machines[0]['data']['m_StateConstantArray']:
        visited = verify_empty_controller(source, docs, root)
    else:
        visited = verify_controller(binding, obj, docs, root)
    target = Path('Controllers') / (sha(identity(obj).encode())[:16] + '.controller')
    token = hashlib.md5(('zjcs:home-native-controller:' + source_guid).encode()).hexdigest()
    pointer = dict(fileID=root, guid=token, type=2)
    binding.redirects[source_guid] = pointer; binding.queue.append((target, token, docs))
    binding.proof[identity(obj)] = dict(source=binding.recovery.apk.provenance(obj),
        original_sha256=sha(obj.get_raw_data()), output=target.as_posix(), guid=token,
        verified_runtime_subassets=visited)
    return pointer


def verify_empty_controller(source, docs, root):
    """Prove original empty Spine authoring controllers, including entry/exit sentinels."""
    controller = docs[root]['AnimatorController']; constants = source['m_Controller']
    if len(constants['m_LayerArray']) != 1 or len(controller['m_AnimatorLayers']) != 1:
        raise ValueError('Review non-flat empty original home controller')
    layer = controller['m_AnimatorLayers'][0]; native_id = layer['m_StateMachine']['fileID']
    actual = docs[native_id]['AnimatorStateMachine']; original = constants['m_StateMachineArray'][0]['data']
    source_layer = constants['m_LayerArray'][0]['data']; names = dict(source['m_TOS'])
    selectors = original['m_SelectorStateConstantArray']
    expected_selectors = [dict(data=dict(m_TransitionConstantArray=[dict(data=dict(m_Destination=destination,
        m_ConditionConstantArray=[]))], m_FullPathID=source_layer['m_Binding'], m_IsEntry=entry))
        for destination, entry in ((4294967295, True), (30000, False))]
    if (source['m_Name'] != controller['m_Name'] or layer['m_Name'] != names[source_layer['m_Binding']]
        or actual['m_Name'] != layer['m_Name'] or selectors != expected_selectors or original['m_DefaultState'] != 0
        or original['m_StateConstantArray'] or original['m_AnyStateTransitionConstantArray']
        or original['m_SynchronizedLayerCount'] != 1 or source['m_AnimationClips'] or source['m_StateMachineBehaviours']
        or source['m_StateMachineBehaviourVectorDescription'] != dict(m_StateMachineBehaviourRanges=[], m_StateMachineBehaviourIndices=[])
        or constants['m_Values']['data']['m_ValueArray'] or any(constants['m_DefaultValues']['data'].values())
        or controller['m_AnimatorParameters'] or layer['m_Motions'] or layer['m_Behaviours'] or layer['m_Mask']['fileID']
        or source_layer['m_StateMachineIndex'] != 0 or source_layer['m_StateMachineSynchronizedLayerIndex'] != 0
        or layer['m_SyncedLayerIndex'] != -1 or source_layer['m_SkeletonMask']['data']['m_Data']
        or source_layer['m_BodyMask'] != dict(word0=4294967295, word1=4294967295, word2=524287)
        or any(source_layer[key] != layer[target] for key, target in (
            ('(int&)m_LayerBlendingMode', 'm_BlendingMode'), ('m_DefaultWeight', 'm_DefaultWeight'),
            ('m_IKPass', 'm_IKPass'), ('m_SyncedLayerAffectsTiming', 'm_SyncedLayerAffectsTiming')))
        or actual['m_DefaultState']['fileID'] or any(actual[key] for key in ('m_ChildStates', 'm_ChildStateMachines',
            'm_AnyStateTransitions', 'm_EntryTransitions', 'm_StateMachineTransitions', 'm_StateMachineBehaviours'))
        or set(docs) != {root, native_id}):
        raise ValueError('Original empty home controller conversion differs')
    return {root: 'controller', native_id: 'empty-state-machine'}


def bind_prefab(binding, bundle, relative, name, scope):
    root, = [pointer.deref() for path, pointer in binding.recovery.apk.container(bundle)
        if path.casefold() == ('Assets/' + relative).casefold() and pointer.deref().type.name == 'GameObject']
    native = documents((binding.native / relative).read_text('utf8'))
    native_root, = [key for key, value in native.items() if value.get('GameObject', {}).get('m_Name') == root.read().m_Name
        and native[value['GameObject']['m_Component'][0]['component']['fileID']]['Transform']['m_Father']['fileID'] == 0]
    binding.recovery.recover(name, binding.work / 'recovered', bundle=bundle,
        native_relative=relative, source_root=root, native_root_id=native_root)
    binding.recovery.finalize(); _, _, panel, _ = binding.recovery.outputs[-1]
    if any(value['type'] != 'MonoBehaviour' for value in panel['unresolved'].values()):
        raise ValueError('Resolve additional original native entity references: ' + name)
    for key, pointer in panel['external'].items():
        binding.objects[pointer['guid']] = binding.original(key)
    for key, file_id in panel['objectMap'].items():
        owner = binding.original(key)
        binding.learn(binding.recovery.fields(owner), next(iter(panel['documents'][file_id].values())), owner)
    for script in panel['scripts']:
        full_name = '.'.join(filter(None, [script['namespace'], script['type']]))
        if full_name not in binding.scripts:
            raise ValueError('Migrate the actual native entity component: ' + full_name)
        panel['documents'][script['nativeFileID']]['MonoBehaviour']['m_Script'] = binding.scripts[full_name]
    resolved = binding.replace(panel['documents']); index = 0
    while index < len(binding.queue):
        _, _, data = binding.queue[index]
        if isinstance(data, dict):
            updated = binding.replace(data); data.clear(); data.update(updated)
        index += 1
    pointer = dict(fileID=panel['root'], guid=hashlib.md5((scope + ':prefab:' + identity(root)).encode()).hexdigest(), type=3)
    binding.queue.append((Path(name + '.prefab'), pointer['guid'], resolved))
    staged = binding.work / 'assets'; outputs = write_staged_assets(binding.queue, staged)
    return outputs, staged, pointer, dict(root=identity(root), objects=panel['objectMap'], scripts=panel['scripts'],
        class_counts=dict(Counter(binding.original(key).type.name for key in panel['objectMap'])),
        resolved_graph_references=panel['unresolved'])
