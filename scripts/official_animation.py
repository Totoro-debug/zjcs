"""Decode the APK's Mecanim scalar curves without running Unity or client code."""
import math
import struct
import zlib


PROPERTIES = [
    'm_IsActive', 'm_Alpha', 'm_BlocksRaycasts', 'm_AnchoredPosition.x', 'm_AnchoredPosition.y',
    'm_SizeDelta.x', 'm_SizeDelta.y', 'm_Color.r', 'm_Color.g', 'm_Color.b', 'm_Color.a',
    'm_AnchorMin.x', 'm_AnchorMin.y', 'm_AnchorMax.x', 'm_AnchorMax.y', 'm_Pivot.x', 'm_Pivot.y',
    'm_fontColor.r', 'm_fontColor.g', 'm_fontColor.b', 'm_fontColor.a',
    'm_fontSize', 'm_FillAmount', 'm_LocalScale.x', 'm_LocalScale.y',
    'localEulerAnglesRaw.z',
    'm_AmplitudeGain','m_FrequencyGain','m_Lens.OrthographicSize','m_Lens.Dutch','m_Priority','m_Intensity','m_Enabled','m_SortingOrder','dummy',
]
PROPERTY_HASHES = {zlib.crc32(p.encode()): p for p in PROPERTIES}


def mecanim_tracks(obj, tree, property_names=(), vector_property_names=(), binding_paths=(), scalar_material_property_names=()):
    data = obj.read_typetree()
    muscle = data['m_MuscleClip']
    duration = muscle['m_StopTime']
    paths = {}

    def visit(node, relative=''):
        paths[zlib.crc32(relative.encode())] = node['path']
        for child in node['children']:
            visit(child, relative + ('/' if relative else '') + child['name'])

    visit(tree)
    # Shared official clips can contain curves for optional prefab nodes. Keep
    # their original paths so Unity naturally ignores absent targets at runtime.
    paths.update({zlib.crc32(path.encode()): tree['path'] + '/' + path for path in binding_paths})
    channels = []
    unknown = []
    property_hashes = dict(PROPERTY_HASHES)
    property_hashes.update({zlib.crc32(p.encode()): p for p in property_names})
    vector_property_hashes = {
        zlib.crc32(p.encode()) & 0x0fffffff: p for p in vector_property_names
    }
    scalar_material_hashes = {zlib.crc32(p.encode()) & 0x0fffffff: 'material.'+p
                             for p in scalar_material_property_names}
    for binding in data['m_ClipBindingConstant']['genericBindings']:
        path = paths.get(binding['path'])
        attribute = binding['attribute']
        # Transform vector bindings consume several consecutive scalar channels.
        if binding['typeID'] == 4 and attribute in [1, 2, 3, 4]:
            names = {1: ['position.'+a for a in 'xyz'], 2: ['quaternion.'+a for a in 'xyzw'],
                     3: ['scale.'+a for a in 'xyz'], 4: ['rotation.'+a for a in 'xyz']}[attribute]
        elif scalar_material_property_names and binding['typeID'] in (23, 212) and attribute >> 28 == 8:
            names = [scalar_material_hashes.get(attribute & 0x0fffffff)]
        elif binding['typeID'] == 23 and 4 <= attribute >> 28 <= 7:
            vector = vector_property_hashes.get(attribute & 0x0fffffff)
            names = [vector + '.' + {4: 'r', 5: 'g', 6: 'b', 7: 'a'}[attribute >> 28]] if vector else [None]
        else:
            names = [property_hashes.get(attribute)]
        for name in names:
            channels.append((path, name))
            if path is None or name is None:
                unknown.append({'path_hash': binding['path'], 'attribute_hash': attribute,
                                'type': binding['typeID'], 'path': path,
                                'property': name, 'reason': 'target_absent' if path is None else 'attribute_unknown'})
    if not channels:
        return [], duration, bool(muscle['m_LoopTime']), unknown
    clip = muscle['m_Clip']['data']
    stream = clip['m_StreamedClip']
    raw = struct.pack('<'+'I'*len(stream['data']), *stream['data'])
    frames = {}
    offset = 0
    while offset < len(raw):
        time, count = struct.unpack_from('<fi', raw, offset)
        offset += 8
        if not 0 <= count <= len(channels) or offset + count*20 > len(raw):
            raise ValueError('Invalid streamed animation frame: '+data['m_Name'])
        for _ in range(count):
            index, a, b, c, value = struct.unpack_from('<i4f', raw, offset)
            offset += 20
            if not 0 <= index < len(channels):
                raise ValueError('Invalid animation channel')
            if math.isfinite(time):
                frames.setdefault(index, []).append((max(0, time), (a, b, c, value)))
    keys_by_channel = {}
    for index, samples in frames.items():
        samples = sorted(dict(samples).items())
        keys = []
        for i, (time, coefficients) in enumerate(samples):
            incoming = 0.0
            if i:
                before, previous = samples[i-1]
                dt = time-before
                incoming = 3*previous[0]*dt*dt + 2*previous[1]*dt + previous[2]
            keys.append([time, coefficients[3], incoming, coefficients[2]])
        keys_by_channel[index] = keys
    dense = clip['m_DenseClip']
    first_dense = stream['curveCount']
    for channel in range(dense['m_CurveCount']):
        keys_by_channel[first_dense+channel] = [
            [dense['m_BeginTime']+frame/dense['m_SampleRate'],
             dense['m_SampleArray'][frame*dense['m_CurveCount']+channel], 0, 0]
            for frame in range(dense['m_FrameCount'])]
    first_constant = first_dense+dense['m_CurveCount']
    for i, value in enumerate(clip['m_ConstantClip']['data']):
        keys_by_channel[first_constant+i] = [[0, value, 0, 0]]
    tracks = []
    for index, keys in keys_by_channel.items():
        path, name = channels[index]
        if path is not None and name is not None:
            if name in ['m_IsActive','m_Enabled','m_BlocksRaycasts']:
                keys = [[t, v, None, None] for t, v, _, _ in keys]
            tracks.append({'path': path, 'property': name, 'keys': keys})
    return tracks, duration, bool(muscle['m_LoopTime']), unknown
