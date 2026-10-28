"""Verify all six original idle bone timelines against native Unity curves.

Only source data and the pinned MIT format reader are used. No Spine runtime,
pose baking, texture substitution or character renderer is involved.
"""
import hashlib
import json
from pathlib import Path
import struct

from prepare_unity_profession_native_inputs import ROOT, CACHE
from convert_official_skeletons import WHEEL_SHA256


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def f32(value):
    return struct.unpack('<f', struct.pack('<f', value))[0]


def bezier(a, b, c, d, t):
    u = 1 - t
    return u*u*u*a + 3*u*u*t*b + 3*u*t*t*c + t*t*t*d


def sample(keys, time):
    if time < keys[0]['time']: return dict(time=time, applies=False, value=0)
    for a, b in zip(keys, keys[1:]):
        if time >= b['time']: continue
        if a['mode'] == 1: value = a['value']
        elif a['mode'] == 0:
            value = a['value'] + (b['value'] - a['value']) * (time-a['time'])/(b['time']-a['time'])
        else:
            c = a['controls']
            lo, hi = 0, 1
            for _ in range(64):
                mid = (lo+hi)/2
                if bezier(a['time'], c['x'], c['z'], b['time'], mid) < time: lo = mid
                else: hi = mid
            value = bezier(a['value'], c['y'], c['w'], b['value'], (lo+hi)/2)
        return dict(time=time, applies=True, value=f32(value))
    return dict(time=time, applies=True, value=keys[-1]['value'])


def build():
    curves, inputs, counts = [], [], dict(bezier=0, linear=0, stepped=0, keys=0)
    for sex in ('male', 'female'):
        folder = CACHE / 'skeletons' / sex
        receipt_path = folder / 'source-manifest.json'
        receipt = json.loads(receipt_path.read_bytes())
        path = folder / 'skeleton.json'
        if receipt['manifest'] != '871_156598' or record(path)['sha256'] != receipt['output_sha256']:
            raise ValueError('Original character skeleton output changed: ' + sex)
        if receipt['tool']['wheel_sha256'] != WHEEL_SHA256 or receipt['converter_sha256'] != record(ROOT / 'scripts/convert_official_skeletons.py')['sha256']:
            raise ValueError('Character skeleton decoder differs from verified source: ' + sex)
        for row in receipt['inputs']:
            if record(folder / row['file'])['sha256'] != row['sha256']:
                raise ValueError('Original character binary input changed: ' + row['file'])
        skeleton = json.loads(path.read_bytes())
        inputs.append(dict(sex=sex, receipt=record(receipt_path), source_json=record(path), input_count=len(receipt['inputs'])))
        bone_names = {b['name'] for b in skeleton['bones']}
        for animation_name in ('home/idle_01', '100/idle_01', '200/idle_01'):
            animation = skeleton['animations'][animation_name]
            for bone, channels in animation['bones'].items():
                if bone not in bone_names: raise ValueError('Original idle bone is absent: ' + bone)
                for channel, original in channels.items():
                    if channel not in ('rotate', 'translate', 'scale'):
                        raise ValueError('Restore additional idle bone channel: ' + channel)
                    axes = ('value',) if channel == 'rotate' else ('x', 'y')
                    for axis, field in enumerate(axes):
                        keys = []
                        for index, key in enumerate(original):
                            curve = key.get('curve')
                            mode = 2 if isinstance(curve, list) else 1 if curve == 'stepped' else 0
                            if curve is not None and not isinstance(curve, list) and curve != 'stepped':
                                raise ValueError('Unknown source curve mode')
                            controls = dict(x=0, y=0, z=0, w=0)
                            if mode == 2:
                                if len(curve) != len(axes)*4: raise ValueError('Source channel has an unexpected Bezier handle count')
                                controls = dict(zip(('x', 'y', 'z', 'w'), curve[axis*4:axis*4+4]))
                            keys.append(dict(time=key.get('time', 0), value=key.get(field, 1 if channel == 'scale' else 0), mode=mode, controls=controls))
                            if index + 1 < len(original): counts[{0: 'linear', 1: 'stepped', 2: 'bezier'}[mode]] += 1
                        times = {f32(keys[0]['time'] - .001), f32(keys[-1]['time'] + .1)}
                        times.update(key['time'] for key in keys)
                        for a, b in zip(keys, keys[1:]):
                            if a['time'] >= b['time']: raise ValueError('Source idle key times are not increasing')
                            if a['mode'] == 2:
                                c = a['controls']
                                if not a['time'] < c['x'] <= c['z'] < b['time']:
                                    raise ValueError('Review native representation of original time handles: ' + str((sex, animation_name, bone, channel, field, a)))
                            times.update(f32(a['time'] + (b['time']-a['time'])*fraction) for fraction in (.2, .5, .8))
                        curves.append(dict(sex=sex, animation=animation_name, bone=bone, channel=channel, axis=field,
                            keys=keys, samples=[sample(keys, time) for time in sorted(times)]))
                        counts['keys'] += len(keys)
    payload = dict(curves=curves)
    fixture = CACHE / 'typed/native-curves-validation.json'
    fixture.write_text(json.dumps(payload, ensure_ascii=False, separators=(',', ':')), encoding='utf-8', newline='\n')
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeCurve.cs'
    validation = ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionNativeCurveValidation.cs'
    proof = dict(format=1, manifest='871_156598', source=inputs, generator=record(Path(__file__)),
        format_decoder=record(ROOT / 'scripts/convert_official_skeletons.py'), fixture=record(fixture),
        runtime=[record(runtime), record(runtime.with_name(runtime.name + '.meta'))],
        validation=[record(validation), record(validation.with_name(validation.name + '.meta'))],
        animations=['home/idle_01', '100/idle_01', '200/idle_01'], scalar_curves=len(curves),
        samples=sum(len(c['samples']) for c in curves), counts=counts,
        operation='Independently map every source scalar idle bone track to Unity weighted AnimationCurve keys using absolute time/value Bezier handles, exact source times/values, linear/stepped interpolation and explicit before-first-key non-application. Verify against independently inverted cubic Beziers at every source key and three positions inside every segment, plus both boundaries. No Spine runtime is read or used.',
        references=['https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Keyframe.html', 'https://esotericsoftware.com/spine-json-format'],
        limitations=['Only scalar bone timeline evaluation is implemented here. Bone world transforms/inheritance, skin composition, IK/transform/physics constraints, meshes, dyeing, appearance controllers and full profession UI still require native implementation and integration. No character is rendered by these numerical fixtures.'])
    (ROOT / 'assets/reference/profession-ui/native-curves-source-manifest.json').write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Prepared',len(curves),'original scalar idle curves and',proof['samples'],'independent numerical samples:',counts)


if __name__ == '__main__':
    build()
