"""Verify original panel/button clips and retain the native controller conversion."""
import hashlib
import json
from pathlib import Path
import re
import math
import struct

from official_apk import ROOT
from official_native_yaml_verification import documents
from prepare_unity_map12_matching_animation import equal
from official_animation import mecanim_tracks


def verify_mecanim(obj, exported, path):
    paths = {row['path'] or '' for field in ('m_ScaleCurves', 'm_PositionCurves', 'm_EulerCurves',
        'm_RotationCurves', 'm_FloatCurves') for row in exported[field]}
    tracks, duration, loop, unknown = mecanim_tracks(obj, dict(path='', name='', children=[]),
                                                   binding_paths=paths - {''})
    if unknown:
        raise ValueError('Resolve original compressed team animation channels: ' + str(unknown))
    for row in tracks:
        row['path'] = row['path'].lstrip('/')
    native = {}
    for field, prefix, axes in (('m_ScaleCurves', 'scale.', 'xyz'), ('m_PositionCurves', 'position.', 'xyz'),
                               ('m_EulerCurves', 'rotation.', 'xyz'), ('m_RotationCurves', 'quaternion.', 'xyzw')):
        for row in exported[field]:
            for axis in axes:
                native[(row['path'] or '', prefix + axis)] = [[key['time'], key['value'][axis],
                    key['inSlope'][axis], key['outSlope'][axis]] for key in row['curve']['m_Curve']]
    for row in exported['m_FloatCurves']:
        if row['script']['fileID']:
            raise ValueError('Resolve original compressed animation script')
        native[(row['path'] or '', row['attribute'])] = [[key['time'], key['value'], key['inSlope'],
            key['outSlope']] for key in row['curve']['m_Curve']]
    if set(native) != {(row['path'], row['property']) for row in tracks}:
        raise ValueError('Original compressed team animation channel coverage differs: ' + path)
    for row in tracks:
        keys = native[(row['path'], row['property'])]
        expected = row['keys']
        if len(expected) == 1:
            if any(struct.pack('<f', key[1]) != struct.pack('<f', expected[0][1])
                   or key[2] or key[3] for key in keys):
                raise ValueError('Original compressed constant animation differs')
            continue
        if len(keys) != len(expected):
            raise ValueError('Original compressed team animation key coverage differs')
        for index, (a, b) in enumerate(zip(expected, keys)):
            equal(a[:2], b[:2], path + '/key-time-value')
            # APK stores cubic coefficients, not authoring incoming tangents.
            # The exporter and shared codec round the derived incoming value
            # at different intermediate steps. Permit at most one float32 ULP
            # for this derivative only; retain the exporter's original data.
            incoming = float(str(b[2]).replace('Infinity', 'inf'))
            left, = struct.unpack('<i', struct.pack('<f', a[2]))
            right, = struct.unpack('<i', struct.pack('<f', incoming))
            if abs(left - right) > 1:
                raise ValueError('Original streamed incoming derivative differs: ' + path)
            outgoing = float(str(b[3]).replace('Infinity', 'inf'))
            if math.isinf(outgoing):
                if (index + 1 >= len(expected) or a[1] != expected[index + 1][1]
                        or a[3] != 0 or expected[index + 1][2] != 0):
                    raise ValueError('Original streamed step segment differs: ' + path)
            else:
                equal(a[3], outgoing, path + '/outgoing')
    equal(duration, exported['m_AnimationClipSettings']['m_StopTime'], path + '/duration')
    equal(int(loop), exported['m_AnimationClipSettings']['m_LoopTime'], path + '/loop')


def build(apk, objects, references):
    source = ROOT / 'unity/Assets/Official/World/Map/12/Source/team-panel-animation-native.json'
    data = json.loads(source.read_bytes())
    if data['manifest'] != apk.version or data['editorSource'] != 'AssetRipper 1.3.14':
        raise ValueError('Recheck original team animation conversion')
    root_paths = ('Animation/UI/UIDialog_Show.anim', 'Animation/UI/UIDialog_Hide.anim',
                  'Animation/UI/Common/Btn_Base_Click.controller')
    roots = {path: next(row for row in references.values() if row['convertedPath'] == path)
             for path in root_paths}
    originals = {path: objects[row['identity']] for path, row in roots.items()}
    controller = originals[root_paths[2]]
    clips = controller.read().m_AnimationClips
    for pointer in clips:
        clip = pointer.deref()
        path = 'Animation/UI/Common/' + clip.read().m_Name + '.anim'
        if path in originals:
            raise ValueError('Original button clip identity is ambiguous')
        originals[path] = clip
    if set(originals) != {row['path'] for row in data['files']}:
        raise ValueError('Original team animation closure changed')
    folder = ROOT / 'unity/Assets/Official/World/Map/12/UI/Team/Animations'
    guid = lambda path: hashlib.md5(('zjcs:map12:team-animation:' + path).encode()).hexdigest()
    redirects = {row['guid']: guid(row['path']) for row in data['files']}
    outputs, dependencies = {}, {}
    for row in data['files']:
        text = row['yaml']
        if hashlib.sha256(text.encode()).hexdigest() != row['sha256']:
            raise ValueError('Original team animation native input changed')
        obj = originals[row['path']]
        raw = obj.get_raw_data()
        identity = obj.assets_file.name + ':' + str(obj.path_id)
        if row['path'] in roots and hashlib.sha256(raw).hexdigest() != roots[row['path']]['payload_sha256']:
            raise ValueError('Original panel animation object changed')
        docs = documents(text)
        if obj.type.name == 'AnimationClip':
            (_, exported), = docs.values()
            original = obj.read_typetree()
            for key in ('m_Name', 'm_Legacy', 'm_Compressed', 'm_UseHighQualityCurve',
                        'm_CompressedRotationCurves', 'm_PPtrCurves', 'm_SampleRate', 'm_WrapMode', 'm_Bounds',
                        'm_HasGenericRootTransform', 'm_HasMotionFloatCurves'):
                equal(original[key], exported[key], row['path'] + '/' + key)
            if original['m_Legacy']:
                raise ValueError('Inspect selected team Legacy clip conversion')
            verify_mecanim(obj, exported, row['path'])
            for key, value in original['m_MuscleClip'].items():
                if key in exported['m_AnimationClipSettings']:
                    equal(value, exported['m_AnimationClipSettings'][key], row['path'] + '/' + key)
            if original['m_Events'] or exported['m_Events']:
                raise ValueError('Resolve original animation events')
        elif obj.type.name == 'AnimatorController':
            # Keep all original exported transitions/parameters/states byte-for-byte.
            # Runtime acceptance also checks each original state clip and binding.
            actual_clips = [fields['m_Name'] for kind, fields in docs.values() if kind == 'AnimatorState']
            if set(actual_clips) != {ptr.read().m_Name for ptr in clips}:
                raise ValueError('Original button controller state coverage changed')
        else:
            raise ValueError('Unexpected original panel animation resource')
        unresolved = set(re.findall(r'guid: ([a-f0-9]{32})', text)) - set(redirects)
        if unresolved:
            raise ValueError('Resolve original panel animation dependencies: ' + str(unresolved))
        text = re.sub(r'guid: ([a-f0-9]{32})', lambda match: 'guid: ' + redirects[match[1]], text)
        path = folder / Path(row['path']).name
        outputs[path] = text.encode()
        file_id = '9100000' if path.suffix == '.controller' else '7400000'
        outputs[path.with_name(path.name + '.meta')] = ('fileFormatVersion: 2\nguid: ' + guid(row['path']) +
            '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: ' + file_id + '\n').encode()
        dependencies[identity] = dict(path=path.relative_to(ROOT).as_posix(), nativeGuid=guid(row['path']),
            sha256=hashlib.sha256(outputs[path]).hexdigest(), original=apk.provenance(obj),
            object_sha256=hashlib.sha256(raw).hexdigest(), inputPath=row['path'],
            conversion_sha256=row['sha256'])
    outputs[folder.with_name(folder.name + '.meta')] = ('fileFormatVersion: 2\nguid: ' + guid('folder') +
        '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n').encode()
    return outputs, dependencies, source
