"""Independent complex-plane references for the actual idle world constraints."""
import cmath
import hashlib
import json
import math
from pathlib import Path

from convert_official_skeletons import WHEEL_SHA256, TOOL_ROOT
from prepare_unity_profession_native_inputs import ROOT, CACHE
from prepare_unity_profession_native_curves import record
from prepare_unity_profession_world_inheritance import world


MIXES = ('mixRotate', 'mixX', 'mixY', 'mixScaleX', 'mixScaleY', 'mixShearY')


def phase_difference(a, b):
    return cmath.phase(cmath.rect(1, b - a))


def build():
    baseline = ROOT / 'assets/reference/profession-ui/idle-ik-source-manifest.json'
    ik_proof = json.loads(baseline.read_bytes())
    ik_path = CACHE / 'typed/idle-ik-validation.json'
    if ik_proof['fixture'] != record(ik_path):
        raise ValueError('Verified independent circle-intersection IK references changed')
    wheel = TOOL_ROOT / 'skeltojson-1.0.1-py3-none-any.whl'
    if hashlib.sha256(wheel.read_bytes()).hexdigest() != WHEEL_SHA256:
        raise ValueError('Pinned independent binary field/default reader changed')
    inheritance = ROOT / 'assets/reference/profession-ui/world-inheritance-source-manifest.json'
    inherited = json.loads(inheritance.read_bytes())
    local_path = CACHE / 'typed/bone-pose-validation.json'
    if inherited['original_local_fixture'] != record(local_path):
        raise ValueError('Verified original local poses changed')
    original = json.loads(local_path.read_bytes())
    ik = {(r['sex'], r['animation'], r['time']): r for r in json.loads(ik_path.read_bytes())['cases']}
    cases, inputs = [], []
    for body in original['cases']:
        sex, bones = body['sex'], body['bones']
        path = CACHE / 'skeletons' / sex / 'skeleton.json'
        if record(path) not in inherited['inputs']:
            raise ValueError('Verified original source skeleton changed')
        inputs.append(record(path)); source = json.loads(path.read_bytes())
        bone_ids = {b['name']: i for i, b in enumerate(bones)}
        skins = {s['name']: s for s in source['skins']}
        constraints = source['transform']
        if max(c['order'] for c in source['ik']) >= min(c['order'] for c in constraints):
            raise ValueError('Verify interleaved source IK/world constraint scheduling')
        for profession in (100, 200):
            gender = 'M' if sex == 'male' else 'F'
            selected = ['default', f'body_{profession}_{gender}_01', 'hair_G_01' if sex == 'male' else 'hair_F_01']
            members = {n for s in selected for n in skins[s].get('transform', [])}
            active = {i for i, b in enumerate(bones) if not b['skinRequired']}
            for s in selected:
                for n in skins[s].get('bones', []):
                    i = bone_ids[n]
                    while i >= 0:
                        active.add(i); i = bones[i]['parent']
            for clip in body['clips']:
                for sample in clip['samples']:
                    local = [dict(p) for p in sample['local']]
                    verified = ik[sex, clip['name'], sample['time']]
                    for r in verified['rotations']: local[r['bone']]['rotation'] = r['value']
                    overrides, states = {}, []
                    def evaluate():
                        return {r['bone']: (complex(r['a'], r['c']), complex(r['b'], r['d']), complex(r['x'], r['y']))
                                for r in world(bones, local, active, overrides)}
                    for c in sorted(constraints, key=lambda c: c['order']):
                        if c.get('skin', False) and c['name'] not in members: continue
                        mixes = [c.get(n, 0) for n in MIXES]
                        keys = source['animations'][clip['name']].get('transform', {}).get(c['name'])
                        if keys:
                            if len(keys) != 1: raise ValueError('Verify nonconstant transform keys')
                            if sample['time'] >= keys[0].get('time', 0):
                                mixes = [keys[0].get(n, 0 if n == 'mixShearY' else 1) for n in MIXES]
                        indices = [bone_ids[n] for n in c['bones']]; target = bone_ids[c['target']]
                        states.append(dict(name=c['name'], order=c['order'], target=target, bones=indices, mixes=mixes))
                        if not any(mixes): continue
                        if c.get('local', False) or c.get('relative', False): raise ValueError('Verify active local/relative transform')
                        if c.get('scaleX', 0) or c.get('scaleY', 0) or c.get('shearY', 0): raise ValueError('Verify source offset')
                        matrices = evaluate(); tx, ty, tp = matrices[target]
                        for i in indices:
                            if i not in active: continue
                            x, y, p = matrices[i]
                            r, mx, my, sx, sy, sh = mixes
                            if r:
                                delta = phase_difference(cmath.phase(x), cmath.phase(tx) + math.radians(c.get('rotation', 0))) * r
                                x *= cmath.rect(1, delta); y *= cmath.rect(1, delta)
                            destination = tp + tx * c.get('x', 0) + ty * c.get('y', 0)
                            p = complex(p.real + (destination.real - p.real) * mx, p.imag + (destination.imag - p.imag) * my)
                            if sx: x *= (abs(x) + (abs(tx) - abs(x)) * sx) / abs(x)
                            if sy: y *= (abs(y) + (abs(ty) - abs(y)) * sy) / abs(y)
                            if sh:
                                current = phase_difference(cmath.phase(x), cmath.phase(y))
                                wanted = phase_difference(cmath.phase(tx), cmath.phase(ty))
                                y *= cmath.rect(1, phase_difference(current, wanted) * sh)
                            overrides[i] = (x, y, p)
                    cases.append(dict(sex=sex, skins=selected, animation=clip['name'], time=sample['time'], states=states,
                                      ik=verified['constraints'], expected=world(bones, local, active, overrides)))
    fixture = CACHE / 'typed/idle-transform-validation.json'
    fixture.write_text(json.dumps(dict(cases=cases), ensure_ascii=False, separators=(',', ':')) + '\n', encoding='utf-8', newline='\n')
    paths = ['unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeIdleTransform.cs',
             'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeBonePose.cs',
             'unity/Assets/Game/Editor/ProfessionUI/ProfessionIdleTransformValidation.cs']
    result = dict(format=1, manifest='871_156598', inputs=inputs, baseline_ik=record(baseline), ik_fixture=record(ik_path),
                  baseline_inheritance=record(inheritance), local_fixture=record(local_path), format_reader=record(wheel),
                  references=['https://esotericsoftware.com/spine-transform-constraints'],
                  format_scope='Pinned independent MIT decoder _read_transform_constraints and _read_animation field omission defaults; no SDK implementation.',
                  fixture=record(fixture), generator=record(Path(__file__)),
                  reference_generator=record(ROOT / 'scripts/prepare_unity_profession_world_inheritance.py'),
                  ik_regression_validation=record(ROOT / 'unity/Assets/Game/Editor/ProfessionUI/ProfessionNativeIdleIkValidation.cs'),
                  outputs=[record(ROOT / p) for p in paths for p in (p, p + '.meta')], cases=len(cases),
                  operation='Bind source setup zero defaults and the constant idle animation six-channel defaults separately; evaluate active absolute world constraints after verified idle IK. Independent double-precision complex rotations/affine interpolation supply references.',
                  limitations=['Active local/relative, scale/shear offsets, reflected rotation offsets, singular scale/direction and nonconstant transform timelines explicitly reject.',
                               'Verified source IK constraints precede every transform constraint; interleaved IK/world scheduling is not generalized.',
                               'No physics constraints, original SDK poses, mixed animation, actor raster, full page or Windows Player verification.'],
                  validated_scope='Pending Unity import/compile and actual Play Mode combined idle IK/world-transform validation.')
    (ROOT / 'assets/reference/profession-ui/idle-transform-source-manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Prepared independent combined idle transforms:', len(cases), 'frames')
    return result


if __name__ == '__main__':
    build()
