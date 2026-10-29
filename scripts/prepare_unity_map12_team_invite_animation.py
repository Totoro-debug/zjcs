"""Retain original invite dropdown/tab controllers and verify their actual clips."""
import argparse
import hashlib
import json
import math
import struct
import zlib
from pathlib import Path
import re

from official_apk import Apk, ROOT
from UnityPy.classes.PPtr import PPtr
from verify_map12_invite_controllers import verify_controller
from official_native_yaml_verification import documents
from prepare_unity_bundle_inputs import collect, serialized_files
from prepare_unity_map12_team_sources import BUNDLE
from prepare_unity_map12_team_native_export import ConversionPath
from prepare_unity_map12_matching_animation import equal
from prepare_unity_map12_sources import encode, sha, source_path
from official_animation import PROPERTY_HASHES, mecanim_tracks
from prepare_unity_map12_team_invite_extra_sources import OUTPUT as EXTRA_ORIGINAL, KEYS as EXTRA_NAMES
EXTRA_NATIVE = ROOT / 'unity/Assets/Official/World/Map/12/Source/native-team-invite-extra-prefabs.json'

SOURCE = ROOT / 'unity/Assets/Official/World/Map/12/Source/team-invite-animation-native.json'
NATIVE = ROOT / 'unity/Assets/Official/World/Map/12/Source/native-team-ui-prefabs.json'
NAMES = ('TeamInvitePanel', 'TeamInvitePanelPlayerItem', 'TeamInvitePanelTabGroupItem_11', *EXTRA_NAMES)


def streamed_segments(obj, tracks):
    data = obj.read_typetree()
    paths = {zlib.crc32(row['path'].lstrip('/').encode()): row['path'] for row in tracks}
    channels = []
    for binding in data['m_ClipBindingConstant']['genericBindings']:
        attribute = binding['attribute']
        if binding['typeID'] == 4 and attribute in (1, 2, 3, 4):
            prefix, axes = {1: ('position.', 'xyz'), 2: ('quaternion.', 'xyzw'),
                            3: ('scale.', 'xyz'), 4: ('rotation.', 'xyz')}[attribute]
            names = [prefix + axis for axis in axes]
        else:
            names = [PROPERTY_HASHES[attribute]]
        channels.extend((paths[binding['path']], name) for name in names)
    stream = data['m_MuscleClip']['m_Clip']['data']['m_StreamedClip']
    raw = struct.pack('<' + 'I' * len(stream['data']), *stream['data'])
    offset, result = 0, {}
    while offset < len(raw):
        time, count = struct.unpack_from('<fi', raw, offset); offset += 8
        if count < 0 or count > len(channels) or offset + count * 20 > len(raw):
            raise ValueError('Invalid original invite animation stream')
        for _ in range(count):
            channel, a, b, c, value = struct.unpack_from('<i4f', raw, offset); offset += 20
            if math.isfinite(time):
                result[(*channels[channel], max(0, time))] = (a, b, c, value)
    return result

def verify_invite_mecanim(obj, exported, path):
    normalizations = []
    paths = {row['path'] or '' for field in ('m_ScaleCurves', 'm_PositionCurves', 'm_EulerCurves',
        'm_RotationCurves', 'm_FloatCurves') for row in exported[field]}
    tracks, duration, loop, unknown = mecanim_tracks(obj, dict(path='', name='', children=[]),
                                                   binding_paths=paths - {''})
    if unknown:
        raise ValueError('Resolve original compressed team animation channels: ' + str(unknown))
    for row in tracks:
        row['path'] = row['path'].lstrip('/')
    segment_coefficients = streamed_segments(obj, tracks)
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
        while (len(expected) > len(keys) and expected[0][0] < keys[0][0]
               and len(expected) > 1 and expected[0][1] == expected[1][1]):
            coefficients = segment_coefficients.get((row['path'], row['property'], expected[0][0]))
            if coefficients is None or coefficients[:3] != (0.0, 0.0, 0.0):
                break
            normalizations.append(dict(path=row['path'], property=row['property'],
                                       redundantLeadingConstantKey=expected[0]))
            expected = expected[1:]
        if len(keys) != len(expected):
            raise ValueError('Original compressed invite animation key coverage differs: ' + path +
                             ' ' + repr((row['path'], row['property'], expected, keys)))
        for index, (a, b) in enumerate(zip(expected, keys)):
            equal(a[:2], b[:2], path + '/key-time-value')
            # APK stores cubic coefficients, not authoring incoming tangents.
            # The exporter and shared codec round the derived incoming value
            # at different intermediate steps. Permit at most one float32 ULP
            # for this derivative only; retain the exporter's original data.
            if a[2] is None:
                outgoing = float(str(b[3]).replace('Infinity', 'inf'))
                if (index + 1 < len(expected) and a[1] != expected[index + 1][1]
                        and not math.isinf(outgoing)):
                    raise ValueError('Original discrete invite animation segment differs: ' + path)
                continue
            incoming = float(str(b[2]).replace('Infinity', 'inf'))
            left, = struct.unpack('<i', struct.pack('<f', a[2]))
            right, = struct.unpack('<i', struct.pack('<f', incoming))
            if abs(left - right) > 1:
                raise ValueError('Original streamed incoming derivative differs: ' + path)
            outgoing = float(str(b[3]).replace('Infinity', 'inf'))
            if math.isinf(outgoing):
                coefficients = segment_coefficients.get((row['path'], row['property'], a[0]))
                if (index + 1 >= len(expected) or coefficients is None
                        or coefficients[:3] != (0.0, 0.0, 0.0)):
                    raise ValueError('Original streamed step segment differs: ' + path)
            else:
                equal(a[3], outgoing, path + '/outgoing')
    equal(duration, exported['m_AnimationClipSettings']['m_StopTime'], path + '/duration')
    equal(int(loop), exported['m_AnimationClipSettings']['m_LoopTime'], path + '/loop')
    return normalizations


def native_capture(stage):
    from validation_workspace import validation_directory
    workspace = validation_directory()
    stage = ConversionPath(stage.resolve())
    if not stage.is_relative_to(workspace):
        raise ValueError('Use the owning native export validation workspace')
    proof_path = stage / 'native-export-proof.json'
    proof = json.loads(proof_path.read_bytes())
    for row in proof['outputs']:
        if sha(ConversionPath(ROOT / row['path']).read_bytes()) != row['sha256']:
            raise ValueError('Original native export output changed')
    data = json.loads(NATIVE.read_bytes())
    extra_native = json.loads(EXTRA_NATIVE.read_bytes())
    for key in ('panels', 'bindings', 'externalReferences'):
        data[key].extend(extra_native[key])
    exported = stage / 'export/ExportedProject/Assets'
    index = {}
    for path in exported.rglob('*.meta'):
        match = re.search(r'(?m)^guid: ([a-f0-9]{32})$', path.read_text())
        if match:
            index[match[1]] = path.with_suffix('')
    refs = {row['convertedPath']: row for row in data['externalReferences']}
    wanted_controllers = set()
    original_source = json.loads((ROOT / 'unity/Assets/Official/World/Map/12/Source/team-ui-prefabs.json').read_bytes())
    original_source['objects'].extend(json.loads(EXTRA_ORIGINAL.read_bytes())['objects'])
    source_objects = {row['identity']: row for row in original_source['objects']}
    native_bindings = {(row['panel'], row['fileId']): row['original'] for row in data['bindings']}
    for panel in data['panels']:
        if panel['name'] not in NAMES:
            continue
        for file_id, (kind, fields) in documents(panel['yaml']).items():
            if ((kind == 'Animator' and fields['m_Controller']['fileID'])
                    or (kind == 'Animation' and fields['m_Animation']['fileID'])):
                source = source_objects[native_bindings[(panel['name'], str(file_id))]]
                path_id = str(source['fields']['m_Controller' if kind == 'Animator' else 'm_Animation']['m_PathID'])
                matches = [row for row in data['externalReferences'] if row['identity'].rsplit(':', 1)[1] == path_id]
                reference, = {row['identity']: row for row in matches}.values()
                wanted_controllers.add(reference['convertedPath'])
    apk = Apk.current()
    _, missing = collect(apk, [BUNDLE])
    if missing:
        raise ValueError('Resolve original invite animation CABs: ' + str(missing))
    objects = {file.name + ':' + str(obj.path_id): obj for bundle in apk.loaded.values()
               for file in serialized_files(bundle) for obj in file.objects.values()}
    files, identities = {}, {}

    def retain(obj, native_guid):
        identity = obj.assets_file.name + ':' + str(obj.path_id)
        if native_guid in files:
            if files[native_guid]['identity'] != identity:
                raise ValueError('Original invite animation GUID has conflicting identities')
            return
        if identity in identities and identities[identity] != native_guid:
            raise ValueError('Original invite animation identity has conflicting GUIDs')
        identities[identity] = native_guid
        path = index[native_guid]
        raw = path.read_bytes()
        docs = documents(raw.decode('utf8'))
        entries = [fields for kind, fields in docs.values() if kind == obj.type.name]
        fields, = entries
        if fields['m_Name'] != obj.peek_name():
            raise ValueError('Original invite animation identity differs')
        files[native_guid] = dict(guid=native_guid, identity=identity,
            path=path.relative_to(exported).as_posix(), yaml=raw.decode('utf8'),
            sha256=sha(raw), payloadSha256=sha(obj.get_raw_data()), source=apk.provenance(obj))
        original = obj.read()
        if obj.type.name == 'AnimatorOverrideController':
            retain(original.m_Controller.deref(), fields['m_Controller']['guid'])
            if len(original.m_Clips) != len(fields['m_Clips']):
                raise ValueError('Original invite override clip count differs')
            for left, right in zip(original.m_Clips, fields['m_Clips']):
                for key in ('m_OriginalClip', 'm_OverrideClip'):
                    pointer = getattr(left, key)
                    if pointer.path_id:
                        retain(pointer.deref(), right[key]['guid'])
                    elif right[key]['fileID']:
                        raise ValueError('Original invite null override changed')
        elif obj.type.name == 'AnimatorController':
            clip_objects = {ptr.read().m_Name: ptr.deref() for ptr in original.m_AnimationClips}
            motions = [row['m_Motion'] for kind, row in docs.values()
                       if kind == 'AnimatorState' and row['m_Motion']['fileID']]
            seen_clips = set()
            for motion in motions:
                (_, clip_fields), = documents(index[motion['guid']].read_text()).values()
                name = clip_fields['m_Name']
                if name not in clip_objects:
                    raise ValueError('Original invite controller clip identity changed')
                seen_clips.add(name)
                retain(clip_objects[name], motion['guid'])
            if seen_clips != set(clip_objects):
                raise ValueError('Original invite controller clip coverage changed')
        elif obj.type.name != 'AnimationClip':
            raise ValueError('Unexpected original invite animation resource')

    for controller_path in sorted(wanted_controllers):
        path = exported / controller_path
        controller_guid = re.search(r'(?m)^guid: ([a-f0-9]{32})$', path.with_name(path.name + '.meta').read_text())[1]
        reference = refs[controller_path]
        controller = objects[reference['identity']]
        if sha(controller.get_raw_data()) != reference['payload_sha256']:
            raise ValueError('Original invite controller payload changed')
        retain(controller, controller_guid)
    script_sources = {}
    for row in files.values():
        if not row['path'].endswith('.anim'):
            continue
        (_, fields), = documents(row['yaml']).values()
        for curve in fields['m_FloatCurves']:
            if not curve['script']['fileID']:
                continue
            script_guid = curve['script']['guid']
            script_path = index[script_guid]
            if script_path.name not in ('Image.cs', 'TextMeshProUGUI.cs'):
                raise ValueError('Inspect original invite animated component: ' + str(script_path))
            script_sources[script_guid] = dict(guid=script_guid, name=script_path.stem,
                exportedPath=script_path.relative_to(exported).as_posix(),
                metadataSha256=sha(script_path.with_name(script_path.name + '.meta').read_bytes()))
    value = dict(manifest=apk.version, editorSource='AssetRipper 1.3.14',
                 nativeExportProof=proof,
                 nativeHierarchySource=dict(path=source_path(NATIVE), sha256=sha(NATIVE.read_bytes())),
                 scriptSources=sorted(script_sources.values(), key=lambda row: row['guid']),
                 files=sorted(files.values(), key=lambda row: row['identity']))
    SOURCE.write_bytes(encode(value))
    SOURCE.with_name(SOURCE.name + '.meta').write_text('fileFormatVersion: 2\nguid: ' +
        hashlib.md5(b'zjcs:map12:team-invite-animation-native').hexdigest() + '\nTextScriptImporter:\n  externalObjects: {}\n', encoding='utf8')


def restore_legacy_signed_zero(text, original):
    changes = []
    match = re.search(r'(?ms)^  m_FloatCurves:\n(.*?)(?=^  \w+:)', text)
    if not match:
        raise ValueError('Original Legacy float curve section is absent')
    curves = re.split(r'(?m)(?=^  - serializedVersion: 2\n)', match[1])
    if curves[0] or len(curves) - 1 != len(original['m_FloatCurves']):
        raise ValueError('Original Legacy float curve layout differs')
    for number, source in enumerate(original['m_FloatCurves']):
        keys = re.split(r'(?m)(?=^      - serializedVersion: 3\n)', curves[number + 1])
        if len(keys) - 1 != len(source['curve']['m_Curve']):
            raise ValueError('Original Legacy key layout differs')
        for index, key in enumerate(source['curve']['m_Curve']):
            for field, value in key.items():
                if isinstance(value, float) and value == 0 and math.copysign(1, value) < 0:
                    keys[index + 1], count = re.subn(r'(?m)^        ' + field + r': (?:0|-0(?:\.0)?)$',
                        '        ' + field + ': -0.0', keys[index + 1])
                    if count != 1:
                        raise ValueError('Original Legacy signed zero field differs')
                    changes.append(dict(path=source['path'], property=source['attribute'], key=index, field=field))
        curves[number + 1] = ''.join(keys)
    return text[:match.start(1)] + ''.join(curves) + text[match.end(1):], changes


def build(apk, objects, references):
    data = json.loads(SOURCE.read_bytes())
    if data['manifest'] != apk.version or data['editorSource'] != 'AssetRipper 1.3.14':
        raise ValueError('Original invite animation source differs')
    guid = lambda identity: hashlib.md5(('zjcs:map12:team-invite-animation:' + identity).encode()).hexdigest()
    redirects = {row['guid']: guid(row['identity']) for row in data['files']}
    for script in data['scriptSources']:
        if script['name'] == 'Image':
            path = ROOT / 'unity/Library/PackageCache/com.unity.ugui@1.0.0/Runtime/UI/Core/Image.cs.meta'
        elif script['name'] == 'TextMeshProUGUI':
            path = ROOT / 'unity/Library/PackageCache/com.unity.textmeshpro@3.0.6/Scripts/Runtime/TextMeshProUGUI.cs.meta'
        else:
            raise ValueError('Unexpected original invite script binding')
        redirects[script['guid']] = re.search(r'(?m)^guid: ([a-f0-9]{32})$', path.read_text())[1]
    outputs, dependencies = {}, {}
    folder = ROOT / 'unity/Assets/Official/World/Map/12/UI/Team/InviteAnimations'
    for row in data['files']:
        if sha(row['yaml'].encode()) != row['sha256']:
            raise ValueError('Original invite animation YAML changed')
        obj = objects[row['identity']]
        if sha(obj.get_raw_data()) != row['payloadSha256']:
            raise ValueError('Original invite animation payload changed')
        native_text = row['yaml']
        signed_zeros = []
        if obj.type.name == 'AnimationClip' and obj.read_typetree()['m_Legacy']:
            native_text, signed_zeros = restore_legacy_signed_zero(native_text, obj.read_typetree())
        docs = documents(native_text)
        normalizations = []
        controller_proof = None
        if obj.type.name == 'AnimationClip':
            (_, fields), = docs.values()
            original = obj.read_typetree()
            for key in ('m_Name', 'm_Legacy', 'm_Compressed', 'm_UseHighQualityCurve',
                        'm_CompressedRotationCurves', 'm_PPtrCurves', 'm_SampleRate', 'm_WrapMode', 'm_Bounds',
                        'm_HasGenericRootTransform', 'm_HasMotionFloatCurves'):
                equal(original[key], fields[key], row['path'] + '/' + key)
            if original['m_Legacy']:
                for field in ('m_RotationCurves', 'm_EulerCurves', 'm_PositionCurves',
                              'm_ScaleCurves', 'm_ClipBindingConstant'):
                    equal(original[field], fields[field], row['path'] + '/' + field)
                if len(original['m_FloatCurves']) != len(fields['m_FloatCurves']):
                    raise ValueError('Original invite Legacy channel coverage differs')
                for left, right in zip(original['m_FloatCurves'], fields['m_FloatCurves']):
                    for field in ('path', 'attribute', 'classID', 'flags', 'curve'):
                        equal(left[field], (right[field] or '') if field == 'path' else right[field], row['path'] + '/' + field)
                    if left['script']['m_PathID']:
                        script = PPtr(**left['script'], assetsfile=obj.assets_file).read()
                        expected_script, = [source for source in data['scriptSources'] if source['guid'] == right['script']['guid']]
                        namespaces = {'Image': 'UnityEngine.UI', 'TextMeshProUGUI': 'TMPro'}
                        if (script.m_ClassName != expected_script['name']
                                or script.m_Namespace != namespaces[expected_script['name']]):
                            raise ValueError('Original invite Legacy script identity differs')
                    elif right['script']['fileID']:
                        raise ValueError('Original invite Legacy null script changed')
                for key, value in original['m_MuscleClip'].items():
                    if key in fields['m_AnimationClipSettings']:
                        equal(value, fields['m_AnimationClipSettings'][key], row['path'] + '/' + key)
                equal(original['m_Events'], fields['m_Events'], row['path'] + '/events')
            def clone_fields(value):
                if isinstance(value, dict):
                    return {key: clone_fields(item) for key, item in value.items()}
                if isinstance(value, list):
                    return [clone_fields(item) for item in value]
                return value
            verified_fields = clone_fields(fields)
            script_by_guid = {source['guid']: source['name'] for source in data['scriptSources']}
            for curve in ([] if original['m_Legacy'] else verified_fields['m_FloatCurves']):
                if not curve['script']['fileID']:
                    continue
                match = re.fullmatch(r'script_0x([0-9A-Fa-f]{8})_\w+', curve['attribute'])
                if not match or int(match[1], 16) not in PROPERTY_HASHES:
                    raise ValueError('Resolve original invite animated property hash')
                property_name = PROPERTY_HASHES[int(match[1], 16)]
                expected_script = 'TextMeshProUGUI' if property_name.startswith('m_fontColor.') else 'Image' if property_name.startswith('m_Color.') else None
                if expected_script != script_by_guid[curve['script']['guid']]:
                    raise ValueError('Original invite animated property owner differs')
                curve['attribute'] = property_name
                curve['script'] = dict(fileID=0)
            if not original['m_Legacy']:
                normalizations = verify_invite_mecanim(obj, verified_fields, row['path'])
            for key, value in original['m_MuscleClip'].items():
                if key in fields['m_AnimationClipSettings']:
                    equal(value, fields['m_AnimationClipSettings'][key], row['path'] + '/' + key)
            equal(original['m_Events'], fields['m_Events'], row['path'] + '/events')
        elif obj.type.name == 'AnimatorController':
            controller_proof = verify_controller(obj, docs, {entry['guid']: entry['identity'] for entry in data['files']})
        elif obj.type.name != 'AnimatorOverrideController':
            raise ValueError('Unexpected original invite animation kind')
        unbound = set(re.findall(r'guid: ([a-f0-9]{32})', native_text)) - set(redirects)
        if unbound:
            raise ValueError('Resolve invite controller dependencies: ' + str(unbound))
        text = re.sub(r'guid: ([a-f0-9]{32})', lambda match: 'guid: ' + redirects[match[1]], native_text)
        def animated_property(match):
            value = int(match[1], 16)
            if value not in PROPERTY_HASHES:
                raise ValueError('Unknown original invite animation property')
            return 'attribute: ' + PROPERTY_HASHES[value]
        text = re.sub(r'attribute: script_0x([0-9A-Fa-f]{8})_\w+', animated_property, text)
        original_path = Path(row['path'])
        path = folder / (original_path.stem + '-' + sha(row['identity'].encode())[:10] + original_path.suffix)
        outputs[path] = text.encode()
        file_id = '9100000' if obj.type.name == 'AnimatorController' else '22100000' if obj.type.name == 'AnimatorOverrideController' else '7400000'
        outputs[path.with_name(path.name + '.meta')] = ('fileFormatVersion: 2\nguid: ' + guid(row['identity']) +
            '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: ' + file_id + '\n').encode()
        dependencies[row['identity']] = dict(path=source_path(path), nativeGuid=guid(row['identity']),
            sha256=sha(outputs[path]), original=row['source'], object_sha256=row['payloadSha256'],
            inputPath=row['path'], conversion_sha256=row['sha256'])
        if signed_zeros:
            dependencies[row['identity']]['restoredSignedZeroFields'] = signed_zeros
        if controller_proof:
            dependencies[row['identity']]['compiledStateGraphProof'] = controller_proof
        if normalizations:
            dependencies[row['identity']]['nativeKeyNormalizations'] = normalizations
    outputs[folder.with_name(folder.name + '.meta')] = ('fileFormatVersion: 2\nguid: ' + guid('folder') +
        '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n').encode()
    return outputs, dependencies, SOURCE


def install(write=False):
    apk = Apk.current()
    _, missing = collect(apk, [BUNDLE])
    if missing:
        raise ValueError('Resolve original invitation animation inputs: ' + str(missing))
    objects = {file.name + ':' + str(obj.path_id): obj for bundle in apk.loaded.values()
               for file in serialized_files(bundle) for obj in file.objects.values()}
    outputs, dependencies, source = build(apk, objects, None)
    def record(path):
        return dict(path=source_path(path), sha256=sha(path.read_bytes()))
    value = dict(format=1, manifest=apk.version, mapClassId=12,
        inputs=[record(path) for path in (source, NATIVE, EXTRA_NATIVE, EXTRA_ORIGINAL)],
        dependencies=[dict(identity=key, **row) for key, row in sorted(dependencies.items())],
        adapters=[record(ROOT / name) for name in (
            'scripts/prepare_unity_map12_team_invite_animation.py', 'scripts/official_animation.py',
            'scripts/prepare_unity_map12_matching_animation.py', 'scripts/official_native_yaml_verification.py',
            'scripts/verify_map12_invite_controllers.py',
            'unity/Assets/Game/Runtime/Offline/World/NativeMap12InviteAnimations.cs',
            'unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.InviteAnimations.cs',
            'unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.TeamPanel.cs')],
        outputs=[dict(path=source_path(path), sha256=sha(raw)) for path, raw in outputs.items()],
        operation='Retain all original controller, override-controller and Legacy/Mecanim clips referenced '
                  'by the eight complete invitation prefabs. Verify original CAB identity and payload, '
                  'all animation channels, times, values, controller motion closure and override pointers. '
                  'Restore original Legacy signed-zero tangent fields. Decode compressed cubic '
                  'coefficients independently, checking constant/step segments and documenting only '
                  'redundant leading constant-key normalization and at most one float32 ULP '
                  'of derived incoming-tangent rounding. Bind original Image/TMP animated properties.',
        limitations=['Complete invitation window/player/NPC service integration remains pending.'])
    for path, raw in outputs.items():
        if write:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(raw)
        elif not path.is_file() or path.read_bytes() != raw:
            raise ValueError('Original invitation animation output drift: ' + source_path(path))
    receipt = ROOT / 'assets/reference/official-map/map12-team-invite-animation-source-manifest.json'
    raw = encode(value)
    if write:
        receipt.write_bytes(raw)
    elif not receipt.is_file() or receipt.read_bytes() != raw:
        raise ValueError('Original invitation animation receipt drift')
    print('Original invitation animation closure verified:', len(dependencies), 'native assets', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--capture-native', type=Path)
    parser.add_argument('--write', action='store_true')
    args = parser.parse_args()
    if args.capture_native:
        native_capture(args.capture_native)
    else:
        install(args.write)
